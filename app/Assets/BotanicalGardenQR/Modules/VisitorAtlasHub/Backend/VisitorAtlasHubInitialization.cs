using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace BotanicalGardenQR.VisitorAtlasHub.Backend
{
    internal static class StreamingAssetsUriResolver
    {
        static readonly HashSet<string> SupportedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Uri.UriSchemeFile,
            Uri.UriSchemeHttp,
            Uri.UriSchemeHttps,
            "jar"
        };

        public static Uri Resolve(string streamingAssetsRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(streamingAssetsRoot))
                throw new InvalidOperationException("Application.streamingAssetsPath is empty.");
            var normalized = ValidateRelativePath(relativePath);
            var root = streamingAssetsRoot.Trim();

            if (Uri.TryCreate(root, UriKind.Absolute, out var rootUri) &&
                SupportedSchemes.Contains(rootUri.Scheme))
            {
                var baseText = root.TrimEnd('/', '\\') + "/";
                return new Uri(baseText + normalized, UriKind.Absolute);
            }

            var absolute = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            return new Uri(absolute);
        }

        public static string ValidateRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new InvalidOperationException("Visitor Atlas Hub map path must be relative to StreamingAssets.");
            var normalized = relativePath.Trim().Replace('\\', '/');
            if (normalized.IndexOfAny(new[] { '?', '#', '\0' }) >= 0)
                throw new InvalidOperationException("Visitor Atlas Hub map path contains URI control characters.");
            var decoded = Uri.UnescapeDataString(normalized).Replace('\\', '/');
            if (Path.IsPathRooted(decoded) || decoded.IndexOfAny(new[] { '?', '#', '\0' }) >= 0)
                throw new InvalidOperationException("Visitor Atlas Hub map path contains encoded control characters.");
            var segments = decoded.Split('/');
            if (segments.Length == 0 || Array.Exists(segments, segment =>
                    string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".."))
                throw new InvalidOperationException("Visitor Atlas Hub map path contains an invalid segment.");
            if (!normalized.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Visitor Atlas Hub map asset must be a GLB file.");
            return normalized;
        }
    }

    internal interface IVisitorAtlasHubMapLease : IDisposable
    {
        GameObject Root { get; }
    }

    internal interface IVisitorAtlasHubMapLoader
    {
        Task<IVisitorAtlasHubMapLease> LoadAsync(
            Uri source,
            Transform hiddenParent,
            CancellationToken cancellationToken);
    }

    internal sealed class GltfVisitorAtlasHubMapLoader : IVisitorAtlasHubMapLoader
    {
        public async Task<IVisitorAtlasHubMapLease> LoadAsync(
            Uri source,
            Transform hiddenParent,
            CancellationToken cancellationToken)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (hiddenParent == null) throw new ArgumentNullException(nameof(hiddenParent));
            cancellationToken.ThrowIfCancellationRequested();

            var import = new GltfImport();
            GameObject root = null;
            try
            {
                root = new GameObject("VisitorAtlasHubMapModel");
                root.SetActive(false);
                root.transform.SetParent(hiddenParent, false);
                if (!await import.Load(source, null, cancellationToken))
                    throw new InvalidOperationException("GLB import returned false.");
                cancellationToken.ThrowIfCancellationRequested();
                if (!await import.InstantiateMainSceneAsync(root.transform, cancellationToken))
                    throw new InvalidOperationException("GLB scene instantiation returned false.");
                cancellationToken.ThrowIfCancellationRequested();
                root.SetActive(true);
                var lease = new GltfVisitorAtlasHubMapLease(root, import);
                root = null;
                import = null;
                return lease;
            }
            finally
            {
                DestroyOwnedObject(root);
                import?.Dispose();
            }
        }

        static void DestroyOwnedObject(UnityEngine.Object ownedObject)
        {
            if (ownedObject == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(ownedObject);
                return;
            }
#endif
            UnityEngine.Object.Destroy(ownedObject);
        }
    }

    internal sealed class GltfVisitorAtlasHubMapLease : IVisitorAtlasHubMapLease
    {
        GameObject _root;
        GltfImport _import;

        public GltfVisitorAtlasHubMapLease(GameObject root, GltfImport import)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _import = import ?? throw new ArgumentNullException(nameof(import));
        }

        public GameObject Root => _root;

        public void Dispose()
        {
            var root = _root;
            _root = null;
            if (root != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(root);
                else
#endif
                    UnityEngine.Object.Destroy(root);
            }
            _import?.Dispose();
            _import = null;
        }
    }
}
