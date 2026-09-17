using System;
using System.Collections.Generic;
using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Editor
{
    internal static class ConfigurationInputDigest
    {
        public static string ComputeContentLibrarySourceDigest(ConfigurationAssetSet assets)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            return Compute(ContentLibrarySourcePaths(assets));
        }

        public static string ComputePhysicalAugmentationSourceDigest(ConfigurationAssetSet assets)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            return ComputePhysicalAugmentationSourceDigest(assets.PhysicalAugmentationAuthoring);
        }

        public static string ComputePhysicalAugmentationSourceDigest(
            PhysicalAugmentationCatalogAuthoringAsset authoring)
        {
            if (authoring == null) throw new ArgumentNullException(nameof(authoring));
            return Compute(new[]
            {
                AssetDatabase.GetAssetPath(authoring)
            });
        }

        public static ConfigurationReleaseInputSummary CreateReleaseSummary(ConfigurationAssetSet assets)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            var paths = ProjectAuthorityPaths(assets)
                .Concat(new[]
                {
                    AssetDatabase.GetAssetPath(assets.PrologueTheme),
                    AssetDatabase.GetAssetPath(assets.CoachTheme),
                    AssetDatabase.GetAssetPath(assets.Library),
                    AssetDatabase.GetAssetPath(assets.PhysicalAugmentationCatalog)
                })
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            return new ConfigurationReleaseInputSummary(Compute(paths), paths);
        }

        static IEnumerable<string> ContentLibrarySourcePaths(ConfigurationAssetSet assets)
            => assets.Scenes.Select(AssetDatabase.GetAssetPath)
                .Concat(new[]
                {
                    AssetDatabase.GetAssetPath(assets.Defaults)
                });

        static IEnumerable<string> ProjectAuthorityPaths(ConfigurationAssetSet assets)
            => ContentLibrarySourcePaths(assets)
                .Concat(new[]
                {
                    AssetDatabase.GetAssetPath(assets.Entries),
                    AssetDatabase.GetAssetPath(assets.Environment),
                    AssetDatabase.GetAssetPath(assets.Fairy),
                    AssetDatabase.GetAssetPath(assets.CollectionCatalog),
                    AssetDatabase.GetAssetPath(assets.PhysicalAugmentationAuthoring)
                });

        static string Compute(System.Collections.Generic.IEnumerable<string> paths)
        {
            var canonical = string.Join(
                "\n",
                paths.Where(path => !string.IsNullOrEmpty(path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path =>
                        $"{AssetDatabase.AssetPathToGUID(path)}:{AssetDatabase.GetAssetDependencyHash(path)}"));
            return Hash128.Compute(canonical).ToString();
        }
    }

    public static class ConfigurationReleaseEvidence
    {
        public static ConfigurationReleaseInputSummary CreateValidatedSummary()
        {
            var assets = ContentSceneConfigurationValidator.ValidateAndCompile(out _, out var validation);
            if (!validation.IsValid)
                throw new InvalidOperationException(
                    $"Cannot create release input evidence from invalid configuration:{Environment.NewLine}{validation.Format()}");
            return ConfigurationInputDigest.CreateReleaseSummary(assets);
        }
    }

    public sealed class ConfigurationReleaseInputSummary
    {
        public ConfigurationReleaseInputSummary(
            string projectDigest,
            IReadOnlyList<string> projectAssetPaths)
        {
            ProjectDigest = string.IsNullOrWhiteSpace(projectDigest)
                ? throw new ArgumentException("A project configuration digest is required.", nameof(projectDigest))
                : projectDigest;
            ProjectAssetPaths = projectAssetPaths ?? throw new ArgumentNullException(nameof(projectAssetPaths));
        }

        public string ProjectDigest { get; }
        public IReadOnlyList<string> ProjectAssetPaths { get; }
    }
}
