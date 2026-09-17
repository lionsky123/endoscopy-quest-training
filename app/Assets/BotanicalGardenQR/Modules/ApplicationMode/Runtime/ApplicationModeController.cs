using System;
using System.Collections.Generic;
using System.Diagnostics;
using BotanicalGardenQR.ApplicationMode.Contracts;

namespace BotanicalGardenQR.ApplicationMode.Runtime
{
    public sealed class ApplicationModeController : IApplicationModeController, IDisposable
    {
        readonly float _holdDurationSeconds;
        readonly float _promptTimeoutSeconds;
        readonly IApplicationModeSceneLoader _sceneLoader;
        readonly Dictionary<long, StateSubscription> _subscriptions =
            new Dictionary<long, StateSubscription>();

        ApplicationModeRole _role;
        ApplicationModePhase _phase;
        ApplicationModePromptKind _prompt;
        ApplicationModeFault _fault;
        ApplicationModeState _state;
        float _holdElapsed;
        float _promptIdleElapsed;
        int _activeTransition;
        int _nextTransition;
        long _nextSubscriptionId;
        long _version;
        bool _requiresMenuRelease;
        bool _disposed;

        public ApplicationModeController(
            ApplicationModeRole role,
            float holdDurationSeconds,
            float promptTimeoutSeconds,
            IApplicationModeSceneLoader sceneLoader)
        {
            RequirePositiveFinite(holdDurationSeconds, nameof(holdDurationSeconds));
            RequirePositiveFinite(promptTimeoutSeconds, nameof(promptTimeoutSeconds));

            _role = role;
            _holdDurationSeconds = holdDurationSeconds;
            _promptTimeoutSeconds = promptTimeoutSeconds;
            _sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            _phase = ApplicationModePhase.Idle;
            _prompt = ApplicationModePromptKind.None;
            _state = BuildState();
        }

        public ApplicationModeState CurrentState
        {
            get
            {
                RequireAvailable();
                return _state;
            }
        }

        public void Advance(float unscaledDeltaSeconds, bool isLeftMenuPressed)
        {
            RequireAvailable();
            if (unscaledDeltaSeconds < 0f ||
                float.IsNaN(unscaledDeltaSeconds) ||
                float.IsInfinity(unscaledDeltaSeconds))
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));

            if (AdvancePromptTimeout(unscaledDeltaSeconds)) return;

            if (!isLeftMenuPressed)
            {
                _requiresMenuRelease = false;
                if (_phase == ApplicationModePhase.Holding) SetIdle();
                return;
            }

            if (_requiresMenuRelease || _phase != ApplicationModePhase.Idle &&
                _phase != ApplicationModePhase.Holding)
                return;

            if (_phase == ApplicationModePhase.Idle)
            {
                _phase = ApplicationModePhase.Holding;
                _fault = ApplicationModeFault.None;
                _holdElapsed = 0f;
            }

            _holdElapsed = Math.Min(_holdDurationSeconds, _holdElapsed + unscaledDeltaSeconds);
            if (_holdElapsed >= _holdDurationSeconds)
            {
                _requiresMenuRelease = true;
                if (_role == ApplicationModeRole.Visitor)
                    BeginLoad(ApplicationModeRole.Administrator, ApplicationModePromptKind.None);
                else
                    OpenPrompt(ApplicationModePromptKind.ReturnToVisitor);
                return;
            }

