using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.Model.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Backend;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.PhysicalAugmentation
{
    public sealed class PhysicalAugmentationProductionAssetsTests
    {
        const string Root =
            "Assets/BotanicalGardenQR/Content/Shared/PhysicalAugmentation/physical_point_001/";
        const string BottleTreeTestRoot =
            "Assets/BotanicalGardenQR/Content/Shared/PhysicalAugmentation/physical_point_002/";
        const string BottleTreeAnimatedModel = BottleTreeTestRoot + "Models/Fox.glb";
        const string ReusedModelRoot =
            "Assets/BotanicalGardenQR/Content/Scenes/giant_saguaro/Model/";
        const string ReusedModelPrefab =
            ReusedModelRoot + "Prefabs/giant_saguaro_bat.prefab";
        const string ReusedModelFbx =
            ReusedModelRoot + "Meshes/giant_saguaro_bat.fbx";
        const string GiantSaguaroAuthoringPath =
            "Assets/BotanicalGardenQR/Content/Authoring/Scenes/giant_saguaro/ContentSceneConfig.asset";
        const string AuthoringPath =
            "Assets/BotanicalGardenQR/Content/Authoring/PhysicalAugmentationCatalog.asset";
        const string PublishedPath =
            "Assets/BotanicalGardenQR/Content/Published/PhysicalAugmentationCatalog.asset";
        const string PublishedLibraryPath =
            "Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset";
        const string VisitorRuntimePath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab";
        static readonly HashSet<string> ContentExtensions = new HashSet<string>(
            new[] { ".prefab", ".fbx", ".glb", ".controller", ".mat", ".wav", ".mp3", ".ogg", ".shader", ".png", ".jpg", ".jpeg" },
            StringComparer.OrdinalIgnoreCase);

        [Test]
        public void ConfiguredExamplePointUsesStableIdentityAndMandatoryDepthPolicy()
        {
            var authoring = AssetDatabase.LoadAssetAtPath<PhysicalAugmentationCatalogAuthoringAsset>(
                AuthoringPath);
            var published = AssetDatabase.LoadAssetAtPath<PhysicalAugmentationCatalogAsset>(PublishedPath);

            Assert.That(authoring, Is.Not.Null);
            Assert.That(published, Is.Not.Null);
            Assert.That(authoring.TryBuild(out var authoringCatalog, out var authoringError),
                Is.True, authoringError);
            Assert.That(published.TryBuild(out var publishedCatalog, out var publishedError),
                Is.True, publishedError);
            Assert.That(authoringCatalog.Definitions, Is.Not.Empty);
            Assert.That(publishedCatalog.Definitions, Is.Not.Empty);

            var definition = publishedCatalog.Definitions.Single(candidate =>
                candidate.PointId.Value == "physical_point_001");
            Assert.That(definition.PointId.Value, Is.EqualTo("physical_point_001"));
            Assert.That(definition.InstallationAnchorNumber, Is.EqualTo(1));
            Assert.That(definition.PointId.Value, Does.Not.Contain("cactus"));
            Assert.That(definition.PointId.Value, Does.Not.Contain("saguaro"));
            Assert.That(
                definition.OcclusionPolicy,
                Is.EqualTo(PhysicalAugmentationOcclusionPolicy.RequireEnvironmentDepthAndProxy));
            Assert.That(AssetDatabase.GetAssetPath(definition.PerformancePrefab), Does.StartWith(Root));
            Assert.That(AssetDatabase.GetAssetPath(definition.DepthProxyPrefab), Does.StartWith(Root));
        }

        [Test]
        public void BottleTreeTestPointUsesAnchorTwoAndReusableAnimatedPerformance()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PhysicalAugmentationCatalogAsset>(PublishedPath);
            Assert.That(catalog.TryBuild(out var definitions, out var error), Is.True, error);
            var definition = definitions.Definitions.Single(candidate =>
                candidate.PointId.Value == "physical_point_002");

            Assert.That(definition.InstallationAnchorNumber, Is.EqualTo(2));
            Assert.That(AssetDatabase.GetAssetPath(definition.PerformancePrefab),
                Does.StartWith(BottleTreeTestRoot));
            Assert.That(AssetDatabase.GetAssetPath(definition.DepthProxyPrefab),
                Does.StartWith(BottleTreeTestRoot));
            Assert.That(definition.PerformancePrefab.GetComponent<LegacyAnimationPhysicalAugmentationPerformance>(),
                Is.Not.Null);
            Assert.That(definition.PerformancePrefab.GetComponentsInChildren<Animation>(true),
                Has.Length.EqualTo(1));
            Assert.That(AssetDatabase.GetDependencies(
                    AssetDatabase.GetAssetPath(definition.PerformancePrefab), true),
                Does.Contain(BottleTreeAnimatedModel));
            Assert.That(AssetDatabase.GetDependencies(
                    AssetDatabase.GetAssetPath(definition.PerformancePrefab), true)
                .Any(path => path.StartsWith(ReusedModelRoot, StringComparison.Ordinal)),
                Is.False);
            Assert.That(definition.DepthProxyPrefab.GetComponentsInChildren<Renderer>(true),
                Is.Not.Empty);
            foreach (var renderer in definition.DepthProxyPrefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    Assert.That(material?.shader?.name,
                        Is.EqualTo("BotanicalGardenQR/PhysicalAugmentation/DepthOnlyProxy"));
        }

        [Test]
        public void BottleTreeAnimatedModelPlaysSurveyAtItsAuthoredAnchorLocalStage()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                BottleTreeTestRoot + "Prefabs/PhysicalPoint002Performance.prefab");
            var importedClips = AssetDatabase.LoadAllAssetsAtPath(BottleTreeAnimatedModel)
                .OfType<AnimationClip>()
                .ToDictionary(clip => clip.name, StringComparer.Ordinal);

            Assert.That(importedClips.Keys, Is.EquivalentTo(new[] { "Survey", "Walk", "Run" }));
            Assert.That(importedClips.Values.All(clip => clip.legacy), Is.True);
            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                var performance = instance.GetComponent<LegacyAnimationPhysicalAugmentationPerformance>();
                var animation = instance.GetComponentsInChildren<Animation>(true).Single();
                var visual = instance.GetComponentsInChildren<Transform>(true)
                    .Single(transform => transform.name == "AnchoredModelVisual");

                Assert.That(visual.localPosition, Is.EqualTo(Vector3.forward));
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one * 0.008f));
                Assert.DoesNotThrow(() => performance.Play(1));
                Assert.That(animation.IsPlaying("Survey"), Is.True);
                Assert.That(animation["Survey"], Is.Not.Null);
                Assert.That(animation["Survey"].wrapMode, Is.EqualTo(WrapMode.Once));
                Assert.That(animation["Survey"].normalizedTime, Is.InRange(0f, 0.01f));
                AssertVisibleShaders(instance);
                var renderers = instance.GetComponentsInChildren<Renderer>(false);
                Assert.That(renderers, Is.Not.Empty);
                var bounds = renderers[0].bounds;
                for (var index = 1; index < renderers.Length; index++)
                    bounds.Encapsulate(renderers[index].bounds);
                Assert.That(Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z),
                    Is.InRange(0.1f, 2.5f));
                Assert.That(bounds.SqrDistance(Vector3.forward), Is.LessThan(0.25f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BottleTreeAnimatedPerformanceIsNotHiddenInsideItsDepthProxy()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PhysicalAugmentationCatalogAsset>(PublishedPath);
            Assert.That(catalog.TryBuild(out var definitions, out var error), Is.True, error);
            var definition = definitions.Definitions.Single(candidate =>
                candidate.PointId.Value == "physical_point_002");
            var root = new GameObject("BottleTreeRealityLayoutTestRoot");
            try
            {
                var performance = UnityEngine.Object.Instantiate(
                    definition.PerformancePrefab,
                    root.transform,
                    false);
                var proxy = UnityEngine.Object.Instantiate(
                    definition.DepthProxyPrefab,
                    root.transform,
                    false);
                performance.GetComponent<LegacyAnimationPhysicalAugmentationPerformance>().Play(1);

                var performanceBounds = CalculateRendererBounds(performance);
                var proxyBounds = CalculateRendererBounds(proxy);
                Assert.That(
                    proxyBounds.Contains(performanceBounds.center),
                    Is.False,
                    $"The visible performance is centered inside the depth-only proxy and can be fully occluded. " +
                    $"Performance={performanceBounds}; Proxy={proxyBounds}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void VisibleAssetsUseMetaDepthShadersAndProxyIsDepthOnly()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PhysicalAugmentationCatalogAsset>(PublishedPath);
            Assert.That(catalog.TryBuild(out var definitions, out var error), Is.True, error);
            var definition = definitions.Definitions.Single(candidate =>
                candidate.PointId.Value == "physical_point_001");

            var performanceInstance = UnityEngine.Object.Instantiate(definition.PerformancePrefab);
            try
            {
                performanceInstance.GetComponent<AnimatorPhysicalAugmentationPerformance>()
                    .ResetPerformance();
                AssertVisibleShaders(performanceInstance);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(performanceInstance);
            }
            var proxyRenderers = definition.DepthProxyPrefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(proxyRenderers, Is.Not.Empty);
            foreach (var renderer in proxyRenderers)
                foreach (var material in renderer.sharedMaterials)
                    Assert.That(
                        material?.shader?.name,
                        Is.EqualTo("BotanicalGardenQR/PhysicalAugmentation/DepthOnlyProxy"));

            Assert.That(definition.PerformancePrefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(definition.DepthProxyPrefab.GetComponentsInChildren<Collider>(true), Is.Empty);
        }

        [Test]
        public void PerformanceReusesCurrentModelAsIndependentAnimatorSequence()
        {
            var performance = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "Prefabs/PhysicalPoint001Performance.prefab");
            var sequence = performance.GetComponent<AnimatorPhysicalAugmentationPerformance>();
            var animator = performance.GetComponentsInChildren<Animator>(true).Single();
            var visual = performance.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "AnchoredModelVisual");

            Assert.That(sequence, Is.Not.Null);
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one * 0.08f));
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),
                Does.StartWith(ReusedModelRoot));
            Assert.That(
                AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(visual.gameObject)),
                Is.EqualTo(ReusedModelPrefab));
            Assert.That(performance.GetComponentsInChildren<PhysicalAugmentationPerformanceBehaviour>(true),
                Has.Length.EqualTo(1));

            var allDependencies = AssetDatabase.GetDependencies(
                    AssetDatabase.GetAssetPath(performance),
                    true);
            var contentDependencies = allDependencies
                .Where(path => ContentExtensions.Contains(Path.GetExtension(path)))
                .Where(path => !path.StartsWith("Packages/", StringComparison.Ordinal))
                .ToArray();
            foreach (var dependency in contentDependencies)
                Assert.That(
                    dependency,
                    Does.StartWith(Root).Or.StartWith(ReusedModelRoot),
                    dependency);
            Assert.That(allDependencies, Does.Contain(ReusedModelPrefab));
            foreach (var dependency in allDependencies.Where(path =>
                         ContentExtensions.Contains(Path.GetExtension(path)) &&
                         path.StartsWith("Packages/", StringComparison.Ordinal)))
                Assert.That(
                    dependency,
                    Does.StartWith("Packages/com.meta.xr.sdk.core/")
                        .Or.StartWith("Packages/com.unity.render-pipelines.universal/"),
                    dependency);
            Assert.That(File.Exists(Root + "SOURCE.md"), Is.True);
            Assert.That(File.Exists(Root + "ThirdParty/SpatialLingo-LICENSE.txt"), Is.True);
        }

        [Test]
        public void Point001PerformanceOwnsTheApprovedSpatialBloomAudioLifecycle()
        {
            var performance = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "Prefabs/PhysicalPoint001Performance.prefab");
            var media = performance.GetComponent<PhysicalAugmentationOwnedMedia>();
            var sources = performance.GetComponentsInChildren<AudioSource>(true);

            Assert.That(media, Is.Not.Null);
            Assert.That(sources, Has.Length.EqualTo(1));
            Assert.That(
                AssetDatabase.GetAssetPath(sources[0].clip),
                Is.EqualTo(Root + "Audio/PhysicalPoint001Bloom.wav"));
            Assert.That(sources[0].playOnAwake, Is.False);
            Assert.That(sources[0].loop, Is.False);
            Assert.That(sources[0].spatialize, Is.True);
            Assert.That(sources[0].spatialBlend, Is.EqualTo(1f).Within(0.001f));
            Assert.That(sources[0].maxDistance, Is.LessThanOrEqualTo(8f));
            var importer = AssetImporter.GetAtPath(
                Root + "Audio/PhysicalPoint001Bloom.wav") as AudioImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.forceToMono, Is.True,
                "A fixed-position spatial source must not retain the stereo source layout at runtime.");

            var serialized = new SerializedObject(media);
            var ownedSources = serialized.FindProperty("_audioSources");
            Assert.That(ownedSources.arraySize, Is.EqualTo(1));
            Assert.That(
                ownedSources.GetArrayElementAtIndex(0).objectReferenceValue,
                Is.SameAs(sources[0]));

            var instance = UnityEngine.Object.Instantiate(performance);
            try
            {
                var sequence = instance.GetComponent<AnimatorPhysicalAugmentationPerformance>();
                var ownedMedia = instance.GetComponent<PhysicalAugmentationOwnedMedia>();

                sequence.Play(1);
                Assert.That(ownedMedia.IsPlaying, Is.True);
                Assert.That(ownedMedia.PlayRevision, Is.EqualTo(1));
                sequence.StopPerformance();
                Assert.That(ownedMedia.IsPlaying, Is.False);

                sequence.Play(2);
                Assert.That(ownedMedia.IsPlaying, Is.True);
                Assert.That(ownedMedia.PlayRevision, Is.EqualTo(2));
                sequence.StopPerformance();
                Assert.That(ownedMedia.IsPlaying, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void RealityPerformanceStartsTheConfiguredAnimatorStateWithVisibleRenderers()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "Prefabs/PhysicalPoint001Performance.prefab");
            var instance = UnityEngine.Object.Instantiate(source);
            try
            {
                var performance = instance.GetComponent<AnimatorPhysicalAugmentationPerformance>();
                var animator = instance.GetComponentsInChildren<Animator>(true).Single();

                Assert.DoesNotThrow(() => performance.Play(1));
                animator.Update(0.25f);
                var state = animator.GetCurrentAnimatorStateInfo(0);
                Assert.That(state.IsName("Scene"), Is.True);
                Assert.That(animator.speed, Is.EqualTo(1f));
                Assert.That(
                    instance.GetComponentsInChildren<Renderer>(false),
                    Is.Not.Empty);
                Assert.That(
                    instance.GetComponentsInChildren<Renderer>(false).All(renderer => renderer.enabled),
                    Is.True);
                var renderers = instance.GetComponentsInChildren<Renderer>(false);
                var stateHash = Animator.StringToHash("Scene");
                var normalizedSamples = new[] { 0f, 0.25f, 0.5f, 0.75f, 0.99f };
                foreach (var normalizedTime in normalizedSamples)
                {
                    animator.Play(stateHash, 0, normalizedTime);
                    animator.Update(0f);
                    var bounds = renderers[0].bounds;
                    for (var index = 1; index < renderers.Length; index++)
                        bounds.Encapsulate(renderers[index].bounds);
                    Assert.That(bounds.size.x, Is.InRange(0.05f, 2.5f));
                    Assert.That(bounds.size.y, Is.InRange(0.05f, 2.5f));
                    Assert.That(bounds.size.z, Is.InRange(0.05f, 2.5f));
                    Assert.That(
                        Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z),
                        Is.GreaterThanOrEqualTo(0.5f));
                    Assert.That(
                        bounds.SqrDistance(Vector3.zero),
                        Is.LessThan(0.01f),
                        $"Reality performance must remain close to its meter-space anchor origin. " +
                        $"NormalizedTime={normalizedTime}, Bounds={bounds}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ReplacedBatCactusModelKeepsBoundedGeometryAndCompleteFlightToNectarAnimation()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ReusedModelPrefab);
            var animator = model.GetComponentsInChildren<Animator>(true).Single();
            var clip = animator.runtimeAnimatorController.animationClips
                .Single(candidate => candidate.name == "Scene");
            Assert.That(clip.frameRate, Is.EqualTo(24f).Within(0.01f));
            Assert.That(clip.length, Is.InRange(15.1f, 15.3f));

            var importer = AssetImporter.GetAtPath(
                ReusedModelRoot + "Meshes/giant_saguaro_bat.fbx") as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            var importedClip = importer.clipAnimations.Single(candidate => candidate.name == "Scene");
            Assert.That(importedClip.firstFrame, Is.EqualTo(1f));
            Assert.That(importedClip.lastFrame, Is.EqualTo(365f));
            Assert.That(importedClip.loopTime, Is.False);

            var performance = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "Prefabs/PhysicalPoint001Performance.prefab");
            var performanceSerialized = new SerializedObject(
                performance.GetComponent<AnimatorPhysicalAugmentationPerformance>());
            var fallbackDuration = performanceSerialized
                .FindProperty("_fallbackDurationSeconds")
                .floatValue;
            Assert.That(fallbackDuration, Is.GreaterThanOrEqualTo(clip.length));

            var fbxPath = ReusedModelRoot + "Meshes/giant_saguaro_bat.fbx";
            var triangleCount = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<Mesh>()
                .Sum(mesh => Enumerable.Range(0, mesh.subMeshCount)
                    .Sum(subMesh => (long)mesh.GetIndexCount(subMesh) / 3L));
            Assert.That(triangleCount, Is.InRange(50000L, 90000L));
        }

        [Test]
        public void ReplacedBatCactusKeepsItsDistinctSourceMaterialRegions()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ReusedModelPrefab);
            var assignedMaterials = model.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .ToArray();
            var materialSummary = string.Join(
                "; ",
                assignedMaterials.Select(material =>
                    $"{material.name}:shader={material.shader?.name ?? "<none>"}," +
                    $"texture={material.mainTexture?.name ?? "<none>"}," +
                    $"path={AssetDatabase.GetAssetPath(material)}"));
            var texturedRegions = assignedMaterials
                .Where(material => material.mainTexture != null)
                .Select(material => material.mainTexture)
                .Distinct()
                .ToArray();
            var authoring = AssetDatabase.LoadAssetAtPath<ContentSceneConfig>(
                GiantSaguaroAuthoringPath);
            var publishedLibrary = AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>(
                PublishedLibraryPath);
            var publishedModels = (IModelDefinitionSource)new PublishedSceneResolver(publishedLibrary);
            var performance = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "Prefabs/PhysicalPoint001Performance.prefab");
            var performanceSerialized = new SerializedObject(
                performance.GetComponent<AnimatorPhysicalAugmentationPerformance>());

            Assert.That(assignedMaterials.Length, Is.GreaterThanOrEqualTo(3), materialSummary);
            Assert.That(texturedRegions.Length, Is.GreaterThanOrEqualTo(2), materialSummary);
            Assert.That(assignedMaterials.All(material =>
                    AssetDatabase.GetAssetPath(material).StartsWith(
                        ReusedModelRoot,
                        StringComparison.Ordinal)),
                Is.True,
                materialSummary);
            Assert.That(authoring.Content.Model.MaterialOverride, Is.Null,
                "A single model-page override replaces every source material slot. " + materialSummary);
            Assert.That(authoring.Content.Model.Animation, Is.Not.Null,
                "Material remapping must not remove the authored Model-page animation.");
            Assert.That(
                publishedModels.TryGet(new SceneId("giant_saguaro"), out var publishedModel),
                Is.True);
            Assert.That(publishedModel.Animation, Is.Not.Null,
                "The published Model page must retain the giant-saguaro animation.");
            Assert.That(
                performanceSerialized.FindProperty("_materialOverride").objectReferenceValue,
                Is.Null,
                "A single reality override replaces every source material slot. " + materialSummary);
        }

        [Test]
        public void RetiredCueAndCalibrationPreviewAssetsAndTypesAreAbsent()
        {
            var retiredAssets = new[]
            {
                Root + "Prefabs/PhysicalPoint001VisitorCue.prefab",
                Root + "Prefabs/PhysicalPoint001CalibrationPreview.prefab",
                Root + "Materials/PhysicalPoint001Cue.mat",
                Root + "Materials/PhysicalPoint001PreviewProxy.mat",
                Root + "Materials/PhysicalPoint001Flower.mat",
                Root + "Materials/PhysicalPoint001FlowerCenter.mat",
                BottleTreeTestRoot + "Prefabs/PhysicalPoint002VisitorCue.prefab",
                BottleTreeTestRoot + "Prefabs/PhysicalPoint002CalibrationPreview.prefab"
            };
            foreach (var path in retiredAssets)
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Null, path);

            var retiredTypeNames = new[]
            {
                "BotanicalGardenQR.PhysicalAugmentation.Frontend.PhysicalAugmentationPresenter",
                "BotanicalGardenQR.PhysicalAugmentation.Frontend.PhysicalAugmentationPulseCue"
            };
            foreach (var typeName in retiredTypeNames)
                Assert.That(AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(typeName, false))
                    .Any(type => type != null), Is.False, typeName);
        }

        [Test]
        public void GiantSaguaroPublishesFeatureActionAssociationWithoutSceneSpecificRuntimeBranch()
        {
            var library = AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>(PublishedLibraryPath);
            Assert.That(library, Is.Not.Null);
            var resolver = new PublishedSceneResolver(library);

            Assert.That(
                resolver.GetPhysicalAugmentationPoints(new SceneId("giant_saguaro"))
                    .Select(pointId => pointId.Value),
                Is.EqualTo(new[] { "physical_point_001" }));
            Assert.That(
                resolver.GetPhysicalAugmentationPoints(new SceneId("bottle_tree"))
                    .Select(pointId => pointId.Value),
                Is.EqualTo(new[] { "physical_point_002" }));
            Assert.That(
                resolver.GetPhysicalAugmentationPoints(new SceneId("baobab")),
                Is.Empty);
        }

        [Test]
        public void VisitorRuntimeUsesOneModelFeatureActionWithExternalFocusExit()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorRuntimePath);
            Assert.That(prefab, Is.Not.Null);
            var actions = prefab.GetComponentsInChildren<ShellFlowActionTarget>(true)
                .Where(target => target.Action == ShellFlowAction.FeaturePageAction)
                .ToArray();

            Assert.That(actions, Has.Length.EqualTo(1));
            Assert.That(actions[0].Feature, Is.EqualTo(FeaturePageId.Model));
            Assert.That(actions[0].transform.parent.name, Is.EqualTo("ModelControlSlot"));
            Assert.That(
                actions[0].GetComponentsInChildren<UnityEngine.UI.Text>(true).Single().text,
                Is.EqualTo("现实演示"));
            var modelFrontend = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(component => component != null &&
                                     component.GetType().FullName ==
                                     "BotanicalGardenQR.Model.Frontend.ModelFrontend");
            var modelSerialized = new SerializedObject(modelFrontend);
            var oldAnimationButton = modelSerialized.FindProperty("_animationButton")
                ?.objectReferenceValue as UnityEngine.UI.Button;
            var actionSerialized = new SerializedObject(actions[0]);
            var realityButton = actionSerialized.FindProperty("_button")
                ?.objectReferenceValue as UnityEngine.UI.Button;
            Assert.That(oldAnimationButton, Is.Not.Null);
            Assert.That(realityButton, Is.Not.Null);
            Assert.That(realityButton, Is.Not.SameAs(oldAnimationButton));
            Assert.That(oldAnimationButton.name, Is.EqualTo("Button_ModelAnimation"));
            var focusExits = prefab.GetComponentsInChildren<ShellFlowActionTarget>(true)
                .Where(target => target.Action == ShellFlowAction.FeaturePageActionExitFocus)
                .ToArray();
            Assert.That(focusExits, Has.Length.EqualTo(1));
            Assert.That(focusExits[0].Feature, Is.EqualTo(FeaturePageId.Model));
            Assert.That(
                focusExits[0].GetComponentsInChildren<UnityEngine.UI.Text>(true).Single().text,
                Is.EqualTo("返回面板"));
            var shell = prefab.GetComponentInChildren<GlobalFrontendShell>(true);
            var shellSerialized = new SerializedObject(shell);
            var slots = shellSerialized.FindProperty("_slots");
            var authoredShellRoot = slots.FindPropertyRelative("_shellRoot")
                .objectReferenceValue as RectTransform;
            var authoredHeader = slots.FindPropertyRelative("_headerSlot")
                .objectReferenceValue as RectTransform;
            var authoredStatus = slots.FindPropertyRelative("_statusSlot")
                .objectReferenceValue as RectTransform;
            var authoredVideoModeTitle = slots.FindPropertyRelative("_videoModeTitle")
                .objectReferenceValue as UnityEngine.UI.Text;
            var authoredModelModeTitle = slots.FindPropertyRelative("_modelModeTitle")
                .objectReferenceValue as UnityEngine.UI.Text;
            var authoredFocusExit = slots.FindPropertyRelative("_featureFocusExitSlot")
                .objectReferenceValue as RectTransform;
            Assert.That(authoredStatus, Is.Not.Null);
            Assert.That(authoredStatus.parent, Is.SameAs(authoredShellRoot));
            Assert.That(authoredStatus.IsChildOf(authoredHeader), Is.False);
            Assert.That(authoredStatus.sizeDelta.x, Is.GreaterThan(200f));
            Assert.That(authoredVideoModeTitle, Is.Not.Null);
            Assert.That(authoredModelModeTitle, Is.Not.Null);
            Assert.That(authoredFocusExit, Is.SameAs(focusExits[0].transform));
            Assert.That(authoredFocusExit.IsChildOf(authoredShellRoot), Is.False);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component =>
                    component != null && component.GetType().FullName ==
                    "BotanicalGardenQR.PhysicalAugmentation.Frontend.PhysicalAugmentationPresenter"),
                Is.False);
            Assert.That(
                prefab.GetComponentsInChildren<Transform>(true)
                    .Any(transform => transform.name == "VisitorCues"),
                Is.False);
        }

        static void AssertVisibleShaders(GameObject prefab)
        {
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty, prefab.name);
            foreach (var renderer in renderers)
                foreach (var material in renderer.sharedMaterials)
                    Assert.That(
                        material?.shader?.name,
                        Is.EqualTo("EnvironmentDepth/OcclusionLit")
                            .Or.EqualTo("EnvironmentDepth/URP/OcclusionUnlit"),
                        $"{prefab.name}/{renderer.name}");
        }

        static Bounds CalculateRendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(false);
            Assert.That(renderers, Is.Not.Empty, root.name);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }
    }
}
