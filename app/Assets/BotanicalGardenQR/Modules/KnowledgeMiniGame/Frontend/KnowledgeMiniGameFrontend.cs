using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.KnowledgeMiniGame.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.KnowledgeMiniGame.Frontend
{
    public enum ObservationCompletionExitKind
    {
        ReturnToChoices = 0,
        AcknowledgeCompletion = 1
    }

    [Serializable]
    public sealed class KnowledgeMiniGameAnswerView
    {
        [SerializeField] GameObject _root;
        [SerializeField] Button _button;
        [SerializeField] TMP_Text _label;
        [SerializeField] Image _stateBar;

        public GameObject Root => _root;
        public Button Button => _button;
        public TMP_Text Label => _label;
        public Image StateBar => _stateBar;

        public void Validate(int index)
        {
            if (_root == null || _button == null || _label == null || _stateBar == null)
                throw new InvalidOperationException($"Observation completion answer slot {index} is incomplete.");
        }
    }

    /// <summary>
    /// Thin binding for the authored ObservationCompletionSurface prefab.
    /// Fixed hierarchy and visuals are serialized assets; this presenter enforces
    /// the gaze-only input contract for its semantic controls at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnowledgeMiniGameFrontend : MonoBehaviour, IKnowledgeMiniGameStateSink, IDisposable
    {
        [Header("Authored surface")]
        [SerializeField] GameObject _surfaceRoot;
        [SerializeField] Canvas _canvas;
        [SerializeField] CanvasGroup _group;
        [SerializeField] TMP_Text _question;
        [SerializeField] TMP_Text _feedback;
        [SerializeField] TMP_Text _progress;
        [SerializeField] KnowledgeMiniGameAnswerView[] _answers = Array.Empty<KnowledgeMiniGameAnswerView>();
        [SerializeField] Button _confirmationButton;
        [SerializeField] Button _returnButton;
        [SerializeField] Button _deferButton;
        [SerializeField] TMP_Text _returnLabel;
        [SerializeField] Image _challengeProgress;

        [Header("Completion presentation")]
        [SerializeField] Vector2 _completedFeedbackPosition = new Vector2(0f, -10f);
        [SerializeField] Vector2 _completedFeedbackSize = new Vector2(800f, 230f);
        [SerializeField, Min(20f)] float _completedFeedbackFontSize = 30f;

        [Header("Placement")]
        [SerializeField, Min(0.2f)] float _viewerDistance = .45f;
        [SerializeField] float _verticalOffset = -0.04f;
        [SerializeField] int _sortingOrder = 430;
        [SerializeField] int _gazePriority = 430;

        [Header("State colours")]
        [SerializeField] Color _neutralState = new Color(0.12f, 0.78f, 0.63f, 0.16f);
        [SerializeField] Color _correctState = new Color(0.24f, 0.95f, 0.66f, 1f);
        [SerializeField] Color _incorrectState = new Color(1f, 0.42f, 0.32f, 1f);
        [SerializeField] Color _secondaryText = new Color(0.78f, 0.88f, 0.86f, 0.88f);

        Transform _viewer;
        IKnowledgeMiniGameController _controller;
        IDisposable _stateSubscription;
        IFrontendGazeSurfaceRegistration _gazeRegistration;
        KnowledgeMiniGameState _state;
        SessionToken _session;
        bool _requestedVisible;
        bool _visible;
        bool _configured;
        bool _disposed;
        Vector2 _feedbackPosition;
        Vector2 _feedbackSize;
        float _feedbackFontSize;

        public event Action<ObservationCompletionExitKind> ExitRequested;
        public event Action<bool> SurfaceVisibilityChanged;

        public bool IsVisible => _surfaceRoot != null && _surfaceRoot.activeSelf;

        public void Configure(Transform viewer, IFrontendGazeSurfaceRegistry gazeSurfaces)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KnowledgeMiniGameFrontend));
            if (_configured) throw new InvalidOperationException("Observation completion presenter is already configured.");
            ValidateBindings();
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            if (!WorldSurfacePlacement.HasPositiveScaleChain(_surfaceRoot.transform))
                throw new InvalidOperationException("Observation completion surface requires a positive scale chain.");

            _canvas.worldCamera = viewer.GetComponent<Camera>();
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = _sortingOrder;
            // The authored presentation root is deliberately a non-Canvas wrapper so
            // placement can move the whole surface.  Gaze registration must therefore
            // use the explicit world-space Canvas, not its parent wrapper; the registry
            // requires a Canvas and GraphicRaycaster on the target or an ancestor.
            _gazeRegistration = (gazeSurfaces ?? throw new ArgumentNullException(nameof(gazeSurfaces)))
                .RegisterGazeSurface(_canvas.transform, _gazePriority, "ObservationCompletionSurface");
            ConfigureGazeOnlyControls();
            BindButtons();
            _feedbackPosition = _feedback.rectTransform.anchoredPosition;
            _feedbackSize = _feedback.rectTransform.sizeDelta;
            _feedbackFontSize = _feedback.fontSize;
            _configured = true;
            HideImmediate();
        }

        public void Bind(SessionToken session, IKnowledgeMiniGameController controller)
        {
            RequireConfigured();
            if (!session.IsValid) throw new ArgumentException("A valid session is required.", nameof(session));
            if (_stateSubscription != null)
                throw new InvalidOperationException("Observation completion presenter is already bound.");

            _session = session;
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _stateSubscription = _controller.Observe(this);
        }

        public void SetVisible(bool visible)
        {
            if (_disposed) return;
            _requestedVisible = visible;
            ApplyVisibility();
        }

        public void Publish(KnowledgeMiniGameState state)
        {
            if (_disposed || state == null) return;
            if (state.Session != _session || !state.Session.IsValid) return;
            if (_state != null && state.Version <= _state.Version) return;
            _state = state;
            if (state.Phase != KnowledgeMiniGamePhase.Closed) Render(state);
            ApplyVisibility();
        }

        public void Unbind()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;
            _controller = null;
            _state = null;
            _session = default;
            _requestedVisible = false;
            HideImmediate();
        }

        public void Unconfigure()
        {
            if (_disposed) return;
            Unbind();
            UnbindButtons();
            _gazeRegistration?.Dispose();
            _gazeRegistration = null;
            _viewer = null;
            _configured = false;
            ExitRequested = null;
            SurfaceVisibilityChanged = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Unconfigure();
            _disposed = true;
        }

        void OnDestroy() => Dispose();

        void BindButtons()
        {
            _confirmationButton.onClick.AddListener(SubmitConfirmation);
            _returnButton.onClick.AddListener(ReturnToChoices);
            if (_answers.Length > 0) _answers[0].Button.onClick.AddListener(SubmitAnswer0);
            if (_answers.Length > 1) _answers[1].Button.onClick.AddListener(SubmitAnswer1);
            if (_answers.Length > 2) _answers[2].Button.onClick.AddListener(SubmitAnswer2);
            if (_answers.Length > 3) _answers[3].Button.onClick.AddListener(SubmitAnswer3);
        }

        void ConfigureGazeOnlyControls()
        {
            ConfigureGazeOnlyControl(_confirmationButton);
            ConfigureGazeOnlyControl(_returnButton);
            ConfigureGazeOnlyControl(_deferButton);
            for (var index = 0; index < _answers.Length; index++)
                ConfigureGazeOnlyControl(_answers[index]?.Button);
        }

        static void ConfigureGazeOnlyControl(Button button)
        {
            if (button == null) return;
            if (button.GetComponent<IFrontendGazeProgressPresenter>() == null)
                throw new InvalidOperationException(
                    $"Gaze-only control '{button.name}' requires an authored gaze progress presenter.");

            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;

            var graphics = button.GetComponentsInChildren<Graphic>(true);
            for (var index = 0; index < graphics.Length; index++)
                if (graphics[index] != null) graphics[index].raycastTarget = false;
        }

        void UnbindButtons()
        {
            if (_confirmationButton != null) _confirmationButton.onClick.RemoveListener(SubmitConfirmation);
            if (_returnButton != null) _returnButton.onClick.RemoveListener(ReturnToChoices);
            if (_answers.Length > 0 && _answers[0]?.Button != null) _answers[0].Button.onClick.RemoveListener(SubmitAnswer0);
            if (_answers.Length > 1 && _answers[1]?.Button != null) _answers[1].Button.onClick.RemoveListener(SubmitAnswer1);
            if (_answers.Length > 2 && _answers[2]?.Button != null) _answers[2].Button.onClick.RemoveListener(SubmitAnswer2);
            if (_answers.Length > 3 && _answers[3]?.Button != null) _answers[3].Button.onClick.RemoveListener(SubmitAnswer3);
        }

        void SubmitAnswer0() => SubmitAnswer(0);
        void SubmitAnswer1() => SubmitAnswer(1);
        void SubmitAnswer2() => SubmitAnswer(2);
        void SubmitAnswer3() => SubmitAnswer(3);

        void SubmitAnswer(int optionIndex)
        {
            var state = _state;
            if (!CanAcceptInput || _controller == null || state == null || !state.CanSubmit ||
                state.Definition.Kind != KnowledgeMiniGameKind.SingleChoice ||
                state.CurrentQuestion == null ||
                optionIndex < 0 || optionIndex >= state.CurrentQuestion.Options.Count)
                return;
            _controller.Submit(_session, state.CurrentQuestion.Options[optionIndex].AnswerId);
        }

        void SubmitConfirmation()
        {
            if (!CanAcceptInput || _controller == null || _state == null || !_state.CanSubmit ||
                _state.Definition.Kind != KnowledgeMiniGameKind.Confirmation)
                return;
            _controller.Submit(_session, "confirm");
        }

        bool CanAcceptInput => !_disposed && _requestedVisible && _visible &&
                               _surfaceRoot != null && _surfaceRoot.activeInHierarchy &&
                               _state != null && _state.Session == _session &&
                               _state.Phase != KnowledgeMiniGamePhase.Closed;

        void ReturnToChoices()
        {
            if (!CanAcceptInput) return;
            ExitRequested?.Invoke(_state.IsCompleted
                ? ObservationCompletionExitKind.AcknowledgeCompletion
                : ObservationCompletionExitKind.ReturnToChoices);
        }

        void Render(KnowledgeMiniGameState state)
        {
            var definition = state.Definition;
            var confirmation = definition.Kind == KnowledgeMiniGameKind.Confirmation;
            var question = state.CurrentQuestion;
            var completed = state.IsCompleted;
            var solvedCount = completed ? state.QuestionCount : state.QuestionIndex;
            _question.text = completed
                ? confirmation ? "观察完成" : "挑战完成！"
                : confirmation ? definition.ConfirmationPrompt : question.Question;
            _progress.text = confirmation
                ? completed ? "已完成观察" : "观察确认"
                : completed
                    ? $"已解开 {solvedCount} / {state.QuestionCount}"
                    : $"第 {state.QuestionIndex + 1} / {state.QuestionCount} 题 · 已解开 {solvedCount}";
            _challengeProgress.fillAmount = (float)solvedCount / state.QuestionCount;
            _confirmationButton.gameObject.SetActive(confirmation && !completed);
            _confirmationButton.interactable = confirmation && state.CanSubmit;
            _returnButton.gameObject.SetActive(true);
            _returnButton.interactable = true;
            _returnLabel.text = completed ? "继续探索" : "返回选择";
            _deferButton.gameObject.SetActive(false);
            _feedback.rectTransform.anchoredPosition = completed ? _completedFeedbackPosition : _feedbackPosition;
            _feedback.rectTransform.sizeDelta = completed ? _completedFeedbackSize : _feedbackSize;
            _feedback.fontSize = completed ? _completedFeedbackFontSize : _feedbackFontSize;
            LayoutAnswerCards(confirmation || completed ? 0 : question.Options.Count);

            for (var index = 0; index < _answers.Length; index++)
            {
                var view = _answers[index];
                var active = !confirmation && !completed && index < question.Options.Count;
                view.Root.SetActive(active);
                if (!active) continue;

                var option = question.Options[index];
                view.Label.text = $"{(char)('A' + index)}  ·  {option.Text}";
                view.Button.interactable = state.CanSubmit;
                var selected = string.Equals(state.SelectedAnswerId, option.AnswerId, StringComparison.Ordinal);
                view.StateBar.color = selected
                    ? state.IsCompleted ? _correctState : _incorrectState
                    : _neutralState;
            }

            switch (state.Phase)
            {
                case KnowledgeMiniGamePhase.Incorrect:
                    _feedback.text = "再想一想 · " + state.Feedback;
                    _feedback.color = _incorrectState;
                    break;
                case KnowledgeMiniGamePhase.Completed:
                    _feedback.text = state.Feedback;
                    _feedback.color = _correctState;
                    break;
                default:
                    if (!string.IsNullOrWhiteSpace(state.Feedback))
                    {
                        _feedback.text = "上一题答对了 · " + state.Feedback;
                        _feedback.color = _correctState;
                    }
                    else
                    {
                        _feedback.text = confirmation
                            ? "准星对准确认卡，停留到进度完成"
                            : "准星对准选项并停留确认 · 答错可以再试";
                        _feedback.color = _secondaryText;
                    }
                    break;
            }
        }

        void LayoutAnswerCards(int activeCount)
        {
            if (activeCount <= 0) return;

            for (var index = 0; index < activeCount && index < _answers.Length; index++)
            {
                if (!(_answers[index].Root.transform is RectTransform rect)) continue;
                switch (activeCount)
                {
                    case 2:
                        rect.anchoredPosition = new Vector2(index == 0 ? -220f : 220f, -12f);
                        break;
                    case 3:
                        rect.anchoredPosition = index < 2
                            ? new Vector2(index == 0 ? -220f : 220f, 55f)
                            : new Vector2(0f, -85f);
                        break;
                    default:
                        rect.anchoredPosition = new Vector2(
                            index % 2 == 0 ? -220f : 220f,
                            index < 2 ? 55f : -85f);
                        break;
                }
            }
        }

        void ApplyVisibility()
        {
            if (_surfaceRoot == null) return;
            var visible = _requestedVisible && _state != null && _state.Phase != KnowledgeMiniGamePhase.Closed;
            if (visible && !_surfaceRoot.activeSelf)
                WorldSurfacePlacement.PlaceViewerFront(_surfaceRoot.transform, _viewer, _viewerDistance, _verticalOffset);
            _group.alpha = visible ? 1f : 0f;
            _group.interactable = visible;
            _group.blocksRaycasts = visible;
            _surfaceRoot.SetActive(visible);
            PublishVisibility(visible);
        }

        void HideImmediate()
        {
            if (_surfaceRoot == null) return;
            if (_group != null)
            {
                _group.alpha = 0f;
                _group.interactable = false;
                _group.blocksRaycasts = false;
            }
            _surfaceRoot.SetActive(false);
            PublishVisibility(false);
        }

        void PublishVisibility(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            SurfaceVisibilityChanged?.Invoke(visible);
        }

        void ValidateBindings()
        {
            if (_surfaceRoot == null || _canvas == null || _group == null || _question == null ||
                _feedback == null || _progress == null || _confirmationButton == null ||
                _returnButton == null || _deferButton == null || _returnLabel == null ||
                _challengeProgress == null)
                throw new InvalidOperationException("Observation completion prefab bindings are incomplete.");
            if (_answers == null || _answers.Length != KnowledgeMiniGameDefinition.MaximumOptionCount)
                throw new InvalidOperationException("Observation completion prefab requires exactly four authored answer slots.");
            for (var index = 0; index < _answers.Length; index++)
                (_answers[index] ?? throw new InvalidOperationException($"Answer slot {index} is missing.")).Validate(index);
            if (_canvas.renderMode != RenderMode.WorldSpace)
                throw new InvalidOperationException("Observation completion requires a world-space Canvas.");
        }

        void RequireConfigured()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(KnowledgeMiniGameFrontend));
            if (!_configured) throw new InvalidOperationException("Observation completion presenter is not configured.");
        }
    }
}
