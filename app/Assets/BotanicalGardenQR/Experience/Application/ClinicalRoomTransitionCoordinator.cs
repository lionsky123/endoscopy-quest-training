using System;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Experience.Application
{
    public enum ClinicalRoomTransitionPhase
    {
        Active,
        Loading,
        Failed
    }

    public enum ClinicalRoomTransitionFailure
    {
        None,
        AlreadyLoading,
        RecoveryRequired,
        NoTransitionInProgress,
        StaleRequest,
        RoomLoadFailed,
        CommitRejected,
        JourneyRejected
    }

    /// <summary>
    /// The small seam between the in-memory journey and a Unity room adapter.
    /// The adapter receives a request, performs fade-out, old-room disposal,
    /// target load/validation, entry alignment and activation, then calls
    /// Complete. A failed load must restore the previous room before calling
    /// Complete with roomLoaded=false.
    /// </summary>
    public readonly struct ClinicalRoomTransitionRequest
    {
        public ClinicalRoomTransitionRequest(ClinicalJourneyTransitionTicket journeyTicket)
        {
            JourneyTicket = journeyTicket;
        }

        public ClinicalJourneyTransitionTicket JourneyTicket { get; }
        public string FromRoomId => JourneyTicket.FromRoomId;
        public string TargetRoomId => JourneyTicket.TargetRoomId;
        public bool AdvancesMainline => JourneyTicket.AdvancesMainline;
        public bool IsValid => JourneyTicket.IsValid;
    }

    public readonly struct ClinicalRoomTransitionStartResult
    {
        ClinicalRoomTransitionStartResult(bool accepted, ClinicalRoomTransitionFailure failure,
            ClinicalJourneyTransitionFailure journeyFailure, ClinicalRoomTransitionRequest request)
        {
            Accepted = accepted;
            Failure = failure;
            JourneyFailure = journeyFailure;
            Request = request;
        }

        public bool Accepted { get; }
        public ClinicalRoomTransitionFailure Failure { get; }
        public ClinicalJourneyTransitionFailure JourneyFailure { get; }
        public ClinicalRoomTransitionRequest Request { get; }

        public static ClinicalRoomTransitionStartResult Accept(ClinicalRoomTransitionRequest request)
            => new ClinicalRoomTransitionStartResult(true, ClinicalRoomTransitionFailure.None,
                ClinicalJourneyTransitionFailure.None, request);

        public static ClinicalRoomTransitionStartResult Reject(ClinicalRoomTransitionFailure failure,
            ClinicalJourneyTransitionFailure journeyFailure = ClinicalJourneyTransitionFailure.None)
            => new ClinicalRoomTransitionStartResult(false, failure, journeyFailure, default);
    }

    public readonly struct ClinicalRoomTransitionCompletion
    {
        ClinicalRoomTransitionCompletion(bool succeeded, ClinicalRoomTransitionFailure failure,
            string fromRoomId, string targetRoomId)
        {
            Succeeded = succeeded;
            Failure = failure;
            FromRoomId = fromRoomId;
            TargetRoomId = targetRoomId;
        }

        public bool Succeeded { get; }
        public ClinicalRoomTransitionFailure Failure { get; }
        public string FromRoomId { get; }
        public string TargetRoomId { get; }

        public static ClinicalRoomTransitionCompletion Success(string fromRoomId, string targetRoomId)
            => new ClinicalRoomTransitionCompletion(true, ClinicalRoomTransitionFailure.None,
                fromRoomId, targetRoomId);

        public static ClinicalRoomTransitionCompletion Reject(ClinicalRoomTransitionFailure failure,
            string fromRoomId, string targetRoomId)
            => new ClinicalRoomTransitionCompletion(false, failure, fromRoomId, targetRoomId);
    }

    public sealed class ClinicalRoomTransitionCoordinator
    {
        readonly ClinicalJourneySession _session;
        ClinicalRoomTransitionRequest _pendingRequest;

        public ClinicalRoomTransitionCoordinator(ClinicalJourneySession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            Phase = ClinicalRoomTransitionPhase.Active;
        }

        public ClinicalJourneySession Session => _session;
        public ClinicalRoomTransitionPhase Phase { get; private set; }
        public ClinicalRoomTransitionFailure LastFailure { get; private set; }
        public bool HasPendingRequest => Phase == ClinicalRoomTransitionPhase.Loading;

        public ClinicalRoomTransitionStartResult TryBeginAtDoor(string targetRoomId, bool atDoor, bool handConfirmed)
        {
            if (Phase == ClinicalRoomTransitionPhase.Loading)
                return ClinicalRoomTransitionStartResult.Reject(ClinicalRoomTransitionFailure.AlreadyLoading,
                    ClinicalJourneyTransitionFailure.TransitionInProgress);
            if (Phase == ClinicalRoomTransitionPhase.Failed)
                return ClinicalRoomTransitionStartResult.Reject(ClinicalRoomTransitionFailure.RecoveryRequired);

            if (!_session.TryPrepareRoomTransition(targetRoomId, atDoor, handConfirmed,
                    out var ticket, out var journeyFailure))
                return ClinicalRoomTransitionStartResult.Reject(ClinicalRoomTransitionFailure.JourneyRejected,
                    journeyFailure);

            _pendingRequest = new ClinicalRoomTransitionRequest(ticket);
            Phase = ClinicalRoomTransitionPhase.Loading;
            LastFailure = ClinicalRoomTransitionFailure.None;
            return ClinicalRoomTransitionStartResult.Accept(_pendingRequest);
        }

        public ClinicalRoomTransitionCompletion Complete(ClinicalRoomTransitionRequest request, bool roomLoaded)
        {
            if (Phase != ClinicalRoomTransitionPhase.Loading)
                return ClinicalRoomTransitionCompletion.Reject(ClinicalRoomTransitionFailure.NoTransitionInProgress,
                    request.FromRoomId, request.TargetRoomId);
            if (!MatchesPending(request))
                return ClinicalRoomTransitionCompletion.Reject(ClinicalRoomTransitionFailure.StaleRequest,
                    request.FromRoomId, request.TargetRoomId);

            if (!roomLoaded)
            {
                _session.TryCancelRoomTransition(request.JourneyTicket);
                Phase = ClinicalRoomTransitionPhase.Failed;
                LastFailure = ClinicalRoomTransitionFailure.RoomLoadFailed;
                _pendingRequest = default;
                return ClinicalRoomTransitionCompletion.Reject(LastFailure,
                    request.FromRoomId, request.TargetRoomId);
            }

            if (!_session.TryCommitRoomTransition(request.JourneyTicket))
            {
                _session.TryCancelRoomTransition(request.JourneyTicket);
                Phase = ClinicalRoomTransitionPhase.Failed;
                LastFailure = ClinicalRoomTransitionFailure.CommitRejected;
                _pendingRequest = default;
                return ClinicalRoomTransitionCompletion.Reject(LastFailure,
                    request.FromRoomId, request.TargetRoomId);
            }

            Phase = ClinicalRoomTransitionPhase.Active;
            LastFailure = ClinicalRoomTransitionFailure.None;
            _pendingRequest = default;
            return ClinicalRoomTransitionCompletion.Success(request.FromRoomId, request.TargetRoomId);
        }

        public bool RecoverAfterFailure()
        {
            if (Phase != ClinicalRoomTransitionPhase.Failed) return false;
            Phase = ClinicalRoomTransitionPhase.Active;
            LastFailure = ClinicalRoomTransitionFailure.None;
            return true;
        }

        bool MatchesPending(ClinicalRoomTransitionRequest request)
        {
            var pending = _pendingRequest.JourneyTicket;
            var candidate = request.JourneyTicket;
            return request.IsValid && _pendingRequest.IsValid &&
                pending.Sequence == candidate.Sequence &&
                pending.FromRoomId == candidate.FromRoomId &&
                pending.TargetRoomId == candidate.TargetRoomId;
        }
    }
}
