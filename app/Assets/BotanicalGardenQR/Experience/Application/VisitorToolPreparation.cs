using System;

namespace BotanicalGardenQR.Experience.Application
{
    public enum VisitorToolPreparationPhase { Waiting, AwaitingSummon, AwaitingDismiss, ReadyToExplore, Completed }

    /// <summary>Dialogue pauses for real tool use. Departure and skill proof remain separate facts.</summary>
    public sealed class VisitorToolPreparation : IDisposable
    {
        bool _disposed;
        public VisitorToolPreparationPhase Phase { get; private set; }
        public bool IsExplanationOpen { get; private set; }
        public bool HasExited => Phase == VisitorToolPreparationPhase.Completed;
        public bool IsActive => Phase != VisitorToolPreparationPhase.Waiting && !HasExited;
        public bool IsPracticing => !IsExplanationOpen &&
            (Phase == VisitorToolPreparationPhase.AwaitingSummon || Phase == VisitorToolPreparationPhase.AwaitingDismiss);
        public bool HasSummoned { get; private set; }
        public event Action Changed;
        public void CompleteWithoutTools()
        {
            if (_disposed || HasExited) return;
            Phase = VisitorToolPreparationPhase.Completed;
            IsExplanationOpen = false;
            Changed?.Invoke();
        }
        public void Begin()
        {
            if (_disposed || Phase != VisitorToolPreparationPhase.Waiting) return;
            Phase = VisitorToolPreparationPhase.AwaitingSummon;
            IsExplanationOpen = true;
            Changed?.Invoke();
        }
        public void AdvanceExplanation()
        {
            if (_disposed || Phase == VisitorToolPreparationPhase.Waiting) return;
            if (IsExplanationOpen) IsExplanationOpen = false;
            else if (Phase == VisitorToolPreparationPhase.ReadyToExplore) Phase = VisitorToolPreparationPhase.Completed;
            else return;
            Changed?.Invoke();
        }
        public void Replay()
        {
            if (_disposed || Phase == VisitorToolPreparationPhase.Waiting || IsExplanationOpen) return;
            IsExplanationOpen = true;
            Changed?.Invoke();
        }
        public void ReportSummoned()
        {
            if (_disposed || !IsPracticing || Phase != VisitorToolPreparationPhase.AwaitingSummon) return;
            HasSummoned = true;
            Phase = VisitorToolPreparationPhase.AwaitingDismiss;
            Changed?.Invoke();
        }
        public void ReportHidden()
        {
            if (_disposed || Phase != VisitorToolPreparationPhase.AwaitingDismiss) return;
            Phase = VisitorToolPreparationPhase.ReadyToExplore;
            IsExplanationOpen = false;
            Changed?.Invoke();
        }
        public void DeferPractice()
        {
            if (_disposed || !IsPracticing) return;
            Phase = VisitorToolPreparationPhase.ReadyToExplore;
            Changed?.Invoke();
        }
        public void Dispose() { _disposed = true; Changed = null; }
    }
}
