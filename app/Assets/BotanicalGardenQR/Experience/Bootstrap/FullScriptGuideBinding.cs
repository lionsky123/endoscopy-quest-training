using System;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.VisitorCoach.Frontend;
using UnityEngine.XR;

namespace BotanicalGardenQR.Bootstrap
{
    internal readonly struct FullScriptGuideCopy
    {
        internal FullScriptGuideCopy(string chapter, string body, string actionLabel)
        {
            Chapter = chapter ?? string.Empty;
            Body = body ?? string.Empty;
            ActionLabel = actionLabel ?? string.Empty;
        }

        internal string Chapter { get; }
        internal string Body { get; }
        internal string ActionLabel { get; }

        internal static FullScriptGuideCopy Resolve(string roomName, bool firstLobbyVisit, bool finalOfficeVisit)
        {
            if (firstLobbyVisit)
            {
                var body = "欢迎来到内镜中心监督检查。伸出食指，轻轻按一下“开始学习”；推荐先从办公室开始。";
                if (!XRSettings.isDeviceActive) body += "\n当前仅显示场景预览，连接头显后用手操作。";
                return new FullScriptGuideCopy("欢迎", body, "开始学习");
            }
            if (finalOfficeVisit)
                return new FullScriptGuideCopy(roomName, "各房间的检查记录已汇集到这里。请逐项回看，核对未完成和待补内容，再决定是否结束。", "查看汇总");
            if (roomName == "办公室")
                return new FullScriptGuideCopy(roomName, "先查看办公室的模拟资料，再到各房间现场核对需要确认的内容。", "查看办公室资料");
            return new FullScriptGuideCopy(roomName, $"我们现在在{roomName}。请先查看本室检查项目，按提示观察和操作。", "开始本室检查");
        }
    }

    /// <summary>Connects the current room's short authored entry cue to its live stationary action.</summary>
    internal sealed class FullScriptGuideBinding : IDisposable
    {
        const string Speaker = "安小卫";
        readonly VisitorCoachPresenter _presenter;
        readonly FullScriptRoomVisit _visit;
        readonly VisitorDialogueContextId _context;
        Action _continueAction;
        long _revision;
        bool _disposed;

        internal FullScriptGuideBinding(VisitorCoachPresenter presenter, FullScriptRoomVisit visit)
        {
            _presenter = presenter != null ? presenter : throw new ArgumentNullException(nameof(presenter));
            _visit = visit ?? throw new ArgumentNullException(nameof(visit));
            _context = new VisitorDialogueContextId($"full-script-guide:{visit.RoomId}:{Guid.NewGuid():N}");
            _presenter.IntentRequested += HandleIntent;
            _visit.TeachingRequested += PresentTeaching;
        }

        void PresentTeaching(FullScriptGuideCopy copy,Action resume)
        {
            if(_disposed || !_visit.InputAllowed)return;
            _continueAction=resume;
            _presenter.SetInputMode(VisitorDialogueInputMode.HandPoke);
            _presenter.Present(CreateSurfaceState(++_revision,_context,copy));
        }

        internal bool TryPresent()
        {
            if (_disposed || !_visit.InputAllowed || !_visit.ShouldShowEntryGuide || _continueAction != null)
                return false;

            var definition = _visit.RoomId;
            var copy = FullScriptGuideCopy.Resolve(
                _visit.DisplayName,
                definition == _visit.StartRoomId && _visit.MainlineIndex == 0,
                _visit.IsFinalSummaryVisit);
            _continueAction = _visit.BeginFromEntryGuide;
            _presenter.SetInputMode(VisitorDialogueInputMode.HandPoke);
            _presenter.Present(CreateSurfaceState(++_revision, _context, copy));
            return true;
        }

        internal static VisitorDialogueSurfaceState CreateSurfaceState(
            long revision,
            VisitorDialogueContextId context,
            FullScriptGuideCopy copy)
            => new VisitorDialogueSurfaceState(
                revision,
                context,
                VisitorDialogueOwner.Guidance,
                VisitorDialogueSurfaceMode.Dialogue,
                copy.Chapter,
                Speaker,
                copy.Body,
                0,
                1,
                primaryActionLabel: copy.ActionLabel,
                allowDefer: false,
                allowRestart: false,
                primaryIntent: VisitorDialogueIntentKind.ChoosePrimary,
                expression: VisitorDialogueExpression.Welcome);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _presenter.IntentRequested -= HandleIntent;
            _visit.TeachingRequested -= PresentTeaching;
            HideActiveSurface();
            _continueAction = null;
        }

        void HandleIntent(VisitorDialogueIntent intent)
        {
            if (_disposed || intent.Owner != VisitorDialogueOwner.Guidance ||
                intent.Context != _context || intent.Revision != _revision ||
                intent.Kind != VisitorDialogueIntentKind.ChoosePrimary ||
                intent.Origin != VisitorDialogueInputOrigin.HandPoke ||
                _continueAction == null || !_visit.InputAllowed)
                return;

            var action = _continueAction;
            _continueAction = null;
            HideActiveSurface();
            action();
        }

        void HideActiveSurface()
        {
            if (!_presenter || !_context.IsValid) return;
            _presenter.Hide(_context);
        }
    }
}
