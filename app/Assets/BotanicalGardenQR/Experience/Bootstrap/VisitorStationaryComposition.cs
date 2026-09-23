using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Fairy.Backend;
using BotanicalGardenQR.VisitorCoach.Frontend;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    public sealed partial class VisitorRuntimeComposition
    {
        // Shares the authored encounter and hand interaction, not the archived
        // course flow, scene resolver, QR activation, collection or walking map.
        static VisitorRuntimeComposition CreateStationary(VisitorRuntimeBindings bindings,
            Action<DiagnosticEvent> diagnostics, FullScriptRoomVisit visit)
        {
            var ownership = new BootstrapOwnershipScope();
            try
            {
                var config = bindings.Configuration;
                var platform = bindings.Platform;
                var ui = bindings.Presentation;
                var viewer = platform.Viewer;
                var input = ui.HeadGaze;
                input.Configure(viewer.GetComponent<Camera>(), platform.EventSystem);
                input.SetHandOnly(true);
                ownership.Register(input.Unconfigure);
                ui.GazeReticle.SetPresentationEnabled(false);

                var parent = ui.DisplayRoot ? ui.DisplayRoot.parent : null;
                var dialogueObject = InstantiateConfiguredPresentation(config.VisitorCoachTheme.PresentationPrefab,
                    parent, "VisitorCoachPresentation");
                ownership.Register(() => Destroy(dialogueObject));
                var dialogue = RequiredComponent<VisitorCoachPresenter>(dialogueObject, "Stationary dialogue");
                ownership.Register(dialogue.Dispose);
                dialogue.Configure(viewer, config.VisitorCoachTheme, config.UiDefaults.SharedFont, input);

                if (!config.FairyDefinitions.TryGet(out var definition) || definition == null)
                    throw new InvalidOperationException("The current script requires its authored guide.");
                var roots = bindings.RuntimeRoots;
                ownership.Register(() => DestroyCreatedRuntime(roots.Fairy, "FairyModuleRuntime"));
                var fairyController = FairyModuleFactory.Create(roots.Fairy, viewer, platform.FairyGroundReference,
                    platform.FairyArrivalPassthroughLayer, platform.FairyArrivalEnvironmentLight, diagnostics,
                    null, visit.Room.GuidePath, bypassArrival: true);
                var fairy = FairyModuleFactory.BindAsCompanion(fairyController, definition, initiallyVisible: false);
                ownership.Register(fairy.Dispose);
                fairy.SetAmbientAudioSuppressed(true);
                var dialogueFairy = new VisitorDialogueFairyBinding(dialogue, fairy);
                ownership.Register(dialogueFairy.Dispose);
                var guide = new FullScriptGuideBinding(dialogue, visit);
                ownership.Register(guide.Dispose);

                var composition = new VisitorRuntimeComposition(ownership, null, null,
                    dt => { dialogue.Tick(dt); visit.TickStationary(dt); }, () => visit.InputAllowed);
                composition._begin = () =>
                {
                    var forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up).normalized;
                    if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
                    fairy.Show(viewer.position + forward * .85f - Vector3.up * .4f);
                    if (!guide.TryPresent()) visit.BeginStationary();
                };
                return composition;
            }
            catch { ownership.Dispose(); throw; }
        }

    }
}
