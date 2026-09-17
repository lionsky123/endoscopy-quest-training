using BotanicalGardenQR.FrontendShell.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.KnowledgeMiniGame.Frontend
{
    /// <summary>
    /// Knowledge-card-specific dwell feedback. Keeping this presenter on the authored
    /// target prevents the generic button visual from turning pointer raycasts back on.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class KnowledgeMiniGameGazeTargetVisual : MonoBehaviour, IFrontendGazeProgressPresenter
    {
        static readonly Color FocusSurface = new Color(0.025f, 0.16f, 0.125f, 1f);
        static readonly Color FocusOutline = new Color(0.208f, 0.949f, 0.761f, 0.78f);

        Button _button;
        Image _surface;
        Outline _outline;
        Color _baseSurface;
        Color _baseOutline;
        bool _cached;

        public void PresentGazeProgress(float progress)
        {
            EnsureCached();
            var value = _button != null && _button.interactable
                ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress))
                : 0f;

            if (_surface != null)
                _surface.color = Color.Lerp(_baseSurface, FocusSurface, value);
            if (_outline != null)
                _outline.effectColor = Color.Lerp(_baseOutline, FocusOutline, value);
        }

        void Awake() => EnsureCached();

        void OnDisable()
        {
            if (!_cached) return;
            if (_surface != null) _surface.color = _baseSurface;
            if (_outline != null) _outline.effectColor = _baseOutline;
        }

        void EnsureCached()
        {
            if (_cached) return;
            _button = GetComponent<Button>();
            _surface = GetComponent<Image>();
            _outline = GetComponent<Outline>();
            _baseSurface = _surface != null ? _surface.color : Color.clear;
            _baseOutline = _outline != null ? _outline.effectColor : Color.clear;
            _cached = true;
        }
    }
}
