using System;
using BotanicalGardenQR.FrontendShell.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    // Reuses the reference project's rounded surfaces; contains presentation only.
    public static class ClinicalPanelStyle
    {
        public static readonly Color Surface = new Color(.055f, .075f, .073f, .96f);
        public static readonly Color Accent = new Color(.24f, .88f, .69f, 1f);
        public static readonly Color Muted = new Color(.65f, .73f, .7f, 1f);
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
        public static Image Fill(RectTransform rect, Color color, float radius = 14)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            EntryUIShapes.ApplySizedRoundedFill(image, rect.sizeDelta, radius);
            return image;
        }
        public static void Frame(RectTransform rect)
        {
            Fill(rect, Surface, 22);
            var border = Rect(rect, "FineBorder", 0, 0, rect.sizeDelta.x, rect.sizeDelta.y);
            var image = border.gameObject.AddComponent<Image>();
            image.color = new Color(.74f, .89f, .81f, .22f);
            image.raycastTarget = false;
            EntryUIShapes.ApplySizedRoundedOutline(image, rect.sizeDelta, 22, 1);
        }
        public static TMP_Text Label(Transform parent, TMP_FontAsset font, string name,
            float x, float y, float w, float h, float size)
        {
            var label = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = new Color(.94f, .97f, .94f, 1);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }
        public static Button Button(Transform parent, TMP_FontAsset font, string name, string copy,
            float x, float y, float w, float h, Action action, bool primary = false)
        {
            var rect = Rect(parent, name, x, y, w, h);
            var image = Fill(rect, primary ? Accent : new Color(.11f, .17f, .15f, 1), 12);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<NearOnlyButton>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(.85f, 1, .94f);
            colors.pressedColor = new Color(.6f, .8f, .72f);
            colors.disabledColor = new Color(.5f, .5f, .5f, .45f);
            button.colors = colors;
            button.onClick.AddListener(() => action?.Invoke());
            var text = Label(rect, font, "Label", 0, 0, w - 20, h - 6, 23);
            text.text = copy;
            text.alignment = TextAlignmentOptions.Center;
            if (primary) text.color = new Color(.035f, .12f, .09f, 1);
            return button;
        }
    }
}
