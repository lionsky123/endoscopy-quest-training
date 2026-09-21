using System;
using System.Collections.Generic;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Panorama.Frontend
{
    internal sealed class PanoramaSpatialWorld : IDisposable
    {
        static readonly Vector3 ExitBubblePosition = new Vector3(-0.24f, -0.08f, 0.50f);
        static readonly Vector3 EnvironmentBubblePosition = new Vector3(0.24f, 0.04f, 0.55f);
        static readonly Vector3 ImageBubblePosition = new Vector3(0f, 0.16f, 0.55f);

        readonly Transform _viewer;
        readonly Action _requestExit;
        readonly GameObject _root;
        readonly PanoramaSpatialBubble _exitBubble;
        readonly PanoramaSpatialBubble _environmentBubble;
        readonly PanoramaSpatialBubble _imageBubble;
        readonly PanoramaEnvironmentMomentController _environmentMoments;
        readonly PanoramaSpatialWorldTicker _ticker;
        readonly GameObject _tutorialHintRoot;
        readonly TMP_Text _tutorialHint;
        readonly ClinicalGuidedObservationControls _clinicalControls;

        bool _placed;
        bool _visible;
        bool _controlsVisible;
        bool _tutorialHintRequested;
        bool _disposed;

        public PanoramaSpatialWorld(
            Transform runtimeRoot,
            Transform viewer,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            TMP_FontAsset sharedFont,
            Action requestExit,
            bool hasImageRing,
            Action requestImageRing,
            IReadOnlyList<PanoramaEnvironmentMomentDefinition> environmentMoments,
            IPanoramaEnvironmentMomentRuntimeFactory environmentMomentFactory = null,
            bool clinicalLearning = false,
            Texture clinicalTexture = null,
            IReadOnlyList<Texture> teachingComparisons = null,
            Action requestClinicalCompletion = null,
            float panoramaYaw = 0,
            ClinicalObservationProgress clinicalProgress = null, Func<bool> clinicalReadOnly = null,
            Action closeClinicalReview = null)
        {
            if (runtimeRoot == null) throw new ArgumentNullException(nameof(runtimeRoot));
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            if (gazeSurfaces == null) throw new ArgumentNullException(nameof(gazeSurfaces));
            if (sharedFont == null) throw new ArgumentNullException(nameof(sharedFont));
            _requestExit = requestExit ?? throw new ArgumentNullException(nameof(requestExit));

            _root = new GameObject("PanoramaSpatialRoot");
            _root.transform.SetParent(runtimeRoot, false);
            _root.SetActive(false);

            if (clinicalLearning)
            {
                _clinicalControls = new ClinicalGuidedObservationControls(_root.transform, sharedFont, gazeSurfaces,
                    requestClinicalCompletion ?? HandleExitSelected, clinicalTexture, viewer, panoramaYaw,
                    clinicalProgress, clinicalReadOnly, closeClinicalReview ?? HandleExitSelected);
                (_tutorialHintRoot, _tutorialHint) = CreateTutorialHint(_root.transform, sharedFont);
                _ticker = _root.AddComponent<PanoramaSpatialWorldTicker>();
                _ticker.Bind(this);
                return;
            }

            _exitBubble = new PanoramaSpatialBubble(
                _root.transform,
                gazeSurfaces,
                sharedFont,
                "PanoramaExitBubble",
                ExitBubblePosition,
                PanoramaSpatialBubbleIcon.Exit,
                new Color(0.56f, 0.91f, 1f, 1f),
                0.35f,
                "返回观察",
                HandleExitSelected);
            _environmentMoments = new PanoramaEnvironmentMomentController(
                environmentMoments ?? throw new ArgumentNullException(nameof(environmentMoments)),
                _root.transform,
                environmentMomentFactory);
            if (_environmentMoments.HasMoments)
                _environmentBubble = new PanoramaSpatialBubble(
                    _root.transform,
                    gazeSurfaces,
                    sharedFont,
                    "PanoramaEnvironmentMomentBubble",
                    EnvironmentBubblePosition,
                    PanoramaSpatialBubbleIcon.EnvironmentMoment,
                    new Color(0.48f, 0.88f, 0.96f, 1f),
                    2.1f,
                    "环境瞬间",
                    CycleEnvironmentMoment);
            if (hasImageRing)
            {
                if (requestImageRing == null)
                    throw new ArgumentNullException(nameof(requestImageRing));
                _imageBubble = new PanoramaSpatialBubble(
                    _root.transform,
                    gazeSurfaces,
                    sharedFont,
                    "PanoramaImageRingBubble",
                    ImageBubblePosition,
                    PanoramaSpatialBubbleIcon.Images,
                    new Color(0.46f, 0.93f, 0.82f, 1f),
                    1.25f,
                    "图像环廊",
                    requestImageRing);
            }
            (_tutorialHintRoot, _tutorialHint) = CreateTutorialHint(_root.transform, sharedFont);
            _ticker = _root.AddComponent<PanoramaSpatialWorldTicker>();
            _ticker.Bind(this);
        }

        public void SetVisible(bool visible)
        {
            if (_disposed || _visible == visible) return;
            _visible = visible;
            _clinicalControls?.InvalidateInput();

            if (!visible)
            {
                ResetEnvironmentMoment();
                _root.SetActive(false);
                _controlsVisible = false;
                _placed = false;
                ApplyTutorialHintVisibility();
                return;
            }

            PlaceAtEntryPose();
            ResetEnvironmentMoment();
            _controlsVisible = true;
            _root.SetActive(true);
            ApplyTutorialHintVisibility();
            Tick();
        }

        public void SetControlsVisible(bool visible)
        {
            if (_disposed || !_visible || _controlsVisible == visible) return;
            _controlsVisible = visible;
            _clinicalControls?.InvalidateInput();
            ResetEnvironmentMoment();
            _root.SetActive(visible);
            ApplyTutorialHintVisibility();
            if (visible) Tick();
        }

        public void SetTutorialHint(string copy, bool visible)
        {
            if (_disposed) return;
            if (visible && string.IsNullOrWhiteSpace(copy))
                throw new ArgumentException("Visible Panorama tutorial copy is required.", nameof(copy));
            if (!string.IsNullOrWhiteSpace(copy)) _tutorialHint.text = copy.Trim();
            _tutorialHintRequested = visible;
            ApplyTutorialHintVisibility();
        }

        internal bool TutorialHintVisible =>
            !_disposed && _tutorialHintRoot != null && _tutorialHintRoot.activeSelf;
        internal string TutorialHintText => _tutorialHint != null ? _tutorialHint.text : string.Empty;
        internal bool ExitFocusLabelVisible => _exitBubble != null && _exitBubble.IsFocusLabelVisible;
        internal string ExitFocusLabelText => _exitBubble != null ? _exitBubble.FocusLabelText : string.Empty;

        public void Tick()
        {
            if (!_visible || !_controlsVisible || _disposed) return;
            var time = Time.unscaledTime;
            _clinicalControls?.Tick(Time.unscaledDeltaTime);
            _exitBubble?.Tick(time);
            _environmentBubble?.Tick(time);
            _imageBubble?.Tick(time);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _visible = false;
            _ticker?.Unbind();
            _environmentMoments?.Dispose();
            _clinicalControls?.Dispose();
            _exitBubble?.Dispose();
            _environmentBubble?.Dispose();
            _imageBubble?.Dispose();
            Destroy(_root);
        }

        void PlaceAtEntryPose()
        {
            if (_placed || _viewer == null || _root == null) return;

            var horizontalForward = Vector3.ProjectOnPlane(_viewer.forward, Vector3.up);
            if (horizontalForward.sqrMagnitude < 0.0001f)
                horizontalForward = Vector3.forward;
            horizontalForward.Normalize();

            _root.transform.SetPositionAndRotation(
                _viewer.position,
                Quaternion.LookRotation(horizontalForward, Vector3.up));
            _placed = true;
            _clinicalControls?.Reset();
        }

        void HandleExitSelected()
        {
            if (!_visible || _disposed) return;
            _requestExit();
        }

        void CycleEnvironmentMoment()
        {
            if (!_visible || _disposed) return;
            try
            {
                var active = _environmentMoments.Cycle();
                _environmentBubble?.SetEnvironmentMoment(active != null, active?.Accent ?? default);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ResetEnvironmentMoment();
            }
        }

        void ResetEnvironmentMoment()
        {
            try { _environmentMoments?.Reset(); }
            catch (Exception exception) { Debug.LogException(exception); }
            _environmentBubble?.SetEnvironmentMoment(false, default);
        }

        void ApplyTutorialHintVisibility()
        {
            if (_tutorialHintRoot != null)
                _tutorialHintRoot.SetActive(
                    _clinicalControls == null && _tutorialHintRequested && _visible && _controlsVisible && !_disposed);
        }

        static (GameObject Root, TMP_Text Label) CreateTutorialHint(
            Transform parent,
            TMP_FontAsset sharedFont)
        {
            var root = new GameObject(
                "PanoramaTutorialHint",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(820f, 96f);
            rect.localPosition = new Vector3(0f, -0.34f, 1.08f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * 0.001f;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 510;
            var group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var background = CreateRect(rect, "Background", rect.sizeDelta).gameObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.07f, 0.07f, 0.86f);
            background.raycastTarget = false;
            var label = CreateRect(rect, "Copy", rect.sizeDelta - new Vector2(36f, 14f))
                .gameObject.AddComponent<TextMeshProUGUI>();
            label.font = sharedFont;
            label.fontSize = 24f;
            label.color = new Color(0.92f, 1f, 0.97f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = string.Empty;
            root.SetActive(false);
            return (root, label);
        }

        static RectTransform CreateRect(Transform parent, string name, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }

    internal sealed class PanoramaSpatialWorldTicker : MonoBehaviour
    {
        PanoramaSpatialWorld _world;

        public void Bind(PanoramaSpatialWorld world) => _world = world;

        public void Unbind() => _world = null;

        void LateUpdate() => _world?.Tick();
    }
}
