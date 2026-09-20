using System;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Fairy.Backend;
using BotanicalGardenQR.Fairy.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.VisitorCoach.Frontend;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using BotanicalGardenQR.VisitorPrologue.Frontend;

namespace BotanicalGardenQR.Bootstrap
{
    /// <summary>Joins actual Fairy readiness and the fieldbook's opening to one authored encounter.</summary>
    internal sealed class VisitorPrologueStartupBinding : IVisitorPrologueStateSink, IFairyStateSink, IDisposable
    {
        readonly IVisitorPrologue _prologue;
        readonly VisitorProloguePresenter _presenter;
        readonly VisitorCoachPresenter _dialogue;
        readonly Func<FairyResult?> _showFairy;
        readonly VisitorPrologueThemeAsset _theme;
        readonly Action _startRecognition;
        readonly Action<bool> _setGazeReticlePresentation;
        readonly IDisposable _subscription, _fairySubscription;
        VisitorDialogueContextId _context;
        bool _fairyActive, _showing, _beginningDialogue, _recognitionSent, _recognitionSending, _disposed;
        long _shownEpoch = -1;
        public VisitorPrologueStartupBinding(IVisitorPrologue prologue, VisitorProloguePresenter presenter,
            VisitorCoachPresenter dialogue, FairyCompanionBinding fairy, VisitorPrologueThemeAsset theme,
            Action startRecognition, Action<bool> setGazeReticlePresentation)
            : this(prologue, presenter, dialogue, fairy == null ? null : new Func<FairyResult?>(() => fairy.Show(presenter.InvitationWorldOrigin)),
                startRecognition, setGazeReticlePresentation, theme,
                fairy == null ? null : new Func<IFairyStateSink, IDisposable>(fairy.Observe)) { }
        internal VisitorPrologueStartupBinding(IVisitorPrologue prologue, VisitorProloguePresenter presenter,
            VisitorCoachPresenter dialogue, Func<FairyResult?> showFairy, Action startRecognition,
            Action<bool> setGazeReticlePresentation, VisitorPrologueThemeAsset theme = null,
            Func<IFairyStateSink, IDisposable> observeFairy = null)
        {
            _prologue = prologue ?? throw new ArgumentNullException(nameof(prologue));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _dialogue = dialogue; _showFairy = showFairy; _theme = theme;
            _startRecognition = startRecognition ?? throw new ArgumentNullException(nameof(startRecognition));
            _setGazeReticlePresentation = setGazeReticlePresentation ?? throw new ArgumentNullException(nameof(setGazeReticlePresentation));
            _fairyActive = observeFairy == null;
            if (_dialogue != null) _dialogue.IntentRequested += OnIntent;
            _fairySubscription = observeFairy?.Invoke(this);
            _subscription = prologue.Observe(this);
        }
        public void Begin()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisitorPrologueStartupBinding));
            _prologue.Begin();
        }
        public void OnVisitorPrologueStateChanged(VisitorPrologueViewState state)
        {
            if (_disposed || state == null) return;
            _setGazeReticlePresentation(ShouldShowGazeReticle(state));
            _dialogue?.SetInputMode(ResolveDialogueInput(state));
            if (state.Phase == VisitorProloguePhase.Arrival)
            {
                ShowFairy(state.Epoch);
                if (_fairyActive && _prologue.CurrentState.Phase == VisitorProloguePhase.Arrival)
                    _prologue.ReportArrivalReady(state.Epoch);
            }
            if (_prologue.CurrentState.Phase == VisitorProloguePhase.Encounter) PresentEncounter(_prologue.CurrentState);
            else HideDialogue();
            if (!_prologue.CurrentState.IsExplorationReady || _recognitionSent || _recognitionSending) return;
            _recognitionSending = true;
            try { _startRecognition(); _recognitionSent = true; }
            finally { _recognitionSending = false; }
        }
        void ShowFairy(long epoch)
        {
            if (_showing || _shownEpoch == epoch) return;
            _showing = true;
            try
            {
                var result = _showFairy?.Invoke();
                if (result.HasValue && !result.Value.Succeeded)
                    _prologue.Fail("联系暂时中断了。准备好后，可以重新回应邀请。");
                else _shownEpoch = epoch;
            }
            catch (Exception) { _prologue.Fail("联系暂时中断了。准备好后，可以重新回应邀请。"); }
            finally { _showing = false; }
        }
        void IFairyStateSink.Publish(FairyState state)
        {
            if (_disposed) return;
            _fairyActive = state.Phase == FairyPhase.Active;
            var current = _prologue.CurrentState;
            if (current.Phase != VisitorProloguePhase.Arrival) return;
            if (_fairyActive) _prologue.ReportArrivalReady(current.Epoch);
            else if (state.Phase == FairyPhase.Failed) _prologue.Fail("联系暂时中断了。请重新回应邀请。");
        }
        void PresentEncounter(VisitorPrologueViewState state)
        {
            if (_dialogue == null || _theme == null || _beginningDialogue) return;
            if (!state.HasDialogue)
            {
                _beginningDialogue = true;
                try { _prologue.BeginDialogue(state.Epoch, _theme.Copy.EncounterPageCount); }
                finally { _beginningDialogue = false; }
                state = _prologue.CurrentState;
            }
            if (!state.HasDialogue || !_theme.Copy.TryGetEncounterPage(state.DialoguePageIndex, out var body)) return;
            _context = new VisitorDialogueContextId($"encounter:{state.Epoch}");
            var expression = state.DialoguePageIndex == 0 ? VisitorDialogueExpression.Wonder : VisitorDialogueExpression.Listening;
            if (state.DialoguePageIndex == 2)
            {
                body = state.Reply == VisitorEncounterReply.AskAboutWorld ? _theme.Copy.CuriousResponse : _theme.Copy.CompanionResponse;
                expression = state.Reply == VisitorEncounterReply.AskAboutWorld ? VisitorDialogueExpression.Wonder : VisitorDialogueExpression.Welcome;
            }
            _dialogue.Present(new VisitorDialogueSurfaceState(state.Version, _context, VisitorDialogueOwner.Prologue,
                VisitorDialogueSurfaceMode.Dialogue, "相遇 · 见闻之约", _theme.Copy.FairySpeakerName, body,
                state.DialoguePageIndex, state.DialoguePageCount,
                primaryActionLabel: state.IsReplyPage ? _theme.Copy.CuriousReplyLabel : state.DialoguePageIndex == 3 ? "约好了，一起出发" : state.DialoguePageIndex == 0 ? _theme.Copy.FirstContinueAction : "继续  ›",
                secondaryActionLabel: state.IsReplyPage ? _theme.Copy.CompanionReplyLabel : null,
                primaryIntent: state.IsReplyPage ? VisitorDialogueIntentKind.ChoosePrimary : VisitorDialogueIntentKind.Advance,
                secondaryIntent: state.IsReplyPage ? VisitorDialogueIntentKind.ChooseSecondary : VisitorDialogueIntentKind.Replay,
                expression: expression));
        }
        void OnIntent(VisitorDialogueIntent intent)
        {
            if (_disposed || intent.Owner != VisitorDialogueOwner.Prologue || intent.Context != _context) return;
            var state = _prologue.CurrentState;
            if (state.Phase != VisitorProloguePhase.Encounter || intent.Revision != state.Version) return;
            VisitorPrologueResult result;
            switch (intent.Kind)
            {
                case VisitorDialogueIntentKind.Advance: result = _prologue.AdvanceDialogue(state.Epoch, intent.Revision); break;
                case VisitorDialogueIntentKind.ChoosePrimary: result = _prologue.ChooseReply(state.Epoch, intent.Revision, VisitorEncounterReply.AskAboutWorld); break;
                case VisitorDialogueIntentKind.ChooseSecondary: result = _prologue.ChooseReply(state.Epoch, intent.Revision, VisitorEncounterReply.PromiseToExplore); break;
                case VisitorDialogueIntentKind.Replay: _prologue.ReplayDialogue(state.Epoch); return;
                default: return;
            }
            if (result.Succeeded && intent.Origin == VisitorDialogueInputOrigin.GazeDwell)
                _presenter.ReportDialogueGazeAccepted(state.Epoch);
        }
        void HideDialogue()
        {
            if (_context.IsValid) _dialogue?.Hide(_context);
            _context = default;
        }
        internal static bool ShouldShowGazeReticle(VisitorPrologueViewState state)
            => false;
        internal static VisitorDialogueInputMode ResolveDialogueInput(VisitorPrologueViewState state)
            => state.Phase == VisitorProloguePhase.Encounter || state.IsExplorationReady
                ? VisitorDialogueInputMode.HandPoke : VisitorDialogueInputMode.Unavailable;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _subscription.Dispose(); _fairySubscription?.Dispose();
            if (_dialogue != null) _dialogue.IntentRequested -= OnIntent;
            HideDialogue(); _setGazeReticlePresentation(false);
        }
    }
}
