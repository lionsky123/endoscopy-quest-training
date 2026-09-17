using System;
using System.Collections.Generic;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.VisitorCoach.Contracts;

namespace BotanicalGardenQR.VisitorCoach.Runtime
{
    public sealed class VisitorCoachController : IVisitorCoach
    {
        const int CapabilityCount = 7;

        readonly VisitorCoachTiming _timing;
        readonly VisitorCoachMastery[] _mastery = new VisitorCoachMastery[CapabilityCount];
        readonly Dictionary<VisitorCoachOpportunityId, OpportunityRecord> _opportunities =
            new Dictionary<VisitorCoachOpportunityId, OpportunityRecord>();
        readonly StateChannel<VisitorCoachViewState> _states;

        VisitorCoachViewState _state;
        VisitorCoachSessionId _session;
        long _version;
        long _opportunityOrder;
        bool _cuesSuppressed = true;
        bool _disposed;

        public VisitorCoachController(VisitorCoachTiming timing)
        {
            _timing = timing;
            _state = CreateViewState(0, null, false);
            _states = StateChannel<VisitorCoachViewState>.ForCurrentThread(_state, value => value.Version);
        }

        public VisitorCoachViewState CurrentState
        {
            get { RequireAvailable(); return _state; }
        }

        public VisitorCoachResult BeginSession(VisitorCoachSessionId session)
        {
            RequireAvailable();
            if (!session.IsValid) return VisitorCoachResult.Failure(VisitorCoachFailureCode.InvalidSession);
            if (_session == session) return VisitorCoachResult.Success;

            _session = session;
            Array.Clear(_mastery, 0, _mastery.Length);
            _opportunities.Clear();
            _opportunityOrder = 0;
            _cuesSuppressed = true;
            PublishCurrent(force: true);
            return VisitorCoachResult.Success;
        }

        public VisitorCoachResult EndSession(VisitorCoachSessionId session)
        {
            RequireAvailable();
            var sessionResult = ValidateSession(session);
            if (!sessionResult.Succeeded) return sessionResult;

            _session = default;
            Array.Clear(_mastery, 0, _mastery.Length);
            _opportunities.Clear();
            _opportunityOrder = 0;
            _cuesSuppressed = true;
            PublishCurrent(force: true);
            return VisitorCoachResult.Success;
        }

        public VisitorCoachResult OfferOpportunity(VisitorCoachOpportunity opportunity)
        {
            RequireAvailable();
            if (opportunity == null)
                return VisitorCoachResult.Failure(VisitorCoachFailureCode.InvalidOpportunity);
            var sessionResult = ValidateSession(opportunity.Session);
            if (!sessionResult.Succeeded) return sessionResult;
            if (_mastery[(int)opportunity.Capability] == VisitorCoachMastery.Proven)
                return VisitorCoachResult.Success;

            if (_opportunities.TryGetValue(opportunity.Id, out var existing))
            {
                if (!existing.Matches(opportunity))
                    return VisitorCoachResult.Failure(VisitorCoachFailureCode.InvalidOpportunity);
                return VisitorCoachResult.Success;
            }

            _opportunities.Add(opportunity.Id, new OpportunityRecord(opportunity, ++_opportunityOrder));
            if (_mastery[(int)opportunity.Capability] == VisitorCoachMastery.Unknown)
                _mastery[(int)opportunity.Capability] = VisitorCoachMastery.Prompted;
            PublishCurrent(force: true);
            return VisitorCoachResult.Success;
        }

        public VisitorCoachResult WithdrawOpportunity(
            VisitorCoachSessionId session,
            VisitorCoachOpportunityId opportunityId)
        {
            RequireAvailable();
            var sessionResult = ValidateSession(session);
            if (!sessionResult.Succeeded) return sessionResult;
            if (!opportunityId.IsValid)
                return VisitorCoachResult.Failure(VisitorCoachFailureCode.InvalidOpportunity);
            if (_opportunities.Remove(opportunityId)) PublishCurrent(force: true);
            return VisitorCoachResult.Success;
        }

        public VisitorCoachResult ReportProven(
            VisitorCoachSessionId session,
            VisitorCoachCapability capability)
        {
            RequireAvailable();
            var sessionResult = ValidateSession(session);
            if (!sessionResult.Succeeded) return sessionResult;
            if (!Enum.IsDefined(typeof(VisitorCoachCapability), capability))
                return VisitorCoachResult.Failure(VisitorCoachFailureCode.InvalidOpportunity);
            if (_mastery[(int)capability] == VisitorCoachMastery.Proven)
                return VisitorCoachResult.Success;

            _mastery[(int)capability] = VisitorCoachMastery.Proven;
            var removals = new List<VisitorCoachOpportunityId>();
            foreach (var pair in _opportunities)
                if (pair.Value.Opportunity.Capability == capability)
                    removals.Add(pair.Key);
            for (var index = 0; index < removals.Count; index++) _opportunities.Remove(removals[index]);
            PublishCurrent(force: true);
            return VisitorCoachResult.Success;
        }

