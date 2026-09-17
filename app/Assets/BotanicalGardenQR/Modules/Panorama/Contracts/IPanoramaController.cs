using System;
using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Panorama.Contracts
{
    public interface IPanoramaController
    {
        PanoramaResult Open(SessionToken session, PanoramaDefinition definition, PanoramaSurfaceLease surface);
        PanoramaResult Dispatch(SessionToken session, PanoramaIntent intent);
        PanoramaResult Close(SessionToken session);
        IDisposable Observe(IPanoramaStateSink sink);
    }
    public interface IPanoramaStateSink { void Publish(PanoramaState state); }
}
