using System;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;
using BotanicalGardenQR.VisitorCoach.Contracts;
using BotanicalGardenQR.VisitorCoach.Frontend;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed class VisitorToolPreparationBinding : IDisposable
    {
        readonly VisitorToolPreparation _preparation;
        readonly IVisitorAtlasHubController _hub;
        readonly VisitorCoachPresenter _dialogue;
        readonly VisitorCoachThemeAsset _theme;
        readonly Action _startExploration;
        readonly VisitorDialogueContextId _context;
        long _revision;
        bool _disposed, _exitRequested;
        public VisitorToolPreparationBinding(VisitorToolPreparation preparation, IVisitorAtlasHubController hub,
            VisitorCoachPresenter dialogue, VisitorCoachThemeAsset theme, VisitorCoachSessionId session, Action startExploration)
        {
            _preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            _theme = theme ?? throw new ArgumentNullException(nameof(theme));
            _startExploration = startExploration ?? throw new ArgumentNullException(nameof(startExploration));
            _context = new VisitorDialogueContextId($"tools:{session.Value}");
            _preparation.Changed += Present;
            _dialogue.IntentRequested += OnIntent;
            _hub.Summoned += OnSummoned;
            _hub.Hidden += OnHidden;
        }
        public void Begin()
        {
            if (_disposed || _preparation.Phase != VisitorToolPreparationPhase.Waiting) return;
            _hub.Hide();
            _preparation.Begin();
        }
        public void Replay() { if (!_disposed) _preparation.Replay(); }
        void OnSummoned() { if (!_disposed) _preparation.ReportSummoned(); }
        void OnHidden() { if (!_disposed) _preparation.ReportHidden(); }
        void OnIntent(VisitorDialogueIntent intent)
        {
            if (_disposed || intent.Owner != VisitorDialogueOwner.ToolPreparation || intent.Context != _context ||
                intent.Revision != _revision) return;
            if (intent.Kind == VisitorDialogueIntentKind.Advance) _preparation.AdvanceExplanation();
            else if (intent.Kind == VisitorDialogueIntentKind.Replay) _preparation.Replay();
            else if (intent.Kind == VisitorDialogueIntentKind.Defer)
            {
                _preparation.DeferPractice();
                _hub.Hide();
            }
        }
        void Present()
        {
            if (_disposed || _preparation.Phase == VisitorToolPreparationPhase.Waiting) return;
            if (_preparation.HasExited && !_preparation.IsExplanationOpen)
            {
                _dialogue.Hide(_context);
                if (!_exitRequested)
                {
                    _hub.Hide();
                    _startExploration();
                    _exitRequested = true;
                }
                return;
            }
            var introduction = _preparation.IsExplanationOpen;
            _dialogue.SetInputMode(VisitorDialogueInputMode.HandPoke);
            if (_preparation.IsPracticing)
            {
                // Replay is the existing non-modal action surface: the sole modal owner keeps the real Hub armed.
                _dialogue.Present(new VisitorDialogueSurfaceState(++_revision, _context, VisitorDialogueOwner.ToolPreparation,
                    VisitorDialogueSurfaceMode.Replay, "同行 · 试着呼唤", _theme.FairySpeakerName, string.Empty, 0, 1,
                    actionHint: _preparation.HasSummoned ? _theme.ToolPreparationSummonedCopy : _theme.ToolPreparationPracticeCopy,
                    allowDefer: true, primaryActionLabel: "再告诉我一次"));
                return;
            }
            _dialogue.Present(new VisitorDialogueSurfaceState(++_revision, _context, VisitorDialogueOwner.ToolPreparation,
                VisitorDialogueSurfaceMode.Dialogue, introduction ? "同行 · 见闻册与卷轴" : "我们的第一站",
                _theme.FairySpeakerName, introduction ? _theme.ToolPreparationIntroduction :
                    (_preparation.HasSummoned ? _theme.ToolPreparationSuccessCopy : _theme.ToolPreparationDeferredCopy) + "\n" + _theme.ToolPreparationReadyCopy, 0, 1,
                primaryActionLabel: introduction ? (_preparation.HasExited ? "记住了" : "让我试试") : _theme.ToolPreparationStartLabel,
                allowRestart: !introduction, expression: VisitorDialogueExpression.Welcome));
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true; _preparation.Changed -= Present; _dialogue.IntentRequested -= OnIntent;
            _hub.Summoned -= OnSummoned; _hub.Hidden -= OnHidden;
            _dialogue.Hide(_context);
        }
    }
}
