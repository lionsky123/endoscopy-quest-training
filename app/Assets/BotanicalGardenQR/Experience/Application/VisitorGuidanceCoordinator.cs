using System;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;

namespace BotanicalGardenQR.Experience.Application
{
    public enum VisitorGuidancePhase
    {
        WaitingForPreparation, PreparingFirstLeg, Guiding, AwaitingDiscovery, AwaitingNextLeg, Ended, Unavailable
    }

    /// <summary>Sequences geometric legs; completed content never selects a plant or a route.</summary>
    public sealed class VisitorGuidanceCoordinator : IDisposable
    {
        readonly IMapNavigation _navigation;
        readonly Action _startRecognition;
        readonly Func<string, bool> _openDiscovery;
        readonly float _arrivalRadius, _arrivalExitRadius, _arrivalStableSeconds;
        float _arrivalStable;
        bool _arrivalInside, _opening, _openFailed;
        bool _firstReleased, _begun, _disposed, _targetConfirmed, _mayDepart, _mapReady;
        long _completionAtOpen, _latestCompletion;
        string _target;
        public VisitorGuidanceCoordinator(IMapNavigation navigation, Action startRecognition,
            Func<string, bool> openDiscovery = null, float arrivalRadius = 1.2f, float arrivalExitRadius = 1.5f,
            float arrivalStableSeconds = .35f)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _startRecognition = startRecognition ?? throw new ArgumentNullException(nameof(startRecognition));
            if (!(arrivalRadius > 0) || !(arrivalExitRadius >= arrivalRadius) || !(arrivalStableSeconds > 0) ||
                float.IsInfinity(arrivalExitRadius) || float.IsInfinity(arrivalStableSeconds))
                throw new ArgumentOutOfRangeException(nameof(arrivalRadius));
            _arrivalRadius = arrivalRadius; _arrivalExitRadius = arrivalExitRadius;
            _arrivalStableSeconds = arrivalStableSeconds; _openDiscovery = openDiscovery;
            _navigation.Changed += OnNavigationChanged;
        }

        public VisitorGuidancePhase Phase { get; private set; }
        public bool BlocksFirstScan => !_firstReleased;
        public string TargetTitle => _target ?? string.Empty;
        public bool IsPaused { get; private set; }
        public bool SuppressMap => IsPaused || Surface.Kind != VisitorGuidanceSurfaceKind.Hidden;
        public bool ShowTarget => !IsPaused && !_targetConfirmed && (Phase == VisitorGuidancePhase.Guiding || Phase == VisitorGuidancePhase.AwaitingDiscovery);
        public bool ShowRoute => Phase == VisitorGuidancePhase.Guiding && !IsPaused;
        public VisitorGuidanceSurface Surface => new VisitorGuidanceSurface(
            IsPaused ? VisitorGuidanceSurfaceKind.Hidden :
            Phase == VisitorGuidancePhase.AwaitingDiscovery && _openDiscovery != null && !_targetConfirmed ?
                (_openFailed ? VisitorGuidanceSurfaceKind.DiscoveryFailed : VisitorGuidanceSurfaceKind.Discovery) :
            Phase == VisitorGuidancePhase.Unavailable ? VisitorGuidanceSurfaceKind.Unavailable :
            Phase == VisitorGuidancePhase.AwaitingNextLeg && _mayDepart ? VisitorGuidanceSurfaceKind.Departure : VisitorGuidanceSurfaceKind.Hidden,
            Phase == VisitorGuidancePhase.AwaitingNextLeg ? _navigation.NextPointId : TargetTitle);
        public event Action Changed;

        public void Begin()
        {
            if (_disposed || _begun) return;
            _begun = true;
            _target = _navigation.FirstPointId;
            SetPhase(VisitorGuidancePhase.PreparingFirstLeg);
        }

