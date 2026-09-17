using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.Narration.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Narration.Frontend
{
    public sealed class NarrationFrontend : MonoBehaviour, INarrationStateSink
    {
        [SerializeField] GameObject _dockRoot;
        [SerializeField] MonoBehaviour _dock;

        INarrationController _controller;
        SessionToken _session;
        NarrationState _state;
        string _title;
        bool _bound;
        bool _hasRenderedState;
        double _displayPosition;

        IEntryMediaControlDock Dock => _dock as IEntryMediaControlDock;

        public void Bind(SessionToken session, INarrationController controller, string title)
        {
            if (!session.IsValid) throw new ArgumentException("A valid session is required.", nameof(session));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _session = session;
            _state = default;
            _title = string.IsNullOrWhiteSpace(title) ? "语音讲解" : title.Trim();
            _hasRenderedState = false;
            _displayPosition = 0d;
            _bound = true;
            SetVisible(false);
        }

        public void Unbind()
        {
            SetVisible(false);
            _bound = false;
            _session = default;
            _controller = null;
            _state = default;
            _title = null;
            _hasRenderedState = false;
            _displayPosition = 0d;
        }

        public void SetVisible(bool visible)
        {
            if (_dockRoot != null) _dockRoot.SetActive(visible);
            var dock = Dock;
            if (dock == null) return;
            if (visible) RenderDock();
            else dock.Render(EntryMediaControlDockViewModel.Hidden, default);
        }

        public void TogglePlayback() => Dispatch(new NarrationIntent(NarrationIntentKind.TogglePlayback));
        public void Replay() => Dispatch(new NarrationIntent(NarrationIntentKind.Replay));
        public void ToggleMute() => Dispatch(new NarrationIntent(NarrationIntentKind.ToggleMute));

        public void Publish(NarrationState state)
        {
            if (!_bound || state.Session != _session) return;
            var requiresFullRender = !_hasRenderedState || RequiresFullRender(_state, state);
            _state = state;
            _displayPosition = state.PositionSeconds;
            if (requiresFullRender) RenderDock();
            else RenderProgress();
            _hasRenderedState = true;
        }

        void Update()
        {
            if (!_bound || _state.Phase != NarrationPhase.Playing || _state.DurationSeconds <= 0d)
                return;
            _displayPosition = Math.Min(_state.DurationSeconds, _displayPosition + Time.unscaledDeltaTime);
            RenderProgress();
        }

        void RenderDock()
        {
            var dock = Dock;
            if (dock == null || !_bound) return;
            var progress = _state.DurationSeconds > 0d
                ? Mathf.Clamp01((float)(_displayPosition / _state.DurationSeconds))
                : -1f;
            var mode = _state.Phase == NarrationPhase.Loading
                ? EntryMediaControlDockMode.Preparing
                : _state.Phase == NarrationPhase.Failed
                    ? EntryMediaControlDockMode.Error
                    : _state.Phase == NarrationPhase.Closed
                        ? EntryMediaControlDockMode.Hidden
                        : _state.Phase == NarrationPhase.Completed
                            ? EntryMediaControlDockMode.NarrationCompleted
                            : EntryMediaControlDockMode.Narration;
            var view = new EntryMediaControlDockViewModel(
                mode,
                _title,
                _state.Phase == NarrationPhase.Playing,
                _state.CanReplay,
                _state.IsMuted,
                progress,
                _state.CanPauseResume,
                _state.CanMute);
            var callbacks = new EntryMediaControlDockCallbacks(TogglePlayback, Replay, ToggleMute);
            dock.Render(view, callbacks);
        }

        void RenderProgress()
        {
            var dock = Dock;
            if (dock == null || !_bound) return;
            var hasProgress = _state.DurationSeconds > 0d;
            var progress = hasProgress
                ? Mathf.Clamp01((float)(_displayPosition / _state.DurationSeconds))
                : 0f;
            dock.UpdateProgress(progress, hasProgress);
        }

        static bool RequiresFullRender(NarrationState previous, NarrationState next)
            => previous.Phase != next.Phase ||
               previous.DurationSeconds != next.DurationSeconds ||
               previous.IsMuted != next.IsMuted ||
               previous.CanPauseResume != next.CanPauseResume ||
               previous.CanReplay != next.CanReplay ||
               previous.CanMute != next.CanMute ||
               !ReferenceEquals(previous.Fault, next.Fault);

        void Dispatch(NarrationIntent intent)
        {
            if (!_bound) return;
            _controller.Dispatch(_session, intent);
        }

        void OnDestroy() => Unbind();

        void OnValidate()
        {
            if (_dock != null && Dock == null)
                Debug.LogError($"{nameof(NarrationFrontend)} requires a dock component that implements {nameof(IEntryMediaControlDock)}.", this);
        }
    }
}
