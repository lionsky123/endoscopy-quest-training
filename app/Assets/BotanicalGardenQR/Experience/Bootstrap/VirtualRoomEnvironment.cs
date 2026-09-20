using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.Panorama.Contracts;
using UnityEngine;
using UnityEngine.Rendering;

namespace BotanicalGardenQR.Bootstrap
{
    /// <summary>One immutable room frame for architecture, station anchors and navigation.
    /// Does not move the XR rig or require any scanned real-world geometry.</summary>
    internal sealed class VirtualRoomEnvironment : IDisposable, IPanoramaStateSink
    {
        readonly GameObject _root;
        readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        readonly AmbientMode _ambientMode;
        readonly Color _ambientLight;
        readonly bool _fog;
        VirtualRoomTrackingOrigin _trackingOrigin;
        internal VirtualRoomTrackingOrigin TrackingOrigin => _trackingOrigin;
        internal VirtualRoomGuidePath GuidePath { get; private set; }
        public MapFrame Frame { get; }

        VirtualRoomEnvironment(Transform rig, MapDefinition definition)
        {
            _ambientMode = RenderSettings.ambientMode;
            _ambientLight = RenderSettings.ambientLight;
            _fog = RenderSettings.fog;
            // Model -X is down the long aisle, and is the initial forward view.
            var yaw = rig.eulerAngles.y + 90f;
            var rotation = Quaternion.Euler(0, yaw, 0);
            var start = definition.start;
            var origin = rig.position - rotation * new Vector3(start.x, start.y, start.z) * definition.scale;
            Frame = new MapFrame(new MapPosition(origin.x, origin.y, origin.z), yaw, definition.scale);
            _root = new GameObject("VirtualWashingRoom");
            _root.transform.SetPositionAndRotation(origin, rotation);
            _root.transform.localScale = Vector3.one * definition.scale;
        }

