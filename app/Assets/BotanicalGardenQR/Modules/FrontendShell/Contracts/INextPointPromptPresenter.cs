namespace BotanicalGardenQR.FrontendShell.Contracts
{
    /// <summary>
    /// Renders source-neutral Journey status feedback.
    /// It never resolves routes or decides which point is next.
    /// </summary>
    public interface INextPointPromptPresenter
    {
        void ShowJourneyMessage(string message);
        void ShowJourneyStatus(string message);
        void SetClosedContentContext(string message);
        void ClearJourneyPrompt();
    }
}
