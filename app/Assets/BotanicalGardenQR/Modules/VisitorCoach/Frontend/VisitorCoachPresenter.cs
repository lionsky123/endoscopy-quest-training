using System;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.FrontendShell.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.VisitorCoach.Frontend
{
    public enum VisitorDialogueInputMode { Unavailable, HandPoke, HeadGaze }

    /// <summary>One presentation context's resolved, world-fixed composition.</summary>
    public sealed class VisitorDialogueComposition
    {
        internal VisitorDialogueComposition(VisitorDialogueContextId context, VisitorDialogueSurfaceMode mode,
            Pose stagePose, Vector3 fairyWorldPosition, VisitorDialogueExpression expression)
        {
            Context = context;
            Mode = mode;
            StagePose = stagePose;
            FairyWorldPosition = fairyWorldPosition;
            Expression = expression;
        }

        public VisitorDialogueContextId Context { get; }
        public VisitorDialogueSurfaceMode Mode { get; }
        public Pose StagePose { get; }
        public Vector3 FairyWorldPosition { get; }
        public VisitorDialogueExpression Expression { get; }

        internal VisitorDialogueComposition WithPresentation(VisitorDialogueSurfaceMode mode, VisitorDialogueExpression expression)
            => mode == Mode && expression == Expression ? this :
                new VisitorDialogueComposition(Context, mode, StagePose, FairyWorldPosition, expression);
    }

    /// <summary>
    /// The one application-global tutorial dialogue stage. Business page state
    /// stays in Prologue/Coach; this presenter owns composition, visual reveal and input feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisitorCoachPresenter : MonoBehaviour, IDisposable
    {
        [Header("Surface")]
        [SerializeField] Canvas _canvas;
        [SerializeField] CanvasGroup _group;
        [SerializeField] RectTransform _portrait;
        [SerializeField] Image _portraitImage;
        [SerializeField] GameObject _dialogueRoot;
        [SerializeField] RectTransform _panel;
        [SerializeField] Image _panelImage;
        [SerializeField] RectTransform _innerPanel;
        [SerializeField] Image _innerPanelImage;
        [SerializeField] RectTransform _accent;
        [SerializeField] Image _accentImage;

        [Header("Dialogue copy")]
        [SerializeField] TMP_Text _chapter;
        [SerializeField] TMP_Text _speaker;
        [SerializeField] TMP_Text _body;
        [SerializeField] TMP_Text _page;

        [Header("Dialogue actions")]
        [SerializeField] Button _continueButton;
        [SerializeField] TMP_Text _continueLabel;
        [SerializeField] VisitorDialoguePointableTarget _continuePokeTarget;
        [SerializeField] Button _restartButton;
        [SerializeField] TMP_Text _restartLabel;
        [SerializeField] VisitorDialoguePointableTarget _restartPokeTarget;

        [Header("Action replay")]
        [SerializeField] GameObject _actionRoot;
        [SerializeField] TMP_Text _actionHint;
        [SerializeField] Button _replayButton;
        [SerializeField] TMP_Text _replayLabel;
        [SerializeField] VisitorDialoguePointableTarget _replayPokeTarget;
        [SerializeField] Button _deferButton;
        [SerializeField] TMP_Text _deferLabel;

        Transform _viewer;
        AudioSource _confirmationAudio;
        VisitorCoachThemeAsset _theme;
        VisitorDialogueSurfaceState _state;
        IFrontendGazeSurfaceRegistration _gazeRegistration;
        VisitorDialogueInputMode _inputMode;
        float _inputReadyAt;
        int _bodyCharacterCount;
        int _lastAcceptedFrame = -1;
        bool _dialogueVisible;
        bool _targetVisible;
        bool _configured;
        bool _disposed;
        DialogueContractGraphic _dialogueFinish;

        public event Action<VisitorDialogueIntent> IntentRequested;
        public event Action<bool> DialogueVisibilityChanged;
        public event Action<VisitorDialogueSurfaceState> SurfaceStateChanged;

        public VisitorDialogueOwner? CurrentOwner => _state?.Owner;
        public VisitorDialogueSurfaceState CurrentState => _state;
        public VisitorDialogueComposition CurrentComposition { get; private set; }
        public event Action<VisitorDialogueComposition> CompositionChanged;

        public void Configure(
            Transform viewer,
            VisitorCoachThemeAsset theme,
            TMP_FontAsset sharedFont,
            IFrontendGazeSurfaceRegistry gazeSurfaces)
        {
            if (_configured || _disposed) throw new InvalidOperationException("Dialogue stage cannot be configured twice.");
            if (gazeSurfaces == null) throw new ArgumentNullException(nameof(gazeSurfaces));
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            _theme = theme != null ? theme : throw new ArgumentNullException(nameof(theme));
            if (sharedFont == null) throw new ArgumentNullException(nameof(sharedFont));
            if (!_theme.IsValid(out var error)) throw new InvalidOperationException(error);
            ValidateSerializedReferences();

            var rootRect = (RectTransform)transform;
            rootRect.sizeDelta = _theme.PanelPixels;
            rootRect.localScale = Vector3.one * _theme.CanvasScale;
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.worldCamera = _viewer.GetComponent<Camera>();
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 460;

            ApplyFont(_chapter, sharedFont, 18f, _theme.AccentColor);
            ApplyFont(_speaker, sharedFont, _theme.SpeakerFontSize, _theme.SpeakerColor);
            ApplyFont(_body, sharedFont, _theme.DialogueFontSize, _theme.TextColor);
            ApplyFont(_page, sharedFont, 18f, _theme.DetailTextColor);
            ApplyFont(_continueLabel, sharedFont, 21f, _theme.TextColor);
            ApplyFont(_restartLabel, sharedFont, 18f, _theme.DetailTextColor);
            ApplyFont(_actionHint, sharedFont, _theme.DetailFontSize, _theme.TextColor);
            ApplyFont(_replayLabel, sharedFont, 19f, _theme.SpeakerColor);
            ApplyFont(_deferLabel, sharedFont, 19f, _theme.TextColor);
            _deferLabel.text = "稍后再学";

            _body.enableAutoSizing = false;
            _body.enableWordWrapping = true;
            _body.overflowMode = TextOverflowModes.Overflow;
            _panelImage.color = _theme.PanelColor;
            _innerPanelImage.color = new Color(
                Mathf.Clamp01(_theme.PanelColor.r + 0.018f),
                Mathf.Clamp01(_theme.PanelColor.g + 0.025f),
                Mathf.Clamp01(_theme.PanelColor.b + 0.02f),
                0.995f);
            _accentImage.color = _theme.AccentColor;
            _portraitImage.color = Color.white;
            _portraitImage.preserveAspect = true;
            ApplyContractPresentation();
            _confirmationAudio = gameObject.AddComponent<AudioSource>();
            _confirmationAudio.playOnAwake = false;
            _confirmationAudio.spatialBlend = 0f;
            _confirmationAudio.volume = .3f;
            _confirmationAudio.dopplerLevel = 0f;

            _continueButton.onClick.AddListener(HandleContinueSelected);
            _restartButton.onClick.AddListener(HandleRestartSelected);
            _replayButton.onClick.AddListener(HandleReplaySelected);
            _deferButton.onClick.AddListener(HandleDeferSelected);
            _continuePokeTarget.Selected += HandleContinuePoke;
            _restartPokeTarget.Selected += HandleReplayPoke;
            _replayPokeTarget.Selected += HandleReplayPoke;
            _gazeRegistration = gazeSurfaces.RegisterGazeSurface(_canvas.transform, 460, "VisitorDialogue");

            _group.alpha = 0f;
            SetInteraction(false, false, false);
            _dialogueRoot.SetActive(false);
            _actionRoot.SetActive(false);
            gameObject.SetActive(false);
            _configured = true;
        }

        public void SetInputMode(VisitorDialogueInputMode mode)
        {
            if (_disposed || _inputMode == mode) return;
            _inputMode = mode;
            _gazeRegistration?.Invalidate();
            if (!_configured) return;
            var dialogue = _targetVisible && _state?.Mode == VisitorDialogueSurfaceMode.Dialogue;
            var replay = _targetVisible && _state?.Mode == VisitorDialogueSurfaceMode.Replay;
            SetInteraction(dialogue, dialogue, replay);
        }

        long _guidanceRevision;
        public void PresentGuidance(VisitorGuidanceSurface surface)
        {
            var context = new VisitorDialogueContextId("map-guidance");
            if (surface.Kind == VisitorGuidanceSurfaceKind.Hidden) { Hide(context); return; }
            var departure = surface.Kind == VisitorGuidanceSurfaceKind.Departure;
            var discovery = surface.Kind == VisitorGuidanceSurfaceKind.Discovery || surface.Kind == VisitorGuidanceSurfaceKind.DiscoveryFailed;
            var body = discovery ? (surface.Kind == VisitorGuidanceSurfaceKind.DiscoveryFailed
                ? "这一页暂时没能打开。留在这里，点重试即可。"
                : $"已到达 {surface.TargetTitle}。翻开这一页，一起记录新的见闻。") : departure ? string.Format(_theme.GuidanceDepartureCopy, surface.TargetTitle) : _theme.GuidanceUnavailableCopy;
            if (_state?.Context == context && _state.Body == body) return;
            SetInputMode(VisitorDialogueInputMode.HandPoke);
            Present(new VisitorDialogueSurfaceState(++_guidanceRevision, context, VisitorDialogueOwner.Guidance,
                VisitorDialogueSurfaceMode.Dialogue,
                discovery ? "见闻册 · 新的一页" : _theme.GuidanceChapter, _theme.FairySpeakerName, body, 0, 1,
                primaryActionLabel: discovery ? (surface.Kind == VisitorGuidanceSurfaceKind.DiscoveryFailed ? "重试" : "开启发现") : departure ? _theme.GuidanceDepartureLabel : _theme.GuidanceEndLabel,
                allowDefer: false, allowRestart: false,
                primaryIntent: departure || discovery ? VisitorDialogueIntentKind.Advance : VisitorDialogueIntentKind.Dismiss,
                secondaryIntent: VisitorDialogueIntentKind.Dismiss));
        }

        void ApplyDialogueTheme()
        {
            _panelImage.color = _theme.PanelColor;
            _innerPanelImage.color = new Color(
                Mathf.Clamp01(_theme.PanelColor.r + .018f), Mathf.Clamp01(_theme.PanelColor.g + .025f),
                Mathf.Clamp01(_theme.PanelColor.b + .02f), .995f);
            _body.color = _theme.TextColor;
            _speaker.color = _theme.TextColor;
            _chapter.color = new Color(_theme.AccentColor.r, _theme.AccentColor.g, _theme.AccentColor.b, .75f);
            _page.color = new Color(_theme.DetailTextColor.r, _theme.DetailTextColor.g, _theme.DetailTextColor.b, .45f);
        }

        public void Present(VisitorDialogueSurfaceState state)
        {
            if (_disposed || !_configured) return;
            if (state == null) throw new ArgumentNullException(nameof(state));
            ApplyDialogueTheme();
            var previous = _state;
            var previousContext = previous?.Context ?? default;
            var contextChanged = previousContext != state.Context;
            var pageChanged = previous == null || contextChanged || previous.Mode != state.Mode ||
                              previous.PageIndex != state.PageIndex || previous.PageCount != state.PageCount ||
                              !string.Equals(previous.Chapter, state.Chapter, StringComparison.Ordinal) ||
                              !string.Equals(previous.Speaker, state.Speaker, StringComparison.Ordinal) ||
                              !string.Equals(previous.PrimaryActionLabel, state.PrimaryActionLabel, StringComparison.Ordinal) ||
                              !string.Equals(previous.SecondaryActionLabel, state.SecondaryActionLabel, StringComparison.Ordinal) ||
                              previous.AllowRestart != state.AllowRestart || previous.Expression != state.Expression ||
                              !string.Equals(previous.Body, state.Body, StringComparison.Ordinal);
            _state = state;
            _deferLabel.text = string.IsNullOrWhiteSpace(state.SecondaryActionLabel) ? "稍后再学" : state.SecondaryActionLabel;
            _deferButton.gameObject.SetActive(state.AllowDefer);
            _deferButton.interactable = state.AllowDefer && _inputMode == VisitorDialogueInputMode.HandPoke;
            if (previous?.AllowDefer != state.AllowDefer) _gazeRegistration.Invalidate();
            if (pageChanged) _gazeRegistration.Invalidate();
            if (contextChanged)
            {
                var pose = WorldSurfacePlacement.CreateViewerFrontPose(
                    _viewer, _theme.ViewerDistance, _theme.DialogueVerticalOffset);
                transform.SetPositionAndRotation(pose.position, pose.rotation);
                var fairyPosition = pose.position +
                                    pose.rotation * Vector3.right * _theme.FairyDialogueHorizontalOffset.x +
                                    pose.rotation * Vector3.forward * _theme.FairyDialogueHorizontalOffset.y;
                // Only horizontal placement belongs to the reading composition.
                // The Fairy driver resolves Y from its explicitly injected XR floor.
                fairyPosition.y = 0f;
                CurrentComposition = new VisitorDialogueComposition(
                    state.Context, state.Mode, pose, fairyPosition, state.Expression);
            }
            else
            {
                CurrentComposition = CurrentComposition.WithPresentation(state.Mode, state.Expression);
            }

            gameObject.SetActive(true);
            _targetVisible = true;
            if (state.Mode == VisitorDialogueSurfaceMode.Dialogue)
            {
                if (pageChanged) RenderDialogue(state);
                else SetDialogueVisible(true);
            }
            else
            {
                if (pageChanged) RenderReplay(state);
                else UpdateReplayCopy(state);
            }
            SurfaceStateChanged?.Invoke(state);
            // A surface subscriber may synchronously replace or hide this context.
            // Publish the current composition, never the stale incoming page.
            CompositionChanged?.Invoke(CurrentComposition);
        }

        public void Hide(VisitorDialogueContextId context)
        {
            if (_disposed || !_configured || _state == null || _state.Context != context) return;
            _state = null;
            _gazeRegistration.Invalidate();
            CurrentComposition = null;
            _targetVisible = false;
            SetDialogueVisible(false);
            SetInteraction(false, false, false);
            SurfaceStateChanged?.Invoke(null);
            CompositionChanged?.Invoke(CurrentComposition);
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (_disposed || !_configured || unscaledDeltaSeconds < 0f ||
                float.IsNaN(unscaledDeltaSeconds) || float.IsInfinity(unscaledDeltaSeconds))
                return;

            var target = _targetVisible ? 1f : 0f;
            var speed = 1f / Mathf.Max(0.01f, _theme.FadeSeconds);
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, unscaledDeltaSeconds * speed);
            if (!_targetVisible && _group.alpha <= 0f)
            {
                _dialogueRoot.SetActive(false);
                _actionRoot.SetActive(false);
                // Let the accepted action's short tail finish after the visual/input surface has closed.
                if (_confirmationAudio == null || !_confirmationAudio.isPlaying) gameObject.SetActive(false);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_continueButton != null) _continueButton.onClick.RemoveListener(HandleContinueSelected);
            if (_restartButton != null) _restartButton.onClick.RemoveListener(HandleRestartSelected);
            if (_replayButton != null) _replayButton.onClick.RemoveListener(HandleReplaySelected);
            if (_deferButton != null) _deferButton.onClick.RemoveListener(HandleDeferSelected);
            if (_continuePokeTarget != null) _continuePokeTarget.Selected -= HandleContinuePoke;
            if (_restartPokeTarget != null) _restartPokeTarget.Selected -= HandleReplayPoke;
            if (_replayPokeTarget != null) _replayPokeTarget.Selected -= HandleReplayPoke;
            _gazeRegistration?.Dispose();
            _gazeRegistration = null;
            _state = null;
            _targetVisible = false;
            SetDialogueVisible(false);
            SetInteraction(false, false, false);
            if (_group != null) _group.alpha = 0f;
            if (gameObject != null) gameObject.SetActive(false);
            CurrentComposition = null;
            CompositionChanged?.Invoke(null);
            SurfaceStateChanged?.Invoke(null);
            IntentRequested = null;
            DialogueVisibilityChanged = null;
            SurfaceStateChanged = null;
            CompositionChanged = null;
        }

        internal bool IsDialogueVisible => _dialogueVisible;
        internal bool IsReplayVisible => _state != null && _state.Mode == VisitorDialogueSurfaceMode.Replay;
        internal VisitorDialogueContextId CurrentContext => _state?.Context ?? default;
        internal int VisibleCharacterCount => _body != null ? _body.maxVisibleCharacters : 0;
        internal int BodyCharacterCount => _bodyCharacterCount;
        internal string DisplayedBody => _body != null ? _body.text : string.Empty;
        internal string DisplayedPage => _page != null ? _page.text : string.Empty;
        internal bool BodyFits => FitsBody();

        internal bool ConfirmForTest(float now)
            => Confirm(now, bypassFrameGate: true);

        internal bool ReplayForTest(float now)
            => RequestReplay(now, bypassFrameGate: true);

        void RenderDialogue(VisitorDialogueSurfaceState state)
        {
            _portraitImage.sprite = state.Expression switch
            {
                VisitorDialogueExpression.Welcome => _theme.WelcomePortrait,
                VisitorDialogueExpression.Wonder => _theme.WonderPortrait,
                _ => _theme.ListeningPortrait
            };
            _portrait.gameObject.SetActive(_portraitImage.sprite != null);
            _dialogueRoot.SetActive(true);
            _actionRoot.SetActive(false);
            _chapter.text = string.IsNullOrWhiteSpace(state.Chapter) ? "同行见闻" : state.Chapter;
            _speaker.text = string.IsNullOrWhiteSpace(state.Speaker) ? "小精灵" : state.Speaker;
            _body.text = state.Body;
            _body.ForceMeshUpdate(true, true);
            _bodyCharacterCount = _body.textInfo.characterCount;
            _page.text = $"{state.PageIndex + 1}/{state.PageCount}";
            _continueLabel.text = !string.IsNullOrWhiteSpace(state.PrimaryActionLabel)
                ? state.PrimaryActionLabel : state.IsFinalPage ? "好，我们走吧" : "继续  ›";
            _restartLabel.text = string.IsNullOrWhiteSpace(state.SecondaryActionLabel) ? "从头回看" : state.SecondaryActionLabel;
            _restartButton.gameObject.SetActive(state.AllowRestart);
            ((RectTransform)_continueButton.transform).anchoredPosition = new Vector2(state.AllowRestart ? 247f : 100f, -130f);
            ((RectTransform)_continueButton.transform).sizeDelta = new Vector2(state.AllowRestart ? 292f : 580f, 58f);
            _body.maxVisibleCharacters = int.MaxValue;
            _inputReadyAt = Time.unscaledTime + _theme.ConfirmDebounceSeconds;
            _lastAcceptedFrame = -1;
            SetInteraction(true, true, false);
            SetDialogueVisible(true);

            if (!FitsBody())
                throw new InvalidOperationException(
                    $"Dialogue '{state.Context}' page {state.PageIndex + 1} exceeds the authored body region.");
        }

        void RenderReplay(VisitorDialogueSurfaceState state)
        {
            _portrait.gameObject.SetActive(false);
            _dialogueRoot.SetActive(false);
            _actionRoot.SetActive(true);
            UpdateReplayCopy(state);
            _inputReadyAt = Time.unscaledTime + _theme.ConfirmDebounceSeconds;
            _lastAcceptedFrame = -1;
            SetInteraction(false, false, true);
            SetDialogueVisible(false);
        }

        void UpdateReplayCopy(VisitorDialogueSurfaceState state)
        {
            _actionHint.text = state.ActionHint;
            _actionHint.gameObject.SetActive(!string.IsNullOrWhiteSpace(state.ActionHint));
            _replayLabel.text = string.IsNullOrWhiteSpace(state.PrimaryActionLabel) ? "重看说明" : state.PrimaryActionLabel;
        }

        void ApplyContractPresentation()
        {
            _panelImage.enabled = false;
            _innerPanelImage.enabled = false;
            _accentImage.enabled = false;
            _dialogueFinish = CreateContractGraphic(_panel, false);
            var actionBackground = _actionRoot.GetComponent<Image>();
            if (actionBackground != null) actionBackground.enabled = false;
            CreateContractGraphic((RectTransform)_actionRoot.transform, false);
            _portrait.anchoredPosition = new Vector2(-327f, -4f);
            _portrait.sizeDelta = new Vector2(225f, 225f);
            _speaker.rectTransform.anchoredPosition = new Vector2(-320f, 105f);
            _speaker.rectTransform.sizeDelta = new Vector2(180f, 32f);
            _speaker.alignment = TextAlignmentOptions.Center;
            _chapter.color = new Color(_theme.AccentColor.r, _theme.AccentColor.g, _theme.AccentColor.b, .75f);
            _page.color = new Color(_theme.DetailTextColor.r, _theme.DetailTextColor.g, _theme.DetailTextColor.b, .45f);
            _body.rectTransform.anchoredPosition = new Vector2(100f, 15f);
            _body.rectTransform.sizeDelta = new Vector2(610f, 170f);
            StyleChoice(_continueButton, _continueLabel);
            StyleChoice(_restartButton, _restartLabel);
            StyleChoice(_replayButton, _replayLabel);
            StyleChoice(_deferButton, _deferLabel);
            ((RectTransform)_restartButton.transform).anchoredPosition = new Vector2(-57f, -130f);
            ((RectTransform)_restartButton.transform).sizeDelta = new Vector2(292f, 58f);
        }

        DialogueContractGraphic CreateContractGraphic(RectTransform parent, bool choice)
        {
            var child = new GameObject("ContractFinish", typeof(RectTransform), typeof(CanvasRenderer), typeof(DialogueContractGraphic));
            var rect = (RectTransform)child.transform;
            rect.SetParent(parent, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var graphic = child.GetComponent<DialogueContractGraphic>();
            graphic.Initialize(_theme.AccentColor, choice);
            return graphic;
        }

        void StyleChoice(Button button, TMP_Text label)
        {
            var oldImage = button.GetComponent<Image>();
            if (oldImage != null) oldImage.enabled = false;
            var progress = button.transform.Find("GazeProgress");
            if (progress != null) progress.gameObject.SetActive(false);
            var graphic = CreateContractGraphic((RectTransform)button.transform, true);
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.None;
            button.gameObject.AddComponent<DialogueChoiceFeedback>().Initialize(graphic, button);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(36f, 5f); label.rectTransform.offsetMax = new Vector2(-12f, -5f);
            label.fontSize = 21f;
            label.color = _theme.TextColor;
        }

        void HandleContinueSelected() => SubmitInput(VisitorDialogueIntentKind.Advance, VisitorDialogueInputMode.HandPoke, Time.unscaledTime);
        void HandleRestartSelected() => HandleReplaySelected();
        void HandleReplaySelected() => SubmitInput(VisitorDialogueIntentKind.Replay, VisitorDialogueInputMode.HandPoke, Time.unscaledTime);
        void HandleDeferSelected() => SubmitInput(VisitorDialogueIntentKind.Defer, VisitorDialogueInputMode.HandPoke, Time.unscaledTime);
        void HandleContinuePoke() => SubmitInput(VisitorDialogueIntentKind.Advance, VisitorDialogueInputMode.HandPoke, Time.unscaledTime);
        void HandleReplayPoke() => SubmitInput(VisitorDialogueIntentKind.Replay, VisitorDialogueInputMode.HandPoke, Time.unscaledTime);

        internal bool SubmitInput(VisitorDialogueIntentKind intent, VisitorDialogueInputMode source, float now)
        {
            if (source != _inputMode || source == VisitorDialogueInputMode.Unavailable) return false;
            if (intent == VisitorDialogueIntentKind.Defer) return RequestDefer(now, false);
            return intent == VisitorDialogueIntentKind.Advance ? Confirm(now, false) : RequestReplay(now, false);
        }

        internal bool DeferForTest(float now) => RequestDefer(now, true);

        bool RequestDefer(float now, bool bypassFrameGate)
        {
            if (_disposed || _inputMode != VisitorDialogueInputMode.HandPoke || _state == null ||
                !_state.AllowDefer || now < _inputReadyAt || !AcceptFrame(bypassFrameGate)) return false;
            _gazeRegistration.Invalidate();
            IntentRequested?.Invoke(new VisitorDialogueIntent(_state.Context, _state.Owner, VisitorDialogueIntentKind.Defer,
                _state.Revision, VisitorDialogueInputOrigin.HandPoke));
            return true;
        }

        void OnDisable() => _gazeRegistration?.Invalidate();

        bool Confirm(float now, bool bypassFrameGate)
        {
            if (_disposed || _inputMode == VisitorDialogueInputMode.Unavailable ||
                _state == null || _state.Mode != VisitorDialogueSurfaceMode.Dialogue ||
                now < _inputReadyAt || !AcceptFrame(bypassFrameGate))
                return false;
            _gazeRegistration.Invalidate();
            IntentRequested?.Invoke(new VisitorDialogueIntent(
                _state.Context,
                _state.Owner,
                _state.PrimaryIntent, _state.Revision,
                _inputMode == VisitorDialogueInputMode.HeadGaze ? VisitorDialogueInputOrigin.GazeDwell : VisitorDialogueInputOrigin.HandPoke));
            return true;
        }

        bool RequestReplay(float now, bool bypassFrameGate)
        {
            if (_disposed || _inputMode == VisitorDialogueInputMode.Unavailable ||
                _state == null || (_state.Mode == VisitorDialogueSurfaceMode.Dialogue && !_state.AllowRestart) ||
                now < _inputReadyAt || !AcceptFrame(bypassFrameGate)) return false;
            _gazeRegistration.Invalidate();
            IntentRequested?.Invoke(new VisitorDialogueIntent(
                _state.Context,
                _state.Owner,
                _state.SecondaryIntent, _state.Revision,
                _inputMode == VisitorDialogueInputMode.HeadGaze ? VisitorDialogueInputOrigin.GazeDwell : VisitorDialogueInputOrigin.HandPoke));
            return true;
        }

        bool AcceptFrame(bool bypassFrameGate)
        {
            if (bypassFrameGate) return true;
            if (_lastAcceptedFrame == Time.frameCount) return false;
            _lastAcceptedFrame = Time.frameCount;
            if (Application.isPlaying && _theme.ConfirmSound != null)
            {
                _confirmationAudio.Stop();
                _confirmationAudio.PlayOneShot(_theme.ConfirmSound);
            }
            return true;
        }

        bool FitsBody()
        {
            if (_body == null) return false;
            var rect = _body.rectTransform.rect;
            var preferred = _body.GetPreferredValues(_body.text, Mathf.Max(1f, rect.width), 0f);
            return preferred.y <= rect.height + 0.5f;
        }

        void SetInteraction(bool continueArmed, bool restartArmed, bool replayArmed)
        {
            var available = _inputMode != VisitorDialogueInputMode.Unavailable;
            continueArmed &= available;
            restartArmed &= available && _state?.AllowRestart != false;
            replayArmed &= available;
            _group.interactable = continueArmed || restartArmed || replayArmed;
            _group.blocksRaycasts = _group.interactable && _inputMode == VisitorDialogueInputMode.HeadGaze;
            _continueButton.interactable = continueArmed;
            _restartButton.interactable = restartArmed;
            _replayButton.interactable = replayArmed;
            if (_deferButton != null)
                _deferButton.interactable = _targetVisible && _state?.AllowDefer == true &&
                    _inputMode == VisitorDialogueInputMode.HandPoke;
            var hand = _inputMode == VisitorDialogueInputMode.HandPoke;
            _continuePokeTarget.SetArmed(continueArmed && hand);
            _restartPokeTarget.SetArmed(restartArmed && hand);
            _replayPokeTarget.SetArmed(replayArmed && hand);
        }

        void SetDialogueVisible(bool visible)
        {
            if (_dialogueVisible == visible) return;
            _dialogueVisible = visible;
            DialogueVisibilityChanged?.Invoke(visible);
        }

        static void ApplyFont(TMP_Text text, TMP_FontAsset font, float size, Color color)
        {
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.raycastTarget = false;
        }

        void ValidateSerializedReferences()
        {
            if (_canvas == null || _group == null || _portrait == null || _portraitImage == null ||
                _dialogueRoot == null || _panel == null || _panelImage == null ||
                _innerPanel == null || _innerPanelImage == null || _accent == null || _accentImage == null ||
                _chapter == null || _speaker == null || _body == null || _page == null ||
                _continueButton == null || _continueLabel == null || _continuePokeTarget == null ||
                _restartButton == null || _restartLabel == null || _restartPokeTarget == null ||
                _actionRoot == null || _actionHint == null || _replayButton == null ||
                _replayLabel == null || _replayPokeTarget == null || _deferButton == null || _deferLabel == null)
                throw new InvalidOperationException(
                    "Visitor dialogue prefab is missing authored stage, copy, or interaction references.");
        }
    }
}
