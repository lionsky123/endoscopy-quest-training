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
        public static Image Outline(RectTransform rect, Color color, float radius = 8, float thickness = 3)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            EntryUIShapes.ApplySizedRoundedOutline(image, rect.sizeDelta, radius, thickness);
            return image;
        }
        public static void ResizeOutline(Image image, Vector2 size)
            => EntryUIShapes.ApplySizedRoundedOutline(image, size, 8, 3);
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
            var button = rect.gameObject.AddComponent<Button>();
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
            if (primary) text.color = new Color(.75f, 1f, .9f, 1);
            // Use the template's existing progress renderer and unique dwell timer.
            var progress = Rect(rect, "GazeProgress", 0, 0, w + 6, h + 6);
            progress.gameObject.AddComponent<Image>().raycastTarget = false;
            Rect(progress, "Fill", 0, 0, w + 6, h + 6).gameObject.AddComponent<Image>().raycastTarget = false;
            EntryUIButtonVisual.Apply(button, Accent, primary);
            return button;
        }
        public static void RefreshButton(Button button, bool primary = false)
            => EntryUIButtonVisual.Apply(button, Accent, primary);
        public static void EmphasizeButton(Button button, bool selected = false, bool primary = false)
        {
            var visual = button.GetComponent<ClinicalChoiceVisual>();
            if (visual == null) { visual = button.gameObject.AddComponent<ClinicalChoiceVisual>(); visual.Initialize(button); }
            EntryUIButtonVisual.Apply(button, Accent, primary);
            visual.SetState(selected, primary);
        }
    }

    // Keeps solid, readable choices while sharing the existing gaze timer and progress dispatch.
    public sealed class ClinicalChoiceVisual : MonoBehaviour, IFrontendGazeProgressPresenter
    {
        Button _button; Image _surface, _border; TMP_Text _label; RectTransform _progress;
        bool _selected, _primary;
        public void Initialize(Button button)
        {
            _button = button; _surface = button.targetGraphic as Image; _label = button.GetComponentInChildren<TMP_Text>();
            var rect = (RectTransform)button.transform;
            _border = ClinicalPanelStyle.Outline(ClinicalPanelStyle.Rect(rect, "ChoiceBorder", 0, 0, rect.rect.width, rect.rect.height), Color.white, 12, 3);
            _progress = ClinicalPanelStyle.Rect(rect, "ChoiceDwellProgress", 0, -rect.rect.height * .5f + 7, rect.rect.width - 20, 5);
            _progress.pivot = new Vector2(0, .5f); _progress.anchoredPosition = new Vector2(-rect.rect.width * .5f + 10, _progress.anchoredPosition.y);
            ClinicalPanelStyle.Fill(_progress, new Color(.55f, .88f, 1f), 2);
            button.transition = Selectable.Transition.None;
            _surface.canvasRenderer.SetColor(Color.white);
        }
        public void SetState(bool selected, bool primary) { _selected = selected; _primary = primary; PresentGazeProgress(0); }
        public void PresentGazeProgress(float progress)
        {
            if (_button == null) return;
            bool enabled = _button.IsInteractable();
            _surface.color = !enabled ? new Color(.10f,.14f,.18f,1) : _selected ? new Color(.08f,.34f,.62f,1)
                : _primary ? new Color(.06f,.39f,.31f,1) : new Color(.09f,.19f,.29f,1);
            _label.color = enabled ? Color.white : new Color(.53f,.60f,.67f,1);
            _border.color = !enabled ? new Color(.35f,.43f,.51f,1) : _selected || progress > 0 ? new Color(.57f,.85f,1,1)
                : _primary ? new Color(.33f,.89f,.69f,1) : new Color(.45f,.64f,.79f,1);
            _progress.sizeDelta = new Vector2((((RectTransform)_button.transform).rect.width - 20) * Mathf.Clamp01(progress), 5);
        }
    }
}
