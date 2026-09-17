using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [System.Serializable]
    public sealed class PresentationSpec
    {
        [SerializeField] TextStyleSpec _titleStyle;
        [SerializeField] TextStyleSpec _subtitleStyle;
        [SerializeField] LayoutSpec _layout;
        [SerializeField] AnimationSpec _animation;

        public PresentationSpec(TextStyleSpec titleStyle, TextStyleSpec subtitleStyle, LayoutSpec layout, AnimationSpec animation)
        { _titleStyle=titleStyle; _subtitleStyle=subtitleStyle; _layout=layout; _animation=animation; }
        public TextStyleSpec TitleStyle => _titleStyle;
        public TextStyleSpec SubtitleStyle => _subtitleStyle;
        public LayoutSpec Layout => _layout;
        public AnimationSpec Animation => _animation;
    }

    [System.Serializable] public struct TextStyleSpec { [SerializeField] float _fontSize; [SerializeField] Color _color; [SerializeField] Vector2 _position; [SerializeField] float _width; public TextStyleSpec(float fontSize,Color color,Vector2 position,float width){_fontSize=fontSize;_color=color;_position=position;_width=width;} public float FontSize=>_fontSize; public Color Color=>_color; public Vector2 Position=>_position; public float Width=>_width; }
    [System.Serializable] public struct LayoutSpec { [SerializeField] Vector2 _panelSize; [SerializeField] Vector2 _panelOffset; [SerializeField] float _spacing; public LayoutSpec(Vector2 panelSize,Vector2 panelOffset,float spacing){_panelSize=panelSize;_panelOffset=panelOffset;_spacing=spacing;} public Vector2 PanelSize=>_panelSize; public Vector2 PanelOffset=>_panelOffset; public float Spacing=>_spacing; }
    [System.Serializable] public struct AnimationSpec { [SerializeField] float _transitionSeconds; public AnimationSpec(float transitionSeconds){_transitionSeconds=transitionSeconds;} public float TransitionSeconds=>_transitionSeconds; }
}
