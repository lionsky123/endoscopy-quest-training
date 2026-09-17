using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.Panorama.Contracts;
using TMPro;
using UnityEngine;

namespace BotanicalGardenQR.Panorama.Frontend
{
    public sealed class PanoramaAuxiliaryExperienceBinding
    {
        readonly Func<SceneId, bool> _isAvailable;
        readonly Func<SessionToken, SceneId, Action, bool> _open;
        readonly Action<SessionToken> _close;

        SessionToken _session;
        Action _closed;
        int _generation;
        bool _isOpen;

        public PanoramaAuxiliaryExperienceBinding(
            Func<SceneId, bool> isAvailable,
            Func<SessionToken, SceneId, Action, bool> open,
            Action<SessionToken> close)
        {
            _isAvailable = isAvailable ?? throw new ArgumentNullException(nameof(isAvailable));
            _open = open ?? throw new ArgumentNullException(nameof(open));
            _close = close ?? throw new ArgumentNullException(nameof(close));
        }

        public bool IsAvailable(SceneId sceneId) => _isAvailable(sceneId);

        public bool TryOpen(SessionToken session, SceneId sceneId, Action closed)
        {
            if (!session.IsValid || !sceneId.IsValid || closed == null) return false;
            if (_isOpen) return session == _session;

            var generation = ++_generation;
            _session = session;
            _closed = closed;
            _isOpen = true;
            try
            {
                var opened = _open(
                    session,
                    sceneId,
                    () => HandleClosed(session, generation));
                if (opened && IsCurrent(session, generation)) return true;
                if (IsCurrent(session, generation)) Reset();
                return false;
            }
            catch
            {
                if (IsCurrent(session, generation)) Reset();
                throw;
            }
        }

        public void Close(SessionToken session)
        {
            if (!_isOpen || session != _session) return;

            var closingSession = _session;
            ++_generation;
            Reset();
            _close(closingSession);
        }

        void HandleClosed(SessionToken session, int generation)
        {
            if (!IsCurrent(session, generation)) return;
            var closed = _closed;
            Reset();
            closed();
        }

        bool IsCurrent(SessionToken session, int generation)
            => _isOpen && session == _session && generation == _generation;

        void Reset()
        {
            _isOpen = false;
            _session = default;
            _closed = null;
        }
    }

    public sealed class PanoramaPageLifecycleBinding : IFeaturePageLifecycle, IPanoramaStateSink
    {
        readonly IGlobalFrontendShell _shell;
        readonly IPanoramaDefinitionSource _definitions;
        readonly IPanoramaController _controller;
        readonly PanoramaFrontend _frontend;
        readonly Transform _runtimeRoot;
        readonly Transform _viewer;
        readonly IFrontendGazeSurfaceRegistry _gazeSurfaces;
        readonly TMP_FontAsset _sharedFont;
        readonly PanoramaAuxiliaryExperienceBinding _auxiliaryExperience;

        PanoramaSurfaceLease _featureLease;
        IDisposable _subscription;
        SessionToken _session;
        SceneId _sceneId;
        UserFault _failureFault;
        bool _active;
        bool _failureHandled;

        public PanoramaPageLifecycleBinding(
            IGlobalFrontendShell shell,
            IPanoramaDefinitionSource definitions,
            IPanoramaController controller,
            PanoramaFrontend frontend,
            Transform runtimeRoot,
            Transform viewer,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            TMP_FontAsset sharedFont,
            PanoramaAuxiliaryExperienceBinding auxiliaryExperience)
        {
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _frontend = frontend != null ? frontend : throw new ArgumentNullException(nameof(frontend));
            _runtimeRoot = runtimeRoot ?? throw new ArgumentNullException(nameof(runtimeRoot));
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            _gazeSurfaces = gazeSurfaces ?? throw new ArgumentNullException(nameof(gazeSurfaces));
            _sharedFont = sharedFont != null
                ? sharedFont
                : throw new ArgumentNullException(nameof(sharedFont));
            _auxiliaryExperience = auxiliaryExperience ??
                                   throw new ArgumentNullException(nameof(auxiliaryExperience));
        }

        public FeaturePageId PageId => FeaturePageId.Panorama;

        public FlowResult Prepare(SessionToken session, SceneId sceneId)
        {
            if (_session.IsValid) return FlowResult.Reject(FlowFailure.InvalidTransition);
            if (!_definitions.TryGet(sceneId, out var definition) || definition == null)
                return FlowResult.Reject(FlowFailure.PageUnavailable);

            _session = session;
            _sceneId = sceneId;
            _failureFault = null;
            _active = false;
            _failureHandled = false;
            _featureLease = new PanoramaSurfaceLease(_runtimeRoot, new Vector2(1f, 1f), false, () => { });
            var hasImageRing = _auxiliaryExperience.IsAvailable(sceneId);
            _frontend.Bind(
                session,
                _gazeSurfaces,
                _viewer,
                _runtimeRoot,
                _sharedFont,
                RequestExit,
                definition.EnvironmentMoments,
                hasImageRing,
                RequestImageRing,
                definition.ClinicalLearning,
                definition.Source.Texture,
                definition.TeachingComparisons,
                RequestClinicalCompletion);
            _subscription = _controller.Observe(this);

            var opened = _controller.Open(session, definition, _featureLease);
            if (opened.Succeeded && _failureFault == null) return FlowResult.Success;

            var fault = _failureFault ?? new UserFault("360 加载失败，请重试。");
            Cleanup(session, true);
            return FlowResult.Reject(FlowFailure.PreparationFailed, fault);
        }

