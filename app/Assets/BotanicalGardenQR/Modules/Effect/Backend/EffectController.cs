using System;
using BotanicalGardenQR.Effect.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Effect.Backend
{
    internal sealed class EffectController : MonoBehaviour, IEffectController
    {
        StateChannel<EffectState> _states;
        EffectImplementationSelector _selector;
        GameObject _instance;
        IEffectRuntime _runtime;
        SessionToken _session;
        EffectPhase _phase = EffectPhase.Closed;
        long _version;
        Action<DiagnosticEvent> _diagnostics;

        public void Initialize(Action<DiagnosticEvent> diagnostics)
        {
            _diagnostics = diagnostics;
            _states = StateChannel<EffectState>.ForCurrentThread(
                new EffectState(default, 0, EffectPhase.Closed),
                state => state.Version);
        }

        public EffectResult Open(SessionToken session, EffectDefinition definition)
        {
            if (!session.IsValid)
                return EffectResult.Failure(EffectFailureCode.StaleSession, "effect.session.invalid");
            if (definition == null || definition.Prefab == null)
                return EffectResult.Failure(EffectFailureCode.InvalidDefinition, "effect.definition.invalid");
            if (_instance != null)
                return EffectResult.Failure(EffectFailureCode.InvalidIntent, "effect.already_open");

            _session = session;
            Publish(EffectPhase.Loading);
            try
            {
                _instance = Instantiate(definition.Prefab, transform, false);
                _instance.name = "EffectRuntimeInstance";
                _instance.transform.localScale = definition.Prefab.transform.localScale * definition.Scale;
                _selector = new EffectImplementationSelector();
                _runtime = _selector.Create(_instance);
                _runtime.Stop();
                Publish(EffectPhase.Ready);
                return EffectResult.Success();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Diagnose("EFFECT_OPEN_FAILED", $"Effect setup failed: {exception.Message}", "open");
                ReleaseInstance("open_cleanup");
                Publish(EffectPhase.Failed, new UserFault("展示效果加载失败"));
                return EffectResult.Failure(EffectFailureCode.LoadFailed, "effect.open.failed");
            }
        }

        public EffectResult Dispatch(SessionToken session, EffectIntent intent)
        {
            if (_instance == null)
                return EffectResult.Failure(EffectFailureCode.NotOpen, "effect.not_open");
            if (session != _session)
                return EffectResult.Failure(EffectFailureCode.StaleSession, "effect.stale_session");

            try
            {
                switch (intent)
                {
                    case EffectIntent.Trigger:
                        _runtime.Trigger();
                        Publish(EffectPhase.Active);
                        return EffectResult.Success();
                    case EffectIntent.Stop:
                        _runtime.Stop();
                        Publish(EffectPhase.Ready);
                        return EffectResult.Success();
                    case EffectIntent.Reset:
                        _runtime.Reset();
                        Publish(EffectPhase.Ready);
                        return EffectResult.Success();
                    default:
                        return EffectResult.Failure(EffectFailureCode.InvalidIntent, "effect.intent.unknown");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Diagnose("EFFECT_RUNTIME_FAILED", $"Effect command failed: {exception.Message}", "dispatch");
                Publish(EffectPhase.Failed, new UserFault("展示效果运行失败"));
                return EffectResult.Failure(EffectFailureCode.LoadFailed, "effect.runtime.failed");
            }
        }

        public EffectResult Close(SessionToken session)
        {
            if (_instance == null) return EffectResult.Success();
            if (session != _session)
                return EffectResult.Failure(EffectFailureCode.StaleSession, "effect.stale_session");
            ReleaseInstance("close");
            Publish(EffectPhase.Closed);
            return EffectResult.Success();
        }

        public IDisposable Observe(IEffectStateSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (_states == null) throw new InvalidOperationException("Effect controller is not initialized.");
            return _states.Observe(sink.Publish);
        }

        void ReleaseInstance(string stage)
        {
            var runtime = _runtime;
            var instance = _instance;
            _runtime = null;
            _selector = null;
            _instance = null;

            try
            {
                runtime?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Diagnose("EFFECT_RELEASE_FAILED", $"Effect runtime release failed: {exception.Message}", stage);
            }

            if (instance != null) Destroy(instance);
        }

        void OnDestroy()
        {
            ReleaseInstance("destroy");
            _states?.Dispose();
        }

        void Diagnose(string code, string message, string stage)
            => _diagnostics?.Invoke(new DiagnosticEvent(
                code,
                message,
                "Effect",
                stage,
                DateTimeOffset.UtcNow,
                sessionToken: _session));

        void Publish(EffectPhase phase, UserFault fault = null)
        {
            _phase = phase;
            var state = new EffectState(_session, ++_version, phase, fault);
            _states.Publish(state);
        }
    }
}
