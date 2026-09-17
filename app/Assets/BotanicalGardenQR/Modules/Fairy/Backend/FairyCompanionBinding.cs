using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Fairy.Contracts;

namespace BotanicalGardenQR.Fairy.Backend
{
    public sealed class FairyCompanionBinding : IDisposable, IFairyMotion, IFairyRecovery
    {
        readonly IFairyController _controller;
        readonly SessionToken _session;
        bool _isOpen;
        bool _requestedVisible;
        bool _isVisible;
        bool _presentationSuppressed;
        bool _disposed;

        public FairyCompanionBinding(
            IFairyController controller,
            FairyDefinition definition,
            bool initiallyVisible = true)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            _session = SessionToken.CreateNew();
            var opened = _controller.Open(_session, definition);
            if (!opened.Succeeded)
                throw new InvalidOperationException(
                    $"Fairy companion failed to open: {opened.FailureCode} ({opened.DiagnosticTag}).");
            _isOpen = true;
            _requestedVisible = initiallyVisible;
            _isVisible = true;
            if (!initiallyVisible)
            {
                var hidden = _controller.Dispatch(_session, FairyIntent.Hide);
                if (!hidden.Succeeded)
                    throw new InvalidOperationException(
                        $"Fairy companion failed to enter Dormant state: {hidden.FailureCode} ({hidden.DiagnosticTag}).");
                _isVisible = false;
            }
        }

        public FairyResult Show(UnityEngine.Vector3? arrivalOrigin = null)
        {
            if (_disposed || !_isOpen)
                return FairyResult.Failure(FairyFailureCode.NotOpen, "fairy.companion.not_open");

            _requestedVisible = true;
            if (_presentationSuppressed || _isVisible)
                return FairyResult.Success();

            var result = _controller.Dispatch(_session, FairyIntent.Show, arrivalOrigin);
            if (result.Succeeded) _isVisible = true;
            return result;
        }

        public FairyResult Hide()
        {
            if (_disposed || !_isOpen)
                return FairyResult.Failure(FairyFailureCode.NotOpen, "fairy.companion.not_open");

            _requestedVisible = false;
            return ApplyVisible(false);
        }

        public FairyResult SetPresentationSuppressed(bool suppressed)
        {
            if (_disposed || !_isOpen)
                return FairyResult.Failure(FairyFailureCode.NotOpen, "fairy.companion.not_open");
            if (_presentationSuppressed == suppressed)
                return FairyResult.Success();

            var result = ApplyVisible(suppressed ? false : _requestedVisible);
            if (result.Succeeded) _presentationSuppressed = suppressed;
            return result;
        }

        public bool IsPresentationVisible => !_disposed && _isOpen && _isVisible && !_presentationSuppressed;

        public FairyResult Speak(FairySpeech speech, Action<FairyResult> completion = null)
        {
            if (_disposed || !_isOpen)
                return FairyResult.Failure(FairyFailureCode.NotOpen, "fairy.companion.not_open");
            if (_presentationSuppressed || !_isVisible)
                return FairyResult.Failure(FairyFailureCode.NotVisible, "fairy.speech.presentation_hidden");
            return _controller.Speak(_session, speech, completion);
        }

        public FairyResult PresentCue(FairyCompanionCue cue)
            => _disposed || !_isOpen
                ? FairyResult.Failure(FairyFailureCode.NotOpen, "fairy.companion.not_open")
                : _controller.PresentCompanionCue(_session, cue);

        public FairyResult CancelSpeech(Guid requestId)
            => _disposed || !_isOpen ? FairyResult.Success() : _controller.CancelSpeech(_session, requestId);

        public FairyResult SetAmbientAudioSuppressed(bool suppressed)
            => _disposed || !_isOpen
                ? FairyResult.Failure(FairyFailureCode.NotOpen, "fairy.companion.not_open")
                : _controller.SetAmbientAudioSuppressed(_session, suppressed);

        public bool TryRecallMotion(UnityEngine.Vector3 position)
            => IsPresentationVisible && _controller is IFairyRecovery recovery && recovery.TryRecallMotion(position);
        public bool TryGetMotionPosition(out UnityEngine.Vector3 position)
        { position=default;return IsPresentationVisible && _controller is IFairyMotion motion && motion.TryGetMotionPosition(out position); }
        public bool ApplyMotion(long requestId,UnityEngine.Vector3 position,UnityEngine.Vector3 forward,bool moving,float entryRadius,float speed)
            => IsPresentationVisible && _controller is IFairyMotion motion && motion.ApplyMotion(requestId,position,forward,moving,entryRadius,speed);
        public void HoldMotion(long requestId){if(_controller is IFairyMotion motion)motion.HoldMotion(requestId);}
        public void ReleaseMotion(long requestId){if(_controller is IFairyMotion motion)motion.ReleaseMotion(requestId);}

        public IDisposable Observe(IFairyStateSink sink) => _controller.Observe(sink);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _requestedVisible = false;
            _isVisible = false;
            _presentationSuppressed = false;
            if (!_isOpen) return;
            _controller.Close(_session);
            _isOpen = false;
        }

        FairyResult ApplyVisible(bool visible)
        {
            if (_isVisible == visible)
                return FairyResult.Success();

            var result = _controller.Dispatch(
                _session,
                visible ? FairyIntent.Show : FairyIntent.Hide);
            if (result.Succeeded) _isVisible = visible;
            return result;
        }
    }
}
