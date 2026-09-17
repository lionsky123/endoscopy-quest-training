using System;
using System.Collections.Generic;
using System.Threading;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.Experience.Flow;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode.Experience
{
    public sealed class ExperienceFlowTests
    {
        static readonly SceneId Scene = new SceneId("test_scene");

        [Test]
        public void PreparedFlowCommit_PublishesMainState()
        {
            var fixture = new Fixture();
            using var subscription = fixture.Flow.Observe(fixture.States);
            var session = fixture.Commit();

            Assert.That(fixture.States.Latest.Session, Is.EqualTo(session));
            Assert.That(fixture.States.Latest.SceneId, Is.EqualTo(Scene));
            Assert.That(fixture.States.Latest.Page, Is.EqualTo(FlowPage.Main));
            Assert.That(fixture.States.Latest.AvailablePages, Is.EquivalentTo(new[] { FeaturePageId.Video }));
        }

        [Test]
        public void FeatureRoundTrip_InvokesLifecycleInOrder_AndReturnsToMain()
        {
            var fixture = new Fixture();
            var session = fixture.Commit();

            Assert.That(fixture.Flow.EnterFeature(session, FeaturePageId.Video).Succeeded, Is.True);
            Assert.That(fixture.Flow.BackToMain(session).Succeeded, Is.True);

            Assert.That(
                fixture.Calls,
                Is.EqualTo(new[] { "main.before", "feature.prepare", "feature.activate", "feature.deactivate", "feature.release" }));
        }

        [Test]
        public void ActivateFailure_ReleasesPreparedFeature_AndRestoresMainWithFault()
        {
            var expectedFault = new UserFault("injected activation failure");
            var fixture = new Fixture();
            fixture.Lifecycle.ActivateResult = FlowResult.Reject(FlowFailure.LifecycleFailed, expectedFault);
            var session = fixture.Commit();

            var result = fixture.Flow.EnterFeature(session, FeaturePageId.Video);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(FlowFailure.LifecycleFailed));
            Assert.That(fixture.Calls, Is.EqualTo(new[] { "main.before", "feature.prepare", "feature.activate", "feature.release" }));
            Assert.That(fixture.States.Latest.Page, Is.EqualTo(FlowPage.Main));
            Assert.That(fixture.States.Latest.Fault, Is.SameAs(expectedFault));
        }

        [Test]
        public void ReleaseFailure_ReturnsToMain_WithoutRetryingDisposedLifecycle()
        {
            var expectedFault = new UserFault("injected release failure");
            var fixture = new Fixture();
            fixture.Lifecycle.ReleaseResults.Enqueue(
                FlowResult.Reject(FlowFailure.LifecycleFailed, expectedFault));
            var session = fixture.Commit();
            Assert.That(fixture.Flow.EnterFeature(session, FeaturePageId.Video).Succeeded, Is.True);

            var first = fixture.Flow.BackToMain(session);
            var second = fixture.Flow.BackToMain(session);

            Assert.That(first.Succeeded, Is.False);
            Assert.That(first.Failure, Is.EqualTo(FlowFailure.LifecycleFailed));
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.Failure, Is.EqualTo(FlowFailure.InvalidTransition));
            Assert.That(fixture.States.Latest.Page, Is.EqualTo(FlowPage.Main));
            Assert.That(fixture.States.Latest.Fault, Is.SameAs(expectedFault));
            Assert.That(
                fixture.Calls,
                Is.EqualTo(new[]
                {
                    "main.before",
                    "feature.prepare",
                    "feature.activate",
                    "feature.deactivate",
                    "feature.release"
                }));
        }

        [Test]
        public void DeactivateFailure_StillAttemptsRelease_AndReturnsToMain()
        {
            var fixture = new Fixture();
            fixture.Lifecycle.DeactivateResult = FlowResult.Reject(
                FlowFailure.LifecycleFailed,
                new UserFault("injected deactivate failure"));
            var session = fixture.Commit();
            Assert.That(fixture.Flow.EnterFeature(session, FeaturePageId.Video).Succeeded, Is.True);

            var result = fixture.Flow.BackToMain(session);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(fixture.States.Latest.Page, Is.EqualTo(FlowPage.Main));
            Assert.That(
                fixture.Calls,
                Is.EqualTo(new[]
                {
                    "main.before",
                    "feature.prepare",
                    "feature.activate",
                    "feature.deactivate",
                    "feature.release"
                }));
        }

        [Test]
        public void ThrowingRelease_IsolatedAndDoesNotLockFlow()
        {
            var fixture = new Fixture();
            fixture.Lifecycle.ReleaseException = new InvalidOperationException("injected release exception");
            var session = fixture.Commit();
            Assert.That(fixture.Flow.EnterFeature(session, FeaturePageId.Video).Succeeded, Is.True);

            var result = fixture.Flow.BackToMain(session);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(FlowFailure.LifecycleFailed));
            Assert.That(fixture.States.Latest.Page, Is.EqualTo(FlowPage.Main));
            Assert.That(fixture.Flow.BackToMain(session).Failure, Is.EqualTo(FlowFailure.InvalidTransition));
        }

        [Test]
        public void StaleSession_IsRejectedWithoutTouchingFeatureLifecycle()
        {
            var fixture = new Fixture();
            fixture.Commit();

            var result = fixture.Flow.EnterFeature(SessionToken.CreateNew(), FeaturePageId.Video);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(FlowFailure.StaleSession));
            Assert.That(fixture.Calls, Is.Empty);
        }

        [Test]
        public void ThrowingStateObserver_DoesNotBlockHealthyObserverOrCommit()
        {
            var fixture = new Fixture();
            using var throwing = fixture.Flow.Observe(new ThrowAfterInitialStateSink());
            using var healthy = fixture.Flow.Observe(fixture.States);

            Assert.That(() => fixture.Commit(), Throws.Nothing);
            Assert.That(fixture.States.Latest.Page, Is.EqualTo(FlowPage.Main));
        }

        [Test]
        public void ObserverThatThrowsInitially_IsIsolatedAndRemovedBeforeLaterPublishes()
        {
            var fixture = new Fixture();

            Assert.That(
                () => fixture.Flow.Observe(new AlwaysThrowingStateSink()),
                Throws.Nothing);

            using var healthy = fixture.Flow.Observe(fixture.States);
            Assert.That(() => fixture.Commit(), Throws.Nothing);
            Assert.That(fixture.States.Latest.Page, Is.EqualTo(FlowPage.Main));
        }

        sealed class Fixture
        {
            public Fixture()
            {
                Calls = new List<string>();
                Lifecycle = new FakeLifecycle(Calls);
                Flow = new ExperienceFlow(
                    new DescriptorSource(),
                    new FeaturePageRegistry(new[] { Lifecycle }),
                    Thread.CurrentThread.ManagedThreadId);
                Flow.SetMainSurfaceLifecycle(new FakeMainSurface(Calls));
                States = new RecordingStateSink();
                Flow.Observe(States);
            }

            public ExperienceFlow Flow { get; }
            public FakeLifecycle Lifecycle { get; }
            public RecordingStateSink States { get; }
            public List<string> Calls { get; }

            public SessionToken Commit()
            {
                var session = SessionToken.CreateNew();
                var prepared = Flow.Prepare(session, Scene);
                Assert.That(prepared.Succeeded, Is.True);
                using (prepared.Lease)
                    prepared.Lease.Commit();
                return session;
            }
        }

        sealed class DescriptorSource : ISceneFlowDescriptorSource
        {
            public bool TryGet(SceneId sceneId, out SceneFlowDescriptor descriptor)
            {
                if (sceneId != Scene)
                {
                    descriptor = null;
                    return false;
                }

                descriptor = new SceneFlowDescriptor(
                    Scene,
                    "Test",
                    "Subtitle",
                    "Summary",
                    new[] { FeaturePageId.Video });
                return true;
            }
        }

        sealed class FakeMainSurface : IMainSurfaceLifecycle
        {
            readonly List<string> _calls;

            public FakeMainSurface(List<string> calls) => _calls = calls;

            public void BeforeFeatureEnter(SessionToken session) => _calls.Add("main.before");
        }

        sealed class FakeLifecycle : IFeaturePageLifecycle
        {
            readonly List<string> _calls;

            public FakeLifecycle(List<string> calls) => _calls = calls;

            public FeaturePageId PageId => FeaturePageId.Video;
            public FlowResult PrepareResult { get; set; } = FlowResult.Success;
            public FlowResult ActivateResult { get; set; } = FlowResult.Success;
            public FlowResult DeactivateResult { get; set; } = FlowResult.Success;
            public Exception ReleaseException { get; set; }
            public Queue<FlowResult> ReleaseResults { get; } = new Queue<FlowResult>();

            public FlowResult Prepare(SessionToken session, SceneId sceneId)
            {
                _calls.Add("feature.prepare");
                return PrepareResult;
            }

            public FlowResult Activate(SessionToken session)
            {
                _calls.Add("feature.activate");
                return ActivateResult;
            }

            public FlowResult Deactivate(SessionToken session)
            {
                _calls.Add("feature.deactivate");
                return DeactivateResult;
            }

            public FlowResult Release(SessionToken session)
            {
                _calls.Add("feature.release");
                if (ReleaseException != null)
                    throw ReleaseException;
                return ReleaseResults.Count > 0 ? ReleaseResults.Dequeue() : FlowResult.Success;
            }
        }

        sealed class RecordingStateSink : IFlowStateSink
        {
            readonly List<ExperienceFlowState> _states = new List<ExperienceFlowState>();

            public ExperienceFlowState Latest => _states[^1];

            public void OnStateChanged(ExperienceFlowState state) => _states.Add(state);
        }

        sealed class ThrowAfterInitialStateSink : IFlowStateSink
        {
            int _publishCount;

            public void OnStateChanged(ExperienceFlowState state)
            {
                if (++_publishCount > 1)
                    throw new InvalidOperationException("Injected observer failure.");
            }
        }

        sealed class AlwaysThrowingStateSink : IFlowStateSink
        {
            public void OnStateChanged(ExperienceFlowState state)
                => throw new InvalidOperationException("Injected initial observer failure.");
        }
    }
}
