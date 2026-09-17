using System;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Activation.Contracts
{
    public interface IRecallController
    {
        RecallResult Recall();
        IDisposable Observe(IRecallStateSink sink);
    }

    public interface IRecallStateSink
    {
        void OnStateChanged(RecallState state);
    }

    public readonly struct RecallResult
    {
        RecallResult(bool succeeded, RecallFailure failure, UserFault fault)
        {
            Succeeded = succeeded;
            Failure = failure;
            Fault = fault;
        }

        public bool Succeeded { get; }
        public RecallFailure Failure { get; }
        public UserFault Fault { get; }
        public static RecallResult Success => new RecallResult(true, RecallFailure.None, null);

        public static RecallResult Reject(RecallFailure failure, UserFault fault = null)
        {
            if (failure == RecallFailure.None)
                throw new ArgumentException("A rejected recall requires a failure code.", nameof(failure));
            return new RecallResult(false, failure, fault);
        }
    }

    public enum RecallFailure
    {
        None = 0,
        Unavailable = 1,
        EvidenceRequired = 2,
        PreparationFailed = 3,
        AlreadyActive = 4
    }
}
