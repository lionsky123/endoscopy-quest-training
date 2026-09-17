using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BotanicalGardenQR.PhysicalAugmentation.Backend;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Installation;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.PhysicalAugmentation
{
    public sealed class PhysicalAnchorLocalizationCoordinatorTests
    {
        static readonly DateTimeOffset Now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        GameObject[] _assets;
        MemoryStorage _storage;
        PhysicalAnchorBindingStore _store;
        FakePlatform _platform;
        PhysicalAnchorLocalizationCoordinator _coordinator;
        IReadOnlyList<PhysicalAugmentationPointState> _last;

        [SetUp]
        public void SetUp()
        {
            _assets = new[]
            {
                new GameObject("Performance"), new GameObject("Proxy")
            };
            _storage = new MemoryStorage();
            _store = new PhysicalAnchorBindingStore(_storage);
            _platform = new FakePlatform();
            _coordinator = new PhysicalAnchorLocalizationCoordinator(_store, _platform);
            _coordinator.StateChanged += states => _last = states;
        }

        [TearDown]
        public void TearDown()
        {
            _coordinator.Dispose();
            for (var index = 0; index < _assets.Length; index++)
                UnityEngine.Object.DestroyImmediate(_assets[index]);
        }

        [Test]
        public void MissingBindingIsUnconfiguredAndDoesNotCallPlatform()
        {
            Assert.That(_coordinator.TryStart(new[] { Definition("point_a") }, 1, out _), Is.True);

            Assert.That(_last, Has.Count.EqualTo(1));
            Assert.That(_last[0].Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Unconfigured));
            Assert.That(_platform.Requests, Is.Empty);
        }

        [Test]
        public void EmptyPlatformResultFailsOnlyConfiguredPoint()
        {
            Bind("point_a", Guid.NewGuid());
            _platform.Handler = _ => Task.FromResult(new PhysicalAnchorPlatformLoadResult(
                true,
                Array.Empty<IPhysicalAnchorPlatformLease>()));

            Assert.That(_coordinator.TryStart(new[] { Definition("point_a"), Definition("point_b") }, 1, out _), Is.True);

            Assert.That(Find("point_a").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Failed));
            Assert.That(Find("point_b").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Unconfigured));
        }

        [Test]
        public void StorageReadFailureRecoversAfterAnExplicitRestart()
        {
            var uuid = Guid.NewGuid();
            Bind("point_a", uuid);
            _storage.ReadException = new IOException("injected transient read failure");

            Assert.That(_coordinator.TryStart(new[] { Definition("point_a") }, 1, out _), Is.True);
            Assert.That(Find("point_a").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Failed));
            Assert.That(Find("point_a").DiagnosticTag, Is.EqualTo("binding_store.read_unavailable"));

            _coordinator.Stop(1);
            _storage.ReadException = null;
            _platform.Handler = _ => Task.FromResult(new PhysicalAnchorPlatformLoadResult(
                true,
                new IPhysicalAnchorPlatformLease[] { new FakeLease(uuid, Pose.identity) }));

            Assert.That(_coordinator.TryStart(new[] { Definition("point_a") }, 2, out _), Is.True);
            Assert.That(Find("point_a").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Stabilizing));
            Assert.That(_platform.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void PlatformExceptionRecoversAfterAnExplicitRestart()
        {
            var uuid = Guid.NewGuid();
            Bind("point_a", uuid);
            _platform.Handler = _ => throw new InvalidOperationException("injected transient platform failure");

            Assert.That(_coordinator.TryStart(new[] { Definition("point_a") }, 1, out _), Is.True);
            Assert.That(Find("point_a").DiagnosticTag, Is.EqualTo("physical_locator.platform_exception"));

            _coordinator.Stop(1);
            _platform.Handler = _ => Task.FromResult(new PhysicalAnchorPlatformLoadResult(
                true,
                new IPhysicalAnchorPlatformLease[] { new FakeLease(uuid, Pose.identity) }));

            Assert.That(_coordinator.TryStart(new[] { Definition("point_a") }, 2, out _), Is.True);
            Assert.That(Find("point_a").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Stabilizing));
            Assert.That(_platform.Requests, Has.Count.EqualTo(2));
        }

        [Test]
        public void PartialPlatformResultKeepsOtherPointFailedAndLeaseStabilizing()
        {
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            Bind("point_a", first);
            Bind("point_b", second);
            var lease = new FakeLease(first, Pose.identity);
            _platform.Handler = _ => Task.FromResult(new PhysicalAnchorPlatformLoadResult(
                true,
                new IPhysicalAnchorPlatformLease[] { lease }));

            _coordinator.TryStart(new[] { Definition("point_a"), Definition("point_b") }, 1, out _);

            Assert.That(Find("point_a").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Stabilizing));
            Assert.That(Find("point_b").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Failed));
        }

        [Test]
        public void StableRequiresContinuousWindowAndRecoveryRestabilizesHidden()
        {
            var uuid = Guid.NewGuid();
            Bind("point_a", uuid);
            var lease = new FakeLease(uuid, new Pose(new Vector3(1f, 2f, 3f), Quaternion.identity));
            _platform.Handler = _ => Task.FromResult(new PhysicalAnchorPlatformLoadResult(
                true,
                new IPhysicalAnchorPlatformLease[] { lease }));
            _coordinator.TryStart(new[] { Definition("point_a") }, 1, out _);

            _coordinator.Tick(0f);
            Assert.That(Find("point_a").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Stabilizing));
            _coordinator.Tick(0.61f);
            Assert.That(Find("point_a").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Stable));
            Assert.That(Find("point_a").HasResolvedPose, Is.True);

            lease.Tracked = false;
            _coordinator.Tick(1f);
            Assert.That(Find("point_a").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Lost));
            Assert.That(Find("point_a").HasResolvedPose, Is.True);
            _coordinator.Tick(1.36f);
            Assert.That(Find("point_a").HasResolvedPose, Is.False);

            lease.Tracked = true;
            _coordinator.Tick(2f);
            Assert.That(Find("point_a").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Stabilizing));
            Assert.That(Find("point_a").HasResolvedPose, Is.False);
            _coordinator.Tick(2.61f);
            Assert.That(Find("point_a").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Stable));
        }

        [Test]
        public void LargePoseCorrectionHidesPointUntilRebaseStabilizes()
        {
            var uuid = Guid.NewGuid();
            Bind("point_a", uuid);
            var lease = new FakeLease(uuid, Pose.identity);
            _platform.Handler = _ => Task.FromResult(new PhysicalAnchorPlatformLoadResult(
                true,
                new IPhysicalAnchorPlatformLease[] { lease }));
            _coordinator.TryStart(new[] { Definition("point_a") }, 1, out _);
            _coordinator.Tick(0f);
            _coordinator.Tick(0.61f);
            Assert.That(Find("point_a").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Stable));

            lease.Pose = new Pose(new Vector3(0.2f, 0f, 0f), Quaternion.identity);
            _coordinator.Tick(1f);

            Assert.That(Find("point_a").Phase, Is.EqualTo(PhysicalAugmentationLocalizationPhase.Stabilizing));
            Assert.That(Find("point_a").HasResolvedPose, Is.False);
        }

        [Test]
        public void ConfiguredModelRotationAndScaleAreComposedWithTheAnchorPose()
        {
            var uuid = Guid.NewGuid();
            Bind("point_a", uuid);
            var anchorPose = new Pose(
                new Vector3(1f, 2f, 3f),
                Quaternion.Euler(0f, 30f, 0f));
            _platform.Handler = _ => Task.FromResult(new PhysicalAnchorPlatformLoadResult(
                true,
                new IPhysicalAnchorPlatformLease[] { new FakeLease(uuid, anchorPose) }));

            _coordinator.TryStart(
                new[] { Definition("point_a", new Vector3(0f, 45f, 0f), 1.5f) },
                1,
                out _);
            _coordinator.Tick(0f);
            _coordinator.Tick(0.61f);

            var state = Find("point_a");
            Assert.That(state.ResolvedPose.position, Is.EqualTo(anchorPose.position));
            Assert.That(
                Quaternion.Angle(state.ResolvedPose.rotation, Quaternion.Euler(0f, 75f, 0f)),
                Is.LessThan(0.01f));
            Assert.That(state.UniformScale, Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public async Task CallbackAfterStopIsRejectedAndReturnedLeaseIsReleased()
        {
            var uuid = Guid.NewGuid();
            Bind("point_a", uuid);
            var completion = new TaskCompletionSource<PhysicalAnchorPlatformLoadResult>();
            _platform.Handler = _ => completion.Task;
            _coordinator.TryStart(new[] { Definition("point_a") }, 1, out _);
            _coordinator.Stop(1);
            var lateLease = new FakeLease(uuid, Pose.identity);

            completion.SetResult(new PhysicalAnchorPlatformLoadResult(
                true,
                new IPhysicalAnchorPlatformLease[] { lateLease }));
            await Task.Yield();

            Assert.That(lateLease.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void SecondStartIsRejectedUntilCurrentLeaseOwnerStops()
        {
            _platform.Handler = _ => new TaskCompletionSource<PhysicalAnchorPlatformLoadResult>().Task;
            var uuid = Guid.NewGuid();
            Bind("point_a", uuid);

            Assert.That(_coordinator.TryStart(new[] { Definition("point_a") }, 1, out _), Is.True);
            Assert.That(_coordinator.TryStart(new[] { Definition("point_a") }, 2, out var tag), Is.False);
            Assert.That(tag, Is.EqualTo("physical_locator.single_flight"));
        }

        [Test]
        public void RequestsAreDeduplicatedAndBoundedToFiftyPerBatch()
        {
            var definitions = new List<PhysicalAugmentationDefinition>();
            for (var index = 0; index < 51; index++)
            {
                var pointId = $"point_{index:D2}";
                definitions.Add(Definition(pointId));
                Bind(pointId, Guid.NewGuid());
            }
            _platform.Handler = _ => Task.FromResult(new PhysicalAnchorPlatformLoadResult(
                true,
                Array.Empty<IPhysicalAnchorPlatformLease>()));

            _coordinator.TryStart(definitions, 1, out _);

            Assert.That(_platform.Requests.Select(request => request.Length), Is.EqualTo(new[] { 50, 1 }));
            Assert.That(_platform.Requests.SelectMany(request => request).Distinct().Count(), Is.EqualTo(51));
        }

        void Bind(string pointId, Guid uuid)
        {
            var read = _store.Read();
            Assert.That(read.Succeeded, Is.True);
            Assert.That(_store.RegisterAndAssign(
                new PhysicalAugmentationPointId(pointId),
                uuid,
                Pose.identity,
                1f,
                read.Snapshot.StoreVersion,
                Now).Succeeded, Is.True);
        }

        PhysicalAugmentationDefinition Definition(
            string pointId,
            Vector3 modelEulerAngles = default,
            float modelUniformScale = 1f)
            => new PhysicalAugmentationDefinition(
                new PhysicalAugmentationPointId(pointId),
                pointId,
                1,
                _assets[0], _assets[1],
                0.6f, 0.015f, 2f, 0.35f, 0.03f, 5f,
                0.5f, 2f,
                PhysicalAugmentationOcclusionPolicy.RequireEnvironmentDepthAndProxy,
                modelEulerAngles,
                modelUniformScale);

        PhysicalAugmentationPointState Find(string pointId)
            => _last.Single(state => state.PointId.Value == pointId);

        sealed class MemoryStorage : IPhysicalAnchorBindingStorage
        {
            public string Content { get; set; }
            public Exception ReadException { get; set; }
            public bool Exists => Content != null;
            public string ReadAllText()
            {
                if (ReadException != null) throw ReadException;
                return Content;
            }
            public void ReplaceAllText(string content) => Content = content;
        }

        sealed class FakePlatform : IPhysicalAnchorPlatform
        {
            public Func<IReadOnlyList<Guid>, Task<PhysicalAnchorPlatformLoadResult>> Handler { get; set; }
            public List<Guid[]> Requests { get; } = new List<Guid[]>();
            public Task<PhysicalAnchorPlatformLoadResult> LoadBatchAsync(IReadOnlyList<Guid> uuids)
            {
                Requests.Add(uuids.ToArray());
                return Handler != null
                    ? Handler(uuids)
                    : Task.FromResult(new PhysicalAnchorPlatformLoadResult(
                        true,
                        Array.Empty<IPhysicalAnchorPlatformLease>()));
            }
        }

        sealed class FakeLease : IPhysicalAnchorPlatformLease
        {
            public FakeLease(Guid uuid, Pose pose)
            {
                Uuid = uuid;
                Pose = pose;
            }
            public Guid Uuid { get; }
            public bool Tracked { get; set; } = true;
            public Pose Pose { get; set; }
            public int DisposeCount { get; private set; }
            public bool IsTracked => Tracked;
            public bool TryGetPose(out Pose pose)
            {
                pose = Pose;
                return Tracked;
            }
            public void Dispose() => DisposeCount++;
        }
    }
}
