using System;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    public sealed class ScenePresentationBinding : IFlowStateSink, IDisposable
    {
        readonly PublishedSceneResolver _presentations;
        readonly GlobalFrontendShell _shell;
        readonly IDisposable _subscription;
        readonly IFrontendGazeSurfaceRegistry _surfaces;

        public ScenePresentationBinding(IExperienceFlow flow, PublishedSceneResolver presentations, GlobalFrontendShell shell,
            IFrontendGazeSurfaceRegistry surfaces = null)
        {
            _presentations = presentations ?? throw new ArgumentNullException(nameof(presentations));
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _surfaces = surfaces;
            _subscription = (flow ?? throw new ArgumentNullException(nameof(flow))).Observe(this);
        }

        public void OnStateChanged(ExperienceFlowState state)
        {
            if (state.Page.Kind != FlowPageKind.Closed &&
                _presentations.TryGetPresentation(state.SceneId, out var presentation))
            {
                if (_surfaces != null) _shell.PresentClinicalLesson(state, _presentations, _surfaces);
                _shell.ApplyPresentation(presentation);
            }
        }

        public void Dispose() => _subscription.Dispose();
    }
}
