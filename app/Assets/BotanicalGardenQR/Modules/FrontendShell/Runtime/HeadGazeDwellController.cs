using System;
using System.Collections.Generic;
using BotanicalGardenQR.FrontendShell.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    [DisallowMultipleComponent]
    public sealed class HeadGazeDwellController : MonoBehaviour, IFrontendGazeSurfaceRegistry
    {
        [SerializeField, Min(0.1f)] float _dwellSeconds = 0.65f;
        [SerializeField, Min(0f)] float _focusGraceSeconds = 0.2f;
        readonly EntryGazeDwellState _dwellState = new EntryGazeDwellState();
        readonly List<RaycastResult> _raycastResults = new List<RaycastResult>(32);
        readonly Dictionary<long, GazeCanvasBinding> _gazeSurfaces =
            new Dictionary<long, GazeCanvasBinding>();
        Camera _camera;
        EventSystem _eventSystem;
        PointerEventData _gazePointer;
        GazeReticlePresenter _reticlePresenter;
        Button _lastProgressButton;
        Button _pointerFocusedButton;
        bool _hasGazeSurfaceHit;
        float _gazeSurfaceDistance;
        long _nextSurfaceId;
        long _focusedSurfaceId;
        object _lastTargetIdentity;
        Matrix4x4 _lastTargetWorldToLocal;
        Rect _lastTargetRect;
        Vector3 _lastTargetPosition;
        Vector3 _lastTargetNormal;
        readonly HashSet<long> _inputSuspensions = new HashSet<long>();
        long _nextSuspensionId;
        public bool HandOnly { get; private set; }
        public void SetHandOnly(bool handOnly)
        {
            HandOnly = handOnly;
            ResetInteractionState();
            if (!handOnly) return;
            _reticlePresenter?.SetPresentationEnabled(false);
            foreach (var binding in _gazeSurfaces.Values) ClinicalNearTouch.Bind(binding.Canvas.transform);
        }

        public void Configure(Camera camera, EventSystem eventSystem, GazeReticlePresenter reticlePresenter = null)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (eventSystem == null) throw new ArgumentNullException(nameof(eventSystem));
            if (_gazePointer != null) throw new InvalidOperationException("Head gaze input is already configured.");
            if (!IsPositiveFinite(_dwellSeconds) || !IsNonNegativeFinite(_focusGraceSeconds))
                throw new InvalidOperationException("Head gaze dwell and focus grace timing values are invalid.");
            _camera = camera;
            _eventSystem = eventSystem;
            _gazePointer = new PointerEventData(eventSystem);
            _reticlePresenter = reticlePresenter;
            _dwellState.Reset();
            _lastTargetIdentity = null;
            enabled = true;
        }

        public void Unconfigure()
        {
            ResetInteractionState();
            _dwellState.Reset();
            _lastTargetIdentity = null;
            _gazeSurfaces.Clear();
            _inputSuspensions.Clear();
            _camera = null;
            _eventSystem = null;
            _gazePointer = null;
            _reticlePresenter = null;
            enabled = false;
        }

        public void Dispose() => Unconfigure();

        public IDisposable SuspendPanelInput()
        {
            if (_gazePointer == null) throw new InvalidOperationException("Head gaze input is not configured.");
            var id = ++_nextSuspensionId;
            _inputSuspensions.Add(id);
            ResetInteractionState();
            _dwellState.ClearTarget(requireGazeExit: true);
            return new InputSuspension(this, id);
        }

        sealed class InputSuspension : IDisposable
        {
            HeadGazeDwellController _owner;
            readonly long _id;
            public InputSuspension(HeadGazeDwellController owner, long id) { _owner = owner; _id = id; }
            public void Dispose()
            {
                var owner = _owner;
                _owner = null;
                if (owner != null && owner._inputSuspensions.Remove(_id)) owner.ResetInteractionState();
            }
        }

        public IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform surfaceRoot, int priority, string label)
        {
            if (_camera == null || _eventSystem == null)
                throw new InvalidOperationException("Head gaze input must be configured before registering a supplemental surface.");
            if (surfaceRoot == null) throw new ArgumentNullException(nameof(surfaceRoot));
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A surface label is required.", nameof(label));

            var binding = CreateCanvasBinding(surfaceRoot, priority, label);
            binding.Canvas.worldCamera = _camera;
            if (HandOnly) ClinicalNearTouch.Bind(surfaceRoot);
            var id = ++_nextSurfaceId;
            _gazeSurfaces.Add(id, binding);
            ResetInteractionState();
            return new GazeSurfaceRegistration(this, id);
        }

        void Update() => TickInput(Time.unscaledDeltaTime);

        internal void TickInput(float unscaledDeltaTime)
        {
            if (HandOnly || !isActiveAndEnabled || _camera == null || _eventSystem == null || _inputSuspensions.Count != 0)
            {
                ResetInteractionState();
                return;
            }

            var button = FindFocusedButton();
            var eligible = IsButtonEligible(button);
            var dwellTarget = ResolveDwellTarget(button);
            _focusedSurfaceId = ResolveSurfaceId(eligible ? button : null);
            SetPointerFocus(eligible ? button : null);
            var result = _dwellState.UpdateTarget(
                dwellTarget,
                eligible,
                unscaledDeltaTime,
                _dwellSeconds,
                _focusGraceSeconds);
            var progressButton = ResolveProgressButton(button, eligible, result.Progress);

            _reticlePresenter?.PresentGaze(
                _hasGazeSurfaceHit,
                _gazeSurfaceDistance,
                eligible,
                result.Progress);
            UpdateButtonProgress(progressButton, result.Progress);

            if (result.Activated) ActivatePointer(button);
        }

        void OnDisable() => ResetInteractionState();

        void OnDestroy() => Unconfigure();

        object ResolveDwellTarget(Button button)
        {
            if (button != null && button.transform is RectTransform rect)
            {
                _lastTargetIdentity = button;
                _lastTargetWorldToLocal = rect.worldToLocalMatrix;
                _lastTargetRect = rect.rect;
                _lastTargetPosition = rect.position;
                _lastTargetNormal = rect.forward;
                return button;
            }
            if (_lastTargetIdentity == null) return null;
            var ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var plane = new Plane(_lastTargetNormal, _lastTargetPosition);
            if (plane.Raycast(ray, out var distance) && distance > 0f &&
                _lastTargetRect.Contains(_lastTargetWorldToLocal.MultiplyPoint3x4(ray.GetPoint(distance))))
            {
                // Hidden, removed, or destroyed controls do not prove that the visitor looked away.
                return _lastTargetIdentity;
            }
            _lastTargetIdentity = null;
            return null;
        }

        Button FindFocusedButton()
        {
            _hasGazeSurfaceHit = false;
            _gazeSurfaceDistance = 0f;
            var gazeRay = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var pointer = _gazePointer;
            if (pointer == null) return null;
            pointer.Reset();
            pointer.position = GetGazeScreenPoint();
            var closestButtonDistance = float.PositiveInfinity;
            var closestSurfaceDistance = float.PositiveInfinity;
            var focusedButtonPriority = int.MinValue;
            var focusedSurfacePriority = int.MinValue;
            Button focusedButton = null;

            foreach (var binding in _gazeSurfaces.Values)
                EvaluateBinding(
                    binding,
                    pointer,
                    gazeRay,
                    ref closestButtonDistance,
                    ref closestSurfaceDistance,
                    ref focusedButtonPriority,
                    ref focusedSurfacePriority,
                    ref focusedButton);

            return focusedButton;
        }

        void EvaluateBinding(
            GazeCanvasBinding binding,
            PointerEventData pointer,
            Ray gazeRay,
            ref float closestButtonDistance,
            ref float closestSurfaceDistance,
            ref int focusedButtonPriority,
            ref int focusedSurfacePriority,
            ref Button focusedButton)
        {
            if (!binding.IsUsable) return;

            _raycastResults.Clear();
            binding.Raycaster.Raycast(pointer, _raycastResults);
            for (var resultIndex = 0; resultIndex < _raycastResults.Count; resultIndex++)
            {
                var result = _raycastResults[resultIndex];
                if (result.gameObject == null) continue;

                var distance = ResolveRaycastDistance(result, gazeRay);
                if (distance <= 0f || float.IsInfinity(distance)) continue;
                if (IsBetterGazeHit(binding.Priority, distance, focusedSurfacePriority, closestSurfaceDistance))
                {
                    focusedSurfacePriority = binding.Priority;
                    closestSurfaceDistance = distance;
                    _hasGazeSurfaceHit = true;
                    _gazeSurfaceDistance = distance;
                }

                var button = result.gameObject.GetComponentInParent<Button>();
                // Retain visible disabled targets so cancellation cannot look like a gaze exit.
                if (button == null || !button.gameObject.activeInHierarchy ||
                    !IsBetterGazeHit(binding.Priority, distance, focusedButtonPriority, closestButtonDistance))
                    continue;
                focusedButtonPriority = binding.Priority;
                closestButtonDistance = distance;
                focusedButton = button;
            }

            TryFindFocusedButtonByGeometry(
                binding,
                gazeRay,
                ref closestButtonDistance,
                ref closestSurfaceDistance,
                ref focusedButtonPriority,
                ref focusedSurfacePriority,
                ref focusedButton);
        }

        void TryFindFocusedButtonByGeometry(
            GazeCanvasBinding binding,
            Ray gazeRay,
            ref float closestButtonDistance,
            ref float closestSurfaceDistance,
            ref int focusedButtonPriority,
            ref int focusedSurfacePriority,
            ref Button focusedButton)
        {
            if (TryGetRectTransformDistance(binding.Canvas.transform as RectTransform, gazeRay, out var canvasDistance) &&
                IsBetterGazeHit(binding.Priority, canvasDistance, focusedSurfacePriority, closestSurfaceDistance))
            {
                focusedSurfacePriority = binding.Priority;
                closestSurfaceDistance = canvasDistance;
                _hasGazeSurfaceHit = true;
                _gazeSurfaceDistance = canvasDistance;
            }

            var buttons = binding.Buttons;
            for (var index = 0; index < buttons.Length; index++)
            {
                var button = buttons[index];
                if (button == null || !button.gameObject.activeInHierarchy ||
                    !TryGetRectTransformDistance(button.transform as RectTransform, gazeRay, out var buttonDistance))
                    continue;

                if (IsBetterGazeHit(binding.Priority, buttonDistance, focusedSurfacePriority, closestSurfaceDistance))
                {
                    focusedSurfacePriority = binding.Priority;
                    closestSurfaceDistance = buttonDistance;
                    _hasGazeSurfaceHit = true;
                    _gazeSurfaceDistance = buttonDistance;
                }

                if (!IsBetterGazeHit(binding.Priority, buttonDistance, focusedButtonPriority, closestButtonDistance))
                    continue;
                focusedButtonPriority = binding.Priority;
                closestButtonDistance = buttonDistance;
                focusedButton = button;
            }
        }

        static bool IsBetterGazeHit(int priority, float distance, int bestPriority, float bestDistance)
            => priority > bestPriority || (priority == bestPriority && distance < bestDistance);

        static bool IsButtonEligible(Button button)
            => button != null && button.isActiveAndEnabled && button.interactable && button.gameObject.activeInHierarchy;

        Button ResolveProgressButton(Button focusedButton, bool eligible, float progress)
        {
            if (eligible) return focusedButton;
            if (progress > 0f && IsButtonEligible(_lastProgressButton)) return _lastProgressButton;
            return null;
        }

        float ResolveRaycastDistance(RaycastResult result, Ray gazeRay)
        {
            if (result.distance > 0.001f && !float.IsInfinity(result.distance)) return result.distance;
            var rect = result.gameObject.GetComponent<RectTransform>() ??
                       result.gameObject.GetComponentInParent<RectTransform>();
            if (rect == null || !RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    rect,
                    GetGazeScreenPoint(),
                    GetEventCamera(rect),
                    out var position))
                return 0f;
            return Vector3.Distance(gazeRay.origin, position);
        }

        static bool TryGetRectTransformDistance(RectTransform rect, Ray gazeRay, out float distance)
        {
            distance = 0f;
            if (rect == null) return false;
            var plane = new Plane(rect.forward, rect.position);
            if (!plane.Raycast(gazeRay, out var enter) || enter <= 0f) return false;
            var localPoint = rect.InverseTransformPoint(gazeRay.GetPoint(enter));
            if (!rect.rect.Contains(localPoint)) return false;
            distance = enter;
            return true;
        }

        Vector2 GetGazeScreenPoint()
            => _camera.ViewportToScreenPoint(new Vector3(0.5f, 0.5f, 0f));

        Camera GetEventCamera(RectTransform target)
        {
            var canvas = target != null ? target.GetComponentInParent<Canvas>() : null;
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null
                ? canvas.worldCamera
                : _camera;
        }

        static GazeCanvasBinding CreateCanvasBinding(Transform target, int priority, string label)
        {
            // SpatialDisplayHost keeps the entire panel inactive until a QR session commits.
            // Bind its canvas now; GazeCanvasBinding.IsUsable still excludes it until visible.
            var canvas = target != null ? target.GetComponentInParent<Canvas>(true) : null;
            var raycaster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
            if (canvas == null || raycaster == null || canvas.renderMode != RenderMode.WorldSpace)
                throw new InvalidOperationException($"{label} must provide a world-space Canvas and GraphicRaycaster.");
            return new GazeCanvasBinding(
                canvas,
                raycaster,
                canvas.GetComponent<CanvasGroup>(),
                canvas.GetComponentsInChildren<Button>(true),
                priority);
        }

        void ResetInteractionState()
        {
            SetPointerFocus(null);
            _dwellState.ClearTarget();
            _focusedSurfaceId = 0;
            _hasGazeSurfaceHit = false;
            _gazeSurfaceDistance = 0f;
            UpdateButtonProgress(null, 0f);
            _reticlePresenter?.PresentGaze(false, 0f, false, 0f);
        }

        void RemoveSurface(long id)
        {
            if (!_gazeSurfaces.Remove(id)) return;
            ResetInteractionState();
        }

        long ResolveSurfaceId(Button focusedButton)
        {
            if (focusedButton == null) return 0;
            foreach (var pair in _gazeSurfaces)
            {
                var buttons = pair.Value.Buttons;
                for (var index = 0; index < buttons.Length; index++)
                    if (buttons[index] == focusedButton)
                        return pair.Key;
            }
            return 0;
        }

        bool IsSurfaceFocused(long id)
            => id > 0 && id == _focusedSurfaceId;

        void UpdateButtonProgress(Button button, float progress)
        {
            if (_lastProgressButton != null && _lastProgressButton != button)
                SetButtonProgress(_lastProgressButton, 0f);
            _lastProgressButton = button;
            if (button != null) SetButtonProgress(button, progress);
        }

        static void SetButtonProgress(Button button, float progress)
            => EntryUIButtonVisual.ApplyGazeFocus(button, progress);

        internal void SetPointerFocus(Button target)
        {
            var pointer = _gazePointer;
            if (_pointerFocusedButton == target)
            {
                if (pointer != null) pointer.pointerEnter = target != null ? target.gameObject : null;
                return;
            }

            var previous = _pointerFocusedButton;
            _pointerFocusedButton = target;
            if (pointer == null) return;

            if (previous != null)
            {
                pointer.pointerEnter = previous.gameObject;
                ExecuteEvents.Execute(previous.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            }

            pointer.pointerEnter = target != null ? target.gameObject : null;
            if (target != null) ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        }

        internal bool ActivatePointer(Button target)
        {
            var pointer = _gazePointer;
            if (pointer == null || target == null || target != _pointerFocusedButton || !IsButtonEligible(target))
                return false;

            pointer.button = PointerEventData.InputButton.Left;
            pointer.pointerPress = target.gameObject;
            pointer.rawPointerPress = target.gameObject;
            pointer.eligibleForClick = true;
            ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            var shouldClick = target == _pointerFocusedButton && IsButtonEligible(target);
            if (target != null) ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            if (shouldClick)
            {
                pointer.clickCount = 1;
                ExecuteEvents.Execute(target.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            }

            pointer.pointerPress = null;
            pointer.rawPointerPress = null;
            pointer.eligibleForClick = false;
            if (target != null && _eventSystem != null && _eventSystem.currentSelectedGameObject == target.gameObject)
                _eventSystem.SetSelectedGameObject(null, pointer);
            return shouldClick;
        }

        static bool IsPositiveFinite(float value)
            => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        static bool IsNonNegativeFinite(float value)
            => value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        readonly struct GazeCanvasBinding
        {
            public GazeCanvasBinding(
                Canvas canvas,
                GraphicRaycaster raycaster,
                CanvasGroup group,
                Button[] buttons,
                int priority)
            {
                Canvas = canvas;
                Raycaster = raycaster;
                Group = group;
                Buttons = buttons ?? Array.Empty<Button>();
                Priority = priority;
            }

            public Canvas Canvas { get; }
            public GraphicRaycaster Raycaster { get; }
            public CanvasGroup Group { get; }
            public Button[] Buttons { get; }
            public int Priority { get; }
            public bool IsUsable => Canvas != null && Canvas.isActiveAndEnabled && Canvas.gameObject.activeInHierarchy &&
                                    Raycaster != null && Raycaster.isActiveAndEnabled &&
                                    (Group == null || (Group.alpha > 0.001f && Group.interactable && Group.blocksRaycasts));
        }

        sealed class GazeSurfaceRegistration : IFrontendGazeSurfaceRegistration
        {
            HeadGazeDwellController _owner;
            readonly long _id;

            public GazeSurfaceRegistration(HeadGazeDwellController owner, long id)
            {
                _owner = owner;
                _id = id;
            }

            public void Invalidate()
            {
                var owner = _owner;
                if (owner != null && owner._gazeSurfaces.ContainsKey(_id))
                    owner.ResetInteractionState();
            }

            public bool IsFocused
            {
                get
                {
                    var owner = _owner;
                    return owner != null && owner.IsSurfaceFocused(_id);
                }
            }

            public void Dispose()
            {
                var owner = _owner;
                _owner = null;
                if (owner != null) owner.RemoveSurface(_id);
            }
        }
    }
}
