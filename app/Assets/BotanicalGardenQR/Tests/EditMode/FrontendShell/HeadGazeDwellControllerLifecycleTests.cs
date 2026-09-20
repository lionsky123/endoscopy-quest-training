using System;
using System.Reflection;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.VisitorCoach.Frontend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.FrontendShell
{
    public sealed class HeadGazeDwellControllerLifecycleTests
    {
        [Test]
        public void ProductionDialogueUsesGazeWithoutMovingOrRepeatingAcrossPages()
        {
            using var fixture = Fixture.Create();
            fixture.Surface.SetActive(false);
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/VisitorCoachTheme.asset");
            var defaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(
                "Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset");
            var instance = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
            var presenter = instance.GetComponentInChildren<VisitorCoachPresenter>(true);
            try
            {
                // Use the production registry: a permissive fake previously hid a missing raycaster.
                presenter.Configure(fixture.Viewer.transform, theme, defaults.SharedFont, fixture.Controller);
                presenter.SetInputMode(VisitorDialogueInputMode.HeadGaze);
                var page = new VisitorDialogueSurfaceState(1, new VisitorDialogueContextId("startup-regression"),
                    VisitorDialogueOwner.Prologue, VisitorDialogueSurfaceMode.Dialogue,
                    "探索教学", "小精灵", "把准星对准按钮，保持到圆环填满。", 0, 2);
                presenter.Present(page);
                Assert.That(presenter.CurrentState, Is.SameAs(page));
                Assert.That(presenter.gameObject.activeInHierarchy, Is.True);
                Assert.That(presenter.BodyCharacterCount, Is.GreaterThan(0));
                var continueButton = (Button)typeof(VisitorCoachPresenter).GetField("_continueButton",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);
                var readyAt = typeof(VisitorCoachPresenter).GetField("_inputReadyAt",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                // EditMode has no wall-clock frame advance; bypass only the initial debounce clock.
                readyAt.SetValue(presenter, -1f);
                presenter.Tick(1f);
                Canvas.ForceUpdateCanvases();
                var position = presenter.transform.position;
                var rotation = presenter.transform.rotation;
                var targetPosition = continueButton.transform.position;
                var confirmations = 0;
                presenter.IntentRequested += _ =>
                {
                    confirmations++;
                    if (confirmations != 1) return;
                    presenter.Present(new VisitorDialogueSurfaceState(2, page.Context,
                        VisitorDialogueOwner.Prologue, VisitorDialogueSurfaceMode.Dialogue,
                        "探索教学", "小精灵", "现在我们跟着路线走。", 1, 2));
                    readyAt.SetValue(presenter, -1f);
                };
                fixture.Viewer.transform.position += Vector3.forward * .1f;
                fixture.Viewer.transform.LookAt(continueButton.transform.position);
                fixture.Controller.TickInput(.3f);
                Assert.That(confirmations, Is.Zero);
                Assert.That(continueButton.transform.position, Is.EqualTo(targetPosition),
                    "Focus feedback must not move the button hit plane.");
                fixture.Controller.TickInput(.4f);
                Assert.That(confirmations, Is.EqualTo(1));
                fixture.Controller.TickInput(2f);
                Assert.That(confirmations, Is.EqualTo(1), "Holding gaze across a page refresh cannot advance again.");
                Assert.That(presenter.transform.position, Is.EqualTo(position));
                Assert.That(Quaternion.Angle(presenter.transform.rotation, rotation), Is.LessThan(.001f));
                foreach (var poke in instance.GetComponentsInChildren<VisitorDialoguePointableTarget>(true))
                    foreach (var collider in poke.GetComponents<Collider>())
                        Assert.That(collider.enabled, Is.False, "A gaze panel must not also submit hand pokes.");
                fixture.Viewer.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                fixture.Controller.TickInput(.25f);
                fixture.Viewer.transform.LookAt(continueButton.transform.position);
                fixture.Controller.TickInput(.7f);
                Assert.That(confirmations, Is.EqualTo(2));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OverlappingHandInteractionsBlockPanelsUntilAllReleaseAndGazeRearms(bool priorDwell)
        {
            using var fixture = Fixture.Create();
            Canvas.ForceUpdateCanvases();
            fixture.Viewer.transform.LookAt(fixture.Button.transform.position);
            var recorder = fixture.Button.gameObject.AddComponent<PointerEventRecorder>();
            if (priorDwell) fixture.Controller.TickInput(0.3f);
            var first = fixture.Controller.SuspendPanelInput();
            var second = fixture.Controller.SuspendPanelInput();
            fixture.Controller.TickInput(1f);
            first.Dispose();
            first.Dispose();
            fixture.Controller.TickInput(1f);
            Assert.That(recorder.Events, Does.Not.Contain("Click"));
            second.Dispose();
            fixture.Controller.TickInput(1f);
            Assert.That(recorder.Events, Does.Not.Contain("Click"), "Hand release cannot commit a background selection.");
            fixture.Viewer.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            fixture.Controller.TickInput(0.25f);
            fixture.Viewer.transform.LookAt(fixture.Button.transform.position);
            fixture.Controller.TickInput(0.7f);
            Assert.That(recorder.Events.FindAll(value => value == "Click").Count, Is.EqualTo(1));
        }

        [Test]
        public void OldHandLeaseCannotReleaseANewConfigurationSuspension()
        {
            using var fixture = Fixture.Create();
            var old = fixture.Controller.SuspendPanelInput();
            fixture.Controller.Unconfigure();
            fixture.ConfigureInput();
            using var current = fixture.Controller.SuspendPanelInput();
            old.Dispose();
            var recorder = fixture.Button.gameObject.AddComponent<PointerEventRecorder>();
            Canvas.ForceUpdateCanvases();
            fixture.Viewer.transform.LookAt(fixture.Button.transform.position);
            fixture.Controller.TickInput(1f);
            Assert.That(recorder.Events, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancelledDwellRequiresNeutralGazeBeforeSameOrReplacementTarget(bool replacement)
        {
            var dwell = new EntryGazeDwellState();
            var first = new object();
            var next = replacement ? new object() : first;
            Assert.That(dwell.UpdateTarget(first, true, 0.3f, 0.65f, 0.2f).Activated, Is.False);
            dwell.ClearTarget();
            var cancelled = dwell.UpdateTarget(next, true, 1f, 0.65f, 0.2f);
            Assert.That(cancelled.Activated, Is.False, "Cancellation must not turn a continuously aimed target into a new click.");
            Assert.That(cancelled.Progress, Is.Zero);
            dwell.ClearTarget();
            Assert.That(dwell.UpdateTarget(next, true, 1f, 0.65f, 0.2f).Activated, Is.False,
                "Repeated surface updates must preserve the requirement to leave the target.");
            dwell.UpdateTarget(null, false, 0.21f, 0.65f, 0.2f);
            Assert.That(dwell.UpdateTarget(next, true, 0.3f, 0.65f, 0.2f).Activated, Is.False);
            Assert.That(dwell.UpdateTarget(next, true, 0.4f, 0.65f, 0.2f).Activated, Is.True);
        }

        [Test]
        public void TemporarilyDisabledTargetCannotRearmCancelledDwell()
        {
            var dwell = new EntryGazeDwellState();
            var target = new object();
            dwell.UpdateTarget(target, true, 0.3f, 0.65f, 0.2f);
            dwell.ClearTarget();
            dwell.UpdateTarget(target, false, 1f, 0.65f, 0.2f);
            Assert.That(dwell.UpdateTarget(target, true, 1f, 0.65f, 0.2f).Activated, Is.False);
            dwell.UpdateTarget(null, false, 0.21f, 0.65f, 0.2f);
            Assert.That(dwell.UpdateTarget(target, true, 0.65f, 0.65f, 0.2f).Activated, Is.True);
        }

        [Test]
        public void InitialRegistrationAllowsFirstDwellAndBriefLossRetainsProgress()
        {
            var dwell = new EntryGazeDwellState();
            var target = new object();
            dwell.ClearTarget();
            dwell.ClearTarget();
            var started = dwell.UpdateTarget(target, true, 0.3f, 0.65f, 0.2f);
            Assert.That(started.Progress, Is.GreaterThan(0f));
            var lost = dwell.UpdateTarget(null, false, 0.1f, 0.65f, 0.2f);
            Assert.That(lost.Progress, Is.EqualTo(started.Progress));
            Assert.That(dwell.UpdateTarget(target, true, 0.4f, 0.65f, 0.2f).Activated, Is.True);
            Assert.That(dwell.UpdateTarget(target, true, 2f, 0.65f, 0.2f).Activated, Is.False);
            dwell.ClearTarget();
            Assert.That(dwell.UpdateTarget(target, true, 1f, 0.65f, 0.2f).Activated, Is.False,
                "Surface refresh after a successful click must not unlock a second click.");
        }

        [Test]
        public void RegisteredSurfaceRefreshCancelsDwellUntilVisitorLooksAwayAndBack()
        {
            using var fixture = Fixture.Create();
            Canvas.ForceUpdateCanvases();
            fixture.Viewer.transform.LookAt(fixture.Button.transform.position);
            var recorder = fixture.Button.gameObject.AddComponent<PointerEventRecorder>();
            fixture.Controller.TickInput(0.3f);
            Assert.That(recorder.Events, Does.Contain("Enter"), "The fixture must hit the production button through gaze geometry.");
            fixture.Registration.Invalidate();
            fixture.Controller.TickInput(1f);
            Assert.That(recorder.Events, Does.Not.Contain("Click"), "The retained gaze cannot confirm a refreshed decision.");
            fixture.Viewer.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            fixture.Controller.TickInput(0.25f);
            fixture.Viewer.transform.LookAt(fixture.Button.transform.position);
            fixture.Controller.TickInput(0.3f);
            Assert.That(recorder.Events, Does.Not.Contain("Click"));
            fixture.Controller.TickInput(0.4f);
            Assert.That(recorder.Events.FindAll(item => item == "Click").Count, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisabledButtonDoesNotCountAsLookingAway(bool disableComponent)
        {
            using var fixture = Fixture.Create();
            var registration = fixture.Registration;
            Canvas.ForceUpdateCanvases();
            fixture.Viewer.transform.LookAt(fixture.Button.transform.position);
            var recorder = fixture.Button.gameObject.AddComponent<PointerEventRecorder>();
            fixture.Controller.TickInput(0.3f);
            Assert.That(recorder.Events, Does.Contain("Enter"));
            Assert.That(registration.IsFocused, Is.True);
            if (disableComponent) fixture.Button.enabled = false;
            else fixture.Button.interactable = false;
            fixture.Controller.TickInput(1f);
            Assert.That(recorder.Events, Does.Not.Contain("Click"));
            Assert.That(registration.IsFocused, Is.False,
                "Disabled targets block rearming without reporting an actionable surface focus.");
            if (disableComponent) fixture.Button.enabled = true;
            else fixture.Button.interactable = true;
            fixture.Controller.TickInput(1f);
            Assert.That(recorder.Events, Does.Not.Contain("Click"),
                "A visible but disabled button must not masquerade as neutral gaze.");
            fixture.Viewer.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            fixture.Controller.TickInput(0.25f);
            fixture.Viewer.transform.LookAt(fixture.Button.transform.position);
            fixture.Controller.TickInput(0.7f);
            Assert.That(recorder.Events.FindAll(item => item == "Click").Count, Is.EqualTo(1));
        }

        [Test]
        public void CancelledDwellRequiresContinuousNeutralInterval()
        {
            var dwell = new EntryGazeDwellState();
            var target = new object();
            dwell.UpdateTarget(target, true, 0.3f, 0.65f, 0.2f);
            dwell.ClearTarget();
            dwell.UpdateTarget(null, false, 0.1f, 0.65f, 0.2f);
            dwell.UpdateTarget(target, true, 0.1f, 0.65f, 0.2f);
            dwell.UpdateTarget(null, false, 0.11f, 0.65f, 0.2f);
            Assert.That(dwell.UpdateTarget(target, true, 1f, 0.65f, 0.2f).Activated, Is.False,
                "Separate tracking gaps must not accumulate into a deliberate gaze exit.");
            dwell.UpdateTarget(null, false, 0.21f, 0.65f, 0.2f);
            Assert.That(dwell.UpdateTarget(target, true, 0.65f, 0.65f, 0.2f).Activated, Is.True);
        }

        [Test]
        public void NaturalTargetChangeAndExpiredFocusLossStartFreshDwell()
        {
            var dwell = new EntryGazeDwellState();
            var first = new object();
            var next = new object();
            dwell.UpdateTarget(first, true, 0.3f, 0.65f, 0.2f);
            Assert.That(dwell.UpdateTarget(next, true, 0.4f, 0.65f, 0.2f).Activated, Is.False);
            Assert.That(dwell.UpdateTarget(next, true, 0.3f, 0.65f, 0.2f).Activated, Is.True);
            dwell.UpdateTarget(null, false, 0.21f, 0.65f, 0.2f);
            Assert.That(dwell.UpdateTarget(next, true, 0.3f, 0.65f, 0.2f).Activated, Is.False);
            Assert.That(dwell.UpdateTarget(next, true, 0.4f, 0.65f, 0.2f).Activated, Is.True);
        }

        [Test]
        public void ReconfigurationStartsFreshDwellWithoutPreviousCancellation()
        {
            using var fixture = Fixture.Create();
            Canvas.ForceUpdateCanvases();
            fixture.Viewer.transform.LookAt(fixture.Button.transform.position);
            var recorder = fixture.Button.gameObject.AddComponent<PointerEventRecorder>();
            fixture.Controller.TickInput(0.3f);
            Assert.That(recorder.Events, Does.Contain("Enter"));
            fixture.Controller.Unconfigure();
            fixture.ConfigureInput();
            Canvas.ForceUpdateCanvases();
            fixture.Viewer.transform.LookAt(fixture.Button.transform.position);
            fixture.Controller.TickInput(0.3f);
            Assert.That(recorder.Events, Does.Not.Contain("Click"));
            fixture.Controller.TickInput(0.4f);
            Assert.That(recorder.Events.FindAll(item => item == "Click").Count, Is.EqualTo(1));
        }

        [Test]
        public void DwellActivation_EmitsMatchedPointerLifecycleAndDisableCancelsFocus()
        {
            using var fixture = Fixture.Create();
            var recorder = fixture.Button.gameObject.AddComponent<PointerEventRecorder>();
            fixture.Controller.SetPointerFocus(fixture.Button);
            Assert.That(fixture.Controller.ActivatePointer(fixture.Button), Is.True);
            fixture.Controller.enabled = false;
            // This minimal EditMode fixture does not run MonoBehaviour lifecycle callbacks.
            fixture.Controller.TickInput(0f);

            Assert.That(recorder.Events, Is.EqualTo(new[] { "Enter", "Down", "Up", "Click", "Exit" }));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void UnavailableSurfaceCannotRearmGazeAtItsLastTargetPosition(int unavailableKind)
        {
            using var fixture = Fixture.Create();
            var recorder = fixture.Button.gameObject.AddComponent<PointerEventRecorder>();
            fixture.Controller.TickInput(0.3f);
            Assert.That(recorder.Events, Does.Contain("Enter"));
            if (unavailableKind == 0) fixture.Surface.SetActive(false);
            else if (unavailableKind == 1) fixture.Surface.GetComponent<CanvasGroup>().interactable = false;
            else if (unavailableKind == 2) fixture.Registration.Dispose();
            else UnityEngine.Object.DestroyImmediate(fixture.Button.gameObject);
            fixture.Controller.TickInput(1f);
            Assert.That(recorder.Events, Does.Not.Contain("Click"));
            if (unavailableKind == 0) fixture.Surface.SetActive(true);
            else if (unavailableKind == 1) fixture.Surface.GetComponent<CanvasGroup>().interactable = true;
            else
            {
                if (unavailableKind == 3)
                {
                    fixture.CreateButton();
                    recorder = fixture.Button.gameObject.AddComponent<PointerEventRecorder>();
                }
                fixture.RegisterSurface();
            }
            Canvas.ForceUpdateCanvases();
            fixture.Controller.TickInput(1f);
            Assert.That(recorder.Events, Does.Not.Contain("Click"));
            fixture.Viewer.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            fixture.Controller.TickInput(0.25f);
            fixture.Viewer.transform.rotation = Quaternion.identity;
            fixture.Controller.TickInput(0.7f);
            Assert.That(recorder.Events.FindAll(item => item == "Click").Count, Is.EqualTo(1));
        }

        [Test]
        public void UnconfigureAndStaleRegistrationReleaseInputIdempotently()
        {
            using var fixture = Fixture.Create();
            var recorder = fixture.Button.gameObject.AddComponent<PointerEventRecorder>();
            fixture.Controller.TickInput(0.3f);
            Assert.That(fixture.Registration.IsFocused, Is.True);
            var oldRegistration = fixture.Registration;
            fixture.Controller.Unconfigure();
            fixture.Controller.Unconfigure();
            Assert.That(oldRegistration.IsFocused, Is.False);
            Assert.That(recorder.Events, Is.EqualTo(new[] { "Enter", "Exit" }));
            fixture.ConfigureInput();
            fixture.Controller.TickInput(0.3f);
            oldRegistration.Invalidate();
            oldRegistration.Dispose();
            fixture.Controller.TickInput(0.4f);
            Assert.That(recorder.Events.FindAll(item => item == "Click").Count, Is.EqualTo(1),
                "A stale registration cannot cancel the next input configuration.");
        }

        sealed class Fixture : IDisposable
        {
            public GameObject Surface { get; private set; }
            public GameObject Viewer { get; private set; }
            public GameObject EventSystemObject { get; private set; }
            public HeadGazeDwellController Controller { get; private set; }
            public Button Button { get; private set; }
            public IFrontendGazeSurfaceRegistration Registration { get; private set; }

            public static Fixture Create()
            {
                var fixture = new Fixture();
                fixture.Viewer = new GameObject("GazeTestViewer", typeof(Camera));
                fixture.Viewer.GetComponent<Camera>().enabled = false;
                fixture.EventSystemObject = new GameObject("GazeTestEvents", typeof(EventSystem));
                fixture.Surface = new GameObject("GazeTestSurface", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup));
                fixture.Surface.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                fixture.Surface.transform.position = new Vector3(0f, 0f, 1f);
                fixture.Surface.transform.localScale = Vector3.one * 0.001f;
                ((RectTransform)fixture.Surface.transform).sizeDelta = new Vector2(500f, 500f);
                fixture.Controller = fixture.EventSystemObject.AddComponent<HeadGazeDwellController>();
                fixture.CreateButton();
                fixture.ConfigureInput();
                Canvas.ForceUpdateCanvases();
                return fixture;
            }

            public void CreateButton()
            {
                var target = new GameObject("GazeTestButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ProgressRecorder));
                target.transform.SetParent(Surface.transform, false);
                ((RectTransform)target.transform).sizeDelta = new Vector2(200f, 100f);
                Button = target.GetComponent<Button>();
            }

            public void ConfigureInput()
            {
                Controller.Configure(Viewer.GetComponent<Camera>(), EventSystemObject.GetComponent<EventSystem>());
                RegisterSurface();
            }

            public void RegisterSurface()
            {
                Registration?.Dispose();
                Registration = Controller.RegisterGazeSurface(Surface.transform, 100, "GazeTestSurface");
            }

            public void Dispose()
            {
                Registration?.Dispose();
                if (Controller != null) Controller.Unconfigure();
                UnityEngine.Object.DestroyImmediate(Surface);
                UnityEngine.Object.DestroyImmediate(EventSystemObject);
                UnityEngine.Object.DestroyImmediate(Viewer);
            }
        }

        sealed class ProgressRecorder : MonoBehaviour, IFrontendGazeProgressPresenter
        {
            public float Progress { get; private set; }
            public void PresentGazeProgress(float progress) => Progress = progress;
        }

        sealed class PointerEventRecorder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
            IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
        {
            public readonly System.Collections.Generic.List<string> Events = new System.Collections.Generic.List<string>();
            public void OnPointerEnter(PointerEventData eventData) => Events.Add("Enter");
            public void OnPointerExit(PointerEventData eventData) => Events.Add("Exit");
            public void OnPointerDown(PointerEventData eventData) => Events.Add("Down");
            public void OnPointerUp(PointerEventData eventData) => Events.Add("Up");
            public void OnPointerClick(PointerEventData eventData) => Events.Add("Click");
        }
    }
}
