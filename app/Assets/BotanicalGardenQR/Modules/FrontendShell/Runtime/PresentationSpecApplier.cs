using System;
using BotanicalGardenQR.Configuration.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    public static class PresentationSpecApplier
    {
        public static void Apply(ShellSlotReferences slots, PresentationSpec presentation)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));

            ApplyText(slots.Title, presentation.TitleStyle);
            ApplyText(slots.Subtitle, presentation.SubtitleStyle);
            slots.ShellRoot.sizeDelta = presentation.Layout.PanelSize;
            slots.ShellRoot.anchoredPosition = presentation.Layout.PanelOffset;
            ApplyVerticalSpacing(slots.HeaderSlot, presentation.Layout.Spacing);
            ApplySurfaceShapes(slots.ShellRoot);
            EntryUIIcon.RefreshExisting(slots.ShellRoot);
        }

        static void ApplyText(TMP_Text text, TextStyleSpec style)
        {
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(style.FontSize));
            text.color = style.Color;
            var rect = text.rectTransform;
            rect.anchoredPosition = style.Position;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, style.Width);
        }

        static void ApplyVerticalSpacing(RectTransform header, float spacing)
        {
            if (header == null) return;
            var layout = header.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (layout != null) layout.spacing = spacing;
        }

        static void ApplySurfaceShapes(RectTransform shellRoot)
        {
            if (shellRoot == null) return;
            foreach (var image in shellRoot.GetComponentsInChildren<Image>(true))
            {
                if (image == null) continue;
                switch (image.gameObject.name)
                {
                    case "KnowledgePanel":
                    case "SpatialDock":
                    case "ObservationMediaSlot":
                    case "StatusPill":
                    case "ModeCrumb":
                    case "VideoStage":
                    case "VideoControlSlot":
                    case "ModelStage":
                    case "ModelInfoSlot":
                    case "ModelControlSlot":
                    case "ButtonSurface":
                    case "DepthShadow":
                    case "InnerColorWash":
                    case "GlassOutline":
                    case "ButtonOutline":
                        ApplySizedFill(image);
                        break;
                }
            }
        }

        static void ApplySizedFill(Image image)
        {
            if (image == null) return;
            NormalizeFillLayerColor(image);
            var size = ResolveRectSize(image.rectTransform);
            float radius = RadiusFor(image.gameObject.name, size);
            EntryUIShapes.ApplySizedRoundedFill(image, size, radius);
        }

        static void NormalizeFillLayerColor(Image image)
        {
            var color = image.color;
            switch (image.gameObject.name)
            {
                case "GlassOutline":
                    image.color = new Color(1f, 1f, 1f, Mathf.Min(color.a, 0.045f));
                    break;
                case "ButtonOutline":
                    image.color = new Color(color.r, color.g, color.b, Mathf.Min(color.a, 0.30f));
                    break;
                case "InnerColorWash":
                    image.color = new Color(color.r, color.g, color.b, Mathf.Min(color.a, 0.085f));
                    break;
                case "DepthShadow":
                    image.color = new Color(0f, 0f, 0f, Mathf.Min(Mathf.Max(color.a, 0.20f), 0.34f));
                    break;
            }
        }

        static Vector2 ResolveRectSize(RectTransform rect)
        {
            if (rect == null) return Vector2.one;
            var size = rect.rect.size;
            if (Mathf.Abs(size.x) <= 1f || Mathf.Abs(size.y) <= 1f)
                size = rect.sizeDelta;
            return new Vector2(Mathf.Max(1f, Mathf.Abs(size.x)), Mathf.Max(1f, Mathf.Abs(size.y)));
        }

        static float RadiusFor(string objectName, Vector2 size)
        {
            var halfMin = Mathf.Min(size.x, size.y) * 0.5f;
            switch (objectName)
            {
                case "StatusPill":
                case "ModeCrumb":
                    return halfMin;
                case "ButtonSurface":
                case "ButtonOutline":
                    return size.x > size.y * 1.45f ? Mathf.Min(10f, halfMin) : halfMin;
                default:
                    return Mathf.Min(14f, halfMin);
            }
        }
    }
}
