using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Activation.Runtime
{
    internal readonly struct QrFocusTarget
    {
        public QrFocusTarget(
            Guid observationId,
            SpatialEvidence evidence,
            Rect planeRect)
        {
            ObservationId = observationId;
            Evidence = evidence;
            PlaneRect = planeRect;
        }

        public Guid ObservationId { get; }
        public SpatialEvidence Evidence { get; }
        public Rect PlaneRect { get; }
    }

    internal readonly struct QrFocusSignal
    {
        public QrFocusSignal(Guid observationId, float progress, bool isConfirmation)
        {
            ObservationId = observationId;
            Progress = progress;
            IsConfirmation = isConfirmation;
        }

        public Guid ObservationId { get; }
        public float Progress { get; }
        public bool IsConfirmation { get; }
    }

    internal readonly struct QrFocusFrame
    {
        public QrFocusFrame(QrFocusSignal? cleared, QrFocusSignal? current)
        {
            Cleared = cleared;
            Current = current;
        }

        public QrFocusSignal? Cleared { get; }
        public QrFocusSignal? Current { get; }
    }

    internal sealed class QrFocusConfirmationEngine
    {
        const double MinimumClockGapResetSeconds = 0.5d;
        const double MaximumCountedTickSeconds = 0.05d;
        const float HitDistanceTieEpsilon = 0.001f;
        readonly double _confirmationSeconds;
        readonly double _gazeLostGraceSeconds;
        readonly HashSet<Guid> _consumedObservations = new HashSet<Guid>();
        readonly List<Guid> _consumedReleaseBuffer = new List<Guid>();
        Guid _focusedId;
        double _focusedSeconds;
        double _lastTickAt;
        double? _focusLostAt;
        float _lastPublishedProgress;
        bool _hasClock;
        bool _confirmed;
        bool _publishZeroNextTick;

        public QrFocusConfirmationEngine(float confirmationSeconds, float gazeLostGraceSeconds)
        {
            if (!IsFinitePositive(confirmationSeconds))
                throw new ArgumentOutOfRangeException(nameof(confirmationSeconds));
            if (!IsFiniteNonNegative(gazeLostGraceSeconds))
                throw new ArgumentOutOfRangeException(nameof(gazeLostGraceSeconds));
            _confirmationSeconds = confirmationSeconds;
            _gazeLostGraceSeconds = gazeLostGraceSeconds;
        }

        public bool Rearm(Guid observationId)
        {
            if (observationId == Guid.Empty) return false;
            var wasConsumed = _consumedObservations.Remove(observationId);
            if (observationId != _focusedId) return wasConsumed;
            _focusedSeconds = 0d;
            _focusLostAt = null;
            _lastPublishedProgress = 0f;
            _confirmed = false;
            _publishZeroNextTick = true;
            return true;
        }

        public QrFocusFrame Tick(
            double now,
            RecognitionFocusQuery? focusQuery,
            IReadOnlyList<QrFocusTarget> targets)
        {
            if (!IsFiniteNonNegative(now))
            {
                var invalidClockClear = Clear();
                _hasClock = false;
                return new QrFocusFrame(invalidClockClear, null);
            }

            var previousTickAt = _lastTickAt;
            var clockGap = _hasClock &&
                           (now < previousTickAt ||
                            now - previousTickAt >
                            Math.Max(MinimumClockGapResetSeconds, _gazeLostGraceSeconds * 2d));
            _lastTickAt = now;
            _hasClock = true;

            var hasSelection = TrySelect(focusQuery, targets, out var selectedId);
            if (clockGap)
            {
                var cleared = Clear();
                if (!hasSelection) return new QrFocusFrame(cleared, null);
                Begin(selectedId);
                return new QrFocusFrame(
                    cleared.HasValue && cleared.Value.ObservationId != selectedId ? cleared : null,
                    Current(0f, false));
            }

            if (_focusedId == Guid.Empty)
            {
                if (!hasSelection) return default;
                Begin(selectedId);
                return new QrFocusFrame(null, Current(0f, false));
            }

            if (hasSelection && selectedId != _focusedId)
            {
                var cleared = Clear();
                Begin(selectedId);
                return new QrFocusFrame(cleared, Current(0f, false));
            }

            if (!hasSelection)
            {
                if (!_focusLostAt.HasValue) _focusLostAt = now;
                if (now - _focusLostAt.Value < _gazeLostGraceSeconds) return default;
                return new QrFocusFrame(Clear(), null);
            }

            if (_focusLostAt.HasValue)
            {
                if (now - _focusLostAt.Value >= _gazeLostGraceSeconds)
                {
                    Clear();
                    Begin(selectedId);
                    return new QrFocusFrame(null, Current(0f, false));
                }
                _focusLostAt = null;
                return default;
            }

            if (_publishZeroNextTick)
            {
                _publishZeroNextTick = false;
                return new QrFocusFrame(null, Current(0f, false));
            }
            if (_confirmed || !_hasClock) return default;

            var delta = now - previousTickAt;
            if (delta > 0d) _focusedSeconds += Math.Min(delta, MaximumCountedTickSeconds);
            var progress = Mathf.Clamp01((float)(_focusedSeconds / _confirmationSeconds));
            if (_focusedSeconds >= _confirmationSeconds - 0.000001d)
            {
                _confirmed = true;
                ConsumeFocusedObservation(focusQuery.Value, targets);
                _lastPublishedProgress = 1f;
                return new QrFocusFrame(null, Current(1f, true));
            }
            if (progress - _lastPublishedProgress < 0.02f) return default;
            _lastPublishedProgress = progress;
            return new QrFocusFrame(null, Current(progress, false));
        }

        void Begin(Guid observationId)
        {
            _focusedId = observationId;
            _focusedSeconds = 0d;
            _focusLostAt = null;
            _lastPublishedProgress = 0f;
            _confirmed = false;
            _publishZeroNextTick = false;
        }

        QrFocusSignal? Clear()
        {
            if (_focusedId == Guid.Empty) return null;
            var signal = new QrFocusSignal(_focusedId, 0f, false);
            _focusedId = Guid.Empty;
            _focusedSeconds = 0d;
            _focusLostAt = null;
            _lastPublishedProgress = 0f;
            _confirmed = false;
            _publishZeroNextTick = false;
            return signal;
        }

        QrFocusSignal Current(float progress, bool isConfirmation)
            => new QrFocusSignal(_focusedId, progress, isConfirmation);

        bool TrySelect(
            RecognitionFocusQuery? query,
            IReadOnlyList<QrFocusTarget> targets,
            out Guid selectedId)
        {
            selectedId = Guid.Empty;
            if (!query.HasValue || targets == null) return false;

            var hasBest = false;
            var bestTarget = default(QrFocusTarget);
            var bestDistance = float.PositiveInfinity;
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                if (_consumedObservations.Contains(target.ObservationId) ||
                    !TryHit(query.Value, target, out var distance))
                    continue;
                var sameDistance = Mathf.Abs(distance - bestDistance) <= HitDistanceTieEpsilon;
                var preferTarget = !hasBest ||
                                   distance < bestDistance - HitDistanceTieEpsilon ||
                                   sameDistance && PreferEquivalentTarget(target, bestTarget);
                if (!preferTarget) continue;
                hasBest = true;
                bestTarget = target;
                selectedId = target.ObservationId;
                bestDistance = distance;
            }

            return selectedId != Guid.Empty;
        }

        bool PreferEquivalentTarget(QrFocusTarget candidate, QrFocusTarget currentBest)
        {
            var candidateIsCurrent = candidate.ObservationId == _focusedId;
            var bestIsCurrent = currentBest.ObservationId == _focusedId;
            if (candidateIsCurrent != bestIsCurrent)
                return candidateIsCurrent;

            return candidate.ObservationId.CompareTo(currentBest.ObservationId) < 0;
        }

        void ConsumeFocusedObservation(
            RecognitionFocusQuery query,
            IReadOnlyList<QrFocusTarget> targets)
        {
            // A previously confirmed observation remains consumed while it
            // overlaps the currently confirmed gaze ray. This prevents stale
            // MRUK trackables at the same scan position from alternating.
            // Once another QR confirms elsewhere, the old observation can
            // participate again when the visitor returns to it.
            _consumedReleaseBuffer.Clear();
            foreach (var consumedId in _consumedObservations)
            {
                var stillHit = false;
                for (var index = 0; index < targets.Count; index++)
                {
                    var target = targets[index];
                    if (target.ObservationId != consumedId ||
                        !TryHit(query, target, out _))
                        continue;
                    stillHit = true;
                    break;
                }

                if (!stillHit) _consumedReleaseBuffer.Add(consumedId);
            }

            for (var index = 0; index < _consumedReleaseBuffer.Count; index++)
                _consumedObservations.Remove(_consumedReleaseBuffer[index]);
            _consumedObservations.Add(_focusedId);
        }

        static bool TryHit(
            RecognitionFocusQuery query,
            QrFocusTarget target,
            out float distance)
        {
            distance = 0f;
            if (target.ObservationId == Guid.Empty || !target.Evidence.PoseIsValid ||
                !IsUsableRect(target.PlaneRect))
                return false;

            var inverseRotation = Quaternion.Inverse(target.Evidence.Rotation);
            var localOrigin = inverseRotation * (query.Ray.origin - target.Evidence.Position);
            var localDirection = inverseRotation * query.Ray.direction;
            if (!IsFinite(localOrigin) || !IsFinite(localDirection) || localDirection.z >= -0.000001f)
                return false;

            distance = -localOrigin.z / localDirection.z;
            if (!IsFinitePositive(distance)) return false;
            var impact = localOrigin + localDirection * distance;
            var padding = distance * Mathf.Tan(query.AngularPaddingDegrees * Mathf.Deg2Rad);
            var rect = target.PlaneRect;
            if (impact.x < rect.xMin - padding || impact.x > rect.xMax + padding ||
                impact.y < rect.yMin - padding || impact.y > rect.yMax + padding)
                return false;
            return true;
        }

        static bool IsUsableRect(Rect value)
            => IsFinite(value.xMin) && IsFinite(value.xMax) &&
               IsFinite(value.yMin) && IsFinite(value.yMax) &&
               value.width > 0.000001f && value.height > 0.000001f;

        static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        static bool IsFinitePositive(double value)
            => value > 0d && !double.IsNaN(value) && !double.IsInfinity(value);

        static bool IsFiniteNonNegative(double value)
            => value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
