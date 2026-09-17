using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Contracts
{
    public enum PhysicalAugmentationPhase
    {
        Stopped,
        Starting,
        Ready,
        Playing,
        Failed
    }

    public enum PhysicalAugmentationLocalizationPhase
    {
        Unconfigured,
        Loading,
        Localizing,
        Stabilizing,
        Stable,
        Lost,
        Failed
    }

    public enum PhysicalAugmentationPointActivityPhase
    {
        Inactive,
        Suppressed,
        Available,
        Playing,
        Presented,
        Failed
    }

    public readonly struct PhysicalAugmentationPointState
    {
        public PhysicalAugmentationPointState(
            PhysicalAugmentationPointId pointId,
            PhysicalAugmentationLocalizationPhase phase,
            long generation,
            bool hasResolvedPose,
            Pose resolvedPose,
            float uniformScale,
            PhysicalAugmentationPointActivityPhase activityPhase,
            string diagnosticTag = null)
        {
            if (!pointId.IsValid) throw new ArgumentException("A valid point ID is required.", nameof(pointId));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            if (!Enum.IsDefined(typeof(PhysicalAugmentationLocalizationPhase), phase))
                throw new ArgumentOutOfRangeException(nameof(phase));
            if (!Enum.IsDefined(typeof(PhysicalAugmentationPointActivityPhase), activityPhase))
                throw new ArgumentOutOfRangeException(nameof(activityPhase));
            if ((activityPhase == PhysicalAugmentationPointActivityPhase.Available ||
                 activityPhase == PhysicalAugmentationPointActivityPhase.Playing ||
                 activityPhase == PhysicalAugmentationPointActivityPhase.Presented) &&
                (phase != PhysicalAugmentationLocalizationPhase.Stable || !hasResolvedPose))
                throw new ArgumentException(
                    "An available, playing, or presented point must have a stable resolved pose.",
                    nameof(activityPhase));
            if (hasResolvedPose && (!(uniformScale > 0f) || float.IsNaN(uniformScale) || float.IsInfinity(uniformScale)))
                throw new ArgumentOutOfRangeException(nameof(uniformScale));
            PointId = pointId;
            Phase = phase;
            Generation = generation;
            HasResolvedPose = hasResolvedPose;
            ResolvedPose = resolvedPose;
            UniformScale = uniformScale;
            ActivityPhase = activityPhase;
            DiagnosticTag = diagnosticTag ?? string.Empty;
        }

        public PhysicalAugmentationPointId PointId { get; }
        public PhysicalAugmentationLocalizationPhase Phase { get; }
        public long Generation { get; }
        public bool HasResolvedPose { get; }
        public Pose ResolvedPose { get; }
        public float UniformScale { get; }
        public PhysicalAugmentationPointActivityPhase ActivityPhase { get; }
        public bool CanActivate =>
            ActivityPhase == PhysicalAugmentationPointActivityPhase.Available ||
            ActivityPhase == PhysicalAugmentationPointActivityPhase.Playing ||
            ActivityPhase == PhysicalAugmentationPointActivityPhase.Presented;
        public string DiagnosticTag { get; }
    }

    public readonly struct PhysicalAugmentationState
    {
        readonly ReadOnlyCollection<PhysicalAugmentationPointState> _points;

        public PhysicalAugmentationState(
            long version,
            long generation,
            PhysicalAugmentationPhase phase,
            IReadOnlyList<PhysicalAugmentationPointState> points,
            string faultMessage = null)
        {
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            if (points == null) throw new ArgumentNullException(nameof(points));
            var copy = new PhysicalAugmentationPointState[points.Count];
            var pointIds = new HashSet<PhysicalAugmentationPointId>();
            var activePresentationCount = 0;
            for (var index = 0; index < points.Count; index++)
            {
                var point = points[index];
                if (point.Generation != generation)
                    throw new ArgumentException("Every point state must use the owning state generation.", nameof(points));
                if (!pointIds.Add(point.PointId))
                    throw new ArgumentException("Point states must be unique.", nameof(points));
                if (point.ActivityPhase == PhysicalAugmentationPointActivityPhase.Playing ||
                    point.ActivityPhase == PhysicalAugmentationPointActivityPhase.Presented)
                    activePresentationCount++;
                copy[index] = point;
            }
            if (phase == PhysicalAugmentationPhase.Playing)
            {
                if (activePresentationCount == 0)
                    throw new ArgumentException(
                        "Playing state requires at least one active presentation point.",
                        nameof(points));
            }
            else if (activePresentationCount != 0)
            {
                throw new ArgumentException(
                    "Only Playing state may expose active presentation points.",
                    nameof(points));
            }
            Version = version;
            Generation = generation;
            Phase = phase;
            _points = Array.AsReadOnly(copy);
            FaultMessage = faultMessage ?? string.Empty;
        }

        public long Version { get; }
        public long Generation { get; }
        public PhysicalAugmentationPhase Phase { get; }
        public IReadOnlyList<PhysicalAugmentationPointState> Points => _points ?? Array.AsReadOnly(Array.Empty<PhysicalAugmentationPointState>());
        public string FaultMessage { get; }
    }

    public enum PhysicalAugmentationIntentKind
    {
        Activate
    }

    public readonly struct PhysicalAugmentationIntent
    {
        public PhysicalAugmentationIntent(
            PhysicalAugmentationIntentKind kind,
            PhysicalAugmentationPointId pointId,
            long generation)
        {
            if (!pointId.IsValid) throw new ArgumentException("A valid point ID is required.", nameof(pointId));
            if (generation <= 0) throw new ArgumentOutOfRangeException(nameof(generation));
            Kind = kind;
            PointId = pointId;
            Generation = generation;
        }

        public PhysicalAugmentationIntentKind Kind { get; }
        public PhysicalAugmentationPointId PointId { get; }
        public long Generation { get; }
    }
}
