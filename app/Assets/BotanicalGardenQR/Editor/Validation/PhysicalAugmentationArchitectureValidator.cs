using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BotanicalGardenQR.Configuration.Editor;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Editor.Validation
{
    internal static class PhysicalAugmentationArchitectureValidator
    {
        const string AuthoringPath =
            "Assets/BotanicalGardenQR/Content/Authoring/PhysicalAugmentationCatalog.asset";
        const string PublishedPath =
            "Assets/BotanicalGardenQR/Content/Published/PhysicalAugmentationCatalog.asset";
        const string ModuleRoot = "Assets/BotanicalGardenQR/Modules/PhysicalAugmentation";
        const string ConfigurationEditorRoot =
            "Assets/BotanicalGardenQR/Configuration/Editor";
        const string ConfigurationEditorAsmdef =
            ConfigurationEditorRoot + "/BotanicalGardenQR.Configuration.Editor.asmdef";
        const string ContentPublishTool = "Tools/Publish-BotanicalGardenContent.ps1";
        const string VisitorRuntimePrefabPath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab";
        const string PackageManifestPath = "Packages/manifest.json";
        const string PackageLockPath = "Packages/packages-lock.json";
        const string OpenXrSettingsPath = "Assets/XR/Settings/OpenXR Package Settings.asset";
        const string AndroidManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        const string OculusProjectConfigPath = "Assets/Oculus/OculusProjectConfig.asset";

        static readonly string[] RetiredCueAndPreviewPaths =
        {
            ModuleRoot + "/Frontend/PhysicalAugmentationPresenter.cs",
            ModuleRoot + "/Frontend/PhysicalAugmentationPulseCue.cs",
            ModuleRoot + "/Frontend/AssemblyInfo.cs",
            ModuleRoot + "/Frontend/BotanicalGardenQR.PhysicalAugmentation.Frontend.asmdef",
            "Assets/BotanicalGardenQR/Content/Shared/PhysicalAugmentation/physical_point_001/Prefabs/PhysicalPoint001VisitorCue.prefab",
            "Assets/BotanicalGardenQR/Content/Shared/PhysicalAugmentation/physical_point_001/Prefabs/PhysicalPoint001CalibrationPreview.prefab",
            "Assets/BotanicalGardenQR/Content/Shared/PhysicalAugmentation/physical_point_001/Materials/PhysicalPoint001Cue.mat",
            "Assets/BotanicalGardenQR/Content/Shared/PhysicalAugmentation/physical_point_001/Materials/PhysicalPoint001PreviewProxy.mat",
            "Assets/BotanicalGardenQR/Content/Shared/PhysicalAugmentation/physical_point_001/Materials/PhysicalPoint001Flower.mat",
            "Assets/BotanicalGardenQR/Content/Shared/PhysicalAugmentation/physical_point_001/Materials/PhysicalPoint001FlowerCenter.mat",
            "Assets/BotanicalGardenQR/Content/Shared/PhysicalAugmentation/physical_point_002/Prefabs/PhysicalPoint002VisitorCue.prefab",
            "Assets/BotanicalGardenQR/Content/Shared/PhysicalAugmentation/physical_point_002/Prefabs/PhysicalPoint002CalibrationPreview.prefab"
        };

        static readonly string[] RetiredCatalogFields =
        {
            "_calibrationPreviewPrefab",
            "_visitorCuePrefab",
            "_activationDistanceMeters",
            "_focusConeDegrees",
            "_dwellSeconds"
        };

        static readonly Regex RetiredStoreDeclaration = new Regex(
            @"\bclass\s+AnchorUuidStore\b",
            RegexOptions.Compiled);

        public static void Validate(
            ValidationScope scope,
            ICollection<CommercialValidationIssue> issues)
        {
            if (!CommercialArchitectureValidator.Includes(
                    scope,
                    ValidationScope.PhysicalAugmentation))
                return;

            ValidateCatalogs(issues);
            ValidateRetiredCueAndPreviewPathIsAbsent(issues);
            ValidateAssemblyOwnership(issues);
            ValidateAuthoringOwnership(issues);
            ValidateAnchorApiLocality(issues);
            ValidateFeaturePageEntryAndEnvironmentDepth(issues);
        }

        static void ValidateCatalogs(ICollection<CommercialValidationIssue> issues)
        {
            var authoringAssets = FindAssets<PhysicalAugmentationCatalogAuthoringAsset>();
            var publishedAssets = FindAssets<PhysicalAugmentationCatalogAsset>();

            RequireSingleAsset(authoringAssets, AuthoringPath, "authoring catalog", issues);
            RequireSingleAsset(publishedAssets, PublishedPath, "published catalog", issues);
            if (authoringAssets.Length != 1 || publishedAssets.Length != 1)
                return;

            var authoring = authoringAssets[0].Asset;
            var published = publishedAssets[0].Asset;
            if (!authoring.TryBuild(out var authoringCatalog, out var authoringError))
            {
                Add(issues, "COM-PHYSICAL-002", authoringAssets[0].Path,
                    authoringError);
                return;
            }
            if (!published.TryBuild(out var publishedCatalog, out var publishedError))
            {
                Add(issues, "COM-PHYSICAL-003", publishedAssets[0].Path,
                    publishedError);
                return;
            }

            if (string.IsNullOrWhiteSpace(published.SourceDigest) ||
                published.PublishedVersion <= 0 ||
                string.IsNullOrWhiteSpace(published.PublishedAtUtc) ||
                !DateTimeOffset.TryParse(published.PublishedAtUtc, out _) ||
                !string.Equals(
                    published.SourceDigest,
                    ConfigurationInputDigest.ComputePhysicalAugmentationSourceDigest(
                        authoring),
                    StringComparison.Ordinal))
                Add(issues, "COM-PHYSICAL-004", publishedAssets[0].Path,
                    "Published physical catalog metadata or authoring-source digest is stale." );

            var authoringDefinitions = authoringCatalog.Definitions;
            var publishedDefinitions = publishedCatalog.Definitions;
            if (authoringDefinitions.Count != publishedDefinitions.Count)
            {
                Add(issues, "COM-PHYSICAL-005", publishedAssets[0].Path,
                    "Published physical points do not match the authoring catalog.");
                return;
            }

            for (var index = 0; index < authoringDefinitions.Count; index++)
            {
                var source = authoringDefinitions[index];
                var output = publishedDefinitions[index];
                if (!Equivalent(source, output))
                    Add(issues, "COM-PHYSICAL-005", publishedAssets[0].Path,
                        $"Published point '{output.PointId}' differs from authoring.");
                ValidateClosedAssetRoot(source, authoringAssets[0].Path, issues);
            }
        }

        static void ValidateClosedAssetRoot(
            PhysicalAugmentationDefinition definition,
            string ownerPath,
            ICollection<CommercialValidationIssue> issues)
        {
            var expectedRoot =
                $"Assets/BotanicalGardenQR/Content/Shared/PhysicalAugmentation/{definition.PointId.Value}/";
            foreach (var asset in new[]
                     {
                         definition.PerformancePrefab,
                         definition.DepthProxyPrefab
                     })
            {
                var path = AssetDatabase.GetAssetPath(asset);
                if (!path.StartsWith(expectedRoot, StringComparison.Ordinal))
                    Add(issues, "COM-PHYSICAL-006", ownerPath,
                        $"Point '{definition.PointId}' asset '{path}' is outside its closed content root '{expectedRoot}'.");
            }
        }

        static bool Equivalent(
            PhysicalAugmentationDefinition left,
            PhysicalAugmentationDefinition right)
            => left.PointId == right.PointId &&
               string.Equals(left.AdminDisplayName, right.AdminDisplayName,
                   StringComparison.Ordinal) &&
               left.InstallationAnchorNumber == right.InstallationAnchorNumber &&
               left.PerformancePrefab == right.PerformancePrefab &&
               left.DepthProxyPrefab == right.DepthProxyPrefab &&
               Approximately(left.StabilitySeconds, right.StabilitySeconds) &&
               Approximately(left.PositionToleranceMeters, right.PositionToleranceMeters) &&
               Approximately(left.OrientationToleranceDegrees, right.OrientationToleranceDegrees) &&
               Approximately(left.LostGraceSeconds, right.LostGraceSeconds) &&
               Approximately(left.RebasePositionThresholdMeters, right.RebasePositionThresholdMeters) &&
               Approximately(left.RebaseOrientationThresholdDegrees, right.RebaseOrientationThresholdDegrees) &&
               Approximately(left.MinimumCalibrationScale, right.MinimumCalibrationScale) &&
               Approximately(left.MaximumCalibrationScale, right.MaximumCalibrationScale) &&
               left.OcclusionPolicy == right.OcclusionPolicy &&
               Quaternion.Angle(left.ModelRotationInAnchorSpace, right.ModelRotationInAnchorSpace) <= 0.0001f &&
               Approximately(left.ModelUniformScale, right.ModelUniformScale);

        static bool Approximately(float left, float right)
            => Mathf.Abs(left - right) <= 0.0001f;

        static void ValidateRetiredCueAndPreviewPathIsAbsent(
            ICollection<CommercialValidationIssue> issues)
        {
            foreach (var path in RetiredCueAndPreviewPaths)
                if (File.Exists(Path.GetFullPath(path)))
                    Add(issues, "COM-PHYSICAL-018", path,
                        "Retired spatial Cue/Calibration Preview production assets and code must not return.");

            foreach (var path in new[] { AuthoringPath, PublishedPath })
            {
                var absolutePath = Path.GetFullPath(path);
                if (!File.Exists(absolutePath)) continue;
                var serialized = File.ReadAllText(absolutePath);
                foreach (var marker in RetiredCatalogFields.Where(serialized.Contains))
                    Add(issues, "COM-PHYSICAL-018", path,
                        $"Retired catalog field '{marker}' must not return; the feature action is the only visitor entry.");
            }
        }

        static void ValidateAssemblyOwnership(
            ICollection<CommercialValidationIssue> issues)
        {
            RequireReferences(
                $"{ModuleRoot}/Contracts/BotanicalGardenQR.PhysicalAugmentation.Contracts.asmdef",
                Array.Empty<string>(),
                issues);
            RequireReferences(
                $"{ModuleRoot}/Installation/BotanicalGardenQR.PhysicalAugmentation.Installation.asmdef",
                new[] { "BotanicalGardenQR.PhysicalAugmentation.Contracts" },
                issues);
            RequireReferences(
                $"{ModuleRoot}/Backend/BotanicalGardenQR.PhysicalAugmentation.Backend.asmdef",
                new[]
                {
                    "BotanicalGardenQR.PhysicalAugmentation.Contracts",
                    "BotanicalGardenQR.PhysicalAugmentation.Installation",
                    "Meta.XR.EnvironmentDepth",
                    "Oculus.VR"
                },
                issues);
        }

        static void RequireReferences(
            string path,
            IReadOnlyCollection<string> expected,
            ICollection<CommercialValidationIssue> issues)
        {
            var absolutePath = Path.GetFullPath(path);
            if (!File.Exists(absolutePath))
            {
                Add(issues, "COM-PHYSICAL-007", path,
                    "Required PhysicalAugmentation assembly definition is missing.");
                return;
            }
            var data = JsonUtility.FromJson<AsmdefJson>(File.ReadAllText(absolutePath));
            var actual = new HashSet<string>(
                data?.references ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            if (!actual.SetEquals(expected))
                Add(issues, "COM-PHYSICAL-007", path,
                    $"Assembly references must be exactly: {string.Join(", ", expected)}.");
        }

        static void ValidateAnchorApiLocality(
            ICollection<CommercialValidationIssue> issues)
        {
            foreach (var path in AssetDatabase.FindAssets(
                         "t:MonoScript",
                         new[] { "Assets/BotanicalGardenQR" })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .Where(path => !path.Contains("/Editor/") &&
                                    !path.Contains("/Tests/")))
            {
                var source = File.ReadAllText(Path.GetFullPath(path));
                if (RetiredStoreDeclaration.IsMatch(source))
                    Add(issues, "COM-PHYSICAL-008", path,
                        "Retired AnchorUuidStore production class must not return.");

                if (!source.Contains("OVRSpatialAnchor") &&
                    !source.Contains("AnchorUuid"))
                    continue;
                if (path.StartsWith(
                        $"{ModuleRoot}/Backend/",
                        StringComparison.Ordinal) ||
                    path.StartsWith(
                        $"{ModuleRoot}/Installation/",
                        StringComparison.Ordinal) ||
                    path.StartsWith(
                        "Assets/BotanicalGardenQR/SpatialAnchorAdmin/",
                        StringComparison.Ordinal))
                    continue;
                Add(issues, "COM-PHYSICAL-009", path,
                    "Meta anchor UUID/platform APIs may only live in PhysicalAugmentation Backend/Installation or SpatialAnchorAdmin.");
            }
        }

        static void ValidateAuthoringOwnership(
            ICollection<CommercialValidationIssue> issues)
        {
            var asmdefPath = Path.GetFullPath(ConfigurationEditorAsmdef);
            if (!File.Exists(asmdefPath))
            {
                Add(issues, "COM-PHYSICAL-010", ConfigurationEditorAsmdef,
                    "Configuration Editor assembly definition is missing.");
            }
            else
            {
                var data = JsonUtility.FromJson<AsmdefJson>(File.ReadAllText(asmdefPath));
                var forbidden = new HashSet<string>(new[]
                {
                    "BotanicalGardenQR.PhysicalAugmentation.Backend",
                    "BotanicalGardenQR.PhysicalAugmentation.Frontend"
                }, StringComparer.Ordinal);
                foreach (var reference in data?.references ?? Array.Empty<string>())
                    if (forbidden.Contains(reference))
                        Add(issues, "COM-PHYSICAL-010", ConfigurationEditorAsmdef,
                            $"Configuration authoring must not depend on concrete PhysicalAugmentation assembly '{reference}'.");
            }

            var forbiddenSourceMarkers = new[]
            {
                "BotanicalGardenQR.PhysicalAugmentation.Backend",
                "BotanicalGardenQR.PhysicalAugmentation.Frontend",
                "PhysicalAugmentationContentAuthoring",
                "EnsureFirstCommercialPoint"
            };
            foreach (var path in AssetDatabase.FindAssets(
                         "t:MonoScript",
                         new[] { ConfigurationEditorRoot })
                     .Select(AssetDatabase.GUIDToAssetPath))
            {
                var source = File.ReadAllText(Path.GetFullPath(path));
                foreach (var marker in forbiddenSourceMarkers.Where(source.Contains))
                    Add(issues, "COM-PHYSICAL-010", path,
                        $"Configuration publishing must consume authored Point assets and cannot restore point-specific generator marker '{marker}'.");
            }

            var publishToolPath = Path.GetFullPath(ContentPublishTool);
            if (!File.Exists(publishToolPath))
            {
                Add(issues, "COM-PHYSICAL-011", ContentPublishTool,
                    "Standard content publish tool is missing.");
                return;
            }
            var publishTool = File.ReadAllText(publishToolPath);
            foreach (var marker in new[]
                     {
                         "Content\\Authoring\\PhysicalAugmentationCatalog.asset",
                         "Content\\Shared\\PhysicalAugmentation"
                     })
                if (publishTool.Contains(marker))
                    Add(issues, "COM-PHYSICAL-011", ContentPublishTool,
                        "Standard publishing may copy generated outputs only; PhysicalAugmentation authoring assets and content closures are project inputs, not mirror outputs.");
        }

        static void ValidateFeaturePageEntryAndEnvironmentDepth(
            ICollection<CommercialValidationIssue> issues)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorRuntimePrefabPath);
            if (prefab == null)
            {
                Add(issues, "COM-PHYSICAL-012", VisitorRuntimePrefabPath,
                    "VisitorRuntime prefab is missing.");
            }
            else
            {
                var actions = prefab.GetComponentsInChildren<ShellFlowActionTarget>(true)
                    .Where(target => target.Action == ShellFlowAction.FeaturePageAction)
                    .ToArray();
                if (actions.Length != 1)
                    Add(issues, "COM-PHYSICAL-012", VisitorRuntimePrefabPath,
                        $"VisitorRuntime requires exactly one feature-page Physical Augmentation action; found {actions.Length}.");
                else if (actions[0].Feature != FeaturePageId.Model ||
                         actions[0].transform.parent == null ||
                         !string.Equals(
                             actions[0].transform.parent.name,
                             "ModelControlSlot",
                             StringComparison.Ordinal))
                    Add(issues, "COM-PHYSICAL-012", VisitorRuntimePrefabPath,
                        "The Physical Augmentation action must be authored inside the Model control slot and target the Model page.");
                if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component =>
                        component != null && string.Equals(
                            component.GetType().FullName,
                            "BotanicalGardenQR.PhysicalAugmentation.Frontend.PhysicalAugmentationPresenter",
                            StringComparison.Ordinal)) ||
                    prefab.GetComponentsInChildren<Transform>(true).Any(transform =>
                        string.Equals(transform.name, "VisitorCues", StringComparison.Ordinal)))
                    Add(issues, "COM-PHYSICAL-013", VisitorRuntimePrefabPath,
                        "The retired spatial cue/gaze-dwell production entry must not remain in VisitorRuntime.");
            }

            RequireText(PackageManifestPath, text =>
                    Regex.IsMatch(text, "\\\"com\\.unity\\.xr\\.meta-openxr\\\"\\s*:\\s*\\\"2\\.5\\.1\\\""),
                "COM-PHYSICAL-014",
                "Packages manifest must pin Unity Meta OpenXR 2.5.1 for the current Unity/OpenXR baseline.",
                issues);
            RequireText(PackageLockPath, text =>
                    Regex.IsMatch(text,
                        "\\\"com\\.unity\\.xr\\.meta-openxr\\\"\\s*:\\s*\\{(?s:.*?)\\\"version\\\"\\s*:\\s*\\\"2\\.5\\.1\\\""),
                "COM-PHYSICAL-014",
                "Package lock must resolve Unity Meta OpenXR 2.5.1.",
                issues);
            RequireText(OpenXrSettingsPath, text =>
                    Regex.IsMatch(text,
                        @"m_Name:\s*AROcclusionFeature Android\s*\r?\n(?:.*\r?\n){0,5}?\s*m_enabled:\s*1\s*$",
                        RegexOptions.Multiline),
                "COM-PHYSICAL-015",
                "Android OpenXR settings must enable the Meta Quest Occlusion feature.",
                issues);
            RequireText(OpenXrSettingsPath, text =>
                    Regex.IsMatch(text,
                        @"m_Name:\s*ARSessionFeature Android\s*\r?\n(?:.*\r?\n){0,5}?\s*m_enabled:\s*1\s*$",
                        RegexOptions.Multiline),
                "COM-PHYSICAL-017",
                "Android OpenXR settings must enable Meta Quest Session because Occlusion depends on it.",
                issues);
            RequireText(AndroidManifestPath, text =>
                    !text.Contains("horizonos.permission.HEADSET_CAMERA"),
                "COM-PHYSICAL-016",
                "Environment Depth must not request raw HEADSET_CAMERA permission.",
                issues);

            var oculusProjectConfig = AssetDatabase.LoadMainAssetAtPath(OculusProjectConfigPath);
            if (oculusProjectConfig == null)
            {
                Add(issues, "COM-PHYSICAL-018", OculusProjectConfigPath,
                    "Meta XR project configuration is missing.");
            }
            else
            {
                var serialized = new SerializedObject(oculusProjectConfig);
                var cameraAccess = serialized.FindProperty("isPassthroughCameraAccessEnabled");
                if (cameraAccess == null || cameraAccess.propertyType != SerializedPropertyType.Boolean ||
                    cameraAccess.boolValue)
                    Add(issues, "COM-PHYSICAL-018", OculusProjectConfigPath,
                        "QR uses Meta Scene/Anchor trackables; raw Passthrough Camera Access must remain disabled so builds cannot inject HEADSET_CAMERA.");
            }
        }

        static void RequireText(
            string relativePath,
            Func<string, bool> predicate,
            string code,
            string message,
            ICollection<CommercialValidationIssue> issues)
        {
            var path = Path.GetFullPath(relativePath);
            if (!File.Exists(path) || !predicate(File.ReadAllText(path)))
                Add(issues, code, relativePath, message);
        }

        static AssetRef<T>[] FindAssets<T>() where T : UnityEngine.Object
            => AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new AssetRef<T>(
                    path,
                    AssetDatabase.LoadAssetAtPath<T>(path)))
                .Where(item => item.Asset != null)
                .ToArray();

        static void RequireSingleAsset<T>(
            IReadOnlyList<AssetRef<T>> assets,
            string expectedPath,
            string label,
            ICollection<CommercialValidationIssue> issues)
            where T : UnityEngine.Object
        {
            if (assets.Count != 1)
            {
                Add(issues, "COM-PHYSICAL-001", expectedPath,
                    $"Exactly one {label} is required; found {assets.Count}.");
                return;
            }
            if (!string.Equals(assets[0].Path, expectedPath,
                    StringComparison.Ordinal))
                Add(issues, "COM-PHYSICAL-001", assets[0].Path,
                    $"The {label} must live at '{expectedPath}'.");
        }

        static void Add(
            ICollection<CommercialValidationIssue> issues,
            string code,
            string path,
            string message)
            => CommercialArchitectureValidator.Add(
                issues,
                code,
                path,
                "PhysicalAugmentation",
                message);

        [Serializable]
        sealed class AsmdefJson
        {
            public string[] references;
        }

        readonly struct AssetRef<T> where T : UnityEngine.Object
        {
            public AssetRef(string path, T asset)
            {
                Path = path;
                Asset = asset;
            }

            public string Path { get; }
            public T Asset { get; }
        }
    }
}
