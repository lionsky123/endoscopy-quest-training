using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.FrontendShell
{
    public sealed class StartupRecallPresenterLifecycleTests
    {
        [Test]
        public void UnfinishedPictureLessonOnlyOffersRestartAndConsumesItsCloseDecision()
        {
            using var fixture = new Fixture();
            int quizzes = 0, skips = 0;
            fixture.Presenter.ObservationCompletionRequested += () => quizzes++;
            fixture.Presenter.SkipQuizAndCollectRequested += () => skips++;
            fixture.Presenter.SetClosedContentRequiresLearning(true);
            var buttons = fixture.Root.GetComponentsInChildren<Button>();
            Assert.That(buttons, Has.Length.EqualTo(1));
            Assert.That(buttons[0].GetComponentInChildren<TMP_Text>().text, Is.EqualTo("重新进入学习"));
            fixture.Presenter.BeginClosedContentQuiz();
            Assert.That(quizzes + skips, Is.Zero);
            fixture.Presenter.CompleteClosedContent();
            fixture.Recall.Publish(Closed(2));
            Assert.That(fixture.Root.activeSelf, Is.False);
            Assert.That(fixture.Presenter.IsCloseDecisionVisible, Is.False);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void VisiblePanelKeepsItsPoseWhenAnActionRefreshesStatus(bool journeyStatus)
        {
            using var fixture = new Fixture();
            var position = fixture.Root.transform.position;
            var rotation = fixture.Root.transform.rotation;
            // Leaning forward while reaching must not push an already visible panel away.
            fixture.Viewer.transform.position += Vector3.forward * .35f;
            fixture.Viewer.transform.rotation = Quaternion.Euler(0, 35, 0);
            if (journeyStatus) fixture.Presenter.ShowJourneyStatus("请继续观察");
            else fixture.Recall.Publish(Closed(2));
            InvokeLifecycle(fixture.Presenter, "Update");
            Assert.That(Vector3.Distance(fixture.Root.transform.position, position), Is.LessThan(.001f),
                "Status feedback after interaction must not relocate the visible panel away from the visitor.");
            Assert.That(Quaternion.Angle(fixture.Root.transform.rotation, rotation), Is.LessThan(.01f));
        }

        [Test]
        public void PrimeDoesNotRequireRecallOrActivateTheSurface()
        {
            using var fixture = new Fixture(false);
            fixture.Presenter.Prime(fixture.Viewer.transform, "请扫描二维码");
            Assert.That(fixture.Root.activeSelf, Is.False);
            Assert.That(fixture.Recall.ObserveCalls, Is.Zero);
            Assert.That(fixture.Registry.Registrations, Is.Zero);
        }

        [Test]
        public void DisableEnableCallbacksRetainStateWithoutDuplicatingSubscriptions()
        {
            using var fixture = new Fixture();
            Assert.That(fixture.Root.activeInHierarchy, Is.True);
            var invalidations = fixture.Registry.Invalidations;
            fixture.Presenter.enabled = false;
            // EditMode does not dispatch MonoBehaviour lifecycle callbacks for this fixture.
            InvokeLifecycle(fixture.Presenter, "OnDisable");
            Assert.That(fixture.Root.activeSelf, Is.False);
            Assert.That(fixture.Presenter.IsCloseDecisionVisible, Is.False);
            Assert.That(fixture.Registry.Invalidations, Is.GreaterThan(invalidations));
            fixture.Recall.Publish(Closed(2));
            Assert.That(fixture.Root.activeSelf, Is.False);
            fixture.Presenter.enabled = true;
            InvokeLifecycle(fixture.Presenter, "OnEnable");
            Assert.That(fixture.Root.activeInHierarchy, Is.True);
            Assert.That(fixture.Presenter.IsCloseDecisionVisible, Is.True);
            Assert.That(fixture.Recall.ObserveCalls, Is.EqualTo(1));
            Assert.That(fixture.Recall.DisposeCalls, Is.Zero);
        }

        [Test]
        public void RecallRefreshInvalidatesTheExistingGazeLease()
        {
            using var fixture = new Fixture();
            var invalidations = fixture.Registry.Invalidations;
            fixture.Recall.Publish(Closed(2));
            Assert.That(fixture.Registry.Registrations, Is.EqualTo(1));
            Assert.That(fixture.Registry.ActiveRegistrations, Is.EqualTo(1));
            Assert.That(fixture.Registry.Invalidations, Is.GreaterThan(invalidations));
        }

        [Test]
        public void UnconfigureReleasesSubscriptionsAndSupportsANewConfiguration()
        {
            using var fixture = new Fixture();
            fixture.Presenter.Unconfigure();
            fixture.Presenter.Unconfigure();
            Assert.That(fixture.Recall.DisposeCalls, Is.EqualTo(1));
            Assert.That(fixture.Registry.ActiveRegistrations, Is.Zero);
            Assert.That(fixture.Presenter.IsCloseDecisionVisible, Is.False);
            fixture.Recall.Publish(Closed(2));
            Assert.That(fixture.Root.activeSelf, Is.False);
            fixture.Configure();
            Assert.That(fixture.Recall.ObserveCalls, Is.EqualTo(2));
            Assert.That(fixture.Root.activeInHierarchy, Is.True);
        }

        [Test]
        public void DestroyCallbackReleasesRecallAndGazeExactlyOnce()
        {
            using var fixture = new Fixture();
            // Exercise the destruction callback explicitly in this EditMode fixture.
            InvokeLifecycle(fixture.Presenter, "OnDestroy");
            UnityEngine.Object.DestroyImmediate(fixture.Presenter);
            Assert.That(fixture.Recall.DisposeCalls, Is.EqualTo(1));
            Assert.That(fixture.Registry.ActiveRegistrations, Is.Zero);
            fixture.Recall.Publish(Closed(2));
            Assert.That(fixture.Root.activeSelf, Is.False);
        }

        [Test]
        public void PermissionRecoveryPreemptsAndRestoresJourneyCopy()
        {
            using var fixture = new Fixture();
            fixture.Presenter.ShowJourneyStatus("温室");
            var retries = 0;
            fixture.Presenter.RetrySpatialPermissionRequested += () => retries++;
            fixture.Presenter.OnSpatialDataPermissionStateChanged(SpatialDataPermissionState.Denied);
            Assert.That(fixture.RecallButton.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("重试授权"));
            Assert.That(fixture.RecallButton.gameObject.activeSelf, Is.True);
            fixture.RecallButton.onClick.Invoke();
            Assert.That(retries, Is.EqualTo(1));
            fixture.Presenter.OnSpatialDataPermissionStateChanged(SpatialDataPermissionState.PermanentlyDenied);
            Assert.That(fixture.Text.text, Does.Contain("系统设置"));
            Assert.That(fixture.RecallButton.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("检查授权"));
            fixture.Presenter.OnSpatialDataPermissionStateChanged(SpatialDataPermissionState.Granted);
            Assert.That(fixture.Text.text, Does.Contain("温室"));
        }

        // Test callback behavior without asking EditMode to simulate a running MonoBehaviour.
        // Engine callback delivery is covered by the Play/Quest acceptance pass.
        static void InvokeLifecycle(StartupRecallPresenter presenter, string callback)
        {
            var method = typeof(StartupRecallPresenter).GetMethod(callback,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(presenter, null);
        }

        static RecallState Closed(long version) => new RecallState(version, isClosed: true, canRecall: true, hasPreviousContent: true);

        sealed class Fixture : IDisposable
        {
            readonly GameObject _instance;
            public readonly GameObject Viewer;
            public readonly StartupRecallPresenter Presenter;
            public readonly GameObject Root;
            public readonly TMP_Text Text;
            public readonly Button RecallButton;
            public readonly RecallStub Recall = new RecallStub();
            public readonly RegistryStub Registry = new RegistryStub();

            public Fixture(bool configure = true)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab");
                _instance = UnityEngine.Object.Instantiate(prefab);
                Viewer = new GameObject("StartupRecallTestViewer", typeof(Camera));
                Viewer.GetComponent<Camera>().enabled = false;
                Presenter = _instance.GetComponentInChildren<StartupRecallPresenter>(true);
                Assert.That(Presenter, Is.Not.Null);
                var serialized = new SerializedObject(Presenter);
                Root = (GameObject)serialized.FindProperty("_startupRoot").objectReferenceValue;
                Text = (TMP_Text)serialized.FindProperty("_startupText").objectReferenceValue;
                RecallButton = (Button)serialized.FindProperty("_recallButton").objectReferenceValue;
                if (configure) Configure();
            }

            public void Configure() => Presenter.Configure(Viewer.transform, Recall, Registry, "请扫描二维码");

            public void Dispose()
            {
                if (Presenter != null) Presenter.Unconfigure();
                UnityEngine.Object.DestroyImmediate(_instance);
                UnityEngine.Object.DestroyImmediate(Viewer);
            }
        }

        sealed class RecallStub : IRecallController
        {
            IRecallStateSink _sink;
            public RecallState CurrentState { get; private set; } = Closed(1);
            public int ObserveCalls { get; private set; }
            public int DisposeCalls { get; private set; }
            public RecallResult Recall() => RecallResult.Success;
            public IDisposable Observe(IRecallStateSink sink)
            {
                Assert.That(_sink, Is.Null);
                _sink = sink;
                ObserveCalls++;
                sink.OnStateChanged(CurrentState);
                return new Callback(() => { _sink = null; DisposeCalls++; });
            }
            public void Publish(RecallState state)
            {
                CurrentState = state;
                _sink?.OnStateChanged(state);
            }
        }

        sealed class RegistryStub : IFrontendGazeSurfaceRegistry
        {
            public System.IDisposable SuspendPanelInput() => new Suspension();
            sealed class Suspension : System.IDisposable { public void Dispose() { } }
            public int Registrations;
            public int ActiveRegistrations;
            public int Invalidations;
            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform surfaceRoot, int priority, string label)
            {
                Registrations++;
                ActiveRegistrations++;
                return new Registration(this);
            }
            sealed class Registration : IFrontendGazeSurfaceRegistration
            {
                RegistryStub _owner;
                public Registration(RegistryStub owner) => _owner = owner;
                public bool IsFocused => false;
                public void Invalidate() { if (_owner != null) _owner.Invalidations++; }
                public void Dispose()
                {
                    if (_owner == null) return;
                    _owner.ActiveRegistrations--;
                    _owner = null;
                }
            }
        }

        sealed class Callback : IDisposable
        {
            Action _action;
            public Callback(Action action) => _action = action;
            public void Dispose() { var action = _action; _action = null; action?.Invoke(); }
        }
    }
}
