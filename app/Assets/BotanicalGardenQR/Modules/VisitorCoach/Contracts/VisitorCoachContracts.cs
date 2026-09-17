using System;
using System.Collections.Generic;

namespace BotanicalGardenQR.VisitorCoach.Contracts
{
    public static class VisitorCoachCueKeys
    {
        public const string QrConfirm = "entry.qr.confirm";
        public const string ArtifactGrab = "collection.artifact.grab";
        public const string ArtifactPlace = "collection.artifact.place";
        public const string ArtifactPlaceRetry = "collection.artifact.place-retry";
        public const string PalmRecall = "atlas-hub.palm.summon";
        public const string PanoramaEntry = "panorama.entry";
    }

    public enum VisitorCoachCapability
    {
        Poke = 0,
        GazeDwell = 1,
        QrConfirm = 2,
        ArtifactGrab = 3,
        ArtifactPlace = 4,
        PalmRecall = 5,
        PanoramaExit = 6
    }

    public enum VisitorCoachMastery
    {
        Unknown = 0,
        Prompted = 1,
        Proven = 2
    }

    public enum VisitorCoachHintLevel
    {
        None = 0,
        Initial = 1,
        Direct = 2,
        Demonstration = 3,
        Recovery = 4
    }

    public readonly struct VisitorCoachSessionId : IEquatable<VisitorCoachSessionId>
    {
        public VisitorCoachSessionId(string value) => Value = value?.Trim() ?? string.Empty;
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public static VisitorCoachSessionId CreateNew() => new VisitorCoachSessionId(Guid.NewGuid().ToString("N"));
        public bool Equals(VisitorCoachSessionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is VisitorCoachSessionId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public static bool operator ==(VisitorCoachSessionId left, VisitorCoachSessionId right) => left.Equals(right);
        public static bool operator !=(VisitorCoachSessionId left, VisitorCoachSessionId right) => !left.Equals(right);
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct VisitorCoachOpportunityId : IEquatable<VisitorCoachOpportunityId>
    {
        public VisitorCoachOpportunityId(string value) => Value = value?.Trim() ?? string.Empty;
        public string Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public bool Equals(VisitorCoachOpportunityId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is VisitorCoachOpportunityId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public static bool operator ==(VisitorCoachOpportunityId left, VisitorCoachOpportunityId right) => left.Equals(right);
        public static bool operator !=(VisitorCoachOpportunityId left, VisitorCoachOpportunityId right) => !left.Equals(right);
        public override string ToString() => Value ?? string.Empty;
    }

    public sealed class VisitorCoachOpportunity
    {
        public VisitorCoachOpportunity(
            VisitorCoachSessionId session,
            VisitorCoachOpportunityId id,
            VisitorCoachCapability capability,
            string cueKey,
            int priority,
            int dialoguePageCount = 0)
        {
            if (!session.IsValid) throw new ArgumentException("A valid Coach session is required.", nameof(session));
            if (!id.IsValid) throw new ArgumentException("A stable opportunity id is required.", nameof(id));
            if (!Enum.IsDefined(typeof(VisitorCoachCapability), capability))
                throw new ArgumentOutOfRangeException(nameof(capability));
            if (string.IsNullOrWhiteSpace(cueKey)) throw new ArgumentException("A cue key is required.", nameof(cueKey));
            if (dialoguePageCount < 0)
                throw new ArgumentOutOfRangeException(nameof(dialoguePageCount));

            Session = session;
            Id = id;
            Capability = capability;
            CueKey = cueKey.Trim();
            Priority = priority;
            DialoguePageCount = dialoguePageCount;
        }

        public VisitorCoachSessionId Session { get; }
        public VisitorCoachOpportunityId Id { get; }
        public VisitorCoachCapability Capability { get; }
        public string CueKey { get; }
        public int Priority { get; }
        public int DialoguePageCount { get; }
    }

    public readonly struct VisitorCoachTiming
    {
        public VisitorCoachTiming(float directSeconds, float demonstrationSeconds, float recoverySeconds)
        {
            if (!IsPositiveFinite(directSeconds) ||
                !IsPositiveFinite(demonstrationSeconds) ||
                !IsPositiveFinite(recoverySeconds) ||
                directSeconds >= demonstrationSeconds ||
                demonstrationSeconds >= recoverySeconds)
                throw new ArgumentException("Coach timing must be finite, positive, and strictly increasing.");
            DirectSeconds = directSeconds;
            DemonstrationSeconds = demonstrationSeconds;
            RecoverySeconds = recoverySeconds;
        }

        public float DirectSeconds { get; }
        public float DemonstrationSeconds { get; }
        public float RecoverySeconds { get; }
        public static VisitorCoachTiming Default => new VisitorCoachTiming(4f, 8f, 12f);

        static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class VisitorCoachCapabilityState
    {
        public VisitorCoachCapabilityState(VisitorCoachCapability capability, VisitorCoachMastery mastery)
        {
            Capability = capability;
            Mastery = mastery;
        }

        public VisitorCoachCapability Capability { get; }
        public VisitorCoachMastery Mastery { get; }
    }

    public sealed class VisitorCoachViewState
    {
        readonly VisitorCoachCapabilityState[] _capabilities;

        public VisitorCoachViewState(
            long version,
            VisitorCoachSessionId session,
            IReadOnlyList<VisitorCoachCapabilityState> capabilities,
            bool isCueVisible,
            VisitorCoachOpportunityId opportunityId,
            VisitorCoachCapability capability,
            VisitorCoachHintLevel hintLevel,
            string cueKey,
            int dialoguePageIndex = 0,
            int dialoguePageCount = 0,
            bool isDialogueOpen = false)
        {
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (dialoguePageCount < 0) throw new ArgumentOutOfRangeException(nameof(dialoguePageCount));
            if (dialoguePageCount == 0 && (dialoguePageIndex != 0 || isDialogueOpen))
                throw new ArgumentException("An empty Coach dialogue cannot have a page or be open.");
            if (dialoguePageCount > 0 && (dialoguePageIndex < 0 || dialoguePageIndex >= dialoguePageCount))
                throw new ArgumentOutOfRangeException(nameof(dialoguePageIndex));
            Version = version;
            Session = session;
            _capabilities = CopyCapabilities(capabilities);
            IsCueVisible = isCueVisible;
            OpportunityId = opportunityId;
            Capability = capability;
            HintLevel = hintLevel;
            CueKey = cueKey?.Trim() ?? string.Empty;
            DialoguePageIndex = dialoguePageIndex;
            DialoguePageCount = dialoguePageCount;
            IsDialogueOpen = isDialogueOpen;
        }

        public long Version { get; }
        public VisitorCoachSessionId Session { get; }
        public IReadOnlyList<VisitorCoachCapabilityState> Capabilities => _capabilities;
        public bool IsCueVisible { get; }
        public VisitorCoachOpportunityId OpportunityId { get; }
        public VisitorCoachCapability Capability { get; }
        public VisitorCoachHintLevel HintLevel { get; }
        public string CueKey { get; }
        public int DialoguePageIndex { get; }
        public int DialoguePageCount { get; }
        public bool IsDialogueOpen { get; }
        public bool HasDialogue => DialoguePageCount > 0;

        public VisitorCoachMastery GetMastery(VisitorCoachCapability capability)
        {
            for (var index = 0; index < _capabilities.Length; index++)
                if (_capabilities[index].Capability == capability)
                    return _capabilities[index].Mastery;
            return VisitorCoachMastery.Unknown;
        }

        static VisitorCoachCapabilityState[] CopyCapabilities(IReadOnlyList<VisitorCoachCapabilityState> capabilities)
        {
            if (capabilities == null) return Array.Empty<VisitorCoachCapabilityState>();
            var copy = new VisitorCoachCapabilityState[capabilities.Count];
            for (var index = 0; index < copy.Length; index++) copy[index] = capabilities[index];
            return copy;
        }
    }

    public enum VisitorCoachFailureCode
    {
        None = 0,
        InvalidSession = 1,
        StaleSession = 2,
        InvalidOpportunity = 3,
        InvalidTime = 4
    }

    public readonly struct VisitorCoachResult
    {
        VisitorCoachResult(bool succeeded, VisitorCoachFailureCode failureCode)
        {
            Succeeded = succeeded;
            FailureCode = failureCode;
        }

        public bool Succeeded { get; }
        public VisitorCoachFailureCode FailureCode { get; }
        public static VisitorCoachResult Success => new VisitorCoachResult(true, VisitorCoachFailureCode.None);
        public static VisitorCoachResult Failure(VisitorCoachFailureCode code) =>
            new VisitorCoachResult(false, code);
    }

    public interface IVisitorCoach : IDisposable
    {
        VisitorCoachViewState CurrentState { get; }
        VisitorCoachResult BeginSession(VisitorCoachSessionId session);
        VisitorCoachResult EndSession(VisitorCoachSessionId session);
        VisitorCoachResult OfferOpportunity(VisitorCoachOpportunity opportunity);
        VisitorCoachResult WithdrawOpportunity(VisitorCoachSessionId session, VisitorCoachOpportunityId opportunityId);
        VisitorCoachResult ReportProven(VisitorCoachSessionId session, VisitorCoachCapability capability);
        VisitorCoachResult DismissCue(VisitorCoachSessionId session, VisitorCoachOpportunityId opportunityId);
        VisitorCoachResult AdvanceDialogue(VisitorCoachSessionId session, VisitorCoachOpportunityId opportunityId);
        VisitorCoachResult ReplayDialogue(VisitorCoachSessionId session, VisitorCoachOpportunityId opportunityId);
        VisitorCoachResult Advance(VisitorCoachSessionId session, float deltaSeconds, bool cuesSuppressed);
        IDisposable Observe(IVisitorCoachStateSink sink);
    }

    public interface IVisitorCoachStateSink
    {
        void OnVisitorCoachStateChanged(VisitorCoachViewState state);
    }
}
