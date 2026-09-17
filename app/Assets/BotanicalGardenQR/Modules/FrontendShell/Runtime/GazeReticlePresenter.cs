using System;
using BotanicalGardenQR.Activation.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    // Scan feedback stays separate from button hit testing and panel lifetime.
    // The visual hierarchy is authored once in VisitorRuntime.prefab.
    [DisallowMultipleComponent]
    public sealed class GazeReticlePresenter : MonoBehaviour, IScanFeedbackSink
    {
        const float ReticleSurfaceOffset = 0.01f;
        const float ConfirmationFeedbackSeconds = 0.25f;
        const float VisualTransitionSeconds = 0.1f;
        const float AuthoredScale = 0.001f;

        [Header("Prefab-authored Quest reticle")]
        [SerializeField, Min(0.2f)] float _idleDistance = 1.05f;
        [SerializeField] RectTransform _reticleRoot;
        [SerializeField] Image _idleRing;
        [SerializeField] Image _progressRing;
        [SerializeField] Image _reticleDot;
        [SerializeField] Image[] _lockCorners = Array.Empty<Image>();

        Camera _camera;
        Transform _authoredParent;
        IScanFeedbackSource _scanFeedback;
        IDisposable _scanSubscription;
        bool _hasGazeSurfaceHit;
        float _gazeSurfaceDistance;
        bool _buttonHovered;
        float _dwellProgress;
        float _scanProgress;
        float _scanSuccessUntil;
        float _focusBlend;
        float _scanBlend;
        bool _presentationEnabled;

        /// <summary>
        /// The reticle may be prepared before scanning is permitted, but its visual
        /// lifetime is owned by the visitor prologue. Preparing it must not expose
        /// a scan affordance on the start panel.
        /// </summary>
        public void SetPresentationEnabled(bool enabled)
        {
            _presentationEnabled = enabled;
            if (!enabled)
            {
                SetReticleVisible(false);
                return;
            }

            Render();
        }

        public void Prime(Transform viewer)
        {
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));

            var camera = viewer.GetComponent<Camera>();
            if (camera == null)
                throw new InvalidOperationException("The configured visitor viewer must own the gaze Camera component.");
            ValidateConfiguration();

            _camera = camera;
            BindAuthoredReticle();
            ResetPresentation();
            UpdateReticlePose(false, 0f);
            _presentationEnabled = false;
            SetReticleVisible(false);
            enabled = true;
        }

        internal void ValidateConfiguration()
        {
            if (!IsPositiveFinite(_idleDistance))
                throw new InvalidOperationException("Gaze reticle idle distance must be positive and finite.");
            if (_reticleRoot == null || _idleRing == null || _progressRing == null || _reticleDot == null ||
                _lockCorners == null || _lockCorners.Length != 4)
                throw new InvalidOperationException("Gaze reticle requires its complete semantic visual roles.");
            var authoredParent = _authoredParent != null ? _authoredParent : _reticleRoot.parent;
            if (authoredParent != transform)
                throw new InvalidOperationException("Gaze reticle visual ownership must remain local to its presenter.");

            var canvas = _reticleRoot.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                throw new InvalidOperationException("Gaze reticle requires a World-Space Canvas.");
            if (_reticleRoot.GetComponent<GraphicRaycaster>() != null)
                throw new InvalidOperationException("Gaze reticle presentation must not consume input raycasts.");

            ValidateNonInteractiveImage(_idleRing, "idle ring");
            ValidateNonInteractiveImage(_progressRing, "progress ring");
            ValidateNonInteractiveImage(_reticleDot, "reticle dot");
            foreach (var corner in _lockCorners)
            {
                if (corner == null)
                    throw new InvalidOperationException("Gaze reticle lock-corner roles cannot contain null entries.");
                ValidateNonInteractiveImage(corner, "lock corner");
            }
        }

        public void Configure(Transform viewer, IScanFeedbackSource scanFeedback)
        {
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));
            if (scanFeedback == null) throw new ArgumentNullException(nameof(scanFeedback));
            if (_scanSubscription != null) throw new InvalidOperationException("Gaze reticle is already configured.");

            Prime(viewer);
            _scanFeedback = scanFeedback;
            _scanSubscription = _scanFeedback.ObserveScan(this);
        }

        public void Unconfigure()
        {
            _scanSubscription?.Dispose();
            _scanSubscription = null;
            _scanFeedback = null;
            _camera = null;
            _hasGazeSurfaceHit = false;
            _gazeSurfaceDistance = 0f;
            _buttonHovered = false;
            _dwellProgress = 0f;
            _scanProgress = 0f;
            _scanSuccessUntil = 0f;
            _focusBlend = 0f;
            _scanBlend = 0f;
            _presentationEnabled = false;
            if (_reticleRoot != null && _authoredParent != null)
                _reticleRoot.SetParent(_authoredParent, false);
            SetReticleVisible(false);
            enabled = false;
        }

        public void PresentGaze(bool hasSurfaceHit, float surfaceDistance, bool buttonHovered, float dwellProgress)
        {
            _hasGazeSurfaceHit = hasSurfaceHit;
            _gazeSurfaceDistance = surfaceDistance;
            _buttonHovered = buttonHovered;
            _dwellProgress = Mathf.Clamp01(dwellProgress);
            Render();
        }

        public void OnScanFeedbackChanged(ScanFeedbackState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            _scanProgress = state.Progress;
            _scanSuccessUntil = ResolveSuccessUntil(_scanSuccessUntil, state, Time.unscaledTime);
            Render();
        }

        internal static float ResolveSuccessUntil(float currentSuccessUntil, ScanFeedbackState state, float now)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.IsConfirmation) return now + ConfirmationFeedbackSeconds;
            return state.Progress <= 0f && currentSuccessUntil <= now ? 0f : currentSuccessUntil;
        }

        void Update()
        {
            if (_scanSuccessUntil > 0f && Time.unscaledTime >= _scanSuccessUntil)
            {
                _scanSuccessUntil = 0f;
                if (_scanProgress >= 1f) _scanProgress = 0f;
            }
            Render();
        }

        void OnDisable() => SetReticleVisible(false);

        void OnDestroy()
        {
            _scanSubscription?.Dispose();
            _scanSubscription = null;
            if (Application.isPlaying && _reticleRoot != null &&
                _authoredParent != null && _reticleRoot.parent != _authoredParent)
                Destroy(_reticleRoot.gameObject);
        }

        void Render()
        {
            if (_camera == null) return;
            UpdateReticlePose(_hasGazeSurfaceHit, _gazeSurfaceDistance);
            SetReticleVisible(_presentationEnabled);
            var successActive = _scanSuccessUntil > Time.unscaledTime;
            var scanActive = _scanProgress > 0f || successActive;
            var progress = successActive ? 1f : scanActive ? _scanProgress : _dwellProgress;
            SetReticleState(_buttonHovered, scanActive, successActive, progress);
        }

        float CalculateReticleDistance(bool hasCanvasHit, float canvasHitDistance)
        {
            var distance = !hasCanvasHit || !IsPositiveFinite(canvasHitDistance)
                ? _idleDistance
                : Mathf.Max(ReticleSurfaceOffset, canvasHitDistance - ReticleSurfaceOffset);
            // A GraphicRaycaster hit can be very close to the camera on Quest.
            // Keep the reticle in front of the actual XR near clip plane.
            return Mathf.Max(_camera.nearClipPlane + ReticleSurfaceOffset, distance);
        }

        void UpdateReticlePose(bool hasCanvasHit, float canvasDistance)
        {
            if (_reticleRoot == null || _camera == null) return;
            var distance = CalculateReticleDistance(hasCanvasHit, canvasDistance);
            _reticleRoot.SetParent(_camera.transform, false);
            _reticleRoot.localPosition = new Vector3(0f, 0f, distance);
            _reticleRoot.localRotation = Quaternion.identity;
            _reticleRoot.localScale = Vector3.one * (AuthoredScale * distance / _idleDistance);
        }

        void SetReticleState(bool buttonHovered, bool scanActive, bool successActive, float progress)
        {
            if (_reticleDot == null || _progressRing == null || _idleRing == null) return;
            var transitionStep = Time.unscaledDeltaTime <= 0f
                ? 1f
                : Time.unscaledDeltaTime / VisualTransitionSeconds;
            _focusBlend = Mathf.MoveTowards(_focusBlend, buttonHovered || scanActive ? 1f : 0f, transitionStep);
            _scanBlend = Mathf.MoveTowards(_scanBlend, scanActive ? 1f : 0f, transitionStep);

            var emerald = new Color(0.2f, 0.95f, 0.76f, 1f);
            _idleRing.color = Color.Lerp(
                new Color(1f, 1f, 1f, 0.30f),
                new Color(emerald.r, emerald.g, emerald.b, 0.56f),
                _focusBlend);
            _reticleDot.color = successActive
                ? new Color(1f, 0.84f, 0.42f, 1f)
                : Color.Lerp(new Color(1f, 1f, 1f, 0.92f), emerald, _focusBlend);
            _reticleDot.rectTransform.localScale =
                Vector3.one * Mathf.Lerp(1f, successActive ? 1.7f : 1.15f, _focusBlend);
            _progressRing.color = successActive
                ? new Color(1f, 0.84f, 0.42f, 0.98f)
                : new Color(emerald.r, emerald.g, emerald.b, 0.96f * _focusBlend);
            _progressRing.fillAmount = Mathf.Clamp01(progress);

            var lockAlpha = _scanBlend * (0.56f + Mathf.Clamp01(progress) * 0.38f);
            if (_lockCorners != null)
            {
                foreach (var corner in _lockCorners)
                {
                    if (corner == null) continue;
                    corner.color = successActive
                        ? new Color(1f, 0.84f, 0.42f, lockAlpha)
                        : new Color(emerald.r, emerald.g, emerald.b, lockAlpha);
                }
            }
        }

        void SetReticleVisible(bool visible)
        {
            if (_reticleRoot != null && _reticleRoot.gameObject.activeSelf != visible)
                _reticleRoot.gameObject.SetActive(visible);
        }

        void ResetPresentation()
        {
            _hasGazeSurfaceHit = false;
            _gazeSurfaceDistance = 0f;
            _buttonHovered = false;
            _dwellProgress = 0f;
            _scanProgress = 0f;
            _scanSuccessUntil = 0f;
            _focusBlend = 0f;
            _scanBlend = 0f;
            SetReticleState(false, false, false, 0f);
        }

        void BindAuthoredReticle()
        {
            if (_reticleRoot == null || _idleRing == null || _progressRing == null || _reticleDot == null ||
                _lockCorners == null || _lockCorners.Length != 4)
                throw new InvalidOperationException("Gaze reticle requires the complete Prefab-authored visual hierarchy.");
            if (_authoredParent == null)
                _authoredParent = _reticleRoot.parent;
            var canvas = _reticleRoot.GetComponent<Canvas>();
            if (canvas == null)
                throw new InvalidOperationException("Gaze reticle root requires a World-Space Canvas.");
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _camera;
            canvas.sortingOrder = short.MaxValue;
            canvas.overrideSorting = true;
            _progressRing.type = Image.Type.Filled;
            _progressRing.fillMethod = Image.FillMethod.Radial360;
            _progressRing.fillOrigin = 2;
            _progressRing.fillClockwise = true;
            _progressRing.fillAmount = 0f;
            SetReticleState(false, false, false, 0f);
        }

        static bool IsPositiveFinite(float value)
            => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        static void ValidateNonInteractiveImage(Image image, string role)
        {
            if (image.sprite == null)
                throw new InvalidOperationException($"Gaze reticle {role} requires an authored sprite.");
            if (image.raycastTarget)
                throw new InvalidOperationException($"Gaze reticle {role} must not consume input raycasts.");
        }

    }
}
