using System;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.ImageRing.Contracts
{
    public interface IImageRingController : IDisposable
    {
        ImageRingResult Open(SessionToken session, ImageRingDefinition definition);
        ImageRingResult Close(SessionToken session);
        IDisposable Observe(IImageRingStateSink sink);
    }

    public interface IImageRingStateSink
    {
        void Publish(ImageRingState state);
    }
}
