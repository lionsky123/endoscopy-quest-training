using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.Video.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Video.Frontend
{
    public sealed class VideoPageLifecycleBinding : IFeaturePageLifecycle
    {
        readonly IGlobalFrontendShell _shell;
        readonly IVideoDefinitionSource _definitions;
        readonly IVideoController _controller;
        readonly VideoFrontend _frontend;
        FrontendFeatureSurfaceLease _shellLease;
        VideoSurfaceLease _featureLease;
        IDisposable _subscription;
        SessionToken _session;

        public VideoPageLifecycleBinding(IGlobalFrontendShell shell, IVideoDefinitionSource definitions, IVideoController controller, VideoFrontend frontend)
        {
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _frontend = frontend != null ? frontend : throw new ArgumentNullException(nameof(frontend));
        }

        public FeaturePageId PageId => FeaturePageId.Video;

        public FlowResult Prepare(SessionToken session, SceneId sceneId)
        {
            if (_session.IsValid) return FlowResult.Reject(FlowFailure.InvalidTransition);
            if (!_definitions.TryGet(sceneId, out var definition) || definition == null) return FlowResult.Reject(FlowFailure.PageUnavailable);
            var surface = _shell.AcquireFeatureSurface(session, PageId);
            if (!surface.Succeeded) return FlowResult.Reject(FlowFailure.PreparationFailed, surface.Fault);

            _session = session;
            _shellLease = surface.Lease;
            var target = _frontend.CreateSurface(
                _shellLease.MediaStageRoot,
                new Vector2(_shellLease.MediaStageSize.x, _shellLease.MediaStageSize.y));
            _featureLease = new VideoSurfaceLease(target, false, target.Dispose);
            _frontend.Bind(session, _controller);
            _subscription = _controller.Observe(_frontend);
            // VideoPlayer must prepare from an active hierarchy. Flow activation
            // follows synchronously, so expose the loading page before Open.
            _frontend.SetVisible(true);
            var opened = _controller.Open(session, definition, _featureLease);
            if (opened.Succeeded) return FlowResult.Success;
            Cleanup(session, true);
            return FlowResult.Reject(FlowFailure.PreparationFailed);
        }

        public FlowResult Activate(SessionToken session)
        {
            if (session != _session) return FlowResult.Reject(FlowFailure.StaleSession);
            _frontend.SetVisible(true);
            return FlowResult.Success;
        }

        public FlowResult Deactivate(SessionToken session)
        {
            if (session != _session) return FlowResult.Reject(FlowFailure.StaleSession);
            _frontend.SetVisible(false);
            return FlowResult.Success;
        }

        public FlowResult Release(SessionToken session)
        {
            if (session != _session) return FlowResult.Reject(FlowFailure.StaleSession);
            var closed = TryCloseController(session);
            Cleanup(session, false);
            return closed
                ? FlowResult.Success
                : FlowResult.Reject(
                    FlowFailure.LifecycleFailed,
                    new UserFault("视频关闭时发生错误，已返回主界面。"));
        }

        void Cleanup(SessionToken session, bool close)
        {
            if (close) TryCloseController(session);

            var subscription = _subscription;
            _subscription = null;
            var featureLease = _featureLease;
            _featureLease = null;
            var shellLease = _shellLease;
            _shellLease = null;
            _session = default;

            TryCleanup(() => subscription?.Dispose());
            TryCleanup(_frontend.Unbind);
            TryCleanup(() => ReleaseUnreleasedFeatureLease(featureLease));
            TryCleanup(() => shellLease?.Dispose());
        }

        // The controller releases an adopted feature lease. A rejected Open can
        // leave the wrapper with the lifecycle, so only reclaim an active lease.
        bool TryCloseController(SessionToken session)
        {
            try
            {
                var closed = _controller.Close(session);
                if (closed.Succeeded) return true;
                Debug.LogError("Video controller rejected a lifecycle close; local ownership will still be released.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            return false;
        }

        static void ReleaseUnreleasedFeatureLease(VideoSurfaceLease lease)
        {
            if (lease != null && !lease.IsDisposed)
                lease.Dispose();
        }

        static void TryCleanup(Action cleanup)
        {
            try { cleanup(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
