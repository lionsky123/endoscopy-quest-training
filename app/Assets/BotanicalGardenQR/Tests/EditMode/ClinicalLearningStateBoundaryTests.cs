using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalLearningStateBoundaryTests
    {
        [Test]
        public void UnavailablePracticeDoesNotConsumeMethodOrRecordHelp()
        {
            var definition=ClinicalJourneyConfiguration.Load();
            var task=definition.tasks.Single(item=>item.id=="ST-02");
            task.availability=ClinicalContentAvailability.MissingEvidence;
            var session=new ClinicalJourneySession(definition,ClinicalJourneyMode.GuidedLearning);
            Enter(session,"R01_OFFICE");
            Enter(session,"R02_STORAGE");

            Assert.That(session.TryIntroduceLearningMethod("ST-02","register-range"),Is.False);
            Assert.That(session.TryRequestLearningHelp("ST-02","storage-week-1",false),Is.False);
            Assert.That(session.LearningAttempts("ST-02"),Is.Empty);
            Assert.That(session.LearningActionCount("ST-02",ClinicalLearningAction.MethodShown),Is.Zero);
            Assert.That(session.LearningActionCount("ST-02",ClinicalLearningAction.Hint),Is.Zero);

            task.availability=ClinicalContentAvailability.Ready;
            Assert.That(session.TryIntroduceLearningMethod("ST-02","register-range"),Is.True,
                "An unavailable item must not use up the method's first demonstration.");
            Assert.That(session.TryRequestLearningHelp("ST-02","storage-week-1",false),Is.True);
            Assert.That(session.GetLearningAttempt("ST-02","storage-week-1").HintUsed,Is.True);
        }

        [Test]
        public void HelpRequiresAPublishedCriterionAndCannotArriveDuringTransition()
        {
            var definition=ClinicalJourneyConfiguration.Load();
            definition.tasks.Single(item=>item.id=="ST-02").criterionIds=new[] {"storage-week-2"};
            var session=new ClinicalJourneySession(definition,ClinicalJourneyMode.GuidedLearning);
            Enter(session,"R01_OFFICE");
            Enter(session,"R02_STORAGE");

            Assert.That(session.TryRequestLearningHelp("ST-02","storage-week-1",true),Is.False);
            Assert.That(session.LearningAttempts("ST-02"),Is.Empty);
            Assert.That(session.TryPrepareRoomTransition("R03_WAITING",true,true,out var ticket,out _),Is.True);
            Assert.That(session.TryRequestLearningHelp("ST-02","storage-week-2",true),Is.False);
            Assert.That(session.TryCancelRoomTransition(ticket),Is.True);
            Assert.That(session.TryRequestLearningHelp("ST-02","storage-week-2",true),Is.True);
            Assert.That(session.GetLearningAttempt("ST-02","storage-week-2").Attempts,Is.Zero,
                "Opening a lesson is not an answer.");
        }

        [Test]
        public void MethodIsDemonstratedOnceAcrossRoomsAndNewSessionStartsClean()
        {
            var definition=ClinicalJourneyConfiguration.Load();
            var session=new ClinicalJourneySession(definition,ClinicalJourneyMode.GuidedLearning);
            Enter(session,"R01_OFFICE");
            Assert.That(session.TryIntroduceLearningMethod("OF-01","record-comparison"),Is.True);
            Assert.That(session.TryIntroduceLearningMethod("OF-01","record-comparison"),Is.False);
            Enter(session,"R02_STORAGE");
            Assert.That(session.TryIntroduceLearningMethod("ST-02","record-comparison"),Is.False);
            Assert.That(session.LearningActionCount(ClinicalLearningAction.MethodShown),Is.EqualTo(1));

            var restarted=new ClinicalJourneySession(definition,ClinicalJourneyMode.GuidedLearning);
            Enter(restarted,"R01_OFFICE");
            Assert.That(restarted.TryIntroduceLearningMethod("OF-01","record-comparison"),Is.True);
            Assert.That(restarted.LearningActionCount(ClinicalLearningAction.MethodShown),Is.EqualTo(1));
            Assert.That(restarted.LearningAttempts(),Is.Empty);
        }

        [Test]
        public void FinalOfficeSubmissionFreezesGuidedSessionAndKeepsItReadable()
        {
            var definition=ClinicalJourneyConfiguration.Load();
            var session=new ClinicalJourneySession(definition,ClinicalJourneyMode.GuidedLearning);
            foreach(var room in definition.mainlineRoomIds.Skip(1))Enter(session,room);
            Assert.That(session.IsSubmitted,Is.False);

            Assert.That(session.TryFinishGuidedAtSummary(),Is.True);
            Assert.That(session.IsSubmitted,Is.True);
            Assert.That(session.IsFinished,Is.True);
            Assert.That(session.CanEdit,Is.False);
            Assert.That(session.TryRequestLearningHelp("OF-01","date",true),Is.False);
            Assert.That(session.TryIntroduceLearningMethod("OF-01","late-method"),Is.False);
            Assert.That(session.TryFinishGuidedAtSummary(),Is.False);
            Assert.That(session.TryGetTask("OF-01",out _),Is.True);
        }

        static void Enter(ClinicalJourneySession session,string room)
            =>Assert.That(session.TrySwitchRoom(room,true,true).Succeeded,Is.True,room);
    }
}
