using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BotanicalGardenQR.Effect.Backend;
using BotanicalGardenQR.Effect.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class EffectControllerLifecycleTests
    {
        readonly List<GameObject> _ownedObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _ownedObjects.Count - 1; index >= 0; index--)
            {
                if (_ownedObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(_ownedObjects[index]);
            }
            _ownedObjects.Clear();
        }

        [Test]
        public void ValidationFailureLeavesControllerRetryableAndCloseAllowsReopen()
        {
            var controller = EffectModuleFactory.Create(Own("EffectControllerOwner").transform);
            var definition = new EffectDefinition(Own("EffectPrefab"), EffectTriggerPolicy.OnCommand);
            var sink = new RecordingSink();

            using (controller.Observe(sink))
            {
                var invalid = controller.Open(default, definition);
                Assert.That(invalid.Succeeded, Is.False);
                Assert.That(invalid.FailureCode, Is.EqualTo(EffectFailureCode.StaleSession));
                Assert.That(sink.Latest.Phase, Is.EqualTo(EffectPhase.Closed));

                var firstSession = SessionToken.CreateNew();
                Assert.That(controller.Open(firstSession, definition).Succeeded, Is.True);
                Assert.That(sink.Latest.Phase, Is.EqualTo(EffectPhase.Ready));
                Assert.That(controller.Dispatch(firstSession, EffectIntent.Trigger).Succeeded, Is.True);
                Assert.That(sink.Latest.Phase, Is.EqualTo(EffectPhase.Active));

                var staleSession = SessionToken.CreateNew();
                Assert.That(controller.Dispatch(staleSession, EffectIntent.Stop).FailureCode,
                    Is.EqualTo(EffectFailureCode.StaleSession));
                Assert.That(controller.Close(staleSession).FailureCode,
                    Is.EqualTo(EffectFailureCode.StaleSession));
                Assert.That(sink.Latest.Phase, Is.EqualTo(EffectPhase.Active));

                ExpectEditModeDestroyError();
                Assert.That(controller.Close(firstSession).Succeeded, Is.True);
                Assert.That(sink.Latest.Phase, Is.EqualTo(EffectPhase.Closed));
                Assert.That(controller.Close(firstSession).Succeeded, Is.True);

                var secondSession = SessionToken.CreateNew();
                Assert.That(controller.Open(secondSession, definition).Succeeded, Is.True,
                    "A completed Close must release controller ownership so a new session can open.");
                Assert.That(sink.Latest.Session, Is.EqualTo(secondSession));
                Assert.That(sink.Latest.Phase, Is.EqualTo(EffectPhase.Ready));

                ExpectEditModeDestroyError();
                Assert.That(controller.Close(secondSession).Succeeded, Is.True);
                Assert.That(sink.Latest.Phase, Is.EqualTo(EffectPhase.Closed));
            }
        }

        [Test]
        public void FlowBindingRetriesFailedOpenThenReleasesTheAcceptedSessionOnPageExit()
        {
            var flow = new RecordingFlow();
            var controller = new RecordingController(
                EffectResult.Failure(EffectFailureCode.LoadFailed, "test.open.failed"),
                EffectResult.Success());
            var definition = new EffectDefinition(Own("BoundEffectPrefab"), EffectTriggerPolicy.OnSessionOpen);
            var definitions = new SingleDefinitionSource(definition);
            var session = SessionToken.CreateNew();
            var sceneId = new SceneId("test_scene");

            using (EffectModuleFactory.BindToFlow(flow, definitions, controller))
            {
                flow.Publish(CreateFlowState(session, sceneId, 1, FlowPage.Main));
                Assert.That(controller.OpenSessions, Is.EqualTo(new[] { session }));
                Assert.That(controller.Intents, Is.Empty);

                flow.Publish(CreateFlowState(session, sceneId, 2, FlowPage.Main));
                Assert.That(controller.OpenSessions, Is.EqualTo(new[] { session, session }),
                    "A failed Open must not mark the session as owned; the next state can retry.");
                Assert.That(controller.Intents, Is.EqualTo(new[] { EffectIntent.Trigger }));

                flow.Publish(CreateFlowState(session, sceneId, 3, FlowPage.Closed));
                Assert.That(controller.ClosedSessions, Is.EqualTo(new[] { session }));
            }

            Assert.That(flow.SubscriptionDisposeCount, Is.EqualTo(1));
            Assert.That(controller.ClosedSessions, Has.Count.EqualTo(1));
        }

        static ExperienceFlowState CreateFlowState(
            SessionToken session,
            SceneId sceneId,
            long version,
            FlowPage page)
            => new ExperienceFlowState(
                session,
                version,
                sceneId,
                "Test",
                string.Empty,
                string.Empty,
                page,
                Array.Empty<FeaturePageId>());

        GameObject Own(string name)
        {
            var gameObject = new GameObject(name);
            _ownedObjects.Add(gameObject);
            return gameObject;
        }

        static void ExpectEditModeDestroyError()
            => LogAssert.Expect(
                LogType.Error,
                new Regex("Destroy may not be called from edit mode! Use DestroyImmediate instead\\."));

        sealed class RecordingSink : IEffectStateSink
        {
            public List<EffectState> States { get; } = new List<EffectState>();
            public EffectState Latest => States[States.Count - 1];
            public void Publish(EffectState state) => States.Add(state);
        }

        sealed class RecordingFlow : IExperienceFlow
        {
            IFlowStateSink _sink;

            public int SubscriptionDisposeCount { get; private set; }

            public void Publish(ExperienceFlowState state) => _sink?.OnStateChanged(state);

            public IDisposable Observe(IFlowStateSink sink)
            {
                _sink = sink ?? throw new ArgumentNullException(nameof(sink));
                return new CallbackDisposable(() =>
                {
                    _sink = null;
                    SubscriptionDisposeCount++;
                });
            }

            public FlowPrepareResult Prepare(SessionToken candidate, SceneId sceneId)
                => throw new NotSupportedException();
            public FlowResult EnterFeature(SessionToken session, FeaturePageId page)
                => throw new NotSupportedException();
            public FlowResult BackToMain(SessionToken session) => throw new NotSupportedException();
            public FlowResult Close(SessionToken session) => throw new NotSupportedException();
        }

        sealed class SingleDefinitionSource : IEffectDefinitionSource
        {
            readonly EffectDefinition _definition;

            public SingleDefinitionSource(EffectDefinition definition) => _definition = definition;

            public bool TryGet(SceneId sceneId, out EffectDefinition definition)
            {
                definition = _definition;
                return true;
            }
        }

        sealed class RecordingController : IEffectController
        {
            readonly Queue<EffectResult> _openResults;

            public RecordingController(params EffectResult[] openResults)
                => _openResults = new Queue<EffectResult>(openResults);

            public List<SessionToken> OpenSessions { get; } = new List<SessionToken>();
            public List<SessionToken> ClosedSessions { get; } = new List<SessionToken>();
            public List<EffectIntent> Intents { get; } = new List<EffectIntent>();

            public EffectResult Open(SessionToken session, EffectDefinition definition)
            {
                OpenSessions.Add(session);
                return _openResults.Dequeue();
            }

            public EffectResult Dispatch(SessionToken session, EffectIntent intent)
            {
                Intents.Add(intent);
                return EffectResult.Success();
            }

            public EffectResult Close(SessionToken session)
            {
                ClosedSessions.Add(session);
                return EffectResult.Success();
            }

            public IDisposable Observe(IEffectStateSink sink) => new CallbackDisposable(null);
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
