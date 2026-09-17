using TMPro;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [CreateAssetMenu(menuName = "Botanical Garden QR/Global UI Defaults", fileName = "GlobalUiDefaults")]
    public sealed class GlobalUiDefaults : ScriptableObject
    {
        const string DefaultStartupHint = "跟随路线到站，把圆点停在“开启发现”上。";

        [SerializeField] TMP_FontAsset _sharedFont;
        [SerializeField] TextStyleSpec _titleStyle = new TextStyleSpec(48f, Color.white, Vector2.zero, 720f);
        [SerializeField] TextStyleSpec _subtitleStyle = new TextStyleSpec(28f, Color.white, Vector2.zero, 720f);
        [SerializeField] LayoutSpec _layout = new LayoutSpec(new Vector2(900f, 600f), Vector2.zero, 24f);
        [SerializeField] AnimationSpec _animation = new AnimationSpec(0.2f);
        [SerializeField] string _startupHint = DefaultStartupHint;
        public TMP_FontAsset SharedFont => _sharedFont;
        public TextStyleSpec TitleStyle => _titleStyle;
        public TextStyleSpec SubtitleStyle => _subtitleStyle;
        public LayoutSpec Layout => _layout;
        public AnimationSpec Animation => _animation;
        public string StartupHint => string.IsNullOrWhiteSpace(_startupHint)
            ? DefaultStartupHint
            : _startupHint.Trim();
    }
}
