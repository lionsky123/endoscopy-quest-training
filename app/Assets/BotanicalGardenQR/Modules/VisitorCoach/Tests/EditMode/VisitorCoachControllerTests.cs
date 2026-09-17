using BotanicalGardenQR.VisitorCoach.Contracts;
using BotanicalGardenQR.VisitorCoach.Runtime;
using NUnit.Framework;

namespace BotanicalGardenQR.VisitorCoach.Tests.EditMode
{
    public sealed class VisitorCoachControllerTests
    {
        [Test]
        public void TimersEscalateVisibleCuesButPauseWhileSuppressed()
        {
            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var session = new VisitorCoachSessionId("visitor-a");
            Assert.That(coach.BeginSession(session).Succeeded, Is.True);
            Assert.That(coach.Advance(session, 0f, false).Succeeded, Is.True);
            Assert.That(coach.OfferOpportunity(Opportunity(session, "palm", VisitorCoachCapability.PalmRecall, 10)).Succeeded, Is.True);
            Assert.That(coach.CurrentState.IsCueVisible, Is.True);
            Assert.That(coach.CurrentState.HintLevel, Is.EqualTo(VisitorCoachHintLevel.Initial));

            coach.Advance(session, 4f, false);
            Assert.That(coach.CurrentState.HintLevel, Is.EqualTo(VisitorCoachHintLevel.Direct));
            coach.Advance(session, 100f, true);
            Assert.That(coach.CurrentState.IsCueVisible, Is.False);
            coach.Advance(session, 0f, false);
            Assert.That(coach.CurrentState.IsCueVisible, Is.True);
            Assert.That(coach.CurrentState.HintLevel, Is.EqualTo(VisitorCoachHintLevel.Direct),
                "Hidden time must not make a visitor look stuck.");
            coach.Advance(session, 4f, false);
            Assert.That(coach.CurrentState.HintLevel, Is.EqualTo(VisitorCoachHintLevel.Demonstration));
            coach.Advance(session, 4f, false);
            Assert.That(coach.CurrentState.HintLevel, Is.EqualTo(VisitorCoachHintLevel.Recovery));
            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.PalmRecall),
                Is.EqualTo(VisitorCoachMastery.Prompted));
        }

        [Test]
        public void DismissingACueNeverProvesTheCapability()
        {
            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var session = new VisitorCoachSessionId("visitor-a");
            coach.BeginSession(session);
            coach.Advance(session, 0f, false);
            var first = Opportunity(session, "artifact-grab-a", VisitorCoachCapability.ArtifactGrab, 20);
            coach.OfferOpportunity(first);

            coach.DismissCue(session, first.Id);

            Assert.That(coach.CurrentState.IsCueVisible, Is.False);
            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.ArtifactGrab),
                Is.EqualTo(VisitorCoachMastery.Prompted));
            coach.OfferOpportunity(Opportunity(session, "artifact-grab-b", VisitorCoachCapability.ArtifactGrab, 20));
            Assert.That(coach.CurrentState.IsCueVisible, Is.True,
                "A later legitimate opportunity may teach a capability that was not proven.");
        }

        [Test]
        public void OnlyTheHighestPriorityStableOpportunityIsPublished()
        {
            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var session = new VisitorCoachSessionId("visitor-a");
            coach.BeginSession(session);
            coach.Advance(session, 0f, false);
            coach.OfferOpportunity(Opportunity(session, "panorama", VisitorCoachCapability.PanoramaExit, 5));
            coach.OfferOpportunity(Opportunity(session, "artifact", VisitorCoachCapability.ArtifactGrab, 30));
            coach.OfferOpportunity(Opportunity(session, "palm", VisitorCoachCapability.PalmRecall, 30));

            Assert.That(coach.CurrentState.OpportunityId,
                Is.EqualTo(new VisitorCoachOpportunityId("artifact")),
                "Equal priorities keep the earliest stable opportunity.");
            coach.WithdrawOpportunity(session, new VisitorCoachOpportunityId("artifact"));
            Assert.That(coach.CurrentState.OpportunityId,
                Is.EqualTo(new VisitorCoachOpportunityId("palm")));
        }

        [Test]
        public void RealProofRemovesEveryOpportunityForThatCapability()
        {
            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var session = new VisitorCoachSessionId("visitor-a");
            coach.BeginSession(session);
            coach.Advance(session, 0f, false);
            coach.OfferOpportunity(Opportunity(session, "place-a", VisitorCoachCapability.ArtifactPlace, 20));
            coach.OfferOpportunity(Opportunity(session, "place-b", VisitorCoachCapability.ArtifactPlace, 25));

            coach.ReportProven(session, VisitorCoachCapability.ArtifactPlace);

            Assert.That(coach.CurrentState.IsCueVisible, Is.False);
            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.ArtifactPlace),
                Is.EqualTo(VisitorCoachMastery.Proven));
            coach.OfferOpportunity(Opportunity(session, "place-c", VisitorCoachCapability.ArtifactPlace, 50));
            Assert.That(coach.CurrentState.IsCueVisible, Is.False,
                "A proven capability must not be re-prompted in the same session.");
        }

        [Test]
        public void ANewSessionResetsMasteryAndRejectsOldFacts()
        {
            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var first = new VisitorCoachSessionId("visitor-a");
            var second = new VisitorCoachSessionId("visitor-b");
            coach.BeginSession(first);
            coach.ReportProven(first, VisitorCoachCapability.Poke);

            coach.BeginSession(second);

            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.Poke),
                Is.EqualTo(VisitorCoachMastery.Unknown));
            var stale = coach.ReportProven(first, VisitorCoachCapability.GazeDwell);
            Assert.That(stale.Succeeded, Is.False);
            Assert.That(stale.FailureCode, Is.EqualTo(VisitorCoachFailureCode.StaleSession));
            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.GazeDwell),
                Is.EqualTo(VisitorCoachMastery.Unknown));
        }

        [Test]
        public void DialogueRequiresOneExplicitAdvancePerPageAndReplayPreservesMastery()
        {
            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var session = new VisitorCoachSessionId("visitor-dialogue");
            var opportunity = new VisitorCoachOpportunity(
                session,
                new VisitorCoachOpportunityId("qr-dialogue"),
                VisitorCoachCapability.QrConfirm,
                VisitorCoachCueKeys.QrConfirm,
                100,
                dialoguePageCount: 3);
            coach.BeginSession(session);
            coach.Advance(session, 0f, false);
            coach.OfferOpportunity(opportunity);

            Assert.That(coach.CurrentState.IsDialogueOpen, Is.True);
            Assert.That(coach.CurrentState.DialoguePageIndex, Is.Zero);
            Assert.That(coach.AdvanceDialogue(session, opportunity.Id).Succeeded, Is.True);
            Assert.That(coach.CurrentState.DialoguePageIndex, Is.EqualTo(1));
            Assert.That(coach.AdvanceDialogue(session, opportunity.Id).Succeeded, Is.True);
            Assert.That(coach.CurrentState.DialoguePageIndex, Is.EqualTo(2));
            Assert.That(coach.AdvanceDialogue(session, opportunity.Id).Succeeded, Is.True);
            Assert.That(coach.CurrentState.IsDialogueOpen, Is.False,
                "The final click closes only the explanation and hands control to the real task.");

            var mastery = coach.CurrentState.GetMastery(VisitorCoachCapability.QrConfirm);
            var hint = coach.CurrentState.HintLevel;
            Assert.That(coach.ReplayDialogue(session, opportunity.Id).Succeeded, Is.True);
            Assert.That(coach.CurrentState.IsDialogueOpen, Is.True);
            Assert.That(coach.CurrentState.DialoguePageIndex, Is.Zero);
            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.QrConfirm), Is.EqualTo(mastery));
            Assert.That(coach.CurrentState.HintLevel, Is.EqualTo(hint));
        }

        [Test]
        public void StaleOpportunityCannotAdvanceTheCurrentDialogue()
        {
            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var session = new VisitorCoachSessionId("visitor-dialogue-stale");
            coach.BeginSession(session);
            coach.Advance(session, 0f, false);
            var current = new VisitorCoachOpportunity(
                session,
                new VisitorCoachOpportunityId("current"),
                VisitorCoachCapability.QrConfirm,
                VisitorCoachCueKeys.QrConfirm,
                100,
                dialoguePageCount: 2);
            coach.OfferOpportunity(current);

            var stale = coach.AdvanceDialogue(
                session,
                new VisitorCoachOpportunityId("stale"));

            Assert.That(stale.Succeeded, Is.False);
            Assert.That(coach.CurrentState.DialoguePageIndex, Is.Zero);
            Assert.That(coach.CurrentState.IsDialogueOpen, Is.True);
        }

        static VisitorCoachOpportunity Opportunity(
            VisitorCoachSessionId session,
            string id,
            VisitorCoachCapability capability,
            int priority)
            => new VisitorCoachOpportunity(
                session,
                new VisitorCoachOpportunityId(id),
                capability,
                id + ".cue",
                priority);
    }
}
