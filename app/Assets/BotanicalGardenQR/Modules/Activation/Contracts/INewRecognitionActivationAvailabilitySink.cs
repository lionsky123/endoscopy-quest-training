namespace BotanicalGardenQR.Activation.Contracts
{
    /// <summary>
    /// Application-owned gate for starting a different recognition activation
    /// while a higher-priority visitor decision surface is visible.
    /// Existing active-source tracking remains owned by Activation.
    /// </summary>
    public interface INewRecognitionActivationAvailabilitySink
    {
        void SetNewRecognitionActivationAllowed(bool allowed);
    }
}
