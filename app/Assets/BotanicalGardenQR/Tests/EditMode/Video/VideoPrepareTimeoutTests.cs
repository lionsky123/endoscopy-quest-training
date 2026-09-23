using System;
using BotanicalGardenQR.Video.Backend;
using BotanicalGardenQR.Video.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Video
{
    public sealed class VideoPrepareTimeoutTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ListenerSilenceExceptionIsLocalAndOptIn(bool enabled)
        {
            var root=new GameObject("VideoAudioPolicyTest");
            float volume=AudioListener.volume;bool paused=AudioListener.pause;
            try
            {
                AudioListener.volume=0;AudioListener.pause=true;
                var source=root.AddComponent<AudioSource>();
                var other=root.AddComponent<AudioSource>();
                // Headless EditMode may stub AudioListener setters. Compare the
                // actual state before/after configuration, not audible output.
                float beforeVolume=AudioListener.volume;bool beforePause=AudioListener.pause;
                VideoController.ConfigureListenerSilence(source,new VideoRuntimeOptions(20,enabled));
                Assert.That(source.ignoreListenerPause,Is.EqualTo(enabled));
                Assert.That(source.ignoreListenerVolume,Is.EqualTo(enabled));
                Assert.That(other.ignoreListenerPause,Is.False);
                Assert.That(other.ignoreListenerVolume,Is.False);
                Assert.That(AudioListener.volume,Is.EqualTo(beforeVolume));Assert.That(AudioListener.pause,Is.EqualTo(beforePause));
                Assert.That(new VideoRuntimeOptions(20).IgnoreListenerSilence,Is.False);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);AudioListener.volume=volume;AudioListener.pause=paused;}
        }
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
