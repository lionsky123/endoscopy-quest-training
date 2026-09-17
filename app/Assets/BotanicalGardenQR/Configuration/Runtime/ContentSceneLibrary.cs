using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BotanicalGardenQR.Experience.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [CreateAssetMenu(menuName = "Botanical Garden QR/Published Content Scene Library", fileName = "ContentSceneLibrary")]
    public sealed class ContentSceneLibrary : ScriptableObject
    {
        [SerializeField, HideInInspector] ScenePackage[] _packages = Array.Empty<ScenePackage>();
        [SerializeField, HideInInspector] string _sourceDigest;
        [SerializeField, HideInInspector] long _publishedVersion;
        [SerializeField, HideInInspector] string _publishedAtUtc;
        Dictionary<SceneId, ScenePackage> _index;
        ReadOnlyCollection<ScenePackage> _readOnlyPackages;

        public IReadOnlyList<ScenePackage> Packages => _readOnlyPackages ??= Array.AsReadOnly(_packages);
        public string SourceDigest => _sourceDigest ?? string.Empty;
        public long PublishedVersion => _publishedVersion;
        public string PublishedAtUtc => _publishedAtUtc ?? string.Empty;

        public bool TryGet(SceneId sceneId, out ScenePackage package)
        {
            EnsureIndex();
            return _index.TryGetValue(sceneId, out package);
        }

        void OnEnable() { _index = null; _readOnlyPackages = null; }

        void EnsureIndex()
        {
            if (_index != null) return;
            var index = new Dictionary<SceneId, ScenePackage>();
            foreach (var package in _packages)
            {
                if (package == null) continue;
                index.Add(package.SceneId, package);
            }
            _index = index;
        }

#if UNITY_EDITOR
        public void ReplacePublishedPayload(ScenePackage[] packages, string sourceDigest, long publishedVersion, DateTimeOffset publishedAt)
        {
            if (packages == null) throw new ArgumentNullException(nameof(packages));
            if (string.IsNullOrWhiteSpace(sourceDigest)) throw new ArgumentException("A source digest is required.", nameof(sourceDigest));
            if (publishedVersion <= 0) throw new ArgumentOutOfRangeException(nameof(publishedVersion));
            var copy = (ScenePackage[])packages.Clone();
            var unique = new HashSet<SceneId>();
            foreach (var package in copy)
            {
                if (package == null) throw new ArgumentException("Published packages cannot contain null.", nameof(packages));
                if (!unique.Add(package.SceneId)) throw new ArgumentException($"Duplicate SceneId '{package.SceneId}'.", nameof(packages));
            }
            _packages = copy;
            _sourceDigest = sourceDigest;
            _publishedVersion = publishedVersion;
            _publishedAtUtc = publishedAt.ToUniversalTime().ToString("O");
            _index = null;
            _readOnlyPackages = null;
        }
#endif
    }
}