        public void Refresh(long completionRevision, VisitorGuidanceEnvironment environment, bool mapReady, bool mapFailed)
        {
            IsPaused = environment.ContentOpen || environment.CollectionVisible || environment.OtherDialogue ||
                       environment.CompletionVisible || environment.CloseDecisionVisible || environment.RewardPending;
            _mayDepart = !IsPaused;
            _mapReady = mapReady;
            _latestCompletion = completionRevision;
            if (_disposed || !_begun || Phase == VisitorGuidancePhase.Ended) return;
            if (Phase == VisitorGuidancePhase.PreparingFirstLeg)
            {
                if (mapFailed || _navigation.State.Phase == MapNavigationPhase.Unavailable)
                    SetPhase(VisitorGuidancePhase.Unavailable);
                else if (mapReady && _navigation.HasFrame && _mayDepart)
                    StartLeg(_target);
            }
            if (Phase == VisitorGuidancePhase.AwaitingDiscovery && _targetConfirmed && completionRevision > _completionAtOpen)
            {
                if (string.IsNullOrEmpty(_navigation.NextPointId)) SetPhase(VisitorGuidancePhase.Ended);
                else SetPhase(VisitorGuidancePhase.AwaitingNextLeg);
            }
        }

        public bool HasDiscovery => Phase == VisitorGuidancePhase.AwaitingDiscovery && _openDiscovery != null;

        public void RestoreDiscovery()
        {
            if (!HasDiscovery || IsPaused) return;
            _targetConfirmed = false;
            Changed?.Invoke();
        }

        public void TrackVisitorArrival(MapPosition viewer, bool tracked, float deltaSeconds)
        {
            if (_disposed || Phase != VisitorGuidancePhase.Guiding) return;
            if (!tracked || !viewer.IsFinite || IsPaused || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0)
            { _arrivalStable = 0; _arrivalInside = false; return; }
            var path = _navigation.CurrentWorldPath;
            if (path.Count == 0) return;
            var target = path[path.Count - 1]; viewer.y = target.y;
            var gap = MapPosition.Distance(viewer, target);
            if (gap <= _arrivalRadius) _arrivalInside = true;
            else if (gap > _arrivalExitRadius) { _arrivalInside = false; _arrivalStable = 0; }
            if (!_arrivalInside) return;
            _arrivalStable += Math.Min(deltaSeconds, .1f);
            if (_arrivalStable < _arrivalStableSeconds) return;
            SetPhase(VisitorGuidancePhase.AwaitingDiscovery);
            ReleaseFirstScan();
        }

        public void HandleIntent(VisitorDialogueIntentKind intent)
        {
            if (_disposed) return;
            if (HasDiscovery && !_targetConfirmed)
            {
                if (intent == VisitorDialogueIntentKind.Advance && !IsPaused && !_opening)
                {
                    _opening = true;
                    try { _openFailed = !_openDiscovery(_target); }
                    finally { _opening = false; }
                }
                Changed?.Invoke();
                return;
            }
            if (intent == VisitorDialogueIntentKind.Advance && Phase == VisitorGuidancePhase.AwaitingNextLeg && _mayDepart && _mapReady)
                StartLeg(_navigation.NextPointId);
            else if (intent == VisitorDialogueIntentKind.Dismiss && Phase == VisitorGuidancePhase.Unavailable)
                End();
        }

        public void TargetContentOpened()
        {
            if (_disposed || Phase != VisitorGuidancePhase.AwaitingDiscovery || _targetConfirmed) return;
            _targetConfirmed = true;
            _completionAtOpen = _latestCompletion;
            // Keep the Fairy's terminal hold through reading, reward and the next departure.
            Changed?.Invoke();
        }

        void StartLeg(string point)
        {
            _target = point;
            _targetConfirmed = false;
            _arrivalStable = 0; _arrivalInside = false; _openFailed = false;
            SetPhase(VisitorGuidancePhase.Guiding);
            if (!_navigation.Begin(point)) SetPhase(VisitorGuidancePhase.Unavailable);
        }

        public void End()
        {
            if (_disposed) return;
            _navigation.Cancel();
            SetPhase(VisitorGuidancePhase.Ended);
            ReleaseFirstScan();
        }

        void OnNavigationChanged()
        {
            if (_disposed || Phase != VisitorGuidancePhase.Guiding) return;
            if (_navigation.State.Phase == MapNavigationPhase.Unavailable)
                SetPhase(VisitorGuidancePhase.Unavailable);
        }

        void ReleaseFirstScan()
        {
            if (_firstReleased) return;
            _firstReleased = true;
            Changed?.Invoke();
            _startRecognition();
        }

        void SetPhase(VisitorGuidancePhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            Changed?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _navigation.Changed -= OnNavigationChanged;
            _navigation.Cancel();
            Changed = null;
        }
    }
}
