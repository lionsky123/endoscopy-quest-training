using System;
using System.Collections.Generic;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Collection.Runtime
{
    public sealed class CollectionProgressController : ICollectionProgress, IDisposable
    {
        readonly StateChannel<CollectionProgressViewState> _states;
        readonly Dictionary<string, CollectionArtifactState> _artifacts = new Dictionary<string, CollectionArtifactState>(StringComparer.Ordinal);
        readonly HashSet<string> _unlockedMilestones = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _deferredArtifactPresentations = new HashSet<string>(StringComparer.Ordinal);
        CollectionCatalog _catalog;
        JourneySessionId _journeySession;
        CollectionProgressViewState _state;
        CollectionPendingPresentation _pendingPresentation;
        long _version;
        long _nextPresentationId;
        bool _disposed;

        public CollectionProgressController()
        {
            _state = new CollectionProgressViewState(
                0,
                default,
                Array.Empty<CollectionArtifactState>(),
                0,
                0,
                Array.Empty<string>(),
                null);
            _states = StateChannel<CollectionProgressViewState>.ForCurrentThread(_state, state => state.Version);
        }

        public CollectionProgressViewState CurrentState
        {
            get { RequireAvailable(); return _state; }
        }

        public void BeginSession(JourneySessionId journeySession, CollectionCatalog catalog)
        {
            RequireAvailable();
            if (!journeySession.IsValid) throw new ArgumentException("A valid journey session is required.", nameof(journeySession));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (_journeySession == journeySession && _catalog != null) return;

            _journeySession = journeySession;
            _catalog = catalog;
            _artifacts.Clear();
            _unlockedMilestones.Clear();
            _deferredArtifactPresentations.Clear();
            _pendingPresentation = null;
            for (var index = 0; index < catalog.Artifacts.Count; index++)
            {
                var definition = catalog.Artifacts[index];
                _artifacts.Add(
                    definition.ArtifactId,
                    new CollectionArtifactState(definition, CollectionArtifactStatus.Unseen, default));
            }
            Publish();
        }

        public CollectionMutationResult OfferArtifact(ObservationCompletedFact completed, string artifactId)
        {
            RequireAvailable();
            if (completed == null || !_journeySession.IsValid || completed.JourneySession != _journeySession)
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.InvalidSession);
            if (_catalog == null || !_catalog.TryGet(artifactId, out var definition))
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.InvalidArtifact);
            if (!_artifacts.TryGetValue(definition.ArtifactId, out var current))
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.NotAvailable);
            DeferOtherPendingArtifact(definition.ArtifactId);
            if (current.State == CollectionArtifactStatus.Collected)
            {
                Publish();
                return CollectionMutationResult.NoPresentation;
            }

            var token = CollectionInstanceToken.CreateNew();
            _artifacts[definition.ArtifactId] = new CollectionArtifactState(
                definition,
                CollectionArtifactStatus.Available,
                token);
            _deferredArtifactPresentations.Remove(definition.ArtifactId);
            _pendingPresentation = new CollectionPendingPresentation(
                NextPresentationId(),
                CollectionPresentationKind.ArtifactOffered,
                definition.ArtifactId,
                false,
                Array.Empty<string>());
            Publish();
            return CollectionMutationResult.Success(_pendingPresentation.Id);
        }

        public CollectionMutationResult GrantArtifact(JourneySessionId journeySession, string artifactId)
        {
            RequireAvailable();
            if (!_journeySession.IsValid)
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.NoSession);
            if (!journeySession.IsValid || journeySession != _journeySession)
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.InvalidSession);

            var canonicalArtifactId = artifactId?.Trim() ?? string.Empty;
            if (!_artifacts.TryGetValue(canonicalArtifactId, out var current))
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.InvalidArtifact);
            DeferOtherPendingArtifact(canonicalArtifactId);
            if (current.State == CollectionArtifactStatus.Collected)
            {
                Publish();
                return CollectionMutationResult.NoPresentation;
            }

            _artifacts[canonicalArtifactId] = new CollectionArtifactState(
                current.Definition,
                CollectionArtifactStatus.Collected,
                default);
            _deferredArtifactPresentations.Remove(canonicalArtifactId);
            if (_pendingPresentation != null &&
                string.Equals(_pendingPresentation.ArtifactId, canonicalArtifactId, StringComparison.Ordinal) &&
                _pendingPresentation.Kind == CollectionPresentationKind.ArtifactOffered)
                _pendingPresentation = null;

            ResolveNewMilestones();
            if (_pendingPresentation == null)
                TryQueueNextAvailablePresentation(out _);
            Publish();
            return CollectionMutationResult.NoPresentation;
        }

        public CollectionMutationResult CollectArtifact(string artifactId, CollectionInstanceToken instanceToken)
        {
            RequireAvailable();
            if (!_journeySession.IsValid) return CollectionMutationResult.Failure(CollectionMutationFailureCode.NoSession);
            var canonicalArtifactId = artifactId?.Trim() ?? string.Empty;
            if (!_artifacts.TryGetValue(canonicalArtifactId, out var current))
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.InvalidArtifact);
            if (current.State != CollectionArtifactStatus.Available)
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.NotAvailable);
            if (!instanceToken.IsValid || current.InstanceToken != instanceToken)
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.InvalidInstance);

            var collected = new CollectionArtifactState(current.Definition, CollectionArtifactStatus.Collected, default);
            _artifacts[canonicalArtifactId] = collected;
            _deferredArtifactPresentations.Remove(canonicalArtifactId);
            var newlyUnlocked = ResolveNewMilestones();
            var showFullWorld = newlyUnlocked.Count > 0 || IsComplete();
            _pendingPresentation = new CollectionPendingPresentation(
                NextPresentationId(),
                showFullWorld ? CollectionPresentationKind.Reward : CollectionPresentationKind.LocalReturn,
                canonicalArtifactId,
                showFullWorld,
                newlyUnlocked);
            Publish();
            return CollectionMutationResult.Success(_pendingPresentation.Id);
        }

        public CollectionMutationResult DeferAvailableArtifactPresentation(CollectionPresentationId presentationId)
        {
            RequireAvailable();
            if (!presentationId.IsValid || _pendingPresentation == null || _pendingPresentation.Id != presentationId ||
                _pendingPresentation.Kind != CollectionPresentationKind.ArtifactOffered)
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.StalePresentation);

            _deferredArtifactPresentations.Add(_pendingPresentation.ArtifactId);
            _pendingPresentation = null;
            if (TryQueueNextAvailablePresentation(out var nextPresentation))
            {
                Publish();
                return CollectionMutationResult.Success(nextPresentation.Id);
            }

            Publish();
            return CollectionMutationResult.NoPresentation;
        }

        public CollectionMutationResult RequestAvailableArtifactPresentation(string artifactId)
        {
            RequireAvailable();
            if (_pendingPresentation != null)
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.StalePresentation);
            var canonicalArtifactId = artifactId?.Trim() ?? string.Empty;
            if (!_artifacts.TryGetValue(canonicalArtifactId, out var artifact) ||
                artifact.State != CollectionArtifactStatus.Available)
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.NotAvailable);

            _deferredArtifactPresentations.Remove(canonicalArtifactId);
            _pendingPresentation = new CollectionPendingPresentation(
                NextPresentationId(),
                CollectionPresentationKind.ArtifactOffered,
                canonicalArtifactId,
                false,
                Array.Empty<string>());
            Publish();
            return CollectionMutationResult.Success(_pendingPresentation.Id);
        }

        public CollectionMutationResult AcknowledgePresentation(CollectionPresentationId presentationId)
        {
            RequireAvailable();
            if (!presentationId.IsValid || _pendingPresentation == null || _pendingPresentation.Id != presentationId)
                return CollectionMutationResult.Failure(CollectionMutationFailureCode.StalePresentation);
            var acknowledgedKind = _pendingPresentation.Kind;
            _pendingPresentation = null;
            if (acknowledgedKind != CollectionPresentationKind.ArtifactOffered &&
                TryQueueNextAvailablePresentation(out var nextPresentation))
            {
                Publish();
                return CollectionMutationResult.Success(nextPresentation.Id);
            }
            Publish();
            return CollectionMutationResult.NoPresentation;
        }

        public void EndSession(JourneySessionId journeySession)
        {
            RequireAvailable();
            if (!_journeySession.IsValid || journeySession != _journeySession) return;
            _journeySession = default;
            _catalog = null;
            _artifacts.Clear();
            _unlockedMilestones.Clear();
            _deferredArtifactPresentations.Clear();
            _pendingPresentation = null;
            Publish();
        }

        public IDisposable Observe(ICollectionProgressStateSink sink)
        {
            RequireAvailable();
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            return _states.Observe(sink.OnCollectionProgressStateChanged);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _states.Dispose();
            _catalog = null;
            _journeySession = default;
            _artifacts.Clear();
            _unlockedMilestones.Clear();
            _deferredArtifactPresentations.Clear();
        }

        List<string> ResolveNewMilestones()
        {
            var newlyUnlocked = new List<string>();
            if (_catalog == null) return newlyUnlocked;
            var collected = CollectedCount();
            for (var index = 0; index < _catalog.Milestones.Count; index++)
            {
                var milestone = _catalog.Milestones[index];
                if (_unlockedMilestones.Contains(milestone.MilestoneId) || !IsMilestoneSatisfied(milestone, collected)) continue;
                _unlockedMilestones.Add(milestone.MilestoneId);
                newlyUnlocked.Add(milestone.MilestoneId);
            }
            newlyUnlocked.Sort(StringComparer.Ordinal);
            return newlyUnlocked;
        }

        bool IsMilestoneSatisfied(CollectionMilestoneDefinition milestone, int collected)
        {
            switch (milestone.Kind)
            {
                case CollectionMilestoneKind.FirstCollection: return collected >= 1;
                case CollectionMilestoneKind.CollectedCountReached: return collected >= milestone.Threshold;
                case CollectionMilestoneKind.CollectionCompleted: return IsComplete();
                default: return false;
            }
        }

        bool IsComplete() => _catalog != null && _catalog.CountInTotal > 0 && CollectedCount() >= _catalog.CountInTotal;

        int CollectedCount()
        {
            var count = 0;
            foreach (var artifact in _artifacts.Values)
                if (artifact.State == CollectionArtifactStatus.Collected && artifact.Definition.CountInTotal) count++;
            return count;
        }

        void DeferOtherPendingArtifact(string currentArtifactId)
        {
            if (_pendingPresentation == null || _pendingPresentation.ArtifactId == currentArtifactId) return;
            var previousId = _pendingPresentation.ArtifactId;
            if (_artifacts.TryGetValue(previousId, out var previous) && previous.State == CollectionArtifactStatus.Available)
            {
                _deferredArtifactPresentations.Add(previousId);
                _artifacts[previousId] = new CollectionArtifactState(previous.Definition,
                    CollectionArtifactStatus.Available, CollectionInstanceToken.CreateNew());
            }
            _pendingPresentation = null;
        }

        bool TryQueueNextAvailablePresentation(out CollectionPendingPresentation presentation)
        {
            presentation = null;
            if (_catalog == null) return false;
            for (var index = 0; index < _catalog.Artifacts.Count; index++)
            {
                var definition = _catalog.Artifacts[index];
                if (!_artifacts.TryGetValue(definition.ArtifactId, out var artifact) ||
                    artifact.State != CollectionArtifactStatus.Available ||
                    _deferredArtifactPresentations.Contains(definition.ArtifactId))
                    continue;
                presentation = new CollectionPendingPresentation(
                    NextPresentationId(),
                    CollectionPresentationKind.ArtifactOffered,
                    definition.ArtifactId,
                    false,
                    Array.Empty<string>());
                _pendingPresentation = presentation;
                return true;
            }
            return false;
        }

        CollectionPresentationId NextPresentationId() => new CollectionPresentationId(++_nextPresentationId);

        void Publish()
        {
            var artifacts = new List<CollectionArtifactState>(_artifacts.Values);
            artifacts.Sort((left, right) => left.Definition.SortOrder.CompareTo(right.Definition.SortOrder));
            var collected = CollectedCount();
            _state = new CollectionProgressViewState(
                ++_version,
                _journeySession,
                artifacts,
                collected,
                _catalog?.CountInTotal ?? 0,
                new List<string>(_unlockedMilestones),
                _pendingPresentation);
            _states.Publish(_state);
        }

        void RequireAvailable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(CollectionProgressController));
        }
    }

    /// <summary>
    /// Application-owned bridge from semantic Collection Presentation intents
    /// to the progress owner. Keeping this bridge outside Frontend prevents a
    /// concrete UI from calling the backend directly.
    /// </summary>
    public sealed class CollectionProgressIntentSink : ICollectionPresentationIntentSink
    {
        readonly ICollectionProgress _progress;

        public CollectionProgressIntentSink(ICollectionProgress progress)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public void RequestCollectArtifact(string artifactId, CollectionInstanceToken instanceToken)
            => _progress.CollectArtifact(artifactId, instanceToken);

        public void RequestDeferAvailableArtifactPresentation(CollectionPresentationId presentationId)
            => _progress.DeferAvailableArtifactPresentation(presentationId);

        public void RequestPresentAvailableArtifact(string artifactId)
            => _progress.RequestAvailableArtifactPresentation(artifactId);

        public void RequestAcknowledgePresentation(CollectionPresentationId presentationId)
            => _progress.AcknowledgePresentation(presentationId);
    }

    public static class CollectionProgressModuleFactory
    {
        public static CollectionProgressController Create() => new CollectionProgressController();
    }
}