        public static VirtualRoomEnvironment Create(GameObject xrRig, GameObject mruk, MapDefinition definition, ITrackingOriginTiming timing = null)
        {
            MapDefinitionValidation.Validate(definition);
            if (string.IsNullOrWhiteSpace(definition.roomResource))
                throw new InvalidOperationException("VR requires a published room resource.");
            if (mruk != null) mruk.SetActive(false);
            var manager = xrRig.GetComponentInChildren<OVRManager>(true);
            if (manager != null) manager.isInsightPassthroughEnabled = false;
            foreach (var layer in xrRig.scene.GetRootGameObjects())
                foreach (var passthrough in layer.GetComponentsInChildren<OVRPassthroughLayer>(true))
                    passthrough.enabled = false;
            foreach (var camera in xrRig.GetComponentsInChildren<Camera>(true))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.16f, .20f, .24f, 1f);
                // MR's 10–30 cm near plane visibly slices walls during close inspection.
                // Keep smaller authored values and never compensate by moving the XR rig.
                camera.nearClipPlane = Mathf.Min(camera.nearClipPlane, .03f);
            }
            var room = new VirtualRoomEnvironment(xrRig.transform, definition);
            try
            {
                room.Load(definition.roomResource, definition.modelDigest);
                room.GuidePath = new VirtualRoomGuidePath(definition, room.Frame, room._root.GetComponentsInChildren<MeshFilter>());
                var cameraRig = xrRig.GetComponentInChildren<OVRCameraRig>(true);
                if (cameraRig)
                {
                    var start = room.Frame.Transform(definition.start);
                    var next = room.Frame.Transform(definition.routes[0].samples[1]);
                    var spawn = new Pose(new Vector3(start.x, start.y, start.z),
                        Quaternion.LookRotation(new Vector3(next.x - start.x, 0, next.z - start.z)));
                    room._trackingOrigin = new VirtualRoomTrackingOrigin(cameraRig, manager, timing, spawn);
                }
                foreach (var point in definition.points)
                {
                    var anchor = new GameObject("Station_" + point.id).transform;
                    anchor.SetParent(room._root.transform, false);
                    anchor.localPosition = new Vector3(point.position.x, point.position.y, point.position.z);
                }
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.72f, .76f, .80f);
                RenderSettings.fog = false;
                return room;
            }
            catch { room.Dispose(); throw; }
        }

        void Load(string resource, string expectedDigest)
        {
            var geometry = Resources.Load<TextAsset>(resource + "/geometry");
            var manifestAsset = Resources.Load<TextAsset>(resource + "/manifest");
            var template = Resources.Load<Material>(resource + "/RoomSurface");
            if (geometry == null || manifestAsset == null || template == null)
                throw new InvalidOperationException("Published VR room geometry/materials are missing.");
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                var digest = BitConverter.ToString(hash.ComputeHash(geometry.bytes)).Replace("-", "");
                if (!string.Equals(digest, expectedDigest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The published fairy routes do not match the room model. Republish the room map.");
            }
            var manifest = JsonUtility.FromJson<RoomManifest>(manifestAsset.text);
            var materials = new Material[manifest.materials.Length];
            for (var i = 0; i < materials.Length; i++)
            {
                var source = manifest.materials[i];
                var material = new Material(template) { name = source.name };
                _owned.Add(material);
                material.color = new Color(source.color[0], source.color[1], source.color[2], 1);
                if (!string.IsNullOrEmpty(source.texture))
                {
                    var texture = Resources.Load<Texture2D>(resource + "/" + source.texture);
                    if (texture == null) throw new InvalidOperationException("Missing room texture: " + source.texture);
                    material.mainTexture = texture;
                    // The imported diffuse tint multiplies the texture. Replacing it with
                    // white overexposes the room's pale walls, ceiling and floor.
                }
                material.SetFloat("_Smoothness", .25f);
                materials[i] = material;
            }
            using var reader = new BinaryReader(new MemoryStream(geometry.bytes));
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "ECR1")
                throw new InvalidDataException("Unsupported room geometry format.");
            var count = reader.ReadInt32();
            for (var n = 0; n < count; n++)
            {
                var name = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
                var vertexCount = reader.ReadInt32();
                var vertices = new Vector3[vertexCount];
                var normals = new Vector3[vertexCount];
                var uv = new Vector2[vertexCount];
                for (var i = 0; i < vertexCount; i++)
                {
                    // Same 180-degree normalization used by the original room inspection.
                    vertices[i] = new Vector3(-reader.ReadSingle(), reader.ReadSingle(), -reader.ReadSingle());
                    normals[i] = new Vector3(-reader.ReadSingle(), reader.ReadSingle(), -reader.ReadSingle());
                    uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                }
                var mesh = new Mesh { name = name, indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                _owned.Add(mesh);
                mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv;
                var subCount = reader.ReadInt32();
                mesh.subMeshCount = subCount;
                var assigned = new Material[subCount];
                for (var sub = 0; sub < subCount; sub++)
                {
                    assigned[sub] = materials[reader.ReadInt32()];
                    var indices = new int[reader.ReadInt32()];
                    for (var i = 0; i < indices.Length; i++) indices[i] = reader.ReadInt32();
                    mesh.SetTriangles(indices, sub);
                }
                mesh.RecalculateBounds();
                var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                item.transform.SetParent(_root.transform, false);
                item.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = item.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = assigned;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                // Virtual collider checks are used for guide recovery, never to push the player.
                item.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            if (reader.BaseStream.Position != reader.BaseStream.Length)
                throw new InvalidDataException("Unexpected trailing room geometry data.");
        }

        public void Dispose()
        {
            _trackingOrigin?.Dispose();
            Destroy(_root);
            foreach (var item in _owned) Destroy(item);
            _owned.Clear();
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientLight = _ambientLight;
            RenderSettings.fog = _fog;
        }

        public void Publish(PanoramaState state)
        {
            // Full-screen lesson panoramas temporarily replace the virtual architecture.
            // Returning restores the exact same room frame and physical visitor position.
            if (_root != null) _root.SetActive(state.Phase != PanoramaPhase.Ready && state.Phase != PanoramaPhase.Active);
        }

        static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        [Serializable] sealed class RoomManifest { public RoomMaterial[] materials; }
        [Serializable] sealed class RoomMaterial { public string name, texture; public float[] color; }
    }
}
