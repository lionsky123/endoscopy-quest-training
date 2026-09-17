using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.JourneyNavigation.Contracts;
using BotanicalGardenQR.JourneyNavigation.Runtime;
using NUnit.Framework;

namespace BotanicalGardenQR.JourneyNavigation.Tests.EditMode
{
    public sealed class JourneyNavigationControllerTests
    {
        [TestCase("qr")]
        [TestCase("fieldbook")]
        public void AnyFirstQrBecomesCurrentAndCompletionsCountUniqueContent(string sourceKind)
        {
            using var journey = new JourneyNavigationController();
            var session = JourneySessionId.CreateNew();
            journey.BeginJourney(session);
            Assert.That(journey.CurrentState.CurrentScene.IsValid, Is.False);
            Assert.That(journey.CurrentState.ProgressState, Is.EqualTo(JourneyProgressState.AwaitingQr));
            foreach (var name in new[] { "bottle_tree", "giant_saguaro", "bottle_tree" })
            {
                var scene = new SceneId(name);
                var content = SessionToken.CreateNew();
                Assert.That(journey.AcceptContentOpened(new ContentOpenedFact(content, session, scene, new SourceKind(sourceKind), "verified_entry")), Is.True);
                Assert.That(journey.CurrentState.CurrentScene, Is.EqualTo(scene));
                Assert.That(journey.Dispatch(JourneyIntent.ContinueToNext).Succeeded, Is.False);
                Assert.That(journey.AcceptContentClosed(new ContentClosedFact(content, session, scene, true)), Is.True);
                Assert.That(journey.Dispatch(JourneyIntent.ContinueToNext).Succeeded, Is.True);
                Assert.That(journey.Dispatch(JourneyIntent.ContinueToNext).Succeeded, Is.False);
                Assert.That(journey.CurrentState.CurrentScene, Is.EqualTo(scene), "Completion must not preselect another plant.");
            }
            Assert.That(journey.CurrentState.CompletedCount, Is.EqualTo(2));
            Assert.That(journey.CurrentState.CompletionRevision, Is.EqualTo(3));
            journey.BeginJourney(JourneySessionId.CreateNew());
            Assert.That(journey.CurrentState.CompletedCount, Is.Zero);
            Assert.That(journey.CurrentState.CurrentScene.IsValid, Is.False);
        }

        [Test]
        public void StaleCloseWrongSessionAndRecallCannotCompleteANewScan()
        {
            using var journey = new JourneyNavigationController();
            var session = JourneySessionId.CreateNew();
            var scene = new SceneId("bottle_tree");
            var old = SessionToken.CreateNew();
            var current = SessionToken.CreateNew();
            journey.BeginJourney(session);
            Assert.That(journey.AcceptContentOpened(new ContentOpenedFact(old, JourneySessionId.CreateNew(), scene, RecognitionSourceKinds.Qr, "entry")), Is.False);
            journey.AcceptContentOpened(new ContentOpenedFact(old, session, scene, RecognitionSourceKinds.Qr, "entry"));
            journey.AcceptContentOpened(new ContentOpenedFact(current, session, scene, RecognitionSourceKinds.Qr, "entry"));
            Assert.That(journey.AcceptContentClosed(new ContentClosedFact(old, session, scene, true)), Is.False);
            Assert.That(journey.Dispatch(JourneyIntent.ContinueToNext).Succeeded, Is.False);
            journey.AcceptContentClosed(new ContentClosedFact(current, session, scene, true));
            Assert.That(journey.Dispatch(JourneyIntent.RecallCurrentPoint).RequiresActivationRecall, Is.True);
            Assert.That(journey.AcceptContentOpened(new ContentOpenedFact(SessionToken.CreateNew(), session, scene, RecognitionSourceKinds.Qr, "entry", true)), Is.True);
            Assert.That(journey.CurrentState.CompletionRevision, Is.Zero);
        }
    }
}