        public VisitorCoachResult DismissCue(
            VisitorCoachSessionId session,
            VisitorCoachOpportunityId opportunityId)
        {
            RequireAvailable();
            var sessionResult = ValidateSession(session);
            if (!sessionResult.Succeeded) return sessionResult;
            if (!opportunityId.IsValid)
                return VisitorCoachResult.Failure(VisitorCoachFailureCode.InvalidOpportunity);
            if (_opportunities.TryGetValue(opportunityId, out var opportunity) && !opportunity.Dismissed)
            {
                opportunity.Dismissed = true;
                PublishCurrent(force: true);
            }
            return VisitorCoachResult.Success;
        }

        public VisitorCoachResult AdvanceDialogue(
            VisitorCoachSessionId session,
            VisitorCoachOpportunityId opportunityId)
        {
            RequireAvailable();
            var sessionResult = ValidateSession(session);
            if (!sessionResult.Succeeded) return sessionResult;
            if (!TryGetCurrentDialogue(opportunityId, out var opportunity) || !opportunity.DialogueOpen)
                return VisitorCoachResult.Failure(VisitorCoachFailureCode.InvalidOpportunity);

            if (opportunity.DialoguePageIndex + 1 < opportunity.Opportunity.DialoguePageCount)
                opportunity.DialoguePageIndex++;
            else
                opportunity.DialogueOpen = false;
            PublishCurrent(force: true);
            return VisitorCoachResult.Success;
        }

        public VisitorCoachResult ReplayDialogue(
            VisitorCoachSessionId session,
            VisitorCoachOpportunityId opportunityId)
        {
            RequireAvailable();
            var sessionResult = ValidateSession(session);
            if (!sessionResult.Succeeded) return sessionResult;
            if (!TryGetCurrentDialogue(opportunityId, out var opportunity))
                return VisitorCoachResult.Failure(VisitorCoachFailureCode.InvalidOpportunity);

            opportunity.DialoguePageIndex = 0;
            opportunity.DialogueOpen = true;
            PublishCurrent(force: true);
            return VisitorCoachResult.Success;
        }

        public VisitorCoachResult Advance(
            VisitorCoachSessionId session,
            float deltaSeconds,
            bool cuesSuppressed)
        {
            RequireAvailable();
            var sessionResult = ValidateSession(session);
            if (!sessionResult.Succeeded) return sessionResult;
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
                return VisitorCoachResult.Failure(VisitorCoachFailureCode.InvalidTime);

            if (_cuesSuppressed != cuesSuppressed)
            {
                _cuesSuppressed = cuesSuppressed;
                PublishCurrent(force: true);
            }
            if (_cuesSuppressed) return VisitorCoachResult.Success;

            var active = SelectOpportunity();
            if (active == null || deltaSeconds <= 0f) return VisitorCoachResult.Success;
            var before = HintLevel(active.ElapsedSeconds);
            active.ElapsedSeconds += deltaSeconds;
            if (before != HintLevel(active.ElapsedSeconds)) PublishCurrent(force: true);
            return VisitorCoachResult.Success;
        }

        public IDisposable Observe(IVisitorCoachStateSink sink)
        {
            RequireAvailable();
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            return _states.Observe(sink.OnVisitorCoachStateChanged);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _opportunities.Clear();
            _states.Dispose();
        }

        VisitorCoachResult ValidateSession(VisitorCoachSessionId session)
        {
            if (!session.IsValid) return VisitorCoachResult.Failure(VisitorCoachFailureCode.InvalidSession);
            return _session.IsValid && session == _session
                ? VisitorCoachResult.Success
                : VisitorCoachResult.Failure(VisitorCoachFailureCode.StaleSession);
        }

        OpportunityRecord SelectOpportunity()
        {
            OpportunityRecord selected = null;
            foreach (var pair in _opportunities)
            {
                var candidate = pair.Value;
                if (candidate.Dismissed ||
                    _mastery[(int)candidate.Opportunity.Capability] == VisitorCoachMastery.Proven)
                    continue;
                if (selected == null ||
                    candidate.Opportunity.Priority > selected.Opportunity.Priority ||
                    (candidate.Opportunity.Priority == selected.Opportunity.Priority &&
                     candidate.Order < selected.Order))
                    selected = candidate;
            }
            return selected;
        }

