using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.VisitorCoach.Frontend;
using NUnit.Framework;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode
{
    // Moves an actual SDK PokeInteractor through the real UI surface; no button.onClick injection.
    internal sealed class ClinicalHandFixture : IDisposable
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly GameObject _root, _finger;
        readonly Transform _viewer;
        readonly HeadGazeDwellController _gaze;
        readonly PokeInteractor _poke;
        readonly HashSet<ClinicalNearTouch> _initialized = new HashSet<ClinicalNearTouch>();
        readonly HashSet<VisitorDialoguePointableTarget> _dialogueTargets = new HashSet<VisitorDialoguePointableTarget>();
        float _time = 1;
        bool _disposed;
        static void Lifecycle(MonoBehaviour component, string method) => component.GetType().GetMethod(method, Flags)?.Invoke(component, null);
        public ClinicalHandFixture(GameObject root, Transform viewer, HeadGazeDwellController gaze)
        {
            _root = root; _viewer = viewer; _gaze = gaze; gaze.SetHandOnly(true);
            _finger = new GameObject("Tracked fingertip fixture");
            _poke = _finger.AddComponent<PokeInteractor>(); Lifecycle(_poke, "Awake");
            _poke.InjectAllPokeInteractor(_finger.transform, .008f); _poke.IsRootDriver = false;
            _poke.SetTimeProvider(() => _time); Lifecycle(_poke, "Start");
        }
        void Drive(Vector3 position) { _finger.transform.position = position; _time += .1f; _poke.Drive(); }
        public void Touch(Button button)
        {
            if (button.GetComponent<VisitorDialoguePointableTarget>()) { TouchDialogue(button); return; }
            Assert.That(button.isActiveAndEnabled && button.IsInteractable(), Is.True, button.name);
            foreach (var touch in _root.GetComponentsInChildren<ClinicalNearTouch>(true))
            {
                if (_initialized.Add(touch))
                    foreach (var component in touch.GetComponents<MonoBehaviour>())
                        if (component.GetType().Namespace?.StartsWith("Oculus.Interaction") == true) { Lifecycle(component, "Awake"); Lifecycle(component, "Start"); }
                var surface = touch.GetComponent<PokeInteractable>();
                Lifecycle(touch, "LateUpdate");
                if (touch.Button.isActiveAndEnabled && touch.Button.IsInteractable()) surface.Enable(); else surface.Disable();
            }
            var target = button.GetComponent<ClinicalNearTouch>(); Assert.That(target, Is.Not.Null, button.name);
            target.SetTimeProvider(() => _time);
            Lifecycle(target, "LateUpdate");
            typeof(ClinicalNearTouch).GetField("readyAt", Flags).SetValue(target, float.NegativeInfinity);
            typeof(ClinicalNearTouch).GetField("lastCommit", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, float.NegativeInfinity);
            int commits = 0; UnityEngine.Events.UnityAction count = () => commits++;
            var buttonName=button.name;
            var trace = new List<string>();
            void Track(PointerEvent e) => trace.Add(e.Type.ToString());
            target.GetComponent<PokeInteractable>().WhenPointerEventRaised += Track;
            button.onClick.AddListener(count);
            try
            {
                var touchSurfaces = _root.GetComponentsInChildren<ClinicalNearTouch>(true)
                    .Select(touch => touch.GetComponent<PokeInteractable>()).Where(surface => surface).ToArray();
                var readySurfaces = touchSurfaces.Where(surface =>
                {
                    var candidate = surface.GetComponent<Button>();
                    return surface.isActiveAndEnabled && candidate && candidate.isActiveAndEnabled && candidate.IsInteractable();
                }).ToArray();
                foreach (var surface in touchSurfaces) surface.Disable();
                Drive(_viewer.position - _viewer.forward);
                _viewer.LookAt(button.transform.position); Canvas.ForceUpdateCanvases();
                var approach = button.transform.position - button.transform.forward * .06f;
                // Treat this as a newly tracked fingertip sample. Do not let the synthetic
                // teleport from the viewer spawn sweep across unrelated gallery surfaces.
                Drive(approach);
                foreach (var surface in readySurfaces) surface.Enable();
                Drive(approach);
                _gaze.TickInput(2); Assert.That(commits, Is.Zero, "Gaze alone cannot submit in hand-only VR.");
                Assert.That(_poke.State, Is.EqualTo(InteractorState.Hover), button.name);
                var selectedAtApproach = _poke.Interactable;
                Assert.That(selectedAtApproach, Is.SameAs(target.GetComponent<PokeInteractable>()),
                    button.name + "; finger=" + _finger.transform.position + "; target=" + button.transform.position +
                    "; selected=" + (selectedAtApproach ? selectedAtApproach.name : "<none>") +
                    "; selectedPosition=" + (selectedAtApproach ? selectedAtApproach.transform.position.ToString() : "<none>"));
                for (int step = -10; step <= 2 && commits == 0; step++)
                {
                    Drive(button.transform.position + button.transform.forward * (step * .005f));
                    Lifecycle(target, "LateUpdate");
                }
                for (int hold = 0; hold < 4 && commits == 0; hold++)
                {
                    Drive(button.transform.position + button.transform.forward * .01f);
                    Lifecycle(target, "LateUpdate");
                }
                _gaze.TickInput(2); Assert.That(commits, Is.EqualTo(1), buttonName + " must commit exactly once through actual poke. Events=" + string.Join(",", trace) + " state=" + _poke.State);
                Drive(_viewer.position - _viewer.forward);
            }
            finally
            {
                if(target) target.SetTimeProvider(null);
                if(button) button.onClick.RemoveListener(count);
                if(target) target.GetComponent<PokeInteractable>().WhenPointerEventRaised -= Track;
            }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _poke.Disable();
            foreach (var touch in _initialized) if (touch) touch.GetComponent<PokeInteractable>().Disable();
            foreach (var touch in _dialogueTargets) if (touch) touch.GetComponent<PokeInteractable>().Disable();
            UnityEngine.Object.DestroyImmediate(_finger);
        }
        void TouchDialogue(Button button)
        {
            foreach (var touch in _root.GetComponentsInChildren<ClinicalNearTouch>(true))
            {
                // EditMode does not run the normal LateUpdate which disables hidden surfaces.
                Lifecycle(touch, "LateUpdate");
                var surface = touch.GetComponent<PokeInteractable>();
                if (touch.Button.isActiveAndEnabled && touch.Button.IsInteractable()) surface.Enable(); else surface.Disable();
            }
            var presenter = button.GetComponentInParent<VisitorCoachPresenter>();
            typeof(VisitorCoachPresenter).GetField("_inputReadyAt", Flags).SetValue(presenter, float.NegativeInfinity);
            typeof(VisitorCoachPresenter).GetField("_lastAcceptedFrame", Flags).SetValue(presenter, -1);
            foreach (var target in _root.GetComponentsInChildren<VisitorDialoguePointableTarget>(true))
            {
                if (_dialogueTargets.Add(target))
                {
                    foreach (var component in target.GetComponents<MonoBehaviour>())
                        if (component.GetType().Namespace?.StartsWith("Oculus.Interaction") == true)
                        { Lifecycle(component, "Awake"); Lifecycle(component, "Start"); }
                    Lifecycle(target, "Awake"); Lifecycle(target, "OnEnable");
                }
                bool enabled = target.GetComponent<Button>().isActiveAndEnabled && target.GetComponent<Button>().IsInteractable();
                if (enabled) Assert.That((bool)typeof(VisitorDialoguePointableTarget).GetField("_armed", Flags).GetValue(target),
                    Is.True, "First activation must preserve the presenter's armed state; the fixture must not repair it.");
                if (enabled) target.GetComponent<PokeInteractable>().Enable(); else target.GetComponent<PokeInteractable>().Disable();
            }
            var pointable = button.GetComponent<VisitorDialoguePointableTarget>();
            int commits = 0; void Count() => commits++;
            pointable.Selected += Count;
            try
            {
                Drive(_viewer.position - _viewer.forward);
                _viewer.LookAt(button.transform.position); Canvas.ForceUpdateCanvases();
                _gaze.TickInput(2); Assert.That(commits, Is.Zero);
                Drive(button.transform.position - button.transform.forward * .06f);
                Assert.That(_poke.Interactable, Is.SameAs(button.GetComponent<PokeInteractable>()));
                for (int step = -10; step <= 2 && commits == 0; step++)
                    Drive(button.transform.position + button.transform.forward * (step * .005f));
                Assert.That(commits, Is.EqualTo(1), "The authored dialogue must accept a real hand poke.");
                Drive(_viewer.position - _viewer.forward);
            }
            finally { pointable.Selected -= Count; }
        }
    }
}
