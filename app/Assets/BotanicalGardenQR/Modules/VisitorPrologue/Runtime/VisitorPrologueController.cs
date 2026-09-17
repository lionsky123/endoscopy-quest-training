using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.VisitorPrologue.Contracts;

namespace BotanicalGardenQR.VisitorPrologue.Runtime
{
    public sealed class VisitorPrologueController : IVisitorPrologue
    {
        readonly StateChannel<VisitorPrologueViewState> _states;
        VisitorPrologueViewState _state = new VisitorPrologueViewState(0, 0, VisitorProloguePhase.Boot, false, false);
        long _version, _epoch;
        bool _disposed, _arrivalReady, _bookOpened;
        public VisitorPrologueController()
        { _states = StateChannel<VisitorPrologueViewState>.ForCurrentThread(_state, state => state.Version); }
        public VisitorPrologueViewState CurrentState { get { RequireAvailable(); return _state; } }
        public VisitorPrologueResult Begin()
        {
            RequireAvailable();
            if (_state.Phase != VisitorProloguePhase.Boot && _state.Phase != VisitorProloguePhase.Failed)
                return VisitorPrologueResult.Success;
            _epoch++;
            _arrivalReady = _bookOpened = false;
            Publish(VisitorProloguePhase.Invitation, gazeFallback: false, page: 0, count: 0,
                open: false, reply: VisitorEncounterReply.None);
            return VisitorPrologueResult.Success;
        }
        public VisitorPrologueResult Retry()
        {
            RequireAvailable();
            return _state.Phase == VisitorProloguePhase.Failed ? Begin() : Failure(VisitorPrologueFailureCode.InvalidPhase);
        }
        public VisitorPrologueResult ReportHandAvailability(bool hasReliableHand)
        {
            RequireAvailable();
            if (_state.HasReliableHand != hasReliableHand) Publish(_state.Phase, reliableHand: hasReliableHand);
            return VisitorPrologueResult.Success;
        }
        public VisitorPrologueResult EnableGazeFallback()
        {
            RequireAvailable();
            if (!_state.GazeFallbackAvailable) Publish(_state.Phase, gazeFallback: true);
            return VisitorPrologueResult.Success;
        }
        public VisitorPrologueResult RequestInvitation(long epoch, VisitorPrologueInputModality modality)
        {
            RequireAvailable();
            if (epoch != _epoch) return Failure(VisitorPrologueFailureCode.StaleEpoch);
            if (_state.Phase != VisitorProloguePhase.Invitation) return Failure(VisitorPrologueFailureCode.InvalidPhase);
            if (modality == VisitorPrologueInputModality.PalmHold && !_state.CanInviteWithPalm)
                return Failure(VisitorPrologueFailureCode.HandUnavailable);
            if (modality == VisitorPrologueInputModality.GazeFallback && !_state.CanInviteWithGaze)
                return Failure(VisitorPrologueFailureCode.GazeFallbackUnavailable);
            if (!Enum.IsDefined(typeof(VisitorPrologueInputModality), modality)) return Failure(VisitorPrologueFailureCode.InvalidPhase);
            Publish(VisitorProloguePhase.Arrival);
            return VisitorPrologueResult.Success;
        }
        public VisitorPrologueResult ReportArrivalReady(long epoch) => ReportReady(epoch, true);
        public VisitorPrologueResult ReportBookOpened(long epoch) => ReportReady(epoch, false);
        VisitorPrologueResult ReportReady(long epoch, bool arrival)
        {
            RequireAvailable();
            if (epoch != _epoch) return Failure(VisitorPrologueFailureCode.StaleEpoch);
            if (_state.Phase == VisitorProloguePhase.Encounter) return VisitorPrologueResult.Success;
            if (_state.Phase != VisitorProloguePhase.Arrival) return Failure(VisitorPrologueFailureCode.InvalidPhase);
            if (arrival) _arrivalReady = true; else _bookOpened = true;
            if (_arrivalReady && _bookOpened) Publish(VisitorProloguePhase.Encounter);
            return VisitorPrologueResult.Success;
        }
        public VisitorPrologueResult BeginDialogue(long epoch, int pageCount)
        {
            RequireAvailable();
            if (epoch != _epoch) return Failure(VisitorPrologueFailureCode.StaleEpoch);
            if (_state.Phase != VisitorProloguePhase.Encounter || pageCount != 4)
                return Failure(VisitorPrologueFailureCode.InvalidDialogue);
            if (!_state.HasDialogue) Publish(_state.Phase, page: 0, count: pageCount, open: true);
            return VisitorPrologueResult.Success;
        }
        public VisitorPrologueResult AdvanceDialogue(long epoch, long revision)
        {
            var result = ValidateDialogue(epoch, revision);
            if (!result.Succeeded) return result;
            if (_state.IsReplyPage) return Failure(VisitorPrologueFailureCode.InvalidReply);
            if (_state.DialoguePageIndex + 1 == _state.DialoguePageCount)
                Publish(VisitorProloguePhase.ExplorationIdle, page: 0, count: 0, open: false);
            else Publish(_state.Phase, page: _state.DialoguePageIndex + 1);
            return VisitorPrologueResult.Success;
        }
        public VisitorPrologueResult ChooseReply(long epoch, long revision, VisitorEncounterReply reply)
        {
            var result = ValidateDialogue(epoch, revision);
            if (!result.Succeeded) return result;
            if (!_state.IsReplyPage || reply == VisitorEncounterReply.None || !Enum.IsDefined(typeof(VisitorEncounterReply), reply))
                return Failure(VisitorPrologueFailureCode.InvalidReply);
            Publish(_state.Phase, page: 2, reply: reply);
            return VisitorPrologueResult.Success;
        }
        public VisitorPrologueResult ReplayDialogue(long epoch)
        {
            RequireAvailable();
            if (epoch != _epoch) return Failure(VisitorPrologueFailureCode.StaleEpoch);
            if (_state.Phase != VisitorProloguePhase.Encounter || !_state.HasDialogue)
                return Failure(VisitorPrologueFailureCode.InvalidDialogue);
            Publish(_state.Phase, page: 0, reply: VisitorEncounterReply.None);
            return VisitorPrologueResult.Success;
        }
        VisitorPrologueResult ValidateDialogue(long epoch, long revision)
        {
            RequireAvailable();
            if (epoch != _epoch) return Failure(VisitorPrologueFailureCode.StaleEpoch);
            if (revision != _state.Version) return Failure(VisitorPrologueFailureCode.StaleRevision);
            if (_state.Phase != VisitorProloguePhase.Encounter || !_state.IsDialogueOpen)
                return Failure(VisitorPrologueFailureCode.InvalidDialogue);
            return VisitorPrologueResult.Success;
        }
        public VisitorPrologueResult Fail(string visitorMessage)
        {
            RequireAvailable();
            if (_state.IsExplorationReady) return Failure(VisitorPrologueFailureCode.InvalidPhase);
            Publish(VisitorProloguePhase.Failed, fault: string.IsNullOrWhiteSpace(visitorMessage)
                ? "联系暂时中断，请重新回应邀请。" : visitorMessage, page: 0, count: 0, open: false);
            return VisitorPrologueResult.Success;
        }
        void Publish(VisitorProloguePhase phase, bool? reliableHand = null, bool? gazeFallback = null,
            string fault = null, int? page = null, int? count = null, bool? open = null, VisitorEncounterReply? reply = null)
        {
            _state = new VisitorPrologueViewState(++_version, _epoch, phase,
                reliableHand ?? _state.HasReliableHand, gazeFallback ?? _state.GazeFallbackAvailable,
                fault ?? (phase == VisitorProloguePhase.Failed ? _state.Fault : null), page ?? _state.DialoguePageIndex, count ?? _state.DialoguePageCount,
                open ?? _state.IsDialogueOpen, reply ?? _state.Reply);
            _states.Publish(_state);
        }
        public IDisposable Observe(IVisitorPrologueStateSink sink)
        {
            RequireAvailable();
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            return _states.Observe(sink.OnVisitorPrologueStateChanged);
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _states.Dispose(); }
        void RequireAvailable() { if (_disposed) throw new ObjectDisposedException(nameof(VisitorPrologueController)); }
        static VisitorPrologueResult Failure(VisitorPrologueFailureCode code) => VisitorPrologueResult.Failure(code);
    }
    public static class VisitorPrologueModuleFactory
    { public static VisitorPrologueController Create() => new VisitorPrologueController(); }
}
