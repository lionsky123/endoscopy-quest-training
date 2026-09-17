namespace BotanicalGardenQR.Activation.Contracts
{
    /// <summary>
    /// Supplies the current live observation for activation-local recall checks.
    /// Recognition sources remain responsible only for publishing source facts;
    /// this narrow capability lets Activation verify that a recalled spatial
    /// source is still currently tracked without exposing source implementation
    /// details to the flow or frontend.
    /// </summary>
    public interface ILiveRecognitionEvidenceProvider
    {
        bool TryGetLiveObservation(SourceKind kind, string sourceValue, out RecognitionObservation observation);
    }
}
