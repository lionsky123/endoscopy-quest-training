using System;
using System.Collections.Generic;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.ImageRing.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.ImageRing.Frontend
{
    internal sealed class ImageRingSpatialWorld : IDisposable
    {
        const float MinimumRingRadius = .60f;
        const float MaximumRingRadius = .65f;
        const float CardHeight = -0.03f;

        readonly GameObject _root;
        readonly List<ImageRingSpatialCard> _cards = new List<ImageRingSpatialCard>();
        readonly ImageRingCloseControl _closeControl;
        readonly ImageRingSpatialWorldTicker _ticker;
        int _nextRevealIndex;
        bool _disposed;

        public ImageRingSpatialWorld(
            Transform runtimeRoot,
            Transform viewer,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            TMP_FontAsset sharedFont,
            ImageRingDefinition definition,
            Action<ImageRingItemPlaybackIntent> dispatchPlayback,
            Action requestClose)
        {
            if (runtimeRoot == null) throw new ArgumentNullException(nameof(runtimeRoot));
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));
            if (gazeSurfaces == null) throw new ArgumentNullException(nameof(gazeSurfaces));
            if (sharedFont == null) throw new ArgumentNullException(nameof(sharedFont));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (dispatchPlayback == null) throw new ArgumentNullException(nameof(dispatchPlayback));
            if (requestClose == null) throw new ArgumentNullException(nameof(requestClose));

            _root = new GameObject("ImageRingSpatialWorld");
            _root.transform.SetParent(runtimeRoot, false);
            PlaceAtViewer(_root.transform, viewer);

            try
            {
                CreateCards(definition, gazeSurfaces, sharedFont, dispatchPlayback);
                _closeControl = new ImageRingCloseControl(
                    _root.transform,
                    gazeSurfaces,
                    sharedFont,
                    requestClose);
                _ticker = _root.AddComponent<ImageRingSpatialWorldTicker>();
                _ticker.Bind(this);
                Tick();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Tick()
        {
            if (_disposed) return;
            if (_nextRevealIndex < _cards.Count)
                _cards[_nextRevealIndex++].Reveal();
            foreach (var card in _cards) card.Tick();
            _closeControl?.Tick();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            TryCleanup(() => _ticker?.Unbind());
            TryCleanup(() => _closeControl?.Dispose());
            foreach (var card in _cards) TryCleanup(card.Dispose);
            _cards.Clear();
            TryCleanup(() => ImageRingUiFactory.Destroy(_root));
        }

        void CreateCards(
            ImageRingDefinition definition,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            TMP_FontAsset font,
            Action<ImageRingItemPlaybackIntent> dispatchPlayback)
        {
            var count = definition.Items.Count;
            var radius = Mathf.Lerp(
                MinimumRingRadius,
                MaximumRingRadius,
                Mathf.InverseLerp(
                    ImageRingDefinition.MinimumItemCount,
                    ImageRingDefinition.MaximumItemCount,
                    count));
            for (var index = 0; index < count; index++)
            {
                var angle = index * Mathf.PI * 2f / count;
                var radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                var position = radial * radius;
                position.y = CardHeight;
                _cards.Add(new ImageRingSpatialCard(
                    _root.transform,
                    gazeSurfaces,
                    font,
                    definition.Items[index],
                    index,
                    count,
                    position,
                    Quaternion.LookRotation(radial, Vector3.up),
                    dispatchPlayback));
            }
        }

        static void PlaceAtViewer(Transform root, Transform viewer)
        {
            var forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            root.SetPositionAndRotation(
                viewer.position,
                Quaternion.LookRotation(forward, Vector3.up));
        }

        static void TryCleanup(Action cleanup)
        {
            try { cleanup(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    internal sealed class ImageRingSpatialWorldTicker : MonoBehaviour
    {
        ImageRingSpatialWorld _world;

        public void Bind(ImageRingSpatialWorld world) => _world = world;
        public void Unbind() => _world = null;
        void LateUpdate() => _world?.Tick();
    }

    internal sealed class ImageRingSpatialCard : IDisposable
    {
        const float CanvasScale = 0.00072f;
        const float FocusScale = 1.65f;
        const float FocusSmoothSeconds = 0.14f;
        const float FocusLossGraceSeconds = 0.25f;

        static readonly Vector2 CardPixels = new Vector2(880f, 680f);
        static readonly Vector2 PanelPixels = new Vector2(840f, 630f);
        static readonly Vector2 ImageViewportPixels = new Vector2(780f, 390f);
        static readonly Vector2 ImagePosition = new Vector2(0f, 54f);
        static readonly Color Accent = new Color(0.208f, 0.949f, 0.761f, 1f);
        static readonly Color PanelIdle = new Color(0.008f, 0.012f, 0.018f, 0.9f);
        static readonly Color ImageWellColor = new Color(0.002f, 0.004f, 0.008f, 0.98f);
        static readonly Color TopHighlightColor = new Color(1f, 1f, 1f, 0.075f);
        static readonly Color IndexColor = new Color(Accent.r, Accent.g, Accent.b, 0.72f);
        static readonly Color DescriptionColor = new Color(0.74f, 0.77f, 0.82f, 1f);

        readonly GameObject _root;
        readonly Canvas _canvas;
        readonly Image _panel;
        readonly Image _topHighlight;
        readonly Image _imageWell;
        readonly RawImage _mainImage;
        readonly TextMeshProUGUI _indexLabel;
        readonly TextMeshProUGUI _title;
        readonly TextMeshProUGUI _description;
        readonly Button _button;
        readonly Image _expandedImageHitSurface;
        readonly ImageRingBorderGraphic _imageFrame;
        readonly ImageRingBorderGraphic _imageHalo;
        readonly ImageRingGazeProgressPresenter _progressPresenter;
        readonly IFrontendGazeSurfaceRegistration _gazeRegistration;
        readonly ImageRingItemDefinition _item;
        readonly Action<ImageRingItemPlaybackIntent> _dispatchPlayback;
        readonly int _itemIndex;
        readonly int _itemCount;
        float _focusBlend;
        float _focusVelocity;
        float _focusLostAt = -1f;
        bool _audioActivated;
        bool _revealed;
        bool _disposed;

        public ImageRingSpatialCard(
            Transform parent,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            TMP_FontAsset font,
            ImageRingItemDefinition item,
            int index,
            int itemCount,
            Vector3 localPosition,
            Quaternion localRotation,
            Action<ImageRingItemPlaybackIntent> dispatchPlayback)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (gazeSurfaces == null) throw new ArgumentNullException(nameof(gazeSurfaces));
            if (font == null) throw new ArgumentNullException(nameof(font));
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _dispatchPlayback = dispatchPlayback ?? throw new ArgumentNullException(nameof(dispatchPlayback));
            _itemIndex = index;
            _itemCount = itemCount;

            _root = ImageRingUiFactory.CreateCanvasRoot(
                parent,
                $"ImageRingCard_{index + 1:00}",
                CardPixels,
                CanvasScale,
                320);
            _root.transform.localPosition = localPosition;
            _root.transform.localRotation = localRotation;
            _canvas = _root.GetComponent<Canvas>();
            var canvasRect = (RectTransform)_root.transform;

            _panel = ImageRingUiFactory.CreateImage(
                canvasRect,
                "CardSurface",
                PanelPixels,
                new Vector2(0f, 6f),
                PanelIdle,
                false);
            var panelShadow = _panel.gameObject.AddComponent<Shadow>();
            panelShadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
            panelShadow.effectDistance = new Vector2(0f, -7f);
            panelShadow.useGraphicAlpha = true;
            _topHighlight = ImageRingUiFactory.CreateImage(
                _panel.rectTransform,
                "TopHighlight",
                new Vector2(792f, 3f),
                new Vector2(0f, 306f),
                TopHighlightColor,
                false);

            _indexLabel = ImageRingUiFactory.CreateText(
                _panel.rectTransform,
                "ItemIndex",
                new Vector2(780f, 30f),
                new Vector2(0f, 284f),
                string.Empty,
                font,
                18f,
                IndexColor,
                FontStyles.Bold);
            _indexLabel.alignment = TextAlignmentOptions.Left;
            _indexLabel.characterSpacing = 1.5f;

            _imageWell = ImageRingUiFactory.CreateImage(
                _panel.rectTransform,
                "ImageWell",
                new Vector2(800f, 410f),
                ImagePosition,
                ImageWellColor,
                false);
            var imageRect = ImageRingUiFactory.CreateRect(
                _panel.rectTransform,
                "MainImage",
                ImageViewportPixels,
                ImagePosition);
            _mainImage = imageRect.gameObject.AddComponent<RawImage>();
            _mainImage.color = Color.white;
            _mainImage.raycastTarget = false;
            _imageHalo = ImageRingUiFactory.CreateBorder(
                _panel.rectTransform,
                "ImageFocusHalo",
                ImageViewportPixels + Vector2.one * 32f,
                ImagePosition,
                12f,
                new Color(Accent.r, Accent.g, Accent.b, 0.025f),
                true);
            _imageFrame = ImageRingUiFactory.CreateBorder(
                _panel.rectTransform,
                "ImageFocusFrame",
                ImageViewportPixels + Vector2.one * 8f,
                ImagePosition,
                3f,
                new Color(Accent.r, Accent.g, Accent.b, 0.16f));

            _title = ImageRingUiFactory.CreateText(
                _panel.rectTransform,
                "Title",
                new Vector2(780f, 42f),
                new Vector2(0f, -176f),
                string.Empty,
                font,
                28f,
                Color.white,
                FontStyles.Bold);
            _title.alignment = TextAlignmentOptions.Left;
            _description = ImageRingUiFactory.CreateText(
                _panel.rectTransform,
                "Description",
                new Vector2(780f, 72f),
                new Vector2(0f, -238f),
                string.Empty,
                font,
                20f,
                DescriptionColor,
                FontStyles.Normal);
            _description.alignment = TextAlignmentOptions.TopLeft;

            var buttonRect = ImageRingUiFactory.CreateRect(
                canvasRect,
                "CardButton",
                PanelPixels,
                new Vector2(0f, 6f));
            var hitSurface = buttonRect.gameObject.AddComponent<Image>();
            hitSurface.color = new Color(1f, 1f, 1f, 0.001f);
            hitSurface.raycastTarget = true;
            _button = buttonRect.gameObject.AddComponent<BotanicalGardenQR.FrontendShell.Contracts.NearOnlyButton>();
            _button.targetGraphic = hitSurface;
            _button.transition = Selectable.Transition.None;
            _button.navigation = new Navigation { mode = Navigation.Mode.None };
            _button.onClick.AddListener(HandleSelected);
            _expandedImageHitSurface = ImageRingUiFactory.CreateImage(
                buttonRect,
                "ExpandedImageHitSurface",
                ImageViewportPixels,
                ImagePosition,
                new Color(1f, 1f, 1f, 0.001f),
                true);
            _expandedImageHitSurface.gameObject.SetActive(false);

            var progressTrack = ImageRingUiFactory.CreateGazeProgressBar(
                buttonRect,
                ImageViewportPixels.x,
                Accent,
                out var progressFill);
            _progressPresenter = buttonRect.gameObject.AddComponent<ImageRingGazeProgressPresenter>();
            _progressPresenter.Bind(progressTrack, progressFill, Accent);

            _root.SetActive(false);
            _gazeRegistration = gazeSurfaces.RegisterGazeSurface(
                _root.transform,
                300,
                $"image-ring-card-{index + 1}");
        }

        public void Reveal()
        {
            if (_disposed || _revealed) return;
            _mainImage.texture = _item.Image;
            var imageSize = ImageRingUiFactory.Fit(
                _item.Image,
                ImageViewportPixels.x,
                ImageViewportPixels.y);
            _mainImage.rectTransform.sizeDelta = imageSize;
            _mainImage.rectTransform.pivot = new Vector2(0.5f, 0f);
            _mainImage.rectTransform.anchoredPosition = new Vector2(
                ImagePosition.x,
                ImagePosition.y - imageSize.y * 0.5f);
            MatchImageRect(_imageFrame.rectTransform, imageSize, 4f);
            MatchImageRect(_imageHalo.rectTransform, imageSize, 16f);
            MatchImageRect(_expandedImageHitSurface.rectTransform, imageSize, 10f);
            _indexLabel.text = $"图像  {_itemIndex + 1:00}  /  {_itemCount:00}";
            _title.text = _item.Title;
            _description.text = _item.Description;
            _progressPresenter.SetLayout(_mainImage.rectTransform);
            _root.SetActive(true);
            _revealed = true;
        }

        public void Tick()
        {
            if (_disposed || !_revealed || _root == null || _panel == null) return;
            var focused = _gazeRegistration != null && _gazeRegistration.IsFocused;
            _focusBlend = Mathf.SmoothDamp(
                _focusBlend,
                focused || _audioActivated ? 1f : 0f,
                ref _focusVelocity,
                FocusSmoothSeconds,
                Mathf.Infinity,
                Time.unscaledDeltaTime);
            var focus = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_focusBlend));
            var visualScale = Mathf.Lerp(1f, FocusScale, focus);
            _mainImage.rectTransform.localScale = Vector3.one * visualScale;
            _expandedImageHitSurface.rectTransform.localScale = Vector3.one * visualScale;
            _expandedImageHitSurface.gameObject.SetActive(focus > 0.01f || _audioActivated);
            SetChromeOpacity(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.82f, focus)));
            _canvas.sortingOrder = focus > 0.001f || _audioActivated ? 350 : 320;
            UpdateImageFocusEffects(focus, visualScale);

            if (focused)
            {
                _focusLostAt = -1f;
                return;
            }
            if (!_audioActivated) return;
            if (_focusLostAt < 0f)
            {
                _focusLostAt = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - _focusLostAt < FocusLossGraceSeconds) return;

            _audioActivated = false;
            _focusLostAt = -1f;
            _dispatchPlayback(new ImageRingItemPlaybackIntent(
                _itemIndex,
                ImageRingItemPlaybackIntentKind.Stop));
        }

        void UpdateImageFocusEffects(float focus, float visualScale)
        {
            if (_imageFrame == null || _imageHalo == null) return;
            var progress = _progressPresenter.CurrentProgress;
            var emphasis = Mathf.Max(focus, progress);
            var frameAlpha = Mathf.Lerp(0.16f, 0.68f, emphasis);
            var haloAlpha = Mathf.Lerp(0.025f, 0.2f, emphasis);
            var haloPulseScale = 1f;
            if (_audioActivated)
            {
                var pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 3.8f) * 0.5f;
                frameAlpha = 0.78f + pulse * 0.12f;
                haloAlpha = 0.18f + pulse * 0.12f;
                haloPulseScale = 1.008f + pulse * 0.012f;
            }

            _imageFrame.color = new Color(Accent.r, Accent.g, Accent.b, frameAlpha);
            _imageHalo.color = new Color(Accent.r, Accent.g, Accent.b, haloAlpha);
            _imageFrame.rectTransform.localScale = Vector3.one * visualScale;
            _imageHalo.rectTransform.localScale = Vector3.one * visualScale * haloPulseScale;
        }

        void SetChromeOpacity(float opacity)
        {
            var value = Mathf.Clamp01(opacity);
            _panel.color = WithAlpha(PanelIdle, PanelIdle.a * value);
            _imageWell.color = WithAlpha(ImageWellColor, ImageWellColor.a * value);
            _topHighlight.color = WithAlpha(TopHighlightColor, TopHighlightColor.a * value);
            _indexLabel.color = WithAlpha(IndexColor, IndexColor.a * value);
            _title.color = WithAlpha(Color.white, value);
            _description.color = WithAlpha(DescriptionColor, value);
        }

        static void MatchImageRect(RectTransform target, Vector2 imageSize, float padding)
        {
            target.pivot = new Vector2(0.5f, 0f);
            target.sizeDelta = imageSize + Vector2.one * (padding * 2f);
            target.anchoredPosition = new Vector2(
                ImagePosition.x,
                ImagePosition.y - imageSize.y * 0.5f - padding);
        }

        static Color WithAlpha(Color color, float alpha)
            => new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_button != null) _button.onClick.RemoveListener(HandleSelected);
            _gazeRegistration?.Dispose();
            ImageRingUiFactory.Destroy(_root);
        }

        void HandleSelected()
        {
            if (_disposed || !_revealed) return;
            _audioActivated = true;
            _focusLostAt = -1f;
            _dispatchPlayback(new ImageRingItemPlaybackIntent(
                _itemIndex,
                ImageRingItemPlaybackIntentKind.Play));
        }
    }

    internal sealed class ImageRingCloseControl : IDisposable
    {
        const float CanvasScale = 0.00072f;
        static readonly Color Accent = new Color(0.208f, 0.949f, 0.761f, 1f);
        static readonly Color Idle = new Color(0.012f, 0.018f, 0.026f, 0.92f);
        static readonly Color Focused = new Color(0.022f, 0.03f, 0.042f, 0.98f);

        readonly GameObject _root;
        readonly Image _surface;
        readonly ImageRingBorderGraphic _border;
        readonly Button _button;
        readonly IFrontendGazeSurfaceRegistration _gazeRegistration;
        readonly Action _requestClose;
        bool _disposed;

        public ImageRingCloseControl(
            Transform parent,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            TMP_FontAsset font,
            Action requestClose)
        {
            _requestClose = requestClose ?? throw new ArgumentNullException(nameof(requestClose));
            _root = ImageRingUiFactory.CreateCanvasRoot(
                parent,
                "ImageRingCloseControl",
                new Vector2(300f, 118f),
                CanvasScale,
                390);
            _root.transform.localPosition = new Vector3(0f, -0.32f, .52f);
            _root.transform.localRotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

            var rootRect = (RectTransform)_root.transform;
            var buttonRect = ImageRingUiFactory.CreateRect(
                rootRect,
                "CloseButton",
                new Vector2(280f, 76f),
                new Vector2(0f, 10f));
            _surface = buttonRect.gameObject.AddComponent<Image>();
            _surface.color = Idle;
            _button = buttonRect.gameObject.AddComponent<BotanicalGardenQR.FrontendShell.Contracts.NearOnlyButton>();
            _button.targetGraphic = _surface;
            _button.transition = Selectable.Transition.None;
            _button.navigation = new Navigation { mode = Navigation.Mode.None };
            _button.onClick.AddListener(HandleSelected);
            var shadow = _surface.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.24f);
            shadow.effectDistance = new Vector2(0f, -4f);
            shadow.useGraphicAlpha = true;

            ImageRingUiFactory.CreateText(
                buttonRect,
                "Label",
                new Vector2(240f, 54f),
                Vector2.zero,
                "返回 360",
                font,
                23f,
                Color.white,
                FontStyles.Bold);
            var progressTrack = ImageRingUiFactory.CreateGazeProgressBar(
                buttonRect,
                246f,
                Accent,
                out var progressFill);
            progressTrack.anchoredPosition = new Vector2(0f, -47f);
            var progressPresenter = buttonRect.gameObject.AddComponent<ImageRingGazeProgressPresenter>();
            progressPresenter.Bind(progressTrack, progressFill, Accent);
            _border = ImageRingUiFactory.CreateBorder(
                rootRect,
                "CloseSelectionBorder",
                new Vector2(284f, 80f),
                new Vector2(0f, 10f),
                2f,
                new Color(1f, 1f, 1f, 0.06f));
            _gazeRegistration = gazeSurfaces.RegisterGazeSurface(
                _root.transform,
                340,
                "image-ring-close");
        }

        public void Tick()
        {
            if (_disposed || _surface == null) return;
            var focused = _gazeRegistration != null && _gazeRegistration.IsFocused;
            var blend = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            _surface.color = Color.Lerp(_surface.color, focused ? Focused : Idle, blend);
            _border.color = Color.Lerp(
                new Color(1f, 1f, 1f, 0.06f),
                new Color(Accent.r, Accent.g, Accent.b, 0.32f),
                focused ? 1f : 0f);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_button != null) _button.onClick.RemoveListener(HandleSelected);
            _gazeRegistration?.Dispose();
            ImageRingUiFactory.Destroy(_root);
        }

        void HandleSelected()
        {
            if (!_disposed) _requestClose();
        }
    }

    internal sealed class ImageRingGazeProgressPresenter : MonoBehaviour, IFrontendGazeProgressPresenter
    {
        RectTransform _track;
        RectTransform _fillRect;
        Image _trackImage;
        Image _fillImage;
        Color _accent;

        public float CurrentProgress { get; private set; }

        public void Bind(RectTransform track, Image fill, Color accent)
        {
            _track = track != null ? track : throw new ArgumentNullException(nameof(track));
            _fillImage = fill != null ? fill : throw new ArgumentNullException(nameof(fill));
            _fillRect = _fillImage.rectTransform;
            _trackImage = _track.GetComponent<Image>();
            _accent = accent;
            PresentGazeProgress(0f);
        }

        public void SetLayout(RectTransform image)
        {
            if (_track == null || _fillRect == null || image == null) return;
            var imageSize = image.sizeDelta;
            var imageCenterX = image.anchoredPosition.x + imageSize.x * (0.5f - image.pivot.x);
            var imageBottom = image.anchoredPosition.y - imageSize.y * image.pivot.y;
            _track.sizeDelta = new Vector2(imageSize.x, 5f);
            _track.anchoredPosition = new Vector2(
                imageCenterX,
                imageBottom - 12f);
            _fillRect.sizeDelta = _track.sizeDelta;
        }

        public void PresentGazeProgress(float progress)
        {
            if (_trackImage == null || _fillImage == null) return;
            var value = Mathf.Clamp01(progress);
            CurrentProgress = value;
            _fillImage.fillAmount = value;
            _trackImage.color = new Color(_accent.r, _accent.g, _accent.b, value > 0f ? 0.2f : 0.1f);
            _fillImage.color = new Color(_accent.r, _accent.g, _accent.b, value > 0f ? 0.92f : 0f);
        }

        void OnDisable() => PresentGazeProgress(0f);
    }

    internal sealed class ImageRingBorderGraphic : MaskableGraphic
    {
        float _thickness = 2f;
        bool _softEdge;

        public void Configure(float thickness, bool softEdge = false)
        {
            _thickness = Mathf.Max(1f, thickness);
            _softEdge = softEdge;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            var rect = rectTransform.rect;
            var thickness = Mathf.Min(_thickness, Mathf.Min(rect.width, rect.height) * 0.5f);
            if (_softEdge)
            {
                AddSoftBorder(helper, rect, thickness, color);
                return;
            }

            AddQuad(helper, new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            AddQuad(helper, new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            AddQuad(helper, new Rect(rect.xMin, rect.yMin + thickness, thickness, rect.height - thickness * 2f), color);
            AddQuad(helper, new Rect(rect.xMax - thickness, rect.yMin + thickness, thickness, rect.height - thickness * 2f), color);
        }

        static void AddSoftBorder(VertexHelper helper, Rect rect, float thickness, Color32 inner)
        {
            var transparent = inner;
            transparent.a = 0;
            AddQuad(
                helper,
                new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness),
                inner,
                transparent,
                transparent,
                inner);
            AddQuad(
                helper,
                new Rect(rect.xMin, rect.yMin, rect.width, thickness),
                transparent,
                inner,
                inner,
                transparent);
            AddQuad(
                helper,
                new Rect(rect.xMin, rect.yMin + thickness, thickness, rect.height - thickness * 2f),
                transparent,
                transparent,
                inner,
                inner);
            AddQuad(
                helper,
                new Rect(rect.xMax - thickness, rect.yMin + thickness, thickness, rect.height - thickness * 2f),
                inner,
                inner,
                transparent,
                transparent);
        }

        static void AddQuad(VertexHelper helper, Rect rect, Color32 color)
            => AddQuad(helper, rect, color, color, color, color);

        static void AddQuad(
            VertexHelper helper,
            Rect rect,
            Color32 bottomLeft,
            Color32 topLeft,
            Color32 topRight,
            Color32 bottomRight)
        {
            var start = helper.currentVertCount;
            var vertex = UIVertex.simpleVert;
            vertex.color = bottomLeft;
            vertex.position = new Vector3(rect.xMin, rect.yMin);
            helper.AddVert(vertex);
            vertex.color = topLeft;
            vertex.position = new Vector3(rect.xMin, rect.yMax);
            helper.AddVert(vertex);
            vertex.color = topRight;
            vertex.position = new Vector3(rect.xMax, rect.yMax);
            helper.AddVert(vertex);
            vertex.color = bottomRight;
            vertex.position = new Vector3(rect.xMax, rect.yMin);
            helper.AddVert(vertex);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }
    }

    internal static class ImageRingUiFactory
    {
        public static GameObject CreateCanvasRoot(
            Transform parent,
            string name,
            Vector2 pixelSize,
            float scale,
            int sortingOrder)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = pixelSize;
            rect.localScale = Vector3.one * scale;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            return root;
        }

        public static RectTransform CreateRect(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 position)
        {
            var value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            var rect = (RectTransform)value.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        public static Image CreateImage(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 position,
            Color color,
            bool raycastTarget)
        {
            var rect = CreateRect(parent, name, size, position);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 position,
            string value,
            TMP_FontAsset font,
            float fontSize,
            Color color,
            FontStyles style)
        {
            var rect = CreateRect(parent, name, size, position);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value ?? string.Empty;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        public static RectTransform CreateGazeProgressBar(
            Transform button,
            float width,
            Color accent,
            out Image fill)
        {
            var track = CreateImage(
                button,
                "DwellProgressTrack",
                new Vector2(width, 5f),
                Vector2.zero,
                new Color(accent.r, accent.g, accent.b, 0.1f),
                false);
            fill = CreateImage(
                track.rectTransform,
                "Fill",
                track.rectTransform.sizeDelta,
                Vector2.zero,
                new Color(accent.r, accent.g, accent.b, 0f),
                false);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            return track.rectTransform;
        }

        public static ImageRingBorderGraphic CreateBorder(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 position,
            float thickness,
            Color color,
            bool softEdge = false)
        {
            var rect = CreateRect(parent, name, size, position);
            var border = rect.gameObject.AddComponent<ImageRingBorderGraphic>();
            border.color = color;
            border.Configure(thickness, softEdge);
            return border;
        }

        public static Vector2 Fit(Texture2D texture, float maximumWidth, float maximumHeight)
        {
            var width = Mathf.Max(1, texture.width);
            var height = Mathf.Max(1, texture.height);
            var scale = Mathf.Min(maximumWidth / width, maximumHeight / height);
            return new Vector2(width * scale, height * scale);
        }

        public static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
