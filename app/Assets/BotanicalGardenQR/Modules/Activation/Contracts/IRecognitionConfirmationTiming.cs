namespace BotanicalGardenQR.Activation.Contracts
{
    // The application owns confirmation timing. A recognition source applies
    // it to its source-specific focus session without learning content state.
    public interface IRecognitionConfirmationTiming
    {
        void ConfigureConfirmationTiming(
            float confirmationSeconds,
            float lostGraceSeconds,
            float gazeLostGraceSeconds);
    }
}
