using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Configuration.Editor;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Configuration
{
    public sealed class ContentScenePublisherTests
    {
        const string LibraryPath = "Assets/PublisherTests/ContentSceneLibrary.asset";
        const string MissingTypeAssetPath = "Assets/PublisherTests/MissingManagedReference.asset";
        const string ProductionSceneRoot = "Assets/BotanicalGardenQR/Content/Authoring/Scenes";
        const float ModelStageWidth = 830f;
        const float ModelStageHeight = 540f;
        const float MinimumDominantStageFill = 0.60f;
        const float MaximumDominantStageFill = 0.76f;
        const float VerticalSafeMargin = 0.08f;
        static readonly DateTimeOffset InitialPublishTime =
            new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero);

        ContentSceneLibrary _library;
        ScenePackage[] _packages;
        ConfigurationValidationResult _valid;

        [SetUp]
        public void SetUp()
        {
            _packages = new[] { CreatePackage("publisher_test") };
            _library = ScriptableObject.CreateInstance<ContentSceneLibrary>();
            _library.ReplacePublishedPayload(_packages, "digest-a", 7L, InitialPublishTime);
            _valid = new ConfigurationValidationResult(Array.Empty<ConfigurationIssue>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_library != null) UnityEngine.Object.DestroyImmediate(_library);
        }

        [Test]
        public void MatchingValidInput_IsUpToDateWithoutWriting()
        {
            var before = EditorJsonUtility.ToJson(_library);
            var save = new SaveProbe();

            var result = Publish(_packages, "digest-a", _valid, save);

            Assert.That(result.Status, Is.EqualTo(ContentScenePublishStatus.UpToDate));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(save.Calls, Is.Zero);
            Assert.That(EditorJsonUtility.ToJson(_library), Is.EqualTo(before));
        }

        [Test]
        public void ProductionInputs_AreUpToDateWithoutRewritingTheLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>(
                ContentScenePublisher.DefaultLibraryPath);
            Assert.That(library, Is.Not.Null);
            var before = EditorJsonUtility.ToJson(library);

            var result = ContentScenePublisher.PublishContentLibrary();

            Assert.That(result.Status, Is.EqualTo(ContentScenePublishStatus.UpToDate));
            Assert.That(EditorJsonUtility.ToJson(library), Is.EqualTo(before));
        }

        [Test]
        public void ProductionContentLibraryDigest_MatchesOnlyItsCompiledSources()
        {
            var issues = new List<ConfigurationIssue>();
            var assets = ConfigurationAssetSet.Load(issues);
            var expectedPaths = assets.Scenes
                .Select(AssetDatabase.GetAssetPath)
                .Concat(new[] { AssetDatabase.GetAssetPath(assets.Defaults) })
                .OrderBy(path => path, StringComparer.Ordinal);
            var expectedDigest = Hash128.Compute(string.Join(
                "\n",
                expectedPaths.Select(path =>
                    $"{AssetDatabase.AssetPathToGUID(path)}:{AssetDatabase.GetAssetDependencyHash(path)}")))
                .ToString();

            Assert.That(issues, Is.Empty);
            Assert.That(assets.Library, Is.Not.Null);
            Assert.That(
                ConfigurationInputDigest.ComputeContentLibrarySourceDigest(assets),
                Is.EqualTo(expectedDigest),
                "Application-level Fairy, Collection, Journey, Entry and environment assets must not invalidate a generated scene package they do not populate.");
            Assert.That(assets.Library.SourceDigest, Is.EqualTo(expectedDigest));
        }

        [Test]
        public void ProductionPhysicalCatalog_MatchesItsOwnAuthoringSource()
        {
            var issues = new List<ConfigurationIssue>();
            var assets = ConfigurationAssetSet.Load(issues);

            Assert.That(issues, Is.Empty);
            Assert.That(assets.PhysicalAugmentationCatalog, Is.Not.Null);
            var authoringPath = AssetDatabase.GetAssetPath(assets.PhysicalAugmentationAuthoring);
            var expectedDigest = Hash128.Compute(
                $"{AssetDatabase.AssetPathToGUID(authoringPath)}:{AssetDatabase.GetAssetDependencyHash(authoringPath)}")
                .ToString();
            Assert.That(
                assets.PhysicalAugmentationCatalog.SourceDigest,
                Is.EqualTo(expectedDigest));
            Assert.That(
                ConfigurationInputDigest.ComputePhysicalAugmentationSourceDigest(assets),
                Is.EqualTo(expectedDigest));
            Assert.That(
                assets.PhysicalAugmentationCatalog.Points.Count,
                Is.EqualTo(assets.PhysicalAugmentationAuthoring.Points.Count));
        }

        [Test]
        public void CompilerPreservesMultiplePhysicalAugmentationPointAssociations()
        {
            var clone = CreateProductionSceneClone();
            try
            {
                SetPrivate(
                    clone.Content,
                    "_physicalAugmentationPointIds",
                    new[] { "physical_point_001", "physical_point_002" });
                var loadIssues = new List<ConfigurationIssue>();
                var assets = ConfigurationAssetSet.Load(loadIssues);
                var issues = new List<ConfigurationIssue>();

                var succeeded = ContentSceneCompiler.TryCompile(
                    clone,
                    assets.Defaults,
                    issues,
                    out var package);

                Assert.That(loadIssues, Is.Empty);
                Assert.That(succeeded, Is.True, string.Join("\n", issues.Select(issue => issue.Message)));
                Assert.That(
                    package.PhysicalAugmentationPointIds.Select(pointId => pointId.Value),
                    Is.EqualTo(new[] { "physical_point_001", "physical_point_002" }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void CompilerRejectsDuplicatePhysicalAugmentationPointAssociations()
        {
            var clone = CreateProductionSceneClone();
            try
            {
                SetPrivate(
                    clone.Content,
                    "_physicalAugmentationPointIds",
                    new[] { "physical_point_001", "physical_point_001" });
                var loadIssues = new List<ConfigurationIssue>();
                var assets = ConfigurationAssetSet.Load(loadIssues);
                var issues = new List<ConfigurationIssue>();

                var succeeded = ContentSceneCompiler.TryCompile(
                    clone,
                    assets.Defaults,
                    issues,
                    out _);

                Assert.That(loadIssues, Is.Empty);
                Assert.That(succeeded, Is.False);
                Assert.That(issues.Any(issue => issue.Code == "CFG034"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void AssociationValidationReportsAnUnknownPointInsideAMultiPointScene()
        {
            var clone = CreateProductionSceneClone();
            try
            {
                SetPrivate(
                    clone.Content,
                    "_physicalAugmentationPointIds",
                    new[] { "physical_point_001", "physical_point_missing" });
                var loadIssues = new List<ConfigurationIssue>();
                var assets = ConfigurationAssetSet.Load(loadIssues);
                var issues = new List<ConfigurationIssue>();

                ContentSceneConfigurationValidator.ValidatePhysicalAugmentationAssociations(
                    new[] { clone },
                    assets.PhysicalAugmentationAuthoring,
                    issues);

                Assert.That(loadIssues, Is.Empty);
                Assert.That(
                    issues.Single(issue => issue.Code == "CFG129").Message,
                    Does.Contain("physical_point_missing"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        [Test]
        public void MissingManagedReferenceType_ReportsAssetPathAndExpectedType()
        {
            var createdTestRoot = !AssetDatabase.IsValidFolder("Assets/PublisherTests");
            if (createdTestRoot) AssetDatabase.CreateFolder("Assets", "PublisherTests");
            var sourcePath = AssetDatabase.GUIDToAssetPath(
                AssetDatabase.FindAssets("t:ContentSceneConfig", new[] { ProductionSceneRoot }).First());
            Assert.That(AssetDatabase.CopyAsset(sourcePath, MissingTypeAssetPath), Is.True);

            try
            {
                const string validType =
                    "type: {class: VideoContentSpec, ns: BotanicalGardenQR.Configuration.Runtime, asm: BotanicalGardenQR.Configuration.Runtime}";
                const string missingType =
                    "type: {class: MissingVideoContentSpec, ns: BotanicalGardenQR.Configuration.Runtime, asm: BotanicalGardenQR.Configuration.Runtime}";
                var yaml = File.ReadAllText(MissingTypeAssetPath);
                Assert.That(yaml, Does.Contain(validType));
                File.WriteAllText(MissingTypeAssetPath, yaml.Replace(validType, missingType));
                AssetDatabase.ImportAsset(MissingTypeAssetPath, ImportAssetOptions.ForceUpdate);

                var issue = ContentSceneConfigurationValidator.ValidateAll().Issues.SingleOrDefault(candidate =>
                    candidate.Code == "CFG111" && candidate.AssetPath == MissingTypeAssetPath);
                Assert.That(issue, Is.Not.Null);
                Assert.That(issue.Message, Does.Contain("BotanicalGardenQR.Configuration.Runtime"));
                Assert.That(issue.Message, Does.Contain("MissingVideoContentSpec"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(MissingTypeAssetPath);
                if (createdTestRoot) AssetDatabase.DeleteAsset("Assets/PublisherTests");
            }
        }

        [Test]
        public void ProductionModelPresentations_UseUniformScaleAndFillTheStageSafely()
        {
            var guids = AssetDatabase.FindAssets(
                "t:ContentSceneConfig",
                new[] { ProductionSceneRoot });
            Assert.That(guids, Has.Length.EqualTo(6));

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<ContentSceneConfig>(path);
                var model = config.Content.Model;
                Assert.That(model, Is.Not.Null, config.SerializedSceneId);
                Assert.That(model.Prefab, Is.Not.Null, config.SerializedSceneId);
                Assert.That(model.LocalScale.x, Is.GreaterThan(0f), config.SerializedSceneId);
                Assert.That(model.LocalScale.y, Is.EqualTo(model.LocalScale.x).Within(0.0001f),
                    $"{config.SerializedSceneId} must preserve its authored proportions.");
                Assert.That(model.LocalScale.z, Is.EqualTo(model.LocalScale.x).Within(0.0001f),
                    $"{config.SerializedSceneId} must preserve its authored proportions.");

                var instance = UnityEngine.Object.Instantiate(model.Prefab);
                try
                {
                    instance.transform.SetPositionAndRotation(
                        model.LocalPosition,
                        Quaternion.Euler(model.LocalEulerAngles));
                    instance.transform.localScale = model.LocalScale;

                    var renderers = instance.GetComponentsInChildren<Renderer>(true)
                        .Where(renderer => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                        .ToArray();
                    Assert.That(renderers, Is.Not.Empty, config.SerializedSceneId);
                    var bounds = renderers[0].bounds;
                    for (var index = 1; index < renderers.Length; index++)
                        bounds.Encapsulate(renderers[index].bounds);

                    var dominantFill = Mathf.Max(
                        bounds.size.x / ModelStageWidth,
                        bounds.size.y / ModelStageHeight);
                    Assert.That(
                        dominantFill,
                        Is.InRange(MinimumDominantStageFill, MaximumDominantStageFill),
                        $"{config.SerializedSceneId} visible bounds must fill the model stage without overflowing it.");

                    var verticalLimit = ModelStageHeight * (0.5f - VerticalSafeMargin);
                    Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-verticalLimit),
                        $"{config.SerializedSceneId} must keep the lower model-stage safety margin.");
                    Assert.That(bounds.max.y, Is.LessThanOrEqualTo(verticalLimit),
                        $"{config.SerializedSceneId} must keep the upper model-stage safety margin.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void ReleaseSummary_CoversProjectAuthorities()
        {
            var issues = new List<ConfigurationIssue>();
            var assets = ConfigurationAssetSet.Load(issues);
            var handoff = ConfigurationInputDigest.CreateReleaseSummary(assets);
            var publicEvidence = ConfigurationReleaseEvidence.CreateValidatedSummary();

            Assert.That(issues, Is.Empty);
            Assert.That(publicEvidence.ProjectDigest, Is.EqualTo(handoff.ProjectDigest));
            Assert.That(publicEvidence.ProjectAssetPaths, Is.EqualTo(handoff.ProjectAssetPaths));
            Assert.That(handoff.ProjectAssetPaths, Does.Contain(AssetDatabase.GetAssetPath(assets.Library)));
            Assert.That(handoff.ProjectAssetPaths, Does.Contain(AssetDatabase.GetAssetPath(assets.PrologueTheme)));
            Assert.That(handoff.ProjectAssetPaths, Does.Contain(AssetDatabase.GetAssetPath(assets.CoachTheme)));
            Assert.That(handoff.ProjectAssetPaths, Does.Contain(AssetDatabase.GetAssetPath(assets.Entries)));
            Assert.That(handoff.ProjectAssetPaths, Does.Contain(AssetDatabase.GetAssetPath(assets.Defaults)));
            Assert.That(handoff.ProjectAssetPaths, Does.Contain(AssetDatabase.GetAssetPath(assets.CollectionCatalog)));
            Assert.That(handoff.ProjectAssetPaths, Does.Contain(AssetDatabase.GetAssetPath(assets.Environment)));
            Assert.That(handoff.ProjectAssetPaths, Does.Contain(AssetDatabase.GetAssetPath(assets.Fairy)));
            Assert.That(handoff.ProjectAssetPaths, Does.Contain(AssetDatabase.GetAssetPath(assets.PhysicalAugmentationCatalog)));
            Assert.That(handoff.ProjectAssetPaths, Does.Contain(AssetDatabase.GetAssetPath(assets.PhysicalAugmentationAuthoring)));
            Assert.That(handoff.ProjectAssetPaths, Does.Not.Contain(string.Empty));
        }

        [Test]
        public void ChangedDigest_PublishesExactlyOnce()
        {
            var save = new SaveProbe();

            var result = Publish(_packages, "digest-b", _valid, save);

            Assert.That(result.Status, Is.EqualTo(ContentScenePublishStatus.Published));
            Assert.That(save.Calls, Is.EqualTo(1));
            Assert.That(_library.SourceDigest, Is.EqualTo("digest-b"));
            Assert.That(_library.PublishedVersion, Is.EqualTo(8L));
        }

        [Test]
        public void MatchingDigestWithDifferentPayload_RebuildsTheLibrary()
        {
            var save = new SaveProbe();
            var replacement = new[] { CreatePackage("replacement_test") };

            var result = Publish(replacement, "digest-a", _valid, save);

            Assert.That(result.Status, Is.EqualTo(ContentScenePublishStatus.Published));
            Assert.That(save.Calls, Is.EqualTo(1));
            Assert.That(_library.Packages.Single().SceneId.Value, Is.EqualTo("replacement_test"));
        }

        [Test]
        public void InvalidCompilationResult_DoesNotReplaceTheLibrary()
        {
            var before = EditorJsonUtility.ToJson(_library);
            var save = new SaveProbe();
            var invalid = new ConfigurationValidationResult(new[]
            {
                new ConfigurationIssue("TEST", "source", "Compilation failed.")
            });

            var result = Publish(_packages, string.Empty, invalid, save);

            Assert.That(result.Status, Is.EqualTo(ContentScenePublishStatus.Failed));
            Assert.That(save.Calls, Is.Zero);
            Assert.That(EditorJsonUtility.ToJson(_library), Is.EqualTo(before));
        }

        [Test]
        public void WriteFailure_RestoresThePreviousLibrary()
        {
            var before = EditorJsonUtility.ToJson(_library);
            var save = new SaveProbe { FailFirstCall = true };

            var result = Publish(_packages, "digest-b", _valid, save);

            Assert.That(result.Status, Is.EqualTo(ContentScenePublishStatus.Failed));
            Assert.That(save.Calls, Is.EqualTo(2), "The second save persists the restored snapshot.");
            Assert.That(EditorJsonUtility.ToJson(_library), Is.EqualTo(before));
            Assert.That(result.Validation.Issues.Any(issue => issue.Code == "CFG200"), Is.True);
        }

        [Test]
        public void SecondBundleWriteFailureRestoresBothPublishedAssets()
        {
            var physicalCatalog = ScriptableObject.CreateInstance<PhysicalAugmentationCatalogAsset>();
            try
            {
                physicalCatalog.ReplacePublishedPayload(
                    Array.Empty<PhysicalAugmentationPointRecord>(),
                    "physical-a",
                    7L,
                    InitialPublishTime);
                var libraryBefore = EditorJsonUtility.ToJson(_library);
                var physicalBefore = EditorJsonUtility.ToJson(physicalCatalog);
                var librarySaves = 0;
                var physicalSaves = 0;

                var result = ContentScenePublisher.PublishValidatedBundle(
                    _packages,
                    Array.Empty<PhysicalAugmentationPointRecord>(),
                    "digest-b",
                    "physical-b",
                    _library,
                    physicalCatalog,
                    LibraryPath,
                    "Assets/PublisherTests/PhysicalAugmentationCatalog.asset",
                    _valid,
                    saveLibrary: _ => librarySaves++,
                    savePhysicalCatalog: _ =>
                    {
                        physicalSaves++;
                        if (physicalSaves == 1) throw new IOException("injected second write failure");
                    },
                    deleteCreatedLibrary: () => throw new AssertionException("Existing library must not be deleted."),
                    deleteCreatedPhysicalCatalog: () => throw new AssertionException("Existing physical catalog must not be deleted."));

                Assert.That(result.Status, Is.EqualTo(ContentScenePublishStatus.Failed));
                Assert.That(librarySaves, Is.EqualTo(2));
                Assert.That(physicalSaves, Is.EqualTo(2));
                Assert.That(EditorJsonUtility.ToJson(_library), Is.EqualTo(libraryBefore));
                Assert.That(EditorJsonUtility.ToJson(physicalCatalog), Is.EqualTo(physicalBefore));
                Assert.That(result.Validation.Issues.Any(issue => issue.Code == "CFG200"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(physicalCatalog);
            }
        }

        ContentScenePublishResult Publish(
            ScenePackage[] packages,
            string digest,
            ConfigurationValidationResult validation,
            SaveProbe save)
            => ContentScenePublisher.PublishValidated(
                packages,
                digest,
                _library,
                LibraryPath,
                validation,
                () => throw new AssertionException("An existing test Library must not be recreated."),
                save.Save,
                () => throw new AssertionException("An existing test Library must not be deleted."));

        static ScenePackage CreatePackage(string sceneId)
        {
            var text = new TextStyleSpec(1f, Color.white, Vector2.zero, 1f);
            var presentation = new PresentationSpec(
                text,
                text,
                new LayoutSpec(Vector2.one, Vector2.zero, 0f),
                new AnimationSpec(0f));
            return new ScenePackage(
                sceneId,
                new ContentSpec(),
                presentation,
                Array.Empty<FeaturePageId>());
        }

        static ContentSceneConfig CreateProductionSceneClone()
        {
            var source = AssetDatabase.LoadAssetAtPath<ContentSceneConfig>(
                ProductionSceneRoot + "/giant_saguaro/ContentSceneConfig.asset");
            Assert.That(source, Is.Not.Null);
            return UnityEngine.Object.Instantiate(source);
        }

        static void SetPrivate(object target, string fieldName, object value)
            => target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);

        sealed class SaveProbe
        {
            public int Calls { get; private set; }
            public bool FailFirstCall { get; set; }

            public void Save(ContentSceneLibrary _)
            {
                Calls++;
                if (FailFirstCall && Calls == 1)
                    throw new IOException("Simulated write failure.");
            }
        }
    }
}
