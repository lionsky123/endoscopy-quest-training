namespace BotanicalGardenQR.Activation.Contracts
{
    public interface IRecognitionObservationSink
    {
        void Publish(RecognitionObservation observation);
    }
}
