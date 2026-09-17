using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Contracts
{
    public enum PhysicalAugmentationOcclusionPolicy
    {
        RequireEnvironmentDepthAndProxy,
        AllowProxyFallback
    }

    public sealed class PhysicalAugmentationDefinition
    {
        public PhysicalAugmentationDefinition(
            PhysicalAugmentationPointId pointId,
            string adminDisplayName,
            int installationAnchorNumber,
            GameObject performancePrefab,
            GameObject depthProxyPrefab,
            float stabilitySeconds,
            float positionToleranceMeters,
            float orientationToleranceDegrees,
            float lostGraceSeconds,
            float rebasePositionThresholdMeters,
            float rebaseOrientationThresholdDegrees,
            float minimumCalibrationScale,
            float maximumCalibrationScale,
            PhysicalAugmentationOcclusionPolicy occlusionPolicy,
            Vector3 modelEulerAngles = default,
            float modelUniformScale = 1f)
        {
            if (!pointId.IsValid) throw new ArgumentException("A valid point ID is required.", nameof(pointId));
            if (string.IsNullOrWhiteSpace(adminDisplayName))
                throw new ArgumentException("An administrator display name is required.", nameof(adminDisplayName));
            if (installationAnchorNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(installationAnchorNumber));
            PointId = pointId;
            AdminDisplayName = adminDisplayName.Trim();
            InstallationAnchorNumber = installationAnchorNumber;
            PerformancePrefab = performancePrefab != null ? performancePrefab : throw new ArgumentNullException(nameof(performancePrefab));
            DepthProxyPrefab = depthProxyPrefab != null ? depthProxyPrefab : throw new ArgumentNullException(nameof(depthProxyPrefab));
            StabilitySeconds = RequireRange(stabilitySeconds, 0.05f, 10f, nameof(stabilitySeconds));
            PositionToleranceMeters = RequireRange(positionToleranceMeters, 0.001f, 1f, nameof(positionToleranceMeters));
            OrientationToleranceDegrees = RequireRange(orientationToleranceDegrees, 0.1f, 90f, nameof(orientationToleranceDegrees));
            LostGraceSeconds = RequireRange(lostGraceSeconds, 0f, 10f, nameof(lostGraceSeconds));
            RebasePositionThresholdMeters = RequirePositiveFinite(rebasePositionThresholdMeters, nameof(rebasePositionThresholdMeters));
            RebaseOrientationThresholdDegrees = RequireRange(rebaseOrientationThresholdDegrees, 0.1f, 180f, nameof(rebaseOrientationThresholdDegrees));
            MinimumCalibrationScale = RequirePositiveFinite(minimumCalibrationScale, nameof(minimumCalibrationScale));
            MaximumCalibrationScale = RequirePositiveFinite(maximumCalibrationScale, nameof(maximumCalibrationScale));
            if (MaximumCalibrationScale < MinimumCalibrationScale)
                throw new ArgumentException("The maximum calibration scale cannot be smaller than the minimum scale.", nameof(maximumCalibrationScale));
            if (RebasePositionThresholdMeters < PositionToleranceMeters)
                throw new ArgumentException("The rebase position threshold cannot be smaller than the stability tolerance.", nameof(rebasePositionThresholdMeters));
            if (RebaseOrientationThresholdDegrees < OrientationToleranceDegrees)
                throw new ArgumentException("The rebase orientation threshold cannot be smaller than the stability tolerance.", nameof(rebaseOrientationThresholdDegrees));
            if (!Enum.IsDefined(typeof(PhysicalAugmentationOcclusionPolicy), occlusionPolicy))
                throw new ArgumentOutOfRangeException(nameof(occlusionPolicy));
            OcclusionPolicy = occlusionPolicy;
            if (!IsFinite(modelEulerAngles))
                throw new ArgumentOutOfRangeException(nameof(modelEulerAngles));
            ModelRotationInAnchorSpace = Quaternion.Euler(modelEulerAngles);
            ModelUniformScale = RequirePositiveFinite(modelUniformScale, nameof(modelUniformScale));
            if (!float.IsFinite(ModelUniformScale * MaximumCalibrationScale))
                throw new ArgumentOutOfRangeException(nameof(modelUniformScale));
        }

        public PhysicalAugmentationPointId PointId { get; }
        public string AdminDisplayName { get; }
        public int InstallationAnchorNumber { get; }
        public GameObject PerformancePrefab { get; }
        public GameObject DepthProxyPrefab { get; }
        public float StabilitySeconds { get; }
        public float PositionToleranceMeters { get; }
        public float OrientationToleranceDegrees { get; }
        public float LostGraceSeconds { get; }
        public float RebasePositionThresholdMeters { get; }
        public float RebaseOrientationThresholdDegrees { get; }
        public float MinimumCalibrationScale { get; }
        public float MaximumCalibrationScale { get; }
        public PhysicalAugmentationOcclusionPolicy OcclusionPolicy { get; }
        public Quaternion ModelRotationInAnchorSpace { get; }
        public float ModelUniformScale { get; }

        static float RequirePositiveFinite(float value, string parameterName)
        {
            if (!(value > 0f) || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }

        static float RequireRange(float value, float minimum, float maximum, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }

        static bool IsFinite(Vector3 value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }

    public interface IPhysicalAugmentationDefinitionSource
    {
        IReadOnlyList<PhysicalAugmentationDefinition> Definitions { get; }
        bool TryGet(PhysicalAugmentationPointId pointId, out PhysicalAugmentationDefinition definition);
    }

    public sealed class PhysicalAugmentationDefinitionCatalog : IPhysicalAugmentationDefinitionSource
    {
        readonly Dictionary<PhysicalAugmentationPointId, PhysicalAugmentationDefinition> _index;
        readonly ReadOnlyCollection<PhysicalAugmentationDefinition> _definitions;

        public PhysicalAugmentationDefinitionCatalog(IReadOnlyList<PhysicalAugmentationDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            var copy = new PhysicalAugmentationDefinition[definitions.Count];
            _index = new Dictionary<PhysicalAugmentationPointId, PhysicalAugmentationDefinition>();
            var installationAnchorNumbers = new HashSet<int>();
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index] ?? throw new ArgumentException($"Definition {index} is missing.", nameof(definitions));
                if (!_index.TryAdd(definition.PointId, definition))
                    throw new ArgumentException($"Point ID '{definition.PointId}' is duplicated.", nameof(definitions));
                if (!installationAnchorNumbers.Add(definition.InstallationAnchorNumber))
                    throw new ArgumentException(
                        $"Installation anchor number '{definition.InstallationAnchorNumber}' is duplicated.",
                        nameof(definitions));
                copy[index] = definition;
            }
            Array.Sort(copy, (left, right) => left.PointId.CompareTo(right.PointId));
            _definitions = Array.AsReadOnly(copy);
        }

        public IReadOnlyList<PhysicalAugmentationDefinition> Definitions => _definitions;
        public bool TryGet(PhysicalAugmentationPointId pointId, out PhysicalAugmentationDefinition definition)
            => _index.TryGetValue(pointId, out definition);
    }
}
