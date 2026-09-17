using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Installation;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Backend
{
    internal sealed class PhysicalAnchorLocalizationCoordinator : IPhysicalAugmentationLocator, IDisposable
    {
        const int MaximumBatchSize = 50;
        readonly IPhysicalAnchorBindingReader _bindings;
        readonly IPhysicalAnchorPlatform _platform;
        readonly Dictionary<PhysicalAugmentationPointId, PointRuntime> _points =
            new Dictionary<PhysicalAugmentationPointId, PointRuntime>();
        long _generation;
        bool _running;
        bool _disposed;

        public PhysicalAnchorLocalizationCoordinator(
            IPhysicalAnchorBindingReader bindings,
            IPhysicalAnchorPlatform platform)
        {
            _bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        }

        public event Action<IReadOnlyList<PhysicalAugmentationPointState>> StateChanged;

        public bool TryStart(
            IReadOnlyList<PhysicalAugmentationDefinition> definitions,
            long generation,
            out string diagnosticTag)
        {
            diagnosticTag = string.Empty;
            if (_disposed)
            {
                diagnosticTag = "physical_locator.disposed";
                return false;
            }
            if (_running)
            {
                diagnosticTag = "physical_locator.single_flight";
                return false;
            }
            if (definitions == null || generation <= 0)
            {
                diagnosticTag = "physical_locator.start_invalid";
                return false;
            }

            _running = true;
            _generation = generation;
            _points.Clear();
            var read = _bindings.Read();
            Debug.Log(
                $"[PhysicalAugmentation] locator start generation={generation} definitions={definitions.Count} " +
                $"storeSucceeded={read.Succeeded} " +
                $"anchors={(read.Succeeded ? read.Snapshot.Anchors.Count : -1)} " +
                $"bindings={(read.Succeeded ? read.Snapshot.Bindings.Count : -1)} " +
                $"diagnostic={read.DiagnosticTag}.");
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (definition == null || _points.ContainsKey(definition.PointId))
                {
                    diagnosticTag = "physical_locator.definition_invalid";
                    Stop(generation);
                    return false;
                }
                _points.Add(definition.PointId, new PointRuntime(definition, generation));
            }

            if (!read.Succeeded)
            {
                Debug.LogWarning(
                    $"[PhysicalAugmentation] locator store read failed diagnostic={read.DiagnosticTag}.");
                foreach (var runtime in _points.Values)
                    runtime.Fail(read.DiagnosticTag);
                Publish();
                return true;
            }

            var requests = new List<LoadRequest>();
            foreach (var runtime in _points.Values)
            {
                if (!read.Snapshot.TryGetBinding(runtime.Definition.PointId, out var binding))
                {
                    runtime.SetPhase(PhysicalAugmentationLocalizationPhase.Unconfigured, "physical_locator.unconfigured");
                    continue;
                }
                if (binding.UniformScale < runtime.Definition.MinimumCalibrationScale ||
                    binding.UniformScale > runtime.Definition.MaximumCalibrationScale)
                {
                    runtime.Fail("physical_locator.scale_out_of_range");
                    continue;
                }
                runtime.Configure(binding);
                requests.Add(new LoadRequest(runtime, binding.AnchorUuid));
            }
            Publish();
            Debug.Log(
                $"[PhysicalAugmentation] locator configured requestCount={requests.Count} " +
                $"unconfigured={definitions.Count - requests.Count} generation={generation}.");
            if (requests.Count > 0) _ = RunLoadAsync(requests, generation);
            return true;
        }

        public void Stop(long generation)
        {
            if (!_running) return;
            _running = false;
            _generation++;
            foreach (var runtime in _points.Values) runtime.Release();
            _points.Clear();
        }

        public void Tick(float now)
        {
            if (!_running || _disposed || !float.IsFinite(now)) return;
            var changed = false;
            foreach (var runtime in _points.Values) changed |= runtime.Sample(now);
            if (changed) Publish();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop(_generation);
            _disposed = true;
        }

        async Task RunLoadAsync(List<LoadRequest> requests, long generation)
        {
            for (var offset = 0; offset < requests.Count; offset += MaximumBatchSize)
            {
                if (!IsCurrent(generation)) return;
                var count = Math.Min(MaximumBatchSize, requests.Count - offset);
                var uuids = new Guid[count];
                for (var index = 0; index < count; index++)
                {
                    var request = requests[offset + index];
                    request.Runtime.SetPhase(
                        PhysicalAugmentationLocalizationPhase.Localizing,
                        "physical_locator.localizing");
                    uuids[index] = request.Uuid;
                }
                Publish();

                PhysicalAnchorPlatformLoadResult result;
                Debug.Log(
                    $"[PhysicalAugmentation] locator loading batchCount={count} generation={generation}.");
                try
                {
                    result = await _platform.LoadBatchAsync(uuids);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[PhysicalAugmentation] locator platform exception batchCount={count} " +
                        $"exception={exception.GetType().Name}.");
                    result = new PhysicalAnchorPlatformLoadResult(
                        false,
                        Array.Empty<IPhysicalAnchorPlatformLease>(),
                        "physical_locator.platform_exception");
                }
                if (!IsCurrent(generation))
                {
                    ReleaseAll(result.Leases);
                    return;
                }
                Debug.Log(
                    $"[PhysicalAugmentation] locator batch result batchCount={count} succeeded={result.Succeeded} " +
                    $"leases={result.Leases?.Count ?? 0} diagnostic={result.DiagnosticTag} generation={generation}.");
                if (!result.Succeeded)
                {
                    for (var index = 0; index < count; index++)
                        requests[offset + index].Runtime.Fail(
                            string.IsNullOrWhiteSpace(result.DiagnosticTag)
                                ? "physical_locator.batch_failed"
                                : result.DiagnosticTag);
                    ReleaseAll(result.Leases);
                    Publish();
                    continue;
                }

                var leasesByUuid = new Dictionary<Guid, IPhysicalAnchorPlatformLease>();
                for (var index = 0; index < result.Leases.Count; index++)
                {
                    var lease = result.Leases[index];
                    if (lease == null || lease.Uuid == Guid.Empty || !Contains(uuids, lease.Uuid) ||
                        leasesByUuid.ContainsKey(lease.Uuid))
                    {
                        TryDispose(lease);
                        continue;
                    }
                    leasesByUuid.Add(lease.Uuid, lease);
                }
                var missingCount = 0;
                for (var index = 0; index < count; index++)
                {
                    var request = requests[offset + index];
                    if (leasesByUuid.TryGetValue(request.Uuid, out var lease))
                        request.Runtime.Attach(lease);
                    else
                    {
                        missingCount++;
                        request.Runtime.Fail("physical_locator.anchor_missing");
                    }
                }
                if (missingCount > 0)
                    Debug.LogWarning(
                        $"[PhysicalAugmentation] locator missing platform anchors count={missingCount} " +
                        $"requested={count} generation={generation}.");
                Publish();
            }
        }

        void Publish()
        {
            if (!_running) return;
            var states = new List<PhysicalAugmentationPointState>(_points.Count);
            foreach (var runtime in _points.Values) states.Add(runtime.ToState());
            states.Sort((left, right) => left.PointId.CompareTo(right.PointId));
            try { StateChanged?.Invoke(states); } catch (Exception) { }
        }

        bool IsCurrent(long generation) => _running && !_disposed && _generation == generation;

        static bool Contains(Guid[] values, Guid value)
        {
            for (var index = 0; index < values.Length; index++)
                if (values[index] == value) return true;
            return false;
        }

        static void ReleaseAll(IReadOnlyList<IPhysicalAnchorPlatformLease> leases)
        {
            if (leases == null) return;
            for (var index = 0; index < leases.Count; index++) TryDispose(leases[index]);
        }

        static void TryDispose(IPhysicalAnchorPlatformLease lease)
        {
            if (lease == null) return;
            try { lease.Dispose(); } catch (Exception) { }
        }

        readonly struct LoadRequest
        {
            public LoadRequest(PointRuntime runtime, Guid uuid)
            {
                Runtime = runtime;
                Uuid = uuid;
            }
            public PointRuntime Runtime { get; }
            public Guid Uuid { get; }
        }

        sealed class PointRuntime
        {
            readonly long _generation;
            PhysicalAnchorBinding _binding;
            IPhysicalAnchorPlatformLease _lease;
            PhysicalAugmentationLocalizationPhase _phase;
            string _diagnosticTag;
            Pose _resolvedPose;
            Pose _stabilityBaseline;
            float _stabilityStartedAt;
            float _lostStartedAt;
            bool _hasResolvedPose;
            bool _hasStabilityBaseline;
            bool _released;

            public PointRuntime(PhysicalAugmentationDefinition definition, long generation)
            {
                Definition = definition;
                _generation = generation;
                _phase = PhysicalAugmentationLocalizationPhase.Unconfigured;
                _diagnosticTag = string.Empty;
            }

            public PhysicalAugmentationDefinition Definition { get; }

            public void Configure(PhysicalAnchorBinding binding)
            {
                _binding = binding;
                SetPhase(PhysicalAugmentationLocalizationPhase.Loading, "physical_locator.loading");
            }

            public void Attach(IPhysicalAnchorPlatformLease lease)
            {
                Release();
                _released = false;
                _lease = lease;
                _hasResolvedPose = false;
                _hasStabilityBaseline = false;
                SetPhase(PhysicalAugmentationLocalizationPhase.Stabilizing, "physical_locator.stabilizing");
            }

            public void SetPhase(PhysicalAugmentationLocalizationPhase phase, string diagnosticTag)
            {
                _phase = phase;
                _diagnosticTag = diagnosticTag ?? string.Empty;
            }

            public void Fail(string diagnosticTag)
            {
                Release();
                _hasResolvedPose = false;
                _hasStabilityBaseline = false;
                SetPhase(PhysicalAugmentationLocalizationPhase.Failed, diagnosticTag);
            }

            public bool Sample(float now)
            {
                if (_lease == null || _phase == PhysicalAugmentationLocalizationPhase.Failed ||
                    _phase == PhysicalAugmentationLocalizationPhase.Unconfigured)
                    return false;
                if (!_lease.IsTracked || !_lease.TryGetPose(out var anchorPose))
                    return SampleLost(now);

                var resolved = ResolveConfiguredPose(anchorPose);
                if (_phase == PhysicalAugmentationLocalizationPhase.Lost)
                {
                    BeginStabilizing(anchorPose, now);
                    return true;
                }
                if (_phase == PhysicalAugmentationLocalizationPhase.Stabilizing)
                {
                    if (!_hasStabilityBaseline || !WithinStabilityTolerance(anchorPose, _stabilityBaseline))
                    {
                        BeginStabilizing(anchorPose, now);
                        return true;
                    }
                    if (now - _stabilityStartedAt < Definition.StabilitySeconds) return false;
                    _resolvedPose = resolved;
                    _hasResolvedPose = true;
                    SetPhase(PhysicalAugmentationLocalizationPhase.Stable, string.Empty);
                    return true;
                }
                if (_phase != PhysicalAugmentationLocalizationPhase.Stable) return false;

                if (Vector3.Distance(_resolvedPose.position, resolved.position) >
                        Definition.RebasePositionThresholdMeters ||
                    Quaternion.Angle(_resolvedPose.rotation, resolved.rotation) >
                        Definition.RebaseOrientationThresholdDegrees)
                {
                    _hasResolvedPose = false;
                    BeginStabilizing(anchorPose, now);
                    return true;
                }
                if (Vector3.Distance(_resolvedPose.position, resolved.position) <= 0.001f &&
                    Quaternion.Angle(_resolvedPose.rotation, resolved.rotation) <= 0.1f)
                    return false;
                _resolvedPose = resolved;
                return true;
            }

            bool SampleLost(float now)
            {
                if (_phase != PhysicalAugmentationLocalizationPhase.Lost)
                {
                    _lostStartedAt = now;
                    _hasResolvedPose = _phase == PhysicalAugmentationLocalizationPhase.Stable && _hasResolvedPose;
                    _hasStabilityBaseline = false;
                    SetPhase(PhysicalAugmentationLocalizationPhase.Lost, "physical_locator.lost");
                    return true;
                }
                if (!_hasResolvedPose || now - _lostStartedAt <= Definition.LostGraceSeconds) return false;
                _hasResolvedPose = false;
                _diagnosticTag = "physical_locator.lost_grace_expired";
                return true;
            }

            void BeginStabilizing(Pose anchorPose, float now)
            {
                _phase = PhysicalAugmentationLocalizationPhase.Stabilizing;
                _diagnosticTag = "physical_locator.stabilizing";
                _stabilityBaseline = anchorPose;
                _stabilityStartedAt = now;
                _hasStabilityBaseline = true;
                _hasResolvedPose = false;
            }

            bool WithinStabilityTolerance(Pose current, Pose baseline)
                => Vector3.Distance(current.position, baseline.position) <= Definition.PositionToleranceMeters &&
                   Quaternion.Angle(current.rotation, baseline.rotation) <= Definition.OrientationToleranceDegrees;

            public PhysicalAugmentationPointState ToState()
                => new PhysicalAugmentationPointState(
                    Definition.PointId,
                    _phase,
                    _generation,
                    _hasResolvedPose,
                    _hasResolvedPose ? _resolvedPose : Pose.identity,
                    _hasResolvedPose ? _binding.UniformScale * Definition.ModelUniformScale : 0f,
                    PhysicalAugmentationPointActivityPhase.Inactive,
                    _diagnosticTag);

            Pose ResolveConfiguredPose(Pose anchorPose)
            {
                var installedPose = PhysicalAugmentationPose.Resolve(
                    anchorPose,
                    _binding.AugmentationPoseInAnchorSpace);
                return PhysicalAugmentationPose.Resolve(
                    installedPose,
                    new Pose(Vector3.zero, Definition.ModelRotationInAnchorSpace));
            }

            public void Release()
            {
                if (_released) return;
                _released = true;
                var lease = _lease;
                _lease = null;
                if (lease != null) TryDispose(lease);
            }
        }
    }
}
