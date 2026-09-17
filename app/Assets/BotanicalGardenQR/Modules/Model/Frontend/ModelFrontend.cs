using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Model.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Model.Frontend
{
    public sealed class ModelFrontend : MonoBehaviour, IModelStateSink
    {
        [SerializeField] GameObject _pageRoot;
        [SerializeField] GameObject _controlsRoot;
        [SerializeField] Button _autoMotionButton;
        [SerializeField] Button _resetButton;
        [SerializeField] Button _animationButton;
        [SerializeField] Image _autoMotionIndicator;
        [SerializeField] Image _animationIndicator;
        [SerializeField] Text _statusText;

        IModelController _controller;
        SessionToken _session;
        bool _bound;
        bool _listenersBound;

        public void Bind(SessionToken session, IModelController controller)
        {
            if (!session.IsValid) throw new ArgumentException("A valid session is required.", nameof(session));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _session = session;
            _bound = true;
            BindListeners();
            SetVisible(false);
        }

        public void Unbind()
        {
            SetVisible(false);
            UnbindListeners();
            _bound = false;
            _session = default;
            _controller = null;
        }

        public void SetVisible(bool visible)
        {
            if (_pageRoot != null) _pageRoot.SetActive(visible);
            if (_controlsRoot != null) _controlsRoot.SetActive(visible);
        }

        public void ToggleAutoMotion() => Dispatch(new ModelIntent(ModelIntentKind.ToggleAutoMotion));
        public void ResetView() => Dispatch(new ModelIntent(ModelIntentKind.ResetView));
        public void PlayAnimation() => Dispatch(new ModelIntent(ModelIntentKind.PlayAnimation));

        public void Publish(ModelState state)
        {
            if (!_bound || state.Session != _session) return;
            if (_autoMotionButton != null)
            {
                _autoMotionButton.interactable = state.CanToggleAutoMotion;
                SetButtonText(_autoMotionButton, state.IsAutoMotionActive ? "自动运动·开" : "自动运动");
            }
            if (_resetButton != null)
                _resetButton.interactable = state.CanResetView;
            if (_animationButton != null)
            {
                _animationButton.gameObject.SetActive(state.CanPlayAnimation || state.Phase == ModelPhase.Loading);
                _animationButton.interactable = state.CanPlayAnimation;
                SetButtonText(_animationButton, state.IsAnimationPlaying ? "重播动画" : "播放动画");
            }
            if (_autoMotionIndicator != null)
                _autoMotionIndicator.gameObject.SetActive(state.IsAutoMotionActive);
            if (_animationIndicator != null)
                _animationIndicator.gameObject.SetActive(state.IsAnimationPlaying);
            if (_statusText != null) _statusText.text = StatusText(state);
        }

        void Dispatch(ModelIntent intent)
        {
            if (!_bound) return;
            var result = _controller.Dispatch(_session, intent);
            if (!result.Succeeded && _statusText != null)
                _statusText.text = result.DiagnosticTag;
        }

        void BindListeners()
        {
            if (_listenersBound) return;
            if (_autoMotionButton != null) _autoMotionButton.onClick.AddListener(ToggleAutoMotion);
            if (_resetButton != null) _resetButton.onClick.AddListener(ResetView);
            if (_animationButton != null) _animationButton.onClick.AddListener(PlayAnimation);
            _listenersBound = true;
        }

        void UnbindListeners()
        {
            if (!_listenersBound) return;
            if (_autoMotionButton != null) _autoMotionButton.onClick.RemoveListener(ToggleAutoMotion);
            if (_resetButton != null) _resetButton.onClick.RemoveListener(ResetView);
            if (_animationButton != null) _animationButton.onClick.RemoveListener(PlayAnimation);
            _listenersBound = false;
        }

        static string StatusText(ModelState state)
        {
            if (state.Fault != null) return "模型加载失败";
            switch (state.Phase)
            {
                case ModelPhase.Loading: return "模型加载中…";
                case ModelPhase.Ready: return "模型已就绪";
                case ModelPhase.Failed: return "模型加载失败";
                default: return string.Empty;
            }
        }

        static void SetButtonText(Button button, string value)
        {
            var tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = value;
                return;
            }
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null) text.text = value;
        }

        void OnDestroy() => UnbindListeners();
    }
}
