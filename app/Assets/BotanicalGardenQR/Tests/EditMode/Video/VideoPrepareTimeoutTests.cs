using System;
using BotanicalGardenQR.Video.Backend;
using BotanicalGardenQR.Video.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode.Video
{
    public sealed class VideoPrepareTimeoutTests
    {
        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RuntimeOptions_RejectInvalidTimeout(float seconds)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new VideoRuntimeOptions(seconds));

        [Test]
        public void Timeout_OnlyExpiresLoadingAtOrAfterDeadline()
        {
            Assert.That(VideoController.HasPrepareTimedOut(VideoPhase.Closed, 10f, 5f), Is.False);
            Assert.That(VideoController.HasPrepareTimedOut(VideoPhase.Playing, 10f, 5f), Is.False);
            Assert.That(VideoController.HasPrepareTimedOut(VideoPhase.Loading, 4.99f, 5f), Is.False);
            Assert.That(VideoController.HasPrepareTimedOut(VideoPhase.Loading, 5f, 5f), Is.True);
            Assert.That(VideoController.HasPrepareTimedOut(VideoPhase.Loading, 6f, 5f), Is.True);
        }
    }
}
