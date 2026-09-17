using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace BotanicalGardenQR.SpatialAnchorAdmin.Official
{
    /// <summary>
    /// Owns the single administrator workspace for the official Meta spatial
    /// anchor lifecycle. Route authoring and venue navigation do not enter here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OfficialAdminPanelPresentation : MonoBehaviour
    {
        static readonly Color PanelColor = new Color(0.008f, 0.018f, 0.020f, 0.94f);
        static readonly Color CreateColor = new Color(0.055f, 0.30f, 0.25f, 0.98f);
        static readonly Color LoadColor = new Color(0.075f, 0.19f, 0.24f, 0.98f);
        static readonly Color EraseColor = new Color(0.40f, 0.10f, 0.10f, 0.98f);
        static readonly Color FocusColor = new Color(0.208f, 0.949f, 0.761f, 1f);
        static readonly Color SecondaryText = new Color(1f, 1f, 1f, 0.84f);
        const string PlacementCopy =
            "左射线命中现实表面后按 X，创建待保存候选\n" +
            "点击已保存锚点可微调；长按 Y 取消";
        const float MenuButtonHeight = 100f;

        [SerializeField] MonoBehaviour _officialAnchorAdminSource;
        [SerializeField] Canvas _controllerHintCanvas;
        [SerializeField] RectTransform _panelRoot;
        [SerializeField] Button _createButton;
        [SerializeField] Button _loadButton;
        [SerializeField, FormerlySerializedAs("_eraseSelectedAnchorButton")]
        Button _eraseAnchorButton;
        [SerializeField] TMP_Text _workflowStatusText;
        [SerializeField] GameObject _standardActionsRoot;
        [SerializeField] GameObject _candidateActionsRoot;
        [SerializeField] TMP_Text _anchorIdentityText;
        [SerializeField] Button _renameAnchorButton;
        [SerializeField] Button _saveCandidateButton;
        [SerializeField] Button _cancelCandidateButton;

        TMP_Text _createButtonLabel;
        TMP_Text _loadButtonLabel;
        TMP_Text _eraseAnchorButtonLabel;
        TMP_Text _saveCandidateButtonLabel;
        TMP_Text _cancelCandidateButtonLabel;
        bool _singleErasePending;
        bool _bound;
        IOfficialSpatialAnchorAdminCommands _officialAnchorAdmin;
        TouchScreenKeyboard _renameKeyboard;

        void Awake()
        {
            Bind();
            Apply();
        }

        void OnEnable()
        {
            Bind();
            Apply();
        }

        void OnDisable() => Unbind();

        void Update()
        {
            if (_renameKeyboard == null || _renameKeyboard.status == TouchScreenKeyboard.Status.Visible)
                return;
            var keyboard = _renameKeyboard;
            _renameKeyboard = null;
            if (_renameAnchorButton != null) _renameAnchorButton.interactable = true;
            if (keyboard.status == TouchScreenKeyboard.Status.Canceled)
            {
                ShowError(string.Empty, "已取消修改名称；原名称保持不变。");
                return;
            }
            if (keyboard.status == TouchScreenKeyboard.Status.LostFocus)
            {
                ShowError(string.Empty, "系统键盘失去焦点，名称未保存；请重新点击“修改名称”并明确完成输入。");
                return;
            }
            if (keyboard.status != TouchScreenKeyboard.Status.Done)
            {
                ShowError(string.Empty, "系统键盘未返回明确完成结果，名称未保存。");
                return;
            }
            if (_officialAnchorAdmin == null)
            {
                ShowError(string.Empty, "锚点名称未保存。");
                return;
            }
            if (!_officialAnchorAdmin.RenameAnchorBeingAdjusted(keyboard.text, out var error))
            {
                ShowError(error, "锚点名称未保存。");
                return;
            }
            RefreshAnchorIdentity();
        }

        public void Apply()
        {
            _singleErasePending = _officialAnchorAdmin != null &&
                                  _officialAnchorAdmin.IsSingleAnchorEraseInProgress;
            StylePlacementInstruction();
            if (_panelRoot)
            {
                var panel = _panelRoot.GetComponent<Image>();
                if (panel) panel.color = PanelColor;

                var layout = _panelRoot.GetComponent<VerticalLayoutGroup>();
                if (layout)
                {
                    layout.padding = new RectOffset(18, 18, 18, 18);
                    layout.spacing = 14f;
                    layout.childAlignment = TextAnchor.MiddleCenter;
                    layout.childControlWidth = true;
                    layout.childControlHeight = true;
                    layout.childForceExpandWidth = true;
                    layout.childForceExpandHeight = false;
                }
            }

            StyleButton(_createButton, CreateColor);
            StyleButton(_loadButton, LoadColor);
            StyleButton(_eraseAnchorButton, EraseColor);
            StyleButton(_renameAnchorButton, LoadColor);
            StyleButton(_saveCandidateButton, CreateColor);
            StyleButton(_cancelCandidateButton, EraseColor);
            StyleStatus();
            ApplyLoadState(_officialAnchorAdmin != null
                ? _officialAnchorAdmin.LoadState
                : new OfficialAnchorLoadState(OfficialAnchorLoadPhase.Idle, string.Empty));
            ApplyWorkflowState(
                _officialAnchorAdmin != null
                    ? _officialAnchorAdmin.GuidedPlacementPhase
                    : GuidedAnchorPlacementPhase.Idle,
                string.Empty);
            ApplyEraseModeState(
                _officialAnchorAdmin != null && _officialAnchorAdmin.IsEraseMode,
                string.Empty);

            if (!_panelRoot) return;
            foreach (var text in _panelRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                text.color = SecondaryText;
                text.fontSize = Mathf.Min(text.fontSize, 24f);
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        void Bind()
        {
            if (_bound) return;
            _officialAnchorAdmin = _officialAnchorAdminSource as IOfficialSpatialAnchorAdminCommands;
            _loadButtonLabel = _loadButton != null
                ? _loadButton.GetComponentInChildren<TMP_Text>(true)
                : null;
            _createButtonLabel = _createButton != null
                ? _createButton.GetComponentInChildren<TMP_Text>(true)
                : null;
            _eraseAnchorButtonLabel = _eraseAnchorButton != null
                ? _eraseAnchorButton.GetComponentInChildren<TMP_Text>(true)
                : null;
            _saveCandidateButtonLabel = _saveCandidateButton != null
                ? _saveCandidateButton.GetComponentInChildren<TMP_Text>(true)
                : null;
            _cancelCandidateButtonLabel = _cancelCandidateButton != null
                ? _cancelCandidateButton.GetComponentInChildren<TMP_Text>(true)
                : null;

            if (_officialAnchorAdmin != null)
            {
                _officialAnchorAdmin.GuidedPlacementChanged += HandleGuidedPlacementChanged;
                _officialAnchorAdmin.LoadStateChanged += HandleLoadStateChanged;
                _officialAnchorAdmin.SavedAnchorsChanged += HandleSavedAnchorsChanged;
                _officialAnchorAdmin.AnchorAdjustmentChanged += HandleAnchorAdjustmentChanged;
                _officialAnchorAdmin.EraseModeChanged += HandleEraseModeChanged;
                _officialAnchorAdmin.SingleAnchorEraseCompleted += HandleSingleAnchorEraseCompleted;
            }
            if (_createButton != null)
                _createButton.onClick.AddListener(HandlePlacementAction);
            if (_loadButton != null)
                _loadButton.onClick.AddListener(HandleLoadAction);
            if (_eraseAnchorButton != null)
                _eraseAnchorButton.onClick.AddListener(HandleEraseAnchorAction);
            if (_saveCandidateButton != null)
                _saveCandidateButton.onClick.AddListener(HandleSaveCandidate);
            if (_renameAnchorButton != null)
                _renameAnchorButton.onClick.AddListener(HandleRenameAnchor);
            if (_cancelCandidateButton != null)
                _cancelCandidateButton.onClick.AddListener(HandleCancelCandidate);
            _bound = true;
        }

        void Unbind()
        {
            if (!_bound) return;
            if (_officialAnchorAdmin != null)
            {
                _officialAnchorAdmin.GuidedPlacementChanged -= HandleGuidedPlacementChanged;
                _officialAnchorAdmin.LoadStateChanged -= HandleLoadStateChanged;
                _officialAnchorAdmin.SavedAnchorsChanged -= HandleSavedAnchorsChanged;
                _officialAnchorAdmin.AnchorAdjustmentChanged -= HandleAnchorAdjustmentChanged;
                _officialAnchorAdmin.EraseModeChanged -= HandleEraseModeChanged;
                _officialAnchorAdmin.SingleAnchorEraseCompleted -= HandleSingleAnchorEraseCompleted;
            }
            if (_createButton != null)
                _createButton.onClick.RemoveListener(HandlePlacementAction);
            if (_loadButton != null)
                _loadButton.onClick.RemoveListener(HandleLoadAction);
            if (_eraseAnchorButton != null)
                _eraseAnchorButton.onClick.RemoveListener(HandleEraseAnchorAction);
            if (_saveCandidateButton != null)
                _saveCandidateButton.onClick.RemoveListener(HandleSaveCandidate);
            if (_renameAnchorButton != null)
                _renameAnchorButton.onClick.RemoveListener(HandleRenameAnchor);
            if (_cancelCandidateButton != null)
                _cancelCandidateButton.onClick.RemoveListener(HandleCancelCandidate);
            _renameKeyboard = null;
            _singleErasePending = false;
            _bound = false;
        }

        void HandleGuidedPlacementChanged(GuidedAnchorPlacementPhase phase, string message)
        {
            ApplyWorkflowState(phase, message);
        }

        void ApplyWorkflowState(GuidedAnchorPlacementPhase phase, string message)
        {
            var showCandidateActions = phase == GuidedAnchorPlacementPhase.PreparingCandidate ||
                                       phase == GuidedAnchorPlacementPhase.ReviewingNewAnchor ||
                                       phase == GuidedAnchorPlacementPhase.AdjustingSavedAnchor ||
                                       phase == GuidedAnchorPlacementPhase.Saving ||
                                       phase == GuidedAnchorPlacementPhase.SaveOutcomeUnknown ||
                                       phase == GuidedAnchorPlacementPhase.PendingIndex ||
                                       phase == GuidedAnchorPlacementPhase.PendingConfigurationRecord ||
                                       phase == GuidedAnchorPlacementPhase.SaveFailed ||
                                       phase == GuidedAnchorPlacementPhase.CleaningReplacedAnchor;
            if (_standardActionsRoot != null)
                _standardActionsRoot.SetActive(!showCandidateActions);
            if (_candidateActionsRoot != null)
                _candidateActionsRoot.SetActive(showCandidateActions);
            var showIdentity = phase == GuidedAnchorPlacementPhase.ReviewingNewAnchor ||
                               phase == GuidedAnchorPlacementPhase.AdjustingSavedAnchor;
            var canEditIdentity = phase == GuidedAnchorPlacementPhase.AdjustingSavedAnchor;
            if (_anchorIdentityText != null)
                _anchorIdentityText.gameObject.SetActive(showIdentity);
            if (_renameAnchorButton != null)
            {
                _renameAnchorButton.gameObject.SetActive(canEditIdentity);
                _renameAnchorButton.interactable = canEditIdentity && _renameKeyboard == null;
            }
            if (phase == GuidedAnchorPlacementPhase.ReviewingNewAnchor && _anchorIdentityText != null)
                _anchorIdentityText.text = "待保存新锚点\n取消不会写入设备记录";
            else if (canEditIdentity)
                RefreshAnchorIdentity();
            if (_createButton != null)
                _createButton.interactable = phase == GuidedAnchorPlacementPhase.Idle;
            if (_saveCandidateButton != null)
                _saveCandidateButton.interactable = phase == GuidedAnchorPlacementPhase.ReviewingNewAnchor ||
                                                    phase == GuidedAnchorPlacementPhase.AdjustingSavedAnchor ||
                                                    phase == GuidedAnchorPlacementPhase.SaveFailed ||
                                                    phase == GuidedAnchorPlacementPhase.PendingIndex ||
                                                    phase == GuidedAnchorPlacementPhase.PendingConfigurationRecord;
            if (_cancelCandidateButton != null)
                _cancelCandidateButton.interactable = phase == GuidedAnchorPlacementPhase.ReviewingNewAnchor ||
                                                      phase == GuidedAnchorPlacementPhase.AdjustingSavedAnchor ||
                                                      phase == GuidedAnchorPlacementPhase.SaveFailed;
            if (_saveCandidateButtonLabel == null && _saveCandidateButton != null)
                _saveCandidateButtonLabel = _saveCandidateButton.GetComponentInChildren<TMP_Text>(true);
            if (_saveCandidateButtonLabel != null)
                _saveCandidateButtonLabel.text = phase == GuidedAnchorPlacementPhase.PendingIndex ||
                                                 phase == GuidedAnchorPlacementPhase.PendingConfigurationRecord
                    ? "重试设备记录"
                    : phase == GuidedAnchorPlacementPhase.Saving ||
                      phase == GuidedAnchorPlacementPhase.SaveOutcomeUnknown ||
                      phase == GuidedAnchorPlacementPhase.CleaningReplacedAnchor
                        ? "正在保存…"
                        : phase == GuidedAnchorPlacementPhase.PreparingCandidate
                            ? "正在创建…"
                            : phase == GuidedAnchorPlacementPhase.SaveFailed
                                ? "重试保存"
                                : phase == GuidedAnchorPlacementPhase.ReviewingNewAnchor
                                    ? "保存锚点"
                                    : phase == GuidedAnchorPlacementPhase.AdjustingSavedAnchor
                                        ? "重新保存位置"
                                        : "保存锚点";
            if (_cancelCandidateButtonLabel == null && _cancelCandidateButton != null)
                _cancelCandidateButtonLabel = _cancelCandidateButton.GetComponentInChildren<TMP_Text>(true);
            if (_cancelCandidateButtonLabel != null)
                _cancelCandidateButtonLabel.text = phase == GuidedAnchorPlacementPhase.ReviewingNewAnchor ||
                                                   (phase == GuidedAnchorPlacementPhase.SaveFailed &&
                                                    (_officialAnchorAdmin == null ||
                                                     !_officialAnchorAdmin.TryGetAnchorEditIdentity(out _)))
                    ? "取消"
                    : phase == GuidedAnchorPlacementPhase.AdjustingSavedAnchor
                        ? "取消微调"
                        : "取消";
            ApplyEraseModeState(
                _officialAnchorAdmin != null && _officialAnchorAdmin.IsEraseMode,
                string.Empty);
            if (_createButtonLabel == null && _createButton != null)
                _createButtonLabel = _createButton.GetComponentInChildren<TMP_Text>(true);
            if (_createButtonLabel != null)
                _createButtonLabel.text = "放置锚点";

            if (_workflowStatusText == null || string.IsNullOrWhiteSpace(message)) return;
            _workflowStatusText.text = message;
        }

        void HandlePlacementAction()
        {
            if (_officialAnchorAdmin == null) return;
            if (!_officialAnchorAdmin.BeginGuidedAnchorPlacement(out var error))
                ShowError(error, "当前不能开始放置锚点。");
        }

        void HandleLoadAction()
        {
            if (_officialAnchorAdmin == null) return;
            if (!_officialAnchorAdmin.LoadAnchors(out var error))
                ShowError(error, "当前不能刷新已保存锚点。");
        }

        void HandleSaveCandidate()
        {
            if (_officialAnchorAdmin == null) return;
            var phase = _officialAnchorAdmin.GuidedPlacementPhase;
            string error;
            var succeeded = phase == GuidedAnchorPlacementPhase.PendingIndex ||
                            phase == GuidedAnchorPlacementPhase.PendingConfigurationRecord
                ? _officialAnchorAdmin.RetryGuidedAnchorRecord(out error)
                : phase == GuidedAnchorPlacementPhase.ReviewingNewAnchor ||
                  phase == GuidedAnchorPlacementPhase.SaveFailed
                    ? _officialAnchorAdmin.CommitNewAnchorCandidate(out error)
                    : _officialAnchorAdmin.CommitAnchorAdjustment(out error);
            if (!succeeded) ShowError(error, "当前候选不能保存。");
        }

        void HandleRenameAnchor()
        {
            if (_officialAnchorAdmin == null || _renameKeyboard != null) return;
            if (!_officialAnchorAdmin.TryGetAnchorEditIdentity(out var identity))
            {
                ShowError(string.Empty, "当前没有可改名的已保存锚点。");
                return;
            }
            if (!TouchScreenKeyboard.isSupported)
            {
                ShowError(string.Empty, "当前设备未提供系统键盘；自动编号仍会保持不变。");
                return;
            }
            _renameKeyboard = TouchScreenKeyboard.Open(
                identity.CustomName,
                TouchScreenKeyboardType.Default,
                false,
                false,
                false,
                false,
                "输入锚点名称",
                24);
            if (_renameKeyboard == null)
                ShowError(string.Empty, "系统键盘未能打开；自动编号仍会保持不变。");
            else
            {
                if (_renameAnchorButton != null) _renameAnchorButton.interactable = false;
                if (_workflowStatusText != null)
                    _workflowStatusText.text = "系统键盘已打开；完成输入后名称才会保存。";
            }
        }

        void RefreshAnchorIdentity()
        {
            if (_anchorIdentityText == null || _officialAnchorAdmin == null) return;
            if (!_officialAnchorAdmin.TryGetAnchorEditIdentity(out var identity))
            {
                _anchorIdentityText.text = "当前选择：锚点身份不可用";
                return;
            }
            if (_officialAnchorAdmin.TryGetAnchorAdjustment(out var adjustment))
            {
                RefreshAnchorIdentity(identity, adjustment);
                return;
            }
            _anchorIdentityText.text = $"当前选择：{identity.DisplayLabel}";
        }

        void HandleAnchorAdjustmentChanged(OfficialAnchorAdjustmentSnapshot adjustment)
        {
            if (_officialAnchorAdmin == null ||
                !_officialAnchorAdmin.TryGetAnchorEditIdentity(out var identity))
                return;
            RefreshAnchorIdentity(identity, adjustment);
        }

        void RefreshAnchorIdentity(
            OfficialAnchorIdentitySnapshot identity,
            OfficialAnchorAdjustmentSnapshot adjustment)
        {
            if (_anchorIdentityText == null) return;
            _anchorIdentityText.text =
                $"当前选择：{identity.DisplayLabel}\n" +
                $"微调：横向 {FormatCentimeters(adjustment.HorizontalOffsetMeters)} · " +
                $"高度 {FormatCentimeters(adjustment.VerticalOffsetMeters)}";
        }

        static string FormatCentimeters(float meters)
            => $"{meters * 100f:+0.0;-0.0;0.0} cm";

        void HandleSavedAnchorsChanged()
        {
            if (_officialAnchorAdmin != null)
                ApplyLoadState(_officialAnchorAdmin.LoadState);
            if (_officialAnchorAdmin != null &&
                _officialAnchorAdmin.GuidedPlacementPhase == GuidedAnchorPlacementPhase.AdjustingSavedAnchor)
                RefreshAnchorIdentity();
        }

        void HandleCancelCandidate()
        {
            if (_officialAnchorAdmin == null) return;
            if (!_officialAnchorAdmin.CancelGuidedAnchorPlacement(out var error))
                ShowError(error, "当前候选不能取消。");
        }

        void ShowError(string error, string fallback)
        {
            if (_workflowStatusText != null)
                _workflowStatusText.text = string.IsNullOrWhiteSpace(error) ? fallback : error;
        }

        void HandleEraseModeChanged(bool active, string message)
        {
            _singleErasePending = !active &&
                                  _officialAnchorAdmin != null &&
                                  _officialAnchorAdmin.IsSingleAnchorEraseInProgress;
            ApplyEraseModeState(active, message);
        }

        void HandleEraseAnchorAction()
        {
            if (_officialAnchorAdmin == null || _singleErasePending) return;
            if (!_officialAnchorAdmin.SetEraseMode(!_officialAnchorAdmin.IsEraseMode, out var error) &&
                _workflowStatusText != null && !string.IsNullOrWhiteSpace(error))
                _workflowStatusText.text = error;
        }

        void HandleSingleAnchorEraseCompleted(bool success, string message)
        {
            _singleErasePending = false;
            ApplyEraseModeState(false, message);
        }

        void ApplyEraseModeState(bool active, string message)
        {
            if (_eraseAnchorButtonLabel == null && _eraseAnchorButton != null)
                _eraseAnchorButtonLabel = _eraseAnchorButton.GetComponentInChildren<TMP_Text>(true);
            if (_eraseAnchorButtonLabel != null)
                _eraseAnchorButtonLabel.text = _singleErasePending
                    ? "正在删除…"
                    : active
                        ? "取消删除"
                        : "删除锚点";
            if (_eraseAnchorButton != null)
                _eraseAnchorButton.interactable = !_singleErasePending &&
                                                  (_officialAnchorAdmin == null ||
                                                   _officialAnchorAdmin.GuidedPlacementPhase ==
                                                   GuidedAnchorPlacementPhase.Idle) &&
                                                  (_officialAnchorAdmin == null ||
                                                   !_officialAnchorAdmin.LoadState.IsLoading);
            if (_createButton != null)
                _createButton.interactable = !active && !_singleErasePending &&
                                             (_officialAnchorAdmin == null ||
                                              _officialAnchorAdmin.GuidedPlacementPhase ==
                                              GuidedAnchorPlacementPhase.Idle);
            if (_loadButton != null)
                _loadButton.interactable = !active && !_singleErasePending &&
                                           (_officialAnchorAdmin == null ||
                                            _officialAnchorAdmin.GuidedPlacementPhase ==
                                            GuidedAnchorPlacementPhase.Idle) &&
                                           (_officialAnchorAdmin == null ||
                                            !_officialAnchorAdmin.LoadState.IsLoading);
            if (_workflowStatusText != null && !string.IsNullOrWhiteSpace(message))
                _workflowStatusText.text = message;
        }

        void HandleLoadStateChanged(OfficialAnchorLoadState state)
        {
            ApplyLoadState(state);
            ApplyEraseModeState(
                _officialAnchorAdmin != null && _officialAnchorAdmin.IsEraseMode,
                string.Empty);
        }

        void ApplyLoadState(OfficialAnchorLoadState state)
        {
            var phase = state.Phase;
            var message = state.Message;
            var totalCount = state.SavedAnchorCount;
            var trackedCount = state.TrackedAnchorCount;
            if (_loadButton != null)
                _loadButton.interactable = phase != OfficialAnchorLoadPhase.Loading &&
                                           (_officialAnchorAdmin == null ||
                                            (!_officialAnchorAdmin.IsEraseMode &&
                                             !_officialAnchorAdmin.IsSingleAnchorEraseInProgress &&
                                             _officialAnchorAdmin.GuidedPlacementPhase ==
                                             GuidedAnchorPlacementPhase.Idle));
            if (_loadButtonLabel == null && _loadButton != null)
                _loadButtonLabel = _loadButton.GetComponentInChildren<TMP_Text>(true);
            if (_loadButtonLabel != null)
            {
                _loadButtonLabel.text = phase switch
                {
                    OfficialAnchorLoadPhase.Loading => $"正在加载（{totalCount}）…",
                    OfficialAnchorLoadPhase.Failed => $"重试加载（{totalCount}）",
                    _ => $"查看锚点（{totalCount}）"
                };
            }

            if (_workflowStatusText == null) return;
            if (phase == OfficialAnchorLoadPhase.Completed)
            {
                var summary = $"设备记录共 {totalCount} 个锚点，当前已跟踪 {trackedCount} 个。";
                _workflowStatusText.text = string.IsNullOrWhiteSpace(message)
                    ? summary
                    : $"{message} {summary}";
            }
            else if (phase == OfficialAnchorLoadPhase.Failed && !string.IsNullOrWhiteSpace(message))
            {
                _workflowStatusText.text = message;
            }
        }

        void StyleStatus()
        {
            if (!_workflowStatusText) return;
            _workflowStatusText.color = SecondaryText;
            _workflowStatusText.alignment = TextAlignmentOptions.Center;
            _workflowStatusText.textWrappingMode = TextWrappingModes.Normal;
            _workflowStatusText.overflowMode = TextOverflowModes.Ellipsis;
            _workflowStatusText.enableAutoSizing = true;
            _workflowStatusText.fontSizeMin = 13f;
            _workflowStatusText.fontSizeMax = 18f;
            _workflowStatusText.raycastTarget = false;
        }

        void StylePlacementInstruction()
        {
            if (!_controllerHintCanvas) return;
            if (_controllerHintCanvas.transform is RectTransform canvasRect)
                canvasRect.sizeDelta = new Vector2(440f, 72f);
            var instruction = _controllerHintCanvas.GetComponentInChildren<TMP_Text>(true);
            if (!instruction) return;
            instruction.text = PlacementCopy;
            instruction.color = SecondaryText;
            instruction.alignment = TextAlignmentOptions.Center;
            instruction.textWrappingMode = TextWrappingModes.Normal;
            instruction.overflowMode = TextOverflowModes.Ellipsis;
            instruction.enableAutoSizing = true;
            instruction.fontSizeMin = 14f;
            instruction.fontSizeMax = 20f;
            instruction.rectTransform.sizeDelta = new Vector2(420f, 56f);
        }

        static void StyleButton(Button button, Color color)
        {
            if (!button) return;
            var layout = button.GetComponent<LayoutElement>() ??
                         button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = MenuButtonHeight;
            layout.preferredHeight = MenuButtonHeight;
            layout.flexibleHeight = 0f;
            var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
            if (image)
            {
                image.color = color;
                image.type = Image.Type.Sliced;
            }

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.62f, 1.18f, 0.96f, 1f);
            colors.pressedColor = new Color(1.18f, 0.76f, 0.34f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.38f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(FocusColor.r, FocusColor.g, FocusColor.b, 0.28f);
            outline.effectDistance = new Vector2(2f, 2f);
            outline.useGraphicAlpha = true;
        }

    }
}
