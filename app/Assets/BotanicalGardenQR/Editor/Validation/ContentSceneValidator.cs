using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BotanicalGardenQR.Configuration.Editor;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Model.Contracts;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BotanicalGardenQR.Editor.Validation
{
    internal static class ContentSceneValidator
    {
        static readonly HashSet<string> ContentMediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".anim", ".controller", ".fbx", ".glb", ".jpeg", ".jpg", ".mat", ".mp3", ".mp4", ".obj", ".ogg", ".png", ".prefab", ".tga", ".wav", ".webm"
        };

        public static void Validate(ValidationScope scope, ICollection<CommercialValidationIssue> issues)
        {
            ConfigurationAssetSet assets = null;
            ConfigurationValidationResult configurationValidation = null;
            ScenePackage[] compiledPackages = Array.Empty<ScenePackage>();
            if (scope == ValidationScope.All)
            {
                assets = ContentSceneConfigurationValidator.ValidateAndCompile(
                    out compiledPackages,
                    out configurationValidation);
                foreach (var issue in configurationValidation.Issues)
                    CommercialArchitectureValidator.Add(issues, "COM-CONFIG-" + issue.Code, issue.AssetPath, "Configuration", issue.Message);
            }

            var scenes = assets == null ? LoadAll<ContentSceneConfig>() : assets.Scenes;
            ValidateUniqueNames(scenes, issues);
            foreach (var scene in scenes) ValidateScene(scene, scope, issues);
            if (CommercialArchitectureValidator.Includes(scope, ValidationScope.FrontendShell)) ValidateGlobalUiDefaults(issues);
            if (CommercialArchitectureValidator.Includes(scope, ValidationScope.Fairy)) ValidateGlobalFairy(issues);
            if (scope == ValidationScope.All)
                ValidatePublishedLibrary(
                    scenes,
                    compiledPackages,
                    assets,
                    configurationValidation.IsValid,
                    issues);
        }

        static void ValidateGlobalFairy(ICollection<CommercialValidationIssue> issues)
        {
            var configurations = LoadAll<FairyApplicationConfiguration>();
            if (configurations.Length != 1)
            {
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-FAIRY-001",
                    "Assets",
                    "Fairy",
                    $"Exactly one FairyApplicationConfiguration is required; found {configurations.Length}.");
                return;
            }

            var configuration = configurations[0];
            if (!configuration.IsValid(out var reason))
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-FAIRY-002",
                    AssetDatabase.GetAssetPath(configuration),
                    "Fairy",
                    reason);
        }

        static void ValidateGlobalUiDefaults(ICollection<CommercialValidationIssue> issues)
        {
            var defaults = LoadAll<GlobalUiDefaults>();
            if (defaults.Length != 1) { CommercialArchitectureValidator.Add(issues, "COM-SHELL-001", "Assets", "FrontendShell", $"Exactly one GlobalUiDefaults is required; found {defaults.Length}."); return; }
            var item = defaults[0];
            var path = AssetDatabase.GetAssetPath(item);
            if (!PositiveFinite(item.TitleStyle.FontSize) || !PositiveFinite(item.TitleStyle.Width) ||
                !PositiveFinite(item.SubtitleStyle.FontSize) || !PositiveFinite(item.SubtitleStyle.Width) ||
                !PositiveFinite(item.Layout.PanelSize.x) || !PositiveFinite(item.Layout.PanelSize.y) ||
                !Finite(item.Layout.PanelOffset.x) || !Finite(item.Layout.PanelOffset.y) ||
                !Finite(item.Layout.Spacing) || item.Layout.Spacing < 0f ||
                !Finite(item.Animation.TransitionSeconds) || item.Animation.TransitionSeconds < 0f)
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-002", path, "FrontendShell", "Global frontend defaults contain invalid sizes, offsets, spacing, or transition duration.");
        }

        static bool PositiveFinite(float value) => value > 0f && Finite(value);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static void ValidateUniqueNames(IEnumerable<ContentSceneConfig> scenes, ICollection<CommercialValidationIssue> issues)
        {
            foreach (var group in scenes.GroupBy(x => x.SerializedSceneId, StringComparer.Ordinal).Where(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() > 1))
                foreach (var scene in group)
                    CommercialArchitectureValidator.Add(issues, "COM-CONTENT-001", AssetDatabase.GetAssetPath(scene), "Configuration", string.IsNullOrWhiteSpace(group.Key) ? "SceneId is empty." : $"SceneId '{group.Key}' is not unique.");
        }

        static void ValidateScene(ContentSceneConfig scene, ValidationScope scope, ICollection<CommercialValidationIssue> issues)
        {
            var path = AssetDatabase.GetAssetPath(scene);
            var content = scene.Content;
            if (content == null) { CommercialArchitectureValidator.Add(issues, "COM-CONTENT-002", path, "Configuration", "Content section is missing."); return; }
            if (scene.Presentation == null) CommercialArchitectureValidator.Add(issues, "COM-CONTENT-003", path, "FrontendShell", "Presentation section is missing.");

            if (CommercialArchitectureValidator.Includes(scope, ValidationScope.Video) && content.Video != null)
                ValidateSource(path, "Video", content.Video.Clip, content.Video.StreamingAssetsPath, issues, "COM-VIDEO-001");
            if (CommercialArchitectureValidator.Includes(scope, ValidationScope.Panorama) && content.Panorama != null)
            {
                ValidateSource(path, "Panorama", content.Panorama.Texture, content.Panorama.StreamingAssetsPath, issues, "COM-PANORAMA-001");
                ValidatePanoramaMoments(path, content.Panorama, issues);
            }
            if (CommercialArchitectureValidator.Includes(scope, ValidationScope.ImageRing) && content.ImageRing != null)
                ValidateImageRing(scene, content, scope != ValidationScope.All, issues);
            if (CommercialArchitectureValidator.Includes(scope, ValidationScope.Narration) && content.Narration != null && content.Narration.Clip == null)
                CommercialArchitectureValidator.Add(issues, "COM-NARRATION-001", path, "Narration", "Configured narration AudioClip is missing.");
            if (CommercialArchitectureValidator.Includes(scope, ValidationScope.Effect) && content.Effect != null && content.Effect.Prefab == null)
                CommercialArchitectureValidator.Add(issues, "COM-EFFECT-001", path, "Effect", "Configured effect prefab is missing.");
            if (CommercialArchitectureValidator.Includes(scope, ValidationScope.Model) && content.Model != null)
                ValidateModel(path, content.Model, issues);
            if (scope == ValidationScope.All)
                ValidateSceneOwnedDependencies(scene, content, issues);

            foreach (var dependency in AssetDatabase.GetDependencies(path, true))
                if (dependency.IndexOf("Content/XREAL", StringComparison.OrdinalIgnoreCase) >= 0)
                    CommercialArchitectureValidator.Add(issues, "COM-CONTENT-004", path, "Configuration", $"Production content still depends on retired XREAL layout: {dependency}");
        }

        static void ValidateModel(string path, ModelContentSpec model, ICollection<CommercialValidationIssue> issues)
        {
            if (model.SourceKind == ModelSourceKind.Prefab && model.Prefab == null)
                CommercialArchitectureValidator.Add(issues, "COM-MODEL-001", path, "Model", "Prefab model reference is missing.");
            if (model.SourceKind == ModelSourceKind.Glb)
                ValidateSource(path, "Model", model.GlbAsset, model.GlbStreamingAssetsPath, issues, "COM-MODEL-002");
            if (!Finite(model.BobAmplitude) || model.BobAmplitude < 0f || !Finite(model.BobFrequency) || model.BobFrequency < 0f)
                CommercialArchitectureValidator.Add(issues, "COM-MODEL-007", path, "Model", "Model bob amplitude and frequency must be finite and non-negative.");
            var animation = model.Animation;
            if (animation == null) return;
            if (animation.Driver == ModelAnimationDriver.AnimationClip && animation.Clip == null)
                CommercialArchitectureValidator.Add(issues, "COM-MODEL-003", path, "Model", "AnimationClip driver has no clip.");
            if (animation.Driver != ModelAnimationDriver.AnimatorController) return;
            if (animation.Controller == null) { CommercialArchitectureValidator.Add(issues, "COM-MODEL-004", path, "Model", "Animator driver has no controller."); return; }
            var controller = animation.Controller as AnimatorController;
            if (controller == null) { CommercialArchitectureValidator.Add(issues, "COM-MODEL-005", path, "Model", "Animator driver must reference an AnimatorController asset."); return; }
            if (!ContainsState(controller, animation.InitialState))
                CommercialArchitectureValidator.Add(issues, "COM-MODEL-006", AssetDatabase.GetAssetPath(controller), "Model", $"Animator state '{animation.InitialState}' does not exist in the controller.");
        }

        static void ValidateImageRing(
            ContentSceneConfig scene,
            ContentSpec content,
            bool validateOwnership,
            ICollection<CommercialValidationIssue> issues)
        {
            var path = AssetDatabase.GetAssetPath(scene);
            var ring = content.ImageRing;
            if (content.Panorama == null)
                CommercialArchitectureValidator.Add(issues, "COM-IMAGE-RING-001", path, "ImageRing", "ImageRing requires a Panorama entry in the same scene.");
            if (ring.Items.Count < 3 || ring.Items.Count > 8)
            {
                CommercialArchitectureValidator.Add(issues, "COM-IMAGE-RING-002", path, "ImageRing", "ImageRing requires 3 to 8 items.");
                return;
            }

            var textureIds = new HashSet<int>();
            var audioIds = new HashSet<int>();
            var ownerRoot = $"Assets/BotanicalGardenQR/Content/Scenes/{scene.SerializedSceneId}/";
            for (var index = 0; index < ring.Items.Count; index++)
            {
                var item = ring.Items[index];
                if (item == null || item.Image == null || string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Description))
                {
                    CommercialArchitectureValidator.Add(issues, "COM-IMAGE-RING-003", path, "ImageRing", $"ImageRing item {index} requires a texture, title, and description.");
                    continue;
                }
                if (!textureIds.Add(item.Image.GetInstanceID()))
                    CommercialArchitectureValidator.Add(issues, "COM-IMAGE-RING-004", path, "ImageRing", $"ImageRing item {index} reuses a texture.");
                if (item.Audio == null)
                    CommercialArchitectureValidator.Add(issues, "COM-IMAGE-RING-005", path, "ImageRing", $"ImageRing item {index} requires an AudioClip.");
                else if (!audioIds.Add(item.Audio.GetInstanceID()))
                    CommercialArchitectureValidator.Add(issues, "COM-IMAGE-RING-006", path, "ImageRing", $"ImageRing item {index} reuses an audio clip.");
                if (validateOwnership)
                {
                    ValidateOwnedReference(path, ownerRoot, $"ImageRing item {index}", item.Image, issues);
                    ValidateOwnedReference(path, ownerRoot, $"ImageRing item {index} audio", item.Audio, issues);
                }
            }
        }

        static void ValidateSceneOwnedDependencies(ContentSceneConfig scene, ContentSpec content, ICollection<CommercialValidationIssue> issues)
        {
            var scenePath = AssetDatabase.GetAssetPath(scene);
            var sceneId = scene.SerializedSceneId;
            if (string.IsNullOrWhiteSpace(sceneId)) return;
            var ownerRoot = $"Assets/BotanicalGardenQR/Content/Scenes/{sceneId}/";

            ValidateOwnedReference(scenePath, ownerRoot, "Video", content.Video?.Clip, issues);
            ValidateOwnedReference(scenePath, ownerRoot, "Panorama", content.Panorama?.Texture, issues);
            if (content.Panorama != null)
                for (var index = 0; index < content.Panorama.EnvironmentMoments.Count; index++)
                {
                    var profile = content.Panorama.EnvironmentMoments[index];
                    ValidateSceneOrSharedReference(
                        scenePath,
                        ownerRoot,
                        $"Panorama environment moment {index}",
                        profile,
                        issues);
                    ValidateSceneOrSharedReference(
                        scenePath,
                        ownerRoot,
                        $"Panorama environment moment {index} prefab",
                        profile?.Prefab,
                        issues);
                }
            if (content.ImageRing != null)
                for (var index = 0; index < content.ImageRing.Items.Count; index++)
                {
                    ValidateOwnedReference(
                        scenePath,
                        ownerRoot,
                        $"ImageRing item {index}",
                        content.ImageRing.Items[index]?.Image,
                        issues);
                    ValidateOwnedReference(
                        scenePath,
                        ownerRoot,
                        $"ImageRing item {index} audio",
                        content.ImageRing.Items[index]?.Audio,
                        issues);
                }
            ValidateOwnedReference(scenePath, ownerRoot, "Narration", content.Narration?.Clip, issues);
            if (content.Model != null)
            {
                ValidateOwnedReference(scenePath, ownerRoot, "Model prefab", content.Model.Prefab, issues);
                ValidateOwnedReference(scenePath, ownerRoot, "Model GLB", content.Model.GlbAsset, issues);
                ValidateOwnedReference(scenePath, ownerRoot, "Model material", content.Model.MaterialOverride, issues);
                if (content.Model.Animation != null)
                {
                    ValidateOwnedReference(scenePath, ownerRoot, "Model animation controller", content.Model.Animation.Controller, issues);
                    ValidateOwnedReference(scenePath, ownerRoot, "Model animation clip", content.Model.Animation.Clip, issues);
                }
            }

            foreach (var dependency in AssetDatabase.GetDependencies(scenePath, true))
            {
                if (!dependency.StartsWith("Assets/BotanicalGardenQR/Content/Scenes/", StringComparison.Ordinal) ||
                    dependency.StartsWith(ownerRoot, StringComparison.Ordinal))
                {
                    ValidateContentDependencyClosure(scenePath, sceneId, ownerRoot, dependency, issues);
                    continue;
                }
                CommercialArchitectureValidator.Add(issues, "COM-CONTENT-LOCAL-002", scenePath, "Configuration",
                    $"Scene '{sceneId}' depends on another scene's content asset: {dependency}");
            }
        }

        static void ValidateContentDependencyClosure(string scenePath, string sceneId, string ownerRoot, string dependency, ICollection<CommercialValidationIssue> issues)
        {
            if (!dependency.StartsWith("Assets/BotanicalGardenQR/", StringComparison.Ordinal) ||
                !ContentMediaExtensions.Contains(Path.GetExtension(dependency)) ||
                dependency.StartsWith(ownerRoot, StringComparison.Ordinal) ||
                dependency.StartsWith("Assets/BotanicalGardenQR/Content/Shared/", StringComparison.Ordinal))
                return;

            CommercialArchitectureValidator.Add(issues, "COM-CONTENT-LOCAL-003", scenePath, "Configuration",
                $"Scene '{sceneId}' has a media dependency outside its content closure: {dependency}. Move scene media to '{ownerRoot}' or explicitly shared system media to Content/Shared.");
        }

        static void ValidateOwnedReference(string scenePath, string ownerRoot, string label, UnityEngine.Object asset, ICollection<CommercialValidationIssue> issues)
        {
            if (asset == null) return;
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith(ownerRoot, StringComparison.Ordinal))
                CommercialArchitectureValidator.Add(issues, "COM-CONTENT-LOCAL-001", scenePath, "Configuration",
                    $"{label} must be authored under '{ownerRoot}', but resolves to '{assetPath}'.");
        }

        static void ValidateSceneOrSharedReference(
            string scenePath,
            string ownerRoot,
            string label,
            UnityEngine.Object asset,
            ICollection<CommercialValidationIssue> issues)
        {
            if (asset == null) return;
            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrWhiteSpace(assetPath) &&
                (assetPath.StartsWith(ownerRoot, StringComparison.Ordinal) ||
                 assetPath.StartsWith("Assets/BotanicalGardenQR/Content/Shared/", StringComparison.Ordinal)))
                return;

            CommercialArchitectureValidator.Add(
                issues,
                "COM-CONTENT-LOCAL-004",
                scenePath,
                "Configuration",
                $"{label} must be authored under '{ownerRoot}' or Content/Shared, but resolves to '{assetPath}'.");
        }

        static void ValidatePanoramaMoments(
            string path,
            PanoramaContentSpec panorama,
            ICollection<CommercialValidationIssue> issues)
        {
            if (panorama.EnvironmentMoments.Count > 4)
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-PANORAMA-002",
                    path,
                    "Panorama",
                    "Panorama supports at most four configured environment moments.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < panorama.EnvironmentMoments.Count; index++)
            {
                var profile = panorama.EnvironmentMoments[index];
                var reason = string.Empty;
                if (profile == null || !profile.IsValid(out reason))
                {
                    CommercialArchitectureValidator.Add(
                        issues,
                        "COM-PANORAMA-003",
                        path,
                        "Panorama",
                        profile == null ? $"Environment moment {index} is missing." : reason);
                    continue;
                }
                if (!ids.Add(profile.MomentId))
                    CommercialArchitectureValidator.Add(
                        issues,
                        "COM-PANORAMA-004",
                        path,
                        "Panorama",
                        $"Environment moment ID '{profile.MomentId}' is duplicated.");
            }
        }

        static bool ContainsState(AnimatorController controller, string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName)) return false;
            foreach (var layer in controller.layers)
                if (ContainsState(layer.stateMachine, stateName)) return true;
            return false;
        }

        static bool ContainsState(AnimatorStateMachine machine, string stateName)
        {
            if (machine.states.Any(x => string.Equals(x.state.name, stateName, StringComparison.Ordinal))) return true;
            return machine.stateMachines.Any(x => ContainsState(x.stateMachine, stateName));
        }

        static void ValidateSource(string assetPath, string line, UnityEngine.Object imported, string relativePath, ICollection<CommercialValidationIssue> issues, string code)
        {
            var hasImported = imported != null;
            var hasPath = !string.IsNullOrWhiteSpace(relativePath);
            if (hasImported == hasPath) { CommercialArchitectureValidator.Add(issues, code, assetPath, line, "Exactly one imported asset or StreamingAssets path is required."); return; }
            if (!hasPath) return;
            var normalized = relativePath.Trim().Replace('\\', '/');
            var root = Path.GetFullPath(Application.streamingAssetsPath) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, normalized));
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
                CommercialArchitectureValidator.Add(issues, code, assetPath, line, $"StreamingAssets dependency does not exist or escapes its root: {relativePath}");
        }

        static void ValidatePublishedLibrary(
            ContentSceneConfig[] scenes,
            ScenePackage[] compiledPackages,
            ConfigurationAssetSet assets,
            bool configurationIsValid,
            ICollection<CommercialValidationIssue> issues)
        {
            var libraries = LoadAll<ContentSceneLibrary>();
            if (libraries.Length != 1) { CommercialArchitectureValidator.Add(issues, "COM-LIBRARY-001", "Assets", "Configuration", $"Exactly one published ContentSceneLibrary is required; found {libraries.Length}."); return; }
            var library = libraries[0];
            if (configurationIsValid &&
                !string.Equals(
                    library.SourceDigest,
                    ConfigurationInputDigest.ComputeContentLibrarySourceDigest(assets),
                    StringComparison.Ordinal))
                CommercialArchitectureValidator.Add(issues, "COM-LIBRARY-002", AssetDatabase.GetAssetPath(library), "Configuration", "Published library digest is stale or was modified outside the publish workflow.");
            var sourceNames = new HashSet<string>(scenes.Select(x => x.SerializedSceneId), StringComparer.Ordinal);
            var packageNames = new HashSet<string>(library.Packages.Where(x => x != null).Select(x => x.SceneId.Value), StringComparer.Ordinal);
            if (!sourceNames.SetEquals(packageNames))
                CommercialArchitectureValidator.Add(issues, "COM-LIBRARY-003", AssetDatabase.GetAssetPath(library), "Configuration", "Published library has missing or extra SceneId entries.");
            if (configurationIsValid &&
                !PublishedPayloadMatches(library, compiledPackages))
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-LIBRARY-005",
                    AssetDatabase.GetAssetPath(library),
                    "Configuration",
                    "Published library payload does not match the compiled content-scene authoring input.");
            foreach (var package in library.Packages.Where(x => x != null))
                foreach (var page in package.AvailablePages)
                    if (!Enum.IsDefined(typeof(FeaturePageId), page))
                        CommercialArchitectureValidator.Add(issues, "COM-LIBRARY-004", AssetDatabase.GetAssetPath(library), "Configuration", $"Published SceneId '{package.SceneId}' contains an obsolete or undefined foreground page value '{page}'.");
        }

        static bool PublishedPayloadMatches(
            ContentSceneLibrary library,
            IReadOnlyList<ScenePackage> compiledPackages)
        {
            if (library.Packages.Count != compiledPackages.Count) return false;
            for (var index = 0; index < compiledPackages.Count; index++)
            {
                var published = library.Packages[index];
                var compiled = compiledPackages[index];
                if (published == null || compiled == null || published.SceneId != compiled.SceneId ||
                    !string.Equals(
                        JsonUtility.ToJson(published),
                        JsonUtility.ToJson(compiled),
                        StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        static T[] LoadAll<T>() where T : UnityEngine.Object => FindPaths<T>().Select(AssetDatabase.LoadAssetAtPath<T>).Where(x => x != null).ToArray();
        static IEnumerable<string> FindPaths<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets($"t:{typeof(T).Name}").Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x, StringComparer.Ordinal);
    }
}
