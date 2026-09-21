using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalJourneySessionTests
    {
        // Rule tests use ready synthetic tasks; publication availability is tested separately below.
        ClinicalJourneyDefinition Definition => ReadyDefinition();
        static ClinicalJourneyDefinition ReadyDefinition()
        { var definition=ClinicalJourneyConfiguration.Load();definition.tasks=null;return definition; }

        [Test]
        public void PublishedFullRouteHasTheConfirmedRoomOrder()
        {
            var definition = ClinicalJourneyConfiguration.Load();
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.mainlineRoomIds, Is.EqualTo(new[]
            {
                "R00_LOBBY", "R01_OFFICE", "R02_STORAGE", "R03_WAITING",
                "R04_GI", "R04_RESP", "R05_REPROCESSING", "R01_OFFICE"
            }));
            Assert.That(definition.rooms.Single(r => r.id == "R01_OFFICE").taskIds,
                Is.EqualTo(new[]{"OF-00","OF-01","OF-02","OF-03","OF-04","OF-05"}));
            Assert.That(definition.FindRoom("R05_REPROCESSING").taskIds,Does.Contain("RE-06"));
            Assert.That(definition.FindTask("RE-06").criterionIds,
                Is.EqualTo(new[]{"clothing","mask","eyeFace","cap","gloves","shoes"}));
            Assert.That(definition.FindTask("RE-06").availability,Is.EqualTo(ClinicalContentAvailability.MissingAsset));
        }

        [Test]
        public void RoomSwitchRequiresPhysicalDoorArrivalAndHandConfirmation()
        {
            var session = new ClinicalJourneySession(Definition, ClinicalJourneyMode.GuidedLearning);

            var away = session.TrySwitchRoom("R01_OFFICE", atDoor: false, handConfirmed: true);
            Assert.That(away.Failure, Is.EqualTo(ClinicalJourneyTransitionFailure.NotAtDoor));
            var noHand = session.TrySwitchRoom("R01_OFFICE", atDoor: true, handConfirmed: false);
            Assert.That(noHand.Failure, Is.EqualTo(ClinicalJourneyTransitionFailure.HandConfirmationRequired));

            var entered = session.TrySwitchRoom("R01_OFFICE", atDoor: true, handConfirmed: true);
            Assert.That(entered.Succeeded, Is.True);
            Assert.That(session.CurrentRoomId, Is.EqualTo("R01_OFFICE"));
        }

        [Test]
        public void TransitionCoordinatorKeepsOldRoomUntilAdapterCommits()
        {
            var session = new ClinicalJourneySession(Definition, ClinicalJourneyMode.GuidedLearning);
            var coordinator = new ClinicalRoomTransitionCoordinator(session);

            var start = coordinator.TryBeginAtDoor("R01_OFFICE", atDoor: true, handConfirmed: true);
            Assert.That(start.Accepted, Is.True);
            Assert.That(coordinator.Phase, Is.EqualTo(ClinicalRoomTransitionPhase.Loading));
            Assert.That(session.CurrentRoomId, Is.EqualTo("R00_LOBBY"));
            Assert.That(session.CanEdit, Is.False, "Tasks remain locked while the adapter is loading the room.");

            var duplicate = coordinator.TryBeginAtDoor("R01_OFFICE", atDoor: true, handConfirmed: true);
            Assert.That(duplicate.Failure, Is.EqualTo(ClinicalRoomTransitionFailure.AlreadyLoading));

            var completed = coordinator.Complete(start.Request, roomLoaded: true);
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(coordinator.Phase, Is.EqualTo(ClinicalRoomTransitionPhase.Active));
            Assert.That(session.CurrentRoomId, Is.EqualTo("R01_OFFICE"));
            Assert.That(session.CanEdit, Is.True);
        }

        [Test]
        public void FailedRoomLoadLeavesSessionAtOldRoomAndRequiresRecovery()
        {
            var session = new ClinicalJourneySession(Definition, ClinicalJourneyMode.GuidedLearning);
            var coordinator = new ClinicalRoomTransitionCoordinator(session);
            var start = coordinator.TryBeginAtDoor("R01_OFFICE", atDoor: true, handConfirmed: true);

            var failed = coordinator.Complete(start.Request, roomLoaded: false);
            Assert.That(failed.Failure, Is.EqualTo(ClinicalRoomTransitionFailure.RoomLoadFailed));
            Assert.That(coordinator.Phase, Is.EqualTo(ClinicalRoomTransitionPhase.Failed));
            Assert.That(session.CurrentRoomId, Is.EqualTo("R00_LOBBY"));
            Assert.That(session.HasPendingTransition, Is.False);

            Assert.That(coordinator.TryBeginAtDoor("R01_OFFICE", atDoor: true, handConfirmed: true).Failure,
                Is.EqualTo(ClinicalRoomTransitionFailure.RecoveryRequired));
            Assert.That(coordinator.RecoverAfterFailure(), Is.True);
            var retry = coordinator.TryBeginAtDoor("R01_OFFICE", atDoor: true, handConfirmed: true);
            Assert.That(retry.Accepted, Is.True);
            Assert.That(coordinator.Complete(retry.Request, roomLoaded: true).Succeeded, Is.True);
            Assert.That(session.CurrentRoomId, Is.EqualTo("R01_OFFICE"));
        }

        [Test]
        public void StaleCompletionCannotCommitAnotherRoomRequest()
        {
            var session = new ClinicalJourneySession(Definition, ClinicalJourneyMode.GuidedLearning);
            var coordinator = new ClinicalRoomTransitionCoordinator(session);
            var first = coordinator.TryBeginAtDoor("R01_OFFICE", atDoor: true, handConfirmed: true);
            Assert.That(coordinator.Complete(first.Request, roomLoaded: false).Succeeded, Is.False);
            Assert.That(coordinator.RecoverAfterFailure(), Is.True);

            var second = coordinator.TryBeginAtDoor("R01_OFFICE", atDoor: true, handConfirmed: true);
            var stale = coordinator.Complete(first.Request, roomLoaded: true);
            Assert.That(stale.Failure, Is.EqualTo(ClinicalRoomTransitionFailure.StaleRequest));
            Assert.That(session.CurrentRoomId, Is.EqualTo("R00_LOBBY"));
            Assert.That(coordinator.Phase, Is.EqualTo(ClinicalRoomTransitionPhase.Loading));

            Assert.That(coordinator.Complete(second.Request, roomLoaded: true).Succeeded, Is.True);
            Assert.That(session.CurrentRoomId, Is.EqualTo("R01_OFFICE"));
        }

        [Test]
        public void FutureRoomsCannotBeSkippedButVisitedRoomsCanBeReentered()
        {
            var session = new ClinicalJourneySession(Definition, ClinicalJourneyMode.GuidedLearning);
            Enter(session, "R01_OFFICE");
            var skip = session.TrySwitchRoom("R04_GI", atDoor: true, handConfirmed: true);
            Assert.That(skip.Failure, Is.EqualTo(ClinicalJourneyTransitionFailure.FutureRoomLocked));

            Enter(session, "R02_STORAGE");
            var back = session.TrySwitchRoom("R01_OFFICE", atDoor: true, handConfirmed: true);
            Assert.That(back.Succeeded, Is.True);
            Assert.That(session.MainlineIndex, Is.EqualTo(2), "A revisit must not advance the mainline cursor.");
            Assert.That(session.TrySwitchRoom("R03_WAITING", atDoor: true, handConfirmed: true).Succeeded, Is.True);
        }

        [Test]
        public void GuidedModeCanSkipAndFinishWithoutTurningSkipIntoAResult()
        {
            var session = new ClinicalJourneySession(Definition, ClinicalJourneyMode.GuidedLearning);
            Enter(session, "R01_OFFICE");
            Assert.That(session.TrySkipGuidedTask("OF-01"), Is.True);
            Assert.That(session.TryGetTask("OF-01", out var task), Is.True);
            Assert.That(task.Status, Is.EqualTo(ClinicalJourneyTaskStatus.Skipped));
            Assert.That(session.TryFinishGuidedAtSummary(), Is.False);
        }

        [Test]
        public void IndependentModeAllowsUnansweredAndSubmitsOnlyAtFinalOffice()
        {
            var session = NewAtFinalOffice(ClinicalJourneyMode.IndependentCheck);
            Assert.That(session.TrySetIndependentJudgement("OF-01", ClinicalJourneyJudgement.IssueFound), Is.True);
            Assert.That(session.TryLeaveIndependentUnanswered("OF-02"), Is.True);
            Assert.That(session.TrySubmitIndependentAtSummary(), Is.True);
            Assert.That(session.IsFinished, Is.True);
            Assert.That(session.TrySetIndependentJudgement("OF-01", ClinicalJourneyJudgement.NoIssue), Is.False);
        }

        [Test]
        public void NewSessionStartsCleanAfterApplicationRestart()
        {
            var definition = Definition;
            var first = NewAtFinalOffice(ClinicalJourneyMode.IndependentCheck);
            Assert.That(first.TrySetIndependentJudgement("OF-01", ClinicalJourneyJudgement.IssueFound), Is.True);

            // The application creates a new session object on every launch; no persistence adapter is involved.
            var restarted = new ClinicalJourneySession(definition, ClinicalJourneyMode.IndependentCheck);
            Assert.That(restarted.CurrentRoomId, Is.EqualTo("R00_LOBBY"));
            Assert.That(restarted.VisitedRooms, Has.Count.EqualTo(1));
            Assert.That(restarted.TryGetTask("OF-01", out var task), Is.True);
            Assert.That(task.Status, Is.EqualTo(ClinicalJourneyTaskStatus.Unstarted));
        }

        static ClinicalJourneySession NewAtFinalOffice(ClinicalJourneyMode mode)
        {
            var session = new ClinicalJourneySession(ReadyDefinition(), mode);
            Enter(session, "R01_OFFICE");
            Enter(session, "R02_STORAGE");
            Enter(session, "R03_WAITING");
            Enter(session, "R04_GI");
            Enter(session, "R04_RESP");
            Enter(session, "R05_REPROCESSING");
            Enter(session, "R01_OFFICE");
            return session;
        }

        [Test] public void PublishedUnavailableContentCannotBecomeSkippedCorrectOrUnanswered()
        {
            var definition=ClinicalJourneyConfiguration.Load();
            var guided=new ClinicalJourneySession(definition,ClinicalJourneyMode.GuidedLearning);
            Enter(guided,"R01_OFFICE");
            Assert.That(guided.IsContentAvailable("OF-03"),Is.False);
            Assert.That(guided.TryBeginTask("OF-03"),Is.False);
            Assert.That(guided.TrySkipGuidedTask("OF-03"),Is.False);
            Assert.That(guided.TryCompleteGuidedTask("OF-03"),Is.False);
            var independent=new ClinicalJourneySession(definition,ClinicalJourneyMode.IndependentCheck);
            Assert.That(independent.TryCompleteInstruction("N00"),Is.True);
            Assert.That(independent.TrySetIndependentJudgement("N00",ClinicalJourneyJudgement.NoIssue),Is.False);
            Enter(independent,"R01_OFFICE");
            Assert.That(independent.TryLeaveIndependentUnanswered("OF-03"),Is.False);
            Assert.That(independent.TrySetIndependentJudgement("OF-03",ClinicalJourneyJudgement.NoIssue),Is.False);
            independent.TryGetTask("OF-03",out var result);
            Assert.That(result.Status,Is.EqualTo(ClinicalJourneyTaskStatus.Unstarted));
        }

        [Test] public void FindingsRequireDeclaredCriteriaAndEvidenceAndFreezeAfterSubmission()
        {
            var definition=ReadyDefinition();
            definition.tasks=new[]{new ClinicalJourneyTaskDefinition { id="OF-01",criterionIds=new[]{"patient","scope"},evidenceIds=new[]{"r1","r2"} }};
            var session=new ClinicalJourneySession(definition,ClinicalJourneyMode.IndependentCheck);
            foreach(var room in definition.mainlineRoomIds.Skip(1))Enter(session,room);
            Assert.That(session.TryRecordFinding("OF-01","unknown",ClinicalJourneyJudgement.IssueFound,new[]{"r1"}),Is.False);
            Assert.That(session.TryRecordFinding("OF-01","patient",ClinicalJourneyJudgement.IssueFound,new[]{"foreign"}),Is.False);
            Assert.That(session.TryRecordFinding("OF-01","patient",ClinicalJourneyJudgement.IssueFound,new[]{"r1"}),Is.True);
            session.TryGetTask("OF-01",out var partial);Assert.That(partial.Status,Is.EqualTo(ClinicalJourneyTaskStatus.InProgress));
            Assert.That(session.TryRecordFinding("OF-01","scope",ClinicalJourneyJudgement.NoIssue,new[]{"r2"}),Is.True);
            var copy=session.GetFindings("OF-01");copy[0].EvidenceIds[0]="mutated";
            Assert.That(session.GetFindings("OF-01")[0].EvidenceIds[0],Is.EqualTo("r1"));
            Assert.That(session.TrySubmitIndependentAtSummary(),Is.True);
            Assert.That(session.TryRecordFinding("OF-01","scope",ClinicalJourneyJudgement.IssueFound,new[]{"r1"}),Is.False);
        }

        static void Enter(ClinicalJourneySession session, string roomId)
        {
            Assert.That(session.TrySwitchRoom(roomId, atDoor: true, handConfirmed: true).Succeeded, Is.True, roomId);
        }
    }
}
