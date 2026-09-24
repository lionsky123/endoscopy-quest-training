using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.FrontendShell.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Bootstrap
{
    public sealed class FullScriptAudioRuntimeTests
    {
        [Test]
        public void AudioSettingsClampSeparateMusicAndEffectsAndKeepLevelsWhileMuted()
        {
            var settings = new FullScriptAudioSettings();

            Assert.That(settings.MusicVolume, Is.InRange(0f, 1f));
            Assert.That(settings.EffectsVolume, Is.InRange(0f, 1f));
            Assert.That(settings.EffectiveMusicGain, Is.GreaterThan(0f));
            Assert.That(settings.EffectiveEffectsGain, Is.GreaterThan(0f));

            settings.AdjustMusic(4f);
            settings.AdjustEffects(-4f);
            Assert.That(settings.MusicVolume, Is.EqualTo(1f));
            Assert.That(settings.EffectsVolume, Is.EqualTo(0f));

            settings.SetMuted(true);
            Assert.That(settings.EffectiveMusicGain, Is.EqualTo(0f));
            Assert.That(settings.EffectiveEffectsGain, Is.EqualTo(0f));
            settings.SetMuted(false);
            Assert.That(settings.EffectiveMusicGain, Is.EqualTo(1f));
            Assert.That(settings.EffectiveEffectsGain, Is.EqualTo(0f));
        }

        [Test]
        public void SinkVideoDucksOnlyMusicAndRestoresItsSelectedVolume()
        {
            var settings = new FullScriptAudioSettings();
            var selectedVolume = settings.MusicVolume;

            settings.SetSinkVideoPlaying(true);
            Assert.That(settings.EffectiveMusicGain, Is.LessThan(selectedVolume));
            Assert.That(settings.EffectiveEffectsGain, Is.GreaterThan(0f));

            settings.SetSinkVideoPlaying(false);
            Assert.That(settings.EffectiveMusicGain, Is.EqualTo(selectedVolume));
        }

        [TestCase("Travel_R01_OFFICE", ClinicalTouchFeedbackKind.RoomChange)]
        [TestCase("NextPage", ClinicalTouchFeedbackKind.Page)]
        [TestCase("ReturnFromSinkVideo", ClinicalTouchFeedbackKind.Page)]
        [TestCase("SubmitJourney", ClinicalTouchFeedbackKind.Confirm)]
        public void AcceptedTouchUsesActionSpecificFeedback(string buttonName, ClinicalTouchFeedbackKind expected)
        {
            Assert.That(ClinicalNearTouch.FeedbackKind(buttonName), Is.EqualTo(expected));
        }

        [Test]
        public void GeneratedBackgroundWaveformIsNonSilentAndSourcesAreIsolatedTwoDimensional()
        {
            var samples = FullScriptAudioRuntime.CreateBackgroundSamples(8000);
            var root = new GameObject("FullScriptAudioSourceTest");
            try
            {
                var source = root.AddComponent<AudioSource>();
                FullScriptAudioRuntime.ConfigureSource(source);

                Assert.That(System.Array.Exists(samples, sample => Mathf.Abs(sample) > .0001f), Is.True);
                var peak = 0f;
                foreach (var sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                Assert.That(peak, Is.LessThanOrEqualTo(1f));
                Assert.That(source.spatialBlend, Is.EqualTo(0f));
                Assert.That(source.ignoreListenerPause, Is.True);
                Assert.That(source.ignoreListenerVolume, Is.True);
                Assert.That(source.playOnAwake, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DisposeIsSafeWhenUnityAlreadyDestroyedTheRuntimeObject()
        {
            var root = new GameObject("DestroyedFullScriptAudioRuntimeTest");
            var runtime = root.AddComponent<FullScriptAudioRuntime>();
            Object.DestroyImmediate(root);

            Assert.DoesNotThrow(runtime.Dispose);
        }
    }
}
