using System;
using BotanicalGardenQR.ApplicationMode.Contracts;
using BotanicalGardenQR.ApplicationMode.Runtime;
using UnityEngine;

namespace BotanicalGardenQR.ApplicationMode.Adapters
{
    public sealed class ApplicationModeControllerHost : MonoBehaviour, IApplicationModeController
    {
        [SerializeField] ApplicationModeRole _sceneRole = ApplicationModeRole.Visitor;
        [SerializeField] ApplicationModeOptionsAsset _options;

        ApplicationModeController _controller;
        ApplicationModeState _unavailableState;

        public bool IsReady => _controller != null;
        public ApplicationModeState CurrentState =>
            _controller != null ? _controller.CurrentState : _unavailableState;

        void Awake()
        {
            _unavailableState = new ApplicationModeState(
                0,
                _sceneRole,
                ApplicationModePhase.Idle,
                ApplicationModePromptKind.None,
                ApplicationModeFault.None,
                0);

            if (_options == null)
            {
                Debug.LogError(
                    "[ApplicationMode] MODE_CONFIGURATION_INVALID options_missing",
                    this);
                return;
            }
            if (!_options.TryValidate(_sceneRole, out var reason))
            {
                Debug.LogError(
                    $"[ApplicationMode] MODE_CONFIGURATION_INVALID {reason}",
                    this);
                return;
            }

            _controller = new ApplicationModeController(
                _sceneRole,
                _options.MenuHoldSeconds,
                _options.PromptTimeoutSeconds,
                new UnitySceneTransitionAdapter(_options));
        }

        public void Advance(float unscaledDeltaSeconds, bool isLeftMenuPressed)
        {
            _controller?.Advance(unscaledDeltaSeconds, isLeftMenuPressed);
        }

        public void Dispatch(ApplicationModeIntent intent)
        {
            _controller?.Dispatch(intent);
        }

        public IDisposable Observe(IApplicationModeStateSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (_controller != null) return _controller.Observe(sink);
            sink.OnApplicationModeStateChanged(_unavailableState);
            return EmptySubscription.Instance;
        }

        void OnDestroy()
        {
            _controller?.Dispose();
            _controller = null;
        }

        sealed class EmptySubscription : IDisposable
        {
            public static readonly EmptySubscription Instance = new EmptySubscription();
            public void Dispose() { }
        }
    }
}
