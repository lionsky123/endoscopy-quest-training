using System;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.FrontendShell
{
    public sealed class FeaturePageActionFocusShellTests
    {
        const string VisitorRuntimePath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab";

        [Test]
        public void AuthoredButtonsInvokeActionThenHideAndRestorePanel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorRuntimePath);
            var instance = UnityEngine.Object.Instantiate(prefab);
            var shell = instance.GetComponentInChildren<GlobalFrontendShell>(true);
            var flow = new TestFlow();
            var source = new TestFeatureActionSource();
            IDisposable binding = null;
            try
            {
                shell.Configure(flow, Presentation(), string.Empty);
                binding = shell.BindFeaturePageAction(FeaturePageId.Model, source);
                var session = SessionToken.CreateNew();
                flow.Publish(State(session));

                var slots = new SerializedObject(shell).FindProperty("_slots");
                var videoModeTitle = slots.FindPropertyRelative("_videoModeTitle")
                    .objectReferenceValue as Text;
                var modelModeTitle = slots.FindPropertyRelative("_modelModeTitle")
                    .objectReferenceValue as Text;
                Assert.That(videoModeTitle, Is.Not.Null);
                Assert.That(modelModeTitle, Is.Not.Null);
                Assert.That(videoModeTitle.text, Is.EqualTo("title"));
                Assert.That(modelModeTitle.text, Is.EqualTo("title"));

                var launch = instance.GetComponentsInChildren<ShellFlowActionTarget>(true)
                    .Single(target => target.Action == ShellFlowAction.FeaturePageAction);
                var exit = instance.GetComponentsInChildren<ShellFlowActionTarget>(true)
                    .Single(target => target.Action == ShellFlowAction.FeaturePageActionExitFocus);

                launch.GetComponent<Button>().onClick.Invoke();

                Assert.That(source.InvokeCount, Is.EqualTo(1));
                Assert.That(shell.FrontendRoot.gameObject.activeSelf, Is.False);
                Assert.That(exit.gameObject.activeSelf, Is.True);

                exit.GetComponent<Button>().onClick.Invoke();

                Assert.That(source.ExitFocusCount, Is.EqualTo(1));
                Assert.That(shell.FrontendRoot.gameObject.activeSelf, Is.True);
                Assert.That(exit.gameObject.activeSelf, Is.False);
            }
            finally
            {
                binding?.Dispose();
                shell?.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void AuthoredRealityActionIsReachableThroughTheProductionHeadGazePointerPath()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorRuntimePath);
            var instance = UnityEngine.Object.Instantiate(prefab);
            var viewer = new GameObject("FeatureActionHeadGazeViewer");
            var camera = viewer.AddComponent<Camera>();
            camera.enabled = false;
            var eventSystemObject = new GameObject("FeatureActionHeadGazeEventSystem");
            var eventSystem = eventSystemObject.AddComponent<EventSystem>();
            var shell = instance.GetComponentInChildren<GlobalFrontendShell>(true);
            var headGaze = instance.GetComponentInChildren<HeadGazeDwellController>(true);
            var reticle = instance.GetComponentInChildren<GazeReticlePresenter>(true);
            var flow = new TestFlow();
            var source = new TestFeatureActionSource();
            IDisposable binding = null;
            try
            {
                shell.Configure(flow, Presentation(), string.Empty);
                binding = shell.BindFeaturePageAction(FeaturePageId.Model, source);
                headGaze.Configure(
                    viewer.GetComponent<Camera>(), eventSystem, reticle);
                headGaze.RegisterGazeSurface(shell.FrontendRoot, 100, "FeatureActionTest");
                var session = SessionToken.CreateNew();
                flow.Publish(State(session));
                SetProductionModelSurfaceVisible(instance, true);

                var launch = instance.GetComponentsInChildren<ShellFlowActionTarget>(true)
                    .Single(target => target.Action == ShellFlowAction.FeaturePageAction);
                var button = launch.GetComponent<Button>();
                var hitRect = button.transform as RectTransform;
                Assert.That(button.gameObject.activeInHierarchy, Is.True);
                Assert.That(button.IsInteractable(), Is.True);
                Assert.That(hitRect, Is.Not.Null);
                Assert.That(hitRect.rect.width, Is.GreaterThanOrEqualTo(160f));
                Assert.That(hitRect.rect.height, Is.GreaterThanOrEqualTo(48f));

                viewer.transform.position = hitRect.position - (hitRect.forward.normalized * 1.2f);
                viewer.transform.rotation = Quaternion.LookRotation(
                    hitRect.position - viewer.transform.position,
                    hitRect.up);
                Canvas.ForceUpdateCanvases();

                var focused = InvokeFindFocusedButton(headGaze);
                Assert.That(focused, Is.SameAs(button),
                    "The visitor's central head-gaze ray must resolve the authored reality action, not an overlapping control.");
                headGaze.SetPointerFocus(focused);
                Assert.That(headGaze.ActivatePointer(focused), Is.True);
                Assert.That(source.InvokeCount, Is.EqualTo(1),
                    "The production pointer lifecycle must reach the semantic feature action source.");
            }
            finally
            {
                headGaze?.Unconfigure();
                binding?.Dispose();
                shell?.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
                UnityEngine.Object.DestroyImmediate(eventSystemObject);
            }
        }

        static Button InvokeFindFocusedButton(HeadGazeDwellController controller)
        {
            var method = typeof(HeadGazeDwellController).GetMethod(
                "FindFocusedButton",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(controller, null) as Button;
        }

        static void SetProductionModelSurfaceVisible(GameObject root, bool visible)
        {
            var frontend = root.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(component => component != null &&
                                     component.GetType().FullName ==
                                     "BotanicalGardenQR.Model.Frontend.ModelFrontend");
            var method = frontend.GetType().GetMethod(
                "SetVisible",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            method.Invoke(frontend, new object[] { visible });
        }

        static PresentationSpec Presentation()
        {
            var text = new TextStyleSpec(28f, Color.white, Vector2.zero, 900f);
            return new PresentationSpec(
                text,
                text,
                new LayoutSpec(new Vector2(1080f, 608f), Vector2.zero, 12f),
                new AnimationSpec(0f));
        }

        static ExperienceFlowState State(SessionToken session)
            => new ExperienceFlowState(
                session,
                1,
                new SceneId("test"),
                "title",
                "subtitle",
                "summary",
                FlowPage.ForFeature(FeaturePageId.Model),
                new[] { FeaturePageId.Model });

        sealed class TestFeatureActionSource : IFeaturePageActionSource
        {
            IFeaturePageActionStateSink _sink;

            public int InvokeCount { get; private set; }
            public int ExitFocusCount { get; private set; }

            public IDisposable Observe(IFeaturePageActionStateSink sink)
            {
                _sink = sink;
                Publish(false);
                return new CallbackDisposable(() => _sink = null);
            }

            public void Invoke(SessionToken session)
            {
                InvokeCount++;
                Publish(true);
            }

            public void ExitFocus(SessionToken session)
            {
                ExitFocusCount++;
                Publish(false);
            }

            void Publish(bool focusActive)
                => _sink?.OnFeaturePageActionStateChanged(
                    new FeaturePageActionState(
                        true,
                        !focusActive,
                        "现实演示",
                        string.Empty,
                        focusActive,
                        focusActive ? "返回面板" : string.Empty));
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

        sealed class TestRecallController : IRecallController
        {
            public RecallResult Recall() => RecallResult.Success;

            public IDisposable Observe(IRecallStateSink sink)
            {
                if (sink == null) throw new ArgumentNullException(nameof(sink));
                sink.OnStateChanged(new RecallState(
                    1,
                    isClosed: false,
                    canRecall: false,
                    hasPreviousContent: true));
                return new CallbackDisposable(() => { });
            }
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
