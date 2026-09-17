using System;
using BotanicalGardenQR.FrontendShell.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    /// <summary>
    /// Reusable authored-button beacon for a gaze action that must be discoverable
    /// before the visitor has focused it. Input and dwell ownership remain in the
    /// shared head-gaze controller; this component only presents semantic progress.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class AttentionGazeTargetVisual : MonoBehaviour, IFrontendGazeProgressPresenter
    {
        [SerializeField] Image _outline;
        [SerializeField] Image _focusAura;
        [SerializeField] Image _progressTrack;
        [SerializeField] Image _progressFill;
        [SerializeField] Color _accent = new Color(0.22f, 0.96f, 0.72f, 1f);
        [SerializeField, Min(0.4f)] float _pulseSeconds = 1.2f;

        Button _button;
        Vector3 _authoredAuraScale;
        float _progress;
        bool _initialized;

        void Awake() => EnsureInitialized();

        void OnEnable()
        {
            EnsureInitialized();
            _progress = 0f;
            Render();
        }

        void Update()
        {
            if (_initialized) Render();
        }

        void OnDisable()
        {
            if (!_initialized) return;
            _progress = 0f;
            _progressFill.fillAmount = 0f;
            _focusAura.rectTransform.localScale = _authoredAuraScale;
        }

        public void PresentGazeProgress(float progress)
        {
            EnsureInitialized();
            _progress = Mathf.Clamp01(progress);
            Render();
        }

        void EnsureInitialized()
        {
            if (_initialized) return;
            _button = GetComponent<Button>();
            if (_button == null || _outline == null || _focusAura == null ||
                _progressTrack == null || _progressFill == null)
                throw new InvalidOperationException(
                    "Attention gaze target requires its Button, outline, aura, progress track and progress fill.");
            if (!IsPositiveFinite(_pulseSeconds))
                throw new InvalidOperationException(
                    "Attention gaze target pulse duration must be positive and finite.");
            if (_outline.transform.parent != transform || _focusAura.transform.parent != transform ||
                _progressTrack.transform.parent != transform ||
                _progressFill.transform.parent != _progressTrack.transform)
                throw new InvalidOperationException(
                    "Attention gaze target visual roles must remain local to the authored Button.");

            _outline.raycastTarget = false;
            _focusAura.raycastTarget = false;
            _progressTrack.raycastTarget = false;
            _progressFill.raycastTarget = false;
            _progressFill.type = Image.Type.Filled;
            _progressFill.fillMethod = Image.FillMethod.Radial360;
            _progressFill.fillOrigin = 2;
            _progressFill.fillClockwise = true;
            _authoredAuraScale = _focusAura.rectTransform.localScale;
            _initialized = true;
        }

        void Render()
        {
            if (!_button.interactable)
            {
                _outline.color = WithAlpha(_accent, 0.07f);
                _focusAura.color = Color.clear;
                _progressTrack.color = Color.clear;
                _progressFill.color = Color.clear;
                _progressFill.fillAmount = 0f;
                _focusAura.rectTransform.localScale = _authoredAuraScale;
                return;
            }

            var wave = 0.5f + (0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / _pulseSeconds));
            var focused = _progress > 0.001f;
            var outlineAlpha = focused ? 0.42f + (_progress * 0.28f) : 0.28f + (wave * 0.16f);
            var auraAlpha = focused ? 0.20f + (_progress * 0.26f) : 0.10f + (wave * 0.14f);
            var auraScale = focused ? 1.025f + (_progress * 0.055f) : 1.01f + (wave * 0.035f);

            _outline.color = WithAlpha(_accent, outlineAlpha);
            _focusAura.color = WithAlpha(_accent, auraAlpha);
            _focusAura.rectTransform.localScale = _authoredAuraScale * auraScale;
            _progressTrack.color = WithAlpha(_accent, focused ? 0.18f : 0.10f + (wave * 0.04f));
            _progressFill.fillAmount = _progress;
            _progressFill.color = WithAlpha(_accent, focused ? 0.94f : 0f);
        }

        static Color WithAlpha(Color color, float alpha)
            => new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));

        static bool IsPositiveFinite(float value)
            => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
