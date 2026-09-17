using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.Fairy.Backend;
using BotanicalGardenQR.Fairy.Contracts;
using BotanicalGardenQR.Panorama.Frontend;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;
using BotanicalGardenQR.VisitorCoach.Contracts;
using BotanicalGardenQR.VisitorCoach.Frontend;
using BotanicalGardenQR.VisitorPrologue.Frontend;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed class VisitorCoachRuntimeBinding : IVisitorCoachStateSink, IDisposable
    {
        readonly IVisitorCoach _coach;
        readonly VisitorCoachSessionId _session;
        readonly VisitorCoachPresenter _presenter;
        readonly VisitorCoachThemeAsset _theme;
        readonly Func<bool> _cuesSuppressed;
        readonly IDisposable _subscription;
        VisitorDialogueContextId _activeContext;
        bool _disposed;

        public VisitorCoachRuntimeBinding(
            IVisitorCoach coach,
            VisitorCoachSessionId session,
            VisitorCoachPresenter presenter,
            VisitorCoachThemeAsset theme,
            Func<bool> cuesSuppressed)
        {
            _coach = coach ?? throw new ArgumentNullException(nameof(coach));
            if (!session.IsValid) throw new ArgumentException("A valid Coach session is required.", nameof(session));
            _session = session;
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            _cuesSuppressed = cuesSuppressed ?? throw new ArgumentNullException(nameof(cuesSuppressed));
            _presenter.IntentRequested += HandleDialogueIntent;
            _subscription = _coach.Observe(this);
        }

        public void OnVisitorCoachStateChanged(VisitorCoachViewState state)
        {
            if (_disposed || state == null || state.Session != _session) return;
            if (!state.IsCueVisible || !state.OpportunityId.IsValid || state.DialoguePageCount <= 0)
            {
                HideActiveContext();
                return;
            }

            var context = CoachContext(state.OpportunityId);
            if (_activeContext.IsValid && _activeContext != context)
                _presenter.Hide(_activeContext);
            _activeContext = context;

            var mode = state.IsDialogueOpen
                ? VisitorDialogueSurfaceMode.Dialogue
                : VisitorDialogueSurfaceMode.Replay;
            var body = string.Empty;
            if (mode == VisitorDialogueSurfaceMode.Dialogue &&
                !_theme.TryResolveDialoguePage(state.CueKey, state.DialoguePageIndex, out body))
                throw new InvalidOperationException(
                    $"Visitor Coach cue '{state.CueKey}' has no authored dialogue page {state.DialoguePageIndex + 1}.");
            _theme.TryResolveGlobalCopy(state.CueKey, state.HintLevel, out var actionHint);
            _presenter.Present(new VisitorDialogueSurfaceState(
                state.Version,
                context,
                VisitorDialogueOwner.Coach,
                mode,
                ChapterFor(state.Capability),
                _theme.FairySpeakerName,
                body,
                state.DialoguePageIndex,
                state.DialoguePageCount,
                actionHint));
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (_disposed) return;
            _coach.Advance(_session, unscaledDeltaSeconds, _cuesSuppressed());
            _presenter.Tick(unscaledDeltaSeconds);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription.Dispose();
            _presenter.IntentRequested -= HandleDialogueIntent;
            HideActiveContext();
            _presenter.Dispose();
        }

        void HandleDialogueIntent(VisitorDialogueIntent intent)
        {
            if (_disposed || intent.Owner != VisitorDialogueOwner.Coach ||
                intent.Context != _activeContext)
                return;
            var state = _coach.CurrentState;
            if (state == null || state.Session != _session ||
                CoachContext(state.OpportunityId) != intent.Context)
                return;
            if (intent.Kind == VisitorDialogueIntentKind.Advance)
                _coach.AdvanceDialogue(_session, state.OpportunityId);
            else
                _coach.ReplayDialogue(_session, state.OpportunityId);
        }

        void HideActiveContext()
        {
            if (!_activeContext.IsValid) return;
            _presenter.Hide(_activeContext);
            _activeContext = default;
        }

        VisitorDialogueContextId CoachContext(VisitorCoachOpportunityId opportunity)
            => opportunity.IsValid
                ? new VisitorDialogueContextId($"coach:{_session.Value}:{opportunity.Value}")
                : default;

        static string ChapterFor(VisitorCoachCapability capability)
        {
            switch (capability)
            {
                case VisitorCoachCapability.QrConfirm: return "第一站 · 开启学习";
                case VisitorCoachCapability.ArtifactGrab:
                case VisitorCoachCapability.ArtifactPlace: return "发现收藏";
                case VisitorCoachCapability.PalmRecall: return "图谱魔法";
                case VisitorCoachCapability.PanoramaExit: return "环景观察";
                default: return "探索教学";
            }
        }
    }

    internal sealed class VisitorCoachCollectionBinding :
        IVisitorCoachStateSink,
        IDisposable
    {
        const int ArtifactPriority = 60;

        readonly IVisitorCoach _coach;
        readonly VisitorCoachSessionId _session;
        readonly VisitorCoachThemeAsset _theme;
        readonly CollectionWorldFrontend _collection;
        readonly IDisposable _subscription;

        VisitorCoachOpportunityId _artifactOpportunity;
        bool _disposed;


        public VisitorCoachCollectionBinding(
            IVisitorCoach coach,
            VisitorCoachSessionId session,
            VisitorCoachThemeAsset theme,
            CollectionWorldFrontend collection)
        {
            _coach = coach ?? throw new ArgumentNullException(nameof(coach));
            if (!session.IsValid) throw new ArgumentException("A valid Coach session is required.", nameof(session));
            _session = session;
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            _collection = collection != null ? collection : throw new ArgumentNullException(nameof(collection));
            _collection.ArtifactTutorialStageChanged += HandleArtifactStage;
            _subscription = _coach.Observe(this);
        }

        public void OnVisitorCoachStateChanged(VisitorCoachViewState state)
        {
            if (_disposed || state == null || state.Session != _session) return;
            if (!state.IsCueVisible || state.Capability != VisitorCoachCapability.ArtifactGrab &&
                state.Capability != VisitorCoachCapability.ArtifactPlace)
                return;
            if (_theme.TryResolveCopy(state.CueKey, state.HintLevel, out var copy))
                _collection.SetArtifactTutorialHint(copy);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription.Dispose();
            _collection.ArtifactTutorialStageChanged -= HandleArtifactStage;
        }

        void HandleArtifactStage(CollectionArtifactTutorialStage stage)
        {
            if (_disposed) return;
            switch (stage)
            {
                case CollectionArtifactTutorialStage.Available:
                    ReplaceArtifactOpportunity(
                        "collection.artifact.grab",
                        VisitorCoachCapability.ArtifactGrab,
                        VisitorCoachCueKeys.ArtifactGrab);
                    break;
                case CollectionArtifactTutorialStage.Held:
                    _coach.ReportProven(_session, VisitorCoachCapability.ArtifactGrab);
                    ReplaceArtifactOpportunity(
                        "collection.artifact.place",
                        VisitorCoachCapability.ArtifactPlace,
                        VisitorCoachCueKeys.ArtifactPlace);
                    break;
                case CollectionArtifactTutorialStage.PlacementMissed:
                    ReplaceArtifactOpportunity(
                        "collection.artifact.place-retry",
                        VisitorCoachCapability.ArtifactPlace,
                        VisitorCoachCueKeys.ArtifactPlaceRetry);
                    break;
                case CollectionArtifactTutorialStage.Placed:
                    _coach.ReportProven(_session, VisitorCoachCapability.ArtifactPlace);
                    Withdraw(ref _artifactOpportunity);
                    _collection.SetArtifactTutorialHint(null);
                    break;
                case CollectionArtifactTutorialStage.Hidden:
                case CollectionArtifactTutorialStage.Deferred:
                    Withdraw(ref _artifactOpportunity);
                    _collection.SetArtifactTutorialHint(null);
                    break;
            }
        }

        void ReplaceArtifactOpportunity(
            string id,
            VisitorCoachCapability capability,
            string cueKey)
        {
            Withdraw(ref _artifactOpportunity);
            if (_theme.TryResolveCopy(cueKey, VisitorCoachHintLevel.Initial, out var initialCopy))
                _collection.SetArtifactTutorialHint(initialCopy);
            _artifactOpportunity = new VisitorCoachOpportunityId(id);
            _coach.OfferOpportunity(new VisitorCoachOpportunity(
                _session,
                _artifactOpportunity,
                capability,
                cueKey,
                ArtifactPriority,
                _theme.GetDialoguePageCount(cueKey)));
        }

        void Withdraw(ref VisitorCoachOpportunityId opportunity)
        {
            if (!opportunity.IsValid) return;
            _coach.WithdrawOpportunity(_session, opportunity);
            opportunity = default;
        }
    }

    // Capability proof comes only from the real tool visibility event; no automatic teaching trigger lives here.
    internal sealed class VisitorCoachAtlasHubBinding : IDisposable
    {
        readonly IVisitorCoach _coach;
        readonly VisitorCoachSessionId _session;
        readonly IVisitorAtlasHubController _hub;
        bool _disposed;
        internal VisitorAtlasHubPalmStage LastObservedPalmStage { get; private set; }

        public VisitorCoachAtlasHubBinding(IVisitorCoach coach, VisitorCoachSessionId session,
            IVisitorAtlasHubController hub)
        {
            _coach = coach ?? throw new ArgumentNullException(nameof(coach));
            if (!session.IsValid) throw new ArgumentException("A valid Coach session is required.", nameof(session));
            _session = session;
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _hub.PalmStageChanged += OnPalmStage;
            _hub.Summoned += OnSummoned;
        }
        void OnPalmStage(VisitorAtlasHubPalmStage stage) { if (!_disposed) LastObservedPalmStage = stage; }
        void OnSummoned() { if (!_disposed) _coach.ReportProven(_session, VisitorCoachCapability.PalmRecall); }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _hub.PalmStageChanged -= OnPalmStage;
            _hub.Summoned -= OnSummoned;
        }
    }

    internal sealed class VisitorCoachPanoramaBinding : IVisitorCoachStateSink, IDisposable
    {
        const int PanoramaPriority = 80;
        static readonly VisitorCoachOpportunityId Opportunity =
            new VisitorCoachOpportunityId("panorama.exit");

        readonly IVisitorCoach _coach;
        readonly VisitorCoachSessionId _session;
        readonly VisitorCoachThemeAsset _theme;
        readonly PanoramaFrontend _panorama;
        readonly IDisposable _subscription;
        string _copy = string.Empty;
        bool _surfaceVisible;
        bool _disposed;

        public VisitorCoachPanoramaBinding(
            IVisitorCoach coach,
            VisitorCoachSessionId session,
            VisitorCoachThemeAsset theme,
            PanoramaFrontend panorama)
        {
            _coach = coach ?? throw new ArgumentNullException(nameof(coach));
            if (!session.IsValid) throw new ArgumentException("A valid Coach session is required.", nameof(session));
            _session = session;
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            _panorama = panorama != null ? panorama : throw new ArgumentNullException(nameof(panorama));
            _panorama.SurfaceVisibilityChanged += HandleSurfaceVisibility;
            _panorama.ExitSelected += HandleExitSelected;
            if (!_theme.TryResolveCopy(
                    VisitorCoachCueKeys.PanoramaEntry,
                    VisitorCoachHintLevel.Initial,
                    out _copy))
                throw new InvalidOperationException("Visitor Coach theme has no Panorama entry cue.");
            _subscription = _coach.Observe(this);
        }

        public void OnVisitorCoachStateChanged(VisitorCoachViewState state)
        {
            if (_disposed || !_surfaceVisible || state == null || state.Session != _session ||
                !state.IsCueVisible || state.Capability != VisitorCoachCapability.PanoramaExit)
                return;
            if (_theme.TryResolveCopy(state.CueKey, state.HintLevel, out var copy))
            {
                _copy = copy;
                _panorama.SetTutorialHint(_copy, true);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription.Dispose();
            _panorama.SurfaceVisibilityChanged -= HandleSurfaceVisibility;
            _panorama.ExitSelected -= HandleExitSelected;
            _panorama.SetTutorialHint(_copy, false);
        }

        void HandleSurfaceVisibility(bool visible)
        {
            if (_disposed) return;
            _surfaceVisible = visible;
            if (!visible)
            {
                _panorama.SetTutorialHint(_copy, false);
                _coach.WithdrawOpportunity(_session, Opportunity);
                return;
            }

            var proven = _coach.CurrentState.GetMastery(VisitorCoachCapability.PanoramaExit) ==
                         VisitorCoachMastery.Proven;
            _panorama.SetTutorialHint(_copy, !proven);
            if (proven) return;
            _coach.OfferOpportunity(new VisitorCoachOpportunity(
                _session,
                Opportunity,
                VisitorCoachCapability.PanoramaExit,
                VisitorCoachCueKeys.PanoramaEntry,
                PanoramaPriority,
                _theme.GetDialoguePageCount(VisitorCoachCueKeys.PanoramaEntry)));
        }

        void HandleExitSelected()
        {
            if (_disposed) return;
            _panorama.SetTutorialHint(_copy, false);
            _coach.ReportProven(_session, VisitorCoachCapability.PanoramaExit);
        }
    }

    internal sealed class VisitorCoachPrologueBinding : IDisposable
    {
        readonly IVisitorCoach _coach;
        readonly VisitorCoachSessionId _session;
        readonly VisitorProloguePresenter _presenter;
        bool _disposed;

        public VisitorCoachPrologueBinding(
            IVisitorCoach coach,
            VisitorCoachSessionId session,
            VisitorProloguePresenter presenter)
        {
            _coach = coach ?? throw new ArgumentNullException(nameof(coach));
            if (!session.IsValid) throw new ArgumentException("A valid Coach session is required.", nameof(session));
            _session = session;
            _presenter = presenter != null ? presenter : throw new ArgumentNullException(nameof(presenter));
            _presenter.GazeDwellProven += HandleGazeDwellProven;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _presenter.GazeDwellProven -= HandleGazeDwellProven;
        }

        void HandleGazeDwellProven()
        {
            if (!_disposed) _coach.ReportProven(_session, VisitorCoachCapability.GazeDwell);
        }

    }

    internal sealed class VisitorCoachQrBinding : IContentLifecycleSink, IDisposable
    {
        const int FirstScanPriority = 100;
        static readonly VisitorCoachOpportunityId FirstScanOpportunity =
            new VisitorCoachOpportunityId("entry.qr.first-scan");

        readonly IVisitorCoach _coach;
        readonly VisitorCoachSessionId _session;
        readonly JourneySessionId _journeySession;
        readonly VisitorCoachThemeAsset _theme;
        readonly IDisposable _subscription;
        bool _firstScanOffered;
        bool _disposed;

        public VisitorCoachQrBinding(
            IVisitorCoach coach,
            VisitorCoachSessionId session,
            JourneySessionId journeySession,
            IContentLifecycleSource contentLifecycle)
            : this(coach, session, journeySession, contentLifecycle, null)
        {
        }

        public VisitorCoachQrBinding(
            IVisitorCoach coach,
            VisitorCoachSessionId session,
            JourneySessionId journeySession,
            IContentLifecycleSource contentLifecycle,
            VisitorCoachThemeAsset theme)
        {
            _coach = coach ?? throw new ArgumentNullException(nameof(coach));
            if (!session.IsValid) throw new ArgumentException("A valid Coach session is required.", nameof(session));
            if (!journeySession.IsValid)
                throw new ArgumentException("A valid Journey session is required.", nameof(journeySession));
            _session = session;
            _journeySession = journeySession;
            _theme = theme;
            _subscription = (contentLifecycle ?? throw new ArgumentNullException(nameof(contentLifecycle)))
                .Observe(this);
        }

        public VisitorCoachResult BeginFirstScanOpportunity()
        {
            if (_disposed)
                return VisitorCoachResult.Failure(VisitorCoachFailureCode.InvalidOpportunity);
            if (_firstScanOffered) return VisitorCoachResult.Success;

            var result = _coach.OfferOpportunity(new VisitorCoachOpportunity(
                _session,
                FirstScanOpportunity,
                VisitorCoachCapability.QrConfirm,
                VisitorCoachCueKeys.QrConfirm,
                FirstScanPriority,
                _theme != null ? _theme.GetDialoguePageCount(VisitorCoachCueKeys.QrConfirm) : 0));
            if (result.Succeeded) _firstScanOffered = true;
            return result;
        }

        public void OnContentOpened(ContentOpenedFact fact)
        {
            if (_disposed || fact == null || fact.JourneySession != _journeySession || fact.IsRecall ||
                fact.EntryKind != RecognitionSourceKinds.Qr)
                return;
            _coach.ReportProven(_session, VisitorCoachCapability.QrConfirm);
        }

        public void OnContentClosed(ContentClosedFact fact) { }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription.Dispose();
            if (_firstScanOffered)
                _coach.WithdrawOpportunity(_session, FirstScanOpportunity);
        }
    }

    /// <summary>
    /// Application-only mapping from a resolved first-scan teaching state to one
    /// short Fairy reaction. Neither module learns about the other's internals.
    /// </summary>
    internal sealed class VisitorCoachFairyBinding : IVisitorCoachStateSink, IDisposable
    {
        readonly IVisitorCoach _coach;
        readonly VisitorCoachSessionId _session;
        readonly FairyCompanionBinding _fairy;
        readonly float _attentionSeconds;
        readonly IDisposable _subscription;
        readonly HashSet<VisitorCoachOpportunityId> _reactedOpportunities = new();
        bool _disposed;

        public VisitorCoachFairyBinding(
            IVisitorCoach coach,
            VisitorCoachSessionId session,
            FairyCompanionBinding fairy,
            float attentionSeconds)
        {
            _coach = coach ?? throw new ArgumentNullException(nameof(coach));
            if (!session.IsValid) throw new ArgumentException("A valid Coach session is required.", nameof(session));
            _session = session;
            _fairy = fairy ?? throw new ArgumentNullException(nameof(fairy));
            if (!(attentionSeconds > 0f) || float.IsNaN(attentionSeconds) || float.IsInfinity(attentionSeconds))
                throw new ArgumentOutOfRangeException(nameof(attentionSeconds));
            _attentionSeconds = attentionSeconds;
            _subscription = _coach.Observe(this);
        }

        public void OnVisitorCoachStateChanged(VisitorCoachViewState state)
        {
            if (_disposed || state == null || state.Session != _session)
                return;
            if (!state.IsCueVisible || !state.OpportunityId.IsValid) return;

            if (!_reactedOpportunities.Contains(state.OpportunityId))
            {
                var shown = _fairy.Show();
                if (!shown.Succeeded)
                {
                    Debug.LogWarning(
                        $"教程继续运行，但小精灵无法显示：{shown.FailureCode} ({shown.DiagnosticTag})。");
                    return;
                }
                if (!_fairy.IsPresentationVisible) return;
                _reactedOpportunities.Add(state.OpportunityId);

                if (state.Capability == VisitorCoachCapability.QrConfirm &&
                    string.Equals(state.CueKey, VisitorCoachCueKeys.QrConfirm, StringComparison.Ordinal))
                {
                    var presented = _fairy.PresentCue(FairyCompanionCue.CoachAttention(_attentionSeconds));
                    if (!presented.Succeeded)
                        Debug.LogWarning(
                            $"首次二维码教学继续运行，但小精灵注意动作未呈现：{presented.FailureCode} ({presented.DiagnosticTag})。");
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription.Dispose();
        }
    }

    /// <summary>Forwards the Stage's resolved composition to the independently owned Fairy.</summary>
    internal sealed class VisitorDialogueFairyBinding : IDisposable
    {
        readonly VisitorCoachPresenter _presenter;
        readonly FairyCompanionBinding _fairy;
        bool _focused;
        bool _disposed;

        public VisitorDialogueFairyBinding(VisitorCoachPresenter presenter, FairyCompanionBinding fairy)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _fairy = fairy ?? throw new ArgumentNullException(nameof(fairy));
            _presenter.CompositionChanged += HandleCompositionChanged;
            HandleCompositionChanged(_presenter.CurrentComposition);
        }

        void HandleCompositionChanged(VisitorDialogueComposition composition)
        {
            if (_disposed || !ReferenceEquals(composition, _presenter.CurrentComposition)) return;
            if (composition == null || composition.Mode != VisitorDialogueSurfaceMode.Dialogue)
            {
                ReleaseFocus();
                return;
            }

            var shown = _fairy.Show();
            if (!shown.Succeeded) return;
            var result = _fairy.PresentCue(FairyCompanionCue.DialogueFocus(
                composition.FairyWorldPosition, composition.Expression switch
                {
                    VisitorDialogueExpression.Welcome => FairyDialogueReaction.Welcome,
                    VisitorDialogueExpression.Wonder => FairyDialogueReaction.Wonder,
                    _ => FairyDialogueReaction.Listening
                }));
            if (result.Succeeded) _focused = true;
        }

        void ReleaseFocus()
        {
            if (!_focused) return;
            var result = _fairy.PresentCue(FairyCompanionCue.Idle);
            if (result.Succeeded) _focused = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _presenter.CompositionChanged -= HandleCompositionChanged;
            ReleaseFocus();
        }
    }
}
