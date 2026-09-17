using System;
using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Narration.Contracts
{
    public interface INarrationController
    {
        NarrationResult Open(SessionToken session,NarrationDefinition definition,NarrationSurfaceLease surface);
        NarrationResult Dispatch(SessionToken session,NarrationIntent intent);
        NarrationResult Close(SessionToken session);
        IDisposable Observe(INarrationStateSink sink);
    }
    public interface INarrationStateSink { void Publish(NarrationState state); }
}
