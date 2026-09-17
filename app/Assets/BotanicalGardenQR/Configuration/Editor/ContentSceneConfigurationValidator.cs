using System;
using System.Collections.Generic;
using System.Linq;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using BotanicalGardenQR.SpatialHost.Contracts;
using UnityEditor;

namespace BotanicalGardenQR.Configuration.Editor
{
    public static class ContentSceneConfigurationValidator
    {
        public static ConfigurationValidationResult ValidateAll()
        {
            ValidateAndCompile(out _, out var result);
            return result;
        }

        internal static ConfigurationAssetSet ValidateAndCompile(
            out ScenePackage[] packages,
            out ConfigurationValidationResult result)
        {
            var issues = new List<ConfigurationIssue>();
            var assets = ConfigurationAssetSet.Load(issues);
            var compiled = new List<ScenePackage>();
            var sceneIds = new Dictionary<string, string>(StringComparer.Ordinal);

            ValidateManagedReferences(assets.Library, issues);

            foreach (var scene in assets.Scenes)
            {
                var path = AssetDatabase.GetAssetPath(scene);
                if (!ValidateManagedReferences(scene, issues)) continue;
                var name = scene.SerializedSceneId;
                if (!string.IsNullOrEmpty(name) && sceneIds.TryGetValue(name, out var firstPath))
                    issues.Add(new ConfigurationIssue("CFG110", path, $"SceneId '{name}' duplicates '{firstPath}'."));
                else if (!string.IsNullOrEmpty(name))
                    sceneIds.Add(name, path);

                if (ContentSceneCompiler.TryCompile(scene, assets.Defaults, issues, out var package))
                    compiled.Add(package);
            }

            ValidateEnvironment(assets.Environment, issues, out var registeredKinds);
            ValidateFairy(assets.Fairy, issues);
            ValidateCollection(assets.CollectionCatalog, issues);
            ValidatePhysicalAugmentation(assets.PhysicalAugmentationAuthoring, issues);
            ValidatePhysicalAugmentationAssociations(
                assets.Scenes,
                assets.PhysicalAugmentationAuthoring,
                issues);
            ValidatePrologueTheme(assets.PrologueTheme, issues);
            ValidateCoachTheme(assets.CoachTheme, issues);
            ValidateEntries(assets.Entries, sceneIds.Keys, registeredKinds, issues);
            if (assets.Environment != null && assets.Environment.FieldbookEnabled)
                ValidateFieldbookMapEntries(assets.Entries, issues);
            VisitorMapPublicationValidator.Validate(issues);
            packages = compiled.OrderBy(package => package.SceneId.Value, StringComparer.Ordinal).ToArray();
            result = new ConfigurationValidationResult(issues);
            return assets;
        }

        static void ValidateFieldbookMapEntries(ContentEntryCatalog entries, ICollection<ConfigurationIssue> issues)
        {
            var map = VisitorMapConfiguration.Resolve(AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(VisitorMapPublicationValidator.DefinitionPath));
            if (map == null) return; // Map publication validation reports invalid geometry separately.
            var points = new HashSet<string>(map.points.Select(point => point.id), StringComparer.Ordinal);
            foreach (var point in points)
                if (entries == null || !entries.TryResolveMapPoint(point, out var entry) ||
                    entry.DisplayProfile == null || entry.DisplayProfile.HostMode != HostMode.ViewerFront)
                    issues.Add(new ConfigurationIssue("CFG147", point, "Each map point requires exactly one enabled fieldbook entry with ViewerFront placement."));
            if (entries == null) return;
            foreach (var entry in entries.Routes)
                if (entry != null && entry.Enabled && entry.SerializedEntryKind == RecognitionSourceKinds.Fieldbook.Value &&
                    !points.Contains(entry.MapPointId))
                    issues.Add(new ConfigurationIssue("CFG148", entry.EntryRouteId, "Fieldbook entry must reference a published map point."));
        }

        static bool ValidateManagedReferences(
            UnityEngine.Object asset,
            ICollection<ConfigurationIssue> issues)
        {
            if (asset == null || !SerializationUtility.HasManagedReferencesWithMissingTypes(asset))
                return true;

            var path = AssetDatabase.GetAssetPath(asset);
            foreach (var missing in SerializationUtility.GetManagedReferencesWithMissingTypes(asset))
                issues.Add(new ConfigurationIssue(
                    "CFG111",
                    path,
                    $"Managed reference type '{missing.assemblyName}:{missing.namespaceName}.{missing.className}' " +
                    $"(reference id {missing.referenceId}) is missing."));
            return false;
        }

