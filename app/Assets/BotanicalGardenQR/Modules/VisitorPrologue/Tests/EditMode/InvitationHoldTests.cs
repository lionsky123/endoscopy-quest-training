using BotanicalGardenQR.VisitorPrologue.Frontend;
using NUnit.Framework;

namespace BotanicalGardenQR.VisitorPrologue.Tests.EditMode
{
    public sealed class InvitationHoldTests
    {
        [Test]
        public void AContactMustRemainWithOneHandAndLossCancelsProgress()
        {
            var hold = new InvitationHold();
            for (int i = 0; i < 5; i++) Assert.That(hold.Step(0, .1f, 1f), Is.False);
            Assert.That(hold.Progress, Is.EqualTo(.5f).Within(.001f));
            Assert.That(hold.Step(1, .1f, 1f), Is.False);
            Assert.That(hold.Progress, Is.EqualTo(.1f).Within(.001f));
            hold.Step(-1, .1f, 1f);
            Assert.That(hold.Progress, Is.Zero);
            for (int i = 0; i < 9; i++) Assert.That(hold.Step(1, .1f, 1f), Is.False);
            Assert.That(hold.Step(1, .1f, 1f), Is.True);
            hold.Reset();
            Assert.That(hold.Progress, Is.Zero);
            Assert.That(hold.HandId, Is.EqualTo(-1));
        }
        [Test]
        public void AStalledFrameDoesNotInstantlyAcceptContact()
        {
            var hold = new InvitationHold();
            Assert.That(hold.Step(0, 20f, 1f), Is.False);
            Assert.That(hold.Progress, Is.EqualTo(.1f));
        }
    }
}
