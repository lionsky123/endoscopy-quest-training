using System;
using System.Collections;
using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    public sealed class ShellTransitionPlayer
    {
        const float ExitScale = 0.992f;
        const float EnterScale = 0.985f;

        readonly MonoBehaviour _runner;
        readonly CanvasGroup _canvasGroup;
        readonly RectTransform _contentRoot;
        readonly Vector3 _baseScale;
        Coroutine _transition;

        public ShellTransitionPlayer(MonoBehaviour runner, CanvasGroup canvasGroup, RectTransform contentRoot)
        {
            _runner = runner;
            _canvasGroup = canvasGroup;
            _contentRoot = contentRoot;
            _baseScale = contentRoot != null ? contentRoot.localScale : Vector3.one;
        }

        public void SetVisible(bool visible, float durationSeconds)
        {
            if (_canvasGroup == null) return;
            StopTransition();
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
            if (durationSeconds <= 0f || !_runner.isActiveAndEnabled)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                if (_contentRoot != null) _contentRoot.localScale = _baseScale;
                _transition = null;
                return;
            }
            _transition = _runner.StartCoroutine(Fade(visible, durationSeconds));
        }

        public void Transition(Action apply, float durationSeconds)
        {
            if (apply == null) return;
            if (_canvasGroup == null || durationSeconds <= 0f || !_runner.isActiveAndEnabled)
            {
                apply();
                SetVisible(true, 0f);
                return;
            }

            StopTransition();
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _transition = _runner.StartCoroutine(FadeBetween(apply, durationSeconds));
        }

        void StopTransition()
        {
            if (_transition == null) return;
            _runner.StopCoroutine(_transition);
            _transition = null;
        }

        IEnumerator Fade(bool visible, float durationSeconds)
        {
            var fromAlpha = _canvasGroup.alpha;
            var toAlpha = visible ? 1f : 0f;
            var fromScale = _contentRoot != null ? _contentRoot.localScale : Vector3.one;
            var toScale = visible ? _baseScale : _baseScale * ExitScale;
            var elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / durationSeconds);
                var eased = visible ? EaseOutCubic(progress) : EaseInCubic(progress);
                _canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, eased);
                if (_contentRoot != null)
                    _contentRoot.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
                yield return null;
            }
            _canvasGroup.alpha = toAlpha;
            if (_contentRoot != null) _contentRoot.localScale = toScale;
            _transition = null;
        }

        IEnumerator FadeBetween(Action apply, float durationSeconds)
        {
            var halfDuration = Mathf.Max(0.01f, durationSeconds * 0.5f);
            var currentScale = _contentRoot != null ? _contentRoot.localScale : Vector3.one;
            yield return FadeTo(
                _canvasGroup.alpha,
                0f,
                currentScale,
                _baseScale * ExitScale,
                halfDuration,
                false);
            apply();
            if (_contentRoot != null) _contentRoot.localScale = _baseScale * EnterScale;
            yield return FadeTo(
                0f,
                1f,
                _baseScale * EnterScale,
                _baseScale,
                halfDuration,
                true);
            if (_contentRoot != null) _contentRoot.localScale = _baseScale;
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            _transition = null;
        }

        IEnumerator FadeTo(
            float fromAlpha,
            float toAlpha,
            Vector3 fromScale,
            Vector3 toScale,
            float durationSeconds,
            bool easeOut)
        {
            var elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / durationSeconds);
                var eased = easeOut ? EaseOutCubic(progress) : EaseInCubic(progress);
                _canvasGroup.alpha = Mathf.LerpUnclamped(fromAlpha, toAlpha, eased);
                if (_contentRoot != null)
                    _contentRoot.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
                yield return null;
            }

            _canvasGroup.alpha = toAlpha;
            if (_contentRoot != null) _contentRoot.localScale = toScale;
        }

        static float EaseInCubic(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value;
        }

        static float EaseOutCubic(float value)
        {
            value = 1f - Mathf.Clamp01(value);
            return 1f - value * value * value;
        }
    }
}
