using System;
using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Effect.Contracts
{
    public interface IEffectController
    {
        EffectResult Open(SessionToken session,EffectDefinition definition);
        EffectResult Dispatch(SessionToken session,EffectIntent intent);
        EffectResult Close(SessionToken session);
        IDisposable Observe(IEffectStateSink sink);
    }
    public interface IEffectStateSink { void Publish(EffectState state); }
}
