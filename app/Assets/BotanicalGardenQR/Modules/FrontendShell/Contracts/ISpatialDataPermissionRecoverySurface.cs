using System;
using BotanicalGardenQR.Activation.Contracts;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    /// <summary>
    /// UI-neutral seam for the existing startup/recovery surface. Presentation
    /// chooses copy and layout; callers receive only the retry semantic intent.
    /// </summary>
    public interface ISpatialDataPermissionRecoverySurface :
        ISpatialDataPermissionStateSink
    {
        event Action RetrySpatialPermissionRequested;
    }
}
