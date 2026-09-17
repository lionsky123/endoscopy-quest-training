using System;
using System.Collections.Generic;
using BotanicalGardenQR.Configuration.Runtime;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Editor
{
    public enum ContentScenePublishStatus
    {
        Failed,
        Published,
        UpToDate
    }

    public sealed class ContentScenePublishResult
    {
        internal ContentScenePublishResult(ContentScenePublishStatus status, ConfigurationValidationResult validation, string libraryPath)
        { Status = status; Validation = validation; LibraryPath = libraryPath ?? string.Empty; }
        public ContentScenePublishStatus Status { get; }
        public bool Succeeded => Status != ContentScenePublishStatus.Failed;
        public ConfigurationValidationResult Validation { get; }
        public string LibraryPath { get; }
    }

    public static class ContentScenePublisher
    {
        public const string DefaultLibraryPath = "Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset";
        public const string DefaultPhysicalAugmentationCatalogPath =
            "Assets/BotanicalGardenQR/Content/Published/PhysicalAugmentationCatalog.asset";

        public static ContentScenePublishResult PublishContentLibrary()
        {
            var assets = ContentSceneConfigurationValidator.ValidateAndCompile(out var packages, out var validation);
            var libraryPath = assets.Library == null ? DefaultLibraryPath : AssetDatabase.GetAssetPath(assets.Library);
            if (!validation.IsValid)
                return new ContentScenePublishResult(ContentScenePublishStatus.Failed, validation, libraryPath);

            var libraryDigest = ConfigurationInputDigest.ComputeContentLibrarySourceDigest(assets);
            var physicalCatalogDigest =
                ConfigurationInputDigest.ComputePhysicalAugmentationSourceDigest(assets);
            return PublishValidatedBundle(
                packages,
                assets.PhysicalAugmentationAuthoring.CopyPoints(),
                libraryDigest,
                physicalCatalogDigest,
                assets.Library,
                assets.PhysicalAugmentationCatalog,
                libraryPath,
                DefaultPhysicalAugmentationCatalogPath,
                validation);
        }

        internal static ContentScenePublishResult PublishValidatedBundle(
            ScenePackage[] packages,
            PhysicalAugmentationPointRecord[] physicalPoints,
            string libraryDigest,
            string physicalCatalogDigest,
            ContentSceneLibrary library,
            PhysicalAugmentationCatalogAsset physicalCatalog,
            string libraryPath,
            string physicalCatalogPath,
            ConfigurationValidationResult validation,
            Func<ContentSceneLibrary> createLibrary = null,
            Func<PhysicalAugmentationCatalogAsset> createPhysicalCatalog = null,
            Action<ContentSceneLibrary> saveLibrary = null,
            Action<PhysicalAugmentationCatalogAsset> savePhysicalCatalog = null,
            Action deleteCreatedLibrary = null,
            Action deleteCreatedPhysicalCatalog = null)
        {
            createLibrary ??= () => CreateLibraryAsset(libraryPath);
            createPhysicalCatalog ??= () => CreatePhysicalCatalogAsset(physicalCatalogPath);
            saveLibrary ??= SaveLibraryAsset;
            savePhysicalCatalog ??= SavePhysicalCatalogAsset;
            deleteCreatedLibrary ??= () => AssetDatabase.DeleteAsset(libraryPath);
            deleteCreatedPhysicalCatalog ??= () => AssetDatabase.DeleteAsset(physicalCatalogPath);
            var replaceLibrary = library == null ||
                                 !IsUpToDate(library, packages, libraryDigest);
            var replacePhysicalCatalog = physicalCatalog == null ||
                                         !IsUpToDate(
                                             physicalCatalog,
                                             physicalPoints,
                                             physicalCatalogDigest);
            if (!replaceLibrary && !replacePhysicalCatalog)
                return new ContentScenePublishResult(
                    ContentScenePublishStatus.UpToDate,
                    validation,
                    libraryPath);

            var createdLibrary = false;
            var createdPhysicalCatalog = false;
            var previousLibraryJson = library == null ? null : EditorJsonUtility.ToJson(library);
            var previousPhysicalJson = physicalCatalog == null
                ? null
                : EditorJsonUtility.ToJson(physicalCatalog);
            try
            {
                if (library == null)
                {
                    library = createLibrary();
                    if (library == null) throw new InvalidOperationException("Content library creation returned null.");
                    createdLibrary = true;
                }
                if (physicalCatalog == null)
                {
                    physicalCatalog = createPhysicalCatalog();
                    if (physicalCatalog == null)
                        throw new InvalidOperationException("Physical augmentation catalog creation returned null.");
                    createdPhysicalCatalog = true;
                }

                var publishedAt = DateTimeOffset.UtcNow;
                if (replaceLibrary)
                {
                    var nextLibraryVersion = Math.Max(1L, library.PublishedVersion + 1L);
                    library.ReplacePublishedPayload(
                        packages,
                        libraryDigest,
                        nextLibraryVersion,
                        publishedAt);
                    saveLibrary(library);
                }
                if (replacePhysicalCatalog)
                {
                    var nextPhysicalVersion = Math.Max(1L, physicalCatalog.PublishedVersion + 1L);
                    physicalCatalog.ReplacePublishedPayload(
                        physicalPoints,
                        physicalCatalogDigest,
                        nextPhysicalVersion,
                        publishedAt);
                    savePhysicalCatalog(physicalCatalog);
                }
                return new ContentScenePublishResult(
                    ContentScenePublishStatus.Published,
                    validation,
                    libraryPath);
            }
            catch (Exception exception)
            {
                Exception rollbackException = null;
                try
                {
                    if (createdLibrary) deleteCreatedLibrary();
                    else if (replaceLibrary && library != null && previousLibraryJson != null)
                    {
                        EditorJsonUtility.FromJsonOverwrite(previousLibraryJson, library);
                        saveLibrary(library);
                    }

                    if (createdPhysicalCatalog) deleteCreatedPhysicalCatalog();
                    else if (replacePhysicalCatalog && physicalCatalog != null && previousPhysicalJson != null)
                    {
                        EditorJsonUtility.FromJsonOverwrite(previousPhysicalJson, physicalCatalog);
                        savePhysicalCatalog(physicalCatalog);
                    }
                }
                catch (Exception rollbackFailure)
                {
                    rollbackException = rollbackFailure;
                }

                var issues = new List<ConfigurationIssue>(validation.Issues)
                {
                    new ConfigurationIssue(
                        "CFG200",
                        libraryPath,
                        rollbackException == null
                            ? $"Atomic publish failed; the previous published bundle was retained: {exception.Message}"
                            : $"Atomic publish and rollback failed: {exception.Message}; rollback: {rollbackException.Message}")
                };
                return new ContentScenePublishResult(
                    ContentScenePublishStatus.Failed,
                    new ConfigurationValidationResult(issues),
                    libraryPath);
            }
        }

        internal static ContentScenePublishResult PublishValidated(
            ScenePackage[] packages,
            string digest,
            ContentSceneLibrary library,
            string libraryPath,
            ConfigurationValidationResult validation,
            Func<ContentSceneLibrary> createLibrary,
            Action<ContentSceneLibrary> saveLibrary,
            Action deleteCreatedLibrary)
        {
            if (validation == null) throw new ArgumentNullException(nameof(validation));
            if (!validation.IsValid)
                return new ContentScenePublishResult(ContentScenePublishStatus.Failed, validation, libraryPath);
            if (packages == null) throw new ArgumentNullException(nameof(packages));
            if (string.IsNullOrWhiteSpace(digest)) throw new ArgumentException("A source digest is required.", nameof(digest));
            if (saveLibrary == null) throw new ArgumentNullException(nameof(saveLibrary));
            if (library != null && IsUpToDate(library, packages, digest))
                return new ContentScenePublishResult(ContentScenePublishStatus.UpToDate, validation, libraryPath);

            var created = false;
            var previousJson = library == null ? null : EditorJsonUtility.ToJson(library);
            try
            {
                if (library == null)
                {
                    library = (createLibrary ?? throw new ArgumentNullException(nameof(createLibrary)))();
                    if (library == null) throw new InvalidOperationException("Content library creation returned null.");
                    created = true;
                }

                var nextVersion = Math.Max(1L, library.PublishedVersion + 1L);
                library.ReplacePublishedPayload(packages, digest, nextVersion, DateTimeOffset.UtcNow);
                saveLibrary(library);
                return new ContentScenePublishResult(ContentScenePublishStatus.Published, validation, libraryPath);
            }
            catch (Exception exception)
            {
                Exception rollbackException = null;
                try
                {
                    if (created)
                        (deleteCreatedLibrary ?? throw new ArgumentNullException(nameof(deleteCreatedLibrary)))();
                    else if (library != null && previousJson != null)
                    {
                        EditorJsonUtility.FromJsonOverwrite(previousJson, library);
                        saveLibrary(library);
                    }
                }
                catch (Exception rollbackFailure)
                {
                    rollbackException = rollbackFailure;
                }

                var issues = new List<ConfigurationIssue>(validation.Issues)
                {
                    new ConfigurationIssue(
                        "CFG200",
                        libraryPath,
                        rollbackException == null
                            ? $"Atomic publish failed; the previous Library was retained: {exception.Message}"
                            : $"Atomic publish and rollback failed: {exception.Message}; rollback: {rollbackException.Message}")
                };
                return new ContentScenePublishResult(
                    ContentScenePublishStatus.Failed,
                    new ConfigurationValidationResult(issues),
                    libraryPath);
            }
        }

        static bool IsUpToDate(ContentSceneLibrary library, ScenePackage[] packages, string digest)
        {
            if (!string.Equals(library.SourceDigest, digest, StringComparison.Ordinal) ||
                library.PublishedVersion <= 0 ||
                !DateTimeOffset.TryParse(library.PublishedAtUtc, out _) ||
                library.Packages.Count != packages.Length)
                return false;

            for (var index = 0; index < packages.Length; index++)
            {
                var current = library.Packages[index];
                var candidate = packages[index];
                if (current == null || candidate == null || current.SceneId != candidate.SceneId ||
                    !string.Equals(JsonUtility.ToJson(current), JsonUtility.ToJson(candidate), StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        static bool IsUpToDate(
            PhysicalAugmentationCatalogAsset catalog,
            PhysicalAugmentationPointRecord[] points,
            string digest)
        {
            if (!string.Equals(catalog.SourceDigest, digest, StringComparison.Ordinal) ||
                catalog.PublishedVersion <= 0 ||
                !DateTimeOffset.TryParse(catalog.PublishedAtUtc, out _) ||
                !catalog.TryBuild(out _, out _) ||
                catalog.Points.Count != points.Length)
                return false;

            for (var index = 0; index < points.Length; index++)
                if (!string.Equals(
                        JsonUtility.ToJson(catalog.Points[index]),
                        JsonUtility.ToJson(points[index]),
                        StringComparison.Ordinal))
                    return false;
            return true;
        }

        static ContentSceneLibrary CreateLibraryAsset(string libraryPath)
        {
            EnsureAssetFolder(libraryPath);
            var library = ScriptableObject.CreateInstance<ContentSceneLibrary>();
            AssetDatabase.CreateAsset(library, libraryPath);
            return library;
        }

        static PhysicalAugmentationCatalogAsset CreatePhysicalCatalogAsset(string catalogPath)
        {
            EnsureAssetFolder(catalogPath);
            var catalog = ScriptableObject.CreateInstance<PhysicalAugmentationCatalogAsset>();
            AssetDatabase.CreateAsset(catalog, catalogPath);
            return catalog;
        }

        static void SaveLibraryAsset(ContentSceneLibrary library)
        {
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
        }

        static void SavePhysicalCatalogAsset(PhysicalAugmentationCatalogAsset catalog)
        {
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        static void EnsureAssetFolder(string assetPath)
        {
            var parts = assetPath.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length - 1; index++)
            {
                var next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
