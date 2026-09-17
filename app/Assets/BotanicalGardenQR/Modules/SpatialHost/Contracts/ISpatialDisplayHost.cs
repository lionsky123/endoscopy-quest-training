using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.SpatialHost.Contracts
{
    public interface ISpatialDisplayHost
    {
        HostPrepareResult Prepare(
            SessionToken candidate,
            DisplayProfile profile,
            SpatialEvidence? spatialEvidence);

        HostResult Close(SessionToken session);

        HostResult UpdateSourceTracking(
            SessionToken session,
            TrackingState trackingState,
            DateTimeOffset observedAt);

        HostSourcePolicyResult Tick(SessionToken session, DateTimeOffset now);
    }

    public enum HostSourcePolicyAction
    {
        None = 0,
        Hidden = 1,
        CloseRequested = 2
    }

    public readonly struct HostSourcePolicyResult
    {
        HostSourcePolicyResult(HostSourcePolicyAction action, HostFailure failure)
        { Action = action; Failure = failure; }
        public HostSourcePolicyAction Action { get; }
        public HostFailure Failure { get; }
        public bool Succeeded => Failure == HostFailure.None;
        public static HostSourcePolicyResult NoChange => new HostSourcePolicyResult(HostSourcePolicyAction.None, HostFailure.None);
        public static HostSourcePolicyResult Changed(HostSourcePolicyAction action) => new HostSourcePolicyResult(action, HostFailure.None);
        public static HostSourcePolicyResult Reject(HostFailure failure) => new HostSourcePolicyResult(HostSourcePolicyAction.None, failure);
    }

    public interface IPreparedHostLease : IDisposable
    {
        SessionToken Session { get; }
        void Commit();
    }

    public readonly struct HostPrepareResult
    {
        HostPrepareResult(IPreparedHostLease lease, HostFailure failure, UserFault fault)
        {
            Lease = lease;
            Failure = failure;
            Fault = fault;
        }

        public bool Succeeded => Lease != null;
        public IPreparedHostLease Lease { get; }
        public HostFailure Failure { get; }
        public UserFault Fault { get; }

        public static HostPrepareResult Success(IPreparedHostLease lease)
            => new HostPrepareResult(lease ?? throw new ArgumentNullException(nameof(lease)), HostFailure.None, null);

        public static HostPrepareResult Reject(HostFailure failure, UserFault fault = null)
        {
            if (failure == HostFailure.None)
                throw new ArgumentException("A rejected host preparation requires a failure code.", nameof(failure));
            return new HostPrepareResult(null, failure, fault);
        }
    }

    public readonly struct HostResult
    {
        HostResult(bool succeeded, HostFailure failure)
        {
            Succeeded = succeeded;
            Failure = failure;
        }

        public bool Succeeded { get; }
        public HostFailure Failure { get; }
        public static HostResult Success => new HostResult(true, HostFailure.None);

        public static HostResult Reject(HostFailure failure)
        {
            if (failure == HostFailure.None)
                throw new ArgumentException("A rejected host result requires a failure code.", nameof(failure));
            return new HostResult(false, failure);
        }
    }

    public enum HostFailure
    {
        None = 0,
        InvalidProfile = 1,
        SpatialEvidenceRequired = 2,
        SpatialEvidenceInvalid = 3,
        EnvironmentUnavailable = 4,
        StaleSession = 5
    }
}
