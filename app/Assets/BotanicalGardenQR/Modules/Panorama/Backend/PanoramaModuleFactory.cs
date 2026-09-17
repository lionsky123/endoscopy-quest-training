using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Panorama.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Panorama.Backend
{
    public static class PanoramaModuleFactory
    {
        public static IPanoramaController Create(Transform runtimeRoot, Transform viewer, Action<DiagnosticEvent> diagnostics = null)
        {
            if (runtimeRoot == null) throw new ArgumentNullException(nameof(runtimeRoot));
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));
            return new PanoramaController(runtimeRoot, viewer, diagnostics);
        }
    }
}
