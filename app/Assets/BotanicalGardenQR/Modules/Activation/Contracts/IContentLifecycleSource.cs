using System;

namespace BotanicalGardenQR.Activation.Contracts
{
    /// <summary>
    /// Publishes committed content lifecycle facts. Consumers must not infer them from UI state.
    /// </summary>
    public interface IContentLifecycleSource
    {
        IDisposable Observe(IContentLifecycleSink sink);
    }

    public interface IContentLifecycleSink
    {
        void OnContentOpened(ContentOpenedFact fact);
        void OnContentClosed(ContentClosedFact fact);
    }
}
