using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.KnowledgeMiniGame.Frontend;
using BotanicalGardenQR.VisitorCoach.Frontend;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using BotanicalGardenQR.VisitorPrologue.Frontend;

namespace BotanicalGardenQR.Bootstrap
{
    /// <summary>Maps live presentation facts and commands to the application modal boundary.</summary>
    internal sealed class VisitorModalPresentationBinding : IVisitorModalEnvironment, IFlowStateSink,
        IVisitorPrologueStateSink, IDisposable
    {
        readonly GlobalFrontendShell _shell;
        readonly StartupRecallPresenter _startup;
        readonly VisitorProloguePresenter _prologueSurface;
        readonly VisitorCoachPresenter _dialogue;
        readonly KnowledgeMiniGameFrontend _completion;
        readonly CollectionWorldFrontend _collection;
        readonly INewRecognitionActivationAvailabilitySink _activation;
        readonly IVisitorPrologue _prologue;
        readonly VisitorToolPreparation _toolPreparation;
        IDisposable _flowSubscription;
        IDisposable _prologueSubscription;
        Action _changed;
        long _revision;
        bool _contentClosed;
        bool _guidanceBlocksFirstScan;
        public void SetGuidanceBlocksFirstScan(bool blocked){if(_guidanceBlocksFirstScan==blocked)return;_guidanceBlocksFirstScan=blocked;Changed();}
        bool _disposed;

        public VisitorModalFacts Current => new VisitorModalFacts(
            _revision, _contentClosed, _prologue.CurrentState?.IsExplorationReady == true,
            _prologueSurface.IsSurfaceVisible, _completion.IsVisible, _startup.IsCloseDecisionVisible,
            _dialogue.CurrentState?.Mode == VisitorDialogueSurfaceMode.Dialogue ? _dialogue.CurrentOwner : null,
            _collection.SurfaceKind, _toolPreparation.HasExited, _guidanceBlocksFirstScan);

        public VisitorModalPresentationBinding(GlobalFrontendShell shell, StartupRecallPresenter startup,
            VisitorProloguePresenter prologueSurface, VisitorCoachPresenter dialogue,
            KnowledgeMiniGameFrontend completion, CollectionWorldFrontend collection,
            INewRecognitionActivationAvailabilitySink activation, IExperienceFlow flow, IVisitorPrologue prologue,
            VisitorToolPreparation toolPreparation)
        {
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _startup = startup ?? throw new ArgumentNullException(nameof(startup));
            _prologueSurface = prologueSurface ?? throw new ArgumentNullException(nameof(prologueSurface));
            _dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
            _completion = completion ?? throw new ArgumentNullException(nameof(completion));
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            _activation = activation ?? throw new ArgumentNullException(nameof(activation));
            _prologue = prologue ?? throw new ArgumentNullException(nameof(prologue));
            _toolPreparation = toolPreparation ?? throw new ArgumentNullException(nameof(toolPreparation));
            _toolPreparation.Changed += Changed;
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            _prologueSurface.SurfaceVisibilityChanged += OnVisibility;
            _completion.SurfaceVisibilityChanged += OnVisibility;
            _startup.CloseDecisionVisibilityChanged += OnVisibility;
            _dialogue.SurfaceStateChanged += OnDialogue;
            _collection.SurfaceChanged += OnCollection;
            try
            {
                _flowSubscription = flow.Observe(this) ?? throw new InvalidOperationException("Flow returned no modal lease.");
                _prologueSubscription = prologue.Observe(this) ?? throw new InvalidOperationException("Prologue returned no modal lease.");
            }
            catch { Dispose(); throw; }
        }

        public IDisposable Observe(Action changed)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisitorModalPresentationBinding));
            if (changed == null) throw new ArgumentNullException(nameof(changed));
            if (_changed != null) throw new InvalidOperationException("There is already a modal policy owner.");
            _changed = changed;
            return new Observation(() => { if (_changed == changed) _changed = null; });
        }

        public void Apply(VisitorModalPolicy policy, long expectedRevision)
        {
            if (_disposed) return;
            if (!policy.RecognitionAllowed) _activation.SetNewRecognitionActivationAllowed(false);
            if (!policy.BrowseOpeningAllowed) _collection.SetBrowseOpeningAllowed(false);
            if (policy.CloseBrowse) _collection.CloseBrowseMode();
            if (_revision != expectedRevision) return;
            _shell.SetApplicationSurfaceSuppressed(policy.ShellSuppressed);
            _startup.SetApplicationSurfaceSuppressed(policy.ShellSuppressed);
            if (_revision != expectedRevision) return;
            if (policy.BrowseOpeningAllowed) _collection.SetBrowseOpeningAllowed(true);
            // Synchronous surface changes invalidate the old snapshot before an enabling command can escape.
            if (_revision == expectedRevision && policy.RecognitionAllowed)
                _activation.SetNewRecognitionActivationAllowed(true);
        }

        public void OnStateChanged(ExperienceFlowState state)
        {
            if (_disposed || state == null) return;
            _contentClosed = state.Page.Kind == FlowPageKind.Closed;
            Changed();
        }
        public void OnVisitorPrologueStateChanged(VisitorPrologueViewState state) => Changed();
        void OnVisibility(bool visible) => Changed();
        void OnDialogue(VisitorDialogueSurfaceState state) => Changed();
        void OnCollection(CollectionPresentationSurfaceKind kind) => Changed();
        void Changed() { if (_disposed) return; _revision++; _changed?.Invoke(); }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_toolPreparation != null) _toolPreparation.Changed -= Changed;
            _prologueSurface.SurfaceVisibilityChanged -= OnVisibility;
            _completion.SurfaceVisibilityChanged -= OnVisibility;
            _startup.CloseDecisionVisibilityChanged -= OnVisibility;
            _dialogue.SurfaceStateChanged -= OnDialogue;
            _collection.SurfaceChanged -= OnCollection;
            _prologueSubscription?.Dispose();
            _flowSubscription?.Dispose();
            _changed = null;
        }

        sealed class Observation : IDisposable
        {
            Action _release;
            public Observation(Action release) => _release = release;
            public void Dispose() { var release = _release; _release = null; release?.Invoke(); }
        }
    }
}
