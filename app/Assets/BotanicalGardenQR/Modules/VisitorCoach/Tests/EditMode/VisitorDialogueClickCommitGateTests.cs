using BotanicalGardenQR.VisitorCoach.Frontend;
using NUnit.Framework;
using Oculus.Interaction;
using UnityEngine;

namespace BotanicalGardenQR.VisitorCoach.Tests.EditMode
{
    public sealed class VisitorDialogueClickCommitGateTests
    {
        [Test]
        public void OnePhysicalPressCanCommitOnlyOneDialogueAction()
        {
            var gate = new VisitorDialoguePressCommitGate();

            Assert.That(gate.TryCommit(Event(7, PointerEventType.Hover)), Is.False);
            Assert.That(gate.TryCommit(Event(7, PointerEventType.Select)), Is.True);
            Assert.That(gate.TryCommit(Event(7, PointerEventType.Select)), Is.False);
            Assert.That(gate.TryCommit(Event(8, PointerEventType.Select)), Is.False);
            Assert.That(gate.TryCommit(Event(7, PointerEventType.Unselect)), Is.False);
            Assert.That(gate.TryCommit(Event(8, PointerEventType.Select)), Is.True);
        }

        [Test]
        public void CancelOrExplicitResetRearmsTheDialogueTarget()
        {
            var gate = new VisitorDialoguePressCommitGate();
            Assert.That(gate.TryCommit(Event(3, PointerEventType.Select)), Is.True);
            Assert.That(gate.TryCommit(Event(3, PointerEventType.Cancel)), Is.False);
            Assert.That(gate.TryCommit(Event(4, PointerEventType.Select)), Is.True);
            gate.Reset();
            Assert.That(gate.TryCommit(Event(5, PointerEventType.Select)), Is.True);
        }

        static PointerEvent Event(int pointerId, PointerEventType type)
            => new PointerEvent(pointerId, type, Pose.identity);
    }
}
