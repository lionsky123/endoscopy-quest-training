using System;
using BotanicalGardenQR.Effect.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;

namespace BotanicalGardenQR.Effect.Backend
{
    internal sealed class EffectSessionBinding : IFlowStateSink, IDisposable
    {
        readonly IEffectDefinitionSource _definitions;
        readonly IEffectController _controller;
        readonly IDisposable _subscription;
        SessionToken _openSession;
        bool _isOpen;
        bool _disposed;

        public EffectSessionBinding(IExperienceFlow flow, IEffectDefinitionSource definitions, IEffectController controller)
        {
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _subscription = flow.Observe(this);
        }

        public void OnStateChanged(ExperienceFlowState state)
        {
            if (_disposed || state == null) return;
            if (state.Page.Kind == FlowPageKind.Closed)
            {
                CloseCurrent();
                return;
            }
            if (_isOpen && state.Session == _openSession) return;

            CloseCurrent();
            if (!_definitions.TryGet(state.SceneId, out var definition)) return;
            var opened = _controller.Open(state.Session, definition);
            if (!opened.Succeeded) return;

            _openSession = state.Session;
            _isOpen = true;
            if (definition.TriggerPolicy == EffectTriggerPolicy.OnSessionOpen)
                _controller.Dispatch(state.Session, EffectIntent.Trigger);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription.Dispose();
            CloseCurrent();
        }

        void CloseCurrent()
        {
            if (!_isOpen) return;
            _controller.Close(_openSession);
            _openSession = default;
            _isOpen = false;
        }
    }
}
