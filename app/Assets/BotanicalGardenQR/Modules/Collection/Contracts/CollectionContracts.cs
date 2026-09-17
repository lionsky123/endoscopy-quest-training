using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Collection.Contracts
{
    public enum CollectionArtifactStatus
    {
        Unseen = 0,
        Available = 1,
        Collected = 2
    }

    public enum CollectionMilestoneKind
    {
        FirstCollection = 0,
        CollectedCountReached = 1,
        CollectionCompleted = 2
    }

    public enum CollectionPresentationKind
    {
        ArtifactOffered = 0,
        Reward = 1,
        LocalReturn = 2
    }

    public enum CollectionPresentationPhase
    {
        Hidden = 0,
        Opening = 1,
        Open = 2,
        Closing = 3
    }

    public readonly struct CollectionInstanceToken : IEquatable<CollectionInstanceToken>
    {
        public CollectionInstanceToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("A canonical collection instance token is required.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static CollectionInstanceToken CreateNew()
            => new CollectionInstanceToken(Guid.NewGuid().ToString("N"));

        public bool Equals(CollectionInstanceToken other)
            => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => obj is CollectionInstanceToken other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(CollectionInstanceToken left, CollectionInstanceToken right) => left.Equals(right);
        public static bool operator !=(CollectionInstanceToken left, CollectionInstanceToken right) => !left.Equals(right);
    }

    public readonly struct CollectionPresentationId : IEquatable<CollectionPresentationId>
    {
        public CollectionPresentationId(long value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }

        public long Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(CollectionPresentationId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CollectionPresentationId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static bool operator ==(CollectionPresentationId left, CollectionPresentationId right) => left.Equals(right);
        public static bool operator !=(CollectionPresentationId left, CollectionPresentationId right) => !left.Equals(right);
    }

    public sealed class CollectionArtifactDefinition
    {
        public CollectionArtifactDefinition(
            string artifactId,
            string visitorTitle,
            string summary,
            string category,
            int sortOrder,
            string slotKey,
            bool countInTotal = true)
        {
            if (string.IsNullOrWhiteSpace(artifactId) || !string.Equals(artifactId, artifactId.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("A canonical ArtifactId is required.", nameof(artifactId));
            if (string.IsNullOrWhiteSpace(visitorTitle))
                throw new ArgumentException("A visitor title is required.", nameof(visitorTitle));
            if (string.IsNullOrWhiteSpace(slotKey) || !string.Equals(slotKey, slotKey.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("A canonical SlotKey is required.", nameof(slotKey));

            ArtifactId = artifactId;
            VisitorTitle = visitorTitle.Trim();
            Summary = summary?.Trim() ?? string.Empty;
            Category = category?.Trim() ?? string.Empty;
            SortOrder = sortOrder;
            SlotKey = slotKey;
            CountInTotal = countInTotal;
        }

        public string ArtifactId { get; }
        public string VisitorTitle { get; }
        public string Summary { get; }
        public string Category { get; }
        public int SortOrder { get; }
        public string SlotKey { get; }
        public bool CountInTotal { get; }
    }

    public sealed class CollectionMilestoneDefinition
    {
        public CollectionMilestoneDefinition(string milestoneId, CollectionMilestoneKind kind, int threshold = 0)
        {
            if (string.IsNullOrWhiteSpace(milestoneId) ||
                !string.Equals(milestoneId, milestoneId.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("A canonical milestone ID is required.", nameof(milestoneId));
            if (!Enum.IsDefined(typeof(CollectionMilestoneKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == CollectionMilestoneKind.CollectedCountReached && threshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(threshold));
            if (kind != CollectionMilestoneKind.CollectedCountReached && threshold != 0)
                throw new ArgumentOutOfRangeException(nameof(threshold));

            MilestoneId = milestoneId;
            Kind = kind;
            Threshold = threshold;
        }

        public string MilestoneId { get; }
        public CollectionMilestoneKind Kind { get; }
        public int Threshold { get; }
    }

    public sealed class CollectionCatalog
    {
        readonly ReadOnlyCollection<CollectionArtifactDefinition> _artifacts;
        readonly ReadOnlyCollection<CollectionMilestoneDefinition> _milestones;
        readonly Dictionary<string, CollectionArtifactDefinition> _byId;

        public CollectionCatalog(
            IReadOnlyList<CollectionArtifactDefinition> artifacts,
            IReadOnlyList<CollectionMilestoneDefinition> milestones = null)
        {
            if (artifacts == null) throw new ArgumentNullException(nameof(artifacts));
            var artifactCopy = new CollectionArtifactDefinition[artifacts.Count];
            _byId = new Dictionary<string, CollectionArtifactDefinition>(StringComparer.Ordinal);
            var slotKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < artifactCopy.Length; index++)
            {
                var artifact = artifacts[index] ?? throw new ArgumentException("Artifact definitions cannot contain null.", nameof(artifacts));
                if (_byId.ContainsKey(artifact.ArtifactId))
                    throw new ArgumentException($"ArtifactId '{artifact.ArtifactId}' is duplicated.", nameof(artifacts));
                _byId.Add(artifact.ArtifactId, artifact);
                if (!slotKeys.Add(artifact.SlotKey))
                    throw new ArgumentException($"SlotKey '{artifact.SlotKey}' is duplicated.", nameof(artifacts));
                artifactCopy[index] = artifact;
            }

            var milestoneCopy = milestones == null
                ? Array.Empty<CollectionMilestoneDefinition>()
                : new CollectionMilestoneDefinition[milestones.Count];
            var milestoneIds = new HashSet<string>(StringComparer.Ordinal);
            var singularKinds = new HashSet<CollectionMilestoneKind>();
            var countInTotal = 0;
            for (var index = 0; index < artifactCopy.Length; index++)
                if (artifactCopy[index].CountInTotal) countInTotal++;
            for (var index = 0; index < milestoneCopy.Length; index++)
            {
                var milestone = milestones[index] ?? throw new ArgumentException("Milestone definitions cannot contain null.", nameof(milestones));
                if (!milestoneIds.Add(milestone.MilestoneId))
                    throw new ArgumentException($"MilestoneId '{milestone.MilestoneId}' is duplicated.", nameof(milestones));
                if (milestone.Kind == CollectionMilestoneKind.CollectedCountReached &&
                    milestone.Threshold > countInTotal)
                    throw new ArgumentException(
                        $"Milestone '{milestone.MilestoneId}' threshold exceeds the collection total.",
                        nameof(milestones));
                if (milestone.Kind != CollectionMilestoneKind.CollectedCountReached &&
                    !singularKinds.Add(milestone.Kind))
                    throw new ArgumentException(
                        $"Milestone kind '{milestone.Kind}' may be configured only once.",
                        nameof(milestones));
                milestoneCopy[index] = milestone;
            }

            _artifacts = Array.AsReadOnly(artifactCopy);
            _milestones = Array.AsReadOnly(milestoneCopy);
        }

        public IReadOnlyList<CollectionArtifactDefinition> Artifacts => _artifacts;
        public IReadOnlyList<CollectionMilestoneDefinition> Milestones => _milestones;
        public int TotalCount => CountInTotal;
        public int CountInTotal
        {
            get
            {
                var count = 0;
                for (var index = 0; index < _artifacts.Count; index++)
                    if (_artifacts[index].CountInTotal) count++;
                return count;
            }
        }

        public bool TryGet(string artifactId, out CollectionArtifactDefinition artifact)
        {
            artifact = null;
            return !string.IsNullOrWhiteSpace(artifactId) && _byId.TryGetValue(artifactId.Trim(), out artifact);
        }
    }

    public sealed class CollectionArtifactState
    {
        public CollectionArtifactState(
            CollectionArtifactDefinition definition,
            CollectionArtifactStatus state,
            CollectionInstanceToken instanceToken)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            State = state;
            InstanceToken = instanceToken;
        }

        public CollectionArtifactDefinition Definition { get; }
        public CollectionArtifactStatus State { get; }
        public CollectionInstanceToken InstanceToken { get; }
    }

    /// <summary>
    /// Read-only compendium projection for one artifact. This is deliberately
    /// derived from CollectionArtifactState; it is not a second collection
    /// store and cannot be mutated by the presentation layer.
    /// </summary>
    public sealed class CollectionEntryViewState
    {
        public CollectionEntryViewState(CollectionArtifactState artifact)
        {
            Artifact = artifact ?? throw new ArgumentNullException(nameof(artifact));
        }

        public CollectionArtifactState Artifact { get; }
        public CollectionArtifactDefinition Definition => Artifact.Definition;
        public string ArtifactId => Definition.ArtifactId;
        public string VisitorTitle => Definition.VisitorTitle;
        public string Summary => Definition.Summary;
        public string Category => Definition.Category;
        public int SortOrder => Definition.SortOrder;
        public string SlotKey => Definition.SlotKey;
        public CollectionArtifactStatus Status => Artifact.State;
        public bool IsCollected => Status == CollectionArtifactStatus.Collected;
        public bool IsAvailable => Status == CollectionArtifactStatus.Available;
    }

    /// <summary>
    /// Builds presentation projections from the single progress snapshot. The
    /// returned list is a fresh read-only view and carries no independent
    /// status, total, ordering or unlock facts.
    /// </summary>
    public static class CollectionEntryViewResolver
    {
        public static IReadOnlyList<CollectionEntryViewState> Resolve(CollectionProgressViewState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var entries = new CollectionEntryViewState[state.Artifacts.Count];
            for (var index = 0; index < entries.Length; index++)
                entries[index] = new CollectionEntryViewState(state.Artifacts[index]);
            return Array.AsReadOnly(entries);
        }
    }

    public sealed class CollectionPendingPresentation
    {
        public CollectionPendingPresentation(
            CollectionPresentationId id,
            CollectionPresentationKind kind,
            string artifactId,
            bool showFullWorld,
            IReadOnlyList<string> unlockedMilestones)
        {
            Id = id;
            Kind = kind;
            ArtifactId = artifactId ?? string.Empty;
            ShowFullWorld = showFullWorld;
            var copy = unlockedMilestones == null ? Array.Empty<string>() : new string[unlockedMilestones.Count];
            if (unlockedMilestones != null)
                for (var index = 0; index < copy.Length; index++) copy[index] = unlockedMilestones[index] ?? string.Empty;
            UnlockedMilestones = Array.AsReadOnly(copy);
        }

        public CollectionPresentationId Id { get; }
        public CollectionPresentationKind Kind { get; }
        public string ArtifactId { get; }
        public bool ShowFullWorld { get; }
        public IReadOnlyList<string> UnlockedMilestones { get; }
    }

    public sealed class CollectionProgressViewState
    {
        public CollectionProgressViewState(
            long version,
            JourneySessionId journeySession,
            IReadOnlyList<CollectionArtifactState> artifacts,
            int collectedCount,
            int totalCount,
            IReadOnlyList<string> unlockedMilestones,
            CollectionPendingPresentation pendingPresentation)
        {
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (artifacts == null) throw new ArgumentNullException(nameof(artifacts));
            if (collectedCount < 0 || totalCount < 0 || collectedCount > totalCount)
                throw new ArgumentOutOfRangeException(nameof(collectedCount));
            Version = version;
            JourneySession = journeySession;
            Artifacts = Copy(artifacts);
            CollectedCount = collectedCount;
            TotalCount = totalCount;
            UnlockedMilestones = CopyStrings(unlockedMilestones);
            PendingPresentation = pendingPresentation;
        }

        public long Version { get; }
        public JourneySessionId JourneySession { get; }
        public IReadOnlyList<CollectionArtifactState> Artifacts { get; }
        public int CollectedCount { get; }
        public int TotalCount { get; }
        public bool IsComplete => TotalCount > 0 && CollectedCount >= TotalCount;
        public IReadOnlyList<string> UnlockedMilestones { get; }
        public CollectionPendingPresentation PendingPresentation { get; }

        static IReadOnlyList<CollectionArtifactState> Copy(IReadOnlyList<CollectionArtifactState> values)
        {
            var copy = new CollectionArtifactState[values.Count];
            for (var index = 0; index < copy.Length; index++) copy[index] = values[index] ?? throw new ArgumentException("Artifact state cannot be null.", nameof(values));
            return Array.AsReadOnly(copy);
        }

        static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> values)
        {
            var copy = values == null ? Array.Empty<string>() : new string[values.Count];
            if (values != null)
                for (var index = 0; index < copy.Length; index++) copy[index] = values[index] ?? string.Empty;
            return Array.AsReadOnly(copy);
        }
    }

    public enum CollectionMutationFailureCode
    {
        None = 0,
        InvalidSession = 1,
        InvalidArtifact = 2,
        InvalidInstance = 3,
        NotAvailable = 4,
        NoSession = 5,
        Disposed = 6,
        StalePresentation = 7
    }

    public readonly struct CollectionMutationResult
    {
        CollectionMutationResult(bool succeeded, CollectionMutationFailureCode failureCode, CollectionPresentationId presentationId)
        {
            Succeeded = succeeded;
            FailureCode = failureCode;
            PresentationId = presentationId;
        }

        public bool Succeeded { get; }
        public CollectionMutationFailureCode FailureCode { get; }
        public CollectionPresentationId PresentationId { get; }
        public static CollectionMutationResult Success(CollectionPresentationId id)
            => new CollectionMutationResult(true, CollectionMutationFailureCode.None, id);
        public static CollectionMutationResult NoPresentation
            => new CollectionMutationResult(true, CollectionMutationFailureCode.None, default);
        public static CollectionMutationResult Failure(CollectionMutationFailureCode code)
        {
            if (code == CollectionMutationFailureCode.None) throw new ArgumentOutOfRangeException(nameof(code));
            return new CollectionMutationResult(false, code, default);
        }
    }

    public interface ICollectionProgress
    {
        CollectionProgressViewState CurrentState { get; }
        void BeginSession(JourneySessionId journeySession, CollectionCatalog catalog);
        CollectionMutationResult OfferArtifact(ObservationCompletedFact completed, string artifactId);
        CollectionMutationResult GrantArtifact(JourneySessionId journeySession, string artifactId);
        CollectionMutationResult CollectArtifact(string artifactId, CollectionInstanceToken instanceToken);
        CollectionMutationResult DeferAvailableArtifactPresentation(CollectionPresentationId presentationId);
        CollectionMutationResult RequestAvailableArtifactPresentation(string artifactId);
        CollectionMutationResult AcknowledgePresentation(CollectionPresentationId presentationId);
        void EndSession(JourneySessionId journeySession);
        IDisposable Observe(ICollectionProgressStateSink sink);
    }

    public interface ICollectionProgressStateSink
    {
        void OnCollectionProgressStateChanged(CollectionProgressViewState state);
    }

    /// <summary>
    /// Semantic intents emitted by Collection Presentation. Platform input
    /// (hand, gaze, pointer or controller) is translated before it reaches
    /// this boundary; the presentation never invokes the progress owner
    /// directly.
    /// </summary>
    public interface ICollectionPresentationIntentSink
    {
        void RequestCollectArtifact(string artifactId, CollectionInstanceToken instanceToken);
        void RequestDeferAvailableArtifactPresentation(CollectionPresentationId presentationId);
        void RequestPresentAvailableArtifact(string artifactId);
        void RequestAcknowledgePresentation(CollectionPresentationId presentationId);
    }

    public interface ICollectionBrowseMode
    {
        bool CanOpenBrowseMode { get; }
        void OpenBrowseMode();
        void CloseBrowseMode();
    }

    public interface ICollectionCatalogSource
    {
        bool TryGetCollectionCatalog(out CollectionCatalog catalog);
        bool TryGetArtifactId(SceneId sceneId, out string artifactId);
    }
}
