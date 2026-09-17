using System;
using BotanicalGardenQR.Experience.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    [Serializable]
    public sealed class ShellSlotReferences
    {
        [SerializeField] RectTransform _shellRoot;
        [SerializeField] RectTransform _headerSlot;
        [SerializeField] RectTransform _actionSlot;
        [SerializeField] RectTransform _videoStageRoot;
        [SerializeField] RectTransform _videoControlRoot;
        [SerializeField] RectTransform _modelStageRoot;
        [SerializeField] RectTransform _modelControlRoot;
        [SerializeField] RectTransform _narrationDockSlot;
        [SerializeField] RectTransform _panoramaExitSlot;
        [SerializeField] RectTransform _featureFocusExitSlot;
        [SerializeField] RectTransform _statusSlot;
        [SerializeField] RectTransform _closeSlot;
        [SerializeField] RectTransform _overlaySlot;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _subtitle;
        [SerializeField] TMP_Text _status;
        [SerializeField] Text _videoModeTitle;
        [SerializeField] Text _modelModeTitle;
        [SerializeField] CanvasGroup _shellCanvasGroup;
        [SerializeField] ShellFlowActionTarget[] _flowTargets = Array.Empty<ShellFlowActionTarget>();

        public RectTransform ShellRoot => _shellRoot;
        public RectTransform HeaderSlot => _headerSlot;
        public RectTransform ActionSlot => _actionSlot;
        public RectTransform NarrationDockSlot => _narrationDockSlot;
        public RectTransform PanoramaExitSlot => _panoramaExitSlot;
        public RectTransform FeatureFocusExitSlot => _featureFocusExitSlot;
        public RectTransform StatusSlot => _statusSlot;
        public RectTransform CloseSlot => _closeSlot;
        public RectTransform OverlaySlot => _overlaySlot;
        public TMP_Text Title => _title;
        public TMP_Text Subtitle => _subtitle;
        public TMP_Text Status => _status;
        public Text VideoModeTitle => _videoModeTitle;
        public Text ModelModeTitle => _modelModeTitle;
        public CanvasGroup ShellCanvasGroup => _shellCanvasGroup;
        public Vector2 NarrationDockSize => SizeOf(_narrationDockSlot);
        public ShellFlowActionTarget[] FlowTargets => _flowTargets ?? Array.Empty<ShellFlowActionTarget>();

        public bool TryGetFeatureSurface(
            FeaturePageId feature,
            out Transform stageRoot,
            out Vector3 stageSize,
            out Transform controlRoot,
            out Vector2 controlSize)
        {
            switch (feature)
            {
                case FeaturePageId.Video:
                    stageRoot = _videoStageRoot;
                    stageSize = StageSizeOf(_videoStageRoot);
                    controlRoot = _videoControlRoot;
                    controlSize = SizeOf(_videoControlRoot);
                    return true;
                case FeaturePageId.Model:
                    stageRoot = _modelStageRoot;
                    stageSize = StageSizeOf(_modelStageRoot);
                    controlRoot = _modelControlRoot;
                    controlSize = SizeOf(_modelControlRoot);
                    return true;
                default:
                    stageRoot = null;
                    stageSize = default;
                    controlRoot = null;
                    controlSize = default;
                    return false;
            }
        }

        public void Validate()
        {
            if (_shellRoot == null || _headerSlot == null || _actionSlot == null ||
                _videoStageRoot == null || _videoControlRoot == null ||
                _modelStageRoot == null || _modelControlRoot == null ||
                _narrationDockSlot == null || _panoramaExitSlot == null ||
                _featureFocusExitSlot == null || _statusSlot == null ||
                _closeSlot == null || _overlaySlot == null)
                throw new InvalidOperationException("GlobalFrontendShell requires its main, feature, narration, and immersive slots.");
            if (_title == null || _subtitle == null || _status == null ||
                _videoModeTitle == null || _modelModeTitle == null)
                throw new InvalidOperationException(
                    "GlobalFrontendShell requires title, subtitle, status, and feature identity text references.");
            if (!IsPositiveFinite(StageSizeOf(_videoStageRoot)) || !IsPositiveFinite(SizeOf(_videoControlRoot)) ||
                !IsPositiveFinite(StageSizeOf(_modelStageRoot)) || !IsPositiveFinite(SizeOf(_modelControlRoot)) ||
                !IsPositiveFinite(SizeOf(_narrationDockSlot)))
                throw new InvalidOperationException("GlobalFrontendShell surface slot sizes must be positive and finite.");
            if (_panoramaExitSlot == _shellRoot || _panoramaExitSlot.IsChildOf(_shellRoot))
                throw new InvalidOperationException("The immersive Panorama exit must remain outside the hidden shell root.");
            if (_featureFocusExitSlot == _shellRoot || _featureFocusExitSlot.IsChildOf(_shellRoot))
                throw new InvalidOperationException(
                    "The feature focus exit must remain outside the hidden shell root.");
            if (_statusSlot == _headerSlot || _statusSlot.IsChildOf(_headerSlot))
                throw new InvalidOperationException(
                    "The status slot must remain outside the hidden Main header slot so feature status remains visible.");
            if (_statusSlot != _shellRoot && !_statusSlot.IsChildOf(_shellRoot))
                throw new InvalidOperationException(
                    "The status slot must remain inside the visible ShellRoot surface.");

            var hasModel = false;
            var hasVideo = false;
            var hasPanorama = false;
            var hasBackToMain = false;
            var hasClose = false;
            var featurePageActionCount = 0;
            var featurePageActionExitCount = 0;
            if (_flowTargets == null || _flowTargets.Length == 0)
                throw new InvalidOperationException("GlobalFrontendShell requires semantic flow targets.");
            foreach (var target in _flowTargets)
            {
                if (target == null)
                    throw new InvalidOperationException("GlobalFrontendShell flow targets cannot contain null entries.");
                switch (target.Action)
                {
                    case ShellFlowAction.EnterFeature:
                        if (target.Feature != FeaturePageId.Model &&
                            target.Feature != FeaturePageId.Video &&
                            target.Feature != FeaturePageId.Panorama)
                            throw new InvalidOperationException("GlobalFrontendShell contains an unsupported feature target.");
                        hasModel |= target.Feature == FeaturePageId.Model;
                        hasVideo |= target.Feature == FeaturePageId.Video;
                        hasPanorama |= target.Feature == FeaturePageId.Panorama;
                        break;
                    case ShellFlowAction.BackToMain:
                        hasBackToMain = true;
                        break;
                    case ShellFlowAction.Close:
                        hasClose = true;
                        break;
                    case ShellFlowAction.FeaturePageAction:
                        if (target.Feature == default || !target.HasFeaturePagePresentation)
                            throw new InvalidOperationException(
                                "GlobalFrontendShell feature-page action requires a feature, Button, and exactly one supported label.");
                        featurePageActionCount++;
                        break;
                    case ShellFlowAction.FeaturePageActionExitFocus:
                        if (target.Feature == default || !target.HasFeaturePagePresentation ||
                            target.transform != _featureFocusExitSlot)
                            throw new InvalidOperationException(
                                "GlobalFrontendShell feature focus exit requires the external focus-exit slot, a feature, Button, and exactly one supported label.");
                        featurePageActionExitCount++;
                        break;
                    default:
                        throw new InvalidOperationException("GlobalFrontendShell contains a non-flow action target.");
                }
            }
            if (!hasModel || !hasVideo || !hasPanorama || !hasBackToMain || !hasClose ||
                featurePageActionCount != 1 || featurePageActionExitCount != 1)
                throw new InvalidOperationException(
                    "GlobalFrontendShell requires Model, Video, Panorama, back-to-main, close, and exactly one feature-page action plus focus-exit target.");
        }

        static Vector2 SizeOf(RectTransform slot) => slot.rect.size;
        static Vector3 StageSizeOf(RectTransform slot)
        {
            var size = SizeOf(slot);
            return new Vector3(size.x, size.y, 1f);
        }

        static bool IsPositiveFinite(Vector2 value) => IsPositiveFinite(value.x) && IsPositiveFinite(value.y);
        static bool IsPositiveFinite(Vector3 value) => IsPositiveFinite(value.x) && IsPositiveFinite(value.y) && IsPositiveFinite(value.z);
        static bool IsPositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
