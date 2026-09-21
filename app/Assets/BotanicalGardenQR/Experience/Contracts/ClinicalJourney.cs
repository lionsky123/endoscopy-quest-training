using System;
using System.Collections.Generic;

namespace BotanicalGardenQR.Experience.Contracts
{
    public enum ClinicalJourneyMode
    {
        GuidedLearning,
        IndependentCheck
    }

    public enum ClinicalJourneyTaskStatus
    {
        Unstarted,
        InProgress,
        Completed,
        Skipped,
        Unanswered
    }

    public enum ClinicalJourneyJudgement
    {
        None,
        NoIssue,
        IssueFound
    }

    public enum ClinicalContentAvailability { Ready, MissingAsset, MissingEvidence, RuleUnverified, RuntimeUnavailable }

    [Serializable]
    public sealed class ClinicalJourneyTaskDefinition
    {
        public string id, title, unavailableReason;
        public ClinicalContentAvailability availability;
        public bool instructionOnly;
        public string[] sourceIds, evidenceIds, criterionIds;
    }

    public readonly struct ClinicalFinding
    {
        public ClinicalFinding(string criterionId, ClinicalJourneyJudgement judgement, string[] evidenceIds)
        { CriterionId=criterionId; Judgement=judgement; EvidenceIds=(string[])(evidenceIds ?? new string[0]).Clone(); }
        public string CriterionId { get; }
        public ClinicalJourneyJudgement Judgement { get; }
        public string[] EvidenceIds { get; }
    }

    public enum ClinicalJourneyTransitionFailure
    {
        None,
        SessionFinished,
        UnknownRoom,
        AlreadyInRoom,
        NotAtDoor,
        HandConfirmationRequired,
        TransitionNotAllowed,
        FutureRoomLocked,
        TransitionInProgress,
        NoPendingTransition
    }

    [Serializable]
    public sealed class ClinicalJourneyRoomDefinition
    {
        public string id;
        public string displayName;
        public string[] taskIds;
    }

    [Serializable]
    public sealed class ClinicalJourneyTransferDefinition
    {
        public string fromRoomId;
        public string toRoomId;
    }

    [Serializable]
    public sealed class ClinicalJourneyDefinition
    {
        public string journeyId;
        public string version;
        public string startRoomId;
        public string summaryRoomId;
        public string[] mainlineRoomIds;
        public ClinicalJourneyRoomDefinition[] rooms;
        public ClinicalJourneyTransferDefinition[] transfers;
        public ClinicalJourneyTaskDefinition[] tasks;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(journeyId) || string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("A journey id and version are required.");
            if (rooms == null || rooms.Length == 0)
                throw new ArgumentException("A journey must declare at least one room.");
            if (transfers == null)
                throw new ArgumentException("A journey must declare its door transitions.");
            if (mainlineRoomIds == null || mainlineRoomIds.Length < 2)
                throw new ArgumentException("A journey needs a start and a summary room.");
            if (string.IsNullOrWhiteSpace(startRoomId) || string.IsNullOrWhiteSpace(summaryRoomId))
                throw new ArgumentException("A journey needs a start and a summary room id.");
            if (mainlineRoomIds[0] != startRoomId || mainlineRoomIds[mainlineRoomIds.Length - 1] != summaryRoomId)
                throw new ArgumentException("The mainline must begin at startRoomId and end at summaryRoomId.");

            var roomIds = new HashSet<string>(StringComparer.Ordinal);
            var taskIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var room in rooms)
            {
                if (room == null || string.IsNullOrWhiteSpace(room.id) || !roomIds.Add(room.id))
                    throw new ArgumentException("Room ids must be present and unique.");
                if (room.taskIds == null) room.taskIds = new string[0];
                foreach (var taskId in room.taskIds)
                    if (string.IsNullOrWhiteSpace(taskId) || !taskIds.Add(taskId))
                        throw new ArgumentException("Task ids must be present and globally unique.");
            }

            if (!roomIds.Contains(startRoomId) || !roomIds.Contains(summaryRoomId))
                throw new ArgumentException("Start and summary rooms must be declared.");

            var described = new HashSet<string>(StringComparer.Ordinal);
            foreach(var task in tasks ?? new ClinicalJourneyTaskDefinition[0])
            {
                if(task==null || !taskIds.Contains(task.id) || !described.Add(task.id))
                    throw new ArgumentException("Task descriptions must reference distinct declared tasks.");
                if(!Enum.IsDefined(typeof(ClinicalContentAvailability),task.availability))
                    throw new ArgumentException("Unknown content availability.");
                if(task.availability!=ClinicalContentAvailability.Ready && string.IsNullOrWhiteSpace(task.unavailableReason))
                    throw new ArgumentException("Unavailable content needs an explicit reason.");
            }

            foreach (var roomId in mainlineRoomIds)
                if (string.IsNullOrWhiteSpace(roomId) || !roomIds.Contains(roomId))
                    throw new ArgumentException("The mainline contains an unknown room.");

