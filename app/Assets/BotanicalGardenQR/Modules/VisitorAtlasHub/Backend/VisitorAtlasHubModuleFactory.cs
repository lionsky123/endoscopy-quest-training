using System;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.VisitorAtlasHub.Backend
{
    public static class VisitorAtlasHubModuleFactory
    {
        public static IVisitorAtlasHubController Create(
            IVisitorAtlasHubPresentation presentation,
            Transform viewer,
            Transform interactionRigRoot,
            IMapNavigation mapNavigation,
            Action<DiagnosticEvent> diagnostics = null)
        {
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));
            if (interactionRigRoot == null) throw new ArgumentNullException(nameof(interactionRigRoot));

            var controller = new VisitorAtlasHubController(
                presentation,
                new GltfVisitorAtlasHubMapLoader(),
                diagnostics, mapNavigation);
            try
            {
                controller.Configure(viewer, interactionRigRoot);
                return controller;
            }
            catch (Exception exception)
            {
                controller.Dispose();
                // Configuration faults disable only this optional visitor-global
                // surface. The rest of QR, Journey, Collection and Fairy startup
                // must remain available, while editor contract tests still expose
                // the authored fault before a build is handed off.
                try { presentation.Dispose(); }
                catch (Exception disposeException)
                {
                    Debug.LogWarning(
                        $"[VisitorAtlasHub] Presentation cleanup after configuration failure also failed: {disposeException.Message}");
                }
                diagnostics?.Invoke(new DiagnosticEvent(
                    "ATLAS_CONFIGURATION_FAILED",
                    $"Visitor Atlas Hub configuration failed: {exception.GetType().Name}: {exception.Message}",
                    "VisitorAtlasHub",
                    VisitorAtlasHubFailureStage.Configuration.ToString(),
                    DateTimeOffset.UtcNow));
                Debug.LogWarning(
                    $"[VisitorAtlasHub] Disabled for this visitor session after configuration failure: {exception.Message}");
                return new FailedVisitorAtlasHubController();
            }
        }

        sealed class FailedVisitorAtlasHubController : IVisitorAtlasHubController
        {
            public VisitorAtlasHubPhase Phase => VisitorAtlasHubPhase.Failed;
            public bool MapRequested => false;
            public bool IsMapVisible => false;
            public void SetMapSuppressed(bool suppressed) { }
            public VisitorAtlasHubBookState BookState => VisitorAtlasHubBookState.ClosedInteractive;
            public bool InteractionGateOpen => false;
            public bool IsVisible => false;
            public event Action<VisitorAtlasHubPhase> PhaseChanged { add { } remove { } }
            public event Action<VisitorAtlasHubPalmStage> PalmStageChanged { add { } remove { } }
            public event Action Summoned { add { } remove { } }
            public event Action BookSelected { add { } remove { } }
            public event Action<int> OpenCollectionRequested { add { } remove { } }
            public event Action Hidden { add { } remove { } }
            public event Action<VisitorAtlasHubFailure> Failed { add { } remove { } }
            public void SetInteractionGate(bool open) { }
            public void Tick(float unscaledDeltaSeconds) { }
            public bool TryBeginBookOpening(out int generation)
            {
                generation = 0;
                return false;
            }
            public void ConfirmCollectionOpened(int generation) { }
            public void CancelBookOpening(int generation = 0) { }
            public void NotifyCollectionClosed() { }
            public void RejectBookSelection() { }
            public void Hide() { }
            public void Dispose() { }
        }
    }
}
