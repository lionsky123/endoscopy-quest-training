using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.ImageRing.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.ImageRing.Backend
{
    internal sealed class ImageRingController : IImageRingController
    {
        readonly IImageRingRuntime _runtime;
        readonly Action<DiagnosticEvent> _diagnostics;
        readonly StateChannel<ImageRingState> _states;

        SessionToken _session;
        ImageRingDefinition _definition;
        long _version;
        int _generation;
        int _activeAudioIndex = -1;
        bool _isOpen;
        bool _disposed;

        public ImageRingController(
            IImageRingRuntime runtime,
            Action<DiagnosticEvent> diagnostics = null)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _diagnostics = diagnostics;
            _states = StateChannel<ImageRingState>.ForCurrentThread(
                new ImageRingState(default, 0, ImageRingPhase.Closed),
                state => state.Version);
        }

        public ImageRingResult Open(SessionToken session, ImageRingDefinition definition)
        {
            if (_disposed)
                return ImageRingResult.Failure(ImageRingFailureCode.RuntimeFailed, "image_ring.disposed");
            if (!session.IsValid)
                return ImageRingResult.Failure(ImageRingFailureCode.InvalidSession, "image_ring.session.invalid");
            if (definition == null)
                return ImageRingResult.Failure(ImageRingFailureCode.InvalidDefinition, "image_ring.definition.invalid");
            if (_isOpen || _session.IsValid)
                return ImageRingResult.Failure(ImageRingFailureCode.AlreadyOpen, "image_ring.already_open");

            _session = session;
            _definition = definition;
            _isOpen = true;
            var generation = ++_generation;
            Publish(ImageRingPhase.Opening);

            try
            {
                _runtime.Open(
                    session,
                    definition,
                    intent => HandleRuntimePlaybackIntent(session, generation, intent),
                    () => HandleRuntimeCloseRequest(session, generation));
                if (_isOpen && _session == session && _generation == generation)
                {
                    Diagnose("IMAGE_RING_OPENED", $"Image ring opened with {definition.Items.Count} items.", "open");
                    Publish(ImageRingPhase.Visible);
                }
                return ImageRingResult.Success();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                TryCloseRuntime();
                _isOpen = false;
                ResetPlaybackState();
                Diagnose("IMAGE_RING_OPEN_FAILED", exception.Message, "open");
                Publish(ImageRingPhase.Failed, new UserFault("图片环廊加载失败，请重试。"));
                _session = default;
                return ImageRingResult.Failure(ImageRingFailureCode.RuntimeFailed, "image_ring.open_failed");
            }
        }

        public ImageRingResult Close(SessionToken session)
        {
            if (_disposed) return ImageRingResult.Success();
            if (!_session.IsValid) return ImageRingResult.Success();
            if (session != _session)
                return ImageRingResult.Failure(ImageRingFailureCode.StaleSession, "image_ring.stale_session");

            ++_generation;
            var closingSession = _session;
            try
            {
                _runtime.Close();
                _isOpen = false;
                ResetPlaybackState();
                Diagnose("IMAGE_RING_CLOSED", "Image ring closed.", "close");
                Publish(ImageRingPhase.Closed);
                _session = default;
                return ImageRingResult.Success();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _isOpen = false;
                ResetPlaybackState();
                Diagnose("IMAGE_RING_CLOSE_FAILED", exception.Message, "close");
                Publish(ImageRingPhase.Failed, new UserFault("图片环廊关闭失败。"));
                _session = default;
                return ImageRingResult.Failure(ImageRingFailureCode.RuntimeFailed, "image_ring.close_failed");
            }
            finally
            {
                if (_session == closingSession && !_isOpen)
                    _session = default;
            }
        }

        public IDisposable Observe(IImageRingStateSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (_disposed) throw new ObjectDisposedException(nameof(ImageRingController));
            return _states.Observe(sink.Publish);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ++_generation;
            TryCloseRuntime();
            _runtime.Dispose();
            _isOpen = false;
            _session = default;
            ResetPlaybackState();
            _states.Dispose();
        }

        void HandleRuntimePlaybackIntent(
            SessionToken session,
            int generation,
            ImageRingItemPlaybackIntent intent)
        {
            if (_disposed || !_isOpen || generation != _generation || session != _session || _definition == null)
                return;
            if (intent.ItemIndex < 0 || intent.ItemIndex >= _definition.Items.Count)
            {
                Diagnose("IMAGE_RING_AUDIO_INDEX_INVALID", $"Image-ring audio index {intent.ItemIndex} is invalid.", "audio");
                return;
            }

            try
            {
                if (intent.Kind == ImageRingItemPlaybackIntentKind.Play)
                {
                    if (_activeAudioIndex >= 0 && _activeAudioIndex != intent.ItemIndex)
                        _runtime.StopAudio(true);
                    _runtime.PlayAudio(_definition.Items[intent.ItemIndex].Audio);
                    _activeAudioIndex = intent.ItemIndex;
                    Diagnose("IMAGE_RING_AUDIO_PLAYED", $"Image-ring audio {intent.ItemIndex} started.", "audio");
                    return;
                }

                if (intent.Kind == ImageRingItemPlaybackIntentKind.Stop &&
                    _activeAudioIndex == intent.ItemIndex)
                {
                    _runtime.StopAudio(false);
                    _activeAudioIndex = -1;
                    Diagnose("IMAGE_RING_AUDIO_STOPPED", $"Image-ring audio {intent.ItemIndex} stopped after focus loss.", "audio");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                try { _runtime.StopAudio(true); }
                catch (Exception stopException) { Debug.LogException(stopException); }
                _activeAudioIndex = -1;
                Diagnose("IMAGE_RING_AUDIO_FAILED", exception.Message, "audio");
            }
        }

        void HandleRuntimeCloseRequest(SessionToken session, int generation)
        {
            if (_disposed || !_isOpen || generation != _generation || session != _session) return;
            Close(session);
        }

        void TryCloseRuntime()
        {
            try { _runtime.Close(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        void ResetPlaybackState()
        {
            _activeAudioIndex = -1;
            _definition = null;
        }

        void Diagnose(string code, string message, string stage)
        {
            _diagnostics?.Invoke(new DiagnosticEvent(
                code,
                message,
                "ImageRing",
                stage,
                DateTimeOffset.UtcNow,
                sessionToken: _session));
        }

        void Publish(ImageRingPhase phase, UserFault fault = default)
        {
            var state = new ImageRingState(_session, ++_version, phase, fault);
            _states.Publish(state);
        }
    }
}
