using BotanicalGardenQR.VisitorPrologue.Contracts;
using BotanicalGardenQR.VisitorPrologue.Runtime;
using NUnit.Framework;

namespace BotanicalGardenQR.VisitorPrologue.Tests.EditMode
{
    public sealed class VisitorPrologueControllerTests
    {
        [Test]
        public void OneInvitationReplacesTheOldStartAndSeedSequence()
        {
            using var p = VisitorPrologueModuleFactory.Create();
            p.Begin(); var epoch = p.CurrentState.Epoch;
            Assert.That(p.CurrentState.Phase, Is.EqualTo(VisitorProloguePhase.Invitation));
            Assert.That(p.RequestInvitation(epoch, VisitorPrologueInputModality.PalmHold).Succeeded, Is.False);
            p.ReportHandAvailability(true);
            Assert.That(p.RequestInvitation(epoch, VisitorPrologueInputModality.PalmHold).Succeeded, Is.True);
            Assert.That(p.CurrentState.Phase, Is.EqualTo(VisitorProloguePhase.Arrival));
            var version = p.CurrentState.Version;
            Assert.That(p.RequestInvitation(epoch, VisitorPrologueInputModality.GazeFallback).Succeeded, Is.False);
            Assert.That(p.CurrentState.Version, Is.EqualTo(version));
        }
        [TestCase(true)] [TestCase(false)]
        public void ActualArrivalAndBookOpeningMayCompleteInEitherOrder(bool arrivalFirst)
        {
            using var p = EnterArrival(); var epoch = p.CurrentState.Epoch;
            if (arrivalFirst) p.ReportArrivalReady(epoch); else p.ReportBookOpened(epoch);
            Assert.That(p.CurrentState.Phase, Is.EqualTo(VisitorProloguePhase.Arrival));
            Assert.That(p.BeginDialogue(epoch, 4).Succeeded, Is.False);
            if (arrivalFirst) p.ReportBookOpened(epoch); else p.ReportArrivalReady(epoch);
            Assert.That(p.CurrentState.Phase, Is.EqualTo(VisitorProloguePhase.Encounter));
            Assert.That(p.BeginDialogue(epoch, 4).Succeeded, Is.True);
        }
        [TestCase(VisitorEncounterReply.AskAboutWorld)]
        [TestCase(VisitorEncounterReply.PromiseToExplore)]
        public void DifferentRepliesAreOwnedByPrologueAndRejoinOneExit(VisitorEncounterReply reply)
        {
            using var p = EnterArrival(); var epoch = p.CurrentState.Epoch;
            p.ReportArrivalReady(epoch); p.ReportBookOpened(epoch); p.BeginDialogue(epoch, 4);
            p.AdvanceDialogue(epoch, p.CurrentState.Version);
            var choiceRevision = p.CurrentState.Version;
            Assert.That(p.AdvanceDialogue(epoch, choiceRevision).FailureCode, Is.EqualTo(VisitorPrologueFailureCode.InvalidReply));
            Assert.That(p.ChooseReply(epoch, choiceRevision, reply).Succeeded, Is.True);
            Assert.That(p.CurrentState.Reply, Is.EqualTo(reply));
            Assert.That(p.CurrentState.DialoguePageIndex, Is.EqualTo(2));
            Assert.That(p.ChooseReply(epoch, choiceRevision, reply).FailureCode, Is.EqualTo(VisitorPrologueFailureCode.StaleRevision));
            p.AdvanceDialogue(epoch, p.CurrentState.Version);
            Assert.That(p.CurrentState.IsExplorationReady, Is.False);
            p.AdvanceDialogue(epoch, p.CurrentState.Version);
            Assert.That(p.CurrentState.IsExplorationReady, Is.True);
            Assert.That(p.ReplayDialogue(epoch).Succeeded, Is.False);
        }
        [Test]
        public void GazeFallbackCannotCompeteWithATrackedPalm()
        {
            using var p = VisitorPrologueModuleFactory.Create(); p.Begin(); var epoch = p.CurrentState.Epoch;
            Assert.That(p.RequestInvitation(epoch, VisitorPrologueInputModality.GazeFallback).Succeeded, Is.False);
            p.EnableGazeFallback(); p.ReportHandAvailability(true);
            Assert.That(p.RequestInvitation(epoch, VisitorPrologueInputModality.GazeFallback).Succeeded, Is.False);
            p.ReportHandAvailability(false);
            Assert.That(p.RequestInvitation(epoch, VisitorPrologueInputModality.GazeFallback).Succeeded, Is.True);
        }
        [Test]
        public void RetryRejectsOldReadinessAndReplayOnlyChangesTheCurrentEncounter()
        {
            using var p = EnterArrival(); var old = p.CurrentState.Epoch;
            p.Fail("test interruption"); p.Retry();
            Assert.That(p.ReportBookOpened(old).FailureCode, Is.EqualTo(VisitorPrologueFailureCode.StaleEpoch));
            Assert.That(p.ReportArrivalReady(old).FailureCode, Is.EqualTo(VisitorPrologueFailureCode.StaleEpoch));
            var epoch = p.CurrentState.Epoch;
            p.RequestInvitation(epoch, VisitorPrologueInputModality.PalmHold);
            p.ReportBookOpened(epoch); p.ReportArrivalReady(epoch); p.BeginDialogue(epoch, 4);
            p.AdvanceDialogue(epoch, p.CurrentState.Version);
            p.ChooseReply(epoch, p.CurrentState.Version, VisitorEncounterReply.PromiseToExplore);
            p.ReplayDialogue(epoch);
            Assert.That(p.CurrentState.DialoguePageIndex, Is.Zero);
            Assert.That(p.CurrentState.Reply, Is.EqualTo(VisitorEncounterReply.None));
            Assert.That(p.CurrentState.Phase, Is.EqualTo(VisitorProloguePhase.Encounter));
        }
        static VisitorPrologueController EnterArrival()
        {
            var p = VisitorPrologueModuleFactory.Create(); p.Begin(); p.ReportHandAvailability(true);
            p.RequestInvitation(p.CurrentState.Epoch, VisitorPrologueInputModality.PalmHold);
            return p;
        }
    }
}
