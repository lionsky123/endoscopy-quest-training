using System;
using System.Collections.Generic;
using BotanicalGardenQR.PhysicalAugmentation.Backend;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.PhysicalAugmentation
{
    public sealed class PhysicalAugmentationControllerTests
    {
        GameObject[] _assets;
        FakeLocator _locator;
        FakeSuppression _suppression;
        FakeCapability _capability;
        FakePerformanceFactory _performances;
        PhysicalAugmentationDefinitionCatalog _definitions;
        PhysicalAugmentationController _controller;
        StateSink _sink;

        [SetUp]
        public void SetUp()
        {
            _assets = new[]
            {
                new GameObject("PerformanceA"), new GameObject("ProxyA"),
                new GameObject("PerformanceB"), new GameObject("ProxyB")
            };
            _definitions = new PhysicalAugmentationDefinitionCatalog(new[]
            {
                Definition("point_a", 0),
                Definition("point_b", 2)
            });
            _locator = new FakeLocator();
            _suppression = new FakeSuppression();
            _capability = new FakeCapability();
            _performances = new FakePerformanceFactory();
            _controller = new PhysicalAugmentationController(
                _definitions,
                _locator,
                _suppression,
                _capability,
                _performances);
            _sink = new StateSink();
            _controller.Observe(_sink);
        }

        [TearDown]
        public void TearDown()
        {
            _controller.Stop();
            for (var index = 0; index < _assets.Length; index++)
                UnityEngine.Object.DestroyImmediate(_assets[index]);
        }

        [Test]
        public void StaleGenerationCannotActivateNewSession()
        {
            var first = _controller.Start();
            _controller.Stop();
            var second = _controller.Start();
            _locator.Emit(Stable("point_a", first.Generation));

            var result = _controller.Activate(new PhysicalAugmentationPointId("point_a"), first.Generation);

            Assert.That(second.Generation, Is.GreaterThan(first.Generation));
            Assert.That(result.FailureCode, Is.EqualTo(PhysicalAugmentationFailureCode.StaleGeneration));
            Assert.That(_sink.Last.Points[0].CanActivate, Is.False);
        }

        [Test]
        public void RetryLocalizationRestartsOnlyTheLocatorWithANewGeneration()
        {
            var started = _controller.Start();

            var retry = _controller.RetryLocalization();

            Assert.That(retry.Succeeded, Is.True);
            Assert.That(retry.Generation, Is.GreaterThan(started.Generation));
            Assert.That(_locator.StartCalls, Is.EqualTo(2));
            Assert.That(_locator.StopCalls, Is.EqualTo(1));
            Assert.That(_locator.StartedGenerations, Is.EqualTo(new[]
            {
                started.Generation,
                retry.Generation
            }));
        }

        [Test]
        public void RetryLocalizationNeverInterruptsAPlayingPerformance()
        {
            var started = _controller.Start();
            _locator.Emit(Stable("point_a", started.Generation));
            Assert.That(_controller.Activate(
                new PhysicalAugmentationPointId("point_a"),
                started.Generation).Succeeded, Is.True);
            var playingLease = _performances.LastLease;

            var retry = _controller.RetryLocalization();

            Assert.That(retry.Succeeded, Is.False);
            Assert.That(retry.FailureCode, Is.EqualTo(PhysicalAugmentationFailureCode.Suppressed));
            Assert.That(_locator.StartCalls, Is.EqualTo(1));
            Assert.That(_locator.StopCalls, Is.Zero);
            Assert.That(playingLease.StopCount, Is.Zero);
        }

        [Test]
        public void PermanentLocatorStartFailureIsNotMisclassifiedAsRetryableCapabilityLoss()
        {
            _locator.StartSucceeded = false;
            _locator.StartDiagnosticTag = "physical_locator.definition_invalid";

            var result = _controller.Start();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo(PhysicalAugmentationFailureCode.InvalidDefinition));
            Assert.That(_sink.Last.Phase, Is.EqualTo(PhysicalAugmentationPhase.Failed));
        }

        [Test]
        public void DifferentPointsCanPlayAtTheSameTime()
        {
            var started = _controller.Start();
            _locator.Emit(Stable("point_a", started.Generation), Stable("point_b", started.Generation));

            var first = _controller.Activate(new PhysicalAugmentationPointId("point_a"), started.Generation);
            var second = _controller.Activate(new PhysicalAugmentationPointId("point_b"), started.Generation);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(second.Succeeded, Is.True);
            Assert.That(_sink.Last.Phase, Is.EqualTo(PhysicalAugmentationPhase.Playing));
            Assert.That(_sink.Find("point_a").ActivityPhase, Is.EqualTo(PhysicalAugmentationPointActivityPhase.Playing));
            Assert.That(_sink.Find("point_b").ActivityPhase, Is.EqualTo(PhysicalAugmentationPointActivityPhase.Playing));
            Assert.That(_performances.Leases, Has.Count.EqualTo(2));
        }

        [Test]
        public void FailedPerformanceDoesNotConsumeOpportunity()
        {
            var started = _controller.Start();
            _locator.Emit(Stable("point_a", started.Generation));
            Assert.That(_controller.Activate(new PhysicalAugmentationPointId("point_a"), started.Generation).Succeeded, Is.True);
            _performances.LastLease.Complete(false, "performance.injected_failure");

            var retry = _controller.Activate(new PhysicalAugmentationPointId("point_a"), started.Generation);

            Assert.That(retry.Succeeded, Is.True);
            Assert.That(_performances.PreparedCount, Is.EqualTo(2));
        }

        [Test]
        public void SuccessfulPerformanceKeepsFinalPresentationUntilExplicitStopAndCanReplay()
        {
            var started = _controller.Start();
            _locator.Emit(Stable("point_a", started.Generation));
            Assert.That(_controller.Activate(new PhysicalAugmentationPointId("point_a"), started.Generation).Succeeded, Is.True);
            var completedLease = _performances.LastLease;
            completedLease.Complete(true);

            Assert.That(_sink.Last.Phase, Is.EqualTo(PhysicalAugmentationPhase.Playing));
            Assert.That(
                _sink.Find("point_a").ActivityPhase,
                Is.EqualTo(PhysicalAugmentationPointActivityPhase.Presented));
            Assert.That(completedLease.DisposeCount, Is.Zero);

            var stop = _controller.StopPerformances();

            Assert.That(stop.Succeeded, Is.True);
            Assert.That(_sink.Last.Phase, Is.EqualTo(PhysicalAugmentationPhase.Ready));
            Assert.That(_sink.Find("point_a").ActivityPhase, Is.EqualTo(PhysicalAugmentationPointActivityPhase.Available));
            Assert.That(completedLease.StopCount, Is.EqualTo(1));
            Assert.That(completedLease.DisposeCount, Is.EqualTo(1));

            var replay = _controller.Activate(new PhysicalAugmentationPointId("point_a"), started.Generation);

            Assert.That(replay.Succeeded, Is.True);
            Assert.That(_performances.PreparedCount, Is.EqualTo(2));
            Assert.That(_sink.Find("point_a").ActivityPhase, Is.EqualTo(PhysicalAugmentationPointActivityPhase.Playing));
        }

        [Test]
        public void ActivatingThePlayingPointRestartsItWithoutAllowingTwoPlayingLeases()
        {
            var started = _controller.Start();
            _locator.Emit(Stable("point_a", started.Generation));
            Assert.That(_controller.Activate(new PhysicalAugmentationPointId("point_a"), started.Generation).Succeeded, Is.True);
            var firstLease = _performances.LastLease;

            var replay = _controller.Activate(new PhysicalAugmentationPointId("point_a"), started.Generation);

            Assert.That(replay.Succeeded, Is.True);
            Assert.That(firstLease.StopCount, Is.EqualTo(1));
            Assert.That(_performances.PreparedCount, Is.EqualTo(2));
            Assert.That(_performances.LastLease, Is.Not.SameAs(firstLease));
            Assert.That(_sink.Find("point_a").ActivityPhase, Is.EqualTo(PhysicalAugmentationPointActivityPhase.Playing));
        }

        [Test]
        public void LocalizationLossStopsOnlyTheAffectedPlayingPoint()
        {
            var started = _controller.Start();
            _locator.Emit(Stable("point_a", started.Generation), Stable("point_b", started.Generation));
            Assert.That(_controller.Activate(new PhysicalAugmentationPointId("point_a"), started.Generation).Succeeded, Is.True);
            Assert.That(_controller.Activate(new PhysicalAugmentationPointId("point_b"), started.Generation).Succeeded, Is.True);
            var firstLease = _performances.Leases[0];
            var secondLease = _performances.Leases[1];

            _locator.Emit(Lost("point_a", started.Generation), Stable("point_b", started.Generation));

            Assert.That(firstLease.StopCount, Is.EqualTo(1));
            Assert.That(secondLease.StopCount, Is.Zero);
            Assert.That(_sink.Last.Phase, Is.EqualTo(PhysicalAugmentationPhase.Playing));
            Assert.That(_sink.Find("point_b").ActivityPhase, Is.EqualTo(PhysicalAugmentationPointActivityPhase.Playing));
        }

        [Test]
        public void LocalizationLossStopsVisiblePerformanceWithoutConsumingPoint()
        {
            var started = _controller.Start();
            _locator.Emit(Stable("point_a", started.Generation));
            Assert.That(_controller.Activate(new PhysicalAugmentationPointId("point_a"), started.Generation).Succeeded, Is.True);
            var firstLease = _performances.LastLease;

            _locator.Emit(Lost("point_a", started.Generation));
            _locator.Emit(Stable("point_a", started.Generation));
            var retry = _controller.Activate(new PhysicalAugmentationPointId("point_a"), started.Generation);

            Assert.That(firstLease.StopCount, Is.EqualTo(1));
            Assert.That(retry.Succeeded, Is.True);
        }

        [Test]
        public void SuppressionIsVisibleAndRejectsActivation()
        {
            _suppression.Suppressed = true;
            var started = _controller.Start();
            _locator.Emit(Stable("point_a", started.Generation));

            var result = _controller.Activate(new PhysicalAugmentationPointId("point_a"), started.Generation);

            Assert.That(_sink.Find("point_a").ActivityPhase, Is.EqualTo(PhysicalAugmentationPointActivityPhase.Suppressed));
            Assert.That(result.FailureCode, Is.EqualTo(PhysicalAugmentationFailureCode.Suppressed));
        }

        [Test]
        public void EnvironmentDepthGateNeverSilentlyDowngradesMandatoryPolicy()
        {
            var gate = new EnvironmentDepthCapabilityGate(null);
            var mandatory = Definition("point_a", 0);
            var approvedFallback = Definition(
                "point_b",
                2,
                PhysicalAugmentationOcclusionPolicy.AllowProxyFallback);

            Assert.That(gate.IsAvailable(mandatory, out var mandatoryTag), Is.False);
            Assert.That(mandatoryTag, Is.EqualTo("physical_augmentation.environment_depth_unavailable"));
            Assert.That(gate.IsAvailable(approvedFallback, out var fallbackTag), Is.True);
            Assert.That(fallbackTag, Is.Empty);
        }

        [Test]
        public void BackendPublicSurfaceDoesNotIntroduceSceneIdentity()
        {
            foreach (var type in typeof(PhysicalAugmentationController).Assembly.GetExportedTypes())
                foreach (var member in type.GetMembers())
                    Assert.That(member.ToString(), Does.Not.Contain("SceneId"));
        }

        PhysicalAugmentationDefinition Definition(
            string pointId,
            int offset,
            PhysicalAugmentationOcclusionPolicy occlusionPolicy =
                PhysicalAugmentationOcclusionPolicy.RequireEnvironmentDepthAndProxy)
            => new PhysicalAugmentationDefinition(
                new PhysicalAugmentationPointId(pointId),
                pointId,
                offset / 2 + 1,
                _assets[offset],
                _assets[offset + 1],
                0.6f,
                0.015f,
                2f,
                0.35f,
                0.03f,
                5f,
                0.5f,
                2f,
                occlusionPolicy);

        static PhysicalAugmentationPointState Stable(string pointId, long generation)
            => new PhysicalAugmentationPointState(
                new PhysicalAugmentationPointId(pointId),
                PhysicalAugmentationLocalizationPhase.Stable,
                generation,
                true,
                new Pose(Vector3.one, Quaternion.identity),
                1f,
                PhysicalAugmentationPointActivityPhase.Inactive);

        static PhysicalAugmentationPointState Lost(string pointId, long generation)
            => new PhysicalAugmentationPointState(
                new PhysicalAugmentationPointId(pointId),
                PhysicalAugmentationLocalizationPhase.Lost,
                generation,
                false,
                Pose.identity,
                0f,
                PhysicalAugmentationPointActivityPhase.Inactive);

        sealed class FakeLocator : IPhysicalAugmentationLocator
        {
            public event Action<IReadOnlyList<PhysicalAugmentationPointState>> StateChanged;
            public int StartCalls { get; private set; }
            public int StopCalls { get; private set; }
            public List<long> StartedGenerations { get; } = new List<long>();
            public bool StartSucceeded { get; set; } = true;
            public string StartDiagnosticTag { get; set; } = string.Empty;
            public bool TryStart(IReadOnlyList<PhysicalAugmentationDefinition> definitions, long generation, out string diagnosticTag)
            {
                StartCalls++;
                StartedGenerations.Add(generation);
                diagnosticTag = StartDiagnosticTag;
                return StartSucceeded;
            }
            public void Stop(long generation) => StopCalls++;
            public void Emit(params PhysicalAugmentationPointState[] states) => StateChanged?.Invoke(states);
        }

        sealed class FakeSuppression : IPhysicalAugmentationSuppressionSource
        {
            public bool Suppressed { get; set; }
            public bool IsSuppressed(PhysicalAugmentationPointId pointId, out string diagnosticTag)
            {
                diagnosticTag = Suppressed ? "suppression.injected" : string.Empty;
                return Suppressed;
            }
        }

        sealed class FakeCapability : IPhysicalAugmentationCapabilityGate
        {
            public bool IsAvailable(PhysicalAugmentationDefinition definition, out string diagnosticTag)
            {
                diagnosticTag = string.Empty;
                return true;
            }
        }

        sealed class FakePerformanceFactory : IPhysicalAugmentationPerformanceFactory
        {
            public int PreparedCount { get; private set; }
            public FakeLease LastLease { get; private set; }
            public List<FakeLease> Leases { get; } = new List<FakeLease>();
            public bool TryPrepare(
                PhysicalAugmentationDefinition definition,
                Pose resolvedPose,
                float uniformScale,
                long generation,
                out IPhysicalAugmentationPerformanceLease lease,
                out string diagnosticTag)
            {
                PreparedCount++;
                LastLease = new FakeLease();
                Leases.Add(LastLease);
                lease = LastLease;
                diagnosticTag = string.Empty;
                return true;
            }
        }

        sealed class FakeLease : IPhysicalAugmentationPerformanceLease
        {
            Action<PhysicalAugmentationPerformanceCompletion> _completed;
            public int StopCount { get; private set; }
            public int DisposeCount { get; private set; }
            public void Play(Action<PhysicalAugmentationPerformanceCompletion> completed) => _completed = completed;
            public void Stop() => StopCount++;
            public void Dispose() => DisposeCount++;
            public void Complete(bool succeeded, string tag = null)
                => _completed?.Invoke(new PhysicalAugmentationPerformanceCompletion(succeeded, tag));
        }

        sealed class StateSink : IPhysicalAugmentationStateSink
        {
            public PhysicalAugmentationState Last { get; private set; }
            public void OnPhysicalAugmentationStateChanged(PhysicalAugmentationState state) => Last = state;
            public PhysicalAugmentationPointState Find(string pointId)
            {
                for (var index = 0; index < Last.Points.Count; index++)
                    if (Last.Points[index].PointId.Value == pointId) return Last.Points[index];
                throw new AssertionException($"Point '{pointId}' was not published.");
            }
        }
    }
}
