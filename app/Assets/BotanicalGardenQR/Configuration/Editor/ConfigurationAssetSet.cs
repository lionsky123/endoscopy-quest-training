using System;
using System.Collections.Generic;
using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Editor
{
    internal sealed class ConfigurationAssetSet
    {
        ConfigurationAssetSet(
            ContentSceneConfig[] scenes,
            ContentEntryCatalog entries,
            GlobalUiDefaults defaults,
            RuntimeEnvironmentOptions environment,
            FairyApplicationConfiguration fairy,
            ContentSceneLibrary library,
            CollectionCatalogAsset collectionCatalog,
            VisitorPrologueThemeAsset prologueTheme,
            VisitorCoachThemeAsset coachTheme,
            PhysicalAugmentationCatalogAuthoringAsset physicalAugmentationAuthoring,
            PhysicalAugmentationCatalogAsset physicalAugmentationCatalog)
        {
            Scenes = scenes;
            Entries = entries;
            Defaults = defaults;
            Environment = environment;
            Fairy = fairy;
            Library = library;
            CollectionCatalog = collectionCatalog;
            PrologueTheme = prologueTheme;
            CoachTheme = coachTheme;
            PhysicalAugmentationAuthoring = physicalAugmentationAuthoring;
            PhysicalAugmentationCatalog = physicalAugmentationCatalog;
        }

        public ContentSceneConfig[] Scenes { get; }
        public ContentEntryCatalog Entries { get; }
        public GlobalUiDefaults Defaults { get; }
        public RuntimeEnvironmentOptions Environment { get; }
        public FairyApplicationConfiguration Fairy { get; }
        public ContentSceneLibrary Library { get; }
        public CollectionCatalogAsset CollectionCatalog { get; }
        public VisitorPrologueThemeAsset PrologueTheme { get; }
        public VisitorCoachThemeAsset CoachTheme { get; }
        public PhysicalAugmentationCatalogAuthoringAsset PhysicalAugmentationAuthoring { get; }
        public PhysicalAugmentationCatalogAsset PhysicalAugmentationCatalog { get; }

        public static ConfigurationAssetSet Load(ICollection<ConfigurationIssue> issues)
        {
            var scenes = LoadAll<ContentSceneConfig>();
            if (scenes.Length == 0)
                issues.Add(new ConfigurationIssue("CFG100", "Assets", "At least one ContentSceneConfig is required."));

            var entries = LoadExactlyOne<ContentEntryCatalog>("CFG101", issues);
            var defaults = LoadExactlyOne<GlobalUiDefaults>("CFG102", issues);
            var environment = LoadExactlyOne<RuntimeEnvironmentOptions>("CFG103", issues);
            var fairy = LoadExactlyOne<FairyApplicationConfiguration>("CFG105", issues);
            var collectionCatalog = LoadExactlyOne<CollectionCatalogAsset>("CFG106", issues);
            var prologueTheme = LoadExactlyOne<VisitorPrologueThemeAsset>("CFG107", issues);
            var coachTheme = LoadExactlyOne<VisitorCoachThemeAsset>("CFG113", issues);
            var physicalAugmentationAuthoring = LoadExactlyOne<PhysicalAugmentationCatalogAuthoringAsset>("CFG109", issues);
            var physicalAugmentationCatalog = LoadZeroOrOne<PhysicalAugmentationCatalogAsset>("CFG112", issues);
            var libraries = LoadAll<ContentSceneLibrary>();
            if (libraries.Length > 1)
                issues.Add(new ConfigurationIssue("CFG104", "Assets", "Exactly zero or one generated ContentSceneLibrary may exist."));

            return new ConfigurationAssetSet(
                scenes,
                entries,
                defaults,
                environment,
                fairy,
                libraries.FirstOrDefault(),
                collectionCatalog,
                prologueTheme,
                coachTheme,
                physicalAugmentationAuthoring,
                physicalAugmentationCatalog);
        }

        static T LoadExactlyOne<T>(string code, ICollection<ConfigurationIssue> issues) where T : ScriptableObject
        {
            var assets = LoadAll<T>();
            if (assets.Length != 1)
            {
                issues.Add(new ConfigurationIssue(code, "Assets", $"Exactly one {typeof(T).Name} is required; found {assets.Length}."));
                return null;
            }
            return assets[0];
        }

        static T LoadZeroOrOne<T>(string code, ICollection<ConfigurationIssue> issues) where T : ScriptableObject
        {
            var assets = LoadAll<T>();
            if (assets.Length > 1)
            {
                issues.Add(new ConfigurationIssue(code, "Assets", $"Exactly zero or one generated {typeof(T).Name} may exist; found {assets.Length}."));
                return null;
            }
            return assets.FirstOrDefault();
        }

        static T[] LoadAll<T>() where T : ScriptableObject
            => AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();
    }
}
