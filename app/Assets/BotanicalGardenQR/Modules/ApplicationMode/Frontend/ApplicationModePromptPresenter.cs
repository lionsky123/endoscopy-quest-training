using System;
using BotanicalGardenQR.ApplicationMode.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.ApplicationMode.Frontend
{
    public sealed class ApplicationModePromptPresenter : MonoBehaviour, IApplicationModeStateSink
    {
        [Header("Controller and pose")]
        [SerializeField] MonoBehaviour _controllerSource;
        [SerializeField] Transform _viewOrigin;
        [SerializeField] Transform _promptPoseRoot;
        [SerializeField, Min(0.2f)] float _promptDistance = 1.1f;

        [Header("Preauthored roots")]
        [SerializeField] GameObject _promptRoot;
        [SerializeField] GameObject _returnPromptRoot;
        [SerializeField] GameObject _busyRoot;

        [Header("Copy")]
        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _instructionText;
        [SerializeField] TMP_Text _messageText;

        [Header("Preauthored actions")]
        [SerializeField] Button _cancelButton;
        [SerializeField] Button _confirmButton;

        [Header("Background interaction")]
        [SerializeField] CanvasGroup _backgroundCanvasGroup;
        [SerializeField] Selectable[] _blockedBackgroundSelectables = Array.Empty<Selectable>();
        [SerializeField] Behaviour[] _blockedBackgroundBehaviours = Array.Empty<Behaviour>();
        [SerializeField] GameObject[] _hiddenBackgroundObjects = Array.Empty<GameObject>();

        IApplicationModeController _controller;
        IDisposable _subscription;
        bool[] _backgroundSelectableStates;
        bool[] _backgroundBehaviourStates;
        bool[] _backgroundObjectStates;
        bool _backgroundInteractable;
        bool _backgroundBlocksRaycasts;
        bool _backgroundBlocked;
        bool _listenersBound;
        bool _promptVisible;

        void Awake()
        {
            if (_promptRoot != null) _promptRoot.SetActive(false);
        }

        internal void ValidateConfiguration(IApplicationModeController expectedController)
        {
            var controller = _controllerSource as IApplicationModeController;
            if (controller == null ||
                (expectedController != null && !ReferenceEquals(controller, expectedController)))
                throw new InvalidOperationException(
                    "ApplicationMode prompt requires its scene-local controller.");
            if (_viewOrigin == null || _promptPoseRoot == null || _promptRoot == null ||
                _returnPromptRoot == null || _busyRoot == null)
                throw new InvalidOperationException(
                    "ApplicationMode return prompt requires complete pose and semantic state roots.");
            if (!IsPositiveFinite(_promptDistance))
                throw new InvalidOperationException(
                    "ApplicationMode prompt distance must be positive and finite.");
            if (_titleText == null || _instructionText == null || _messageText == null)
                throw new InvalidOperationException(
                    "ApplicationMode return prompt requires complete semantic copy roles.");
            if (_cancelButton == null || _confirmButton == null)
                throw new InvalidOperationException(
                    "ApplicationMode return prompt requires cancel and confirm actions.");

            var canvas = _promptRoot.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace ||
                _promptRoot.GetComponent<GraphicRaycaster>() == null)
                throw new InvalidOperationException(
                    "ApplicationMode prompt requires a World-Space Canvas input surface.");
        }

        void OnEnable()
        {
            _controller = _controllerSource as IApplicationModeController;
            if (_controller == null)
            {
                Debug.LogError("[ApplicationMode] MODE_PROMPT_CONTROLLER_MISSING", this);
                return;
            }

            BindListeners();
            _subscription = _controller.Observe(this);
        }

        void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
            UnbindListeners();
            SetBackgroundBlocked(false);
            _promptVisible = false;
            if (_promptRoot != null) _promptRoot.SetActive(false);
            _controller = null;
        }

        public void OnApplicationModeStateChanged(ApplicationModeState state)
        {
            var visible = state.Prompt == ApplicationModePromptKind.ReturnToVisitor;
            if (visible && !_promptVisible) CapturePromptPose();
            _promptVisible = visible;

            if (_promptRoot != null) _promptRoot.SetActive(visible);
            if (_returnPromptRoot != null) _returnPromptRoot.SetActive(visible);
            if (_busyRoot != null)
                _busyRoot.SetActive(visible && state.Phase == ApplicationModePhase.LoadingScene);

            SetBackgroundBlocked(state.IsInteractionBlocked);
            RenderCopy(state);
            if (_cancelButton != null) _cancelButton.interactable = state.CanCancel;
            if (_confirmButton != null) _confirmButton.interactable = state.CanConfirm;
        }

        void CapturePromptPose()
        {
            if (_viewOrigin == null || _promptPoseRoot == null) return;

            var forward = Vector3.ProjectOnPlane(_viewOrigin.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = _viewOrigin.forward;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            var position = _viewOrigin.position + forward * Mathf.Max(0.2f, _promptDistance);
            _promptPoseRoot.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        void RenderCopy(ApplicationModeState state)
        {
            if (_titleText != null) _titleText.text = "返回游客模式";
            if (_instructionText != null)
                _instructionText.text = "未完成的锚点操作可能丢失；请使用左手柄射线和左扳机选择";
            if (_messageText == null) return;
            if (state.Phase == ApplicationModePhase.LoadingScene)
                _messageText.text = "正在返回游客模式…";
            else if (state.Fault == ApplicationModeFault.SceneLoadFailed)
                _messageText.text = "暂时无法返回游客模式，请重试";
            else
                _messageText.text = string.Empty;
        }

        void BindListeners()
        {
            if (_listenersBound) return;
            if (_cancelButton != null) _cancelButton.onClick.AddListener(Cancel);
            if (_confirmButton != null) _confirmButton.onClick.AddListener(Confirm);
            _listenersBound = true;
        }

        void UnbindListeners()
        {
            if (!_listenersBound) return;
            if (_cancelButton != null) _cancelButton.onClick.RemoveListener(Cancel);
            if (_confirmButton != null) _confirmButton.onClick.RemoveListener(Confirm);
            _listenersBound = false;
        }

        void Cancel() => _controller?.Dispatch(ApplicationModeIntent.Cancel);
        void Confirm() => _controller?.Dispatch(ApplicationModeIntent.Confirm);

        static bool IsPositiveFinite(float value)
            => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        void CacheBackgroundState()
        {
            if (_backgroundCanvasGroup != null)
            {
                _backgroundInteractable = _backgroundCanvasGroup.interactable;
                _backgroundBlocksRaycasts = _backgroundCanvasGroup.blocksRaycasts;
            }

            var count = _blockedBackgroundSelectables == null
                ? 0
                : _blockedBackgroundSelectables.Length;
            _backgroundSelectableStates = new bool[count];
            for (var i = 0; i < count; i++)
                _backgroundSelectableStates[i] =
                    _blockedBackgroundSelectables[i] != null &&
                    _blockedBackgroundSelectables[i].interactable;

            count = _blockedBackgroundBehaviours == null
                ? 0
                : _blockedBackgroundBehaviours.Length;
            _backgroundBehaviourStates = new bool[count];
            for (var i = 0; i < count; i++)
                _backgroundBehaviourStates[i] =
                    _blockedBackgroundBehaviours[i] != null &&
                    _blockedBackgroundBehaviours[i].enabled;

            count = _hiddenBackgroundObjects == null
                ? 0
                : _hiddenBackgroundObjects.Length;
            _backgroundObjectStates = new bool[count];
            for (var i = 0; i < count; i++)
                _backgroundObjectStates[i] =
                    _hiddenBackgroundObjects[i] != null &&
                    _hiddenBackgroundObjects[i].activeSelf;
        }

        void SetBackgroundBlocked(bool blocked)
        {
            if (blocked == _backgroundBlocked) return;
            if (blocked) CacheBackgroundState();

            if (_backgroundCanvasGroup != null)
            {
                _backgroundCanvasGroup.interactable = blocked ? false : _backgroundInteractable;
                _backgroundCanvasGroup.blocksRaycasts = blocked ? false : _backgroundBlocksRaycasts;
            }

            var selectableCount = _blockedBackgroundSelectables == null
                ? 0
                : _blockedBackgroundSelectables.Length;
            for (var i = 0; i < selectableCount; i++)
            {
                var selectable = _blockedBackgroundSelectables[i];
                if (selectable != null)
                    selectable.interactable = !blocked &&
                                              i < _backgroundSelectableStates.Length &&
                                              _backgroundSelectableStates[i];
            }

            var behaviourCount = _blockedBackgroundBehaviours == null
                ? 0
                : _blockedBackgroundBehaviours.Length;
            for (var i = 0; i < behaviourCount; i++)
            {
                var behaviour = _blockedBackgroundBehaviours[i];
                if (behaviour != null)
                    behaviour.enabled = !blocked &&
                                        i < _backgroundBehaviourStates.Length &&
                                        _backgroundBehaviourStates[i];
            }

            var objectCount = _hiddenBackgroundObjects == null
                ? 0
                : _hiddenBackgroundObjects.Length;
            for (var i = 0; i < objectCount; i++)
            {
                var backgroundObject = _hiddenBackgroundObjects[i];
                if (backgroundObject != null)
                    backgroundObject.SetActive(
                        !blocked &&
                        i < _backgroundObjectStates.Length &&
                        _backgroundObjectStates[i]);
            }
            _backgroundBlocked = blocked;
        }
    }
}
