using BotanicalGardenQR.Bootstrap;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class FullScriptGalleryTutorialTests
    {
        [Test]
        public void GestureAtlasMapsEachTutorialActionToOneSeparateCell()
        {
            Assert.That(FullScriptRoomVisit.GalleryGestureIndex(FullScriptGalleryTutorialStep.MoveToHandle), Is.EqualTo(0));
            Assert.That(FullScriptRoomVisit.GalleryGestureIndex(FullScriptGalleryTutorialStep.PinchHandle), Is.EqualTo(1));
            Assert.That(FullScriptRoomVisit.GalleryGestureIndex(FullScriptGalleryTutorialStep.DragRoom), Is.EqualTo(2));
            Assert.That(FullScriptRoomVisit.GalleryGestureIndex(FullScriptGalleryTutorialStep.ReleaseHandle), Is.EqualTo(3));
            Assert.That(FullScriptRoomVisit.GalleryGestureIndex(FullScriptGalleryTutorialStep.SelectRoom), Is.EqualTo(4));
            Assert.That(FullScriptRoomVisit.GalleryGestureIndex(FullScriptGalleryTutorialStep.Completed), Is.EqualTo(-1));
            Assert.That(FullScriptRoomVisit.GalleryGestureIndex(FullScriptGalleryTutorialStep.Skipped), Is.EqualTo(-1));
            Assert.That(FullScriptRoomVisit.GalleryGestureUv(FullScriptGalleryTutorialStep.MoveToHandle),
                Is.EqualTo(new UnityEngine.Rect(0, .5f, 1f / 3f, .5f)));
            Assert.That(FullScriptRoomVisit.GalleryGestureUv(FullScriptGalleryTutorialStep.SelectRoom),
                Is.EqualTo(new UnityEngine.Rect(1f / 3f, 0, 1f / 3f, .5f)));
            Assert.That(FullScriptRoomVisit.GalleryGestureUv(FullScriptGalleryTutorialStep.Skipped),
                Is.EqualTo(UnityEngine.Rect.zero));
        }

        [Test]
        public void TutorialAdvancesOnlyAfterTheRealHandlePinchDragReleaseAndFreshRoomPoke()
        {
            var tutorial = new FullScriptGalleryTutorial();
            tutorial.Begin();
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.MoveToHandle));
            Assert.That(tutorial.Instruction, Is.EqualTo("把手移到下方亮起的短把手处。"));

            tutorial.ObserveHandle(tracked: true, nearHandle: false, pinching: false);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.MoveToHandle));
            tutorial.ObserveHandle(tracked: true, nearHandle: true, pinching: false);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.PinchHandle));
            Assert.That(tutorial.Instruction, Is.EqualTo("拇指和食指靠近，轻轻捏住短把手。"));
            tutorial.ObserveHandle(tracked: true, nearHandle: true, pinching: true);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.DragRoom));
            Assert.That(tutorial.Instruction, Is.EqualTo("保持捏住，向左或向右移动手。"));

            tutorial.ObserveMovement(tracked: true, pinching: true, rotationDegrees: 2f);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.DragRoom));
            tutorial.ObserveRelease(tracked: true, pinching: false);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.PinchHandle),
                "Releasing before a visible drag cannot pass the drag step.");
            tutorial.ObserveHandle(tracked: true, nearHandle: true, pinching: true);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.DragRoom));
            tutorial.ObserveMovement(tracked: true, pinching: true, rotationDegrees: 1f);
            tutorial.ObserveMovement(tracked: true, pinching: true, rotationDegrees: 1f);
            tutorial.ObserveMovement(tracked: true, pinching: true, rotationDegrees: 1f);
            tutorial.ObserveMovement(tracked: true, pinching: true, rotationDegrees: 1f);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.DragRoom));
            tutorial.ObserveMovement(tracked: true, pinching: true, rotationDegrees: 1f);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.ReleaseHandle));
            Assert.That(tutorial.Instruction, Is.EqualTo("张开拇指和食指，停稳画廊。"));
            tutorial.ObserveRelease(tracked: true, pinching: true);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.ReleaseHandle));
            tutorial.ObserveRelease(tracked: true, pinching: false);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.SelectRoom));
            Assert.That(tutorial.Instruction, Is.EqualTo("伸出食指，轻触想去的房间图片。"));

            Assert.That(tutorial.ObserveSelection(freshPoke: false, dragging: false), Is.False);
            Assert.That(tutorial.ObserveSelection(freshPoke: true, dragging: true), Is.False);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.SelectRoom));
            Assert.That(tutorial.ObserveSelection(freshPoke: true, dragging: false), Is.True);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.Completed));
        }

        [Test]
        public void TrackingLossDuringADragRequiresReconfirmingTheHandleAndNeverCountsAsRelease()
        {
            var tutorial = new FullScriptGalleryTutorial();
            tutorial.Begin();
            tutorial.ObserveHandle(true, true, false);
            tutorial.ObserveHandle(true, true, true);
            tutorial.ObserveMovement(true, true, 8f);

            tutorial.OnTrackingLost();
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.MoveToHandle));
            tutorial.ObserveRelease(tracked: true, pinching: false);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.MoveToHandle));

            tutorial.ObserveHandle(true, true, false);
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.PinchHandle));
        }

        [Test]
        public void SkippingDoesNotCompleteTutorialAndReplayRestartsOnlyTheTutorial()
        {
            var tutorial = new FullScriptGalleryTutorial();
            tutorial.Begin();
            tutorial.Skip();

            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.Skipped));
            tutorial.Begin();
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.Skipped));

            tutorial.Replay();
            Assert.That(tutorial.Step, Is.EqualTo(FullScriptGalleryTutorialStep.MoveToHandle));
            Assert.That(tutorial.Instruction, Does.Contain("短把手"));
        }
    }
}
