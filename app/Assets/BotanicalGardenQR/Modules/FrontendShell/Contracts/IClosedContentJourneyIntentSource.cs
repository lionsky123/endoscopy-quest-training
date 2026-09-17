using System;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    /// <summary>
    /// Emits a visitor's explicit request to skip the quiz for the just-closed content
    /// and directly collect its configured artifact. The application layer retains
    /// content identity and decides whether normal Journey continuation also applies.
    /// </summary>
    public interface IClosedContentJourneyIntentSource
    {
        event Action SkipQuizAndCollectRequested;
    }
}