            var transferIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var transfer in transfers)
            {
                if (transfer == null || string.IsNullOrWhiteSpace(transfer.fromRoomId) ||
                    string.IsNullOrWhiteSpace(transfer.toRoomId) || transfer.fromRoomId == transfer.toRoomId ||
                    !roomIds.Contains(transfer.fromRoomId) || !roomIds.Contains(transfer.toRoomId))
                    throw new ArgumentException("Door transitions must connect two declared, different rooms.");
                if (!transferIds.Add(TransferKey(transfer.fromRoomId, transfer.toRoomId)))
                    throw new ArgumentException("Door transitions must be unique.");
            }

            for (var i = 1; i < mainlineRoomIds.Length; i++)
                if (FindTransfer(mainlineRoomIds[i - 1], mainlineRoomIds[i]) == null)
                    throw new ArgumentException("Every adjacent mainline room needs a door transition.");
        }

        public ClinicalJourneyRoomDefinition FindRoom(string id)
        {
            if (rooms == null || string.IsNullOrWhiteSpace(id)) return null;
            foreach (var room in rooms) if (room != null && room.id == id) return room;
            return null;
        }

        public ClinicalJourneyTaskDefinition FindTask(string id)
        { if(tasks!=null) foreach(var task in tasks) if(task.id==id)return task; return null; }

        public ClinicalJourneyTransferDefinition FindTransfer(string fromRoomId, string toRoomId)
        {
            if (transfers == null) return null;
            foreach (var transfer in transfers)
                if (transfer != null && transfer.fromRoomId == fromRoomId && transfer.toRoomId == toRoomId)
                    return transfer;
            return null;
        }

        public ClinicalJourneyRoomDefinition FindRoomForTask(string taskId)
        {
            if (rooms == null || string.IsNullOrWhiteSpace(taskId)) return null;
            foreach (var room in rooms)
            {
                if (room == null || room.taskIds == null) continue;
                foreach (var candidate in room.taskIds) if (candidate == taskId) return room;
            }
            return null;
        }

        public bool IsMainlineNext(int mainlineIndex, string targetRoomId)
        {
            var next = mainlineIndex + 1;
            return next < mainlineRoomIds.Length && mainlineRoomIds[next] == targetRoomId;
        }

        static string TransferKey(string fromRoomId, string toRoomId) => fromRoomId + "\u001f" + toRoomId;
    }

    public readonly struct ClinicalJourneyTransitionResult
    {
        ClinicalJourneyTransitionResult(bool succeeded, ClinicalJourneyTransitionFailure failure,
            string currentRoomId, string targetRoomId, bool advancedMainline)
        {
            Succeeded = succeeded;
            Failure = failure;
            CurrentRoomId = currentRoomId;
            TargetRoomId = targetRoomId;
            AdvancedMainline = advancedMainline;
        }

        public bool Succeeded { get; }
        public ClinicalJourneyTransitionFailure Failure { get; }
        public string CurrentRoomId { get; }
        public string TargetRoomId { get; }
        public bool AdvancedMainline { get; }

        public static ClinicalJourneyTransitionResult Success(string currentRoomId, string targetRoomId, bool advancedMainline)
            => new ClinicalJourneyTransitionResult(true, ClinicalJourneyTransitionFailure.None, currentRoomId, targetRoomId, advancedMainline);

        public static ClinicalJourneyTransitionResult Reject(ClinicalJourneyTransitionFailure failure, string currentRoomId, string targetRoomId)
            => new ClinicalJourneyTransitionResult(false, failure, currentRoomId, targetRoomId, false);
    }

    /// <summary>
    /// A prepared room change. Preparing does not change the active room or the
    /// mainline cursor; the application commits it only after the room adapter
    /// has loaded and activated the target successfully.
    /// </summary>
    public readonly struct ClinicalJourneyTransitionTicket
    {
        public ClinicalJourneyTransitionTicket(string fromRoomId, string targetRoomId, int sequence, bool advancesMainline)
        {
            FromRoomId = fromRoomId;
            TargetRoomId = targetRoomId;
            Sequence = sequence;
            AdvancesMainline = advancesMainline;
        }

        public string FromRoomId { get; }
        public string TargetRoomId { get; }
        public int Sequence { get; }
        public bool AdvancesMainline { get; }
        public bool IsValid => Sequence > 0 && !string.IsNullOrEmpty(FromRoomId) && !string.IsNullOrEmpty(TargetRoomId);
    }

    public readonly struct ClinicalJourneyTaskSnapshot
    {
        public ClinicalJourneyTaskSnapshot(string taskId, string roomId, ClinicalJourneyTaskStatus status,
            ClinicalJourneyJudgement judgement)
        {
            TaskId = taskId;
            RoomId = roomId;
            Status = status;
            Judgement = judgement;
        }

        public string TaskId { get; }
        public string RoomId { get; }
        public ClinicalJourneyTaskStatus Status { get; }
        public ClinicalJourneyJudgement Judgement { get; }
    }
}
