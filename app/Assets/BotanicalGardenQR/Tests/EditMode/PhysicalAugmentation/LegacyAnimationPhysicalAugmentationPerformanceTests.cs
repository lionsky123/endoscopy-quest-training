using BotanicalGardenQR.PhysicalAugmentation.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.PhysicalAugmentation
{
    public sealed class LegacyAnimationPhysicalAugmentationPerformanceTests
    {
        GameObject _root;
        AnimationClip _clip;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_clip != null) Object.DestroyImmediate(_clip);
        }

        [Test]
        public void PlayStartsConfiguredClipFromBeginningAndStopEndsIt()
        {
            var performance = CreatePerformance("Survey", 1f);
            var animation = _root.GetComponentInChildren<Animation>();

            Assert.DoesNotThrow(() => performance.Play(7));
            Assert.That(animation.IsPlaying("Survey"), Is.True);
            Assert.That(animation["Survey"].normalizedTime, Is.InRange(0f, 0.01f));

            animation["Survey"].time = 0.4f;
            animation.Sample();
            Assert.DoesNotThrow(() => performance.Play(8));
            Assert.That(animation.IsPlaying("Survey"), Is.True);
            Assert.That(animation["Survey"].normalizedTime, Is.InRange(0f, 0.01f));

            performance.StopPerformance();
            Assert.That(animation.IsPlaying("Survey"), Is.False);
        }

        [Test]
        public void PlayRejectsMissingClipWithoutUsingAnotherAnimation()
        {
            var performance = CreatePerformance("Missing", 1f);

            var error = Assert.Throws<System.InvalidOperationException>(() => performance.Play(1));
            Assert.That(error.Message, Does.Contain("Missing"));
        }

        [Test]
        public void PlayRejectsFallbackThatWouldTruncateClip()
        {
            var performance = CreatePerformance("Survey", 0.25f);

            var error = Assert.Throws<System.InvalidOperationException>(() => performance.Play(1));
            Assert.That(error.Message, Does.Contain("must not truncate"));
        }

        LegacyAnimationPhysicalAugmentationPerformance CreatePerformance(
            string clipName,
            float fallbackDuration)
        {
            _root = new GameObject("LegacyPerformance");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(_root.transform, false);
            var animation = visual.AddComponent<Animation>();
            animation.playAutomatically = false;
            _clip = new AnimationClip { legacy = true, name = "Survey" };
            _clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 0.5f, 1f));
            animation.AddClip(_clip, _clip.name);
            animation.clip = _clip;

            var performance = _root.AddComponent<LegacyAnimationPhysicalAugmentationPerformance>();
            var serialized = new SerializedObject(performance);
            serialized.FindProperty("_clipName").stringValue = clipName;
            serialized.FindProperty("_fallbackDurationSeconds").floatValue = fallbackDuration;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return performance;
        }
    }
}