        bool TryGetCurrentDialogue(
            VisitorCoachOpportunityId opportunityId,
            out OpportunityRecord opportunity)
        {
            opportunity = null;
            if (_cuesSuppressed || !opportunityId.IsValid) return false;
            var selected = SelectOpportunity();
            if (selected == null || selected.Opportunity.Id != opportunityId ||
                selected.Opportunity.DialoguePageCount <= 0)
                return false;
            opportunity = selected;
            return true;
        }

        VisitorCoachHintLevel HintLevel(float elapsedSeconds)
        {
            if (elapsedSeconds >= _timing.RecoverySeconds) return VisitorCoachHintLevel.Recovery;
            if (elapsedSeconds >= _timing.DemonstrationSeconds) return VisitorCoachHintLevel.Demonstration;
            if (elapsedSeconds >= _timing.DirectSeconds) return VisitorCoachHintLevel.Direct;
            return VisitorCoachHintLevel.Initial;
        }

        void PublishCurrent(bool force)
        {
            var active = SelectOpportunity();
            var visible = active != null && !_cuesSuppressed;
            var candidate = CreateViewState(_version + 1, active, visible);
            if (!force && Equivalent(_state, candidate)) return;
            _version++;
            _state = CreateViewState(_version, active, visible);
            _states.Publish(_state);
        }

        VisitorCoachViewState CreateViewState(long version, OpportunityRecord active, bool visible)
        {
            var capabilityStates = new VisitorCoachCapabilityState[CapabilityCount];
            for (var index = 0; index < capabilityStates.Length; index++)
                capabilityStates[index] = new VisitorCoachCapabilityState(
                    (VisitorCoachCapability)index,
                    _mastery[index]);

            return new VisitorCoachViewState(
                version,
                _session,
                capabilityStates,
                visible,
                active?.Opportunity.Id ?? default,
                active?.Opportunity.Capability ?? default,
                active != null ? HintLevel(active.ElapsedSeconds) : VisitorCoachHintLevel.None,
                active?.Opportunity.CueKey ?? string.Empty,
                active?.DialoguePageIndex ?? 0,
                active?.Opportunity.DialoguePageCount ?? 0,
                active != null && active.DialogueOpen);
        }

        static bool Equivalent(VisitorCoachViewState left, VisitorCoachViewState right)
        {
            if (left == null || right == null ||
                left.Session != right.Session ||
                left.IsCueVisible != right.IsCueVisible ||
                left.OpportunityId != right.OpportunityId ||
                left.Capability != right.Capability ||
                left.HintLevel != right.HintLevel ||
                !string.Equals(left.CueKey, right.CueKey, StringComparison.Ordinal) ||
                left.DialoguePageIndex != right.DialoguePageIndex ||
                left.DialoguePageCount != right.DialoguePageCount ||
                left.IsDialogueOpen != right.IsDialogueOpen ||
                left.Capabilities.Count != right.Capabilities.Count)
                return false;
            for (var index = 0; index < left.Capabilities.Count; index++)
                if (left.Capabilities[index].Capability != right.Capabilities[index].Capability ||
                    left.Capabilities[index].Mastery != right.Capabilities[index].Mastery)
                    return false;
            return true;
        }

        void RequireAvailable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisitorCoachController));
        }

        sealed class OpportunityRecord
        {
            public OpportunityRecord(VisitorCoachOpportunity opportunity, long order)
            {
                Opportunity = opportunity;
                Order = order;
                DialogueOpen = opportunity.DialoguePageCount > 0;
            }

            public VisitorCoachOpportunity Opportunity { get; }
            public long Order { get; }
            public float ElapsedSeconds { get; set; }
            public bool Dismissed { get; set; }
            public int DialoguePageIndex { get; set; }
            public bool DialogueOpen { get; set; }

            public bool Matches(VisitorCoachOpportunity opportunity) =>
                opportunity != null &&
                Opportunity.Session == opportunity.Session &&
                Opportunity.Id == opportunity.Id &&
                Opportunity.Capability == opportunity.Capability &&
                Opportunity.Priority == opportunity.Priority &&
                Opportunity.DialoguePageCount == opportunity.DialoguePageCount &&
                string.Equals(Opportunity.CueKey, opportunity.CueKey, StringComparison.Ordinal);
        }
    }

    public static class VisitorCoachModuleFactory
    {
        public static VisitorCoachController Create(VisitorCoachTiming timing) =>
            new VisitorCoachController(timing);
    }
}
