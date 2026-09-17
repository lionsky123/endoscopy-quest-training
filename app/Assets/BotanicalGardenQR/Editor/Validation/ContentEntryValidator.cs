using System;
using System.Collections.Generic;
using System.Linq;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.SpatialHost.Contracts;
using UnityEditor;

namespace BotanicalGardenQR.Editor.Validation
{
    internal static class ContentEntryValidator
    {
        public static void Validate(ValidationScope scope, ICollection<CommercialValidationIssue> issues)
        {
            if (CommercialArchitectureValidator.Includes(scope, ValidationScope.SpatialHost))
            {
                foreach (var profilePath in AssetDatabase.FindAssets("t:DisplayProfile").Select(AssetDatabase.GUIDToAssetPath))
                {
                    var profile = AssetDatabase.LoadAssetAtPath<DisplayProfile>(profilePath);
                    if (profile != null && !profile.IsValid(out var reason))
                        CommercialArchitectureValidator.Add(issues, "COM-HOST-001", profilePath, "SpatialHost", reason);
                }
            }
            if (!CommercialArchitectureValidator.Includes(scope, ValidationScope.Activation)) return;
            var catalogs = AssetDatabase.FindAssets("t:ContentEntryCatalog").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ContentEntryCatalog>).Where(x => x != null).ToArray();
            if (catalogs.Length != 1) { CommercialArchitectureValidator.Add(issues, "COM-ACT-001", "Assets", "Activation", $"Exactly one ContentEntryCatalog is required; found {catalogs.Length}."); return; }
            var catalog = catalogs[0];
            var path = AssetDatabase.GetAssetPath(catalog);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var routeIds = new HashSet<string>(StringComparer.Ordinal);
            var sceneTargets = new HashSet<string>(StringComparer.Ordinal);
            var registeredKinds = LoadRegisteredKinds(issues);
            var sceneIds = new HashSet<string>(AssetDatabase.FindAssets("t:ContentSceneConfig").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ContentSceneConfig>).Where(x => x != null).Select(x => x.SerializedSceneId), StringComparer.Ordinal);
            foreach (var route in catalog.Routes)
            {
                if (route == null) { CommercialArchitectureValidator.Add(issues, "COM-ACT-002", path, "Activation", "Content entry catalog contains a null entry."); continue; }
                if (!routeIds.Add(route.EntryRouteId)) CommercialArchitectureValidator.Add(issues, "COM-ACT-003", path, "Activation", $"Duplicate EntryRouteId '{route.EntryRouteId}'.");
                var key = route.SerializedEntryKind + "\n" + route.EntryValue;
                if (!keys.Add(key)) CommercialArchitectureValidator.Add(issues, "COM-ACT-004", path, "Activation", $"Duplicate entry key '{route.SerializedEntryKind}:{route.EntryValue}'.");
                if (route.DisplayProfile == null) { CommercialArchitectureValidator.Add(issues, "COM-ACT-005", path, "Activation", "Enabled entry has no DisplayProfile."); continue; }
                if (!route.DisplayProfile.IsValid(out var reason)) CommercialArchitectureValidator.Add(issues, "COM-ACT-005", AssetDatabase.GetAssetPath(route.DisplayProfile), "SpatialHost", reason);
                if (!sceneIds.Contains(route.SerializedTargetSceneId))
                    CommercialArchitectureValidator.Add(issues, "COM-ACT-008", path, "Activation", $"Entry target SceneId '{route.SerializedTargetSceneId}' cannot be resolved.");
                if (Guid.TryParse(route.EntryValue, out _) || string.Equals(route.SerializedEntryKind, "spatial_anchor", StringComparison.Ordinal))
                    CommercialArchitectureValidator.Add(issues, "COM-ACT-009", path, "Activation", "A persistent anchor UUID cannot be a ContentEntry key.");
                try
                {
                    if (route.Enabled && !registeredKinds.Contains(route.EntryKind))
                        CommercialArchitectureValidator.Add(issues, "COM-ACT-010", path, "Activation", $"Enabled entry kind '{route.SerializedEntryKind}' has no enabled adapter registration.");
                }
                catch (Exception exception)
                {
                    CommercialArchitectureValidator.Add(issues, "COM-ACT-011", path, "Activation", $"Entry kind '{route.SerializedEntryKind}' is invalid: {exception.Message}");
                }
                if (route.Enabled && !sceneTargets.Add(route.SerializedTargetSceneId))
                    CommercialArchitectureValidator.Add(issues, "COM-ACT-012", path, "Activation", $"SceneId '{route.SerializedTargetSceneId}' has more than one enabled ContentEntryRoute.");
            }
        }

        static HashSet<SourceKind> LoadRegisteredKinds(ICollection<CommercialValidationIssue> issues)
        {
            var paths = AssetDatabase.FindAssets("t:RuntimeEnvironmentOptions").Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var kinds = new HashSet<SourceKind>();
            if (paths.Length != 1) { CommercialArchitectureValidator.Add(issues, "COM-ACT-011", "Assets", "Activation", $"Exactly one RuntimeEnvironmentOptions is required; found {paths.Length}."); return kinds; }
            var options = AssetDatabase.LoadAssetAtPath<RuntimeEnvironmentOptions>(paths[0]);
            if (options.FieldbookEnabled) kinds.Add(RecognitionSourceKinds.Fieldbook);
            foreach (var option in options.RecognitionAdapters)
            {
                if (option == null) { CommercialArchitectureValidator.Add(issues, "COM-ACT-012", paths[0], "Activation", "Recognition adapter registration contains a null entry."); continue; }
                try
                {
                    if (option.Enabled && !kinds.Add(option.SourceKind))
                        CommercialArchitectureValidator.Add(issues, "COM-ACT-013", paths[0], "Activation", $"Enabled adapter SourceKind '{option.SerializedSourceKind}' is duplicated.");
                }
                catch (Exception exception) { CommercialArchitectureValidator.Add(issues, "COM-ACT-014", paths[0], "Activation", $"Adapter SourceKind '{option.SerializedSourceKind}' is invalid: {exception.Message}"); }
            }
            return kinds;
        }

    }
}
