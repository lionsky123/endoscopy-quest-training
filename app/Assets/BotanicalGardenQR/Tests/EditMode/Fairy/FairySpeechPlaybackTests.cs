using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Fairy.Backend;
using BotanicalGardenQR.Fairy.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace BotanicalGardenQR.Tests.EditMode.Fairy
{
    [Explicit("Runtime audio investigation; requires a working Editor audio output.")]
    public sealed class FairySpeechPlaybackTests
    {
        GameObject _root;
        FairyController _controller;
        FairyCompanionBinding _binding;
        AudioSource _source;
        Transform _viewer;
        readonly List<DiagnosticEvent> _diagnostics = new();

        [UnitySetUp]
        public IEnumerator EnterRuntime()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _diagnostics.Clear();
            _root = new GameObject("FairySpeechPlaybackTest");
            var viewer = Child("Viewer");
            _viewer = viewer.transform;
            viewer.transform.position = new Vector3(0f, 1.65f, 0f);
            viewer.AddComponent<Camera>();
            viewer.AddComponent<AudioListener>();
            var platform = Child("InactivePlatform");
            platform.SetActive(false);
            var passthrough = platform.AddComponent<OVRPassthroughLayer>();
            var light = platform.AddComponent<Light>();
            var ground = Child("Ground");
            _controller = (FairyController)FairyModuleFactory.Create(
                _root.transform, viewer.transform, ground.transform, passthrough, light, _diagnostics.Add);
            var config = AssetDatabase.LoadAssetAtPath<FairyApplicationConfiguration>(
                "Assets/BotanicalGardenQR/Content/Authoring/FairyApplicationConfiguration.asset");
            Assert.That(config.TryGet(out var definition), Is.True);
            _binding = new FairyCompanionBinding(_controller, definition, initiallyVisible: false);
            _source = _root.GetComponentsInChildren<AudioSource>(true).Single();
            // This probe isolates speech scheduling. The imported sample model
            // still has unrelated PlaySound AnimationEvents without receivers.
            foreach (var animator in _root.GetComponentsInChildren<Animator>(true))
                animator.fireEvents = false;
        }

        [UnityTearDown]
        public IEnumerator LeaveRuntime()
        {
            _binding?.Dispose();
            if (_root != null) Object.Destroy(_root);
            yield return null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PublishedSpeechActuallyStartsAfterArrival()
        {
            var speech = LoadSpeech("fairy-coach-qr-confirm");
            Assert.That(speech.LoadAudioData(), Is.True);
            yield return null;
            Assert.That(speech.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
            var samples = new float[speech.samples * speech.channels];
            Assert.That(speech.GetData(samples, 0), Is.True);
            Assert.That(samples.Any(value => Mathf.Abs(value) > 0.01f), Is.True,
                "The imported clip must contain decoded, non-silent PCM samples.");

            var control = Child("UnrelatedAudioControl").AddComponent<AudioSource>();
            control.clip = speech;
            control.spatialBlend = 0f;
            control.Play();
            yield return new WaitForSecondsRealtime(0.15f);
            var outputAvailable = control.isPlaying;
            TestContext.WriteLine($"Audio output control={outputAvailable}; dspTime={AudioSettings.dspTime}; listenerPaused={AudioListener.pause}");
            control.Stop();
            if (!outputAvailable)
                Assert.Ignore("Even an unrelated 2D AudioSource cannot play in this validation host; audible output requires an audio-enabled runtime.");

            Assert.That(_binding.Show().Succeeded, Is.True);
            Assert.That(_binding.Speak(new FairySpeech(speech)).Succeeded, Is.True);
            yield return new WaitForSecondsRealtime(3.8f);
            Assert.That(_controller.LastSpeechClip, Is.SameAs(speech));
            Assert.That(_source.isPlaying, Is.True,
                "A successful request or a counter is not evidence that AudioSource started.");
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(_source.isPlaying, Is.True);
        }

        [UnityTest]
        public IEnumerator PublishedSpeechProducesNonSilentListenerOutput()
        {
            var speech = LoadSpeech("fairy-coach-qr-confirm");
            Assert.That(speech.LoadAudioData(), Is.True);
            var control = Child("TwoDimensionalOutputControl").AddComponent<AudioSource>();
            control.clip = speech;
            control.spatialBlend = 0f;
            control.Play();
            var controlPeak = 0f;
            yield return MeasureListenerOutput(control, value => controlPeak = value, "2D control");
            control.Stop();
            if (controlPeak <= 0.00001f)
                Assert.Ignore("The independent 2D control has no measurable listener output on this host.");

            Assert.That(_binding.Show().Succeeded, Is.True);
            yield return new WaitForSecondsRealtime(3.8f);
            Assert.That(_binding.Speak(new FairySpeech(speech)).Succeeded, Is.True);
            yield return new WaitForSecondsRealtime(0.2f);
            AssertSpatialGain(8f);
            // Reproduce the previous mix on this test-owned source, at the same
            // position, with the same clip, volume and enabled spatializer.
            Assert.That(_source.SetSpatializerFloat(
                (int)MetaXRAudioSource.NativeParameterIndex.P_GAIN, 0f), Is.True);
            var previousPeak = 0f;
            yield return MeasureListenerOutput(_source, value => previousPeak = value, "Previous Fairy speech mix");

            var boostedRequest = new FairySpeech(speech);
            FairyResult? boostedCompletion = null;
            Assert.That(_binding.Speak(boostedRequest, result => boostedCompletion = result).Succeeded, Is.True);
            var spatialPeak = 0f;
            yield return MeasureListenerOutput(_source, value => spatialPeak = value, "Boosted Fairy production spatializer");
            AssertSpatialGain(8f);

            TestContext.WriteLine(
                $"Listener peaks: 2D={controlPeak}; previous={previousPeak}; boosted={spatialPeak}; " +
                $"measuredGainDb={20f * Mathf.Log10(spatialPeak / previousPeak)}; " +
                $"plugin={AudioSettings.GetSpatializerPluginName()}; sourcePosition={_source.transform.position}; " +
                $"volume={_source.volume}; mute={_source.mute}; active={_source.gameObject.activeInHierarchy}");
            Assert.That(previousPeak, Is.GreaterThan(0.00001f));
            Assert.That(spatialPeak, Is.GreaterThan(previousPeak * 2f),
                "The speech gain must measurably reach the listener, not just change a parameter.");
            Assert.That(spatialPeak, Is.LessThan(0.95f), "The measured listening window must retain output headroom.");
            Assert.That(_source.spatialize, Is.True);
            Assert.That(_source.spatialBlend, Is.EqualTo(1f));
            var completionDeadline = Time.realtimeSinceStartup + speech.length + 2f;
            while (!boostedCompletion.HasValue && Time.realtimeSinceStartup < completionDeadline)
                yield return null;
            Assert.That(boostedCompletion.HasValue, Is.True);
            Assert.That(boostedCompletion.Value.Succeeded, Is.True);
            AssertSpatialGain(0f, "Natural completion must restore the short-feedback gain.");
            Assert.That(_source.volume, Is.EqualTo(1f), "Speech must restore the original short-feedback source volume.");
        }

        static IEnumerator MeasureListenerOutput(AudioSource source, System.Action<float> measured, string label)
        {
            var samples = new float[1024];
            var listenerPeak = 0f;
            var sourcePeak = 0f;
            yield return new WaitForSecondsRealtime(0.2f);
            for (var window = 0; window < 24; window++)
            {
                AudioListener.GetOutputData(samples, 0);
                foreach (var sample in samples) listenerPeak = Mathf.Max(listenerPeak, Mathf.Abs(sample));
                source.GetOutputData(samples, 0);
                foreach (var sample in samples) sourcePeak = Mathf.Max(sourcePeak, Mathf.Abs(sample));
                yield return new WaitForSecondsRealtime(0.05f);
            }
            TestContext.WriteLine($"{label}: listenerPeak={listenerPeak}; sourcePeak={sourcePeak}; playing={source.isPlaying}");
            measured(listenerPeak);
        }

        [UnityTest]
        public IEnumerator OrdinaryCompanionCueDoesNotCancelTeachingSpeech()
        {
            Assert.That(_binding.Show().Succeeded, Is.True);
            yield return new WaitForSecondsRealtime(3.8f);
            var speech = LoadSpeech("fairy-coach-qr-confirm");
            FairyResult? completion = null;
            Assert.That(_binding.Speak(new FairySpeech(speech), result => completion = result).Succeeded, Is.True);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(_source.isPlaying, Is.True);

            Assert.That(_binding.PresentCue(new FairyCompanionCue(
                FairyCompanionCueKind.ArtifactWait, Vector3.forward)).Succeeded, Is.True);
            Assert.That(_binding.PresentCue(FairyCompanionCue.Idle).Succeeded, Is.True);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(_source.isPlaying, Is.True,
                "Ordinary pose/idle cues must not silence an unfinished teaching sentence.");
            Assert.That(_source.clip, Is.SameAs(speech));
            Assert.That(completion.HasValue, Is.False);
            Assert.That(_diagnostics.Any(item => item.Code == "FAIRY_SPEECH_CANCELLED"), Is.False);
            AssertSpatialGain(8f);
            Assert.That(_binding.Hide().Succeeded, Is.True);
            AssertSpatialGain(0f);
        }

        [UnityTest]
        public IEnumerator StaleCancellationCannotStopAReplacementSentence()
        {
            Assert.That(_binding.Show().Succeeded, Is.True);
            yield return new WaitForSecondsRealtime(3.8f);
            var first = new FairySpeech(LoadSpeech("fairy-prologue-awaken"));
            var replacement = new FairySpeech(LoadSpeech("fairy-coach-qr-confirm"));
            var firstResults = new List<FairyResult>();
            var replacementResults = new List<FairyResult>();
            Assert.That(_binding.Speak(first, firstResults.Add).Succeeded, Is.True);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(_binding.Speak(replacement, replacementResults.Add).Succeeded, Is.True);
            Assert.That(firstResults.Count, Is.EqualTo(1));
            Assert.That(firstResults[0].FailureCode, Is.EqualTo(FairyFailureCode.SpeechCancelled));
            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(_binding.CancelSpeech(first.RequestId).Succeeded, Is.True);
            Assert.That(_controller.CancelSpeech(SessionToken.CreateNew(), replacement.RequestId).FailureCode,
                Is.EqualTo(FairyFailureCode.StaleSession));
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(_source.isPlaying, Is.True);
            Assert.That(_source.clip, Is.SameAs(replacement.Clip));
            AssertSpatialGain(8f);
            Assert.That(replacementResults, Is.Empty,
                "An old request or another session must not cancel the current sentence.");

            Assert.That(_binding.CancelSpeech(replacement.RequestId).Succeeded, Is.True);
            Assert.That(_binding.CancelSpeech(replacement.RequestId).Succeeded, Is.True);
            Assert.That(replacementResults.Count, Is.EqualTo(1));
            Assert.That(replacementResults[0].FailureCode, Is.EqualTo(FairyFailureCode.SpeechCancelled));
            Assert.That(_source.isPlaying, Is.False);
            Assert.That(_source.clip, Is.Null);
            AssertSpatialGain(0f);
        }

        void AssertSpatialGain(float expectedDb, string message = null)
        {
            Assert.That(_source.GetSpatializerFloat(
                (int)MetaXRAudioSource.NativeParameterIndex.P_GAIN, out var gainDb), Is.True);
            Assert.That(gainDb, Is.EqualTo(expectedDb).Within(0.01f), message);
        }

        [UnityTest]
        public IEnumerator HiddenSpeechIsRejectedAndPendingCompletionIsCancelledExactlyOnce()
        {
            var speech = LoadSpeech("fairy-prologue-awaken");
            var completions = new List<FairyResult>();
            Assert.That(_binding.Show().Succeeded, Is.True);
            Assert.That(_binding.Speak(new FairySpeech(speech), completions.Add).Succeeded, Is.True);
            Assert.That(_binding.Hide().Succeeded, Is.True);
            Assert.That(_binding.Hide().Succeeded, Is.True);
            Assert.That(completions.Count, Is.EqualTo(1));
            Assert.That(completions[0].FailureCode, Is.EqualTo(FairyFailureCode.SpeechCancelled));
            Assert.That(_binding.Speak(new FairySpeech(speech), completions.Add).FailureCode,
                Is.EqualTo(FairyFailureCode.NotVisible));
            Assert.That(completions.Count, Is.EqualTo(1), "Rejected requests do not produce completion callbacks.");
            Assert.That(_binding.Show().Succeeded, Is.True);
            yield return new WaitForSecondsRealtime(3.8f);
            Assert.That(_controller.SpeechPlayCount, Is.Zero, "Showing again must not replay a cancelled sentence.");
            Assert.That(_source.clip, Is.Null);
        }

        GameObject Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root.transform, false);
            return child;
        }

        static AudioClip LoadSpeech(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                $"Assets/BotanicalGardenQR/Content/Shared/VisitorTutorial/Audio/{name}.mp3");
            Assert.That(clip, Is.Not.Null);
            return clip;
        }

    }
}
