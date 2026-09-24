using System;
using BotanicalGardenQR.FrontendShell.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    // Shared material roles for the spatial control shell and opaque reading surfaces.
    public static class ClinicalPanelStyle
    {
        public static readonly Color Shell = new Color(.105f, .102f, .145f, .98f);
        public static readonly Color ShellEdge = new Color(.39f, .37f, .47f, .85f);
        public static readonly Color ShellText = new Color(.965f, .955f, .985f, 1f);
        public static readonly Color Surface = new Color(.968f, .961f, .976f, 1f);
        public static readonly Color SurfaceRaised = new Color(.995f, .99f, .998f, 1f);
        public static readonly Color SurfaceSubtle = new Color(.884f, .859f, .923f, 1f);
        public static readonly Color ChoiceHover = new Color(.81f, .765f, .89f, 1f);
        public static readonly Color ChoiceDisabled = new Color(.83f, .81f, .85f, 1f);
        public static readonly Color Accent = new Color(.31f, .25f, .66f, 1f);
        public static readonly Color Confirmation = new Color(.95f, .48f, .43f, 1f);
        public static readonly Color TextPrimary = new Color(.13f, .115f, .17f, 1f);
        public static readonly Color Muted = new Color(.32f, .29f, .37f, 1f);
        public static readonly Color Border = new Color(.69f, .65f, .74f, 1f);
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
            var surface = rect.GetComponent<Image>() ?? Fill(rect, Surface, 20);
            surface.color = Surface;
            var wash = Rect(rect, "FrameUpperWash", 0, rect.sizeDelta.y * .5f - 46, rect.sizeDelta.x - 18, 82);
            Fill(wash, new Color(.92f, .905f, .945f, .78f), 15);
            var border = Rect(rect, "FineBorder", 0, 0, rect.sizeDelta.x, rect.sizeDelta.y);
            var image = border.gameObject.AddComponent<Image>();
            image.color = new Color(Border.r, Border.g, Border.b, .72f);
            image.raycastTarget = false;
            EntryUIShapes.ApplySizedRoundedOutline(image, rect.sizeDelta, 20, 2);
            Fill(Rect(rect, "FrameUpperKeyline", 0, rect.sizeDelta.y * .5f - 8,
                rect.sizeDelta.x - 48, 2), new Color(1f, 1f, 1f, .9f), 1);
            Fill(Rect(rect, "FrameEdgeAccent", -rect.sizeDelta.x * .5f + 8, 0,
                3, rect.sizeDelta.y - 70), new Color(Accent.r, Accent.g, Accent.b, .58f), 1);
        }
        public static void ShellFrame(RectTransform rect)
        {
            var surface = rect.GetComponent<Image>() ?? Fill(rect, Shell, 20);
            surface.color = Shell;
            Outline(Rect(rect, "ShellRim", 0, 0, rect.sizeDelta.x, rect.sizeDelta.y), ShellEdge, 20, 2);
            Fill(Rect(rect, "ShellTopLight", 0, rect.sizeDelta.y * .5f - 8,
                rect.sizeDelta.x - 40, 2), new Color(1f, 1f, 1f, .24f), 1);
            Fill(Rect(rect, "ShellMarker", -rect.sizeDelta.x * .5f + 27,
                rect.sizeDelta.y * .5f - 38, 22, 4), Confirmation, 2);
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
            label.color = TextPrimary;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }
        public static Button Button(Transform parent, TMP_FontAsset font, string name, string copy,
            float x, float y, float w, float h, Action action, bool primary = false)
        {
            var rect = Rect(parent, name, x, y, w, h);
            var image = Fill(rect, primary ? Accent : SurfaceSubtle, 12);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => action?.Invoke());
            var text = Label(rect, font, "Label", 0, 0, w - 20, h - 6, 23);
            text.text = copy;
            text.alignment = TextAlignmentOptions.Center;
            text.color = primary ? ShellText : TextPrimary;
            EmphasizeButton(button, primary: primary);
            return button;
        }
        public static void RefreshButton(Button button, bool primary = false)
            => EmphasizeButton(button, primary: primary);
        public static void EmphasizeButton(Button button, bool selected = false, bool primary = false)
        {
            var visual = button.GetComponent<ClinicalChoiceVisual>();
            if (visual == null) { visual = button.gameObject.AddComponent<ClinicalChoiceVisual>(); visual.Initialize(button); }
            visual.SetState(selected, primary);
        }
    }

    // Solid choices keep readable state feedback for hand proximity and intentional presses.
    public sealed class ClinicalChoiceVisual : MonoBehaviour, IFrontendGazeProgressPresenter
    {
        Button _button; Image _surface, _border, _upperLight; TMP_Text _label; RectTransform _progress;
        bool _selected, _primary, _lastInteractable;
        public void Initialize(Button button)
        {
            _button = button; _surface = button.targetGraphic as Image; _label = button.GetComponentInChildren<TMP_Text>();
            _lastInteractable = button.IsInteractable();
            var rect = (RectTransform)button.transform;
            _border = ClinicalPanelStyle.Outline(ClinicalPanelStyle.Rect(rect, "ChoiceBorder", 0, 0, rect.rect.width, rect.rect.height), ClinicalPanelStyle.Border, 12, 2);
            _progress = ClinicalPanelStyle.Rect(rect, "ChoiceDwellProgress", 0, -rect.rect.height * .5f + 7, rect.rect.width - 20, 5);
            _progress.pivot = new Vector2(0, .5f); _progress.anchoredPosition = new Vector2(-rect.rect.width * .5f + 10, _progress.anchoredPosition.y);
            ClinicalPanelStyle.Fill(_progress, ClinicalPanelStyle.Confirmation, 2);
            _upperLight = ClinicalPanelStyle.Fill(ClinicalPanelStyle.Rect(rect, "ChoiceUpperLight", 0,
                rect.rect.height * .5f - 5, rect.rect.width - 26, 2), Color.white, 1);
            button.transition = Selectable.Transition.None;
        }
        void LateUpdate()
        {
            if(!_button || _lastInteractable==_button.IsInteractable())return;
            _lastInteractable=_button.IsInteractable();
            PresentGazeProgress(0);
        }
        public void SetState(bool selected, bool primary) { _selected = selected; _primary = primary; PresentGazeProgress(0); }
        public void PresentGazeProgress(float progress)
        {
            if (_button == null) return;
            bool enabled = _button.IsInteractable();
            _surface.color = !enabled ? ClinicalPanelStyle.ChoiceDisabled
                : _selected || _primary ? ClinicalPanelStyle.Accent
                : Color.Lerp(ClinicalPanelStyle.SurfaceSubtle, ClinicalPanelStyle.ChoiceHover, Mathf.Clamp01(progress));
            _label.color = enabled ? (_selected || _primary ? ClinicalPanelStyle.ShellText : ClinicalPanelStyle.TextPrimary) : ClinicalPanelStyle.Muted;
            _border.color = !enabled ? ClinicalPanelStyle.Border : _selected || progress > 0 || _primary
                ? ClinicalPanelStyle.Accent : ClinicalPanelStyle.Border;
            _upperLight.color = _selected || _primary ? new Color(.86f,.82f,.98f,.5f) : new Color(1f,1f,1f,.62f);
            _progress.sizeDelta = new Vector2((((RectTransform)_button.transform).rect.width - 20) * Mathf.Clamp01((progress-.5f)*2f), 5);
        }
    }
}
