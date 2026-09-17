using BotanicalGardenQR.VisitorAtlasHub.Backend;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode.VisitorAtlasHub
{
    public sealed class VisitorAtlasHubStateTests
    {
        [Test]
        public void InitializationPrerequisitesAreIndependentAndPendingRevealCommitsOnlyOnce()
        {
            var state = new VisitorAtlasHubLifecycle();
            state.BeginInitialization();
            state.SetInteractionGate(true);

            Assert.That(state.RequestSummon(), Is.True);
            Assert.That(state.RequestSummon(), Is.False, "Only one pending reveal is retained.");
            state.MarkMapReady();
            Assert.That(state.Phase, Is.EqualTo(VisitorAtlasHubPhase.InitializingHidden));
            state.MarkPoseReady();
            Assert.That(state.Phase, Is.EqualTo(VisitorAtlasHubPhase.Revealing));
            state.CompleteReveal();

            Assert.That(state.Phase, Is.EqualTo(VisitorAtlasHubPhase.Visible));
            Assert.That(state.RequestSummon(), Is.False, "A held palm never toggles or replays a visible hub.");
        }

        [Test]
        public void GateLossDropsPendingButNeverHidesAnAlreadyVisibleHub()
        {
            var state = ReadyHidden();
            state.SetInteractionGate(true);
            Assert.That(state.RequestSummon(), Is.True);
            state.CompleteReveal();
            Assert.That(state.Phase, Is.EqualTo(VisitorAtlasHubPhase.Visible));

            state.SetInteractionGate(false);
            Assert.That(state.Phase, Is.EqualTo(VisitorAtlasHubPhase.Visible));

            Assert.That(state.Hide(), Is.True);
            state.SetInteractionGate(true);
            Assert.That(state.Phase, Is.EqualTo(VisitorAtlasHubPhase.ReadyHidden));
        }

        [Test]
        public void GateLossDuringInitializationCannotRevealLater()
        {
            var state = new VisitorAtlasHubLifecycle();
            state.BeginInitialization();
            state.SetInteractionGate(true);
            state.RequestSummon();
            state.SetInteractionGate(false);
            state.MarkPoseReady();
            state.MarkMapReady();

            Assert.That(state.Phase, Is.EqualTo(VisitorAtlasHubPhase.ReadyHidden));
            state.SetInteractionGate(true);
            Assert.That(state.Phase, Is.EqualTo(VisitorAtlasHubPhase.ReadyHidden),
                "A dropped pending gesture must not become a delayed surprise reveal.");
        }

        [Test]
        public void BookTransactionRejectsDuplicatesAndStaleAnimationCallbacks()
        {
            var book = new VisitorAtlasHubBookTransaction(0.5f);

            Assert.That(book.TryBegin(out var generation), Is.True);
            Assert.That(book.TryBegin(out _), Is.False);
            Assert.That(book.CompleteAnimation(generation + 1), Is.False);
            Assert.That(book.CompleteAnimation(generation), Is.True);
            Assert.That(book.State, Is.EqualTo(VisitorAtlasHubBookState.AwaitingBrowseConfirmation));
            Assert.That(book.ConfirmBrowse(generation + 1), Is.False);
            Assert.That(book.ConfirmBrowse(generation), Is.True);
            Assert.That(book.State, Is.EqualTo(VisitorAtlasHubBookState.OpenLocked));

            Assert.That(book.Reset(), Is.True);
            Assert.That(book.CompleteAnimation(generation), Is.False,
                "A completion callback from a canceled generation cannot open Collection.");
        }

        [Test]
        public void BookConfirmationTimeoutRollsBackInsteadOfOpeningLater()
        {
            var book = new VisitorAtlasHubBookTransaction(0.5f);
            book.TryBegin(out var generation);
            book.CompleteAnimation(generation);

            Assert.That(book.Advance(0.49f), Is.False);
            Assert.That(book.Advance(0.02f), Is.True);
            Assert.That(book.State, Is.EqualTo(VisitorAtlasHubBookState.ClosedInteractive));
            Assert.That(book.ConfirmBrowse(generation), Is.False);
        }

        static VisitorAtlasHubLifecycle ReadyHidden()
        {
            var state = new VisitorAtlasHubLifecycle();
            state.BeginInitialization();
            state.MarkPoseReady();
            state.MarkMapReady();
            return state;
        }
    }
}
