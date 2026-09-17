using System;

namespace BotanicalGardenQR.Experience.Contracts.Flow
{
    public interface IExperienceFlow
    {
        FlowPrepareResult Prepare(SessionToken candidate, SceneId sceneId);
        FlowResult EnterFeature(SessionToken session, FeaturePageId page);
        FlowResult BackToMain(SessionToken session);
        FlowResult Close(SessionToken session);
        IDisposable Observe(IFlowStateSink sink);
    }

    public interface IMainSurfaceLifecycle
    {
        void BeforeFeatureEnter(SessionToken session);
    }

    public interface IPreparedFlowLease : IDisposable
    {
        SessionToken Session { get; }
        void Commit();
    }

    public readonly struct FlowPrepareResult
    {
        FlowPrepareResult(IPreparedFlowLease lease, UserFault fault)
        {
            Lease = lease;
            Fault = fault;
        }

        public bool Succeeded => Lease != null;
        public IPreparedFlowLease Lease { get; }
        public UserFault Fault { get; }

        public static FlowPrepareResult Success(IPreparedFlowLease lease)
            => new FlowPrepareResult(lease ?? throw new ArgumentNullException(nameof(lease)), null);

        public static FlowPrepareResult Failure(UserFault fault)
            => new FlowPrepareResult(null, fault ?? throw new ArgumentNullException(nameof(fault)));
    }

    public readonly struct FlowResult
    {
        FlowResult(bool succeeded, FlowFailure failure, UserFault fault)
        {
            Succeeded = succeeded;
            Failure = failure;
            Fault = fault;
        }

        public bool Succeeded { get; }
        public FlowFailure Failure { get; }
        public UserFault Fault { get; }
        public static FlowResult Success => new FlowResult(true, FlowFailure.None, null);
        public static FlowResult Reject(FlowFailure failure, UserFault fault = null)
        {
            if (failure == FlowFailure.None)
                throw new ArgumentException("A rejected result requires a failure code.", nameof(failure));
            return new FlowResult(false, failure, fault);
        }
    }

    public enum FlowFailure
    {
        None = 0,
        StaleSession = 1,
        InvalidTransition = 2,
        PageUnavailable = 3,
        PreparationFailed = 4,
        LifecycleFailed = 5
    }
}
