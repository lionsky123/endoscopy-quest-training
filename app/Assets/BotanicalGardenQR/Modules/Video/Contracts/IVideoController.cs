using System;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Video.Contracts
{
    public interface IVideoController
    {
        VideoResult Open(SessionToken session, VideoDefinition definition, VideoSurfaceLease surface);
        VideoResult Dispatch(SessionToken session, VideoIntent intent);
        VideoResult Close(SessionToken session);
        IDisposable Observe(IVideoStateSink sink);
    }

    public interface IVideoStateSink { void Publish(VideoState state); }
}
