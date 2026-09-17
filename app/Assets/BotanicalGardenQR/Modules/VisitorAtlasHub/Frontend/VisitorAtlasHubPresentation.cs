using System;
using System.Collections;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BotanicalGardenQR.VisitorAtlasHub.Frontend
{
    /// <summary>
    /// Owns one authored horizontal map surface at a fixed session-world pose
    /// plus a transient book/scroll entry pair. The entry pair snapshots a
    /// fresh viewer-relative pose whenever it is presented; it never moves the
    /// committed map surface and does not follow the viewer continuously.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisitorAtlasHubPresentation : MonoBehaviour, IVisitorAtlasHubPresentation
    {
        [Header("Authored roots")]
        [SerializeField] GameObject _visualRoot;
        [SerializeField] GameObject _choiceRoot;
        [SerializeField] GameObject _mapSurfaceRoot;
        [SerializeField] Transform _mapContentRoot;
        [SerializeField] Transform _bookRoot;
        [SerializeField] Transform _bookActivationPivot;
        [SerializeField] Transform _mapEntryRoot;
        [SerializeField] Transform _hideRoot;
        [SerializeField] AtlasHubPalmCandidateSource _palmCandidateSource;
        [SerializeField] VisitorAtlasHubPointableRelay _bookTarget;
        [SerializeField] VisitorAtlasHubPointableRelay _mapTarget;
        [SerializeField] TMP_Text _mapLabel;
        [SerializeField] string _openMapLabel = "打开地图";
        [SerializeField] string _closeMapLabel = "关闭地图";
        [SerializeField] Button _hideButton;
        [SerializeField] Button _helpButton;
        [SerializeField] Button _discoveryButton;
        [SerializeField] Button _recallFairyButton;
        bool _discoveryAvailable;

        [Header("One-shot Stage pose")]
        [SerializeField] float _sessionRootWorldHeight = 0.15f;
        [SerializeField, Min(0.25f)] float _poseAttemptTimeoutSeconds = 3f;
        [SerializeField, Min(0f)] float _retryDelaySeconds = 0.5f;

        [Header("Palm timing")]
        [SerializeField, Min(0.1f)] float _palmHoldSeconds = 0.45f;
        [SerializeField, Min(0f)] float _trackingGraceSeconds = 0.20f;
        [SerializeField, Min(0.05f)] float _gestureReleaseSeconds = 0.18f;

        [Header("Viewer-relative entry choices")]
        [SerializeField, Min(0.2f)] float _entryForwardDistance = 0.68f;
        [SerializeField] float _entryVerticalOffset = -0.48f;
        [SerializeField] Vector3 _entryHideLocalPosition = new Vector3(0f, -0.38f, 0.02f);

        [Header("Bounded presentation")]
        [SerializeField, Min(0.05f)] float _revealSeconds = 0.60f;
        [SerializeField, Min(0.05f)] float _bookOpenSeconds = 0.70f;
        [SerializeField, Min(0.1f)] float _browseConfirmationTimeoutSeconds = 0.75f;
        [SerializeField] Vector3 _bookOpenEulerAngles = new Vector3(-12f, -18f, 8f);
        [SerializeField, Range(0.7f, 1f)] float _revealStartScale = 0.92f;
        [SerializeField, Range(0.75f, 1f)] float _rejectedBookScale = 0.92f;

        [Header("Map asset")]
        [SerializeField] string _streamingAssetsPath =
            "VisitorAtlasHub/basement_map_8x8_6points.glb";

        Transform _viewer;
        Vector3 _visualRestScale;
        Vector3 _choiceRestScale;
        Vector3 _mapSurfaceRestScale;
        Vector3 _bookRestScale;
        Quaternion _bookClosedRotation;
        Quaternion _bookOpenRotation;
        Coroutine _revealMotion;
        Coroutine _bookMotion;
        Coroutine _rejectMotion;
        Coroutine _hideMotion;
        bool _poseCommitted;
        bool _configured;
        bool _disposed;
        bool _entryLayerVisible = true;
        bool _toolsVisible, _mapVisible;
        IFrontendGazeSurfaceRegistration _hideGaze;
        IFrontendGazeSurfaceRegistry _gazeInput;
        IDisposable _handInputSuspension;

        public TMP_FontAsset MapLabelFont => _mapLabel.font;
        public Transform MapContentRoot => _mapContentRoot;
        public string StreamingAssetsPath => _streamingAssetsPath;
        public VisitorAtlasHubConfiguration Configuration => new VisitorAtlasHubConfiguration(
            _sessionRootWorldHeight,
            _poseAttemptTimeoutSeconds,
            _retryDelaySeconds,
            _palmHoldSeconds,
            _trackingGraceSeconds,
            _gestureReleaseSeconds,
            _revealSeconds,
            _bookOpenSeconds,
            _browseConfirmationTimeoutSeconds);

        public event Action BookSelected;
        public event Action MapSelected;
        public event Action HideRequested;
        public event Action HelpRequested;
        public event Action DiscoveryRequested;
        public event Action RecallFairyRequested;
        public void PresentRecallResult(bool accepted)
        {
            _recallFairyButton.GetComponentInChildren<TMP_Text>(true).text = accepted ? "精灵正在回来" : "换个位置重试";
        }
        public void SetDiscoveryAvailable(bool available)
        {
            if (_disposed) return;
            _discoveryAvailable = available;
            if (_discoveryButton != null) _discoveryButton.interactable = available && _helpButton.interactable;
            InvalidateGaze();
        }
        public event Action<int> BookOpeningCompleted;

        public void BindGazeInput(IFrontendGazeSurfaceRegistry registry)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisitorAtlasHubPresentation));
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (_hideGaze != null) throw new InvalidOperationException("Atlas gaze input is already bound.");
            ValidateBindings();
            _hideGaze = registry.RegisterGazeSurface(_hideButton.GetComponentInParent<Canvas>(true).transform, 300, "AtlasClose");
            _gazeInput = registry;
        }

        public void Configure(Transform viewer, Transform interactionRigRoot)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisitorAtlasHubPresentation));
            if (_configured) throw new InvalidOperationException("Visitor Atlas Hub presentation is already configured.");
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            if (interactionRigRoot == null) throw new ArgumentNullException(nameof(interactionRigRoot));
            ValidateBindings();
            if (_hideGaze == null) throw new InvalidOperationException("Atlas requires its panel gaze input.");
            _ = Configuration;

            _visualRestScale = _visualRoot.transform.localScale;
            _choiceRestScale = _choiceRoot.transform.localScale;
            _mapSurfaceRestScale = _mapSurfaceRoot.transform.localScale;
            _bookRestScale = _bookRoot.localScale;
            _bookClosedRotation = _bookActivationPivot.localRotation;
            _bookOpenRotation = _bookClosedRotation * Quaternion.Euler(_bookOpenEulerAngles);
            _mapContentRoot.localRotation = Quaternion.identity;
            _mapContentRoot.localScale = Vector3.one;

            _palmCandidateSource.Configure(interactionRigRoot);
            _bookTarget.Configure();
            _mapTarget.Configure();
            _bookTarget.Selected += HandleBookSelected;
            _mapTarget.Selected += HandleMapSelected;
            _bookTarget.HandEngagementChanged += UpdateHandEngagement;
            _mapTarget.HandEngagementChanged += UpdateHandEngagement;
            _hideButton.onClick.AddListener(HandleHideSelected);
            _helpButton.onClick.AddListener(HandleHelpSelected);
            _discoveryButton.onClick.AddListener(HandleDiscoverySelected);
            _recallFairyButton.onClick.AddListener(HandleRecallFairySelected);
            _configured = true;
            _entryLayerVisible = true;
            RefreshVisibility();
            SetInteractionEnabled(false, false, false);
            _visualRoot.SetActive(false);
        }

        public VisitorAtlasHubPalmSample SamplePalmCandidate()
        {
            if (_disposed || !_configured || _viewer == null)
                return new VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage.NoReliableHand, false);
            return _palmCandidateSource.Sample(_viewer.position.y);
        }

        public void CommitSessionRoot(Pose sessionRootPose)
        {
            RequireConfigured();
            if (_poseCommitted)
                throw new InvalidOperationException("Visitor Atlas Hub session pose can be committed only once.");
            if (!IsFinite(sessionRootPose.position) || !IsFinite(sessionRootPose.rotation))
                throw new ArgumentException("Visitor Atlas Hub session pose must be finite.", nameof(sessionRootPose));
            transform.SetPositionAndRotation(sessionRootPose.position, sessionRootPose.rotation);
            _poseCommitted = true;
        }

        public void RefreshEntryChoicePose()
        {
            RequireConfigured();
            if (!_poseCommitted)
                throw new InvalidOperationException(
                    "Visitor Atlas Hub entry choices cannot be placed before the map pose is committed.");
            var pose = CalculateEntryChoicePose(
                new Pose(_viewer.position, _viewer.rotation),
                _entryForwardDistance,
                _entryVerticalOffset,
                transform.forward);
            _choiceRoot.transform.SetPositionAndRotation(pose.position, pose.rotation);
            PlaceHideAtEntry();
        }

        public void SetVisible(bool visible)
        {
            if (_disposed) return;
            RequireConfigured();
            if (visible && !_poseCommitted) throw new InvalidOperationException("Atlas session pose is not ready.");
            if (visible && !_toolsVisible) _recallFairyButton.GetComponentInChildren<TMP_Text>(true).text = "召回精灵";
            _toolsVisible = visible;
            if (!visible)
            {
                StopAllPresentationMotion();
                SetInteractionEnabled(false, false, false);
                ResetBookImmediate();
                _entryLayerVisible = true;
                InvalidateGaze();
            }
            RefreshVisibility();
        }

        public void SetMapDisplay(bool requested, bool visible)
        {
            if (_disposed) return;
            RequireConfigured();
            _mapVisible = visible && _poseCommitted;
            _mapLabel.text = requested ? _closeMapLabel : _openMapLabel;
            RefreshVisibility();
        }

        void RefreshVisibility()
        {
            var choices = _toolsVisible && _entryLayerVisible;
            _visualRoot.SetActive(_toolsVisible || _mapVisible);
            _choiceRoot.SetActive(choices);
            _hideRoot.gameObject.SetActive(choices);
            _mapSurfaceRoot.SetActive(_mapVisible);
            if (choices) PlaceHideAtEntry();
        }

        public void SetEntryLayerVisible(bool visible)
        {
            if (_disposed) return;
            RequireConfigured();
            _entryLayerVisible = visible;
            InvalidateGaze();
            RefreshVisibility();
        }

        public void SetInteractionEnabled(
            bool bookEnabled,
            bool mapEnabled,
            bool hideEnabled)
        {
            if (_disposed) return;
            if (!_configured) return;
            _bookTarget.SetArmed(bookEnabled && _hideMotion == null && _bookTarget.gameObject.activeInHierarchy);
            _mapTarget.SetArmed(mapEnabled && _hideMotion == null && _mapTarget.gameObject.activeInHierarchy);
            var hide = hideEnabled && _hideMotion == null && _hideButton.gameObject.activeInHierarchy;
            if (_hideButton.interactable != hide) InvalidateGaze();
            _hideButton.interactable = hide;
            _helpButton.interactable = hide;
            _discoveryButton.interactable = hide && _discoveryAvailable;
            _recallFairyButton.interactable = hide;
        }

        public void PlayReveal()
        {
            RequireConfigured();
            StopRevealMotion();
            if (!_visualRoot.activeInHierarchy) return;
            _revealMotion = StartCoroutine(AnimateReveal());
        }

        public void BeginBookOpening(int generation)
        {
            RequireConfigured();
            StopBookMotion();
            _bookMotion = StartCoroutine(AnimateBookOpen(generation));
        }

        public void SetBookOpenLocked()
        {
            if (_disposed || !_configured) return;
            StopBookMotion();
            _bookActivationPivot.localRotation = _bookOpenRotation;
        }

        public void ResetBook()
        {
            if (_disposed || !_configured) return;
            StopBookMotion();
            ResetBookImmediate();
        }

        public void RejectBookSelection()
        {
            if (_disposed || !_configured || !_visualRoot.activeInHierarchy) return;
            if (_rejectMotion != null) StopCoroutine(_rejectMotion);
            _rejectMotion = StartCoroutine(AnimateRejectedBook());
        }

        public void Dispose()
        {
            if (_disposed) return;
            StopAllPresentationMotion();
            if (_configured)
            {
                _bookTarget.Selected -= HandleBookSelected;
                _mapTarget.Selected -= HandleMapSelected;
                _bookTarget.HandEngagementChanged -= UpdateHandEngagement;
                _mapTarget.HandEngagementChanged -= UpdateHandEngagement;
                _hideButton.onClick.RemoveListener(HandleHideSelected);
                _helpButton.onClick.RemoveListener(HandleHelpSelected);
                _discoveryButton.onClick.RemoveListener(HandleDiscoverySelected);
                _recallFairyButton.onClick.RemoveListener(HandleRecallFairySelected);
                _bookTarget.Dispose();
                _mapTarget.Dispose();
                _palmCandidateSource.Dispose();
                _visualRoot.SetActive(false);
            }
            _configured = false;
            _hideGaze?.Dispose();
            _hideGaze = null;
            _handInputSuspension?.Dispose();
            _handInputSuspension = null;
            _gazeInput = null;
            _disposed = true;
            _viewer = null;
            BookSelected = null;
            MapSelected = null;
            HideRequested = null;
            HelpRequested = null;
            DiscoveryRequested = null;
            RecallFairyRequested = null;
            BookOpeningCompleted = null;
        }

        void OnDestroy() => Dispose();

        IEnumerator AnimateReveal()
        {
            var elapsed = 0f;
            _choiceRoot.transform.localScale = _choiceRestScale * _revealStartScale;
            while (elapsed < _revealSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Smooth01(elapsed / _revealSeconds);
                _choiceRoot.transform.localScale = Vector3.LerpUnclamped(
                    _choiceRestScale * _revealStartScale,
                    _choiceRestScale,
                    t);
                yield return null;
            }
            _choiceRoot.transform.localScale = _choiceRestScale;
            _revealMotion = null;
        }

        IEnumerator AnimateBookOpen(int generation)
        {
            var start = _bookActivationPivot.localRotation;
            var elapsed = 0f;
            while (elapsed < _bookOpenSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _bookActivationPivot.localRotation = Quaternion.SlerpUnclamped(
                    start,
                    _bookOpenRotation,
                    Smooth01(elapsed / _bookOpenSeconds));
                yield return null;
            }
            _bookActivationPivot.localRotation = _bookOpenRotation;
            _bookMotion = null;
            BookOpeningCompleted?.Invoke(generation);
        }

        IEnumerator AnimateRejectedBook()
        {
            var elapsed = 0f;
            const float duration = 0.16f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var wave = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                _bookRoot.localScale = _bookRestScale * Mathf.Lerp(1f, _rejectedBookScale, wave);
                yield return null;
            }
            _bookRoot.localScale = _bookRestScale;
            _rejectMotion = null;
        }

        void HandleBookSelected() => BookSelected?.Invoke();
        void HandleMapSelected() => MapSelected?.Invoke();
        void HandleHideSelected()
        {
            if (_disposed || _hideMotion != null || !_hideButton.IsActive() || !_hideButton.IsInteractable()) return;
            InvalidateGaze();
            StopRevealMotion();
            SetInteractionEnabled(false, false, false);
            _hideMotion = StartCoroutine(AnimateExplicitHide());
        }

        IEnumerator AnimateExplicitHide()
        {
            var scale = _choiceRoot.transform.localScale;
            var elapsed = 0f;
            while (elapsed < .18f)
            {
                elapsed += Time.unscaledDeltaTime;
                _choiceRoot.transform.localScale = scale * Mathf.Lerp(1f, .18f, Smooth01(elapsed / .18f));
                yield return null;
            }
            _hideMotion = null;
            HideRequested?.Invoke();
        }
        void HandleRecallFairySelected()
        {
            if (!_disposed && _recallFairyButton.IsActive() && _recallFairyButton.IsInteractable())
            { InvalidateGaze(); RecallFairyRequested?.Invoke(); }
        }
        void HandleDiscoverySelected()
        {
            if (!_disposed && _discoveryAvailable && _discoveryButton.IsActive() && _discoveryButton.IsInteractable())
            { InvalidateGaze(); DiscoveryRequested?.Invoke(); }
        }
        void HandleHelpSelected() { if (!_disposed && _helpButton.IsActive() && _helpButton.IsInteractable()) { InvalidateGaze(); HelpRequested?.Invoke(); } }
        void InvalidateGaze() { _hideGaze?.Invalidate(); }
        void OnDisable() { InvalidateGaze(); if (_configured) StopAllPresentationMotion(); }

        void UpdateHandEngagement()
        {
            if (_disposed || _gazeInput == null) return;
            if (_bookTarget.IsHandEngaged || _mapTarget.IsHandEngaged)
            {
                if (_handInputSuspension == null) _handInputSuspension = _gazeInput.SuspendPanelInput();
            }
            else { _handInputSuspension?.Dispose(); _handInputSuspension = null; }
        }

        void StopAllPresentationMotion()
        {
            if (_hideMotion != null) { StopCoroutine(_hideMotion); _hideMotion = null; }
            StopRevealMotion();
            StopBookMotion();
            if (_rejectMotion != null)
            {
                StopCoroutine(_rejectMotion);
                _rejectMotion = null;
            }
            if (_bookRoot != null && _bookRestScale != Vector3.zero) _bookRoot.localScale = _bookRestScale;
            if (_choiceRoot != null && _choiceRestScale != Vector3.zero)
                _choiceRoot.transform.localScale = _choiceRestScale;
            if (_mapSurfaceRoot != null && _mapSurfaceRestScale != Vector3.zero)
                _mapSurfaceRoot.transform.localScale = _mapSurfaceRestScale;
        }

        void StopRevealMotion()
        {
            if (_revealMotion == null) return;
            StopCoroutine(_revealMotion);
            _revealMotion = null;
        }

        void StopBookMotion()
        {
            if (_bookMotion == null) return;
            StopCoroutine(_bookMotion);
            _bookMotion = null;
        }

        void ResetBookImmediate()
        {
            if (_bookActivationPivot != null) _bookActivationPivot.localRotation = _bookClosedRotation;
            if (_bookRoot != null && _bookRestScale != Vector3.zero) _bookRoot.localScale = _bookRestScale;
        }

        void PlaceHideAtEntry()
        {
            if (_hideRoot == null || _choiceRoot == null) return;
            var choice = _choiceRoot.transform;
            _hideRoot.SetPositionAndRotation(
                choice.TransformPoint(_entryHideLocalPosition),
                choice.rotation);
        }

        void ValidateBindings()
        {
            if (_visualRoot == null || _choiceRoot == null || _mapSurfaceRoot == null || _mapContentRoot == null ||
                _bookRoot == null || _bookActivationPivot == null || _mapEntryRoot == null || _hideRoot == null ||
                _palmCandidateSource == null || _bookTarget == null || _mapTarget == null ||
                _mapLabel == null || _hideButton == null || _helpButton == null || _discoveryButton == null || _recallFairyButton == null ||
                ReferenceEquals(_bookTarget, _mapTarget))
                throw new InvalidOperationException("Visitor Atlas Hub prefab bindings are incomplete.");
            if (!_visualRoot.transform.IsChildOf(transform) || !_choiceRoot.transform.IsChildOf(_visualRoot.transform) ||
                !_mapSurfaceRoot.transform.IsChildOf(_visualRoot.transform) || !_mapContentRoot.IsChildOf(_mapSurfaceRoot.transform) ||
                !_bookRoot.IsChildOf(_choiceRoot.transform) || !_bookActivationPivot.IsChildOf(_bookRoot) ||
                !_mapEntryRoot.IsChildOf(_choiceRoot.transform) ||
                !_hideRoot.IsChildOf(_visualRoot.transform) || !_hideButton.transform.IsChildOf(_hideRoot) ||
                !_bookTarget.transform.IsChildOf(_visualRoot.transform) ||
                !_mapTarget.transform.IsChildOf(_visualRoot.transform) ||
                !_hideButton.transform.IsChildOf(_visualRoot.transform))
                throw new InvalidOperationException("Visitor Atlas Hub authored roots do not share one visual hierarchy.");
            if (!WorldSurfacePlacement.HasPositiveScaleChain(transform) ||
                !IsFinite(_bookOpenEulerAngles) || !IsPositiveFinite(_entryForwardDistance) ||
                !IsFinite(_entryVerticalOffset) || !IsFinite(_entryHideLocalPosition) ||
                !IsValidStreamingAssetsPath(_streamingAssetsPath))
                throw new InvalidOperationException("Visitor Atlas Hub authored geometry or map path is invalid.");
        }

        internal static Pose CalculateEntryChoicePose(
            Pose viewerPose,
            float forwardDistance,
            float verticalOffset,
            Vector3 fallbackForward)
        {
            if (!IsFinite(viewerPose.position) || !IsFinite(viewerPose.rotation) ||
                !IsPositiveFinite(forwardDistance) || !IsFinite(verticalOffset))
                throw new ArgumentException("Atlas Hub entry placement inputs must be finite and in range.");

            var forward = Vector3.ProjectOnPlane(viewerPose.rotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(fallbackForward, Vector3.up);
            if (!IsFinite(forward) || forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();
            return new Pose(
                viewerPose.position + forward * forwardDistance + Vector3.up * verticalOffset,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        void RequireConfigured()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisitorAtlasHubPresentation));
            if (!_configured) throw new InvalidOperationException("Visitor Atlas Hub presentation is not configured.");
        }

        static float Smooth01(float value)
        {
            var t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        static bool IsValidStreamingAssetsPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || System.IO.Path.IsPathRooted(path)) return false;
            var normalized = path.Trim().Replace('\\', '/');
            var decoded = Uri.UnescapeDataString(normalized).Replace('\\', '/');
            return normalized.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) &&
                   !System.IO.Path.IsPathRooted(decoded) &&
                   decoded.IndexOfAny(new[] { '?', '#', '\0' }) < 0 &&
                   Array.TrueForAll(decoded.Split('/'), segment =>
                       !string.IsNullOrWhiteSpace(segment) && segment != "." && segment != "..");
        }

        static bool IsPositiveFinite(Vector3 value) =>
            value.x > 0f && value.y > 0f && value.z > 0f && IsFinite(value);
        static bool IsPositiveFinite(float value) => value > 0f && IsFinite(value);
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        static bool IsFinite(Quaternion value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }
}
