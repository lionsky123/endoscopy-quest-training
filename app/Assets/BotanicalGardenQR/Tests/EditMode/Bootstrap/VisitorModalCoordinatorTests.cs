using System;
using System.Collections.Generic;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.FrontendShell.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode.Bootstrap
{
    public sealed class VisitorToolPreparationTests
    {
        [Test]
        public void IntroductionAndExplicitDepartureAreSeparateAndCommitOnce()
        {
            using var preparation = new VisitorToolPreparation();
            var changes = 0;
            preparation.Changed += () => changes++;
            preparation.AdvanceExplanation();
            Assert.That(changes, Is.Zero);
            preparation.Begin();
            preparation.Begin();
            Assert.That(preparation.IsExplanationOpen, Is.True);
            preparation.AdvanceExplanation();
            Assert.That(preparation.HasExited, Is.False);
            Assert.That(preparation.IsPracticing, Is.True);
            preparation.AdvanceExplanation();
            preparation.ReportHidden();
            Assert.That(preparation.Phase, Is.EqualTo(VisitorToolPreparationPhase.AwaitingSummon));
            preparation.ReportSummoned();
            Assert.That(preparation.Phase, Is.EqualTo(VisitorToolPreparationPhase.AwaitingDismiss));
            preparation.ReportHidden();
            preparation.AdvanceExplanation();
            Assert.That(preparation.HasExited, Is.True);
            Assert.That(changes, Is.EqualTo(5));
            preparation.AdvanceExplanation();
            preparation.Begin();
            Assert.That(changes, Is.EqualTo(5));
        }

        [Test]
        public void HelpAfterDepartureNeverRelocksExploration()
        {
            using var preparation = new VisitorToolPreparation();
            preparation.Begin();
            preparation.AdvanceExplanation();
            preparation.DeferPractice();
            Assert.That(preparation.HasSummoned, Is.False);
            preparation.AdvanceExplanation();
            preparation.Replay();
            Assert.That(preparation.HasExited, Is.True);
            Assert.That(preparation.IsActive, Is.False);
            Assert.That(preparation.IsExplanationOpen, Is.True);
            preparation.AdvanceExplanation();
            Assert.That(preparation.HasExited, Is.True);
            Assert.That(preparation.IsExplanationOpen, Is.False);
        }
    }

    public sealed class VisitorModalCoordinatorTests
    {
        [Test]
        public void ToolPracticeLeavesRealHubAvailableWhileKeepingQrAndStartupPromptsClosed()
        {
            var practice = VisitorModalCoordinator.Resolve(new VisitorModalFacts(1, true, true,
                toolPreparationExited: false));
            Assert.That(practice.HubInteractionAllowed, Is.True);
            Assert.That(practice.RecognitionAllowed, Is.False);
            Assert.That(practice.ShellSuppressed, Is.True);
            Assert.That(practice.CoachCuesSuppressed, Is.True);
            var exited = VisitorModalCoordinator.Resolve(new VisitorModalFacts(2, true, true,
                toolPreparationExited: true));
            Assert.That(exited.RecognitionAllowed, Is.True);
            Assert.That(exited.HubInteractionAllowed, Is.True);
        }

        [TestCase(CollectionPresentationSurfaceKind.Hidden, true, false, true)]
        [TestCase(CollectionPresentationSurfaceKind.ArtifactOffer, false, false, false)]
        [TestCase(CollectionPresentationSurfaceKind.Reward, false, true, false)]
        [TestCase(CollectionPresentationSurfaceKind.Browse, true, true, false)]
        public void InitialSurfaceSnapshotDrivesDistinctQrCoachAndHubDecisions(
            CollectionPresentationSurfaceKind surface, bool qr, bool coachSuppressed, bool hub)
        {
            var environment = new EnvironmentStub(new VisitorModalFacts(1, true, true, collectionSurface: surface));
            using var owner = new VisitorModalCoordinator(environment);
            Assert.That(owner.Current.RecognitionAllowed, Is.EqualTo(qr));
            Assert.That(owner.Current.CoachCuesSuppressed, Is.EqualTo(coachSuppressed));
            Assert.That(owner.Current.HubInteractionAllowed, Is.EqualTo(hub));
            Assert.That(environment.Applied.Count, Is.EqualTo(1), "Initial facts are applied without waiting for a later UI event.");
        }

        [TestCase(VisitorDialogueOwner.Coach, false)]
        [TestCase(VisitorDialogueOwner.Prologue, true)]
        public void CoachDialogueDoesNotSuppressItsOwnCue(VisitorDialogueOwner dialogueOwner, bool suppressed)
        {
            using var owner = new VisitorModalCoordinator(new EnvironmentStub(
                new VisitorModalFacts(1, true, true, dialogueOwner: dialogueOwner)));
            Assert.That(owner.CoachCuesSuppressed, Is.EqualTo(suppressed));
            Assert.That(owner.Current.RecognitionAllowed, Is.False);
            Assert.That(owner.Current.BrowseOpeningAllowed, Is.False);
            Assert.That(owner.Current.HubInteractionAllowed, Is.False);
        }

        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]
        public void HubRequiresExplorationAndClosedContent(bool closed, bool ready, bool allowed)
        {
            using var owner = new VisitorModalCoordinator(new EnvironmentStub(new VisitorModalFacts(1, closed, ready)));
            Assert.That(owner.Current.HubInteractionAllowed, Is.EqualTo(allowed));
            if (!ready)
            {
                Assert.That(owner.Current.RecognitionAllowed, Is.False, "A hidden startup surface cannot authorize scanning before exploration.");
                Assert.That(owner.Current.BrowseOpeningAllowed, Is.False);
            }
        }

        [Test]
        public void ContentOpeningClosesBrowseThroughTheSameBoundaryAndPublishesOnlySettledPolicy()
        {
            var environment = new EnvironmentStub(new VisitorModalFacts(1, true, true));
            using var owner = new VisitorModalCoordinator(environment);
            var published = new List<VisitorModalPolicy>();
            owner.PolicyChanged += published.Add;
            environment.BeforeApply = policy =>
            {
                if (policy.CloseBrowse) environment.Publish(new VisitorModalFacts(3, false, true));
            };
            environment.Publish(new VisitorModalFacts(2, false, true, collectionSurface: CollectionPresentationSurfaceKind.Browse));
            Assert.That(published.Count, Is.EqualTo(1));
            Assert.That(published[0].CloseBrowse, Is.False);
            Assert.That(published[0].HubInteractionAllowed, Is.False);
            Assert.That(environment.Current.CollectionSurface, Is.EqualTo(CollectionPresentationSurfaceKind.Hidden));
        }

        [Test]
        public void ReentrantHigherSurfaceCannotPublishOrCommitTheOldEnablingPolicy()
        {
            var environment = new EnvironmentStub(new VisitorModalFacts(1, true, true, prologueVisible: true));
            using var owner = new VisitorModalCoordinator(environment);
            var published = new List<VisitorModalPolicy>();
            owner.PolicyChanged += published.Add;
            environment.BeforeApply = policy =>
            {
                environment.BeforeApply = null;
                environment.Publish(new VisitorModalFacts(3, true, true, collectionSurface: CollectionPresentationSurfaceKind.Reward));
            };
            environment.Publish(new VisitorModalFacts(2, true, true));
            Assert.That(environment.Applied.TrueForAll(policy => !policy.RecognitionAllowed), Is.True);
            Assert.That(published.TrueForAll(policy => !policy.RecognitionAllowed && !policy.HubInteractionAllowed), Is.True);
            Assert.That(owner.Current.ShellSuppressed, Is.True);
        }

        [Test]
        public void InitialApplyFailureReleasesSubscription()
        {
            var environment = new EnvironmentStub(new VisitorModalFacts(1, true, true));
            environment.BeforeApply = _ => throw new InvalidOperationException("injected");
            Assert.Throws<InvalidOperationException>(() => new VisitorModalCoordinator(environment));
            Assert.That(environment.LeaseDisposals, Is.EqualTo(1));
        }

        [Test]
        public void DisposeIsIdempotentAndQueuedOldCallbacksCannotRestoreAvailability()
        {
            var environment = new EnvironmentStub(new VisitorModalFacts(1, true, true));
            var owner = new VisitorModalCoordinator(environment);
            var queued = environment.Callback;
            owner.Dispose();
            var count = environment.Applied.Count;
            owner.Dispose();
            queued();
            Assert.That(environment.LeaseDisposals, Is.EqualTo(1));
            Assert.That(environment.Applied.Count, Is.EqualTo(count));
            Assert.That(owner.Current.RecognitionAllowed, Is.False);
            Assert.That(owner.Current.HubInteractionAllowed, Is.False);
        }

        sealed class EnvironmentStub : IVisitorModalEnvironment
        {
            public EnvironmentStub(VisitorModalFacts state) => Current = state;
            public VisitorModalFacts Current { get; private set; }
            public readonly List<VisitorModalPolicy> Applied = new List<VisitorModalPolicy>();
            public Action Callback;
            public Action<VisitorModalPolicy> BeforeApply;
            public int LeaseDisposals;
            public IDisposable Observe(Action changed)
            {
                Callback = changed;
                changed();
                return new Lease(() => { LeaseDisposals++; Callback = null; });
            }
            public void Publish(VisitorModalFacts state) { Current = state; Callback?.Invoke(); }
            public void Apply(VisitorModalPolicy policy, long expectedRevision)
            {
                BeforeApply?.Invoke(policy);
                if (Current.Revision == expectedRevision) Applied.Add(policy);
            }
            sealed class Lease : IDisposable
            {
                Action _dispose;
                public Lease(Action dispose) => _dispose = dispose;
                public void Dispose() { var action = _dispose; _dispose = null; action?.Invoke(); }
            }
        }
    }
}
