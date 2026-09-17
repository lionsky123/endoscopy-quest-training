using System;
using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Fairy.Contracts
{
    public interface IFairyController
    {
        FairyResult Open(SessionToken session,FairyDefinition definition);
        FairyResult Dispatch(SessionToken session, FairyIntent intent, UnityEngine.Vector3? arrivalOrigin = null);
        // Success accepts the request; completion reports its actual terminal result once.
        // A rejected request does not invoke completion.
        FairyResult Speak(SessionToken session, FairySpeech speech, Action<FairyResult> completion = null);
        // Only the matching current request can be cancelled; stale ids are harmless.
        FairyResult CancelSpeech(SessionToken session, Guid requestId);
        FairyResult PresentCompanionCue(SessionToken session, FairyCompanionCue cue);
        FairyResult SetAmbientAudioSuppressed(SessionToken session, bool suppressed);
        FairyResult Close(SessionToken session);
        IDisposable Observe(IFairyStateSink sink);
    }
    public interface IFairyStateSink { void Publish(FairyState state); }
}
