namespace BotanicalGardenQR.Activation.Contracts
{
    /// <summary>
    /// Optional recognition-source capability used after an explicit visitor
    /// close. The source starts a fresh confirmation round on its next tick;
    /// it must not publish synchronously from this call.
    /// </summary>
    public interface IRecognitionRoundRearm
    {
        bool TryRearm(RecognitionObservation observation);
    }
}
