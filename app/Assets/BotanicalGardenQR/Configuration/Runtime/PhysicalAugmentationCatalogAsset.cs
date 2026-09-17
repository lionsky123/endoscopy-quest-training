using System;
using System.Collections.Generic;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [CreateAssetMenu(menuName = "Botanical Garden QR/Published Physical Augmentation Catalog", fileName = "PhysicalAugmentationCatalog")]
    public sealed class PhysicalAugmentationCatalogAsset : ScriptableObject, IPhysicalAugmentationDefinitionSource
    {
        [SerializeField] PhysicalAugmentationPointRecord[] _points = Array.Empty<PhysicalAugmentationPointRecord>();
        [SerializeField, HideInInspector] string _sourceDigest;
        [SerializeField, HideInInspector] long _publishedVersion;
        [SerializeField, HideInInspector] string _publishedAtUtc;
        PhysicalAugmentationDefinitionCatalog _runtimeCatalog;

        public IReadOnlyList<PhysicalAugmentationPointRecord> Points => _points ?? Array.Empty<PhysicalAugmentationPointRecord>();
        public string SourceDigest => _sourceDigest ?? string.Empty;
        public long PublishedVersion => _publishedVersion;
        public string PublishedAtUtc => _publishedAtUtc ?? string.Empty;

        public IReadOnlyList<PhysicalAugmentationDefinition> Definitions
        {
            get
            {
                EnsureRuntimeCatalog();
                return _runtimeCatalog.Definitions;
            }
        }

        public bool TryGet(PhysicalAugmentationPointId pointId, out PhysicalAugmentationDefinition definition)
        {
            EnsureRuntimeCatalog();
            return _runtimeCatalog.TryGet(pointId, out definition);
        }

        public bool TryBuild(out PhysicalAugmentationDefinitionCatalog catalog, out string error)
            => PhysicalAugmentationCatalogCompiler.TryBuild(Points, out catalog, out error);

        void OnEnable() => _runtimeCatalog = null;

#if UNITY_EDITOR
        public void ReplacePublishedPayload(
            PhysicalAugmentationPointRecord[] points,
            string sourceDigest,
            long publishedVersion,
            DateTimeOffset publishedAt)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (string.IsNullOrWhiteSpace(sourceDigest))
                throw new ArgumentException("A source digest is required.", nameof(sourceDigest));
            if (publishedVersion <= 0) throw new ArgumentOutOfRangeException(nameof(publishedVersion));
            if (!PhysicalAugmentationCatalogCompiler.TryBuild(points, out _, out var error))
                throw new ArgumentException(error, nameof(points));
            _points = (PhysicalAugmentationPointRecord[])points.Clone();
            _sourceDigest = sourceDigest;
            _publishedVersion = publishedVersion;
            _publishedAtUtc = publishedAt.ToUniversalTime().ToString("O");
            _runtimeCatalog = null;
        }
