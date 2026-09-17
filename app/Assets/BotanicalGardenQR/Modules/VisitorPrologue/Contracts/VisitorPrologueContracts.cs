using System;

namespace BotanicalGardenQR.VisitorPrologue.Contracts
{
    public enum VisitorProloguePhase { Boot, Invitation, Arrival, Encounter, ExplorationIdle, Failed }
    public enum VisitorPrologueInputModality { PalmHold, GazeFallback }
    public enum VisitorEncounterReply { None, AskAboutWorld, PromiseToExplore }

    public sealed class VisitorPrologueViewState
    {
        public VisitorPrologueViewState(long version, long epoch, VisitorProloguePhase phase,
            bool hasReliableHand, bool gazeFallbackAvailable, string fault = null,
            int dialoguePageIndex = 0, int dialoguePageCount = 0, bool isDialogueOpen = false,
            VisitorEncounterReply reply = VisitorEncounterReply.None)
        {
            if (version < 0 || epoch < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (!Enum.IsDefined(typeof(VisitorProloguePhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            if (!Enum.IsDefined(typeof(VisitorEncounterReply), reply)) throw new ArgumentOutOfRangeException(nameof(reply));
            if (dialoguePageCount < 0 || dialoguePageIndex < 0 ||
                (dialoguePageCount == 0 ? dialoguePageIndex != 0 || isDialogueOpen : dialoguePageIndex >= dialoguePageCount))
                throw new ArgumentException("Encounter page is outside its authored range.");
            Version = version; Epoch = epoch; Phase = phase;
            HasReliableHand = hasReliableHand; GazeFallbackAvailable = gazeFallbackAvailable;
            Fault = fault?.Trim() ?? string.Empty;
            DialoguePageIndex = dialoguePageIndex; DialoguePageCount = dialoguePageCount;
            IsDialogueOpen = isDialogueOpen; Reply = reply;
        }
        public long Version { get; }
        public long Epoch { get; }
        public VisitorProloguePhase Phase { get; }
        public bool HasReliableHand { get; }
        public bool GazeFallbackAvailable { get; }
        public string Fault { get; }
        public int DialoguePageIndex { get; }
        public int DialoguePageCount { get; }
        public bool IsDialogueOpen { get; }
        public VisitorEncounterReply Reply { get; }
        public bool HasDialogue => DialoguePageCount > 0;
        public bool IsReplyPage => IsDialogueOpen && DialoguePageIndex == 1;
        public bool IsExplorationReady => Phase == VisitorProloguePhase.ExplorationIdle;
        public bool CanInviteWithPalm => Phase == VisitorProloguePhase.Invitation && HasReliableHand;
        public bool CanInviteWithGaze => Phase == VisitorProloguePhase.Invitation && !HasReliableHand && GazeFallbackAvailable;
    }

    public enum VisitorPrologueFailureCode
    { None, InvalidPhase, HandUnavailable, GazeFallbackUnavailable, StaleEpoch, InvalidDialogue, InvalidReply, StaleRevision }
    public readonly struct VisitorPrologueResult
    {
        VisitorPrologueResult(bool succeeded, VisitorPrologueFailureCode failureCode)
        { Succeeded = succeeded; FailureCode = failureCode; }
        public bool Succeeded { get; }
        public VisitorPrologueFailureCode FailureCode { get; }
        public static VisitorPrologueResult Success => new VisitorPrologueResult(true, VisitorPrologueFailureCode.None);
        public static VisitorPrologueResult Failure(VisitorPrologueFailureCode code)
        {
            if (code == VisitorPrologueFailureCode.None) throw new ArgumentOutOfRangeException(nameof(code));
            return new VisitorPrologueResult(false, code);
        }
    }
    public interface IVisitorPrologue : IDisposable
    {
        VisitorPrologueViewState CurrentState { get; }
        VisitorPrologueResult Begin();
        VisitorPrologueResult ReportHandAvailability(bool hasReliableHand);
        VisitorPrologueResult EnableGazeFallback();
        VisitorPrologueResult RequestInvitation(long epoch, VisitorPrologueInputModality modality);
        VisitorPrologueResult ReportArrivalReady(long epoch);
        VisitorPrologueResult ReportBookOpened(long epoch);
        VisitorPrologueResult BeginDialogue(long epoch, int pageCount);
        VisitorPrologueResult AdvanceDialogue(long epoch, long revision);
        VisitorPrologueResult ChooseReply(long epoch, long revision, VisitorEncounterReply reply);
        VisitorPrologueResult ReplayDialogue(long epoch);
        VisitorPrologueResult Fail(string visitorMessage);
        VisitorPrologueResult Retry();
        IDisposable Observe(IVisitorPrologueStateSink sink);
    }
    public interface IVisitorPrologueStateSink
    { void OnVisitorPrologueStateChanged(VisitorPrologueViewState state); }
}
