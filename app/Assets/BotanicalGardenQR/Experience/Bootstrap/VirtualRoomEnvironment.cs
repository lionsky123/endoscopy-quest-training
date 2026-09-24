using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        readonly Color _ambientSky, _ambientEquator, _ambientGround;
        readonly bool _fog;
        Light _sceneDirectionalLight;
        float _originalDirectionalLightIntensity;
        bool _restoreDirectionalLightIntensity;
        VirtualRoomTrackingOrigin _trackingOrigin;
        bool _ownsTracking = true;
        bool _disposed;


        Bounds[] _publishedObstacles;
        internal Vector3? TerminalPosition { get; private set; }
        internal GameObject Root => _root;
        internal VirtualRoomTrackingOrigin TrackingOrigin => _trackingOrigin;
        internal VirtualRoomGuidePath GuidePath { get; private set; }
        public MapFrame Frame { get; private set; }

        VirtualRoomEnvironment(Transform rig, MapDefinition definition)
        {
            _ambientMode = RenderSettings.ambientMode;
            _ambientLight = RenderSettings.ambientLight;
            _ambientSky=RenderSettings.ambientSkyColor;
            _ambientEquator=RenderSettings.ambientEquatorColor;
            _ambientGround=RenderSettings.ambientGroundColor;
            _fog = RenderSettings.fog;
            // Model -X is down the long aisle, and is the initial forward view.
            var firstStep=definition.routes[0].samples[1];
            var yaw = rig.eulerAngles.y-Mathf.Atan2(firstStep.x-definition.start.x,firstStep.z-definition.start.z)*Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0, yaw, 0);
            var start = definition.start;
            var origin = rig.position - rotation * new Vector3(start.x, start.y, start.z) * definition.scale;
            Frame = new MapFrame(new MapPosition(origin.x, origin.y, origin.z), yaw, definition.scale);
            _root = new GameObject(definition.roomResource == "EndoscopyRoom" ? "VirtualWashingRoom" : definition.mapId);
            _root.transform.SetPositionAndRotation(origin, rotation);
            _root.transform.localScale = Vector3.one * definition.scale;
        }

        public static VirtualRoomEnvironment Create(GameObject xrRig, GameObject mruk, MapDefinition definition, ITrackingOriginTiming timing = null,
            VirtualRoomTrackingOrigin sharedTracking = null, bool trackHead = true)
        {
            ValidateDefinition(definition);
            PrepareRig(xrRig, mruk);
            var room = new VirtualRoomEnvironment(xrRig.transform, definition);
            try
            {
                if (definition.roomResource == "EndoscopyRoom") room.SetCleaningRoomLight(xrRig.scene);
                if (definition.roomResource == FullScriptRoomCatalog.DevelopmentResource || definition.roomResource == FullScriptRoomCatalog.FurnishedResource)
                    FullScriptRoomCatalog.BuildDevelopmentGeometry(room._root, definition.mapId, room._owned, definition.roomResource==FullScriptRoomCatalog.FurnishedResource);
                else if(definition.roomResource == FullScriptRoomCatalog.LobbyPanorama) room.LoadLobby();
                else if(definition.roomResource == FullScriptRoomCatalog.WaitingRoom || definition.roomResource == FullScriptRoomCatalog.StorageRoom)
                {
                    var prefab=Resources.Load<GameObject>(definition.roomResource);
                    if(!prefab)throw new InvalidOperationException("Published supplied-architecture room is missing: "+definition.roomResource);
                    UnityEngine.Object.Instantiate(prefab,room._root.transform,false);
                }
                else room.Load(definition.roomResource, definition.modelDigest);
                FinishRoom(room, xrRig, definition, timing, sharedTracking, trackHead);
                return room;
            }
            catch { room.Dispose(); throw; }
        }

        internal static BuildOperation BeginBuild(GameObject xrRig, GameObject mruk, MapDefinition definition,
            VirtualRoomTrackingOrigin sharedTracking = null, bool trackHead = true)
            => new BuildOperation(xrRig, mruk, definition, sharedTracking, trackHead);

        static void ValidateDefinition(MapDefinition definition)
        {
            MapDefinitionValidation.Validate(definition);
            if (string.IsNullOrWhiteSpace(definition.roomResource))
                throw new InvalidOperationException("VR requires a published room resource.");
        }

        static void PrepareRig(GameObject xrRig, GameObject mruk)
        {
            if (mruk != null) mruk.SetActive(false);
            var manager = xrRig.GetComponentInChildren<OVRManager>(true);
            if (manager != null) manager.isInsightPassthroughEnabled = false;
            foreach (var layer in xrRig.scene.GetRootGameObjects())
                foreach (var passthrough in layer.GetComponentsInChildren<OVRPassthroughLayer>(true))
                    passthrough.enabled = false;
            foreach (var camera in xrRig.GetComponentsInChildren<Camera>(true))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.17f, .16f, .22f, 1f);
                // Keep close observation possible without changing the tracked head pose.
                camera.nearClipPlane = Mathf.Min(camera.nearClipPlane, .03f);
            }
        }

        static void FinishRoom(VirtualRoomEnvironment room, GameObject xrRig, MapDefinition definition,
            ITrackingOriginTiming timing, VirtualRoomTrackingOrigin sharedTracking, bool trackHead)
        {
            room.GuidePath = new VirtualRoomGuidePath(definition, room.Frame,
                room._root.GetComponentsInChildren<MeshFilter>(), room._publishedObstacles);
            var cameraRig = xrRig.GetComponentInChildren<OVRCameraRig>(true);
            var manager = xrRig.GetComponentInChildren<OVRManager>(true);
            if (sharedTracking != null)
            {
                room._trackingOrigin = sharedTracking;
                room._ownsTracking = false;
            }
            else if (cameraRig && trackHead)
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
            if (definition.roomResource == FullScriptRoomCatalog.StorageRoom ||
                definition.roomResource == FullScriptRoomCatalog.WaitingRoom)
                room.LightSuppliedArchitecture();
        }

        internal sealed class BuildOperation : IDisposable
        {
            enum Phase { AwaitAssets, AwaitTextures, BuildGeometry, Finish, Complete }

            readonly GameObject _xrRig;
            readonly MapDefinition _definition;
            readonly VirtualRoomTrackingOrigin _sharedTracking;
            readonly bool _trackHead;
            readonly ResourceRequest _roomAsset, _geometry, _manifest, _template;
            readonly Dictionary<string, ResourceRequest> _textureRequests = new Dictionary<string, ResourceRequest>();
            Dictionary<string, Texture2D> _textures;
            System.Collections.Generic.IEnumerator<object> _geometrySteps;
            VirtualRoomEnvironment _room;
            Phase _phase;
            bool _disposed;

            internal bool IsComplete => _phase == Phase.Complete;
            internal string PhaseName => _phase.ToString();

            internal BuildOperation(GameObject xrRig, GameObject mruk, MapDefinition definition,
                VirtualRoomTrackingOrigin sharedTracking, bool trackHead)
            {
                ValidateDefinition(definition);
                PrepareRig(xrRig, mruk);
                _xrRig = xrRig;
                _definition = definition;
                _sharedTracking = sharedTracking;
                _trackHead = trackHead;
                _room = new VirtualRoomEnvironment(xrRig.transform, definition);
                try
                {
                    if (definition.roomResource == "EndoscopyRoom") _room.SetCleaningRoomLight(xrRig.scene);
                    if (definition.roomResource == FullScriptRoomCatalog.DevelopmentResource ||
                        definition.roomResource == FullScriptRoomCatalog.FurnishedResource)
                    {
                        _phase = Phase.Finish;
                        FullScriptRoomCatalog.BuildDevelopmentGeometry(_room._root, definition.mapId, _room._owned,
                            definition.roomResource == FullScriptRoomCatalog.FurnishedResource);
                    }
                    else if (definition.roomResource == FullScriptRoomCatalog.LobbyPanorama ||
                        definition.roomResource == FullScriptRoomCatalog.WaitingRoom ||
                        definition.roomResource == FullScriptRoomCatalog.StorageRoom)
                        _roomAsset = Resources.LoadAsync<GameObject>(definition.roomResource);
                    else
                    {
                        _geometry = Resources.LoadAsync<TextAsset>(definition.roomResource + "/geometry");
                        _manifest = Resources.LoadAsync<TextAsset>(definition.roomResource + "/manifest");
                        _template = Resources.LoadAsync<Material>(definition.roomResource + "/RoomSurface");
                    }
                }
                catch { _room.Dispose(); _room = null; throw; }
            }

            internal bool Tick()
            {
                if (_disposed) throw new ObjectDisposedException(nameof(BuildOperation));
                try
                {
                    switch (_phase)
                    {
                        case Phase.AwaitAssets:
                            if (_roomAsset != null)
                            {
                                if (!_roomAsset.isDone) return false;
                                var prefab = _roomAsset.asset as GameObject;
                                if (!prefab) throw new InvalidOperationException("Published room prefab is missing: " + _definition.roomResource);
                                if (_definition.roomResource == FullScriptRoomCatalog.LobbyPanorama) _room.LoadLobby(prefab);
                                else UnityEngine.Object.Instantiate(prefab, _room._root.transform, false);
                                _phase = Phase.Finish;
                                return false;
                            }
                            if (!_geometry.isDone || !_manifest.isDone || !_template.isDone) return false;
                            var manifestAsset = _manifest.asset as TextAsset;
                            if (!_geometry.asset || !manifestAsset || !_template.asset)
                                throw new InvalidOperationException("Published VR room geometry/materials are missing.");
                            var manifest = JsonUtility.FromJson<RoomManifest>(manifestAsset.text);
                            if (manifest?.materials == null) throw new InvalidDataException("Room material manifest is invalid.");
                            foreach (var material in manifest.materials)
                            {
                                var path = TexturePath(_definition.roomResource, material);
                                if (path != null && !_textureRequests.ContainsKey(path))
                                    _textureRequests.Add(path, Resources.LoadAsync<Texture2D>(path));
                            }
                            _phase = Phase.AwaitTextures;
                            return false;
                        case Phase.AwaitTextures:
                            if (_textureRequests.Values.Any(request => !request.isDone)) return false;
                            _textures = new Dictionary<string, Texture2D>();
                            foreach (var pair in _textureRequests)
                                _textures.Add(pair.Key, pair.Value.asset as Texture2D);
                            _geometrySteps = _room.LoadSteps(_geometry.asset as TextAsset,
                                _manifest.asset as TextAsset, _template.asset as Material,
                                _definition.roomResource, _definition.modelDigest, _textures).GetEnumerator();
                            _phase = Phase.BuildGeometry;
                            return false;
                        case Phase.BuildGeometry:
                            // A bounded amount of parsing and mesh upload per frame keeps the
                            // compositor supplied while the destination remains covered.
                            for (var step = 0; step < 2; step++)
                                if (!_geometrySteps.MoveNext())
                                {
                                    _geometrySteps.Dispose();
                                    _geometrySteps = null;
                                    _phase = Phase.Finish;
                                    break;
                                }
                            return false;
                        case Phase.Finish:
                            FinishRoom(_room, _xrRig, _definition, null, _sharedTracking, _trackHead);
                            _phase = Phase.Complete;
                            return true;
                        case Phase.Complete:
                            return true;
                        default:
                            throw new InvalidOperationException("Unknown room build phase.");
                    }
                }
                catch { Dispose(); throw; }
            }

            internal VirtualRoomEnvironment TakeRoom()
            {
                if (_disposed || !IsComplete) throw new InvalidOperationException("Room creation has not completed.");
                var result = _room;
                _room = null;
                return result;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _geometrySteps?.Dispose();
                _room?.Dispose();
                _room = null;
            }
        }

        static string TexturePath(string resource, RoomMaterial material)
            => !string.IsNullOrEmpty(material.textureResource) ? material.textureResource :
                !string.IsNullOrEmpty(material.texture) ? resource + "/" + material.texture : null;

        void LightSuppliedArchitecture()
        {
            // These two authored layouts share the supplied office architecture.
            // Four ceiling emitters are a bounded lighting approximation, not a
            // surveyed luminaire plan. No camera-following lights or shadow maps.
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.68f,.68f,.67f);
            RenderSettings.ambientEquatorColor=new Color(.56f,.56f,.55f);
            RenderSettings.ambientGroundColor=new Color(.49f,.49f,.48f);
            var group=new GameObject("RoomCeilingLighting").transform;group.SetParent(_root.transform,false);
            int index=0;
            foreach(float x in new[]{-1.4f,1.4f})foreach(float z in new[]{-1.25f,1.25f})
            {
                var lamp=new GameObject("CeilingEmitter"+(index++),typeof(Light));lamp.transform.SetParent(group,false);
                lamp.transform.localPosition=new Vector3(x,2.70f,z);
                lamp.transform.localRotation=Quaternion.Euler(90,0,0);
                var light=lamp.GetComponent<Light>();light.type=LightType.Spot;
                light.color=new Color(1f,.99f,.97f);light.intensity=1.1f;light.range=4.5f;
                light.spotAngle=140;light.innerSpotAngle=120;light.shadows=LightShadows.None;
                light.renderMode=LightRenderMode.ForcePixel;
            }
        }

        void SetCleaningRoomLight(UnityEngine.SceneManagement.Scene scene)
        {
            _sceneDirectionalLight = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Light>(true))
                .FirstOrDefault(light => light && light.type == LightType.Directional && light.isActiveAndEnabled);
            if (!_sceneDirectionalLight) return;
            _originalDirectionalLightIntensity = _sceneDirectionalLight.intensity;
            _sceneDirectionalLight.intensity = .77f;
            _restoreDirectionalLightIntensity = true;
        }
        void LoadLobby()
        {
            var prefab=Resources.Load<GameObject>(FullScriptRoomCatalog.LobbyPanorama);
            if(!prefab)throw new InvalidOperationException("Published lobby panorama is missing.");
            LoadLobby(prefab);
        }
        void LoadLobby(GameObject prefab)
        {
            var panorama=UnityEngine.Object.Instantiate(prefab,_root.transform,false);
            // A panoramic display shell is not physical architecture or an obstacle.
            _publishedObstacles=Array.Empty<Bounds>();
            var view=InspectionViewConfiguration.Load()?.Find("R00_LOBBY")?.initial;
            panorama.transform.localRotation=Quaternion.Euler(0,view?.yaw??0,0);
            panorama.SetActive(true);
        }
        internal void AlignStationaryView(MapDefinition map, Transform viewer, float floor, MapPosition? target = null, Vector3? localForward = null)
        {
            // Called only while covered by the transition curtain, once per visit.
            // Actual head/hand poses and their tracking scale are never modified.
            if(localForward.HasValue)
            {
                var facing=Vector3.ProjectOnPlane(viewer.forward,Vector3.up);
                if(facing.sqrMagnitude<.01f)facing=viewer.root.forward;
                var direction=localForward.Value;
                var yaw=Mathf.Atan2(facing.x,facing.z)*Mathf.Rad2Deg-Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
                _root.transform.rotation=Quaternion.Euler(0,yaw,0);
                var position=_root.transform.position;
                Frame=new MapFrame(new MapPosition(position.x,position.y,position.z),yaw,Frame.Scale);
            }
            var point=Frame.Transform(target ?? map.points[0].position);
            var delta=new Vector3(viewer.position.x-point.x, floor-_root.transform.position.y, viewer.position.z-point.z);
            _root.transform.position+=delta;
            var origin=_root.transform.position;
            Frame=new MapFrame(new MapPosition(origin.x,origin.y,origin.z),Frame.YawDegrees,Frame.Scale);
            GuidePath=new VirtualRoomGuidePath(map,Frame,_root.GetComponentsInChildren<MeshFilter>(),_publishedObstacles);
        }

        void Load(string resource, string expectedDigest)
        {
            var geometry = Resources.Load<TextAsset>(resource + "/geometry");
            var manifestAsset = Resources.Load<TextAsset>(resource + "/manifest");
            var template = Resources.Load<Material>(resource + "/RoomSurface");
            foreach (var step in LoadSteps(geometry, manifestAsset, template, resource, expectedDigest, null)) { }
        }

        IEnumerable<object> LoadSteps(TextAsset geometry, TextAsset manifestAsset, Material template,
            string resource, string expectedDigest, Dictionary<string, Texture2D> stagedTextures)
        {
            if (geometry == null || manifestAsset == null || template == null)
                throw new InvalidOperationException("Published VR room geometry/materials are missing.");
            var geometryBytes = geometry.bytes;
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                const int hashChunk = 4 * 1024 * 1024;
                for (var offset = 0; offset < geometryBytes.Length; offset += hashChunk)
                {
                    var chunkLength = Math.Min(hashChunk, geometryBytes.Length - offset);
                    hash.TransformBlock(geometryBytes, offset, chunkLength, geometryBytes, offset);
                    yield return null;
                }
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var digest = BitConverter.ToString(hash.Hash).Replace("-", "");
                if (!string.Equals(digest, expectedDigest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The published fairy routes do not match the room model. Republish the room map.");
            }
            var manifest = JsonUtility.FromJson<RoomManifest>(manifestAsset.text);
            if (manifest?.materials == null) throw new InvalidDataException("Room material manifest is invalid.");
            if(manifest.obstacles!=null)
            {
                _publishedObstacles=Array.ConvertAll(manifest.obstacles,o=>new Bounds(o.center,o.size));
                TerminalPosition=manifest.terminal;
            }
            var materials = new Material[manifest.materials.Length];
            for (var i = 0; i < materials.Length; i++)
            {
                var source = manifest.materials[i];
                var material = new Material(template) { name = source.name };
                _owned.Add(material);
                material.color = new Color(source.color[0], source.color[1], source.color[2], 1);
                var texturePath = TexturePath(resource, source);
                if (texturePath != null)
                {
                    Texture2D texture;
                    if (stagedTextures != null) stagedTextures.TryGetValue(texturePath, out texture);
                    else texture = Resources.Load<Texture2D>(texturePath);
                    if (texture == null) throw new InvalidOperationException("Missing room texture: " + source.texture);
                    material.mainTexture = texture;
                    // The imported diffuse tint multiplies the texture. Replacing it with
                    // white overexposes the room's pale walls, ceiling and floor.
                }
                material.SetFloat("_Smoothness", .25f);
                materials[i] = material;
                yield return null;
            }
            using var reader = new BinaryReader(new MemoryStream(geometryBytes));
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
                var rawVertexBytes = reader.ReadBytes(vertexCount * 32);
                var rawFloats = new float[vertexCount * 8];
                Buffer.BlockCopy(rawVertexBytes, 0, rawFloats, 0, rawVertexBytes.Length);
                for (var i = 0; i < vertexCount; i++)
                {
                    var f = i * 8;
                    // Same 180-degree normalization used by the original room inspection.
                    vertices[i] = new Vector3(-rawFloats[f], rawFloats[f + 1], -rawFloats[f + 2]);
                    normals[i] = new Vector3(-rawFloats[f + 3], rawFloats[f + 4], -rawFloats[f + 5]);
                    uv[i] = new Vector2(rawFloats[f + 6], rawFloats[f + 7]);
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
                    var indexCount = reader.ReadInt32();
                    var indices = new int[indexCount];
                    var rawIndexBytes = reader.ReadBytes(indexCount * 4);
                    Buffer.BlockCopy(rawIndexBytes, 0, indices, 0, rawIndexBytes.Length);
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
                if(_publishedObstacles==null)item.AddComponent<MeshCollider>().sharedMesh = mesh;
                yield return null;
            }
            if (reader.BaseStream.Position != reader.BaseStream.Length)
                throw new InvalidDataException("Unexpected trailing room geometry data.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_ownsTracking) _trackingOrigin?.Dispose();
            if (_restoreDirectionalLightIntensity && _sceneDirectionalLight)
                _sceneDirectionalLight.intensity = _originalDirectionalLightIntensity;
            if (_root) _root.SetActive(false);
            Destroy(_root);
            foreach (var item in _owned) Destroy(item);
            _owned.Clear();
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientLight = _ambientLight;
            RenderSettings.ambientSkyColor=_ambientSky;
            RenderSettings.ambientEquatorColor=_ambientEquator;
            RenderSettings.ambientGroundColor=_ambientGround;
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

        [Serializable] sealed class RoomManifest { public RoomMaterial[] materials; public RoomObstacle[] obstacles; public Vector3 terminal; }
        [Serializable] sealed class RoomObstacle { public Vector3 center,size; }
        [Serializable] sealed class RoomMaterial { public string name, texture,textureResource; public float[] color; }
    }
}
