using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;

namespace BotanicalGardenQR.Activation.Runtime
{
    internal sealed class ObservationGate
    {
        readonly TimeSpan _debounce;
        readonly Dictionary<Guid, AcceptedObservation> _accepted =
            new Dictionary<Guid, AcceptedObservation>();
        readonly Dictionary<SourceKey, DateTimeOffset> _lastAcceptedBySource =
            new Dictionary<SourceKey, DateTimeOffset>();

        public ObservationGate(TimeSpan debounce)
        {
            if (debounce < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(debounce));
            _debounce = debounce;
        }

        public bool TryAccept(RecognitionObservation observation)
        {
            if (observation.ObservationId == Guid.Empty)
                throw new ArgumentException("A constructed observation is required.", nameof(observation));

            if (_accepted.TryGetValue(observation.ObservationId, out var current))
            {
                if (observation.Revision <= current.Revision ||
                    observation.ObservedAt < current.ObservedAt)
                    return false;
                if (observation.SourceKind != current.SourceKind ||
                    !string.Equals(observation.SourceValue, current.SourceValue, StringComparison.Ordinal))
                    return false;
            }

            var source = new SourceKey(observation.SourceKind, observation.SourceValue);
            if (observation.TrackingState != TrackingState.Lost &&
                !_accepted.ContainsKey(observation.ObservationId) &&
                _lastAcceptedBySource.TryGetValue(source, out var lastAccepted) &&
                observation.ObservedAt >= lastAccepted &&
                observation.ObservedAt - lastAccepted < _debounce)
                return false;

            _accepted[observation.ObservationId] = new AcceptedObservation(
                observation.Revision,
                observation.ObservedAt,
                observation.SourceKind,
                observation.SourceValue);
            if (observation.TrackingState == TrackingState.Lost)
                _lastAcceptedBySource.Remove(source);
            else
                _lastAcceptedBySource[source] = observation.ObservedAt;
            return true;
        }

        public void BeginFreshRound(RecognitionObservation observation)
        {
            if (observation.ObservationId == Guid.Empty)
                throw new ArgumentException("A constructed observation is required.", nameof(observation));

            _lastAcceptedBySource.Remove(new SourceKey(
                observation.SourceKind,
                observation.SourceValue));
        }

        readonly struct SourceKey : IEquatable<SourceKey>
        {
            readonly SourceKind _kind;
            readonly string _value;
            public SourceKey(SourceKind kind, string value) { _kind = kind; _value = value; }
            public bool Equals(SourceKey other) => _kind == other._kind && string.Equals(_value, other._value, StringComparison.Ordinal);
            public override bool Equals(object obj) => obj is SourceKey other && Equals(other);
            public override int GetHashCode() => (_kind.GetHashCode() * 397) ^ StringComparer.Ordinal.GetHashCode(_value);
        }

        readonly struct AcceptedObservation
        {
            public AcceptedObservation(
                long revision,
                DateTimeOffset observedAt,
                SourceKind sourceKind,
                string sourceValue)
            {
                Revision = revision;
                ObservedAt = observedAt;
                SourceKind = sourceKind;
                SourceValue = sourceValue;
            }

            public long Revision { get; }
            public DateTimeOffset ObservedAt { get; }
            public SourceKind SourceKind { get; }
            public string SourceValue { get; }
        }
    }
}
