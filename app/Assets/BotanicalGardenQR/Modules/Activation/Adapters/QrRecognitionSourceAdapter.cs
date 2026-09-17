using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Activation.Runtime;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace BotanicalGardenQR.Activation.Adapters
{
    [DisallowMultipleComponent]
    public sealed class QrRecognitionSourceAdapter : MonoBehaviour, IRecognitionSourceProvider
    {
        [SerializeField] QRCodeManager _manager;
        Subscription _activeSubscription;

        public IRecognitionSource CreateSource()
        {
            if (_manager == null)
                throw new InvalidOperationException("QR recognition requires an explicitly assigned QRCodeManager.");
            return new Source(this, _manager);
        }

        void Update()
            => _activeSubscription?.Tick(Time.realtimeSinceStartupAsDouble, DateTimeOffset.UtcNow);

        void OnDestroy()
        {
            _activeSubscription?.Dispose();
            _activeSubscription = null;
        }

        sealed class Source : IRecognitionSource, IRecognitionConfirmationTiming,
            IRecognitionFocusQueryConsumer, IRecognitionRoundRearm
        {
            readonly QrRecognitionSourceAdapter _owner;
            readonly QRCodeManager _manager;
            float _confirmationSeconds = 1f;
            float _lostGraceSeconds = 0.2f;
            float _gazeLostGraceSeconds = 0.15f;
            IRecognitionFocusQueryProvider _focusQueryProvider;

            public Source(QrRecognitionSourceAdapter owner, QRCodeManager manager)
            {
                _owner = owner;
                _manager = manager;
            }

            public SourceKind Kind => RecognitionSourceKinds.Qr;

            public bool TryRearm(RecognitionObservation observation)
                => _owner._activeSubscription != null &&
                   _owner._activeSubscription.RequestRearm(observation);

            public void ConfigureConfirmationTiming(
                float confirmationSeconds,
                float lostGraceSeconds,
                float gazeLostGraceSeconds)
            {
                if (!IsFinitePositive(confirmationSeconds) || float.IsNaN(lostGraceSeconds) ||
                    float.IsInfinity(lostGraceSeconds) || lostGraceSeconds < 0f ||
                    float.IsNaN(gazeLostGraceSeconds) || float.IsInfinity(gazeLostGraceSeconds) ||
                    gazeLostGraceSeconds < 0f)
                    throw new ArgumentOutOfRangeException(nameof(confirmationSeconds), "QR confirmation timing is invalid.");
                _confirmationSeconds = confirmationSeconds;
                _lostGraceSeconds = lostGraceSeconds;
                _gazeLostGraceSeconds = gazeLostGraceSeconds;
            }

            public void ConfigureFocusQueryProvider(IRecognitionFocusQueryProvider provider)
            {
                _focusQueryProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            }

            public IDisposable Start(IRecognitionObservationSink sink)
            {
                if (sink == null) throw new ArgumentNullException(nameof(sink));
                if (_owner._activeSubscription != null)
                    throw new InvalidOperationException("QR recognition is already subscribed.");
                if (_focusQueryProvider == null)
                    throw new InvalidOperationException("QR recognition requires an explicit focus query provider.");
                var subscription = new Subscription(
                    _owner,
                    _manager,
                    sink,
                    _confirmationSeconds,
                    _lostGraceSeconds,
                    _gazeLostGraceSeconds,
                    _focusQueryProvider);
                _owner._activeSubscription = subscription;
                return subscription;
            }
        }

        sealed class Subscription : IDisposable
        {
            readonly QrRecognitionSourceAdapter _owner;
            readonly TimeSpan _lostGrace;
            readonly IRecognitionFocusQueryProvider _focusQueryProvider;
            readonly QrFocusConfirmationEngine _focus;
            readonly Dictionary<MRUKTrackable, Entry> _entries = new Dictionary<MRUKTrackable, Entry>();
            readonly List<MRUKTrackable> _expired = new List<MRUKTrackable>();
            readonly List<QrFocusTarget> _focusTargets = new List<QrFocusTarget>();
            QRCodeManager _manager;
            IRecognitionObservationSink _sink;

            public Subscription(
                QrRecognitionSourceAdapter owner,
                QRCodeManager manager,
                IRecognitionObservationSink sink,
                float confirmationSeconds,
                float lostGraceSeconds,
                float gazeLostGraceSeconds,
                IRecognitionFocusQueryProvider focusQueryProvider)
            {
                _owner = owner;
                _manager = manager;
                _sink = sink;
                _lostGrace = TimeSpan.FromSeconds(lostGraceSeconds);
                _focusQueryProvider = focusQueryProvider ?? throw new ArgumentNullException(nameof(focusQueryProvider));
                _focus = new QrFocusConfirmationEngine(confirmationSeconds, gazeLostGraceSeconds);
                manager.CodeDetected += OnDetected;
                manager.CodeRemoved += OnRemoved;
                // Meta reports QR discovery through a one-shot TrackableAdded
                // event. Replay codes found before Activation finished wiring so
                // a slow feature bootstrap cannot make an already visible QR inert.
                manager.ReplayActiveCodes(OnDetected);
            }

            public void Dispose()
            {
                var manager = _manager;
                _manager = null;
                _sink = null;
                _entries.Clear();
                _expired.Clear();
                _focusTargets.Clear();
                if (ReferenceEquals(_owner._activeSubscription, this))
                    _owner._activeSubscription = null;
                if (manager == null) return;
                manager.CodeDetected -= OnDetected;
                manager.CodeRemoved -= OnRemoved;
            }

            public void Tick(double monotonicNow, DateTimeOffset observedAt)
            {
                if (_sink == null) return;
                RecoverTrackedEntries(observedAt);
                ExpireLostEntries(observedAt);
                BuildFocusTargets();
                RecognitionFocusQuery? focusQuery = _focusQueryProvider.TryGetQuery(out var query)
                    ? query
                    : (RecognitionFocusQuery?)null;
                Publish(_focus.Tick(monotonicNow, focusQuery, _focusTargets), observedAt);
            }

            public bool RequestRearm(RecognitionObservation observation)
            {
                if (_sink == null || observation.ObservationId == Guid.Empty ||
                    observation.SourceKind != RecognitionSourceKinds.Qr)
                    return false;

                Entry fallback = null;
                foreach (var pair in _entries)
                {
                    var entry = pair.Value;
                    if (pair.Key == null ||
                        !string.Equals(entry.Payload, observation.SourceValue, StringComparison.Ordinal))
                        continue;
                    if (entry.Id == observation.ObservationId && CanRearm(entry))
                    {
                        _focus.Rearm(entry.Id);
                        return true;
                    }
                    if (fallback == null && CanRearm(entry))
                        fallback = entry;
                }

                if (fallback == null) return false;
                _focus.Rearm(fallback.Id);
                return true;
            }

            static bool CanRearm(Entry entry)
                => entry != null &&
                   !entry.LostPublished &&
                   !entry.RemoveWhenLost;

            void RecoverTrackedEntries(DateTimeOffset now)
            {
                // MRUK can restore IsTracked through an update without replaying
                // CodeDetected. Keep the source entry through loss so that the
                // restored trackable can begin a new scan round here.
                foreach (var pair in _entries)
                {
                    var trackable = pair.Key;
                    var entry = pair.Value;
                    if (!entry.LostAt.HasValue || entry.RemoveWhenLost || trackable == null || !trackable.IsTracked)
                        continue;

                    if (entry.LostPublished)
                    {
                        entry.BeginRound(now);
                    }
                    else
                    {
                        entry.LostAt = null;
                    }
                }
            }

            void ExpireLostEntries(DateTimeOffset now)
            {
                _expired.Clear();
                foreach (var pair in _entries)
                {
                    var trackable = pair.Key;
                    var entry = pair.Value;
                    if (!entry.LostAt.HasValue && trackable == null)
                        entry.LostAt = now;
                    if (!entry.LostAt.HasValue && !trackable.IsTracked)
                        entry.LostAt = now;
                    if (entry.LostAt.HasValue)
                    {
                        if (now - entry.LostAt.Value < _lostGrace)
                            continue;
                        if (!entry.LostPublished)
                        {
                            Publish(entry, TrackingState.Lost, trackable == null ? null : trackable.transform, now, 0f, false);
                            entry.LostPublished = true;
                        }
                        if (entry.RemoveWhenLost || trackable == null)
                            _expired.Add(trackable);
                        continue;
                    }

                }

                foreach (var trackable in _expired)
                    _entries.Remove(trackable);
            }

            void BuildFocusTargets()
            {
                _focusTargets.Clear();
                foreach (var pair in _entries)
                {
                    var trackable = pair.Key;
                    var entry = pair.Value;
                    if (trackable == null || !trackable.IsTracked || entry.LostAt.HasValue ||
                        entry.LostPublished || !trackable.PlaneRect.HasValue ||
                        !TryGetEvidence(trackable.transform, out var evidence))
                        continue;
                    _focusTargets.Add(
                        new QrFocusTarget(
                            entry.Id,
                            evidence,
                            trackable.PlaneRect.Value));
                }
            }

            void Publish(QrFocusFrame frame, DateTimeOffset observedAt)
            {
                if (frame.Cleared.HasValue)
                    Publish(frame.Cleared.Value, observedAt);
                if (frame.Current.HasValue)
                    Publish(frame.Current.Value, observedAt);
            }

            void Publish(QrFocusSignal signal, DateTimeOffset observedAt)
            {
                if (!TryFind(signal.ObservationId, out var trackable, out var entry)) return;
                Publish(
                    entry,
                    TrackingState.Tracked,
                    trackable == null ? null : trackable.transform,
                    observedAt,
                    signal.Progress,
                    signal.IsConfirmation);
            }

            bool TryFind(Guid observationId, out MRUKTrackable trackable, out Entry entry)
            {
                foreach (var pair in _entries)
                {
                    if (pair.Value.Id != observationId) continue;
                    trackable = pair.Key;
                    entry = pair.Value;
                    return true;
                }

                trackable = null;
                entry = null;
                return false;
            }

            void OnDetected(MRUKTrackable trackable, string payload)
            {
                if (_sink == null || trackable == null || string.IsNullOrWhiteSpace(payload)) return;
                var normalized = payload.Trim();
                var now = DateTimeOffset.UtcNow;
                if (!_entries.TryGetValue(trackable, out var entry) ||
                    !string.Equals(entry.Payload, normalized, StringComparison.Ordinal) ||
                    (entry.LostAt.HasValue && now - entry.LostAt.Value >= _lostGrace))
                {
                    entry = new Entry(Guid.NewGuid(), normalized, now);
                    _entries[trackable] = entry;
                }
                else
                {
                    entry.LostAt = null;
                    entry.RemoveWhenLost = false;
                }

                entry.Sink = _sink;
            }

            void OnRemoved(MRUKTrackable trackable)
            {
                if (_sink == null || trackable == null || !_entries.TryGetValue(trackable, out var entry)) return;
                if (!entry.LostAt.HasValue)
                    entry.LostAt = DateTimeOffset.UtcNow;
                entry.RemoveWhenLost = true;
            }

            static void Publish(
                Entry entry,
                TrackingState trackingState,
                Transform pose,
                DateTimeOffset observedAt,
                float progress,
                bool isConfirmation)
            {
                if (entry == null) return;
                entry.Revision++;
                entry.LastRefresh = observedAt;
                SpatialEvidence? evidence = null;
                if (pose != null)
                    evidence = new SpatialEvidence(pose.position, pose.rotation, trackingState != TrackingState.Lost);

                entry.Sink?.Publish(new RecognitionObservation(
                    entry.Id,
                    entry.Revision,
                    observedAt,
                    RecognitionSourceKinds.Qr,
                    entry.Payload,
                    trackingState,
                    evidence,
                    progress,
                    isConfirmation));
            }

            sealed class Entry
            {
                public Entry(Guid id, string payload, DateTimeOffset startedAt)
                {
                    Id = id;
                    Payload = payload;
                    LastRefresh = startedAt;
                    Sink = null;
                }

                public Guid Id { get; private set; }
                public string Payload { get; }
                public DateTimeOffset LastRefresh { get; set; }
                public DateTimeOffset? LostAt { get; set; }
                public long Revision { get; set; }
                public bool LostPublished { get; set; }
                public bool RemoveWhenLost { get; set; }
                public IRecognitionObservationSink Sink { get; set; }

                public void BeginRound(DateTimeOffset startedAt)
                {
                    Id = Guid.NewGuid();
                    LastRefresh = startedAt;
                    LostAt = null;
                    Revision = 0;
                    LostPublished = false;
                    RemoveWhenLost = false;
                }
            }
        }

        static bool IsFinitePositive(float value)
            => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        static bool TryGetEvidence(Transform pose, out SpatialEvidence evidence)
        {
            if (pose != null && IsFinite(pose.position) && IsUsable(pose.rotation))
            {
                evidence = new SpatialEvidence(pose.position, pose.rotation, true);
                return true;
            }

            evidence = default;
            return false;
        }

        static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        static bool IsUsable(Quaternion value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w) &&
               value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > 0.000001f;

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
