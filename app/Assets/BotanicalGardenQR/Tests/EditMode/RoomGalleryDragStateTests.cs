using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.FrontendShell.Runtime;
using NUnit.Framework;
using Oculus.Interaction.Input;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class RoomGalleryDragStateTests
    {
        [Test]
        public void GalleryIgnoresHandReferenceWithoutBoundTrackingData()
        {
            var source = new GameObject("Unbound gallery HandRef test");
            try
            {
                var hand = source.AddComponent<HandRef>();
                Assert.That(hand.Hand, Is.Null);
                Assert.That(FullScriptRoomVisit.TryReadGalleryHand(new IHand[] { hand }, 0,
                    out var pinchPoint, out var pinching), Is.False);
                Assert.That(pinchPoint, Is.EqualTo(Vector3.zero));
                Assert.That(pinching, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void DragStartsOnlyForTrackedPinchAtShortHandle()
        {
            var state = new FullScriptRoomGalleryDragState();
            var handle = new Vector3(0, 0, .5f);

            Assert.That(state.TryBegin(0, true, true, new Vector3(.2f, 0, .5f), handle), Is.False);
            Assert.That(state.TryBegin(0, false, true, handle, handle), Is.False);
            Assert.That(state.TryBegin(0, true, false, handle, handle), Is.False);
            Assert.That(state.TryBegin(0, true, true, new Vector3(.05f, 0, .5f), handle), Is.True);
        }

        [Test]
        public void DragStopsOnReleaseOrTrackingLossWithoutCarryingMotionForward()
        {
            var state = new FullScriptRoomGalleryDragState();
            var handle = Vector3.zero;
            Assert.That(state.TryBegin(0, true, true, handle, handle), Is.True);

            Assert.That(state.Sample(1, true, true, Vector3.right, Vector3.right, out var otherHandDelta), Is.False);
            Assert.That(otherHandDelta, Is.Zero);
            Assert.That(state.Sample(0, true, true, Vector3.right * .1f, Vector3.right, out var movingDelta), Is.True);
            Assert.That(movingDelta, Is.EqualTo(-12f).Within(.001f));

            Assert.That(state.Sample(0, true, false, Vector3.right * .1f, Vector3.right, out var releaseDelta), Is.False);
            Assert.That(releaseDelta, Is.Zero);
            Assert.That(state.Sample(0, true, false, Vector3.right, Vector3.right, out var afterReleaseDelta), Is.False);
            Assert.That(afterReleaseDelta, Is.Zero);

            Assert.That(state.TryBegin(1, true, true, Vector3.zero, handle), Is.True);
            Assert.That(state.Sample(1, false, true, Vector3.right, Vector3.right, out var trackingLossDelta), Is.False);
            Assert.That(trackingLossDelta, Is.Zero);
            Assert.That(state.IsDragging, Is.False);
        }

        [Test]
        public void PinchCanCatchTheVisibleHandleFromInFrontWithoutCatchingTheNearbyCard()
        {
            var state = new FullScriptRoomGalleryDragState();
            var handle = new Vector3(0, 0, .87f);
            Assert.That(state.TryBegin(0, true, true, new Vector3(.14f, 0, .79f), handle), Is.True,
                "A natural pinch at the handle end remains in front of the drawn surface.");
            state.Cancel();
            Assert.That(state.TryBegin(0, true, true, new Vector3(.28f, 0, .79f), handle), Is.False);
        }

        [Test]
        public void RoomCardNeedsAStableFreshPokeAndCancelsOnWithdrawal()
        {
            var press = new ClinicalPressHoldState();
            press.Hover(12, 0f);
            Assert.That(press.Select(12, .03f, .08f), Is.True);
            Assert.That(press.TryCommit(.07f, .2f), Is.False,
                "An early press may latch, but a passing finger cannot choose a room.");
            press.Unhover(12);
            press.Hover(12, 1f);
            Assert.That(press.Select(12, 1.1f, .08f), Is.True);
            Assert.That(press.Select(12, 1.2f, .08f), Is.False,
                "Repeated SDK select events must not restart the confirmation timer.");
            Assert.That(press.TryCommit(1.23f, .2f), Is.False);
            press.Unselect(12);
            Assert.That(press.TryCommit(1.4f, .2f), Is.False, "Lifting before confirmation cancels the room change.");
            press.Unhover(12);
            press.Hover(12, 2f);
            Assert.That(press.Select(12, 2.1f, .08f), Is.True);
            Assert.That(press.TryCommit(2.31f, .2f), Is.True);
            Assert.That(press.TryCommit(2.5f, .2f), Is.False, "One poke confirms once.");
            press.Cancel();
            Assert.That(press.Select(12, 2.6f, .08f), Is.False, "Rearming needs a fresh approach.");
        }

        [Test]
        public void FastIntentionalPokeCanFinishItsHoldWithoutASecondSelectEvent()
        {
            var press = new ClinicalPressHoldState();
            press.Hover(3, 0f);
            Assert.That(press.Select(3, .01f, .08f), Is.True);
            Assert.That(press.TryCommit(.27f, .2f), Is.False);
            Assert.That(press.TryCommit(.29f, .2f), Is.True,
                "A fast approach should confirm once the finger remains pressed through both timing gates.");
        }
    }
}
