using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [System.Serializable]
    public sealed class PresentationOverride
    {
        [SerializeReference] TextStyleOverride _titleStyle;
        [SerializeReference] TextStyleOverride _subtitleStyle;
        [SerializeReference] LayoutOverride _layout;
        [SerializeReference] AnimationOverride _animation;
        public TextStyleOverride TitleStyle => _titleStyle;
        public TextStyleOverride SubtitleStyle => _subtitleStyle;
        public LayoutOverride Layout => _layout;
        public AnimationOverride Animation => _animation;
    }

    [System.Serializable]
    public sealed class TextStyleOverride
    {
        [SerializeField] OptionalFloat _fontSize;
        [SerializeField] OptionalColor _color;
        [SerializeField] OptionalVector2 _position;
        [SerializeField] OptionalFloat _width;
        public OptionalFloat FontSize => _fontSize;
        public OptionalColor Color => _color;
        public OptionalVector2 Position => _position;
        public OptionalFloat Width => _width;
    }

    [System.Serializable]
    public sealed class LayoutOverride
    {
        [SerializeField] OptionalVector2 _panelSize;
        [SerializeField] OptionalVector2 _panelOffset;
        [SerializeField] OptionalFloat _spacing;
        public OptionalVector2 PanelSize => _panelSize;
        public OptionalVector2 PanelOffset => _panelOffset;
        public OptionalFloat Spacing => _spacing;
    }

    [System.Serializable]
    public sealed class AnimationOverride
    {
        [SerializeField] OptionalFloat _transitionSeconds;
        public OptionalFloat TransitionSeconds => _transitionSeconds;
    }

    [System.Serializable] public struct OptionalFloat { [SerializeField] bool _hasValue; [SerializeField] float _value; public bool HasValue => _hasValue; public float Value => _value; }
    [System.Serializable] public struct OptionalColor { [SerializeField] bool _hasValue; [SerializeField] Color _value; public bool HasValue => _hasValue; public Color Value => _value; }
    [System.Serializable] public struct OptionalVector2 { [SerializeField] bool _hasValue; [SerializeField] Vector2 _value; public bool HasValue => _hasValue; public Vector2 Value => _value; }
}
