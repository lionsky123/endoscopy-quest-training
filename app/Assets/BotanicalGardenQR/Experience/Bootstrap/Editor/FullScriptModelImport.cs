using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Explicit editor command only. Does not change the Visitor entry, publish a room map, or build a player.
    public static class FullScriptModelImport
    {
        const string Source = "Assets/EndoscopyTheme/ImportedModels/Source/";
        const string Prepared = "Assets/EndoscopyTheme/ImportedModels/Prepared";
        static readonly Dictionary<string, string> OfficeBaseColorTextures = new Dictionary<string, string> {
            { "Material #65", "ScreenShot_2026-09-17_165931_432.png" },
            { "Material #35", "白色.png" },
            { "Material #38", "ScreenShot_2026-09-17_163548_276.png" },
            { "Material #66", "ScreenShot_2026-09-11_155536_034.png" },
            { "Material #37", "微信图片_20260910165213_789_58.png" },
            { "Material #39", "金属贴图2.jpg" },
            { "Material #40", "大理石.png" },
            { "34RT3038", "34RT3001.png" },
            { "34RT3014", "34RT3013.png" },
            { "34RT3017", "34RT3020.png" },
            { "34RT3019_1", "34RT3003.png" },
            { "34RT3019_2", "34RT3004.png" },
            { "34RT3019_3", "34RT3002.png" },
            { "34RT3019_4", "34RT3005.png" },
            { "34RT3045", "34RT3007.png" },
            { "34RT3039_1", "34RT3017.png" },
            { "34RT3039_2", "34RT3018.png" },
            { "34RT3040", "34RT3010.png" },
            { "34RT3010", "34RT3008.png" },
            { "34RT3011", "34RT3009.png" },
            { "34RT3006_1", "34RT3014.png" },
            { "34RT3005", "34RT3015.png" },
            { "34RT3033", "34RT3006.png" },
            { "34RT3035", "34RT3016.png" },
            { "34RT3023", "34RT3012.png" },
            { "34RT3022", "34RT3019.png" },
            { "34RT3021", "34RT3011.png" }
        };
        static readonly Dictionary<string, string> ClinicalBaseColorTextures = new Dictionary<string, string> {
            { "Material #2957", "白色.png" },
            { "Material #2960", "微信图片_20260910164711_787_58.png" },
            { "Material #2958", "ScreenShot_2026-09-17_165931_432.png" },
            { "Material #2959", "ScreenShot_2026-09-11_155536_034.png" },
            { "Material #2956", "ScreenShot_2026-09-17_163548_276.png" }
        };
        [Serializable] public sealed class Item
        {
            public string id, source, prefab, status;
            public int meshes, transforms, missingMaterials, untexturedSlots;
            public long triangles;
            public Vector3 sourceCenter, sourceSize, preparedSize;
            public float normalizationScale;
        }
        [Serializable] public sealed class Report { public Item[] models; public string[] doorMeshes; }
        [Serializable] sealed class Node { public string name; public Vector3 center, size; public int triangles; }
        [Serializable] sealed class Nodes { public Node[] nodes; }

        public static void Prepare()
        {
            int code = 1;
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                    throw new InvalidOperationException("Model preparation requires Android target.");
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "-bgqrCaptureOutput");
                if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing output directory.");
                var output = Path.GetFullPath(args[index + 1]);
                Directory.CreateDirectory(output);
                Directory.CreateDirectory(Prepared + "/Materials");
                AssetDatabase.Refresh();
                var items = new List<Item>();
                var models = new[] {
                    ("OfficeRoom", "办公室.FBX", 0f), ("ClinicalRoom", "诊疗室模型.FBX", 0f),
                    ("Computer", "computer.fbx", .55f), ("Desk", "table.fbx", 1.6f),
                    ("Gastroscope", "Gastroscope/Gastroscope.fbx", .65f) };
                if (args.Contains("-bgqrRoomsOnly")) models = models.Take(2).ToArray();
                var generatePreviews = !args.Contains("-bgqrSkipPreview");
                foreach (var data in models)
                {
                    var importer = (ModelImporter)AssetImporter.GetAtPath(Source + data.Item2);
                    if (!importer) throw new InvalidOperationException("ModelImporter missing: " + data.Item2);
                    importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
                    importer.addCollider = false;
                    importer.SaveAndReimport();
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Source + data.Item2);
                    if (!model) throw new InvalidOperationException("FBX failed to load: " + data.Item2);
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    var root = new GameObject(data.Item1);
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    visual.transform.SetParent(root.transform, false);
                    var sourceBounds = BoundsOf(root);
                    if (sourceBounds.size.sqrMagnitude < .000001f) throw new InvalidOperationException("Empty model bounds.");
                    var factor = data.Item3 > 0 ? data.Item3 / Mathf.Max(sourceBounds.size.x, sourceBounds.size.y, sourceBounds.size.z) : 1f;
                    visual.transform.localScale *= factor;
                    var shifted = BoundsOf(root);
                    visual.transform.localPosition -= new Vector3(shifted.center.x, shifted.min.y, shifted.center.z);
                    var filters = root.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh).ToArray();
                    var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                    var item = new Item {
                        id = data.Item1, source = Source + data.Item2, prefab = Prepared + "/" + data.Item1 + ".prefab",
                        meshes = filters.Length, transforms = root.GetComponentsInChildren<Transform>(true).Length,
                        triangles = filters.Sum(m => TriangleCount(m.sharedMesh)), sourceCenter = sourceBounds.center,
                        sourceSize = sourceBounds.size, normalizationScale = factor, preparedSize = BoundsOf(root).size,
                        status = data.Item3 == 0 ? "Imported room candidate; layout, semantic anchors, materials and Quest optimization pending. Not connected to runtime."
                            : "Prepared visual prop; display scale only, not measured device dimensions. No grab or clinical task implied."
                    };
                    var converted = new Dictionary<Material, Material>();
                    Material fallback = null;
                    foreach (var renderer in renderers)
                    {
                        var materials = renderer.sharedMaterials;
                        for (var i = 0; i < materials.Length; i++)
                        {
                            var original = materials[i];
                            if (!original) item.missingMaterials++;
                            var sourceTexture = SourceBaseColorTexture(data.Item1, original);
                            var texture = sourceTexture ? sourceTexture : original ? (original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : original.mainTexture) : null;
                            if (!texture) item.untexturedSlots++;
                            if (original && !sourceTexture && original.shader && original.shader.name.StartsWith("Universal Render Pipeline/")) continue;
                            if (original && converted.TryGetValue(original, out var cached)) { materials[i] = cached; continue; }
                            if (!original && fallback) { materials[i] = fallback; continue; }
                            var replacement = new Material(Resources.Load<Material>("EndoscopyRoom/RoomSurface"));
                            replacement.name = data.Item1 + "-" + converted.Count;
                            // Restore the diffuse map captured in the supplied FBX while retaining its material tint.
                            replacement.color = SourceColor(original);
                            if (texture) replacement.SetTexture("_BaseMap", texture);
                            replacement.enableInstancing = true;
                            var path = Prepared + "/Materials/" + replacement.name + (original ? "" : "-fallback") + ".mat";
                            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                            if (existing) { EditorUtility.CopySerialized(replacement, existing); Object.DestroyImmediate(replacement); replacement = existing; }
                            else AssetDatabase.CreateAsset(replacement, path);
                            if (original) converted.Add(original, replacement); else fallback = replacement;
                            materials[i] = replacement;
                        }
                        renderer.sharedMaterials = materials;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    if (data.Item3 > 0)
                    {
                        var bounds = BoundsOf(root);
                        var collider = root.AddComponent<BoxCollider>();
                        collider.center = bounds.center; collider.size = bounds.size;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, item.prefab);
                    File.WriteAllText(Path.Combine(output, data.Item1 + "-nodes.json"), JsonUtility.ToJson(new Nodes {
                        nodes = filters.Select(f => new Node { name = Hierarchy(f.transform, root.transform),
                            center = f.GetComponent<Renderer>().bounds.center, size = f.GetComponent<Renderer>().bounds.size,
                            triangles = (int)TriangleCount(f.sharedMesh) }).ToArray()
                    }, true));
                    if (generatePreviews)
                    {
                        SavePreview(root, output, data.Item1, false);
                        if (data.Item3 == 0) SavePreview(root, output, data.Item1 + "-cutaway", true);
                    }
                    items.Add(item);
                    Debug.Log("Prepared " + JsonUtility.ToJson(item));
                }
                var doorPaths = AssetDatabase.FindAssets("t:Mesh", new[] { Source + "OfficeInteractions" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
                if (doorPaths.Length != 6 || doorPaths.Any(p => !AssetDatabase.LoadAssetAtPath<Mesh>(p)))
                    throw new InvalidOperationException("Expected six loadable door meshes.");
                AssetDatabase.SaveAssets();
                File.WriteAllText(Path.Combine(output, "unity-import-report.json"), JsonUtility.ToJson(new Report { models = items.ToArray(), doorMeshes = doorPaths }, true));
                code = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            EditorApplication.Exit(code);
        }
        static Texture2D SourceBaseColorTexture(string modelId, Material material)
        {
            if (!material) return null;
            string textureFile;
            string room;
            var mappings = modelId == "OfficeRoom" ? OfficeBaseColorTextures : modelId == "ClinicalRoom" ? ClinicalBaseColorTextures : null;
            if (mappings == null || !mappings.TryGetValue(material.name, out textureFile)) return null;
            room = modelId == "OfficeRoom" ? "Office" : "Clinical";
            var texture = Resources.Load<Texture2D>("FullScriptRooms/" + room + "/SourceTextures/" + Path.GetFileNameWithoutExtension(textureFile));
            if (!texture) throw new InvalidOperationException("Embedded FBX texture missing: " + room + "/" + textureFile);
            return texture;
        }
        static Color SourceColor(Material material)
        {
            if (!material) return new Color(.8f, .82f, .82f);
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return new Color(.8f, .82f, .82f);
        }
        static long TriangleCount(Mesh mesh) { long count = 0; for (var i = 0; i < mesh.subMeshCount; i++) if (mesh.GetTopology(i) == MeshTopology.Triangles) count += mesh.GetIndexCount(i) / 3; return count; }
        static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No renderer in " + root.name);
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        static string Hierarchy(Transform node, Transform root) => node == root ? node.name : Hierarchy(node.parent, root) + "/" + node.name;
        static void SavePreview(GameObject root, string output, string name, bool cutaway)
        {
            var bounds = BoundsOf(root);
            var hidden = new List<Renderer>();
            if (cutaway) foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                if (renderer.bounds.min.y > bounds.min.y + bounds.size.y * .65f) { renderer.enabled = false; hidden.Add(renderer); }
            var cameraObject = new GameObject("PreviewCamera"); var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.17f,.19f,.22f);
            var size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            camera.transform.position = bounds.center + new Vector3(-1, cutaway ? 1.5f : .8f, -1).normalized * size * 2;
            camera.transform.LookAt(bounds.center); camera.orthographic = true; camera.orthographicSize = size * .72f;
            camera.nearClipPlane = .001f; camera.farClipPlane = Mathf.Max(100, size * 6);
            var lightObject = new GameObject("PreviewLight"); var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(45,-35,0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.white * .65f;
            var target = new RenderTexture(1200,900,24); target.Create(); var previous = RenderTexture.active;
            var image = new Texture2D(1200,900,TextureFormat.RGB24,false);
            try {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,1200,900),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(output,name + ".png"),image.EncodeToPNG());
            } finally {
                camera.targetTexture = null; RenderTexture.active = previous; target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(lightObject);
                foreach (var renderer in hidden) renderer.enabled = true;
            }
        }
    }
}