            PublishState();
        }

        public void Dispatch(ApplicationModeIntent intent)
        {
            RequireAvailable();
            if (_phase != ApplicationModePhase.Prompt) return;

            switch (intent.Kind)
            {
                case ApplicationModeIntentKind.Cancel:
                    SetIdle();
                    break;
                case ApplicationModeIntentKind.Confirm:
                    ConfirmPrompt();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(intent));
            }
        }

        public IDisposable Observe(IApplicationModeStateSink sink)
        {
            RequireAvailable();
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            var id = ++_nextSubscriptionId;
            var subscription = new StateSubscription(this, id, sink);
            _subscriptions.Add(id, subscription);
            try
            {
                sink.OnApplicationModeStateChanged(_state);
                return subscription;
            }
            catch
            {
                subscription.Dispose();
                throw;
            }
        }

        bool AdvancePromptTimeout(float deltaSeconds)
        {
            if (_phase != ApplicationModePhase.Prompt) return false;
            _promptIdleElapsed += deltaSeconds;
            if (_promptIdleElapsed < _promptTimeoutSeconds) return false;
            SetIdle();
            return true;
        }

        void OpenPrompt(ApplicationModePromptKind prompt)
        {
            _phase = ApplicationModePhase.Prompt;
            _prompt = prompt;
            _fault = ApplicationModeFault.None;
            _holdElapsed = 0f;
            _promptIdleElapsed = 0f;
            PublishState();
        }

        void ConfirmPrompt()
        {
            _promptIdleElapsed = 0f;
            if (_prompt != ApplicationModePromptKind.ReturnToVisitor) return;
            BeginLoad(ApplicationModeRole.Visitor, ApplicationModePromptKind.ReturnToVisitor);
        }

        void BeginLoad(
            ApplicationModeRole targetRole,
            ApplicationModePromptKind recoveryPrompt)
        {
            _phase = ApplicationModePhase.LoadingScene;
            _prompt = recoveryPrompt;
            _fault = ApplicationModeFault.None;
            _holdElapsed = 0f;
            var transition = ++_nextTransition;
            _activeTransition = transition;
            PublishState();

            try
            {
                _sceneLoader.Load(
                    targetRole,
                    result => CompleteLoad(transition, targetRole, recoveryPrompt, result));
            }
            catch
            {
                CompleteLoad(
                    transition,
                    targetRole,
                    recoveryPrompt,
                    ApplicationModeLoadResult.Failed);
            }
        }

        void CompleteLoad(
            int transition,
            ApplicationModeRole targetRole,
            ApplicationModePromptKind recoveryPrompt,
            ApplicationModeLoadResult result)
        {
            if (_disposed || transition != _activeTransition) return;
            _activeTransition = 0;

            if (result.Succeeded)
            {
                _role = targetRole;
                _requiresMenuRelease = true;
                SetIdle();
                return;
            }

            _phase = recoveryPrompt == ApplicationModePromptKind.None
                ? ApplicationModePhase.Idle
                : ApplicationModePhase.Prompt;
            _prompt = recoveryPrompt;
            _fault = ApplicationModeFault.SceneLoadFailed;
            _promptIdleElapsed = 0f;
            PublishState();
        }

        void SetIdle()
        {
            _phase = ApplicationModePhase.Idle;
            _prompt = ApplicationModePromptKind.None;
            _fault = ApplicationModeFault.None;
            _holdElapsed = 0f;
            _promptIdleElapsed = 0f;
            PublishState();
        }

        void PublishState()
        {
            _version++;
            _state = BuildState();
            if (_subscriptions.Count == 0) return;
            var snapshot = new StateSubscription[_subscriptions.Count];
            _subscriptions.Values.CopyTo(snapshot, 0);
            for (var i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].Sink.OnApplicationModeStateChanged(_state);
                }
                catch (Exception exception)
                {
                    Trace.TraceError(
                        $"ApplicationMode state sink failed while publishing version {_state.Version}: {exception}");
                }
            }
        }

        ApplicationModeState BuildState()
        {
            var holdProgress = _phase == ApplicationModePhase.Holding
                ? Math.Max(0f, Math.Min(1f, _holdElapsed / _holdDurationSeconds))
                : 0f;
            return new ApplicationModeState(
                _version,
                _role,
                _phase,
                _prompt,
                _fault,
                holdProgress);
        }

        void RemoveSubscription(long id)
        {
            _subscriptions.Remove(id);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _activeTransition = 0;
            _subscriptions.Clear();
        }

        void RequireAvailable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ApplicationModeController));
        }

        static void RequirePositiveFinite(float value, string name)
        {
            if (!(value > 0f) || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }

        sealed class StateSubscription : IDisposable
        {
            ApplicationModeController _owner;

            public StateSubscription(
                ApplicationModeController owner,
                long id,
                IApplicationModeStateSink sink)
            {
                _owner = owner;
                Id = id;
                Sink = sink;
            }

            public long Id { get; }
            public IApplicationModeStateSink Sink { get; }

            public void Dispose()
            {
                var owner = _owner;
                if (owner == null) return;
                _owner = null;
                owner.RemoveSubscription(Id);
            }
        }
    }
}
