using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BotanicalGardenQR.MapNavigation.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // A persistent, room-only editing scene. It is deliberately not a player build scene.
    public static class ClinicalRoomWorkspace
    {
        const string Folder = "Assets/EndoscopyTheme/RoomWorkspace";
        const string ScenePath = Folder + "/WashingRoom.unity";
        const string PrefabPath = Folder + "/WashingRoom.prefab";
        const string MapPath = "Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json";
        const string VisitorPath = "Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity";
        const string RootName = "清洗消毒室";

        [MenuItem("Endoscopy/房间模型/打开房间编辑场景", false, 1)]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("房间模型", "请先停止 Play，再打开房间编辑场景。", "知道了");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else Create();
            InteriorView();
        }

        static void Create()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/EndoscopyTheme", "RoomWorkspace");
            // Use an isolated temporary scene, so production objects are never edited by the loader.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) prefab = BakeRoom();
            var room = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            room.name = RootName;

            var lighting = new GameObject("查看用灯光");
            var light = lighting.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = .65f;
            light.shadows = LightShadows.None;
            lighting.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.72f, .76f, .80f);
            RenderSettings.fog = false;

            var viewer = new GameObject("室内查看相机");
            var camera = viewer.AddComponent<Camera>();
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 100;
            camera.fieldOfView = 75;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.16f, .20f, .24f);
            var definition = ReadMap();
            viewer.transform.position = new Vector3(definition.start.x, 1.6f, definition.start.z);
            viewer.transform.rotation = Quaternion.LookRotation(new Vector3(-1, -.08f, .12f));

            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save room workspace.");
            Debug.Log($"Room workspace ready: {ScenePath}; {room.GetComponentsInChildren<MeshFilter>().Length} meshes. Preview edits do not change the published Quest map.");
        }

        static GameObject BakeRoom()
        {
            var definition = ReadMap();
            var rig = new GameObject("Temporary room export frame");
            rig.transform.rotation = Quaternion.Euler(0, -90, 0);
            rig.transform.position = new Vector3(definition.start.x, definition.start.y, definition.start.z) * definition.scale;
            GameObject copy = null;
            // A unique library also keeps any interrupted previous export intact.
            var library = AssetDatabase.GenerateUniqueAssetPath(Folder + "/RoomGeometry.asset");
            var hasLibrary = false;
            try
            {
                using (VirtualRoomEnvironment.Create(rig, null, definition))
                {
                    var source = rig.scene.GetRootGameObjects().Single(o => o.name == "VirtualWashingRoom");
                    copy = Object.Instantiate(source);
                    copy.name = RootName;
                    copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    // Station anchors belong to the teaching map, not to this room-only workspace.
                    foreach (Transform child in copy.transform.Cast<Transform>().ToArray())
                        if (child.name.StartsWith("Station_", StringComparison.Ordinal)) Object.DestroyImmediate(child.gameObject);

                    var meshes = new Dictionary<Mesh, Mesh>();
                    var materials = new Dictionary<Material, Material>();
                    foreach (var filter in copy.GetComponentsInChildren<MeshFilter>())
                    {
                        var original = filter.sharedMesh;
                        if (!meshes.TryGetValue(original, out var mesh))
                        {
                            mesh = Object.Instantiate(original);
                            mesh.name = original.name;
                            if (!hasLibrary) { AssetDatabase.CreateAsset(mesh, library); hasLibrary = true; }
                            else AssetDatabase.AddObjectToAsset(mesh, library);
                            meshes.Add(original, mesh);
                        }
                        filter.sharedMesh = mesh;
                        var collider = filter.GetComponent<MeshCollider>();
                        if (collider) collider.sharedMesh = mesh;
                        var renderer = filter.GetComponent<MeshRenderer>();
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(originalMaterial =>
                        {
                            if (!materials.TryGetValue(originalMaterial, out var material))
                            {
                                material = Object.Instantiate(originalMaterial);
                                material.name = originalMaterial.name;
                                AssetDatabase.AddObjectToAsset(material, library);
                                materials.Add(originalMaterial, material);
                            }
                            return material;
                        }).ToArray();
                    }
                    AssetDatabase.SaveAssets();
                    var result = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath, out var success);
                    if (!success || !result) throw new IOException("Could not save room prefab.");
                    return result;
                }
            }
            finally
            {
                if (copy) Object.DestroyImmediate(copy);
                Object.DestroyImmediate(rig);
            }
        }

        [MenuItem("Endoscopy/房间模型/回到室内视角", false, 2)]
        public static void InteriorView()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath) return;
            var camera = GameObject.Find("室内查看相机");
            if (!camera) return;
            Selection.activeGameObject = null;
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.in2DMode = false;
            view.orthographic = false;
            view.drawGizmos = false;
            view.sceneLighting = true;
            view.AlignViewToObject(camera.transform);
            view.Focus();
            view.Repaint();
        }

        [MenuItem("Endoscopy/房间模型/选中整个房间", false, 3)]
        public static void SelectRoom()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath) return;
            Selection.activeGameObject = GameObject.Find(RootName);
        }

        [MenuItem("Endoscopy/房间模型/返回主体验场景", false, 20)]
        public static void OpenVisitor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(VisitorPath);
        }

        static MapDefinition ReadMap() => JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>(MapPath).text);
    }
}
