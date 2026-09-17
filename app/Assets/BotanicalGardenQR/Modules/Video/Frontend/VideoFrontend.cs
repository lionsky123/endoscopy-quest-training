using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.Video.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Video.Frontend
{
    public sealed class VideoFrontend : MonoBehaviour, IVideoStateSink
    {
        [SerializeField] GameObject _pageRoot;
        [SerializeField] GameObject _controlsRoot;
        [SerializeField] MonoBehaviour _dock;

        IVideoController _controller;
        SessionToken _session;
        VideoState _state;
        bool _bound;
        bool _hasRenderedState;
        double _displayPosition;

        IEntryMediaControlDock Dock => _dock as IEntryMediaControlDock;

        public IVideoSurfaceTarget CreateSurface(Transform stageRoot, Vector2 availableSize)
            => VideoSurfaceTarget.Create(stageRoot, availableSize);

        public void Bind(SessionToken session, IVideoController controller)
        {
            if (!session.IsValid) throw new ArgumentException("A valid session is required.", nameof(session));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _session = session;
            _state = default;
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
            _hasRenderedState = false;
            _displayPosition = 0d;
        }

        public void SetVisible(bool visible)
        {
            if (_pageRoot != null) _pageRoot.SetActive(visible);
            if (_controlsRoot != null) _controlsRoot.SetActive(visible);
            var dock = Dock;
            if (dock != null)
            {
                if (visible) RenderDock();
                else dock.Render(EntryMediaControlDockViewModel.Hidden, default);
            }
        }

        public void TogglePlayback() => Dispatch(new VideoIntent(VideoIntentKind.TogglePlayback));
        public void Replay() => Dispatch(new VideoIntent(VideoIntentKind.Replay));
        public void ToggleMute() => Dispatch(new VideoIntent(VideoIntentKind.ToggleMute));

        public void Publish(VideoState state)
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
            if (!_bound || _state.Phase != VideoPhase.Playing || _state.DurationSeconds <= 0d)
                return;
            _displayPosition = Math.Min(_state.DurationSeconds, _displayPosition + Time.unscaledDeltaTime);
            RenderProgress();
        }

        void RenderDock()
        {
            var dock = Dock;
            if (dock == null || !_bound) return;
            var isLoading = _state.Phase == VideoPhase.Loading;
            var progress = isLoading
                ? 0f
                : _state.DurationSeconds > 0d
                ? Mathf.Clamp01((float)(_displayPosition / _state.DurationSeconds))
                : -1f;
            var mode = isLoading
                ? EntryMediaControlDockMode.VideoPreparing
                : _state.Phase == VideoPhase.Failed
                    ? EntryMediaControlDockMode.Error
                        : _state.Phase == VideoPhase.Closed
                            ? EntryMediaControlDockMode.Hidden
                            : _state.Phase == VideoPhase.Completed
                                ? EntryMediaControlDockMode.Completed
                                : EntryMediaControlDockMode.Video;
            var view = new EntryMediaControlDockViewModel(
                mode,
                "影像内容",
                _state.Phase == VideoPhase.Playing,
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

        static bool RequiresFullRender(VideoState previous, VideoState next)
            => previous.Phase != next.Phase ||
               previous.DurationSeconds != next.DurationSeconds ||
               previous.IsMuted != next.IsMuted ||
               previous.CanPauseResume != next.CanPauseResume ||
               previous.CanReplay != next.CanReplay ||
               previous.CanMute != next.CanMute ||
               !ReferenceEquals(previous.Fault, next.Fault);

        void Dispatch(VideoIntent intent)
        {
            if (!_bound) return;
            _controller.Dispatch(_session, intent);
        }

        void OnDestroy() => Unbind();

        void OnValidate()
        {
            if (_dock != null && Dock == null)
                Debug.LogError($"{nameof(VideoFrontend)} requires a dock component that implements {nameof(IEntryMediaControlDock)}.", this);
        }
    }
}
