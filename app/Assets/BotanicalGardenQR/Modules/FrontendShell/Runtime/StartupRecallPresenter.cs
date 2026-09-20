using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    [DisallowMultipleComponent]
    public sealed class StartupRecallPresenter : MonoBehaviour,
        IRecallStateSink,
        IObservationCompletionRequestSource,
        IClosedContentJourneyIntentSource,
        INextPointPromptPresenter,
        IVisitorProgressSummaryPresenter,
        ISpatialDataPermissionRecoverySurface
    {
        [Header("Startup and recall")]
        [SerializeField] GameObject _startupRoot;
        [SerializeField] RectTransform _startupBackground;
        [SerializeField] TMP_Text _startupText;
        [SerializeField] Button _recallButton;
        [SerializeField] Button _nextStationButton;
        [SerializeField] Button _skipPointButton;
        [SerializeField, Min(0.2f)] float _startupDistance = .45f;

        [Header("Closed content actions")]
        [SerializeField, Min(0f)] float _closedActionSpacing = 12f;

        const float ClosedActionLabelPadding = 10f;
        const float ClosedActionMultilineHeight = 60f;
        const float ClosedActionMultilineSpacing = 4f;

        Camera _camera;
        IRecallController _recall;
        IDisposable _recallSubscription;
        IFrontendGazeSurfaceRegistration _gazeRegistration;
        RecallState _recallState;
        string _startupHint;
        string _closedContentContext;
        string _journeyPromptMessage;
        VisitorProgressSummary _visitorProgressSummary;
        string _recallButtonBaseLabel;
        Vector2 _startupRootBaseSize;
        Vector2 _startupBackgroundBaseSize;
        Vector2 _startupTextBaseSize;
        Vector2 _startupHintBasePosition;
        Vector2 _recallButtonBasePosition;
        bool _hasStartupActionLayout;
        bool _startupPlacementPending;
        bool _hasStartupPose;
        bool _closedDecisionConsumed;
        bool _applicationSurfaceSuppressed;
        bool _hasJourneyPrompt;
        SpatialDataPermissionState _spatialPermissionState = SpatialDataPermissionState.Unknown;

        public event Action ObservationCompletionRequested;
        public event Action SkipQuizAndCollectRequested;
        public event Action<bool> CloseDecisionVisibilityChanged;
        public event Action RetrySpatialPermissionRequested;
        public bool IsCloseDecisionVisible { get; private set; }

        public void Prime(Transform viewer, string startupHint)
        {
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));
            if (_startupRoot == null || _startupBackground == null || _startupText == null ||
                _recallButton == null || _nextStationButton == null || _skipPointButton == null)
                throw new InvalidOperationException("Startup/Recall requires one fully serialized CloseDecision surface and all three actions.");
            if (!IsPositiveFinite(_startupDistance))
                throw new InvalidOperationException("Startup/Recall startup distance must be positive and finite.");

            _camera = viewer.GetComponent<Camera>();
            if (_camera == null)
                throw new InvalidOperationException("The configured visitor viewer must own the gaze Camera component.");

            _startupHint = startupHint ?? string.Empty;
            SetButtonLabel(_nextStationButton, "完成本站学习");
            SetButtonLabel(_skipPointButton, "暂不答题，结束本站");
            EnsureClosedContentActions();
            SetClosedContentActionsVisible(false);
            _startupText.text = _startupHint;
            var canvas = _startupRoot.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.worldCamera = _camera;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 200;
            }
            PlaceStartupPrompt();
            _startupRoot.SetActive(false);
            enabled = true;
        }

        public void Configure(
            Transform viewer,
            IRecallController recall,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            string startupHint)
        {
            if (recall == null) throw new ArgumentNullException(nameof(recall));
            if (gazeSurfaces == null) throw new ArgumentNullException(nameof(gazeSurfaces));
            if (_recallSubscription != null)
                throw new InvalidOperationException("Startup/Recall presentation is already configured.");
            try
            {
                Prime(viewer, startupHint);
                _recall = recall;
                _gazeRegistration = gazeSurfaces.RegisterGazeSurface(_startupRoot.transform, 200, "CloseDecisionPopup");
                _recallButton.onClick.AddListener(HandleRecallSelected);
                _nextStationButton.onClick.AddListener(HandleKnowledgeQuizSelected);
                _skipPointButton.onClick.AddListener(HandleSkipPointSelected);
                _recallSubscription = _recall.Observe(this);
            }
            catch
            {
                Unconfigure();
                throw;
            }
        }

        public void Unconfigure()
        {
            _recallSubscription?.Dispose();
            _recallSubscription = null;
            if (_recallButton != null) _recallButton.onClick.RemoveListener(HandleRecallSelected);
            if (_nextStationButton != null) _nextStationButton.onClick.RemoveListener(HandleKnowledgeQuizSelected);
            if (_skipPointButton != null) _skipPointButton.onClick.RemoveListener(HandleSkipPointSelected);
            SetClosedContentActionsVisible(false);
            InvalidateInteraction();
            _gazeRegistration?.Dispose();
            _gazeRegistration = null;
            _camera = null;
            _recall = null;
            _recallState = null;
            _startupHint = null;
            _closedContentContext = null;
            _journeyPromptMessage = null;
            _visitorProgressSummary = null;
            _spatialPermissionState = SpatialDataPermissionState.Unknown;
            _startupPlacementPending = false;
            _hasStartupPose = false;
            _closedDecisionConsumed = false;
            _closedContentRequiresLearning = false;
            _applicationSurfaceSuppressed = false;
            _hasJourneyPrompt = false;
            ObservationCompletionRequested = null;
            SkipQuizAndCollectRequested = null;
            CloseDecisionVisibilityChanged = null;
            RetrySpatialPermissionRequested = null;
            if (_startupRoot != null) _startupRoot.SetActive(false);
            enabled = false;
        }

        public void Dispose() => Unconfigure();

        void InvalidateInteraction() => _gazeRegistration?.Invalidate();

        void Update()
        {
            if (_startupPlacementPending && _recallState != null && _recallState.IsClosed)
                PlaceStartupPrompt();
        }

        void OnEnable()
        {
            if (_recallSubscription != null) UpdateStartupVisibility();
        }

        void OnDisable()
        {
            InvalidateInteraction();
            SetCloseDecisionVisibility(false);
            if (_startupRoot != null) _startupRoot.SetActive(false);
        }

        void OnDestroy() => Unconfigure();

        public void OnStateChanged(RecallState state)
        {
            var wasClosed = _recallState != null && _recallState.IsClosed;
            _recallState = state ?? throw new ArgumentNullException(nameof(state));
            var showStartup = state.IsClosed;
            if (!showStartup) _closedDecisionConsumed = false;
            if (!showStartup || !wasClosed) _hasStartupPose = false;
            _startupPlacementPending = showStartup;
            if (!_hasJourneyPrompt && !HasSpatialPermissionRecovery)
                _startupText.text = state.Fault?.Message ??
                                    (state.IsClosed && state.HasPreviousContent
                                        ? ClosedContentMessage()
                                        : _startupHint);
            SetClosedContentActionInteractability(state.CanRecall);
            if (showStartup) PlaceStartupPrompt();
            UpdateStartupVisibility();
            InvalidateInteraction();
        }

        public void OnSpatialDataPermissionStateChanged(SpatialDataPermissionState state)
        {
            if (!Enum.IsDefined(typeof(SpatialDataPermissionState), state))
                throw new ArgumentOutOfRangeException(nameof(state));
            _spatialPermissionState = state;
            _startupPlacementPending = HasSpatialPermissionRecovery;
            if (!HasSpatialPermissionRecovery && _hasJourneyPrompt)
                _startupText.text = DecorateWithProgress(_journeyPromptMessage);
            UpdateStartupVisibility();
            InvalidateInteraction();
        }

        public void SetApplicationSurfaceSuppressed(bool suppressed)
        {
            if (_applicationSurfaceSuppressed == suppressed) return;
            _applicationSurfaceSuppressed = suppressed;
            UpdateStartupVisibility();
            InvalidateInteraction();
        }

        public void ShowJourneyMessage(string message)
        {
            if (_recallState == null || !_recallState.IsClosed || !_recallState.HasPreviousContent ||
                _hasJourneyPrompt)
                return;
            _startupText.text = DecorateWithProgress(string.IsNullOrWhiteSpace(message)
                ? "当前无法前往下一站。"
                : message);
            UpdateStartupVisibility();
            InvalidateInteraction();
        }

        public void ShowJourneyStatus(string message)
        {
            if (_recallState == null || !_recallState.IsClosed || !_recallState.HasPreviousContent)
                return;
            _hasJourneyPrompt = true;
            _startupPlacementPending = true;
            _journeyPromptMessage = string.IsNullOrWhiteSpace(message)
                ? "本次观察状态已更新。"
                : message;
            _startupText.text = DecorateWithProgress(_journeyPromptMessage);
            UpdateStartupVisibility();
            InvalidateInteraction();
        }

        public void SetClosedContentContext(string message)
        {
            _closedContentContext = message?.Trim() ?? string.Empty;
            if (_recallState == null || !_recallState.IsClosed || !_recallState.HasPreviousContent ||
                _hasJourneyPrompt)
                return;
            _startupText.text = ClosedContentMessage();
            UpdateStartupVisibility();
            InvalidateInteraction();
        }

        public void ClearJourneyPrompt()
        {
            if (!_hasJourneyPrompt) return;
            _hasJourneyPrompt = false;
            _journeyPromptMessage = null;
            if (_recallState != null && _recallState.IsClosed)
                _startupText.text = _recallState.Fault?.Message ??
                                    (_recallState.HasPreviousContent
                                        ? ClosedContentMessage()
                                        : _startupHint);
            UpdateStartupVisibility();
            InvalidateInteraction();
        }

        bool _closedContentRequiresLearning;
        public void SetClosedContentRequiresLearning(bool required)
        {
            _closedContentRequiresLearning = required;
            UpdateStartupVisibility();
            _gazeRegistration?.Invalidate();
        }
        public void CompleteClosedContent()
        {
            _closedDecisionConsumed = true;
            _hasJourneyPrompt = false;
            _journeyPromptMessage = null;
            UpdateStartupVisibility();
            InvalidateInteraction();
        }

        public void SetVisitorProgressSummary(VisitorProgressSummary summary)
        {
            _visitorProgressSummary = summary ?? throw new ArgumentNullException(nameof(summary));
            if (_startupText == null || HasSpatialPermissionRecovery) return;
            if (_hasJourneyPrompt && !string.IsNullOrWhiteSpace(_journeyPromptMessage))
                _startupText.text = DecorateWithProgress(_journeyPromptMessage);
            else if (_recallState != null && _recallState.IsClosed && _recallState.HasPreviousContent)
                _startupText.text = ClosedContentMessage();
            if (_hasStartupActionLayout) UpdateStartupVisibility();
        }

        string ClosedContentMessage()
            => DecorateWithProgress(string.IsNullOrWhiteSpace(_closedContentContext)
                ? _closedContentRequiresLearning ? "本站图片核查尚未完成，请重新进入学习。" : "完成本站学习后，跟随小精灵前往下一站"
                : _closedContentContext);

        string DecorateWithProgress(string message)
        {
            var progress = ProgressCopy(_visitorProgressSummary);
            return string.IsNullOrEmpty(progress)
                ? message
                : $"{message}\n{progress}";
        }

        internal static string ProgressCopy(VisitorProgressSummary summary)
        {
            if (summary == null || !summary.HasProgress) return string.Empty;
            var copy = $"学习进度 {summary.MainCompleted}/{summary.MainTotal}";
            return summary.IsMainComplete ? copy + " · 六站学习已完成" : copy;
        }

        void UpdateStartupVisibility()
        {
            if (_startupRoot == null) return;
            var flowIsClosed = _recallState == null || _recallState.IsClosed;
            var permissionRecovery = HasSpatialPermissionRecovery && flowIsClosed;
            var showStartup = isActiveAndEnabled &&
                              !_closedDecisionConsumed &&
                              flowIsClosed &&
                              !_applicationSurfaceSuppressed &&
                              (permissionRecovery ||
                               (_recallState != null &&
                                (_recallState.HasPreviousContent || _recallState.Fault != null)));
            if (permissionRecovery)
            {
                _startupText.text = SpatialPermissionMessage(_spatialPermissionState);
                SetPermissionRecoveryActionVisible(
                    showStartup && CanRetrySpatialPermission,
                    _spatialPermissionState == SpatialDataPermissionState.PermanentlyDenied
                        ? "检查授权"
                        : "重试授权");
            }
            else
            {
                if (!_hasJourneyPrompt)
                    _startupText.text = _recallState?.Fault?.Message ??
                                        (_recallState != null && _recallState.IsClosed &&
                                         _recallState.HasPreviousContent
                                            ? ClosedContentMessage()
                                            : _startupHint);
                SetClosedContentActionsVisible(
                    showStartup && _recallState != null && _recallState.HasPreviousContent &&
                    !_hasJourneyPrompt);
            }
            _startupRoot.SetActive(showStartup);
        }

        void HandleRecallSelected()
        {
            if (HasSpatialPermissionRecovery)
            {
                if (CanRetrySpatialPermission)
                    RetrySpatialPermissionRequested?.Invoke();
                return;
            }
            if (_hasJourneyPrompt || _recall == null || _recallState == null ||
                !_recallState.IsClosed || !_recallState.CanRecall)
                return;
            var result = _recall.Recall();
            if (!result.Succeeded && result.Fault != null) _startupText.text = result.Fault.Message;
        }

        public void BeginClosedContentQuiz() => HandleKnowledgeQuizSelected();

        void HandleKnowledgeQuizSelected()
        {
            if (_closedContentRequiresLearning || _hasJourneyPrompt || _recallState == null || !_recallState.IsClosed ||
                !_recallState.HasPreviousContent)
            {
                Debug.Log("[GazeButtons] quiz selected but blocked by guard " +
                          $"(hasJourneyPrompt={_hasJourneyPrompt}, closed={_recallState?.IsClosed}, " +
                          $"hasPrevious={_recallState?.HasPreviousContent}).");
                return;
            }
            Debug.Log("[GazeButtons] quiz selected -> opening knowledge quiz.");
            ObservationCompletionRequested?.Invoke();
        }

        void HandleSkipPointSelected()
        {
            if (_closedContentRequiresLearning || _hasJourneyPrompt || _recallState == null || !_recallState.IsClosed ||
                !_recallState.HasPreviousContent)
                return;
            if (SkipQuizAndCollectRequested == null)
            {
                ShowJourneyMessage("当前无法处理本次问答。");
                return;
            }
            Debug.Log("[GazeButtons] quiz skip selected -> requesting direct collection.");
            SkipQuizAndCollectRequested.Invoke();
        }

        void EnsureClosedContentActions()
        {
            if (_hasStartupActionLayout) return;

            var startupRect = _startupRoot.transform as RectTransform;
            if (startupRect == null)
                throw new InvalidOperationException("Startup/Recall startup prompt must use a RectTransform.");

            _startupRootBaseSize = startupRect.sizeDelta;
            _startupBackgroundBaseSize = _startupBackground.sizeDelta;
            _startupTextBaseSize = _startupText.rectTransform.sizeDelta;
            _startupHintBasePosition = _startupText.rectTransform.anchoredPosition;
            NormalizeClosedActionButtonLayout();
            _recallButtonBasePosition = ((RectTransform)_recallButton.transform).anchoredPosition;
            _recallButtonBaseLabel = ReadButtonLabel(_recallButton);
            _hasStartupActionLayout = true;
        }

        void NormalizeClosedActionButtonLayout()
        {
            var buttons = new[]
            {
                _recallButton,
                _nextStationButton,
                _skipPointButton
            };
            var requiredHeight = 0f;
            for (var index = 0; index < buttons.Length; index++)
            {
                var buttonRect = buttons[index].transform as RectTransform;
                var label = buttons[index].GetComponentInChildren<TMP_Text>(true);
                if (buttonRect == null || label == null)
                    throw new InvalidOperationException(
                        $"CloseDecision action '{buttons[index].name}' requires one TMP label and RectTransform.");

                var multiline = (label.text ?? string.Empty).IndexOf('\n') >= 0;
                if (multiline)
                    label.lineSpacing = Mathf.Max(label.lineSpacing, ClosedActionMultilineSpacing);
                var labelRect = label.rectTransform;
                var width = Mathf.Max(1f, labelRect.rect.width);
                var preferred = label.GetPreferredValues(label.text ?? string.Empty, width, 0f);
                if (!IsPositiveFinite(preferred.y))
                    throw new InvalidOperationException(
                        $"CloseDecision action '{buttons[index].name}' has an invalid preferred text height.");
                requiredHeight = Mathf.Max(
                    requiredHeight,
                    Mathf.Max(
                        buttonRect.rect.height,
                        Mathf.Ceil(preferred.y + ClosedActionLabelPadding)));
                if (multiline)
                    requiredHeight = Mathf.Max(requiredHeight, ClosedActionMultilineHeight);
            }

            requiredHeight = Mathf.Ceil(requiredHeight);
            for (var index = 0; index < buttons.Length; index++)
            {
                var buttonRect = (RectTransform)buttons[index].transform;
                buttonRect.sizeDelta = new Vector2(buttonRect.sizeDelta.x, requiredHeight);
                var labelRect = buttons[index].GetComponentInChildren<TMP_Text>(true).rectTransform;
                labelRect.sizeDelta = new Vector2(
                    labelRect.sizeDelta.x,
                    Mathf.Max(labelRect.sizeDelta.y, requiredHeight - ClosedActionLabelPadding));
            }
        }

        void SetClosedContentActionsVisible(bool visible)
        {
            if (!_hasStartupActionLayout) return;
            SetButtonLabel(_recallButton, _recallButtonBaseLabel);

            var startupRect = _startupRoot.transform as RectTransform;
            if (startupRect == null) return;

            if (!visible)
            {
                var retryActionRect = (RectTransform)_recallButton.transform;
                retryActionRect.anchoredPosition = _recallButtonBasePosition;
                _recallButton.gameObject.SetActive(false);
                _nextStationButton.gameObject.SetActive(false);
                _skipPointButton.gameObject.SetActive(false);
                ApplyStartupPanelLayout(startupRect, _startupRootBaseSize.y);
                SetCloseDecisionVisibility(false);
                return;
            }

            var actionRect = (RectTransform)_recallButton.transform;
            if (_closedContentRequiresLearning)
            {
                ApplyStartupPanelLayout(startupRect, _startupRootBaseSize.y + actionRect.sizeDelta.y + _closedActionSpacing);
                actionRect.anchoredPosition = _recallButtonBasePosition;
                SetButtonLabel(_recallButton, "重新进入学习");
                _recallButton.gameObject.SetActive(true);
                _nextStationButton.gameObject.SetActive(false); _skipPointButton.gameObject.SetActive(false);
                SetCloseDecisionVisibility(true);
                return;
            }
            var actionStep = actionRect.sizeDelta.y + _closedActionSpacing;
            var topActionY = actionStep;
            var expandedHeight = _startupRootBaseSize.y +
                                 (actionStep * 2f) +
                                 Mathf.Max(0f, actionRect.sizeDelta.y - _closedActionSpacing);
            // Keep the summary above the first action after removing the fourth row.
            var minimumSummaryHeight = _startupRootBaseSize.y + _startupTextBaseSize.y +
                                       2f * (topActionY + actionRect.sizeDelta.y * 0.5f +
                                             _closedActionSpacing - _startupHintBasePosition.y);
            expandedHeight = Mathf.Max(expandedHeight, minimumSummaryHeight);
            ApplyStartupPanelLayout(startupRect, expandedHeight);

            // Keep the primary quiz action on the viewer's central gaze line while
            // the additional rows expand downward inside the enlarged surface.
            actionRect.anchoredPosition = new Vector2(_recallButtonBasePosition.x, topActionY);
            ((RectTransform)_nextStationButton.transform).anchoredPosition =
                new Vector2(_recallButtonBasePosition.x, topActionY - actionStep);
            ((RectTransform)_skipPointButton.transform).anchoredPosition =
                new Vector2(_recallButtonBasePosition.x, topActionY - (actionStep * 2f));
            _recallButton.gameObject.SetActive(true);
            _nextStationButton.gameObject.SetActive(true);
            _skipPointButton.gameObject.SetActive(true);
            SetCloseDecisionVisibility(true);
        }

        void SetPermissionRecoveryActionVisible(bool visible, string label)
        {
            if (!_hasStartupActionLayout) return;
            SetClosedContentActionsVisible(false);
            if (!visible) return;

            var startupRect = _startupRoot.transform as RectTransform;
            var actionRect = _recallButton.transform as RectTransform;
            if (startupRect == null || actionRect == null) return;
            var expandedHeight = _startupRootBaseSize.y +
                                 actionRect.sizeDelta.y +
                                 _closedActionSpacing;
            ApplyStartupPanelLayout(startupRect, expandedHeight);
            actionRect.anchoredPosition = _recallButtonBasePosition;
            SetButtonLabel(_recallButton, label);
            _recallButton.interactable = true;
            _recallButton.gameObject.SetActive(true);
            EntryUIButtonVisual.ApplyFromExisting(_recallButton);
        }

        void ApplyStartupPanelLayout(RectTransform startupRect, float actionLayoutHeight)
        {
            var textHeight = _startupTextBaseSize.y;
            if (_startupText != null && _startupTextBaseSize.x > 0f)
            {
                var preferred = _startupText.GetPreferredValues(
                    _startupText.text ?? string.Empty,
                    _startupTextBaseSize.x,
                    0f);
                if (IsPositiveFinite(preferred.y))
                    textHeight = Mathf.Max(_startupTextBaseSize.y, Mathf.Ceil(preferred.y + 2f));
            }

            var textExpansion = Mathf.Max(0f, textHeight - _startupTextBaseSize.y);
            var panelHeight = Mathf.Max(_startupRootBaseSize.y, actionLayoutHeight + textExpansion);
            startupRect.sizeDelta = new Vector2(_startupRootBaseSize.x, panelHeight);
            _startupBackground.sizeDelta = new Vector2(_startupBackgroundBaseSize.x, panelHeight);
            _startupText.rectTransform.sizeDelta = new Vector2(_startupTextBaseSize.x, textHeight);
            _startupText.rectTransform.anchoredPosition = new Vector2(
                _startupHintBasePosition.x,
                _startupHintBasePosition.y + ((panelHeight - _startupRootBaseSize.y) * 0.5f));
        }

        void SetCloseDecisionVisibility(bool visible)
        {
            if (IsCloseDecisionVisible == visible) return;
            IsCloseDecisionVisible = visible;
            CloseDecisionVisibilityChanged?.Invoke(visible);
        }

        void SetClosedContentActionInteractability(bool canRecall)
        {
            if (!_hasStartupActionLayout) return;
            _recallButton.interactable = canRecall;
            _nextStationButton.interactable = true;
            _skipPointButton.interactable = true;
            EntryUIButtonVisual.ApplyFromExisting(_recallButton);
            EntryUIButtonVisual.ApplyFromExisting(_nextStationButton, active: true);
            EntryUIButtonVisual.ApplyFromExisting(_skipPointButton);
        }

        void PlaceStartupPrompt()
        {
            // Feedback and modal refreshes belong to the same reading session.
            // Leaning into an action must not push the already placed surface away.
            if (_hasStartupPose) { _startupPlacementPending = false; return; }
            if (_camera == null || _startupRoot == null) return;
            WorldSurfacePlacement.PlaceViewerFront(_startupRoot.transform, _camera.transform, _startupDistance);
            _hasStartupPose = true;
            _startupPlacementPending = false;
        }

        bool HasSpatialPermissionRecovery =>
            _spatialPermissionState != SpatialDataPermissionState.Unknown &&
            _spatialPermissionState != SpatialDataPermissionState.Granted;

        bool CanRetrySpatialPermission =>
            _spatialPermissionState == SpatialDataPermissionState.Denied ||
            _spatialPermissionState == SpatialDataPermissionState.PermanentlyDenied;

        static string SpatialPermissionMessage(SpatialDataPermissionState state)
        {
            switch (state)
            {
                case SpatialDataPermissionState.Requesting:
                    return "正在请求空间数据权限…";
                case SpatialDataPermissionState.Denied:
                    return "需要空间数据权限才能识别二维码和定位现场内容。请选择下方按钮重试。";
                case SpatialDataPermissionState.PermanentlyDenied:
                    return "空间数据权限已关闭。请在系统设置的应用权限中允许空间数据，返回后再检查授权。";
                case SpatialDataPermissionState.Unsupported:
                    return "当前设备不支持所需的空间数据能力，请联系工作人员。";
                default:
                    return string.Empty;
            }
        }

        static string ReadButtonLabel(Button button)
        {
            if (button == null) return string.Empty;
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) return tmp.text ?? string.Empty;
            var legacy = button.GetComponentInChildren<Text>(true);
            return legacy != null ? legacy.text ?? string.Empty : string.Empty;
        }

        static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = label ?? string.Empty;
                return;
            }
            var legacy = button.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = label ?? string.Empty;
        }

        static bool IsPositiveFinite(float value)
            => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

    }
}