        public FlowResult Activate(SessionToken session)
        {
            if (session != _session) return FlowResult.Reject(FlowFailure.StaleSession);
            if (_failureFault != null) return FlowResult.Reject(FlowFailure.LifecycleFailed, _failureFault);

            var immersive = _shell.SetImmersiveFeature(session, PageId, true);
            if (!immersive.Succeeded) return immersive;
            try
            {
                _frontend.SetVisible(true);
                _active = true;
                return FlowResult.Success;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _active = false;
                TryCleanup(() => _frontend.SetVisible(false));
                TryCleanup(() => { _shell.SetImmersiveFeature(session, PageId, false); });
                return FlowResult.Reject(
                    FlowFailure.LifecycleFailed,
                    new UserFault("360 显示失败，请重试。"));
            }
        }

        public FlowResult Deactivate(SessionToken session)
        {
            if (session != _session) return FlowResult.Reject(FlowFailure.StaleSession);
            CloseImageRing(session);
            _active = false;
            var immersive = _shell.SetImmersiveFeature(session, PageId, false);
            _frontend.SetVisible(false);
            return immersive.Succeeded ? FlowResult.Success : immersive;
        }

        public FlowResult Release(SessionToken session)
        {
            if (session != _session) return FlowResult.Reject(FlowFailure.StaleSession);
            TryCleanup(() => CloseImageRing(session));
            var closed = TryCloseController(session);
            Cleanup(session, false);
            return closed
                ? FlowResult.Success
                : FlowResult.Reject(
                    FlowFailure.LifecycleFailed,
                    new UserFault("360 关闭时发生错误，已返回主界面。"));
        }

        public void Publish(PanoramaState state)
        {
            if (state.Session != _session || state.Phase != PanoramaPhase.Failed) return;

            _failureFault = state.Fault ?? new UserFault("360 加载失败，请重试。");
            if (!_active || _failureHandled) return;

            _failureHandled = true;
            var returned = _shell.Dispatch(FlowIntent.BackToMain(state.Session));
            if (returned.Succeeded) _shell.ShowStatus(state.Session, _failureFault);
        }

        void RequestExit()
        {
            if (!_active || !_session.IsValid) return;
            _shell.Dispatch(FlowIntent.BackToMain(_session));
        }

        void RequestClinicalCompletion()
        {
            if (!_active || !_session.IsValid) return;
            if (_shell is IClinicalLessonCompletion completion)
            {
                var result = completion.CompleteClinicalLesson(_session);
                if (!result.Succeeded) _shell.ShowStatus(_session, new UserFault("练习暂时无法打开，请重新进入本关。"));
            }
            else RequestExit();
        }

        void RequestImageRing()
        {
            if (!_active || !_session.IsValid) return;
            if (!_auxiliaryExperience.TryOpen(_session, _sceneId, HandleImageRingClosed))
            {
                _shell.ShowStatus(_session, new UserFault("图片环廊加载失败，请重试。"));
                return;
            }

            _frontend.SetControlsVisible(false);
        }

        void HandleImageRingClosed()
        {
            if (_active && _session.IsValid)
                _frontend.SetControlsVisible(true);
        }

        void CloseImageRing(SessionToken session) => _auxiliaryExperience.Close(session);

        void Cleanup(SessionToken session, bool close)
        {
            TryCleanup(() => CloseImageRing(session));
            _active = false;
            if (close) TryCloseController(session);

            var subscription = _subscription;
            _subscription = null;
            var featureLease = _featureLease;
            _featureLease = null;
            _failureFault = null;
            _failureHandled = false;
            _sceneId = default;
            _session = default;

            TryCleanup(() => subscription?.Dispose());
            TryCleanup(_frontend.Unbind);
            TryCleanup(() => ReleaseUnreleasedFeatureLease(featureLease));
            TryCleanup(() => { _shell.SetImmersiveFeature(session, PageId, false); });
        }

        bool TryCloseController(SessionToken session)
        {
            try
            {
                var closed = _controller.Close(session);
                if (closed.Succeeded) return true;
                Debug.LogError("Panorama controller rejected a lifecycle close; local ownership will still be released.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            return false;
        }

        static void ReleaseUnreleasedFeatureLease(PanoramaSurfaceLease lease)
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
