using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.Narration.Contracts;

namespace BotanicalGardenQR.Narration.Frontend
{
    public sealed class NarrationDockBinding : IFlowStateSink, IMainSurfaceLifecycle, IDisposable
    {
        readonly IGlobalFrontendShell _shell;
        readonly INarrationDefinitionSource _definitions;
        readonly INarrationController _controller;
        readonly NarrationFrontend _frontend;
        readonly IDisposable _flowSubscription;
        readonly Action<bool> _setFairyAmbientAudioSuppressed;

        FrontendNarrationDockLease _dockLease;
        NarrationSurfaceLease _surfaceLease;
        IDisposable _controllerSubscription;
        SessionToken _session;
        bool _disposed;

        public NarrationDockBinding(
            IExperienceFlow flow,
            IGlobalFrontendShell shell,
            INarrationDefinitionSource definitions,
            INarrationController controller,
            NarrationFrontend frontend,
            Action<bool> setFairyAmbientAudioSuppressed = null)
        {
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _frontend = frontend ?? throw new ArgumentNullException(nameof(frontend));
            _setFairyAmbientAudioSuppressed = setFairyAmbientAudioSuppressed ?? (_ => { });
            _flowSubscription = (flow ?? throw new ArgumentNullException(nameof(flow))).Observe(this);
        }

        public void BeforeFeatureEnter(SessionToken session)
        {
            if (_disposed || session != _session) return;
            CloseDock(session);
        }

        public void OnStateChanged(ExperienceFlowState state)
        {
            if (_disposed || state == null) return;
            if (state.Page.Kind == FlowPageKind.Main)
            {
                OpenDock(state);
                return;
            }
            if (_session.IsValid)
                CloseDock(_session);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _flowSubscription.Dispose();
            if (_session.IsValid) CloseDock(_session);
        }

        void OpenDock(ExperienceFlowState state)
        {
            if (!state.Session.IsValid) return;
            if (_session == state.Session && _dockLease != null && !_dockLease.IsDisposed)
            {
                _frontend.SetVisible(true);
                return;
            }
            if (_session.IsValid)
                CloseDock(_session);
            if (!_definitions.TryGet(state.SceneId, out var definition) || definition == null)
                return;

            _dockLease = _shell.AcquireNarrationDock(state.Session);
            if (_dockLease == null) return;
            _session = state.Session;
            _surfaceLease = new NarrationSurfaceLease(_dockLease.Root, _dockLease.Size, true, () => { });
            _frontend.Bind(_session, _controller, state.Title);
            _controllerSubscription = _controller.Observe(_frontend);
            _setFairyAmbientAudioSuppressed(true);
            // The controller may start an OnPageEnter clip during Open. Keep the
            // surface active first so an AudioSource parented under the dock is
            // already audible when that policy is evaluated.
            _frontend.SetVisible(true);
            var opened = _controller.Open(_session, definition, _surfaceLease);
            if (!opened.Succeeded)
            {
                CloseDock(_session);
                return;
            }
        }

        void CloseDock(SessionToken session)
        {
            if (_session.IsValid && session == _session)
            {
                _controller.Close(_session);
                _setFairyAmbientAudioSuppressed(false);
            }
            _controllerSubscription?.Dispose();
            _controllerSubscription = null;
            _frontend.Unbind();
            ReleaseUnreleasedSurfaceLease();
            _dockLease?.Dispose();
            _dockLease = null;
            _session = default;
        }

        void ReleaseUnreleasedSurfaceLease()
        {
            if (_surfaceLease != null && !_surfaceLease.IsDisposed)
                _surfaceLease.Dispose();
            _surfaceLease = null;
        }
    }
}
