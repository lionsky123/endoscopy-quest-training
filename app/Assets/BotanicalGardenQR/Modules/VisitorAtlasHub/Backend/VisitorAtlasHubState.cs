using System;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.VisitorAtlasHub.Backend
{
    internal sealed class VisitorAtlasHubLifecycle
    {
        bool _poseReady;
        bool _mapReady;
        bool _pendingReveal;

        public VisitorAtlasHubPhase Phase { get; private set; } = VisitorAtlasHubPhase.Uninitialized;
        public bool InteractionGateOpen { get; private set; }
        public bool IsVisible => Phase == VisitorAtlasHubPhase.Revealing || Phase == VisitorAtlasHubPhase.Visible;

        public void BeginInitialization()
        {
            if (Phase != VisitorAtlasHubPhase.Uninitialized)
                throw new InvalidOperationException("Visitor Atlas Hub initialization already began.");
            Phase = VisitorAtlasHubPhase.InitializingHidden;
        }

        public void SetInteractionGate(bool open)
        {
            InteractionGateOpen = open;
            if (!open) _pendingReveal = false;
        }

        public bool RequestSummon()
        {
            if (!InteractionGateOpen) return false;
            if (Phase == VisitorAtlasHubPhase.InitializingHidden)
            {
                if (_pendingReveal) return false;
                _pendingReveal = true;
                return true;
            }
            if (Phase != VisitorAtlasHubPhase.ReadyHidden) return false;
            Phase = VisitorAtlasHubPhase.Revealing;
            return true;
        }

        public void MarkPoseReady()
        {
            if (_poseReady || Phase != VisitorAtlasHubPhase.InitializingHidden) return;
            _poseReady = true;
            CompleteReadiness();
        }

        public void MarkMapReady()
        {
            if (_mapReady || Phase != VisitorAtlasHubPhase.InitializingHidden) return;
            _mapReady = true;
            CompleteReadiness();
        }

        public bool CompleteReveal()
        {
            if (Phase != VisitorAtlasHubPhase.Revealing) return false;
            Phase = VisitorAtlasHubPhase.Visible;
            return true;
        }

        public bool Hide()
        {
            if (!IsVisible) return false;
            _pendingReveal = false;
            Phase = VisitorAtlasHubPhase.ReadyHidden;
            return true;
        }

        public void Fail()
        {
            if (Phase == VisitorAtlasHubPhase.Disposed) return;
            _pendingReveal = false;
            Phase = VisitorAtlasHubPhase.Failed;
        }

        public void Dispose()
        {
            _pendingReveal = false;
            InteractionGateOpen = false;
            Phase = VisitorAtlasHubPhase.Disposed;
        }

        void CompleteReadiness()
        {
            if (!_poseReady || !_mapReady || Phase != VisitorAtlasHubPhase.InitializingHidden) return;
            Phase = VisitorAtlasHubPhase.ReadyHidden;
            if (_pendingReveal && InteractionGateOpen)
            {
                _pendingReveal = false;
                Phase = VisitorAtlasHubPhase.Revealing;
            }
            else
            {
                _pendingReveal = false;
            }
        }
    }

    internal sealed class VisitorAtlasHubBookTransaction
    {
        readonly float _confirmationTimeoutSeconds;
        float _confirmationElapsed;
        int _generation;

        public VisitorAtlasHubBookTransaction(float confirmationTimeoutSeconds)
        {
            if (!(confirmationTimeoutSeconds > 0f) || !IsFinite(confirmationTimeoutSeconds))
                throw new ArgumentOutOfRangeException(nameof(confirmationTimeoutSeconds));
            _confirmationTimeoutSeconds = confirmationTimeoutSeconds;
        }

        public VisitorAtlasHubBookState State { get; private set; } =
            VisitorAtlasHubBookState.ClosedInteractive;
        public int Generation => _generation;

        public bool TryBegin(out int generation)
        {
            generation = _generation;
            if (State != VisitorAtlasHubBookState.ClosedInteractive) return false;
            generation = NextGeneration();
            State = VisitorAtlasHubBookState.Opening;
            _confirmationElapsed = 0f;
            return true;
        }

        public bool CompleteAnimation(int generation)
        {
            if (generation != _generation || State != VisitorAtlasHubBookState.Opening) return false;
            State = VisitorAtlasHubBookState.AwaitingBrowseConfirmation;
            _confirmationElapsed = 0f;
            return true;
        }

        public bool ConfirmBrowse(int generation)
        {
            if (generation != _generation || State != VisitorAtlasHubBookState.AwaitingBrowseConfirmation)
                return false;
            State = VisitorAtlasHubBookState.OpenLocked;
            _confirmationElapsed = 0f;
            return true;
        }

        public bool Cancel(int generation = 0)
        {
            if (State == VisitorAtlasHubBookState.ClosedInteractive) return false;
            if (generation != 0 && generation != _generation) return false;
            NextGeneration();
            State = VisitorAtlasHubBookState.ClosedInteractive;
            _confirmationElapsed = 0f;
            return true;
        }

        public bool Reset() => Cancel();

        public bool Advance(float unscaledDeltaSeconds)
        {
            if (State != VisitorAtlasHubBookState.AwaitingBrowseConfirmation) return false;
            _confirmationElapsed += Mathf.Max(0f, unscaledDeltaSeconds);
            if (_confirmationElapsed < _confirmationTimeoutSeconds) return false;
            Cancel();
            return true;
        }

        int NextGeneration()
        {
            unchecked
            {
                _generation++;
                if (_generation == 0) _generation = 1;
            }
            return _generation;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    internal sealed class PalmSummonLatch
    {
        readonly float _holdSeconds;
        readonly float _trackingGraceSeconds;
        readonly float _releaseSeconds;
        float _holdElapsed;
        float _trackingLostElapsed;
        float _releaseElapsed;
        bool _requiresRelease;

        public PalmSummonLatch(float holdSeconds, float trackingGraceSeconds, float releaseSeconds)
        {
            _holdSeconds = Positive(holdSeconds, nameof(holdSeconds));
            _trackingGraceSeconds = NonNegative(trackingGraceSeconds, nameof(trackingGraceSeconds));
            _releaseSeconds = Positive(releaseSeconds, nameof(releaseSeconds));
        }

        public bool Advance(VisitorAtlasHubPalmSample sample, float unscaledDeltaSeconds)
        {
            var delta = Mathf.Max(0f, unscaledDeltaSeconds);
            if (_requiresRelease)
            {
                if (!sample.HasReliableTracking)
                {
                    _releaseElapsed = 0f;
                    return false;
                }
                if (sample.IsCandidate)
                {
                    _releaseElapsed = 0f;
                    return false;
                }
                _releaseElapsed += delta;
                if (_releaseElapsed >= _releaseSeconds)
                {
                    _requiresRelease = false;
                    _releaseElapsed = 0f;
                }
                return false;
            }

            if (sample.IsCandidate)
            {
                _trackingLostElapsed = 0f;
                _holdElapsed += delta;
                if (_holdElapsed < _holdSeconds) return false;
                _holdElapsed = 0f;
                _requiresRelease = true;
                return true;
            }

            if (!sample.HasReliableTracking)
            {
                _trackingLostElapsed += delta;
                if (_trackingLostElapsed <= _trackingGraceSeconds) return false;
            }

            _holdElapsed = 0f;
            _trackingLostElapsed = 0f;
            return false;
        }

        public void Prime(bool candidateActive)
        {
            Reset();
            _requiresRelease = candidateActive;
        }

        public void Reset()
        {
            _holdElapsed = 0f;
            _trackingLostElapsed = 0f;
            _releaseElapsed = 0f;
            _requiresRelease = false;
        }

        static float Positive(float value, string name)
        {
            if (!(value > 0f) || !IsFinite(value)) throw new ArgumentOutOfRangeException(name);
            return value;
        }

        static float NonNegative(float value, string name)
        {
            if (value < 0f || !IsFinite(value)) throw new ArgumentOutOfRangeException(name);
            return value;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
