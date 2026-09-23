using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalActSelectionTests
    {
        [Test]
        public void InitialChoicesUsePublishedRoomAndTaskIdsWithoutOfferingLobbyAsAChapter()
        {
            var definition = ClinicalJourneyConfiguration.Load();
            var session = new ClinicalJourneySession(definition, ClinicalJourneyMode.GuidedLearning);

            var options = ClinicalActSelection.AvailableDestinations(definition, session);

            Assert.That(options, Has.Length.EqualTo(1));
            Assert.That(options[0].RoomId, Is.EqualTo("R01_OFFICE"));
            Assert.That(options[0].Kind, Is.EqualTo(ClinicalActDestinationKind.ContinueMainline));
            Assert.That(options[0].IsChapter, Is.True);
            Assert.That(options[0].TaskIds, Is.EqualTo(definition.FindRoom("R01_OFFICE").taskIds));
            Assert.That(options.Any(option => option.RoomId == definition.startRoomId), Is.False);
            Assert.That(options.Any(option => option.RoomId == "R04_GI"), Is.False,
                "Unvisited future rooms are not selectable.");
        }

        [Test]
        public void LobbyReturnAndRoomReviewDoNotAdvanceTheMainlineCursor()
        {
            var definition = ClinicalJourneyConfiguration.Load();
            var session = new ClinicalJourneySession(definition, ClinicalJourneyMode.GuidedLearning);
            Enter(session, "R01_OFFICE");
            Assert.That(session.RoomVisitCount("R01_OFFICE"), Is.EqualTo(1));
            Assert.That(ClinicalActSelection.OfficeVisitStage(definition, session),
                Is.EqualTo(ClinicalOfficeVisitStage.FirstVisit));
            var officeCursor = session.MainlineIndex;

            var officeOptions = ClinicalActSelection.AvailableDestinations(definition, session);
            var returnToLobby = System.Array.Find(officeOptions, option => option.RoomId == definition.startRoomId);
            Assert.That(returnToLobby.Kind, Is.EqualTo(ClinicalActDestinationKind.ReturnToLobby));
            Assert.That(returnToLobby.IsChapter, Is.False);
            Assert.That(returnToLobby.TaskIds, Is.Empty);

            Enter(session, definition.startRoomId);
            Assert.That(session.MainlineIndex, Is.EqualTo(officeCursor));
            var lobbyOptions = ClinicalActSelection.AvailableDestinations(definition, session);
            Assert.That(lobbyOptions.Any(option => option.RoomId == definition.startRoomId), Is.False);
            var officeReview = System.Array.Find(lobbyOptions, option => option.RoomId == "R01_OFFICE");
            Assert.That(officeReview.Kind, Is.EqualTo(ClinicalActDestinationKind.ReviewVisitedChapter));
            Assert.That(officeReview.TaskIds, Is.EqualTo(definition.FindRoom("R01_OFFICE").taskIds));

            Enter(session, officeReview.RoomId);
            Assert.That(session.MainlineIndex, Is.EqualTo(officeCursor));
            Assert.That(session.RoomVisitCount("R01_OFFICE"), Is.EqualTo(2));
            Assert.That(ClinicalActSelection.OfficeVisitStage(definition, session),
                Is.EqualTo(ClinicalOfficeVisitStage.Review));
            var resumed = ClinicalActSelection.AvailableDestinations(definition, session);
            Assert.That(System.Array.Find(resumed, option => option.RoomId == "R02_STORAGE").Kind,
                Is.EqualTo(ClinicalActDestinationKind.ContinueMainline));
        }

        [Test]
        public void RoomVisitCountChangesOnlyWhenTheTransitionCommits()
        {
            var definition = ClinicalJourneyConfiguration.Load();
            var session = new ClinicalJourneySession(definition, ClinicalJourneyMode.GuidedLearning);
            var transitions = new ClinicalRoomTransitionCoordinator(session);

            var failedLoad = transitions.TryBeginAtDoor("R01_OFFICE", atDoor: true, handConfirmed: true);
            Assert.That(failedLoad.Accepted, Is.True);
            Assert.That(transitions.Complete(failedLoad.Request, roomLoaded: false).Succeeded, Is.False);
            Assert.That(session.RoomVisitCount("R01_OFFICE"), Is.Zero);

            Assert.That(transitions.RecoverAfterFailure(), Is.True);
            var successfulLoad = transitions.TryBeginAtDoor("R01_OFFICE", atDoor: true, handConfirmed: true);
            Assert.That(transitions.Complete(successfulLoad.Request, roomLoaded: true).Succeeded, Is.True);
            Assert.That(session.RoomVisitCount("R01_OFFICE"), Is.EqualTo(1));
        }

        [Test]
        public void FinalOfficeIsNotReachedUntilItsLastMainlineOccurrence()
        {
            var definition = ClinicalJourneyConfiguration.Load();
            var session = new ClinicalJourneySession(definition, ClinicalJourneyMode.GuidedLearning);

            for (var i = 1; i < definition.mainlineRoomIds.Length - 1; i++)
                Enter(session, definition.mainlineRoomIds[i]);

            Assert.That(session.MainlineIndex, Is.EqualTo(definition.mainlineRoomIds.Length - 2));
            Assert.That(session.HasVisitedAllMainlineRooms, Is.False,
                "The distinct rooms have been visited, but the final office occurrence is still ahead.");

            Enter(session, definition.summaryRoomId);

            Assert.That(session.MainlineIndex, Is.EqualTo(definition.mainlineRoomIds.Length - 1));
            Assert.That(session.HasVisitedAllMainlineRooms, Is.True);
            Assert.That(ClinicalActSelection.OfficeVisitStage(definition, session),
                Is.EqualTo(ClinicalOfficeVisitStage.FinalSummary));
            Assert.That(session.TryFinishGuidedAtSummary(), Is.True);
        }

        [Test]
        public void SubmittedSessionOffersOnlyReadOnlyReviewDestinations()
        {
            var definition = ClinicalJourneyConfiguration.Load();
            var session = new ClinicalJourneySession(definition, ClinicalJourneyMode.GuidedLearning);
            for (var i = 1; i < definition.mainlineRoomIds.Length; i++)
                Enter(session, definition.mainlineRoomIds[i]);
            Assert.That(session.TryFinishGuidedAtSummary(), Is.True);

            var options = ClinicalActSelection.AvailableDestinations(definition, session);

            Assert.That(options.Length, Is.GreaterThan(0));
            Assert.That(options.Any(option => option.Kind == ClinicalActDestinationKind.ContinueMainline), Is.False);
            Assert.That(options.All(option => session.HasVisited(option.RoomId)), Is.True);
            Assert.That(options.Any(option => option.RoomId == definition.startRoomId && option.IsChapter), Is.False);
        }

        static void Enter(ClinicalJourneySession session, string roomId)
        {
            var result = session.TrySwitchRoom(roomId, atDoor: true, handConfirmed: true);
            Assert.That(result.Succeeded, Is.True, "Could not enter " + roomId + ": " + result.Failure);
        }
    }
}
