using System;
using BotanicalGardenQR.FrontendShell.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    public sealed class EntryMediaControlDock : MonoBehaviour, IEntryMediaControlDock
    {
        private static readonly Color AccentColor = new Color(0.208f, 0.949f, 0.761f, 1f);
        private static readonly Color AmberColor = new Color(1f, 0.82f, 0.4f, 1f);

        [SerializeField] private TMP_Text m_TitleLabel;
        [SerializeField] private TMP_Text m_StateLabel;
        [SerializeField] private TMP_Text m_PauseResumeLabel;
        [SerializeField] private Button m_PauseResumeButton;
        [SerializeField] private Button m_ReplayButton;
        [SerializeField] private Button m_VolumeButton;
        [SerializeField] private Image m_FrameImage;
        [SerializeField] private Image m_FrameOutline;
        [SerializeField] private Image m_FrameTint;
        [SerializeField] private RectTransform m_EmbeddedFrame;
        [SerializeField] private RectTransform m_MediaGlyph;
        [SerializeField] private RectTransform m_GlyphBars;
        [SerializeField] private RectTransform m_VideoGlyph;
        [SerializeField] private RectTransform m_ProgressTrack;
        [SerializeField] private RectTransform m_ProgressFill;

        private string m_LastInvokedButtonName;
        private int m_LastInvokedFrame = -1;
        private float m_LastInvokedTime = -1f;

        private const float DuplicateClickDebounceSeconds = 0.2f;
        private const float TransportToggleDebounceSeconds = 0.85f;

        private void Awake()
        {
            EnsureStaticVisualSurfaces();
        }

        private void OnEnable()
        {
            EnsureStaticVisualSurfaces();
        }

        public void Render(
            EntryMediaControlDockViewModel viewModel,
            EntryMediaControlDockCallbacks callbacks)
        {
            ConfigureClick(m_PauseResumeButton, null);
            ConfigureClick(m_ReplayButton, null);
            ConfigureClick(m_VolumeButton, null);

            if (viewModel.Mode == EntryMediaControlDockMode.Hidden)
            {
                gameObject.SetActive(false);
                return;
            }

            bool hasTransportControls =
                viewModel.Mode == EntryMediaControlDockMode.Narration ||
                viewModel.Mode == EntryMediaControlDockMode.Video ||
                viewModel.Mode == EntryMediaControlDockMode.Completed ||
                viewModel.Mode == EntryMediaControlDockMode.NarrationCompleted;
            bool canPauseResume = hasTransportControls && viewModel.CanPauseResume && callbacks.OnPauseResume != null;
            bool canReplay = hasTransportControls && viewModel.CanReplay && callbacks.OnReplay != null;
            bool canMute = hasTransportControls && viewModel.CanMute && callbacks.OnToggleVolume != null;
            Color transportColor = viewModel.Mode == EntryMediaControlDockMode.Video ? AmberColor : AccentColor;

            gameObject.SetActive(true);
            ApplyPresentationVisibility(viewModel.Mode);
            EnsureStaticVisualSurfaces();
            ApplyFrameTintSurface(viewModel.Mode);
            ApplyGlyph(viewModel.Mode);
            SetTitle(viewModel.Title);
            SetState(viewModel);
            SetProgress(viewModel.Progress01, viewModel.HasProgress);

            SetButtonVisible(m_PauseResumeButton, hasTransportControls && viewModel.CanPauseResume);
            SetButtonVisible(m_ReplayButton, hasTransportControls);
            SetButtonVisible(m_VolumeButton, hasTransportControls);

            if (m_PauseResumeLabel != null)
            {
                m_PauseResumeLabel.text = "";
            }

            if (m_PauseResumeButton != null)
            {
                m_PauseResumeButton.interactable = canPauseResume;
                EntryUIIcon.Replace(
                    m_PauseResumeButton.transform,
                    viewModel.IsPlaying ? EntryUILineIconKind.Pause : EntryUILineIconKind.Play,
                    new Vector2(28f, 28f),
                    Vector2.zero,
                    transportColor,
                    2.2f);
                EntryUIButtonVisual.Apply(m_PauseResumeButton, transportColor, viewModel.IsPlaying && canPauseResume);
            }

            if (m_ReplayButton != null)
            {
                m_ReplayButton.interactable = canReplay;
                EntryUIButtonVisual.Apply(m_ReplayButton, transportColor);
            }

            if (m_VolumeButton != null)
            {
                m_VolumeButton.interactable = canMute;
                Color volumeColor = viewModel.IsMuted ? AmberColor : AccentColor;
                EntryUIIcon.Replace(
                    m_VolumeButton.transform,
                    viewModel.IsMuted ? EntryUILineIconKind.VolumeMuted : EntryUILineIconKind.Volume,
                    new Vector2(32f, 32f),
                    Vector2.zero,
                    volumeColor,
                    2.4f);
                EntryUIButtonVisual.Apply(m_VolumeButton, volumeColor, viewModel.IsMuted && canMute);
            }

            ConfigureClick(m_PauseResumeButton, canPauseResume ? callbacks.OnPauseResume : null, TransportToggleDebounceSeconds);
            ConfigureClick(m_ReplayButton, canReplay ? callbacks.OnReplay : null);
            ConfigureClick(m_VolumeButton, canMute ? callbacks.OnToggleVolume : null, TransportToggleDebounceSeconds);
        }

        public void UpdateProgress(float progress01, bool hasProgress)
        {
            if (!gameObject.activeInHierarchy) return;
            SetProgress(progress01, hasProgress);
        }

        private void ApplyPresentationVisibility(EntryMediaControlDockMode mode)
        {
            Vector2 slotSize = ResolveEmbeddedSlotSize();
            bool isWideVideoDock =
                (mode == EntryMediaControlDockMode.Video ||
                 mode == EntryMediaControlDockMode.VideoPreparing ||
                 mode == EntryMediaControlDockMode.Completed) &&
                slotSize.x >= 700f;

            if (isWideVideoDock)
            {
                SetTextAndProgressVisible(true, true);
                SetGlyphVisible(true);
                return;
            }

            if (slotSize.x < 290f)
            {
                SetTextAndProgressVisible(false, false);
                SetGlyphVisible(false);
                return;
            }

            if (slotSize.x < 360f)
            {
                SetTextAndProgressVisible(false, false);
                SetGlyphVisible(true);
                return;
            }

            SetTextAndProgressVisible(true, true);
            SetGlyphVisible(true);
        }

        private void EnsureStaticVisualSurfaces()
        {
            Vector2 slotSize = ResolveEmbeddedSlotSize();
            ApplySizedFill(m_FrameImage, slotSize, 20f);
            ApplySizedOutline(m_FrameOutline, slotSize, 20f, 1.2f);

            Vector2 tintFallback = new Vector2(
                Mathf.Max(1f, slotSize.x - 4f),
                Mathf.Max(1f, slotSize.y - 4f));
            ApplySizedFill(m_FrameTint, ResolveRectSize(m_FrameTint?.rectTransform, tintFallback), 18f);

            var depthShade = m_EmbeddedFrame != null
                ? m_EmbeddedFrame.Find("DepthShade")?.GetComponent<Image>()
                : null;
            Vector2 depthFallback = new Vector2(slotSize.x + 10f, slotSize.y + 10f);
            ApplySizedFill(depthShade, ResolveRectSize(depthShade?.rectTransform, depthFallback), 25f);

            var glyphImage = m_MediaGlyph != null ? m_MediaGlyph.GetComponent<Image>() : null;
            ClearGlyphRootSurface(glyphImage);

            if (m_GlyphBars != null)
            {
                foreach (var image in m_GlyphBars.GetComponentsInChildren<Image>(true))
                {
                    EnsureGlyphBarSurface(image);
                }
            }

            EntryUIShapes.ApplyRoundedFill(m_ProgressTrack?.GetComponent<Image>());
            EntryUIShapes.ApplyRoundedFill(m_ProgressFill?.GetComponent<Image>());
            ApplyButtonVisualSurfaces(m_PauseResumeButton);
            ApplyButtonVisualSurfaces(m_ReplayButton);
            ApplyButtonVisualSurfaces(m_VolumeButton);
        }

        private static void ApplyButtonVisualSurfaces(Button button)
        {
            if (button == null)
            {
                return;
            }

            EntryUIShapes.ApplyRoundedFill(button.GetComponent<Image>());
            EntryUIIcon.RefreshExisting(button.transform);
            EntryUIButtonVisual.ApplyFromExisting(button);
        }

        private static void ApplySizedFill(Image image, Vector2 size, float radius)
        {
            if (image == null)
            {
                return;
            }

            EntryUIShapes.ApplySizedRoundedFill(image, NormalizeSize(size), Mathf.Max(0f, radius));
        }

        private static void ApplySizedOutline(Image image, Vector2 size, float radius, float width)
        {
            if (image == null)
            {
                return;
            }

            EntryUIShapes.ApplySizedRoundedOutline(image, NormalizeSize(size), Mathf.Max(0f, radius), Mathf.Max(0f, width));
        }

        private static Vector2 ResolveRectSize(RectTransform rect, Vector2 fallback)
        {
            if (rect != null)
            {
                Vector2 rectSize = rect.rect.size;
                if (Mathf.Abs(rectSize.x) > 1f && Mathf.Abs(rectSize.y) > 1f)
                {
                    return new Vector2(Mathf.Abs(rectSize.x), Mathf.Abs(rectSize.y));
                }

                Vector2 serializedSize = rect.sizeDelta;
                if (Mathf.Abs(serializedSize.x) > 1f && Mathf.Abs(serializedSize.y) > 1f)
                {
                    return new Vector2(Mathf.Abs(serializedSize.x), Mathf.Abs(serializedSize.y));
                }
            }

            return NormalizeSize(fallback);
        }

        private static Vector2 NormalizeSize(Vector2 size)
        {
            return new Vector2(Mathf.Max(1f, Mathf.Abs(size.x)), Mathf.Max(1f, Mathf.Abs(size.y)));
        }

        private void ApplyGlyph(EntryMediaControlDockMode mode)
        {
            if (m_MediaGlyph == null)
            {
                return;
            }

            bool video = mode == EntryMediaControlDockMode.Video ||
                         mode == EntryMediaControlDockMode.VideoPreparing ||
                         mode == EntryMediaControlDockMode.Completed;
            Color glyphColor = video ? AmberColor : AccentColor;
            var glyphImage = m_MediaGlyph.GetComponent<Image>();
            if (glyphImage != null)
            {
                ClearGlyphRootSurface(glyphImage);
            }

            if (m_GlyphBars != null)
            {
                m_GlyphBars.gameObject.SetActive(!video);
                foreach (var bar in m_GlyphBars.GetComponentsInChildren<Image>(true))
                {
                    bar.color = glyphColor;
                }
            }

            if (m_VideoGlyph != null)
            {
                m_VideoGlyph.gameObject.SetActive(video);
                var videoGraphic = m_VideoGlyph.GetComponent<Graphic>();
                if (videoGraphic != null)
                {
                    videoGraphic.color = glyphColor;
                }
            }
        }

        private static void ClearGlyphRootSurface(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.color = Color.clear;
            image.raycastTarget = false;
        }

        private static void EnsureGlyphBarSurface(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.raycastTarget = false;
            var rect = image.rectTransform;
            var size = ResolveRectSize(rect, rect != null ? rect.sizeDelta : new Vector2(16f, 4f));
            EntryUIShapes.ApplySizedRoundedFill(image, NormalizeSize(size), Mathf.Max(1f, size.y * 0.5f));
        }

        private void SetTextAndProgressVisible(bool textVisible, bool progressVisible)
        {
            if (m_TitleLabel != null)
            {
                m_TitleLabel.gameObject.SetActive(textVisible);
            }

            if (m_StateLabel != null)
            {
                m_StateLabel.gameObject.SetActive(textVisible);
            }

            if (m_ProgressTrack != null)
            {
                m_ProgressTrack.gameObject.SetActive(progressVisible);
            }
        }

        private Vector2 ResolveEmbeddedSlotSize()
        {
            var parent = transform.parent as RectTransform;
            if (parent != null && parent.sizeDelta.x > 1f && parent.sizeDelta.y > 1f)
            {
                return parent.sizeDelta;
            }

            if (parent != null && parent.rect.width > 1f && parent.rect.height > 1f)
            {
                return parent.rect.size;
            }

            var rect = transform as RectTransform;
            if (rect != null && rect.rect.width > 1f && rect.rect.height > 1f)
            {
                return rect.rect.size;
            }

            var grandParent = transform.parent != null ? transform.parent.parent as RectTransform : null;
            if (grandParent != null && grandParent.sizeDelta.x > 1f && grandParent.sizeDelta.y > 1f)
            {
                return grandParent.sizeDelta;
            }

            return new Vector2(420f, 82f);
        }

        private void SetGlyphVisible(bool visible)
        {
            if (m_MediaGlyph != null)
            {
                m_MediaGlyph.gameObject.SetActive(visible);
            }
        }

        private void SetTitle(string title)
        {
            if (m_TitleLabel != null)
            {
                m_TitleLabel.text = title ?? "";
            }
        }

        private void SetState(EntryMediaControlDockViewModel viewModel)
        {
            if (m_StateLabel == null)
            {
                return;
            }

            if (viewModel.Mode == EntryMediaControlDockMode.VideoPreparing)
            {
                m_StateLabel.text = "视频准备中";
                return;
            }

            if (viewModel.Mode == EntryMediaControlDockMode.Preparing)
            {
                m_StateLabel.text = "讲解准备中";
                return;
            }

            if (viewModel.Mode == EntryMediaControlDockMode.Error)
            {
                m_StateLabel.text = "播放失败";
                return;
            }

            if (viewModel.Mode == EntryMediaControlDockMode.Video || viewModel.Mode == EntryMediaControlDockMode.Completed)
            {
                if (viewModel.Mode == EntryMediaControlDockMode.Completed)
                {
                    m_StateLabel.text = "播放完成";
                    return;
                }

                if (viewModel.IsMuted)
                {
                    m_StateLabel.text = viewModel.IsPlaying ? "静音播放中" : "已暂停";
                    return;
                }

                m_StateLabel.text = viewModel.IsPlaying ? "播放中" : "已暂停";
                return;
            }

            if (viewModel.Mode == EntryMediaControlDockMode.NarrationCompleted)
            {
                m_StateLabel.text = "导览完成";
                return;
            }

            if (viewModel.IsMuted)
            {
                m_StateLabel.text = "导览已静音";
                return;
            }

            m_StateLabel.text = viewModel.IsPlaying ? "导览播放中" : "导览已暂停";
        }

        private void SetProgress(float progress01, bool hasProgress)
        {
            if (m_ProgressFill == null)
            {
                return;
            }

            float width = 0f;
            if (hasProgress && m_ProgressTrack != null)
            {
                width = m_ProgressTrack.sizeDelta.x * Mathf.Clamp01(progress01);
            }

            m_ProgressFill.sizeDelta = new Vector2(width, m_ProgressFill.sizeDelta.y);
        }

        private void ApplyFrameTintSurface(EntryMediaControlDockMode mode)
        {
            if (m_FrameTint == null)
            {
                return;
            }

            Color color = mode == EntryMediaControlDockMode.Video ||
                          mode == EntryMediaControlDockMode.VideoPreparing ||
                          mode == EntryMediaControlDockMode.Completed
                ? AmberColor
                : AccentColor;
            m_FrameTint.color = new Color(color.r, color.g, color.b, 0.035f);
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private void ConfigureClick(Button button, Action action)
        {
            ConfigureClick(button, action, DuplicateClickDebounceSeconds);
        }

        private void ConfigureClick(Button button, Action action, float debounceSeconds)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(() =>
                {
                    if (IsDuplicateClick(button.name, debounceSeconds))
                    {
                        return;
                    }

                    action.Invoke();
                });
            }
        }

        private bool IsDuplicateClick(string buttonName, float debounceSeconds)
        {
            int frame = Time.frameCount;
            float time = Time.unscaledTime;
            float safeDebounce = Mathf.Max(0f, debounceSeconds);
            bool duplicate = string.Equals(m_LastInvokedButtonName, buttonName, StringComparison.Ordinal) &&
                (m_LastInvokedFrame == frame || time - m_LastInvokedTime <= safeDebounce);

            m_LastInvokedButtonName = buttonName;
            m_LastInvokedFrame = frame;
            m_LastInvokedTime = time;
            return duplicate;
        }
    }
}