#endif

        void EnsureRuntimeCatalog()
        {
            if (_runtimeCatalog != null) return;
            if (!TryBuild(out _runtimeCatalog, out var error))
                throw new InvalidOperationException(error);
        }
    }

    internal static class PhysicalAugmentationCatalogCompiler
    {
        public static bool TryBuild(
            IReadOnlyList<PhysicalAugmentationPointRecord> points,
            out PhysicalAugmentationDefinitionCatalog catalog,
            out string error)
        {
            catalog = null;
            error = string.Empty;
            try
            {
                var definitions = new List<PhysicalAugmentationDefinition>();
                var pointIds = new HashSet<PhysicalAugmentationPointId>();
                var installationAnchorNumbers = new HashSet<int>();
                points = points ?? Array.Empty<PhysicalAugmentationPointRecord>();
                for (var index = 0; index < points.Count; index++)
                {
                    var record = points[index];
                    if (record == null)
                        throw new ArgumentException($"Physical augmentation point {index} is missing.");
                    var pointId = record.ParsePointId();
                    if (!pointIds.Add(pointId))
                        throw new ArgumentException($"Physical augmentation point ID '{pointId}' is duplicated.");
                    if (!record.Enabled) continue;
                    if (record.InstallationAnchorNumber <= 0)
                        throw new ArgumentException(
                            $"Physical augmentation point '{pointId}' requires a positive installation anchor number.");
                    if (!installationAnchorNumbers.Add(record.InstallationAnchorNumber))
                        throw new ArgumentException(
                            $"Physical augmentation installation anchor number '{record.InstallationAnchorNumber}' is duplicated.");
                }
                for (var index = 0; index < points.Count; index++)
                {
                    var record = points[index];
                    if (!record.Enabled) continue;
                    definitions.Add(record.ToDefinition());
                }
                catalog = new PhysicalAugmentationDefinitionCatalog(definitions);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Physical augmentation catalog is invalid: {exception.Message}";
                return false;
            }
        }
    }

    [Serializable]
    public sealed class PhysicalAugmentationPointRecord
    {
        [SerializeField] bool _enabled = true;
        [SerializeField] string _pointId;
        [SerializeField] string _adminDisplayName;
        [SerializeField, Min(1)] int _installationAnchorNumber = 1;
        [Header("Closed asset set")]
        [SerializeField] GameObject _performancePrefab;
        [SerializeField] GameObject _depthProxyPrefab;
        [Header("Localization")]
        [SerializeField, Min(0.05f)] float _stabilitySeconds = 0.6f;
        [SerializeField, Min(0.001f)] float _positionToleranceMeters = 0.015f;
        [SerializeField, Min(0.1f)] float _orientationToleranceDegrees = 2f;
        [SerializeField, Min(0f)] float _lostGraceSeconds = 0.35f;
        [SerializeField, Min(0.001f)] float _rebasePositionThresholdMeters = 0.03f;
        [SerializeField, Min(0.1f)] float _rebaseOrientationThresholdDegrees = 5f;
        [Header("Calibration")]
        [SerializeField, Min(0.01f)] float _minimumCalibrationScale = 0.5f;
        [SerializeField, Min(0.01f)] float _maximumCalibrationScale = 2f;
        [Header("Reality model transform")]
        [SerializeField] Vector3 _modelEulerAngles;
        [SerializeField, Min(0.01f)] float _modelUniformScale = 1f;
        [Header("Occlusion")]
        [SerializeField] PhysicalAugmentationOcclusionPolicy _occlusionPolicy = PhysicalAugmentationOcclusionPolicy.RequireEnvironmentDepthAndProxy;

        public bool Enabled => _enabled;
        public string SerializedPointId => _pointId ?? string.Empty;
        public string AdminDisplayName => _adminDisplayName ?? string.Empty;
        public int InstallationAnchorNumber => _installationAnchorNumber;
        public GameObject PerformancePrefab => _performancePrefab;
        public GameObject DepthProxyPrefab => _depthProxyPrefab;
        public float MinimumCalibrationScale => _minimumCalibrationScale;
        public float MaximumCalibrationScale => _maximumCalibrationScale;
        public Vector3 ModelEulerAngles => _modelEulerAngles;
        public float ModelUniformScale => _modelUniformScale;

        public PhysicalAugmentationPointId ParsePointId() => new PhysicalAugmentationPointId(_pointId);

        public PhysicalAugmentationDefinition ToDefinition()
        {
            if (!IsCanonicalRoot(_performancePrefab))
                throw new ArgumentException($"Point '{SerializedPointId}' performance prefab root must use identity local transform.");
            if (!IsCanonicalRoot(_depthProxyPrefab))
                throw new ArgumentException($"Point '{SerializedPointId}' depth proxy root must use identity local transform.");
            return new PhysicalAugmentationDefinition(
                ParsePointId(),
                _adminDisplayName,
                _installationAnchorNumber,
                _performancePrefab,
                _depthProxyPrefab,
                _stabilitySeconds,
                _positionToleranceMeters,
                _orientationToleranceDegrees,
                _lostGraceSeconds,
                _rebasePositionThresholdMeters,
                _rebaseOrientationThresholdDegrees,
                _minimumCalibrationScale,
                _maximumCalibrationScale,
                _occlusionPolicy,
                _modelEulerAngles,
                _modelUniformScale);
        }

        static bool IsCanonicalRoot(GameObject prefab)
        {
            if (prefab == null) return false;
            var root = prefab.transform;
            return root.localPosition == Vector3.zero &&
                   root.localRotation == Quaternion.identity &&
                   root.localScale == Vector3.one;
        }
    }
}
