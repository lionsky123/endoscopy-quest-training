using BotanicalGardenQR.PhysicalAugmentation.Backend;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.PhysicalAugmentation
{
    public sealed class PhysicalAugmentationPerformanceFactoryTests
    {
        GameObject _factoryRoot;
        GameObject _performancePrefab;
        GameObject _proxyPrefab;
        AudioClip _audioClip;

        [SetUp]
        public void SetUp()
        {
            _factoryRoot = new GameObject("PerformanceFactoryRoot");
            _performancePrefab = new GameObject("PerformancePrefab");
            _performancePrefab.AddComponent<TestPerformanceBehaviour>();
            _proxyPrefab = new GameObject("ProxyPrefab");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_factoryRoot);
            Object.DestroyImmediate(_performancePrefab);
            Object.DestroyImmediate(_proxyPrefab);
            if (_audioClip != null) Object.DestroyImmediate(_audioClip);
        }

        [Test]
        public void PrepareCreatesOneHiddenRootAndPlayRejectsStaleCompletion()
        {
            var factory = new PrefabPhysicalAugmentationPerformanceFactory(_factoryRoot.transform);
            Assert.That(factory.TryPrepare(
                Definition(),
                new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 20f, 0f)),
                1.25f,
                7,
                out var lease,
                out var diagnosticTag), Is.True, diagnosticTag);
            var instanceRoot = _factoryRoot.transform.GetChild(0).gameObject;
            var behaviour = instanceRoot.GetComponentInChildren<TestPerformanceBehaviour>(true);

            Assert.That(instanceRoot.activeSelf, Is.False);
            Assert.That(instanceRoot.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(instanceRoot.transform.localScale, Is.EqualTo(Vector3.one * 1.25f));

            var completionCount = 0;
            lease.Play(_ => completionCount++);
            behaviour.CompleteNow(8);
            behaviour.CompleteNow(7);
            behaviour.CompleteNow(7);

            Assert.That(instanceRoot.activeSelf, Is.True);
            Assert.That(completionCount, Is.EqualTo(1));
            lease.Dispose();
            Assert.That(_factoryRoot.transform.childCount, Is.Zero);
        }

        [Test]
        public void StopAndDisposeAreIdempotent()
        {
            var factory = new PrefabPhysicalAugmentationPerformanceFactory(_factoryRoot.transform);
            Assert.That(factory.TryPrepare(
                Definition(), Pose.identity, 1f, 2, out var lease, out _), Is.True);
            var instanceRoot = _factoryRoot.transform.GetChild(0).gameObject;

            lease.Play(_ => { });
            lease.Stop();
            lease.Stop();

            Assert.That(instanceRoot.activeSelf, Is.False);
            Assert.That(() => lease.Dispose(), Throws.Nothing);
            Assert.That(() => lease.Dispose(), Throws.Nothing);
            Assert.That(_factoryRoot.transform.childCount, Is.Zero);
        }

        [Test]
        public void ExplicitStopHidesACompletedPresentationBeforeDisposal()
        {
            var factory = new PrefabPhysicalAugmentationPerformanceFactory(_factoryRoot.transform);
            Assert.That(factory.TryPrepare(
                Definition(), Pose.identity, 1f, 3, out var lease, out _), Is.True);
            var instanceRoot = _factoryRoot.transform.GetChild(0).gameObject;
            var behaviour = instanceRoot.GetComponentInChildren<TestPerformanceBehaviour>(true);

            lease.Play(_ => { });
            behaviour.CompleteNow(3);

            Assert.That(instanceRoot.activeSelf, Is.True,
                "A completed performance remains visible until the visitor returns to the panel.");
            lease.Stop();
            Assert.That(instanceRoot.activeSelf, Is.False,
                "Returning to the panel must hide the presentation immediately.");
            lease.Dispose();
            Assert.That(_factoryRoot.transform.childCount, Is.Zero);
        }

        [Test]
        public void OwnedMediaStopsOnLeaseStopAndCompletionAndRestartsWithoutOverlap()
        {
            var source = _performancePrefab.AddComponent<AudioSource>();
            _audioClip = AudioClip.Create("OwnedMedia", 4410, 1, 44100, false);
            source.clip = _audioClip;
            source.playOnAwake = false;
            var authoredMedia = _performancePrefab.AddComponent<PhysicalAugmentationOwnedMedia>();
            var serialized = new SerializedObject(authoredMedia);
            var audioSources = serialized.FindProperty("_audioSources");
            audioSources.arraySize = 1;
            audioSources.GetArrayElementAtIndex(0).objectReferenceValue = source;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var factory = new PrefabPhysicalAugmentationPerformanceFactory(_factoryRoot.transform);
            Assert.That(factory.TryPrepare(
                Definition(), Pose.identity, 1f, 4, out var lease, out var diagnosticTag),
                Is.True,
                diagnosticTag);
            var instanceRoot = _factoryRoot.transform.GetChild(0);
            var media = instanceRoot.GetComponentInChildren<PhysicalAugmentationOwnedMedia>(true);
            var behaviour = instanceRoot.GetComponentInChildren<TestPerformanceBehaviour>(true);

            lease.Play(_ => { });
            Assert.That(media.IsPlaying, Is.True);
            Assert.That(media.PlayRevision, Is.EqualTo(1));
            behaviour.CompleteNow(5);
            Assert.That(media.IsPlaying, Is.True,
                "A stale completion must not stop media owned by the active generation.");

            lease.Stop();
            Assert.That(media.IsPlaying, Is.False);

            lease.Play(_ => { });
            Assert.That(media.IsPlaying, Is.True);
            Assert.That(media.PlayRevision, Is.EqualTo(2),
                "Replay must reset the old owned media before starting a new revision.");

            behaviour.CompleteNow(4);
            Assert.That(media.IsPlaying, Is.False);
            lease.Dispose();
        }

        [Test]
        public void MissingPerformanceContractFailsClosedWithoutLeakingInstance()
        {
            Object.DestroyImmediate(_performancePrefab.GetComponent<TestPerformanceBehaviour>());
            var factory = new PrefabPhysicalAugmentationPerformanceFactory(_factoryRoot.transform);

            var succeeded = factory.TryPrepare(
                Definition(), Pose.identity, 1f, 2, out var lease, out var diagnosticTag);

            Assert.That(succeeded, Is.False);
            Assert.That(lease, Is.Null);
            Assert.That(diagnosticTag, Is.EqualTo("physical_augmentation.performance_prepare_failed"));
            Assert.That(_factoryRoot.transform.childCount, Is.Zero);
        }

        PhysicalAugmentationDefinition Definition()
            => new PhysicalAugmentationDefinition(
                new PhysicalAugmentationPointId("performance_test"),
                "Performance Test",
                1,
                _performancePrefab,
                _proxyPrefab,
                0.6f, 0.015f, 2f, 0.35f, 0.03f, 5f,
                0.5f, 2f,
                PhysicalAugmentationOcclusionPolicy.RequireEnvironmentDepthAndProxy);

        public sealed class TestPerformanceBehaviour : PhysicalAugmentationPerformanceBehaviour
        {
            public override void ResetPerformance() => ResetOwnedMedia();
            public override void Play(long generation) => PlayOwnedMedia();
            public override void StopPerformance() => StopOwnedMedia();
            public void CompleteNow(long generation) => Complete(generation, true);
        }
    }
}
