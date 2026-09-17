using System;

namespace BotanicalGardenQR.Activation.Contracts
{
    public interface IRecognitionSource
    {
        SourceKind Kind { get; }
        IDisposable Start(IRecognitionObservationSink sink);
    }

    public interface IRecognitionSourceProvider
    {
        IRecognitionSource CreateSource();
    }

}
