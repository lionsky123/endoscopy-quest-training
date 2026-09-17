using System;
namespace BotanicalGardenQR.FrontendShell.Contracts
{
    /// <summary>
    /// Emits the visitor's explicit request to begin a post-close knowledge quiz.
    /// It is an intent source, not evidence that the observation is complete.
    /// </summary>
    public interface IObservationCompletionRequestSource
    {
        event Action ObservationCompletionRequested;
    }
}
