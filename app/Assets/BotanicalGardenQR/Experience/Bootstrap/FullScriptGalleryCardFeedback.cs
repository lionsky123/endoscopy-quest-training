using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed class FullScriptGalleryCardFeedback : MonoBehaviour, IFrontendGazeProgressPresenter
    {
        Image _surface, _rim, _progress;
        RectTransform _progressRect;
        Color _baseColor, _rimColor;
        float _width;

        internal void Initialize(Image surface, Image rim, Image progress, Color baseColor, Color rimColor)
        {
            _surface = surface; _rim = rim; _progress = progress;
            _progressRect = progress.rectTransform;
            _width = _progressRect.rect.width;
            _baseColor = baseColor; _rimColor = rimColor;
            PresentGazeProgress(0);
        }

        public void PresentGazeProgress(float value)
        {
            value = Mathf.Clamp01(value);
            _surface.color = Color.Lerp(_baseColor, Color.white, value * .09f);
            _rim.color = Color.Lerp(_rimColor, ClinicalPanelStyle.Accent, value * .65f);
            _progressRect.sizeDelta = new Vector2(_width * Mathf.Clamp01((value - .5f) * 2f), _progressRect.sizeDelta.y);
            _progress.color = new Color(.39f, .84f, .67f, value > .5f ? .95f : 0);
        }
    }
}
