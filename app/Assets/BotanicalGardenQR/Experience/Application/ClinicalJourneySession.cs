using System;
using System.Collections.Generic;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Experience.Application
{
    /// <summary>
    /// Owns one in-memory full-script run. Room movement is requested only after
    /// the Unity room adapter has established that the visitor is at a door and
    /// a hand has confirmed the door control.
    /// </summary>
    public sealed partial class ClinicalJourneySession
    {
        sealed class TaskRecord
        {
            public ClinicalJourneyTaskStatus Status;
            public ClinicalJourneyJudgement Judgement;
            public readonly Dictionary<string,ClinicalFinding> Findings = new Dictionary<string,ClinicalFinding>(StringComparer.Ordinal);
        }

        readonly ClinicalJourneyDefinition _definition;
        readonly Dictionary<string, TaskRecord> _tasks = new Dictionary<string, TaskRecord>(StringComparer.Ordinal);
        readonly HashSet<string> _visitedRooms = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, int> _roomVisitCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int _mainlineIndex;
        int _transitionSequence;
        ClinicalJourneyTransitionTicket _pendingTransition;

        public ClinicalJourneySession(ClinicalJourneyDefinition definition, ClinicalJourneyMode mode)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _definition.Validate();
            Mode = mode;
            CurrentRoomId = _definition.startRoomId;
            _visitedRooms.Add(CurrentRoomId);
            _roomVisitCounts.Add(CurrentRoomId, 1);
            foreach (var room in _definition.rooms)
                foreach (var taskId in room.taskIds ?? new string[0])
                    _tasks.Add(taskId, new TaskRecord());
        }

        public ClinicalJourneyMode Mode { get; }
        public string CurrentRoomId { get; private set; }
        public bool IsSubmitted { get; private set; }
        public bool IsFinished { get; private set; }
        public int MainlineIndex => _mainlineIndex;
        public IReadOnlyCollection<string> VisitedRooms => _visitedRooms;
        public bool HasPendingTransition => _pendingTransition.IsValid;
        public bool CanEdit => !IsSubmitted && !IsFinished && !HasPendingTransition;
        public bool IsAtFinalSummary => CurrentRoomId == _definition.summaryRoomId &&
            _mainlineIndex == _definition.mainlineRoomIds.Length - 1;

        public bool HasVisited(string roomId) => _visitedRooms.Contains(roomId);
        public int RoomVisitCount(string roomId)
            => _roomVisitCounts.TryGetValue(roomId, out var count) ? count : 0;

        public bool TryBeginTask(string taskId)
        {
            if (!CanEdit || !TaskIsInCurrentRoom(taskId) || !IsContentAvailable(taskId)) return false;
            var record = _tasks[taskId];
            if (record.Status == ClinicalJourneyTaskStatus.Unstarted || record.Status == ClinicalJourneyTaskStatus.Unanswered ||
                record.Status == ClinicalJourneyTaskStatus.Skipped)
                record.Status = ClinicalJourneyTaskStatus.InProgress;
            return true;
        }

        public bool TryCompleteGuidedTask(string taskId)
        {
            if (Mode != ClinicalJourneyMode.GuidedLearning || !CanEdit || !TaskIsInCurrentRoom(taskId) || !IsContentAvailable(taskId)) return false;
            if(!LearningCriteriaComplete(taskId))return false;
            var record = _tasks[taskId];
            record.Status = ClinicalJourneyTaskStatus.Completed;
            record.Judgement = ClinicalJourneyJudgement.None;
            return true;
        }

        public bool TrySkipGuidedTask(string taskId)
        {
            if (!CanLearnHere(taskId) || !IsContentAvailable(taskId)) return false;
            var record = _tasks[taskId];
            record.Status = ClinicalJourneyTaskStatus.Skipped;
            record.Judgement = ClinicalJourneyJudgement.None;
            return true;
        }

        public bool TrySetIndependentJudgement(string taskId, ClinicalJourneyJudgement judgement)
        {
            if((_definition.FindTask(taskId)?.criterionIds?.Length??0)>0)return false;
            return SetIndependentJudgement(taskId,judgement);
        }

        bool SetIndependentJudgement(string taskId,ClinicalJourneyJudgement judgement)
        {
            if (Mode != ClinicalJourneyMode.IndependentCheck || !CanEdit || !TaskIsInCurrentRoom(taskId) ||
                (judgement != ClinicalJourneyJudgement.NoIssue && judgement != ClinicalJourneyJudgement.IssueFound) || !IsContentAvailable(taskId) || _definition.FindTask(taskId)?.instructionOnly==true)
                return false;
            var record = _tasks[taskId];
            record.Status = ClinicalJourneyTaskStatus.Completed;
            record.Judgement = judgement;
            return true;
        }

        public bool TryLeaveIndependentUnanswered(string taskId)
        {
            if (Mode != ClinicalJourneyMode.IndependentCheck || !CanEdit || !TaskIsInCurrentRoom(taskId) || !IsContentAvailable(taskId)) return false;
            var record = _tasks[taskId];
            record.Status = ClinicalJourneyTaskStatus.Unanswered;
            record.Judgement = ClinicalJourneyJudgement.None;
            record.Findings.Clear();
            return true;
        }

        public ClinicalContentAvailability AvailabilityOf(string taskId)
            => _definition.FindTask(taskId)?.availability ?? ClinicalContentAvailability.RuntimeUnavailable;
        public bool IsContentAvailable(string taskId) => _tasks.ContainsKey(taskId) && AvailabilityOf(taskId)==ClinicalContentAvailability.Ready;
        public bool TryCompleteInstruction(string taskId)
        {
            if(!CanEdit || !TaskIsInCurrentRoom(taskId) || !IsContentAvailable(taskId) || _definition.FindTask(taskId)?.instructionOnly!=true)return false;
            _tasks[taskId].Status=ClinicalJourneyTaskStatus.Completed;
            return true;
        }
        public bool TryRecordFinding(string taskId,string criterionId,ClinicalJourneyJudgement judgement,string[] evidenceIds)
        {
            var definition=_definition.FindTask(taskId);
            if(definition==null || Array.IndexOf(definition.criterionIds??new string[0],criterionId)<0 || evidenceIds==null || evidenceIds.Length==0)return false;
            foreach(var id in evidenceIds) if(Array.IndexOf(definition.evidenceIds??new string[0],id)<0)return false;
            if(!SetIndependentJudgement(taskId,judgement))return false;
            var record=_tasks[taskId];
            record.Findings[criterionId]=new ClinicalFinding(criterionId,judgement,evidenceIds);
            if(record.Findings.Count<definition.criterionIds.Length)record.Status=ClinicalJourneyTaskStatus.InProgress;
            // A parent result must not silently become whichever child was answered last.
            record.Judgement=ClinicalJourneyJudgement.NoIssue;
            foreach(var item in record.Findings.Values)if(item.Judgement==ClinicalJourneyJudgement.IssueFound)record.Judgement=ClinicalJourneyJudgement.IssueFound;
            return true;
        }
        public ClinicalFinding[] GetFindings(string taskId)
        {
            if(!_tasks.TryGetValue(taskId,out var task))return new ClinicalFinding[0];
            var result=new ClinicalFinding[task.Findings.Count];int n=0;
            foreach(var finding in task.Findings.Values)result[n++]=new ClinicalFinding(finding.CriterionId,finding.Judgement,finding.EvidenceIds);
            return result;
        }

        public bool TryGetTask(string taskId, out ClinicalJourneyTaskSnapshot snapshot)
        {
            if (!_tasks.TryGetValue(taskId, out var record))
            {
                snapshot = default;
                return false;
            }
            var room = _definition.FindRoomForTask(taskId);
            snapshot = new ClinicalJourneyTaskSnapshot(taskId, room.id, record.Status, record.Judgement);
            return true;
        }

        public ClinicalJourneyTransitionResult TrySwitchRoom(string targetRoomId, bool atDoor, bool handConfirmed)
        {
            if (!TryPrepareRoomTransition(targetRoomId, atDoor, handConfirmed,
                    out var ticket, out var failure))
                return ClinicalJourneyTransitionResult.Reject(failure, CurrentRoomId, targetRoomId);
            if (!TryCommitRoomTransition(ticket))
                return ClinicalJourneyTransitionResult.Reject(ClinicalJourneyTransitionFailure.NoPendingTransition,
                    CurrentRoomId, targetRoomId);
            return ClinicalJourneyTransitionResult.Success(CurrentRoomId, targetRoomId, ticket.AdvancesMainline);
        }

        public bool TryPrepareRoomTransition(string targetRoomId, bool atDoor, bool handConfirmed,
            out ClinicalJourneyTransitionTicket ticket, out ClinicalJourneyTransitionFailure failure,
            bool roomGallerySelection = false)
        {
            ticket = default;
            if (HasPendingTransition)
            {
                failure = ClinicalJourneyTransitionFailure.TransitionInProgress;
                return false;
            }
            if (_definition.FindRoom(targetRoomId) == null)
            {
                failure = ClinicalJourneyTransitionFailure.UnknownRoom;
                return false;
            }
            if (targetRoomId == CurrentRoomId)
            {
                failure = ClinicalJourneyTransitionFailure.AlreadyInRoom;
                return false;
            }
            if (!atDoor)
            {
                failure = ClinicalJourneyTransitionFailure.NotAtDoor;
                return false;
            }
            if (!handConfirmed)
            {
                failure = ClinicalJourneyTransitionFailure.HandConfirmationRequired;
                return false;
            }
            // The cursor records the last mainline room completed, not the room
            // currently occupied after a permitted revisit.
            var next = _definition.IsMainlineNext(_mainlineIndex, targetRoomId);
            if (!roomGallerySelection && !next && !_visitedRooms.Contains(targetRoomId))
            {
                failure = ClinicalJourneyTransitionFailure.FutureRoomLocked;
                return false;
            }
            if (!roomGallerySelection && !_definition.CanTransfer(CurrentRoomId, targetRoomId))
            {
                failure = ClinicalJourneyTransitionFailure.TransitionNotAllowed;
                return false;
            }
            // Door destinations include all visited rooms and the mainline continuation.
            // Submission freezes answers, not read-only room review.

            var sequence = ++_transitionSequence;
            if (sequence <= 0) sequence = _transitionSequence = 1;
            ticket = new ClinicalJourneyTransitionTicket(CurrentRoomId, targetRoomId, sequence, next);
            _pendingTransition = ticket;
            failure = ClinicalJourneyTransitionFailure.None;
            return true;
        }

        public bool TryCommitRoomTransition(ClinicalJourneyTransitionTicket ticket)
        {
            if (!MatchesPending(ticket)) return false;
            CurrentRoomId = ticket.TargetRoomId;
            _visitedRooms.Add(ticket.TargetRoomId);
            _roomVisitCounts.TryGetValue(ticket.TargetRoomId, out var visits);
            _roomVisitCounts[ticket.TargetRoomId] = visits + 1;
            if (ticket.AdvancesMainline) _mainlineIndex++;
            _pendingTransition = default;
            return true;
        }

        public bool TryCancelRoomTransition(ClinicalJourneyTransitionTicket ticket)
        {
            if (!MatchesPending(ticket)) return false;
            _pendingTransition = default;
            return true;
        }

        public bool CanSubmitAtSummary => Mode == ClinicalJourneyMode.IndependentCheck && !IsSubmitted &&
            !IsFinished && !HasPendingTransition && IsAtFinalSummary && HasVisitedAllMainlineRooms;

        public bool HasVisitedAllMainlineRooms
        {
            get
            {
                if (_mainlineIndex < _definition.mainlineRoomIds.Length - 1) return false;
                foreach (var roomId in _definition.mainlineRoomIds)
                    if (!_visitedRooms.Contains(roomId)) return false;
                return true;
            }
        }

        public bool TrySubmitIndependentAtSummary()
        {
            if (!CanSubmitAtSummary) return false;
            IsSubmitted = true;
            IsFinished = true;
            return true;
        }

        public bool TryFinishGuidedAtSummary()
        {
            if (Mode != ClinicalJourneyMode.GuidedLearning || IsSubmitted || IsFinished ||
                HasPendingTransition ||
                !IsAtFinalSummary || !HasVisitedAllMainlineRooms)
                return false;
            IsSubmitted = true;
            IsFinished = true;
            return true;
        }

        bool MatchesPending(ClinicalJourneyTransitionTicket ticket)
        {
            return HasPendingTransition && ticket.IsValid &&
                _pendingTransition.Sequence == ticket.Sequence &&
                _pendingTransition.FromRoomId == ticket.FromRoomId &&
                _pendingTransition.TargetRoomId == ticket.TargetRoomId &&
                _pendingTransition.AdvancesMainline == ticket.AdvancesMainline;
        }

        bool TaskIsInCurrentRoom(string taskId)
        {
            var room = _definition.FindRoomForTask(taskId);
            return room != null && room.id == CurrentRoomId && _tasks.ContainsKey(taskId);
        }
    }
}
