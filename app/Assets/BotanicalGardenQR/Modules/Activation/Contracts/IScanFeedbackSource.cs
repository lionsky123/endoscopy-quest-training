using System;

namespace BotanicalGardenQR.Activation.Contracts
{
    // Recognition owns candidate timing. Consumers receive only the visual
    // confirmation state, never source identity, payload, or spatial evidence.
    public interface IScanFeedbackSource
    {
        IDisposable ObserveScan(IScanFeedbackSink sink);
    }

    public interface IScanFeedbackSink
    {
        void OnScanFeedbackChanged(ScanFeedbackState state);
    }
}
