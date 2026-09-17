using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Editor.Validation
{
    /// <summary>
    /// Guards completed content cutovers.  These paths are intentionally retired rather than
    /// alternative production roots, so a new asset there is a structural regression.
    /// </summary>
    internal static class AssetStructureValidator
    {
        const string VisitorRuntimePath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab";

        static readonly string[] RetiredRoots =
        {
            "Assets/BotanicalGardenQR/Models",
            "Assets/BotanicalGardenQR/Resources/BotanicalGardenQR/Prefabs",
            "Assets/BotanicalGardenQR/Resources/BotanicalGardenQR/ScriptableObjects",
            "Assets/BotanicalGardenQR/Resources/BotanicalGardenQR/SpatialVideos",
            "Assets/StreamingAssets/BotanicalGardenQR/Panoramas"
        };

        static readonly string[] RetiredAssets =
        {
            "Assets/BotanicalGardenQR/Resources/BotanicalGardenQR/BotanicalApplicationSettings.asset",
            "Assets/BotanicalGardenQR/Resources/BotanicalGardenQR/BotanicalInteractionSettings.asset"
        };

        public static void Validate(ValidationScope scope, ICollection<CommercialValidationIssue> issues)
        {
            if (scope != ValidationScope.All) return;

            var assetPaths = AssetDatabase.GetAllAssetPaths();
            foreach (var root in RetiredRoots)
            {
                var retiredAsset = assetPaths.FirstOrDefault(path =>
                    path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase) &&
                    !AssetDatabase.IsValidFolder(path));
                if (retiredAsset != null)
                    CommercialArchitectureValidator.Add(issues, "COM-ASSET-001", retiredAsset, "AssetStructure",
                        $"Retired asset root '{root}' contains production asset '{retiredAsset}'. Move it into Content/Scenes/<SceneId> or Content/Shared.");
            }

            foreach (var path in RetiredAssets)
            {
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                    CommercialArchitectureValidator.Add(issues, "COM-ASSET-002", path, "AssetStructure",
                        "Retired Resources settings asset remains in the project. Use the authored Configuration assets instead.");
            }

            ValidateProductionPrefabDependencies(issues);
        }

        static void ValidateProductionPrefabDependencies(
            ICollection<CommercialValidationIssue> issues)
        {
            var roots = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Concat(new[] { VisitorRuntimePath })
                .Concat(AssetDatabase.FindAssets("t:ContentSceneLibrary")
                    .Select(AssetDatabase.GUIDToAssetPath))
                .Where(path => !string.IsNullOrWhiteSpace(path) &&
                    !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var path in AssetDatabase.GetDependencies(roots, true)
                .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var missingCount = prefab.GetComponentsInChildren<Transform>(true)
                    .Sum(owner => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(owner.gameObject));
                if (missingCount > 0)
                    CommercialArchitectureValidator.Add(
                        issues,
                        "COM-ASSET-003",
                        path,
                        "AssetStructure",
                        $"Production dependency contains {missingCount} missing MonoBehaviour script(s).");
            }
        }
    }
}
