using System.Runtime.CompilerServices;
using BotanicalGardenQR.FrontendShell.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    internal static class EntryUIButtonVisual
    {
        static readonly Color DefaultAccent = new Color(0.208f, 0.949f, 0.761f, 1f);
        static readonly Color DisabledAccent = new Color(0.74f, 0.78f, 0.76f, 1f);
        static ConditionalWeakTable<Button, ButtonVisualState> States =
            new ConditionalWeakTable<Button, ButtonVisualState>();
        const float StandardRingPadding = 6f;

        public static void ApplyFromExisting(Button button, bool active = false, float gazeProgress = 0f)
        {
            Apply(button, ResolveAccent(button), active, gazeProgress);
        }

        public static void Apply(Button button, Color accent, bool active = false, float gazeProgress = 0f)
        {
            if (button == null)
            {
                return;
            }

            var persisted = EnsureState(button);
            persisted.Accent = NormalizeAccent(accent);
            persisted.Active = active;

            var disabled = !button.interactable;
            var confirming = !disabled && gazeProgress > 0.001f;
            var stateAccent = disabled ? DisabledAccent : BoostAccent(persisted.Accent, active || confirming);
            ConfigureButtonTint(button, disabled);
            ApplySurface(button, stateAccent, disabled, active, confirming);
            ApplyOutline(button, stateAccent, disabled, active, confirming);
            ApplyFocusAura(button, stateAccent, disabled, active, gazeProgress);
            ApplyGazeProgress(button, stateAccent, disabled, active, gazeProgress);
            ApplyIconTint(button, stateAccent, disabled, active);
            CacheGazeVisuals(button, persisted);
            persisted.LastDisabled = disabled;
            persisted.HasVisualState = true;
        }

        public static void ApplyGazeFocus(Button button, float gazeProgress)
        {
            if (button == null)
            {
                return;
            }

            var persisted = EnsureState(button);
            if (!persisted.HasCachedGazeVisuals)
                CacheGazeVisuals(button, persisted);
            if (persisted.CustomGazePresenter != null)
            {
                persisted.CustomGazePresenter.PresentGazeProgress(Mathf.Clamp01(gazeProgress));
                return;
            }

            if (!persisted.HasVisualState || !HasCurrentLayout(persisted))
                Apply(
                    button,
                    persisted.HasVisualState ? persisted.Accent : ResolveAccent(button),
                    persisted.Active,
                    gazeProgress);

            ApplyCachedGazeFocus(button, persisted, gazeProgress);
        }

        static bool HasCurrentLayout(ButtonVisualState persisted)
            => persisted.Root != null && persisted.Root.rect.size == persisted.RootSize;

        static void CacheGazeVisuals(Button button, ButtonVisualState persisted)
        {
            var root = button.transform;
            persisted.Root = root as RectTransform;
            persisted.Surface = root.Find("ButtonSurface")?.GetComponent<Image>() ?? button.GetComponent<Image>();
            persisted.Outline = root.Find("ButtonOutline")?.GetComponent<Image>();
            persisted.FocusAura = root.Find("FocusAura") as RectTransform;
            persisted.FocusAuraImage = persisted.FocusAura != null
                ? persisted.FocusAura.GetComponent<Image>()
                : null;
            persisted.GazeTrack = root.Find("GazeProgress") as RectTransform;
            persisted.GazeTrackImage = persisted.GazeTrack != null
                ? persisted.GazeTrack.GetComponent<Image>()
                : null;
            persisted.GazeFill = persisted.GazeTrack != null
                ? persisted.GazeTrack.Find("Fill") as RectTransform
                : null;
            persisted.GazeFillImage = persisted.GazeFill != null
                ? persisted.GazeFill.GetComponent<Image>()
                : null;
            persisted.Icon = FindLineIconGraphic(root);
            persisted.CustomGazePresenter = button.GetComponent<IFrontendGazeProgressPresenter>();
            persisted.RootSize = persisted.Root != null ? persisted.Root.rect.size : Vector2.zero;
            persisted.HasCachedGazeVisuals = true;
        }

        static void ApplyCachedGazeFocus(Button button, ButtonVisualState persisted, float gazeProgress)
        {
            var progress = Mathf.Clamp01(gazeProgress);
            var disabled = !button.interactable;
            var confirming = !disabled && progress > 0.001f;
            var accent = disabled
                ? DisabledAccent
                : BoostAccent(persisted.Accent, persisted.Active || confirming);
            if (persisted.LastDisabled != disabled)
            {
                ConfigureButtonTint(button, disabled);
                persisted.LastDisabled = disabled;
            }

            ApplyCachedSurface(button, persisted.Surface, accent, disabled, persisted.Active, confirming);
            ApplyCachedOutline(persisted.Outline, accent, disabled, persisted.Active, confirming);
            ApplyCachedFocusAura(
                persisted.FocusAura,
                persisted.FocusAuraImage,
                accent,
                disabled,
                persisted.Active,
                progress);
            ApplyCachedGazeProgress(
                persisted.GazeTrack,
                persisted.GazeTrackImage,
                persisted.GazeFill,
                persisted.GazeFillImage,
                accent,
                disabled,
                persisted.Active,
                progress);
            ApplyCachedIcon(persisted.Icon, accent, disabled, persisted.Active);
        }

        static void ApplyCachedSurface(
            Button button,
            Image surface,
            Color accent,
            bool disabled,
            bool active,
            bool confirming)
        {
            if (surface == null) return;
            surface.raycastTarget = surface == button.targetGraphic;
            if (disabled)
                surface.color = new Color(1f, 1f, 1f, 0.028f);
            else if (active)
                surface.color = new Color(accent.r, accent.g, accent.b, 0.2f);
            else if (confirming)
                surface.color = new Color(accent.r, accent.g, accent.b, 0.14f);
            else
                surface.color = new Color(1f, 1f, 1f, 0.092f);
        }

        static void ApplyCachedOutline(Image outline, Color accent, bool disabled, bool active, bool confirming)
        {
            if (outline == null) return;
            outline.raycastTarget = false;
            float alpha = disabled ? 0.07f : active ? 0.32f : confirming ? 0.24f : 0.1f;
            outline.color = new Color(accent.r, accent.g, accent.b, alpha);
        }

        static void ApplyCachedFocusAura(
            RectTransform aura,
            Image image,
            Color accent,
            bool disabled,
            bool active,
            float gazeProgress)
        {
            if (aura == null || image == null) return;
            image.raycastTarget = false;
            if (disabled)
            {
                image.color = Color.clear;
                aura.localScale = Vector3.one;
                return;
            }

            var value = Mathf.SmoothStep(0f, 1f, gazeProgress);
            float alpha = active ? Mathf.Max(0.1f, value * 0.22f) : value <= 0f ? 0f : 0.08f + value * 0.22f;
            image.color = new Color(accent.r, accent.g, accent.b, alpha);
            aura.localScale = Vector3.one * (1f + value * 0.06f);
        }

        static void ApplyCachedGazeProgress(
            RectTransform track,
            Image trackImage,
            RectTransform fill,
            Image fillImage,
            Color accent,
            bool disabled,
            bool active,
            float gazeProgress)
        {
            if (trackImage != null)
            {
                trackImage.raycastTarget = false;
                var trackAlpha = disabled || (!active && gazeProgress <= 0.001f) ? 0f : 0.12f;
                trackImage.color = new Color(accent.r, accent.g, accent.b, trackAlpha);
            }

            if (fillImage == null) return;
            fillImage.raycastTarget = false;
            if (fillImage.type == Image.Type.Filled)
                fillImage.fillAmount = gazeProgress;
            else if (track != null && fill != null)
                fill.sizeDelta = new Vector2(track.sizeDelta.x * gazeProgress, fill.sizeDelta.y);
            var fillAlpha = disabled || gazeProgress <= 0.001f ? 0f : 0.88f;
            fillImage.color = new Color(accent.r, accent.g, accent.b, fillAlpha);
        }

        static void ApplyCachedIcon(Graphic graphic, Color accent, bool disabled, bool active)
        {
            if (graphic == null) return;
            if (disabled)
            {
                graphic.color = new Color(DisabledAccent.r, DisabledAccent.g, DisabledAccent.b, 0.48f);
                return;
            }

            float alpha = active ? 1f : 0.96f;
            graphic.color = new Color(accent.r, accent.g, accent.b, alpha);
        }

        public static Color ResolveAccent(Button button)
        {
            if (button == null)
            {
                return DefaultAccent;
            }

            var icon = FindLineIconGraphic(button.transform);
            if (icon != null && icon.color.a > 0.01f)
            {
                return NormalizeAccent(icon.color);
            }

            var outline = button.transform.Find("ButtonOutline")?.GetComponent<Image>();
            if (outline != null && outline.color.a > 0.01f)
            {
                return NormalizeAccent(outline.color);
            }

            var progress = button.transform.Find("GazeProgress")?.GetComponent<Image>();
            if (progress != null && progress.color.a > 0.01f)
            {
                return NormalizeAccent(progress.color);
            }

            return DefaultAccent;
        }

        static void ApplySurface(Button button, Color accent, bool disabled, bool active, bool confirming)
        {
            var surface = button.transform.Find("ButtonSurface")?.GetComponent<Image>();
            if (surface == null)
            {
                surface = button.GetComponent<Image>();
            }

            if (surface == null) return;
            var size = ResolveRectSize(surface.rectTransform, new Vector2(52f, 52f));
            EntryUIShapes.ApplySizedRoundedFill(surface, size, size.y * 0.5f);
            surface.raycastTarget = surface == button.targetGraphic;
            if (disabled)
            {
                surface.color = new Color(1f, 1f, 1f, 0.028f);
            }
            else if (active)
            {
                surface.color = new Color(accent.r, accent.g, accent.b, 0.2f);
            }
            else if (confirming)
            {
                surface.color = new Color(accent.r, accent.g, accent.b, 0.14f);
            }
            else
            {
                surface.color = new Color(1f, 1f, 1f, 0.092f);
            }
        }

        static void ApplyOutline(Button button, Color accent, bool disabled, bool active, bool confirming)
        {
            var outline = button.transform.Find("ButtonOutline")?.GetComponent<Image>();
            if (outline == null) return;

            var size = ResolveRectSize(outline.rectTransform, new Vector2(52f, 52f));
            EntryUIShapes.ApplySizedRoundedOutline(outline, size, size.y * 0.5f, disabled ? 1.1f : 1.6f);
            outline.raycastTarget = false;
            float alpha = disabled ? 0.07f : active ? 0.32f : confirming ? 0.24f : 0.1f;
            outline.color = new Color(accent.r, accent.g, accent.b, alpha);
        }

        static void ApplyFocusAura(Button button, Color accent, bool disabled, bool active, float gazeProgress)
        {
            var aura = button.transform.Find("FocusAura") as RectTransform;
            var image = aura != null ? aura.GetComponent<Image>() : null;
            if (aura == null || image == null) return;

            var rootSize = ResolveRectSize(button.transform as RectTransform, ResolveRectSize(aura, new Vector2(58f, 58f)));
            var size = rootSize + Vector2.one * 10f;
            EntryUILayout.SetCentered(aura, size, Vector2.zero);
            EntryUIShapes.ApplySizedRoundedOutline(image, size, size.y * 0.5f, disabled ? 1.5f : 2.2f);
            image.raycastTarget = false;
            if (disabled)
            {
                image.color = Color.clear;
                aura.localScale = Vector3.one;
                return;
            }

            var value = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(gazeProgress));
            float alpha = active ? Mathf.Max(0.1f, value * 0.22f) : value <= 0f ? 0f : 0.08f + value * 0.22f;
            image.color = new Color(accent.r, accent.g, accent.b, alpha);
            aura.localScale = Vector3.one * (1f + value * 0.06f);
        }

        static void ApplyGazeProgress(Button button, Color accent, bool disabled, bool active, float gazeProgress)
        {
            var track = button.transform.Find("GazeProgress") as RectTransform;
            var fill = track != null ? track.Find("Fill") as RectTransform : null;
            var trackImage = track != null ? track.GetComponent<Image>() : null;
            var fillImage = fill != null ? fill.GetComponent<Image>() : null;
            var rootRect = button.transform as RectTransform;
            var rootSize = ResolveRectSize(rootRect, new Vector2(58f, 58f));
            var padding = StandardRingPadding;
            var ringSize = new Vector2(
                Mathf.Max(1f, rootSize.x + padding),
                Mathf.Max(1f, rootSize.y + padding));
            var cornerRadius = Mathf.Min(ringSize.x, ringSize.y) * 0.5f;

            if (trackImage != null)
            {
                EntryUILayout.SetCentered(track, ringSize, Vector2.zero);
                EntryUIShapes.ApplySizedRoundedOutline(trackImage, track.sizeDelta, cornerRadius, disabled ? 1.1f : 1.8f);
                var trackAlpha = disabled || (!active && gazeProgress <= 0.001f) ? 0f : 0.12f;
                trackImage.color = new Color(accent.r, accent.g, accent.b, trackAlpha);
                trackImage.raycastTarget = false;
            }

            if (fillImage != null)
            {
                float fillAmount = fillImage.fillAmount;
                EntryUILayout.SetCentered(fill, ringSize, Vector2.zero);
                EntryUIShapes.ApplySizedRoundedProgressOutline(fillImage, fill.sizeDelta, cornerRadius, disabled ? 1.1f : 2.2f);
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Radial360;
                fillImage.fillOrigin = 2;
                fillImage.fillClockwise = true;
                fillImage.fillAmount = fillAmount;

                var fillAlpha = disabled || gazeProgress <= 0.001f ? 0f : 0.88f;
                fillImage.color = new Color(accent.r, accent.g, accent.b, fillAlpha);
                fillImage.raycastTarget = false;
            }
        }

        static void ApplyIconTint(Button button, Color accent, bool disabled, bool active)
        {
            var graphic = FindLineIconGraphic(button.transform);
            if (graphic == null) return;

            if (disabled)
            {
                graphic.color = new Color(DisabledAccent.r, DisabledAccent.g, DisabledAccent.b, 0.48f);
                return;
            }

            float alpha = active ? 1f : 0.96f;
            graphic.color = new Color(accent.r, accent.g, accent.b, alpha);
        }

        static void ConfigureButtonTint(Button button, bool disabled)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(1f, 1f, 1f, 0.92f);
            colors.selectedColor = Color.white;
            colors.disabledColor = disabled ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        static Graphic FindLineIconGraphic(Transform root)
        {
            if (root == null) return null;
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null || graphic.color.a <= 0.01f)
                {
                    continue;
                }

                var current = graphic.transform;
                while (current != null && current != root)
                {
                    if (current.name.StartsWith("LineIcon_", System.StringComparison.Ordinal))
                    {
                        return graphic;
                    }

                    current = current.parent;
                }

                if (graphic.transform.name.StartsWith("LineIcon_", System.StringComparison.Ordinal))
                {
                    return graphic;
                }
            }

            return null;
        }

        static Vector2 ResolveRectSize(RectTransform rect, Vector2 fallback)
        {
            if (rect != null)
            {
                var rectSize = rect.rect.size;
                if (Mathf.Abs(rectSize.x) > 1f && Mathf.Abs(rectSize.y) > 1f)
                    return new Vector2(Mathf.Abs(rectSize.x), Mathf.Abs(rectSize.y));
                var serializedSize = rect.sizeDelta;
                if (Mathf.Abs(serializedSize.x) > 1f && Mathf.Abs(serializedSize.y) > 1f)
                    return new Vector2(Mathf.Abs(serializedSize.x), Mathf.Abs(serializedSize.y));
            }

            return fallback;
        }

        static Color BoostAccent(Color color, bool emphasized)
        {
            var mix = emphasized ? 0.18f : 0.08f;
            return new Color(
                Mathf.Lerp(color.r, 1f, mix),
                Mathf.Lerp(color.g, 1f, mix),
                Mathf.Lerp(color.b, 1f, mix),
                1f);
        }

        static Color NormalizeAccent(Color color)
        {
            if (color.a <= 0.01f) return DefaultAccent;
            return new Color(color.r, color.g, color.b, 1f);
        }

        static ButtonVisualState EnsureState(Button button)
            => States.GetValue(button, _ => new ButtonVisualState());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
            => States = new ConditionalWeakTable<Button, ButtonVisualState>();

        sealed class ButtonVisualState
        {
            public Color Accent = EntryUIButtonVisual.DefaultAccent;
            public bool Active;
            public bool LastDisabled;
            public bool HasVisualState;
            public bool HasCachedGazeVisuals;
            public RectTransform Root;
            public Vector2 RootSize;
            public Image Surface;
            public Image Outline;
            public RectTransform FocusAura;
            public Image FocusAuraImage;
            public RectTransform GazeTrack;
            public Image GazeTrackImage;
            public RectTransform GazeFill;
            public Image GazeFillImage;
            public Graphic Icon;
            public IFrontendGazeProgressPresenter CustomGazePresenter;
        }
    }
}
