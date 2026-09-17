using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.ImageRing.Contracts;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed class PanoramaImageRingBinding : IImageRingStateSink, IDisposable
    {
        readonly IImageRingDefinitionSource _definitions;
        readonly IImageRingController _controller;
        readonly IDisposable _subscription;

        SessionToken _session;
        Action _closed;
        bool _active;
        bool _disposed;

        public PanoramaImageRingBinding(
            IImageRingDefinitionSource definitions,
            IImageRingController controller)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _subscription = _controller.Observe(this);
        }

        public bool HasDefinition(SceneId sceneId)
            => !_disposed && _definitions.TryGet(sceneId, out var definition) && definition != null;

        public bool Open(SessionToken session, SceneId sceneId, Action closed)
        {
            if (_disposed || _active || !session.IsValid || closed == null) return false;
            if (!_definitions.TryGet(sceneId, out var definition) || definition == null) return false;

            _session = session;
            _closed = closed;
            _active = true;
            var opened = _controller.Open(session, definition);
            if (opened.Succeeded && _active && _session == session) return true;

            Reset(false);
            return false;
        }

        public void Close(SessionToken session)
        {
            if (_disposed || !_active || session != _session) return;
            var closed = _controller.Close(session);
            if (!closed.Succeeded) Reset(true);
        }

        public void Publish(ImageRingState state)
        {
            if (_disposed || !_active || state.Session != _session) return;
            if (state.Phase == ImageRingPhase.Closed || state.Phase == ImageRingPhase.Failed)
                Reset(true);
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_active) _controller.Close(_session);
            _disposed = true;
            _subscription.Dispose();
            Reset(false);
        }

        void Reset(bool notify)
        {
            var callback = notify ? _closed : null;
            _active = false;
            _session = default;
            _closed = null;
            callback?.Invoke();
        }
    }
}
