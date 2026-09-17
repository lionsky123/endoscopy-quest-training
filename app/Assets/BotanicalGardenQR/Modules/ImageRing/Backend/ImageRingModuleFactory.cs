using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.ImageRing.Contracts;

namespace BotanicalGardenQR.ImageRing.Backend
{
    public static class ImageRingModuleFactory
    {
        public static IImageRingController Create(
            IImageRingRuntime runtime,
            Action<DiagnosticEvent> diagnostics = null)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            return new ImageRingController(runtime, diagnostics);
        }
    }
}
