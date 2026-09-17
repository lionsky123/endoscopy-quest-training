using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts.Flow;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    public static class FrontendShellModuleFactory
    {
        public static GlobalFrontendShell Configure(
            GlobalFrontendShell shell,
            HeadGazeDwellController headGaze,
            StartupRecallPresenter startupRecall,
            GazeReticlePresenter gazeReticle,
            Transform viewer,
            EventSystem eventSystem,
            IExperienceFlow flow,
            IRecallController recall,
            IScanFeedbackSource scanFeedback,
            PresentationSpec presentation,
            string startupHint)
        {
            if (shell == null) throw new ArgumentNullException(nameof(shell));
            if (headGaze == null) throw new ArgumentNullException(nameof(headGaze));
            if (startupRecall == null) throw new ArgumentNullException(nameof(startupRecall));
            if (gazeReticle == null) throw new ArgumentNullException(nameof(gazeReticle));
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));
            if (eventSystem == null) throw new ArgumentNullException(nameof(eventSystem));
            if (scanFeedback == null) throw new ArgumentNullException(nameof(scanFeedback));
            shell.Configure(flow, presentation, startupHint);
            try
            {
                gazeReticle.Configure(viewer, scanFeedback);
                headGaze.Configure(viewer.GetComponent<Camera>(), eventSystem, gazeReticle);
                // Main has the same lifetime as this input configuration.
                headGaze.RegisterGazeSurface(shell.FrontendRoot, 100, "FrontendShell");
                startupRecall.Configure(viewer, recall, headGaze, startupHint);
            }
            catch
            {
                startupRecall.Unconfigure();
                headGaze.Unconfigure();
                gazeReticle.Unconfigure();
                shell.Dispose();
                throw;
            }
            return shell;
        }
    }
}
