using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.PhysicalAugmentation
{
    public sealed class PhysicalAugmentationFeaturePageActionBindingTests
    {
        static readonly SceneId GiantSaguaro = new SceneId("giant_saguaro");
        static readonly PhysicalAugmentationPointId Point =
            new PhysicalAugmentationPointId("physical_point_001");
        static readonly PhysicalAugmentationPointId OtherPoint =
            new PhysicalAugmentationPointId("physical_point_002");

        [Test]
        public void SpatialPermission_GatesPhysicalAnchorLocalizationStartup()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            var permission = new TestPermissionGate();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller,
                permission);

            Assert.That(controller.StartCalls, Is.Zero);
            permission.Publish(SpatialDataPermissionState.Denied);
            Assert.That(controller.StartCalls, Is.Zero);
            permission.Publish(SpatialDataPermissionState.Granted);
            permission.Publish(SpatialDataPermissionState.Granted);
            Assert.That(controller.StartCalls, Is.EqualTo(1));
        }

        [Test]
        public void RetryableStartFailureRequiresOneExplicitActionPerRetry()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            controller.StartResults.Enqueue(PhysicalAugmentationResult.Failure(
                PhysicalAugmentationFailureCode.CapabilityUnavailable,
                "physical_augmentation.locator_start_failed",
                1));
            controller.StartResults.Enqueue(PhysicalAugmentationResult.Success(2));
            var permission = new TestPermissionGate();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller,
                permission);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(session, GiantSaguaro, FlowPage.ForFeature(FeaturePageId.Model)));
            permission.Publish(SpatialDataPermissionState.Granted);

            Assert.That(controller.StartCalls, Is.EqualTo(1));
            Assert.That(sink.State.Interactable, Is.True);
            Assert.That(sink.State.Label, Is.EqualTo("重试定位"));

            permission.Publish(SpatialDataPermissionState.Granted);
            Assert.That(controller.StartCalls, Is.EqualTo(1),
                "Permission/state refreshes must not create an automatic retry loop.");

            binding.Invoke(session);

            Assert.That(controller.StartCalls, Is.EqualTo(2));
            controller.Publish(PhysicalState(StablePoint(Point)));
            Assert.That(sink.State.Interactable, Is.True);
            Assert.That(sink.State.Label, Is.EqualTo("现实演示"));
        }

        [Test]
        public void InvalidDefinitionStartFailureRemainsDisabledAndCannotRetry()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            controller.StartResults.Enqueue(PhysicalAugmentationResult.Failure(
                PhysicalAugmentationFailureCode.InvalidDefinition,
                "physical_augmentation.definition_invalid",
                1));
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(session, GiantSaguaro, FlowPage.ForFeature(FeaturePageId.Model)));

            Assert.That(sink.State.Interactable, Is.False);
            Assert.That(sink.State.Status, Does.Contain("配置无效"));
            binding.Invoke(session);
            Assert.That(controller.StartCalls, Is.EqualTo(1));
        }

        [Test]
        public void TransientLocalizationFailureOffersOneExplicitReloadPerAction()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(session, GiantSaguaro, FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(FailedLocalizationPoint(
                Point,
                "physical_locator.meta_load_failed")));

            Assert.That(sink.State.Interactable, Is.True);
            Assert.That(sink.State.Label, Is.EqualTo("重试定位"));
            Assert.That(controller.RetryLocalizationCalls, Is.Zero);

            binding.Invoke(session);

            Assert.That(controller.RetryLocalizationCalls, Is.EqualTo(1));
            Assert.That(controller.ActivationCount, Is.Zero);
        }

        [Test]
        public void TransientLocalizationReloadFailureRemainsExplicitlyRetryable()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            controller.RetryLocalizationResults.Enqueue(PhysicalAugmentationResult.Failure(
                PhysicalAugmentationFailureCode.RuntimeFailed,
                "physical_augmentation.localization_retry_exception",
                2));
            controller.RetryLocalizationResults.Enqueue(PhysicalAugmentationResult.Success(3));
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(session, GiantSaguaro, FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(FailedLocalizationPoint(
                Point,
                "physical_locator.platform_exception")));

            binding.Invoke(session);
            Assert.That(controller.RetryLocalizationCalls, Is.EqualTo(1));
            Assert.That(sink.State.Interactable, Is.True);
            Assert.That(sink.State.Label, Is.EqualTo("重试定位"));

            binding.Invoke(session);
            Assert.That(controller.RetryLocalizationCalls, Is.EqualTo(2));
        }

        [Test]
        public void PermanentLocalizationFailureStaysDisabled()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(session, GiantSaguaro, FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(FailedLocalizationPoint(
                Point,
                "physical_locator.scale_out_of_range")));

            Assert.That(sink.State.Interactable, Is.False);
            binding.Invoke(session);
            Assert.That(controller.RetryLocalizationCalls, Is.Zero);
        }

        [Test]
        public void MappedModelPageKeepsUnavailableActionVisibleWithReason()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(
                session,
                GiantSaguaro,
                FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(
                new PhysicalAugmentationPointState(
                    Point,
                    PhysicalAugmentationLocalizationPhase.Unconfigured,
                    1,
                    false,
                    Pose.identity,
                    0f,
                    PhysicalAugmentationPointActivityPhase.Inactive)));

            Assert.That(sink.State.Visible, Is.True);
            Assert.That(sink.State.Interactable, Is.False);
            Assert.That(sink.State.Label, Is.EqualTo("现实演示"));
            Assert.That(sink.State.Status, Does.Contain("尚未配置"));
        }

        [Test]
        public void StableMappedPointInvokesControllerAndEntersExplicitlyReversibleFocus()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(
                session,
                GiantSaguaro,
                FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(
                new PhysicalAugmentationPointState(
                    Point,
                    PhysicalAugmentationLocalizationPhase.Stable,
                    1,
                    true,
                    new Pose(new Vector3(1f, 2f, 3f), Quaternion.identity),
                    1f,
                    PhysicalAugmentationPointActivityPhase.Available)));

            Assert.That(sink.State.Visible, Is.True);
            Assert.That(sink.State.Interactable, Is.True);
            binding.Invoke(session);
            Assert.That(controller.ActivationCount, Is.EqualTo(1));
            Assert.That(controller.ActivatedPoint, Is.EqualTo(Point));
            Assert.That(controller.ActivatedGeneration, Is.EqualTo(1));
            Assert.That(sink.State.FocusActive, Is.True);
            Assert.That(sink.State.FocusExitLabel, Is.EqualTo("返回面板"));

            binding.ExitFocus(session);

            Assert.That(controller.StopPerformancesCalls, Is.EqualTo(1));
            Assert.That(sink.State.FocusActive, Is.False);
            Assert.That(sink.State.Visible, Is.True);
        }

        [Test]
        public void ReturningToPanelStopsRealityPerformanceBeforeAllowingAnotherStart()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(
                session,
                GiantSaguaro,
                FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(
                new PhysicalAugmentationPointState(
                    Point,
                    PhysicalAugmentationLocalizationPhase.Stable,
                    1,
                    true,
                    Pose.identity,
                    1f,
                    PhysicalAugmentationPointActivityPhase.Available)));
            binding.Invoke(session);
            Assert.That(sink.State.FocusActive, Is.True);
            binding.ExitFocus(session);

            Assert.That(sink.State.FocusActive, Is.False);
            Assert.That(sink.State.Interactable, Is.True);
            Assert.That(controller.StopPerformancesCalls, Is.EqualTo(1));

            binding.Invoke(session);

            Assert.That(controller.ActivationCount, Is.EqualTo(2));
            Assert.That(sink.State.FocusActive, Is.True);
        }

        [Test]
        public void AnotherPlayingPointKeepsTheMappedRealityActionDisabled()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            flow.Publish(State(
                SessionToken.CreateNew(),
                GiantSaguaro,
                FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(new PhysicalAugmentationState(
                2,
                1,
                PhysicalAugmentationPhase.Playing,
                new[]
                {
                    new PhysicalAugmentationPointState(
                        Point,
                        PhysicalAugmentationLocalizationPhase.Stable,
                        1,
                        true,
                        Pose.identity,
                        1f,
                        PhysicalAugmentationPointActivityPhase.Available),
                    new PhysicalAugmentationPointState(
                        OtherPoint,
                        PhysicalAugmentationLocalizationPhase.Stable,
                        1,
                        true,
                        Pose.identity,
                        1f,
                        PhysicalAugmentationPointActivityPhase.Playing)
                }));

            Assert.That(sink.State.Interactable, Is.False);
            Assert.That(sink.State.Status, Does.Contain("另一段"));
        }

        [Test]
        public void ActivationFailureKeepsPanelFocusInactiveAndPublishesConcreteReason()
        {
            var flow = new TestFlow();
            var controller = new TestController
            {
                ActivationResult = PhysicalAugmentationResult.Failure(
                    PhysicalAugmentationFailureCode.CapabilityUnavailable,
                    "physical_augmentation.environment_depth_unavailable",
                    1)
            };
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(
                session,
                GiantSaguaro,
                FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(
                new PhysicalAugmentationPointState(
                    Point,
                    PhysicalAugmentationLocalizationPhase.Stable,
                    1,
                    true,
                    Pose.identity,
                    1f,
                    PhysicalAugmentationPointActivityPhase.Available)));

            binding.Invoke(session);

            Assert.That(sink.State.FocusActive, Is.False);
            Assert.That(sink.State.Visible, Is.True);
            Assert.That(sink.State.Interactable, Is.False);
            Assert.That(sink.State.Status, Does.Contain("空间数据"));
            binding.Invoke(session);
            Assert.That(controller.ActivationCount, Is.EqualTo(1),
                "A disabled safety failure must reject duplicate semantic submissions.");
        }

        [Test]
        public void RuntimeActivationFailureCanBeRetriedOnlyByAnotherExplicitAction()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            controller.ActivationResults.Enqueue(PhysicalAugmentationResult.Failure(
                PhysicalAugmentationFailureCode.RuntimeFailed,
                "physical_augmentation.play_failed",
                1));
            controller.ActivationResults.Enqueue(PhysicalAugmentationResult.Success(1));
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(session, GiantSaguaro, FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(StablePoint(Point)));
            binding.Invoke(session);

            Assert.That(controller.ActivationCount, Is.EqualTo(1));
            Assert.That(sink.State.FocusActive, Is.False);
            Assert.That(sink.State.Interactable, Is.True);
            Assert.That(sink.State.Label, Is.EqualTo("重试演示"));

            binding.Invoke(session);

            Assert.That(controller.ActivationCount, Is.EqualTo(2));
            Assert.That(sink.State.FocusActive, Is.True);
        }

        [Test]
        public void CompletedPerformanceFailureCanBeExplicitlyReplayed()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(session, GiantSaguaro, FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(
                new PhysicalAugmentationPointState(
                    Point,
                    PhysicalAugmentationLocalizationPhase.Stable,
                    1,
                    true,
                    Pose.identity,
                    1f,
                    PhysicalAugmentationPointActivityPhase.Failed,
                    "physical_augmentation.performance_failed")));

            Assert.That(sink.State.Interactable, Is.True);
            Assert.That(sink.State.Label, Is.EqualTo("重试演示"));
            binding.Invoke(session);
            Assert.That(controller.ActivationCount, Is.EqualTo(1));
            Assert.That(sink.State.FocusActive, Is.True);
        }

        [Test]
        public void LeavingModelPageAlwaysRestoresPanelFocusState()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(
                session,
                GiantSaguaro,
                FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(
                new PhysicalAugmentationPointState(
                    Point,
                    PhysicalAugmentationLocalizationPhase.Stable,
                    1,
                    true,
                    Pose.identity,
                    1f,
                    PhysicalAugmentationPointActivityPhase.Available)));
            binding.Invoke(session);
            Assert.That(sink.State.FocusActive, Is.True);

            flow.Publish(State(session, GiantSaguaro, FlowPage.Main));
            Assert.That(controller.StopPerformancesCalls, Is.EqualTo(1));
            Assert.That(sink.State.FocusActive, Is.False);
            Assert.That(sink.State.Visible, Is.False);

            flow.Publish(State(
                session,
                GiantSaguaro,
                FlowPage.ForFeature(FeaturePageId.Model)));
            Assert.That(sink.State.FocusActive, Is.False);
        }

        [Test]
        public void ReplacingTheModelContextStopsRealityPerformanceBeforeRebindingThePanel()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(
                session,
                GiantSaguaro,
                FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(StablePoint(Point)));
            binding.Invoke(session);
            Assert.That(sink.State.FocusActive, Is.True);

            flow.Publish(State(
                session,
                new SceneId("baobab"),
                FlowPage.ForFeature(FeaturePageId.Model)));

            Assert.That(controller.StopPerformancesCalls, Is.EqualTo(1));
            Assert.That(sink.State.FocusActive, Is.False);
            Assert.That(sink.State.Visible, Is.False);
        }

        [Test]
        public void EnvironmentDepthFailureDisablesVisibleActionWithUserReason()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);

            flow.Publish(State(
                SessionToken.CreateNew(),
                GiantSaguaro,
                FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(
                new PhysicalAugmentationPointState(
                    Point,
                    PhysicalAugmentationLocalizationPhase.Stable,
                    1,
                    true,
                    new Pose(Vector3.one, Quaternion.identity),
                    1f,
                    PhysicalAugmentationPointActivityPhase.Failed,
                    "physical_augmentation.environment_depth_unavailable")));

            Assert.That(sink.State.Visible, Is.True);
            Assert.That(sink.State.Interactable, Is.False);
            Assert.That(sink.State.Status, Does.Contain("空间数据"));
        }

        [Test]
        public void MainPageAndUnmappedModelPageDoNotExposeAction()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(session, GiantSaguaro, FlowPage.Main));
            Assert.That(sink.State.Visible, Is.False);

            flow.Publish(State(
                session,
                new SceneId("baobab"),
                FlowPage.ForFeature(FeaturePageId.Model)));
            Assert.That(sink.State.Visible, Is.False);
        }

        [Test]
        public void OneActionStartsEveryAvailablePointMappedToTheScene()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(Point, OtherPoint),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(session, GiantSaguaro, FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(
                StablePoint(Point),
                StablePoint(OtherPoint)));

            Assert.That(sink.State.Interactable, Is.True);
            Assert.That(sink.State.Status, Does.Contain("2 个现实位置"));
            binding.Invoke(session);

            Assert.That(controller.ActivatedPoints, Is.EqualTo(new[] { Point, OtherPoint }));
            Assert.That(sink.State.FocusActive, Is.True);
            Assert.That(sink.State.Status, Does.Contain("现实演示已开始"));
        }

        [Test]
        public void MissingMappedPointDoesNotBlockTheAvailablePoint()
        {
            var flow = new TestFlow();
            var controller = new TestController();
            using var binding = new PhysicalAugmentationVisitorBinding(
                flow,
                new TestAssociations(Point, OtherPoint),
                controller);
            var sink = new RecordingActionSink();
            using var observation = binding.Observe(sink);
            var session = SessionToken.CreateNew();

            flow.Publish(State(session, GiantSaguaro, FlowPage.ForFeature(FeaturePageId.Model)));
            controller.Publish(PhysicalState(StablePoint(Point)));

            Assert.That(sink.State.Interactable, Is.True);
            Assert.That(sink.State.Status, Does.Contain("1/2"));
            binding.Invoke(session);

            Assert.That(controller.ActivatedPoints, Is.EqualTo(new[] { Point }));
            Assert.That(sink.State.FocusActive, Is.True);
            Assert.That(sink.State.Status, Does.Contain("1/2"));
        }

        static ExperienceFlowState State(SessionToken session, SceneId sceneId, FlowPage page)
            => new ExperienceFlowState(
                session,
                1,
                sceneId,
                "title",
                "subtitle",
                "summary",
                page,
                Array.Empty<FeaturePageId>());

        static PhysicalAugmentationState PhysicalState(params PhysicalAugmentationPointState[] points)
            => new PhysicalAugmentationState(
                1,
                1,
                PhysicalAugmentationPhase.Ready,
                points);

        static PhysicalAugmentationPointState StablePoint(PhysicalAugmentationPointId pointId)
            => new PhysicalAugmentationPointState(
                pointId,
                PhysicalAugmentationLocalizationPhase.Stable,
                1,
                true,
                Pose.identity,
                1f,
                PhysicalAugmentationPointActivityPhase.Available);

        static PhysicalAugmentationPointState FailedLocalizationPoint(
            PhysicalAugmentationPointId pointId,
            string diagnosticTag)
            => new PhysicalAugmentationPointState(
                pointId,
                PhysicalAugmentationLocalizationPhase.Failed,
                1,
                false,
                Pose.identity,
                0f,
                PhysicalAugmentationPointActivityPhase.Inactive,
                diagnosticTag);

        sealed class TestAssociations : IPhysicalAugmentationSceneAssociationSource
        {
            readonly IReadOnlyList<PhysicalAugmentationPointId> _points;

            public TestAssociations(params PhysicalAugmentationPointId[] points)
                => _points = points == null || points.Length == 0
                    ? new[] { Point }
                    : points;

            public IReadOnlyList<PhysicalAugmentationPointId> GetPhysicalAugmentationPoints(SceneId sceneId)
            {
                return sceneId == GiantSaguaro
                    ? _points
                    : Array.Empty<PhysicalAugmentationPointId>();
            }
        }

        sealed class TestFlow : IExperienceFlow
        {
            IFlowStateSink _sink;
            public IDisposable Observe(IFlowStateSink sink)
            {
                _sink = sink;
                return new CallbackDisposable(() => _sink = null);
            }
            public void Publish(ExperienceFlowState state) => _sink?.OnStateChanged(state);
            public FlowPrepareResult Prepare(SessionToken candidate, SceneId sceneId)
                => FlowPrepareResult.Failure(new UserFault("unused"));
            public FlowResult EnterFeature(SessionToken session, FeaturePageId page)
                => FlowResult.Reject(FlowFailure.InvalidTransition);
            public FlowResult BackToMain(SessionToken session)
                => FlowResult.Reject(FlowFailure.InvalidTransition);
            public FlowResult Close(SessionToken session)
                => FlowResult.Reject(FlowFailure.InvalidTransition);
        }

        sealed class TestController : IPhysicalAugmentationController
        {
            IPhysicalAugmentationStateSink _sink;
            PhysicalAugmentationState _state = new PhysicalAugmentationState(
                0,
                0,
                PhysicalAugmentationPhase.Stopped,
                Array.Empty<PhysicalAugmentationPointState>());
            public int ActivationCount { get; private set; }
            public PhysicalAugmentationPointId ActivatedPoint { get; private set; }
            public long ActivatedGeneration { get; private set; }
            public List<PhysicalAugmentationPointId> ActivatedPoints { get; } =
                new List<PhysicalAugmentationPointId>();
            public PhysicalAugmentationResult ActivationResult { get; set; } =
                PhysicalAugmentationResult.Success(1);
            public Queue<PhysicalAugmentationResult> ActivationResults { get; } =
                new Queue<PhysicalAugmentationResult>();
            public Queue<PhysicalAugmentationResult> StartResults { get; } =
                new Queue<PhysicalAugmentationResult>();
            public Queue<PhysicalAugmentationResult> RetryLocalizationResults { get; } =
                new Queue<PhysicalAugmentationResult>();
            public int StartCalls { get; private set; }
            public int RetryLocalizationCalls { get; private set; }
            public int StopPerformancesCalls { get; private set; }
            public PhysicalAugmentationResult Start()
            {
                StartCalls++;
                return StartResults.Count > 0
                    ? StartResults.Dequeue()
                    : PhysicalAugmentationResult.Success(1);
            }
            public PhysicalAugmentationResult Activate(PhysicalAugmentationPointId pointId, long generation)
            {
                ActivationCount++;
                ActivatedPoint = pointId;
                ActivatedGeneration = generation;
                ActivatedPoints.Add(pointId);
                var configured = ActivationResults.Count > 0
                    ? ActivationResults.Dequeue()
                    : ActivationResult;
                return configured.Succeeded
                    ? PhysicalAugmentationResult.Success(generation)
                    : PhysicalAugmentationResult.Failure(
                        configured.FailureCode,
                        configured.DiagnosticTag,
                        generation);
            }
            public PhysicalAugmentationResult RetryLocalization()
            {
                RetryLocalizationCalls++;
                var result = RetryLocalizationResults.Count > 0
                    ? RetryLocalizationResults.Dequeue()
                    : PhysicalAugmentationResult.Success(2);
                if (!result.Succeeded)
                {
                    _state = new PhysicalAugmentationState(
                        result.Generation,
                        result.Generation,
                        PhysicalAugmentationPhase.Failed,
                        Array.Empty<PhysicalAugmentationPointState>());
                    _sink?.OnPhysicalAugmentationStateChanged(_state);
                }
                return result;
            }
            public PhysicalAugmentationResult Stop() => PhysicalAugmentationResult.Success(1);
            public PhysicalAugmentationResult StopPerformances()
            {
                StopPerformancesCalls++;
                return PhysicalAugmentationResult.Success(1);
            }
            public IDisposable Observe(IPhysicalAugmentationStateSink sink)
            {
                _sink = sink;
                sink.OnPhysicalAugmentationStateChanged(_state);
                return new CallbackDisposable(() => _sink = null);
            }
            public void Publish(PhysicalAugmentationState state)
            {
                _state = state;
                _sink?.OnPhysicalAugmentationStateChanged(state);
            }
        }

        sealed class TestPermissionGate : ISpatialDataPermissionGate
        {
            ISpatialDataPermissionStateSink _sink;
            public SpatialDataPermissionState CurrentState { get; private set; } =
                SpatialDataPermissionState.Unknown;
            public SpatialDataPermissionCommandResult Request() =>
                SpatialDataPermissionCommandResult.Success;
            public SpatialDataPermissionCommandResult Refresh() =>
                SpatialDataPermissionCommandResult.Success;
            public IDisposable Observe(ISpatialDataPermissionStateSink sink)
            {
                _sink = sink;
                sink.OnSpatialDataPermissionStateChanged(CurrentState);
                return new CallbackDisposable(() => _sink = null);
            }
            public void Publish(SpatialDataPermissionState state)
            {
                CurrentState = state;
                _sink?.OnSpatialDataPermissionStateChanged(state);
            }
            public void Dispose() => _sink = null;
        }

        sealed class RecordingActionSink : IFeaturePageActionStateSink
        {
            public FeaturePageActionState State { get; private set; }
            public void OnFeaturePageActionStateChanged(FeaturePageActionState state) => State = state;
        }

        sealed class CallbackDisposable : IDisposable
        {
            Action _callback;
            public CallbackDisposable(Action callback) => _callback = callback;
            public void Dispose()
            {
                var callback = _callback;
                _callback = null;
                callback?.Invoke();
            }
        }
    }
}
