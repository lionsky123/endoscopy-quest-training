using System;
using System.Collections.Generic;
using System.Linq;
using BotanicalGardenQR.SpatialAnchorAdmin.Official;
using Meta.XR;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BotanicalGardenQR.SpatialAnchorAdmin.Authoring
{
    public readonly struct AdminControllerInputActions
    {
        public AdminControllerInputActions(
            bool triggerPressed,
            bool triggerReleased,
            bool placementPressed,
            bool recallRequested)
        {
            TriggerPressed = triggerPressed;
            TriggerReleased = triggerReleased;
            PlacementPressed = placementPressed;
            RecallRequested = recallRequested;
        }

        public bool TriggerPressed { get; }
        public bool TriggerReleased { get; }
        public bool PlacementPressed { get; }
        public bool RecallRequested { get; }
    }

    public static class AdminControllerControlMap
    {
        public const string ControllerAnchorName = "LeftControllerInHandAnchor";
        public static OVRInput.Controller TrackedController => OVRInput.Controller.LTouch;
        public static OVRInput.RawButton PanelConfirmButton => OVRInput.RawButton.LIndexTrigger;
        public static OVRInput.RawButton SpatialConfirmButton => OVRInput.RawButton.X;
        public static OVRInput.RawButton RecallButton => OVRInput.RawButton.Y;
        public static OVRInput.RawAxis2D CandidateFineTuneAxis => OVRInput.RawAxis2D.LThumbstick;
    }

    public static class AdminCandidatePoseResolver
    {
        public static bool TryResolve(
            Ray ray,
            bool hasEnvironmentHit,
            Vector3 environmentPoint,
            Vector3 environmentNormal,
            out Pose pose,
            out string diagnosticTag)
        {
            pose = default;
            diagnosticTag = string.Empty;
            if (hasEnvironmentHit && IsFinite(environmentPoint) && IsFinite(environmentNormal) &&
                environmentNormal.sqrMagnitude > 0.0001f)
            {
                var up = Vector3.up;
                var forward = Vector3.ProjectOnPlane(environmentNormal, up);
                if (forward.sqrMagnitude < 0.0001f)
                    forward = Vector3.ProjectOnPlane(-ray.direction, up);
                if (forward.sqrMagnitude < 0.0001f)
                    forward = Vector3.forward;
                pose = new Pose(environmentPoint, Quaternion.LookRotation(forward.normalized, up));
                diagnosticTag = "admin_candidate.environment_raycast";
                return true;
            }
            diagnosticTag = "admin_candidate.environment_no_hit";
            return false;
        }

        static bool IsFinite(Vector3 value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    }

    public static class AdminCandidateFineTuneResolver
    {
        public static Vector3 ResolveWorldDelta(
            Pose candidatePose,
            Vector2 axis,
            float unscaledDeltaTime,
            float metersPerSecond,
            float deadZone)
        {
            if (!IsFinite(candidatePose.position) || !IsFinite(candidatePose.rotation) ||
                !IsFinite(axis) || !float.IsFinite(unscaledDeltaTime) ||
                !float.IsFinite(metersPerSecond) || !float.IsFinite(deadZone) ||
                unscaledDeltaTime <= 0f || metersPerSecond <= 0f)
                return Vector3.zero;

            var clampedDeadZone = Mathf.Clamp(deadZone, 0f, 0.95f);
            var magnitude = Mathf.Clamp01(axis.magnitude);
            if (magnitude <= clampedDeadZone) return Vector3.zero;

            var normalizedMagnitude = (magnitude - clampedDeadZone) / (1f - clampedDeadZone);
            var normalizedAxis = axis.normalized * normalizedMagnitude;
            var right = Vector3.ProjectOnPlane(candidatePose.rotation * Vector3.right, Vector3.up);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            return (right.normalized * normalizedAxis.x + Vector3.up * normalizedAxis.y) *
                   metersPerSecond * unscaledDeltaTime;
        }

        static bool IsFinite(Vector2 value)
            => float.IsFinite(value.x) && float.IsFinite(value.y);

        static bool IsFinite(Vector3 value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        static bool IsFinite(Quaternion value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) &&
               float.IsFinite(value.z) && float.IsFinite(value.w);
    }

    /// <summary>Pure edge/rearm gate for the left trigger, X, and Y hold.</summary>
    public sealed class AdminControllerInputLatch
    {
        bool _triggerHeld;
        bool _triggerRequiresRelease;
        bool _placementHeld;
        bool _placementRequiresRelease;
        float _recallStartedAt = -1f;
        bool _recallTriggered;
        bool _recallRequiresRelease;

        public AdminControllerInputActions Update(
            bool triggerHeld,
            bool placementHeld,
            bool recallHeld,
            float now,
            float recallHoldSeconds)
        {
            var triggerPressed = false;
            var triggerReleased = false;
            if (_triggerRequiresRelease)
            {
                _triggerHeld = triggerHeld;
                if (!triggerHeld) _triggerRequiresRelease = false;
            }
            else
            {
                triggerPressed = triggerHeld && !_triggerHeld;
                triggerReleased = !triggerHeld && _triggerHeld;
                _triggerHeld = triggerHeld;
            }

            var placementPressed = false;
            if (_placementRequiresRelease)
            {
                _placementHeld = placementHeld;
                if (!placementHeld) _placementRequiresRelease = false;
            }
            else
            {
                placementPressed = placementHeld && !_placementHeld;
                _placementHeld = placementHeld;
            }

            var recallRequested = false;
            if (_recallRequiresRelease)
            {
                _recallStartedAt = -1f;
                _recallTriggered = false;
                if (!recallHeld) _recallRequiresRelease = false;
            }
            else if (!recallHeld)
            {
                _recallStartedAt = -1f;
                _recallTriggered = false;
            }
            else if (_recallStartedAt < 0f)
            {
                _recallStartedAt = now;
            }
            else if (!_recallTriggered &&
                     now - _recallStartedAt >= Mathf.Max(0.01f, recallHoldSeconds))
            {
                _recallTriggered = true;
                recallRequested = true;
            }

            return new AdminControllerInputActions(
                triggerPressed,
                triggerReleased,
                placementPressed,
                recallRequested);
        }

        public void Reset()
        {
            _triggerHeld = false;
            _triggerRequiresRelease = true;
            _placementHeld = false;
            _placementRequiresRelease = true;
            _recallStartedAt = -1f;
            _recallTriggered = false;
            _recallRequiresRelease = true;
        }
    }

    /// <summary>
    /// Drives the standard Unity pointer lifecycle for one custom controller
    /// pointer without taking ownership of Button callbacks or presentation.
    /// </summary>
    internal sealed class AdminButtonPointerSession
    {
        const int PointerId = -901;

        readonly EventSystem _eventSystem;
        readonly PointerEventData _eventData;
        Button _focused;
        Button _pressed;

        public AdminButtonPointerSession(EventSystem eventSystem)
        {
            _eventSystem = eventSystem ?? throw new ArgumentNullException(nameof(eventSystem));
            _eventData = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = PointerId
            };
        }

        public bool IsPressed => _pressed != null;
        public bool IsPressedTarget(Button target)
            => target != null && _pressed == target && _focused == target;

        public void SetFocus(Button target)
        {
            if (_focused == target) return;

            var previous = _focused;
            _focused = target;
            if (previous != null)
            {
                _eventData.pointerEnter = previous.gameObject;
                Execute(previous, ExecuteEvents.pointerExitHandler);
            }

            if (_pressed != null && _pressed != target)
            {
                var pressed = _pressed;
                _pressed = null;
                Execute(pressed, ExecuteEvents.pointerUpHandler);
                _eventData.pointerPress = null;
                _eventData.rawPointerPress = null;
                ClearSelection(pressed);
            }

            _eventData.pointerEnter = target != null ? target.gameObject : null;
            if (target != null) Execute(target, ExecuteEvents.pointerEnterHandler);
            if (_pressed == null) ClearSelection(previous);
        }

        public void Press()
        {
            if (_pressed != null || _focused == null) return;
            _pressed = _focused;
            _eventData.pointerPress = _pressed.gameObject;
            _eventData.rawPointerPress = _pressed.gameObject;
            Execute(_pressed, ExecuteEvents.pointerDownHandler);
        }

        public bool Release(Button releaseTarget)
        {
            if (_pressed == null) return false;

            var pressed = _pressed;
            _pressed = null;
            Execute(pressed, ExecuteEvents.pointerUpHandler);
            var clicked = false;
            if (pressed == releaseTarget && pressed == _focused && IsAvailable(pressed))
            {
                _eventData.clickCount = 1;
                Execute(pressed, ExecuteEvents.pointerClickHandler);
                clicked = true;
            }

            _eventData.pointerPress = null;
            _eventData.rawPointerPress = null;
            ClearSelection(pressed);
            return clicked;
        }

        public void Cancel()
        {
            var pressed = _pressed;
            _pressed = null;
            if (pressed != null) Execute(pressed, ExecuteEvents.pointerUpHandler);

            var focused = _focused;
            _focused = null;
            if (focused != null)
            {
                _eventData.pointerEnter = focused.gameObject;
                Execute(focused, ExecuteEvents.pointerExitHandler);
            }

            _eventData.pointerEnter = null;
            _eventData.pointerPress = null;
            _eventData.rawPointerPress = null;
            ClearSelection(pressed != null ? pressed : focused);
        }

        void Execute<T>(Button target, ExecuteEvents.EventFunction<T> handler)
            where T : IEventSystemHandler
        {
            if (!IsAvailable(target)) return;
            _eventData.button = PointerEventData.InputButton.Left;
            ExecuteEvents.Execute(target.gameObject, _eventData, handler);
        }

        void ClearSelection(Button target)
        {
            if (target != null && _eventSystem.currentSelectedGameObject == target.gameObject)
                _eventSystem.SetSelectedGameObject(null, _eventData);
        }

        static bool IsAvailable(Button button)
            => button != null && button.gameObject != null && button.gameObject.activeInHierarchy;
    }

    internal enum AdminPointerFeedbackState
    {
        RayOnly,
        Panel,
        Focused,
        Pressed
    }

    /// <summary>
    /// Presents one controller ray and one non-interactive endpoint ring. Input
    /// hit testing remains owned by AdminControllerInput.
    /// </summary>
    internal sealed class AdminPointerFeedbackPresenter
    {
        const float RingDiameterMeters = 0.036f;
        const float SurfaceOffsetMeters = 0.003f;

        static readonly Color Idle = new Color(0.72f, 0.78f, 0.82f, 0.9f);
        static readonly Color Focused = new Color(0.2f, 0.95f, 0.76f, 1f);
        static readonly Color Pressed = new Color(1f, 0.72f, 0.22f, 1f);

        readonly LineRenderer _pointerLine;
        readonly AdminPointerRingGraphic _focusRing;
        readonly float _maximumRayDistance;

        public AdminPointerFeedbackPresenter(
            LineRenderer pointerLine,
            AdminPointerRingGraphic focusRing,
            float maximumRayDistance)
        {
            _pointerLine = pointerLine ?? throw new ArgumentNullException(nameof(pointerLine));
            _focusRing = focusRing ?? throw new ArgumentNullException(nameof(focusRing));
            _maximumRayDistance = Mathf.Max(0.5f, maximumRayDistance);
        }

        public void Present(
            Ray ray,
            float distance,
            RectTransform surface,
            Canvas canvas,
            AdminPointerFeedbackState state)
        {
            var safeDistance = Mathf.Clamp(distance, 0.1f, _maximumRayDistance);
            var color = ResolveColor(state);
            _pointerLine.enabled = true;
            _pointerLine.positionCount = 2;
            _pointerLine.SetPosition(0, ray.origin);
            _pointerLine.SetPosition(1, ray.GetPoint(safeDistance));
            SetColor(_pointerLine, color);

            if (state == AdminPointerFeedbackState.RayOnly || surface == null || canvas == null)
            {
                HideRing();
                return;
            }

            var direction = ray.direction.sqrMagnitude > Mathf.Epsilon
                ? ray.direction.normalized
                : surface.forward;
            var center = ray.GetPoint(safeDistance) - direction * SurfaceOffsetMeters;
            var ringTransform = _focusRing.rectTransform;
            if (ringTransform.parent != canvas.transform)
                ringTransform.SetParent(canvas.transform, false);
            if (ringTransform.GetSiblingIndex() != canvas.transform.childCount - 1)
                ringTransform.SetAsLastSibling();
            ringTransform.anchorMin = Vector2.one * 0.5f;
            ringTransform.anchorMax = Vector2.one * 0.5f;
            ringTransform.pivot = Vector2.one * 0.5f;
            ringTransform.sizeDelta = Vector2.one;
            ringTransform.gameObject.layer = canvas.gameObject.layer;
            ringTransform.SetPositionAndRotation(
                center,
                Quaternion.LookRotation(surface.forward, surface.up));
            SetWorldScale(ringTransform, RingDiameterMeters);
            _focusRing.raycastTarget = false;
            _focusRing.color = color;
            _focusRing.enabled = true;
        }

        public void HideRing()
        {
            _focusRing.enabled = false;
        }

        public void Hide()
        {
            _pointerLine.enabled = false;
            HideRing();
        }

        static Color ResolveColor(AdminPointerFeedbackState state)
        {
            if (state == AdminPointerFeedbackState.Pressed) return Pressed;
            if (state == AdminPointerFeedbackState.Focused) return Focused;
            return Idle;
        }

        static void SetColor(LineRenderer renderer, Color color)
        {
            renderer.startColor = color;
            renderer.endColor = color;
        }

        static void SetWorldScale(Transform target, float size)
        {
            target.localScale = Vector3.one;
            var lossyScale = target.lossyScale;
            target.localScale = new Vector3(
                size / Mathf.Max(Mathf.Abs(lossyScale.x), 0.0001f),
                size / Mathf.Max(Mathf.Abs(lossyScale.y), 0.0001f),
                size / Mathf.Max(Mathf.Abs(lossyScale.z), 0.0001f));
        }
    }

    internal readonly struct AdminPointerHit
    {
        public AdminPointerHit(
            Button button,
            RectTransform surface,
            Canvas canvas,
            float distance)
        {
            Button = button;
            Surface = surface;
            Canvas = canvas;
            Distance = distance;
        }

        public Button Button { get; }
        public RectTransform Surface { get; }
        public Canvas Canvas { get; }
        public float Distance { get; }
        public bool HasPanel => Surface != null && Canvas != null;
    }

    /// <summary>
    /// The only Admin production input owner. A left-controller ray targets
    /// world-space buttons. The left index trigger is UI-only; X confirms an
    /// explicit spatial-selection mode, and Y-hold cancels or recalls.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class AdminControllerInput : MonoBehaviour
    {
        [Header("Scene bindings")]
        [SerializeField] Transform _rayOrigin;
        [SerializeField] Transform _placementPose;
        [SerializeField] EventSystem _eventSystem;
        [SerializeField] MonoBehaviour _officialAnchorAdminSource;
        [SerializeField] LineRenderer _pointerLine;
        [SerializeField] AdminPointerRingGraphic _focusRing;
        [SerializeField] EnvironmentRaycastManager _environmentRaycastManager;

        [Header("Controller input")]
        [SerializeField, Min(0.5f)] float _maximumRayDistance = 8f;
        [SerializeField, Min(0.2f)] float _recallHoldSeconds = 0.55f;
        [SerializeField, Min(0.25f)] float _canvasRefreshSeconds = 1f;
        [SerializeField, Min(0.005f)] float _candidateFineTuneMetersPerSecond = 0.04f;
        [SerializeField, Range(0f, 0.95f)] float _candidateFineTuneDeadZone = 0.2f;

        readonly AdminControllerInputLatch _latch = new AdminControllerInputLatch();
        AdminButtonPointerSession _buttonPointer;
        AdminPointerFeedbackPresenter _pointerFeedback;
        CanvasBinding[] _bindings = Array.Empty<CanvasBinding>();
        readonly List<RaycastResult> _graphicRaycastResults = new List<RaycastResult>();
        Camera _graphicRaycastCamera;
        PointerEventData _graphicRaycastEventData;
        float _nextCanvasRefreshAt;
        bool _reportedControllerUnavailable;
        IOfficialSpatialAnchorAdminCommands _officialAnchorAdmin;

        public bool IsConfigured =>
            _rayOrigin != null && _placementPose != null && _eventSystem != null &&
            (_officialAnchorAdmin ?? _officialAnchorAdminSource as IOfficialSpatialAnchorAdminCommands) != null &&
            _pointerLine != null && _focusRing != null;

        void Awake()
        {
            _officialAnchorAdmin = _officialAnchorAdminSource as IOfficialSpatialAnchorAdminCommands;
            ValidateConfiguration();
            _buttonPointer = new AdminButtonPointerSession(_eventSystem);
            _pointerFeedback = new AdminPointerFeedbackPresenter(
                _pointerLine,
                _focusRing,
                _maximumRayDistance);
            RefreshCanvasBindings();
        }

        void OnEnable()
        {
            _latch.Reset();
            _nextCanvasRefreshAt = 0f;
            _reportedControllerUnavailable = false;
        }

        void OnDisable()
        {
            _latch.Reset();
            _buttonPointer?.Cancel();
            HidePointerFeedback();
            if (_placementPose != null) _placementPose.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_graphicRaycastCamera == null) return;
            var probeObject = _graphicRaycastCamera.gameObject;
            _graphicRaycastCamera = null;
            _graphicRaycastEventData = null;
            if (Application.isPlaying) Destroy(probeObject);
            else DestroyImmediate(probeObject);
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextCanvasRefreshAt)
            {
                RefreshCanvasBindings();
                _nextCanvasRefreshAt = Time.unscaledTime + Mathf.Max(0.25f, _canvasRefreshSeconds);
            }

            var controllerAvailable = OVRInput.IsControllerConnected(
                AdminControllerControlMap.TrackedController);
            if (!controllerAvailable)
            {
                _buttonPointer?.Cancel();
                HidePointerFeedback();
                if (_placementPose != null) _placementPose.gameObject.SetActive(false);
                if (!_reportedControllerUnavailable)
                {
                    _reportedControllerUnavailable = true;
                    Debug.LogWarning("[AdminInteraction] admin.controller.left_unavailable", this);
                }
                _latch.Reset();
                return;
            }
            _reportedControllerUnavailable = false;

            var actions = _latch.Update(
                OVRInput.Get(AdminControllerControlMap.PanelConfirmButton),
                OVRInput.Get(AdminControllerControlMap.SpatialConfirmButton),
                OVRInput.Get(AdminControllerControlMap.RecallButton),
                Time.unscaledTime,
                _recallHoldSeconds);
            if (actions.RecallRequested)
            {
                _buttonPointer.Cancel();
                HidePointerFeedback();
                if (_officialAnchorAdmin.GuidedPlacementPhase == GuidedAnchorPlacementPhase.Idle)
                {
                    _officialAnchorAdmin.SetWorkspaceVisible(true);
                }
                else if (!_officialAnchorAdmin.CancelGuidedAnchorPlacement(out var error))
                {
                    _officialAnchorAdmin.ReportGuidedPlacementFailure(error);
                }
                return;
            }

            var placing = _officialAnchorAdmin.GuidedPlacementPhase ==
                          GuidedAnchorPlacementPhase.PlacingCandidate;
            if (placing)
            {
                _buttonPointer.Cancel();
                HidePointerFeedback();
                var candidateRay = new Ray(_rayOrigin.position, _rayOrigin.forward);
                var hasEnvironmentHit = false;
                var environmentPoint = default(Vector3);
                var environmentNormal = default(Vector3);
                if (_environmentRaycastManager != null && EnvironmentRaycastManager.IsSupported &&
                    _environmentRaycastManager.Raycast(candidateRay, out var environmentHit, _maximumRayDistance))
                {
                    hasEnvironmentHit = true;
                    environmentPoint = environmentHit.point;
                    environmentNormal = environmentHit.normal;
                }

                var hasCandidatePose = AdminCandidatePoseResolver.TryResolve(
                    candidateRay,
                    hasEnvironmentHit,
                    environmentPoint,
                    environmentNormal,
                    out var candidatePose,
                    out var poseTag);

                if (hasCandidatePose)
                    _placementPose.SetPositionAndRotation(candidatePose.position, candidatePose.rotation);
                _placementPose.gameObject.SetActive(hasCandidatePose);

                if (actions.PlacementPressed)
                {
                    if (!hasCandidatePose)
                    {
                        _officialAnchorAdmin.ReportGuidedPlacementFailure(
                            $"射线尚未命中现实表面（{poseTag}）。请重新瞄准后再按 X。" );
                    }
                    else if (!_officialAnchorAdmin.PlaceGuidedAnchorCandidate(candidatePose, out var error))
                    {
                        _officialAnchorAdmin.ReportGuidedPlacementFailure(error);
                    }
                }
                return;
            }

            if (_officialAnchorAdmin.GuidedPlacementPhase == GuidedAnchorPlacementPhase.AdjustingSavedAnchor &&
                _officialAnchorAdmin.TryGetAnchorAdjustment(out var adjustment))
            {
                var reviewPose = adjustment.WorldPose;
                _placementPose.SetPositionAndRotation(reviewPose.position, reviewPose.rotation);
                _placementPose.gameObject.SetActive(true);
                var fineTuneAxis = OVRInput.Get(
                    AdminControllerControlMap.CandidateFineTuneAxis,
                    AdminControllerControlMap.TrackedController);
                var worldDelta = AdminCandidateFineTuneResolver.ResolveWorldDelta(
                    reviewPose,
                    fineTuneAxis,
                    Time.unscaledDeltaTime,
                    _candidateFineTuneMetersPerSecond,
                    _candidateFineTuneDeadZone);
                if (worldDelta.sqrMagnitude > 0f &&
                    !_officialAnchorAdmin.AdjustAnchorAdjustmentPose(worldDelta, out _, out var error))
                    _officialAnchorAdmin.ReportGuidedPlacementFailure(error);
            }

            var ray = new Ray(_rayOrigin.position, _rayOrigin.forward);
            var hit = FindPointerHit(ray);
            var button = hit.Button;
            _buttonPointer.SetFocus(button);
            if (actions.TriggerPressed)
            {
                if (button != null) _buttonPointer.Press();
                else if (!hit.HasPanel && _officialAnchorAdmin.IsEraseMode)
                    _officialAnchorAdmin.BeginEraseHoveredAnchor(out _);
                else if (!hit.HasPanel)
                    _officialAnchorAdmin.BeginRetuneHoveredAnchor(out _);
            }
            if (actions.TriggerReleased)
            {
                if (_buttonPointer.Release(button) && button != null)
                    Debug.Log($"[AdminInteraction] admin.controller.select target={button.name}", button);
            }
            var feedbackState = _buttonPointer.IsPressedTarget(button)
                ? AdminPointerFeedbackState.Pressed
                : button != null
                    ? AdminPointerFeedbackState.Focused
                    : hit.HasPanel
                        ? AdminPointerFeedbackState.Panel
                        : AdminPointerFeedbackState.RayOnly;
            _pointerFeedback.Present(
                ray,
                hit.Distance,
                hit.Surface,
                hit.Canvas,
                feedbackState);
        }

        public void ValidateConfiguration()
        {
            if (!IsConfigured)
                throw new InvalidOperationException(
                    "Admin controller input requires left ray origin, official placement Pose, EventSystem, bridge, pointer line, and focus ring bindings.");
            if (_maximumRayDistance <= 0f || _recallHoldSeconds <= 0f ||
                _candidateFineTuneMetersPerSecond <= 0f ||
                _candidateFineTuneDeadZone < 0f || _candidateFineTuneDeadZone >= 1f)
                throw new InvalidOperationException("Admin controller ray or recall thresholds are invalid.");
        }

        AdminPointerHit FindPointerHit(Ray ray)
        {
            var bestSortingLayer = int.MinValue;
            var bestSortingOrder = int.MinValue;
            var hitDistance = Mathf.Max(0.5f, _maximumRayDistance);
            CanvasBinding? selectedBinding = null;
            Graphic selectedGraphic = null;
            for (var bindingIndex = 0; bindingIndex < _bindings.Length; bindingIndex++)
            {
                var binding = _bindings[bindingIndex];
                if (!binding.IsUsable ||
                    !TryGetRaycastGraphic(binding, ray, out var graphic, out var distance) ||
                    distance > _maximumRayDistance ||
                    !IsBetter(
                        binding.SortingLayerValue,
                        binding.SortingOrder,
                        distance,
                        bestSortingLayer,
                        bestSortingOrder,
                        hitDistance))
                    continue;
                bestSortingLayer = binding.SortingLayerValue;
                bestSortingOrder = binding.SortingOrder;
                hitDistance = distance;
                selectedBinding = binding;
                selectedGraphic = graphic;
            }

            if (!selectedBinding.HasValue)
                return new AdminPointerHit(null, null, null, hitDistance);

            var selected = selectedBinding.Value;
            var focused = selectedGraphic.GetComponentInParent<Button>();
            if (!IsButtonEligible(focused) || focused.GetComponentInParent<Canvas>() != selected.Canvas)
                focused = null;
            return new AdminPointerHit(
                focused,
                selectedGraphic.rectTransform,
                selected.Canvas,
                hitDistance);
        }

        void RefreshCanvasBindings()
        {
            if (!gameObject.scene.IsValid()) return;
            _bindings = gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Where(canvas => canvas != null && canvas.renderMode == RenderMode.WorldSpace &&
                                 canvas.GetComponent<GraphicRaycaster>() != null)
                .Select(canvas =>
                {
                    var sortingCanvas = canvas.overrideSorting || canvas.rootCanvas == null
                        ? canvas
                        : canvas.rootCanvas;
                    return new CanvasBinding(
                        canvas,
                        canvas.GetComponent<GraphicRaycaster>(),
                        SortingLayer.GetLayerValueFromID(sortingCanvas.sortingLayerID),
                        sortingCanvas.sortingOrder);
                })
                .ToArray();
        }

        static bool IsButtonEligible(Button button)
        {
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable() ||
                !button.gameObject.activeInHierarchy)
                return false;
            return true;
        }

        bool TryGetRaycastGraphic(
            CanvasBinding binding,
            Ray ray,
            out Graphic graphic,
            out float distance)
        {
            graphic = null;
            distance = float.PositiveInfinity;
            var canvasTransform = binding.Canvas.transform;
            var plane = new Plane(canvasTransform.forward, canvasTransform.position);
            if (!plane.Raycast(ray, out var canvasDistance) || canvasDistance <= 0f)
                return false;

            EnsureGraphicRaycastProbe();
            var hitPoint = ray.GetPoint(canvasDistance);
            var direction = ray.direction.sqrMagnitude > Mathf.Epsilon
                ? ray.direction.normalized
                : canvasTransform.forward;
            var cameraTransform = _graphicRaycastCamera.transform;
            cameraTransform.SetPositionAndRotation(
                hitPoint - direction,
                Quaternion.LookRotation(direction, canvasTransform.up));

            var previousWorldCamera = binding.Canvas.worldCamera;
            _graphicRaycastResults.Clear();
            try
            {
                binding.Canvas.worldCamera = _graphicRaycastCamera;
                _graphicRaycastEventData.Reset();
                _graphicRaycastEventData.pointerId = -901;
                _graphicRaycastEventData.button = PointerEventData.InputButton.Left;
                _graphicRaycastEventData.position = _graphicRaycastCamera.WorldToScreenPoint(hitPoint);
                binding.Raycaster.Raycast(_graphicRaycastEventData, _graphicRaycastResults);
            }
            finally
            {
                binding.Canvas.worldCamera = previousWorldCamera;
            }

            for (var index = 0; index < _graphicRaycastResults.Count; index++)
            {
                var candidate = _graphicRaycastResults[index].gameObject?.GetComponent<Graphic>();
                if (!IsVisibleRaycastGraphic(candidate) || candidate.canvas != binding.Canvas ||
                    !TryGetRectDistance(candidate.rectTransform, ray, out var candidateDistance))
                    continue;
                graphic = candidate;
                distance = candidateDistance;
                return true;
            }
            return false;
        }

        void EnsureGraphicRaycastProbe()
        {
            if (_graphicRaycastCamera != null && _graphicRaycastEventData != null) return;

            var probeObject = new GameObject("AdminGraphicRaycastProbeCamera", typeof(Camera))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            probeObject.transform.SetParent(transform, false);
            _graphicRaycastCamera = probeObject.GetComponent<Camera>();
            _graphicRaycastCamera.enabled = false;
            _graphicRaycastCamera.cullingMask = 0;
            _graphicRaycastCamera.nearClipPlane = 0.01f;
            _graphicRaycastCamera.farClipPlane = 2f;
            _graphicRaycastCamera.fieldOfView = 60f;
            _graphicRaycastEventData = new PointerEventData(
                _eventSystem != null ? _eventSystem : EventSystem.current);
        }

        static bool IsVisibleRaycastGraphic(Graphic graphic)
            => graphic != null && graphic.isActiveAndEnabled && graphic.raycastTarget &&
               graphic.gameObject.activeInHierarchy && !graphic.canvasRenderer.cull &&
               graphic.depth >= 0 && graphic.color.a > 0.001f &&
               graphic.canvasRenderer.GetInheritedAlpha() > 0.001f;

        void HidePointerFeedback()
        {
            if (_pointerFeedback != null) _pointerFeedback.Hide();
            else
            {
                if (_pointerLine != null) _pointerLine.enabled = false;
                if (_focusRing != null) _focusRing.enabled = false;
            }
        }

        void HideFocusRing()
        {
            if (_pointerFeedback != null) _pointerFeedback.HideRing();
            else if (_focusRing != null) _focusRing.enabled = false;
        }

        static bool IsBetter(
            int sortingLayer,
            int sortingOrder,
            float distance,
            int bestSortingLayer,
            int bestSortingOrder,
            float bestDistance)
            => distance > 0f && !float.IsInfinity(distance) &&
               (sortingLayer > bestSortingLayer ||
                sortingLayer == bestSortingLayer && sortingOrder > bestSortingOrder ||
                sortingLayer == bestSortingLayer && sortingOrder == bestSortingOrder && distance < bestDistance);

        static bool TryGetRectDistance(RectTransform rect, Ray ray, out float distance)
        {
            distance = 0f;
            if (rect == null) return false;
            var plane = new Plane(rect.forward, rect.position);
            if (!plane.Raycast(ray, out var enter) || enter <= 0f) return false;
            var local = rect.InverseTransformPoint(ray.GetPoint(enter));
            if (!rect.rect.Contains(local)) return false;
            distance = enter;
            return true;
        }

        readonly struct CanvasBinding
        {
            public CanvasBinding(
                Canvas canvas,
                GraphicRaycaster raycaster,
                int sortingLayerValue,
                int sortingOrder)
            {
                Canvas = canvas;
                Raycaster = raycaster;
                SortingLayerValue = sortingLayerValue;
                SortingOrder = sortingOrder;
            }

            public Canvas Canvas { get; }
            public GraphicRaycaster Raycaster { get; }
            public int SortingLayerValue { get; }
            public int SortingOrder { get; }
            public bool IsUsable => Canvas != null && Canvas.enabled && Canvas.gameObject.activeInHierarchy &&
                                    Raycaster != null && Raycaster.isActiveAndEnabled;
        }
    }
}
