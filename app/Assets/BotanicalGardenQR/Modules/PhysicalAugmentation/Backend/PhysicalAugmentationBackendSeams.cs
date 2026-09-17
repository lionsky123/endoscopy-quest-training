using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Backend
{
    internal interface IPhysicalAugmentationLocator
    {
        event Action<IReadOnlyList<PhysicalAugmentationPointState>> StateChanged;
        bool TryStart(IReadOnlyList<PhysicalAugmentationDefinition> definitions, long generation, out string diagnosticTag);
        void Stop(long generation);
    }

    internal interface IPhysicalAugmentationSuppressionSource
    {
        bool IsSuppressed(PhysicalAugmentationPointId pointId, out string diagnosticTag);
    }

    internal interface IPhysicalAugmentationCapabilityGate
    {
        bool IsAvailable(PhysicalAugmentationDefinition definition, out string diagnosticTag);
    }

    internal interface IPhysicalAugmentationPerformanceFactory
    {
        bool TryPrepare(
            PhysicalAugmentationDefinition definition,
            Pose resolvedPose,
            float uniformScale,
            long generation,
            out IPhysicalAugmentationPerformanceLease lease,
            out string diagnosticTag);
    }

    internal interface IPhysicalAugmentationPerformanceLease : IDisposable
    {
        void Play(Action<PhysicalAugmentationPerformanceCompletion> completed);
        void Stop();
    }

    internal readonly struct PhysicalAugmentationPerformanceCompletion
    {
        public PhysicalAugmentationPerformanceCompletion(bool succeeded, string diagnosticTag = null)
        {
            Succeeded = succeeded;
            DiagnosticTag = diagnosticTag ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string DiagnosticTag { get; }
    }

    internal interface IPhysicalAnchorPlatform
    {
        Task<PhysicalAnchorPlatformLoadResult> LoadBatchAsync(IReadOnlyList<Guid> uuids);
    }

    internal interface IPhysicalAnchorPlatformLease : IDisposable
    {
        Guid Uuid { get; }
        bool IsTracked { get; }
        bool TryGetPose(out Pose pose);
    }

    internal readonly struct PhysicalAnchorPlatformLoadResult
    {
        public PhysicalAnchorPlatformLoadResult(
            bool succeeded,
            IReadOnlyList<IPhysicalAnchorPlatformLease> leases,
            string diagnosticTag = null)
        {
            Succeeded = succeeded;
            Leases = leases ?? Array.Empty<IPhysicalAnchorPlatformLease>();
            DiagnosticTag = diagnosticTag ?? string.Empty;
        }

        public bool Succeeded { get; }
        public IReadOnlyList<IPhysicalAnchorPlatformLease> Leases { get; }
        public string DiagnosticTag { get; }
    }
}
