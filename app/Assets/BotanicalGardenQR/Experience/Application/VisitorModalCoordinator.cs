using System;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;

namespace BotanicalGardenQR.Experience.Application
{
    /// <summary>The sole application owner of modal policy and derived Hub availability.</summary>
    public sealed class VisitorModalCoordinator : IDisposable
    {
        readonly IVisitorModalEnvironment _environment;
        IDisposable _subscription;
        bool _applying;
        bool _dirty;
        bool _disposed;
        public VisitorModalPolicy Current { get; private set; } = VisitorModalPolicy.Inactive;
        public bool CoachCuesSuppressed => Current.CoachCuesSuppressed;
        public event Action<VisitorModalPolicy> PolicyChanged;

        public VisitorModalCoordinator(IVisitorModalEnvironment environment)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            // Subscription can synchronously announce its initial state; defer commands until the lease exists.
            _applying = true;
            try
            {
                _subscription = environment.Observe(Refresh) ??
                    throw new InvalidOperationException("Modal environment returned no observation lease.");
                _applying = false;
                Refresh();
            }
            catch
            {
                _disposed = true;
                _subscription?.Dispose();
                throw;
            }
        }

        void Refresh()
        {
            if (_disposed) return;
            _dirty = true;
            if (_applying) return;
            _applying = true;
            try
            {
                while (_dirty && !_disposed)
                {
                    _dirty = false;
                    var facts = _environment.Current;
                    var next = Resolve(facts);
                    _environment.Apply(next, facts.Revision);
                    if (_disposed) break;
                    if (_dirty || _environment.Current.Revision != facts.Revision)
                    {
                        _dirty = true;
                        continue;
                    }
                    var changed = !next.Equals(Current);
                    Current = next;
                    if (changed) PolicyChanged?.Invoke(next);
                }
            }
            finally { _applying = false; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription?.Dispose();
            _subscription = null;
            Current = VisitorModalPolicy.Inactive;
            PolicyChanged = null;
            _environment.Apply(Current, _environment.Current.Revision);
        }

        public static VisitorModalPolicy Resolve(VisitorModalFacts facts)
        {
            var collectionVisible = facts.CollectionSurface != CollectionPresentationSurfaceKind.Hidden;
            var dialogueVisible = facts.DialogueOwner.HasValue;
            var preparationPending = facts.ExplorationReady && !facts.ToolPreparationExited;
            var shellSuppressed = facts.PrologueVisible || facts.CompletionVisible || collectionVisible || dialogueVisible;
            var surfaceSuppressed = shellSuppressed || facts.CloseDecisionVisible;
            var coachSuppressed = preparationPending || facts.PrologueVisible || facts.CompletionVisible || facts.CloseDecisionVisible ||
                (dialogueVisible && facts.DialogueOwner != VisitorDialogueOwner.Coach) ||
                (collectionVisible && facts.CollectionSurface != CollectionPresentationSurfaceKind.ArtifactOffer);
            var recognitionAllowed = !facts.GuidanceBlocksFirstScan && facts.ExplorationReady && facts.ToolPreparationExited && !facts.PrologueVisible && !facts.CompletionVisible &&
                !facts.CloseDecisionVisible && !dialogueVisible &&
                (facts.CollectionSurface == CollectionPresentationSurfaceKind.Hidden ||
                 facts.CollectionSurface == CollectionPresentationSurfaceKind.Browse);
            return new VisitorModalPolicy(shellSuppressed || preparationPending, coachSuppressed, surfaceSuppressed,
                facts.ExplorationReady && facts.ContentClosed && !surfaceSuppressed, recognitionAllowed,
                facts.ExplorationReady && facts.ContentClosed && !surfaceSuppressed,
                !facts.ContentClosed && facts.CollectionSurface == CollectionPresentationSurfaceKind.Browse);
        }
    }
}
