using BotanicalGardenQR.Configuration.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.VisitorAtlasHub.Backend;
using BotanicalGardenQR.VisitorAtlasHub.Frontend;
using NUnit.Framework;
using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.VisitorAtlasHub
{
    public sealed class VisitorAtlasHubAssetContractTests
    {
        const string PrefabPath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/VisitorAtlasHubPresentation.prefab";
        const string VisitorRuntimePath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab";
        const string StreamingAssetsPath = "VisitorAtlasHub/basement_map_8x8_6points.glb";
        const string MenuAssetRoot =
            "Assets/BotanicalGardenQR/Content/Shared/VisitorAtlasHub/MenuModels/RpgItemCollection2";
        const string BookMaterialPath = MenuAssetRoot + "/AtlasMenuBook.mat";
        const string ScrollMaterialPath = MenuAssetRoot + "/AtlasMenuScroll.mat";

        [TestCase("VisualRoot/CloseRoot")]
        public void PanelActionsHaveGazeFeedbackWithoutPhysicalPokeTargets(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var root = prefab.transform.Find(path);
            var button = root.GetComponentInChildren<Button>(true);
            Assert.That(button, Is.Not.Null);
            Assert.That(button.GetComponentInParent<Canvas>(true).renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(button.GetComponentInParent<GraphicRaycaster>(true), Is.Not.Null);
            Assert.That(button.transform.Find("GazeProgress/Fill"), Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<VisitorAtlasHubPointableRelay>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(button.GetComponentInChildren<TMP_Text>(true).text, Is.Not.Empty);
        }

        [Test]
        public void ProductionPrefabKeepsTwoPhysicalEntriesAndGazePanelActionsWithHorizontalMap()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);

            var presentation = prefab.GetComponent<VisitorAtlasHubPresentation>();
            Assert.That(presentation, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<AtlasHubPalmCandidateSource>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<VisitorAtlasHubPointableRelay>(true), Has.Length.EqualTo(2));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(2),
                "Only the book and scroll have physical hit volumes; map markers remain display-only.");
            var visualRoot = prefab.transform.Find("VisualRoot");
            var choiceRoot = prefab.transform.Find("VisualRoot/EntryChoices");
            var mapSurface = prefab.transform.Find("VisualRoot/MapSurface");
            var bookRoot = prefab.transform.Find("VisualRoot/EntryChoices/BookRoot");
            var mapEntryRoot = prefab.transform.Find("VisualRoot/EntryChoices/MapEntryRoot");
            var bookVisual = prefab.transform.Find(
                "VisualRoot/EntryChoices/BookRoot/BookActivationPivot/BookVisual");
            var mapScrollVisual = prefab.transform.Find(
                "VisualRoot/EntryChoices/MapEntryRoot/MapScrollVisual");
            var entryLabels = prefab.transform.Find("VisualRoot/EntryChoices/EntryLabels");
            var closeRoot = prefab.transform.Find("VisualRoot/CloseRoot");
            Assert.That(visualRoot, Is.Not.Null);
            Assert.That(visualRoot.gameObject.activeSelf, Is.False,
                "Startup must preload behind one inactive visual root.");
            Assert.That(choiceRoot, Is.Not.Null);
            Assert.That(choiceRoot.gameObject.activeSelf, Is.True);
            Assert.That(choiceRoot.GetComponentsInChildren<Renderer>(true), Has.Length.EqualTo(2),
                "There is no third menu panel: the physical book and scroll are the two choices.");
            Assert.That(mapSurface, Is.Not.Null);
            Assert.That(mapSurface.gameObject.activeSelf, Is.False,
                "Palm summon must start at the physical menu, not expose the full map.");
            Assert.That(bookRoot, Is.Not.Null);
            Assert.That(mapEntryRoot, Is.Not.Null);
            Assert.That(bookVisual, Is.Not.Null);
            Assert.That(mapScrollVisual, Is.Not.Null);
            Assert.That(entryLabels, Is.Not.Null);
            Assert.That(closeRoot, Is.Not.Null);
            Assert.That(bookRoot.localPosition, Is.EqualTo(new Vector3(-0.39f, 0f, 0f)));
            Assert.That(mapEntryRoot.localPosition, Is.EqualTo(new Vector3(0.39f, 0.015f, 0f)));
            Assert.That(Vector3.Dot(bookVisual.localRotation * Vector3.forward, Vector3.back),
                Is.GreaterThan(0.999f), "The imported book display face must point toward the viewer.");
            Assert.That(Vector3.Dot(mapScrollVisual.localRotation * Vector3.forward, Vector3.back),
                Is.GreaterThan(0.999f), "The imported scroll display face must point toward the viewer.");
            Assert.That(entryLabels.GetComponent<Canvas>()?.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(entryLabels.GetComponent<GraphicRaycaster>(), Is.Null,
                "Entry labels distinguish the objects but must not create another input path.");
            Assert.That(
                entryLabels.GetComponentsInChildren<TMP_Text>(true).Select(value => value.text),
                Is.EquivalentTo(new[] { "植物图鉴", "打开地图" }));
            Assert.That(mapSurface.localPosition, Is.EqualTo(Vector3.zero),
                "The map remains at the one committed authored pose; only the entry choices move.");
            Assert.That(closeRoot.localPosition, Is.EqualTo(new Vector3(0.62f, 0.79f, 0.78f)),
                "The authored close pose belongs to the fixed map and is restored after leaving entry choices.");
            Assert.That(prefab.transform.Find("VisualRoot/EntryChoices/BookRoot/BookPokeTarget"), Is.Not.Null);
            Assert.That(prefab.transform.Find("VisualRoot/EntryChoices/MapEntryRoot/MapPokeTarget"), Is.Not.Null);
            Assert.That(prefab.transform.Find("VisualRoot/CloseRoot/PanelCanvas/Action"), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Animator>(true), Is.Empty,
                "The bounded project-authored interpolation must not leave a hidden Animator ticking.");

            var bookMaterials = bookRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Select(renderer => AssetDatabase.GetAssetPath(renderer.sharedMaterial))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Assert.That(bookMaterials, Is.EqualTo(new[] { BookMaterialPath }));
            var mapEntryMaterials = mapEntryRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Select(renderer => AssetDatabase.GetAssetPath(renderer.sharedMaterial))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Assert.That(mapEntryMaterials, Is.EqualTo(new[] { ScrollMaterialPath }));

            var serialized = new SerializedObject(presentation);
            Assert.That(serialized.FindProperty("_visualRoot").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("_choiceRoot").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("_mapSurfaceRoot").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("_mapContentRoot").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("_hideRoot").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("_entryForwardDistance").floatValue, Is.EqualTo(0.68f).Within(0.0001f));
            Assert.That(serialized.FindProperty("_entryVerticalOffset").floatValue, Is.EqualTo(-0.48f).Within(0.0001f));
            Assert.That(serialized.FindProperty("_entryHideLocalPosition").vector3Value,
                Is.EqualTo(new Vector3(0f, -0.28f, 0.02f)));
            Assert.That(serialized.FindProperty("_streamingAssetsPath").stringValue, Is.EqualTo(StreamingAssetsPath));
            Assert.That(VisitorMapConfiguration.Resolve(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json")).scale * Vector3.one, Is.EqualTo(Vector3.one * 2f));
            Assert.That(serialized.FindProperty("_palmHoldSeconds").floatValue, Is.EqualTo(0.45f).Within(0.0001f));
            var palm = prefab.GetComponent<AtlasHubPalmCandidateSource>();
            Assert.That(palm.MinimumPalmUpAlignment, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(presentation.Configuration.RevealSeconds, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(presentation.Configuration.BookOpenSeconds, Is.EqualTo(0.7f).Within(0.0001f));
        }

        [Test]
        public void EntryChoicePoseUsesCurrentViewerYawAndIgnoresPitch()
        {
            var first = VisitorAtlasHubPresentation.CalculateEntryChoicePose(
                new Pose(new Vector3(1f, 1.6f, 2f), Quaternion.Euler(35f, 90f, 0f)),
                0.68f,
                -0.48f,
                Vector3.forward);
            var moved = VisitorAtlasHubPresentation.CalculateEntryChoicePose(
                new Pose(new Vector3(4f, 1.7f, -3f), Quaternion.Euler(-20f, 180f, 0f)),
                0.68f,
                -0.48f,
                Vector3.forward);

            Assert.That(first.position.x, Is.EqualTo(1.68f).Within(0.0001f));
            Assert.That(first.position.y, Is.EqualTo(1.12f).Within(0.0001f));
            Assert.That(first.position.z, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(moved.position.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(moved.position.y, Is.EqualTo(1.22f).Within(0.0001f));
            Assert.That(moved.position.z, Is.EqualTo(-3.68f).Within(0.0001f));
            Assert.That(Vector3.Dot(first.rotation * Vector3.forward, Vector3.right),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(Vector3.Dot(moved.rotation * Vector3.forward, Vector3.back),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void VisitorRuntimeReferencesTheUniqueAtlasHubPrefab()
        {
            var runtime = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorRuntimePath);
            var expected = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(runtime, Is.Not.Null, VisitorRuntimePath);
            Assert.That(expected, Is.Not.Null, PrefabPath);
            var installer = runtime.GetComponent<VisitorInstaller>();
            Assert.That(installer, Is.Not.Null);
            var serialized = new SerializedObject(installer);
            Assert.That(
                serialized.FindProperty("_atlasHubPresentationPrefab").objectReferenceValue,
                Is.SameAs(expected));
        }

        [Test]
        public void MarkerGlbContainsSixDisplayPointsAndApproximatelyEightByEightHorizontalBounds()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null);
            var path = Path.Combine(projectRoot, "Assets", "StreamingAssets", StreamingAssetsPath);
            Assert.That(File.Exists(path), Is.True, path);

            var gltf = ReadGltfJson(path);
            Assert.That(gltf.asset.upAxis, Is.Null,
                "glTF uses the specification's Y-up convention without a custom axis override.");
            Assert.That(
                gltf.nodes.Count(node => node.name != null && node.name.StartsWith("MapPoint_P", StringComparison.Ordinal)),
                Is.EqualTo(6));
            Assert.That(gltf.nodes.Any(node => node.name == "Route_P01_to_P06"), Is.True);
            Assert.That(gltf.nodes.Any(node => node.name != null &&
                                             (node.name.Contains("Ground") || node.name.Contains("Floor") ||
                                              node.name.Contains("Building") || node.name.Contains("Column"))),
                Is.False, "The atlas is an incomplete abstract sandbox, not captured real scenery.");

            var positions = new HashSet<int>();
            foreach (var mesh in gltf.meshes)
            foreach (var primitive in mesh.primitives)
                positions.Add(primitive.attributes.POSITION);
            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            foreach (var index in positions)
            {
                var accessor = gltf.accessors[index];
                if (accessor.min == null || accessor.max == null || accessor.min.Length != 3 || accessor.max.Length != 3)
                    continue;
                min = Vector3.Min(min, new Vector3(accessor.min[0], accessor.min[1], accessor.min[2]));
                max = Vector3.Max(max, new Vector3(accessor.max[0], accessor.max[1], accessor.max[2]));
            }

            Assert.That(max.x - min.x, Is.InRange(7.5f, 8.2f));
            Assert.That(max.z - min.z, Is.InRange(7.5f, 8.2f));
            Assert.That(max.y - min.y, Is.LessThan(1f),
                "The route mesh itself should remain a shallow horizontal authoring layer; bubbles are translated nodes.");
        }

        [UnityTest]
        public IEnumerator ProductionMapImportsAtSixteenMetresWithSixDisplayOnlyPoints()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var serialized = new SerializedObject(prefab.GetComponent<VisitorAtlasHubPresentation>());
            var parent = new GameObject("AtlasImportVerification");
            parent.SetActive(false);
            parent.transform.localScale = VisitorMapConfiguration.Resolve(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json")).scale * Vector3.one;
            parent.transform.position = Vector3.up *
                serialized.FindProperty("_sessionRootWorldHeight").floatValue;
            var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            IVisitorAtlasHubMapLease lease = null;
            // EditMode has no player loop or DontDestroyOnLoad scene. Keep the
            // production loader unchanged and provide glTFast's test import scheduler.
            var deferAgent = new GLTFast.UninterruptedDeferAgent();
            GLTFast.GltfImport.SetDefaultDeferAgent(deferAgent);
            try
            {
                var path = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, StreamingAssetsPath));
                var load = new GltfVisitorAtlasHubMapLoader().LoadAsync(
                    new Uri(path), parent.transform, cancellation.Token);
                while (!load.IsCompleted) yield return null;
                lease = load.GetAwaiter().GetResult();
                Assert.That(lease.Root.activeInHierarchy, Is.False,
                    "The real GLB must be ready without exposing it during startup preload.");
                parent.SetActive(true);
                var renderers = lease.Root.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers, Is.Not.Empty);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Assert.That(bounds.size.x, Is.EqualTo(16f).Within(0.025f));
                Assert.That(bounds.size.z, Is.EqualTo(16f).Within(0.025f));
                Assert.That(bounds.max.y, Is.LessThan(2.1f),
                    "Expanding the footprint must keep markers in the standing viewing band.");
                Assert.That(lease.Root.GetComponentsInChildren<Transform>()
                    .Count(value => value.name.StartsWith("MapPoint_P", StringComparison.Ordinal)), Is.EqualTo(6));
                Assert.That(lease.Root.GetComponentsInChildren<Collider>(), Is.Empty);
                var materials = renderers.SelectMany(value => value.sharedMaterials).Distinct().ToArray();
                Assert.That(materials.Length, Is.LessThanOrEqualTo(12));
                Assert.That(materials.All(value => value != null && value.renderQueue < 2501), Is.True,
                    "Hollow markers use opaque geometry, avoiding a large transparent overdraw layer.");
                Debug.Log($"[AtlasMapImport] world bounds={bounds.size}; renderers={renderers.Length}; materials={materials.Length}");
            }
            finally
            {
                cancellation.Cancel();
                cancellation.Dispose();
                lease?.Dispose();
                GLTFast.GltfImport.UnsetDefaultDeferAgent(deferAgent);
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void ImportedBookAndScrollEntryModelsAreLowPolySingleMaterialAndLicenseClosed()
        {
            AssertImportedMenuModel("book", expectedTriangles: 52, maximumSourceExtent: 1.2f);
            AssertImportedMenuModel("scroll", expectedTriangles: 240, maximumSourceExtent: 2.3f);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    $"{MenuAssetRoot}/RPGItemCollection2-SOURCE-AND-LICENSE.txt"),
                Is.Not.Null);
        }

        [Test]
        [TestCase("VisualRoot/EntryChoices/BookRoot/BookPokeTarget")]
        [TestCase("VisualRoot/EntryChoices/MapEntryRoot/MapPokeTarget")]
        public void MenuRelayEmitsOncePerPhysicalSelectPress(string targetPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = UnityEngine.Object.Instantiate(prefab);
            var target = instance.transform.Find(targetPath);
            var relay = target != null ? target.GetComponent<VisitorAtlasHubPointableRelay>() : null;
            var poke = target != null ? target.GetComponent<PokeInteractable>() : null;
            try
            {
                Assert.That(relay, Is.Not.Null);
                Assert.That(poke, Is.Not.Null);
                relay.Configure();
                relay.SetArmed(true);
                var selected = 0;
                relay.Selected += () => selected++;
                var pose = new Pose(target.position, target.rotation);

                poke.PublishPointerEvent(new PointerEvent(812, PointerEventType.Select, pose));
                poke.PublishPointerEvent(new PointerEvent(812, PointerEventType.Select, pose));
                Assert.That(selected, Is.EqualTo(1));

                poke.PublishPointerEvent(new PointerEvent(812, PointerEventType.Unselect, pose));
                poke.PublishPointerEvent(new PointerEvent(812, PointerEventType.Select, pose));
                Assert.That(selected, Is.EqualTo(2));
            }
            finally
            {
                relay?.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        static void AssertImportedMenuModel(
            string assetName,
            int expectedTriangles,
            float maximumSourceExtent)
        {
            var path = $"{MenuAssetRoot}/{assetName}.obj";
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var mesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().SingleOrDefault();
            Assert.That(root, Is.Not.Null, path);
            Assert.That(mesh, Is.Not.Null, $"{path} must import exactly one low-poly Mesh.");
            Assert.That(mesh.triangles.Length / 3, Is.EqualTo(expectedTriangles));
            Assert.That(mesh.bounds.size.x, Is.LessThanOrEqualTo(maximumSourceExtent));
            Assert.That(mesh.bounds.size.y, Is.LessThanOrEqualTo(maximumSourceExtent));
            Assert.That(mesh.bounds.size.z, Is.LessThanOrEqualTo(maximumSourceExtent));

            var renderer = root.GetComponentInChildren<MeshRenderer>(true);
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1));
            Assert.That(
                AssetDatabase.GetAssetPath(renderer.sharedMaterial),
                Is.EqualTo($"{MenuAssetRoot}/AtlasMenu{char.ToUpperInvariant(assetName[0])}{assetName.Substring(1)}.mat"));
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out var guid, out long localId), Is.True);
            Debug.Log($"[VisitorAtlasHubAssetContract] {assetName} mesh guid={guid} localId={localId}");
        }

        static GltfRoot ReadGltfJson(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.That(Encoding.ASCII.GetString(bytes, 0, 4), Is.EqualTo("glTF"));
            var jsonLength = BitConverter.ToInt32(bytes, 12);
            var json = Encoding.UTF8.GetString(bytes, 20, jsonLength).TrimEnd(' ', '\0');
            return JsonUtility.FromJson<GltfRoot>(json);
        }

        [Serializable]
        sealed class GltfRoot
        {
            public GltfAsset asset;
            public GltfNode[] nodes = Array.Empty<GltfNode>();
            public GltfMesh[] meshes = Array.Empty<GltfMesh>();
            public GltfAccessor[] accessors = Array.Empty<GltfAccessor>();
        }

        [Serializable]
        sealed class GltfAsset
        {
            public string upAxis;
        }

        [Serializable]
        sealed class GltfNode
        {
            public string name;
        }

        [Serializable]
        sealed class GltfMesh
        {
            public GltfPrimitive[] primitives = Array.Empty<GltfPrimitive>();
        }

        [Serializable]
        sealed class GltfPrimitive
        {
            public GltfAttributes attributes;
        }

        [Serializable]
        sealed class GltfAttributes
        {
            public int POSITION;
        }

        [Serializable]
        sealed class GltfAccessor
        {
            public float[] min;
            public float[] max;
        }
    }
}