        static void ValidateEnvironment(
            RuntimeEnvironmentOptions environment,
            ICollection<ConfigurationIssue> issues,
            out HashSet<SourceKind> registeredKinds)
        {
            registeredKinds = new HashSet<SourceKind>();
            if (environment == null) return;
            var path = AssetDatabase.GetAssetPath(environment);
            if (!IsFinitePositive(environment.ArrivalRadius) || !IsFinitePositive(environment.ArrivalExitRadius) ||
                environment.ArrivalExitRadius < environment.ArrivalRadius || !IsFinitePositive(environment.ArrivalStableSeconds))
                issues.Add(new ConfigurationIssue("CFG145", path, "Discovery arrival limits must be finite, positive and hysteretic."));
            if (environment.FieldbookEnabled)
            {
                registeredKinds.Add(RecognitionSourceKinds.Fieldbook);
                if (environment.RecognitionAdapters.Any(option => option != null && option.Enabled))
                    issues.Add(new ConfigurationIssue("CFG146", path, "Fieldbook arrival confirmation excludes live recognition adapters."));
            }
            if (!IsFiniteNonNegative(environment.RecognitionDebounceSeconds) ||
                !IsFinitePositive(environment.ScanConfirmationSeconds) ||
                !IsFiniteNonNegative(environment.ScanLostGraceSeconds) ||
                !IsFiniteNonNegative(environment.ScanFocusPaddingDegrees) ||
                environment.ScanFocusPaddingDegrees > 10f ||
                !IsFiniteNonNegative(environment.ScanGazeLostGraceSeconds) ||
                !IsFinitePositive(environment.VideoPrepareTimeoutSeconds))
                issues.Add(new ConfigurationIssue("CFG120", path, "Runtime timing values must be finite; scan confirmation and video prepare timeout must be positive, focus padding must be between 0 and 10 degrees, and grace/debounce values non-negative."));

            foreach (var option in environment.RecognitionAdapters)
            {
                if (option == null)
                {
                    issues.Add(new ConfigurationIssue("CFG121", path, "Recognition adapter options cannot contain null entries."));
                    continue;
                }
                try
                {
                    var kind = option.SourceKind;
                    if (option.Enabled && !registeredKinds.Add(kind))
                        issues.Add(new ConfigurationIssue("CFG122", path, $"Enabled recognition adapter kind '{kind}' is duplicated."));
                }
                catch (Exception exception)
                {
                    issues.Add(new ConfigurationIssue("CFG123", path, $"Invalid SourceKind '{option.SerializedSourceKind}': {exception.Message}"));
                }
            }
        }

        static void ValidateFairy(
            FairyApplicationConfiguration fairy,
            ICollection<ConfigurationIssue> issues)
        {
            if (fairy == null) return;
            if (!fairy.IsValid(out var reason))
                issues.Add(new ConfigurationIssue("CFG124", AssetDatabase.GetAssetPath(fairy), reason));
        }

        static void ValidateCollection(
            CollectionCatalogAsset collectionCatalog,
            ICollection<ConfigurationIssue> issues)
        {
            if (collectionCatalog == null) return;
            if (!collectionCatalog.TryBuild(out _, out var error))
                issues.Add(new ConfigurationIssue("CFG125", AssetDatabase.GetAssetPath(collectionCatalog), error));
            if (!collectionCatalog.TryValidatePresentation(out var presentationError))
                issues.Add(new ConfigurationIssue("CFG126", AssetDatabase.GetAssetPath(collectionCatalog), presentationError));
        }

        static void ValidatePhysicalAugmentation(
            PhysicalAugmentationCatalogAuthoringAsset catalog,
            ICollection<ConfigurationIssue> issues)
        {
            if (catalog == null) return;
            if (!catalog.TryBuild(out _, out var error))
                issues.Add(new ConfigurationIssue("CFG128", AssetDatabase.GetAssetPath(catalog), error));
        }

        internal static void ValidatePhysicalAugmentationAssociations(
            IReadOnlyList<ContentSceneConfig> scenes,
            PhysicalAugmentationCatalogAuthoringAsset catalog,
            ICollection<ConfigurationIssue> issues)
        {
            if (catalog == null || !catalog.TryBuild(out var definitions, out _)) return;
            var knownPoints = new HashSet<PhysicalAugmentationPointId>(
                definitions.Definitions.Select(definition => definition.PointId));
            foreach (var scene in scenes)
            {
                if (scene == null) continue;
                try
                {
                    var pointIds = scene.Content.ParsePhysicalAugmentationPointIds();
                    for (var index = 0; index < pointIds.Length; index++)
                        if (!knownPoints.Contains(pointIds[index]))
                            issues.Add(new ConfigurationIssue(
                                "CFG129",
                                AssetDatabase.GetAssetPath(scene),
                                $"Physical Augmentation point '{pointIds[index]}' is not present in the authoring catalog."));
                }
                catch (Exception)
                {
                    // ContentSceneCompiler owns the canonical-value diagnostic.
                }
            }
        }

        static void ValidatePrologueTheme(
            VisitorPrologueThemeAsset prologueTheme,
            ICollection<ConfigurationIssue> issues)
        {
            if (prologueTheme == null) return;
            if (!prologueTheme.IsValid(out var error))
                issues.Add(new ConfigurationIssue("CFG127", AssetDatabase.GetAssetPath(prologueTheme), error));
        }

