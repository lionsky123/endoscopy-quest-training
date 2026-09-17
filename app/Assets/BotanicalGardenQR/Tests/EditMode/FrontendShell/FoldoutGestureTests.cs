using BotanicalGardenQR.Collection.Frontend;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode.FrontendShell
{
    public sealed class FoldoutGestureTests
    {
        static void MoveTo(FoldoutGesture gesture, float from, float to)
        {
            for (var i = 1; i <= 10; i++) gesture.Move(from + (to - from) * i / 10, .025f, true);
        }
        static void Hold(FoldoutGesture gesture, float axis)
        { for (var i = 0; i < 10; i++) gesture.Move(axis, .025f, true); }

        [TestCase(-1)] [TestCase(1)]
        public void EitherEdgeRequiresOpenThenCloseAndCompletesOnlyOnce(int side)
        {
            var gesture = new FoldoutGesture(.2f);
            gesture.Begin(0, side);
            Hold(gesture, 0);
            Assert.That(gesture.Completed, Is.False);
            MoveTo(gesture, 0, .2f * side); Hold(gesture, .2f * side);
            Assert.That(gesture.Revealed, Is.True);
            Assert.That(gesture.Completed, Is.False);
            MoveTo(gesture, .2f * side, 0); Hold(gesture, 0);
            Assert.That(gesture.Completed, Is.True);
            Assert.That(gesture.Move(0, .05f, true), Is.False);
        }

        [Test]
        public void ReleaseAndSwitchEdgeRetainsProgressWithoutSnap()
        {
            var gesture = new FoldoutGesture(.2f);
            gesture.Begin(0, 1); MoveTo(gesture, 0, .1f); gesture.End();
            gesture.Begin(5, -1); gesture.Move(5, .025f, true);
            Assert.That(gesture.Progress, Is.EqualTo(.5f).Within(.001f));
            MoveTo(gesture, 5, 4.9f); Hold(gesture, 4.9f);
            Assert.That(gesture.Revealed, Is.True);
            Assert.That(gesture.Completed, Is.False);
        }

        [TestCase(false, .11f)] [TestCase(true, 3f)] [TestCase(true, float.NaN)]
        public void TrackingLossOrJumpFreezesUntilFreshGrab(bool tracked, float axis)
        {
            var gesture = new FoldoutGesture(.2f);
            gesture.Begin(0, 1); MoveTo(gesture, 0, .1f);
            gesture.Move(axis, .025f, tracked);
            Assert.That(gesture.Progress, Is.EqualTo(.5f).Within(.001f));
            Assert.That(gesture.IsHeld, Is.False);
            Hold(gesture, .2f);
            Assert.That(gesture.Revealed, Is.False);
        }

        [Test]
        public void BriefThresholdCrossingAndReleaseCannotCollect()
        {
            var gesture = new FoldoutGesture(.2f);
            gesture.Begin(0, 1); MoveTo(gesture, 0, .18f); gesture.End();
            Assert.That(gesture.Revealed, Is.False);
            gesture.Begin(.18f, 1); MoveTo(gesture, .18f, 0); Hold(gesture, 0);
            Assert.That(gesture.Completed, Is.False);
        }
    }
}
