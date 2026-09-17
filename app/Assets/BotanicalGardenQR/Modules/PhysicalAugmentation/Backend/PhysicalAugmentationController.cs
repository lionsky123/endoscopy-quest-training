using System;
using System.Collections.Generic;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Backend
{
    public sealed class PhysicalAugmentationController : IPhysicalAugmentationController
    {
        readonly IPhysicalAugmentationDefinitionSource _definitions;
        readonly IPhysicalAugmentationLocator _locator;
        readonly IPhysicalAugmentationSuppressionSource _suppression;
        readonly IPhysicalAugmentationCapabilityGate _capability;
        readonly IPhysicalAugmentationPerformanceFactory _performances;
        readonly List<IPhysicalAugmentationStateSink> _observers = new List<IPhysicalAugmentationStateSink>();
        readonly Dictionary<PhysicalAugmentationPointId, PhysicalAugmentationPointState> _localized =
            new Dictionary<PhysicalAugmentationPointId, PhysicalAugmentationPointState>();
        readonly Dictionary<PhysicalAugmentationPointId, string> _pointFailures =
            new Dictionary<PhysicalAugmentationPointId, string>();
        readonly Dictionary<PhysicalAugmentationPointId, IPhysicalAugmentationPerformanceLease> _activePerformances =
            new Dictionary<PhysicalAugmentationPointId, IPhysicalAugmentationPerformanceLease>();
        readonly HashSet<PhysicalAugmentationPointId> _presented =
            new HashSet<PhysicalAugmentationPointId>();

        PhysicalAugmentationState _state;
        long _generation;
        long _version;
        bool _started;

        internal PhysicalAugmentationController(
            IPhysicalAugmentationDefinitionSource definitions,
            IPhysicalAugmentationLocator locator,
            IPhysicalAugmentationSuppressionSource suppression,
            IPhysicalAugmentationCapabilityGate capability,
            IPhysicalAugmentationPerformanceFactory performances)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _locator = locator ?? throw new ArgumentNullException(nameof(locator));
            _suppression = suppression ?? throw new ArgumentNullException(nameof(suppression));
            _capability = capability ?? throw new ArgumentNullException(nameof(capability));
            _performances = performances ?? throw new ArgumentNullException(nameof(performances));
            _locator.StateChanged += HandleLocatorStateChanged;
            _state = new PhysicalAugmentationState(
                0,
                0,
                PhysicalAugmentationPhase.Stopped,
                Array.Empty<PhysicalAugmentationPointState>());
        }

        public PhysicalAugmentationResult Start()
        {
            if (_started)
                return PhysicalAugmentationResult.Failure(
                    PhysicalAugmentationFailureCode.AlreadyStarted,
                    "physical_augmentation.already_started",
                    _generation);

            IReadOnlyList<PhysicalAugmentationDefinition> definitions;
            try
            {
                definitions = _definitions.Definitions;
                if (definitions == null) throw new InvalidOperationException("Definition source returned null.");
            }
            catch (Exception)
            {
                _generation++;
                PublishFailure("physical_augmentation.definition_invalid");
                return PhysicalAugmentationResult.Failure(
                    PhysicalAugmentationFailureCode.InvalidDefinition,
                    "physical_augmentation.definition_invalid",
                    _generation);
            }

            _generation++;
            _started = true;
            _localized.Clear();
            _pointFailures.Clear();
            StopAllPerformances();
            _presented.Clear();
            Publish(PhysicalAugmentationPhase.Starting, BuildPointStates(definitions));
            try
            {
                if (_locator.TryStart(definitions, _generation, out var diagnosticTag))
                {
                    Refresh();
                    return PhysicalAugmentationResult.Success(_generation);
                }

                _started = false;
                PublishFailure(string.IsNullOrWhiteSpace(diagnosticTag)
                    ? "physical_augmentation.locator_start_failed"
                    : diagnosticTag);
                return PhysicalAugmentationResult.Failure(
                    MapLocatorStartFailure(diagnosticTag),
                    "physical_augmentation.locator_start_failed",
                    _generation);
            }
            catch (Exception)
            {
                _started = false;
                PublishFailure("physical_augmentation.locator_start_exception");
                return PhysicalAugmentationResult.Failure(
                    PhysicalAugmentationFailureCode.RuntimeFailed,
                    "physical_augmentation.locator_start_exception",
                    _generation);
            }
        }

        public PhysicalAugmentationResult Activate(PhysicalAugmentationPointId pointId, long generation)
        {
            if (!_started)
                return Failure(PhysicalAugmentationFailureCode.NotStarted, "physical_augmentation.not_started");
            if (generation != _generation)
                return Failure(PhysicalAugmentationFailureCode.StaleGeneration, "physical_augmentation.generation_stale");
            if (!pointId.IsValid || !_definitions.TryGet(pointId, out var definition))
                return Failure(PhysicalAugmentationFailureCode.UnconfiguredPoint, "physical_augmentation.point_unknown");
            if (!_localized.TryGetValue(pointId, out var localized) ||
                localized.Phase != PhysicalAugmentationLocalizationPhase.Stable ||
                !localized.HasResolvedPose)
                return Failure(PhysicalAugmentationFailureCode.PointNotStable, "physical_augmentation.point_not_stable");
            _pointFailures.Remove(pointId);
            if (IsSuppressed(pointId, out _))
                return Failure(PhysicalAugmentationFailureCode.Suppressed, "physical_augmentation.point_suppressed");
            if (!IsCapabilityAvailable(definition, out var capabilityTag))
                return Failure(
                    PhysicalAugmentationFailureCode.CapabilityUnavailable,
                    string.IsNullOrWhiteSpace(capabilityTag) ? "physical_augmentation.capability_unavailable" : capabilityTag);
            if (!_performances.TryPrepare(
                    definition,
                    localized.ResolvedPose,
                    localized.UniformScale,
                    _generation,
                    out var lease,
                    out var preparationTag) ||
                lease == null)
                return Failure(
                    PhysicalAugmentationFailureCode.PreparationFailed,
                    string.IsNullOrWhiteSpace(preparationTag) ? "physical_augmentation.prepare_failed" : preparationTag);

            StopPerformance(pointId);
            _activePerformances.Add(pointId, lease);
            Refresh();
            try
            {
                lease.Play(completion => HandlePerformanceCompleted(lease, pointId, generation, completion));
                return PhysicalAugmentationResult.Success(_generation);
            }
            catch (Exception)
            {
                if (_activePerformances.TryGetValue(pointId, out var active) && ReferenceEquals(active, lease))
                    StopPerformance(pointId);
                Refresh();
                return Failure(PhysicalAugmentationFailureCode.RuntimeFailed, "physical_augmentation.play_failed");
            }
        }

        public PhysicalAugmentationResult StopPerformances()
        {
            StopAllPerformances();
            _presented.Clear();
            if (_started) Refresh();
            return PhysicalAugmentationResult.Success(_generation);
        }

        public PhysicalAugmentationResult RetryLocalization()
        {
            if (!_started)
                return Failure(
                    PhysicalAugmentationFailureCode.NotStarted,
                    "physical_augmentation.localization_retry_not_started");
            if (_activePerformances.Count > 0)
                return Failure(
                    PhysicalAugmentationFailureCode.Suppressed,
                    "physical_augmentation.localization_retry_while_playing");

            IReadOnlyList<PhysicalAugmentationDefinition> definitions;
            try
            {
                definitions = _definitions.Definitions;
                if (definitions == null) throw new InvalidOperationException("Definition source returned null.");
            }
            catch (Exception)
            {
                PublishFailure("physical_augmentation.definition_invalid");
                return Failure(
                    PhysicalAugmentationFailureCode.InvalidDefinition,
                    "physical_augmentation.definition_invalid");
            }

            var previousGeneration = _generation;
            try
            {
                _locator.Stop(previousGeneration);
            }
            catch (Exception)
            {
                PublishFailure("physical_augmentation.localization_retry_stop_failed");
                return Failure(
                    PhysicalAugmentationFailureCode.RuntimeFailed,
                    "physical_augmentation.localization_retry_stop_failed");
            }

            _generation++;
            _localized.Clear();
            _pointFailures.Clear();
            Publish(PhysicalAugmentationPhase.Starting, BuildPointStates(definitions));
            try
            {
                if (_locator.TryStart(definitions, _generation, out var diagnosticTag))
                {
                    Refresh();
                    return PhysicalAugmentationResult.Success(_generation);
                }

                PublishFailure(string.IsNullOrWhiteSpace(diagnosticTag)
                    ? "physical_augmentation.localization_retry_failed"
                    : diagnosticTag);
                return Failure(
                    MapLocatorStartFailure(diagnosticTag),
                    "physical_augmentation.localization_retry_failed");
            }
            catch (Exception)
            {
                PublishFailure("physical_augmentation.localization_retry_exception");
                return Failure(
                    PhysicalAugmentationFailureCode.RuntimeFailed,
                    "physical_augmentation.localization_retry_exception");
            }
        }

        public PhysicalAugmentationResult Stop()
        {
            if (!_started)
                return PhysicalAugmentationResult.Success(_generation);
            var stoppedGeneration = _generation;
            _started = false;
            _generation++;
            StopAllPerformances();
            _presented.Clear();
            _localized.Clear();
            _pointFailures.Clear();
            try
            {
                _locator.Stop(stoppedGeneration);
            }
            catch (Exception)
            {
                PublishFailure("physical_augmentation.locator_stop_failed");
                return PhysicalAugmentationResult.Failure(
                    PhysicalAugmentationFailureCode.RuntimeFailed,
                    "physical_augmentation.locator_stop_failed",
                    _generation);
            }
            Publish(
                PhysicalAugmentationPhase.Stopped,
                Array.Empty<PhysicalAugmentationPointState>());
            return PhysicalAugmentationResult.Success(_generation);
        }

        static PhysicalAugmentationFailureCode MapLocatorStartFailure(string diagnosticTag)
        {
            switch (diagnosticTag ?? string.Empty)
            {
                case "physical_locator.definition_invalid":
                case "physical_locator.start_invalid":
                case "physical_locator.disposed":
                    return PhysicalAugmentationFailureCode.InvalidDefinition;
                case "physical_locator.single_flight":
                    return PhysicalAugmentationFailureCode.AlreadyStarted;
                default:
                    return PhysicalAugmentationFailureCode.CapabilityUnavailable;
            }
        }

        public IDisposable Observe(IPhysicalAugmentationStateSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (!_observers.Contains(sink)) _observers.Add(sink);
            TryNotify(sink, _state);
            return new Observation(this, sink);
        }

        internal void RefreshExternalAvailability()
        {
            if (_started) Refresh();
        }

        void HandleLocatorStateChanged(IReadOnlyList<PhysicalAugmentationPointState> states)
        {
            if (!_started || states == null) return;
            var next = new Dictionary<PhysicalAugmentationPointId, PhysicalAugmentationPointState>();
            for (var index = 0; index < states.Count; index++)
            {
                var state = states[index];
                if (state.Generation != _generation || !state.PointId.IsValid || next.ContainsKey(state.PointId))
                    return;
                if (!_definitions.TryGet(state.PointId, out _)) continue;
                next.Add(state.PointId, state);
            }
            _localized.Clear();
            foreach (var pair in next) _localized.Add(pair.Key, pair.Value);

            if (_activePerformances.Count > 0)
            {
                var lostPoints = new List<PhysicalAugmentationPointId>();
                foreach (var pair in _activePerformances)
                    if (!_localized.TryGetValue(pair.Key, out var playingState) ||
                        playingState.Phase != PhysicalAugmentationLocalizationPhase.Stable ||
                        !playingState.HasResolvedPose)
                        lostPoints.Add(pair.Key);
                for (var index = 0; index < lostPoints.Count; index++)
                    StopPerformance(lostPoints[index]);
            }
            Refresh();
        }

        void HandlePerformanceCompleted(
            IPhysicalAugmentationPerformanceLease lease,
            PhysicalAugmentationPointId pointId,
            long generation,
            PhysicalAugmentationPerformanceCompletion completion)
        {
            if (!_started || generation != _generation ||
                !_activePerformances.TryGetValue(pointId, out var active) || !ReferenceEquals(active, lease)) return;
            if (completion.Succeeded)
            {
                _pointFailures.Remove(pointId);
                // A completed reality performance remains visible at its authored
                // final pose until the visitor explicitly returns to the panel,
                // leaves the Model page, replays the point, or the module stops.
                _presented.Add(pointId);
            }
            else
            {
                _activePerformances.Remove(pointId);
                try
                {
                    lease.Dispose();
                }
                catch (Exception)
                {
                    // The state owner still completes its transaction; adapter cleanup is idempotent and best-effort.
                }
                _pointFailures[pointId] = string.IsNullOrWhiteSpace(completion.DiagnosticTag)
                    ? "physical_augmentation.performance_failed"
                    : completion.DiagnosticTag;
            }
            Refresh();
        }

        void Refresh()
        {
            if (!_started) return;
            var points = BuildPointStates(_definitions.Definitions);
            var phase = _activePerformances.Count > 0
                ? PhysicalAugmentationPhase.Playing
                : PhysicalAugmentationPhase.Ready;
            Publish(phase, points);
        }

        List<PhysicalAugmentationPointState> BuildPointStates(
            IReadOnlyList<PhysicalAugmentationDefinition> definitions)
        {
            var points = new List<PhysicalAugmentationPointState>(definitions.Count);
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                var hasLocalized = _localized.TryGetValue(definition.PointId, out var localized);
                var localizationPhase = hasLocalized
                    ? localized.Phase
                    : PhysicalAugmentationLocalizationPhase.Unconfigured;
                var hasPose = hasLocalized && localized.HasResolvedPose;
                var pose = hasPose ? localized.ResolvedPose : Pose.identity;
                var scale = hasPose ? localized.UniformScale : 0f;
                var diagnosticTag = hasLocalized ? localized.DiagnosticTag : string.Empty;
                PhysicalAugmentationPointActivityPhase activity;
                if (_activePerformances.ContainsKey(definition.PointId))
                {
                    activity = _presented.Contains(definition.PointId)
                        ? PhysicalAugmentationPointActivityPhase.Presented
                        : PhysicalAugmentationPointActivityPhase.Playing;
                }
                else if (_pointFailures.TryGetValue(definition.PointId, out var pointFailureTag))
                {
                    activity = PhysicalAugmentationPointActivityPhase.Failed;
                    diagnosticTag = pointFailureTag;
                }
                else if (localizationPhase == PhysicalAugmentationLocalizationPhase.Failed)
                {
                    activity = PhysicalAugmentationPointActivityPhase.Failed;
                }
                else if (localizationPhase != PhysicalAugmentationLocalizationPhase.Stable || !hasPose)
                {
                    activity = PhysicalAugmentationPointActivityPhase.Inactive;
                }
                else if (IsSuppressed(definition.PointId, out var suppressionTag))
                {
                    activity = PhysicalAugmentationPointActivityPhase.Suppressed;
                    diagnosticTag = suppressionTag;
                }
                else if (!IsCapabilityAvailable(definition, out var capabilityTag))
                {
                    activity = PhysicalAugmentationPointActivityPhase.Failed;
                    diagnosticTag = capabilityTag;
                }
                else
                {
                    activity = PhysicalAugmentationPointActivityPhase.Available;
                }

                points.Add(new PhysicalAugmentationPointState(
                    definition.PointId,
                    localizationPhase,
                    _generation,
                    hasPose,
                    pose,
                    scale,
                    activity,
                    diagnosticTag));
            }
            return points;
        }

        bool IsSuppressed(PhysicalAugmentationPointId pointId, out string diagnosticTag)
        {
            try
            {
                return _suppression.IsSuppressed(pointId, out diagnosticTag);
            }
            catch (Exception)
            {
                diagnosticTag = "physical_augmentation.suppression_unavailable";
                return true;
            }
        }

        bool IsCapabilityAvailable(PhysicalAugmentationDefinition definition, out string diagnosticTag)
        {
            try
            {
                return _capability.IsAvailable(definition, out diagnosticTag);
            }
            catch (Exception)
            {
                diagnosticTag = "physical_augmentation.capability_check_failed";
                return false;
            }
        }

        void StopPerformance(PhysicalAugmentationPointId pointId)
        {
            _presented.Remove(pointId);
            if (!_activePerformances.TryGetValue(pointId, out var lease)) return;
            _activePerformances.Remove(pointId);
            try { lease.Stop(); } catch (Exception) { }
            try { lease.Dispose(); } catch (Exception) { }
        }

        void StopAllPerformances()
        {
            if (_activePerformances.Count == 0)
            {
                _presented.Clear();
                return;
            }
            var pointIds = new List<PhysicalAugmentationPointId>(_activePerformances.Keys);
            for (var index = 0; index < pointIds.Count; index++) StopPerformance(pointIds[index]);
        }

        void PublishFailure(string diagnosticTag)
            => Publish(
                PhysicalAugmentationPhase.Failed,
                Array.Empty<PhysicalAugmentationPointState>(),
                diagnosticTag);

        void Publish(
            PhysicalAugmentationPhase phase,
            IReadOnlyList<PhysicalAugmentationPointState> points,
            string diagnosticTag = null)
        {
            _state = new PhysicalAugmentationState(
                ++_version,
                _generation,
                phase,
                points,
                diagnosticTag);
            var observers = _observers.ToArray();
            for (var index = 0; index < observers.Length; index++) TryNotify(observers[index], _state);
        }

        PhysicalAugmentationResult Failure(PhysicalAugmentationFailureCode code, string tag)
            => PhysicalAugmentationResult.Failure(code, tag, _generation);

        static void TryNotify(IPhysicalAugmentationStateSink sink, PhysicalAugmentationState state)
        {
            try { sink.OnPhysicalAugmentationStateChanged(state); } catch (Exception) { }
        }

        void RemoveObserver(IPhysicalAugmentationStateSink sink) => _observers.Remove(sink);

        sealed class Observation : IDisposable
        {
            PhysicalAugmentationController _owner;
            IPhysicalAugmentationStateSink _sink;

            public Observation(PhysicalAugmentationController owner, IPhysicalAugmentationStateSink sink)
            {
                _owner = owner;
                _sink = sink;
            }

            public void Dispose()
            {
                var owner = _owner;
                var sink = _sink;
                _owner = null;
                _sink = null;
                if (owner != null && sink != null) owner.RemoveObserver(sink);
            }
        }
    }
}