        static void ValidateCoachTheme(
            VisitorCoachThemeAsset coachTheme,
            ICollection<ConfigurationIssue> issues)
        {
            if (coachTheme == null) return;
            if (!coachTheme.IsValid(out var error))
                issues.Add(new ConfigurationIssue("CFG114", AssetDatabase.GetAssetPath(coachTheme), error));
        }

        static void ValidateEntries(
            ContentEntryCatalog catalog,
            IEnumerable<string> sceneIds,
            ISet<SourceKind> registeredKinds,
            ICollection<ConfigurationIssue> issues)
        {
            if (catalog == null) return;
            var path = AssetDatabase.GetAssetPath(catalog);
            var knownScenes = new HashSet<string>(sceneIds, StringComparer.Ordinal);
            var enabledKeys = new HashSet<EntryKey>();
            var enabledSceneIds = new HashSet<string>(StringComparer.Ordinal);
            var routeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var route in catalog.Routes)
            {
                if (route == null)
                {
                    issues.Add(new ConfigurationIssue("CFG130", path, "Content entry routes cannot contain null entries."));
                    continue;
                }

                SourceKind kind = default;
                var kindIsValid = true;
                try { kind = route.EntryKind; }
                catch (Exception exception)
                {
                    issues.Add(new ConfigurationIssue("CFG131", path, $"Invalid entry kind '{route.SerializedEntryKind}': {exception.Message}"));
                    kindIsValid = false;
                }

                if (string.IsNullOrWhiteSpace(route.EntryRouteId) || !string.Equals(route.EntryRouteId, route.EntryRouteId.Trim(), StringComparison.Ordinal))
                    issues.Add(new ConfigurationIssue("CFG132", path, "EntryRouteId must be non-empty and canonical (no surrounding whitespace)."));
                else
                {
                    try { _ = new SceneId(route.EntryRouteId); }
                    catch (Exception exception)
                    {
                        issues.Add(new ConfigurationIssue("CFG133", path, $"Invalid EntryRouteId '{route.EntryRouteId}': {exception.Message}"));
                    }
                }

                if (!routeIds.Add(route.EntryRouteId))
                    issues.Add(new ConfigurationIssue("CFG134", path, $"EntryRouteId '{route.EntryRouteId}' is duplicated."));

                if (string.IsNullOrWhiteSpace(route.EntryValue) || !string.Equals(route.EntryValue, route.EntryValue.Trim(), StringComparison.Ordinal))
                    issues.Add(new ConfigurationIssue("CFG135", path, "EntryValue must be non-empty and canonical (no surrounding whitespace)."));

                if (Guid.TryParse(route.EntryValue, out _) || string.Equals(route.SerializedEntryKind, "spatial_anchor", StringComparison.Ordinal))
                    issues.Add(new ConfigurationIssue("CFG136", path, "A persistent anchor UUID cannot be a ContentEntry key."));

                try { _ = route.TargetSceneId; }
                catch (Exception exception)
                {
                    issues.Add(new ConfigurationIssue("CFG137", path, $"Invalid target SceneId '{route.SerializedTargetSceneId}': {exception.Message}"));
                }

                if (route.DisplayProfile == null)
                    issues.Add(new ConfigurationIssue("CFG138", path, "Every content entry requires a DisplayProfile."));
                else if (!route.DisplayProfile.IsValid(out var reason))
                    issues.Add(new ConfigurationIssue("CFG139", AssetDatabase.GetAssetPath(route.DisplayProfile), reason));

                if (!route.Enabled) continue;
                if (kindIsValid && !registeredKinds.Contains(kind))
                    issues.Add(new ConfigurationIssue("CFG140", path, $"Enabled entry kind '{kind}' has no enabled registered adapter."));
                if (!knownScenes.Contains(route.SerializedTargetSceneId))
                    issues.Add(new ConfigurationIssue("CFG141", path, $"Enabled entry targets unknown SceneId '{route.SerializedTargetSceneId}'."));
                if (!enabledSceneIds.Add(route.SerializedTargetSceneId))
                    issues.Add(new ConfigurationIssue("CFG142", path, $"SceneId '{route.SerializedTargetSceneId}' has more than one enabled ContentEntryRoute."));
                if (kindIsValid && !enabledKeys.Add(new EntryKey(kind, route.EntryValue)))
                    issues.Add(new ConfigurationIssue("CFG143", path, $"Enabled entry key '{kind}:{route.EntryValue}' is duplicated."));
            }
        }

        static bool IsFinitePositive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        static bool IsFiniteNonNegative(float value) => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        readonly struct EntryKey : IEquatable<EntryKey>
        {
            public EntryKey(SourceKind kind, string value) { Kind = kind; Value = value ?? string.Empty; }
            SourceKind Kind { get; }
            string Value { get; }
            public bool Equals(EntryKey other) => Kind == other.Kind && string.Equals(Value, other.Value, StringComparison.Ordinal);
            public override bool Equals(object obj) => obj is EntryKey other && Equals(other);
            public override int GetHashCode() => (Kind.GetHashCode() * 397) ^ StringComparer.Ordinal.GetHashCode(Value);
        }
    }
}
