using System;
using BotanicalGardenQR.FrontendShell.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BotanicalGardenQR.Panorama.Frontend
{
    internal enum PanoramaSpatialBubbleIcon
    {
        Exit,
        EnvironmentMoment,
        Images
    }

    internal sealed class PanoramaSpatialBubble : IDisposable
    {
        const float BubbleDiameter = 0.17f;
        const float GazeCanvasScale = 0.001f;
        const float GazeSurfacePixels = 190f;
        const float RingRadius = 0.098f;
        const float RingDepth = -0.091f;
        const int RingSegments = 64;

        static readonly Color EnvironmentOffAccent = new Color(0.48f, 0.88f, 0.96f, 1f);
        readonly GameObject _root;
        readonly Vector3 _baseLocalPosition;
        readonly float _floatPhase;
        readonly Action _selected;
        readonly Button _button;
        readonly Image _gazeFill;
        readonly IFrontendGazeSurfaceRegistration _gazeRegistration;
        readonly GameObject _focusLabelRoot;
        readonly TMP_Text _focusLabel;
        readonly Material _backShellMaterial;
        readonly Material _frontShellMaterial;
        readonly Material _iconMaterial;
        readonly Material _ringTrackMaterial;
        readonly Material _ringProgressMaterial;
        readonly LineRenderer _ringProgress;
        readonly PanoramaSpatialBubbleIcon _icon;

        Color _accent;
        bool _disposed;

        public PanoramaSpatialBubble(
            Transform parent,
            IFrontendGazeSurfaceRegistry gazeSurfaces,
            TMP_FontAsset sharedFont,
            string name,
            Vector3 localPosition,
            PanoramaSpatialBubbleIcon icon,
            Color accent,
            float floatPhase,
            string focusLabel,
            Action selected)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (gazeSurfaces == null) throw new ArgumentNullException(nameof(gazeSurfaces));
            if (sharedFont == null) throw new ArgumentNullException(nameof(sharedFont));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A bubble name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(focusLabel))
                throw new ArgumentException("A focus label is required.", nameof(focusLabel));

            _baseLocalPosition = localPosition;
            _floatPhase = floatPhase;
            _selected = selected ?? throw new ArgumentNullException(nameof(selected));
            _icon = icon;
            _accent = accent;

            _root = new GameObject(name);
            _root.transform.SetParent(parent, false);
            _root.transform.localPosition = localPosition;
            _root.transform.localRotation = Quaternion.identity;

            _backShellMaterial = CreateMaterial(
                "PanoramaSpatialBubble",
                "BotanicalGardenQR/Panorama Spatial Bubble",
                name + "BackShellMaterial",
                (int)RenderQueue.Transparent - 1);
            _frontShellMaterial = CreateMaterial(
                "PanoramaSpatialBubble",
                "BotanicalGardenQR/Panorama Spatial Bubble",
                name + "FrontShellMaterial",
                (int)RenderQueue.Transparent);
            _backShellMaterial.SetFloat("_CullMode", (float)CullMode.Front);
            _backShellMaterial.SetFloat("_LayerOpacity", 0.42f);
            _frontShellMaterial.SetFloat("_CullMode", (float)CullMode.Back);
            _frontShellMaterial.SetFloat("_LayerOpacity", 1f);
            _iconMaterial = CreateMaterial(
                "PanoramaSpatialUnlit",
                "BotanicalGardenQR/Panorama Spatial Unlit",
                name + "IconMaterial",
                (int)RenderQueue.Transparent + 40);
            _ringTrackMaterial = CreateMaterial(
                "PanoramaSpatialUnlit",
                "BotanicalGardenQR/Panorama Spatial Unlit",
                name + "RingTrackMaterial",
                (int)RenderQueue.Transparent + 50);
            _ringProgressMaterial = CreateMaterial(
                "PanoramaSpatialUnlit",
                "BotanicalGardenQR/Panorama Spatial Unlit",
                name + "RingProgressMaterial",
                (int)RenderQueue.Transparent + 51);

            CreateShell(_root.transform, "BubbleBackShell3D", 0.164f, _backShellMaterial, 299);
            CreateShell(_root.transform, "BubbleFrontShell3D", BubbleDiameter, _frontShellMaterial, 300);
            var iconRoot = new GameObject("SpatialIcon");
            iconRoot.transform.SetParent(_root.transform, false);
            if (icon == PanoramaSpatialBubbleIcon.Exit)
            {
                CreateExitIcon(iconRoot.transform, _iconMaterial);
            }
            else if (icon == PanoramaSpatialBubbleIcon.EnvironmentMoment)
            {
                CreateEnvironmentMomentIcon(iconRoot.transform, _iconMaterial);
            }
            else
            {
                CreateImagesIcon(iconRoot.transform, _iconMaterial);
            }

            CreateRing(
                _root.transform,
                "GazeTrack",
                _ringTrackMaterial,
                0.0022f,
                true,
                out _);
            CreateRing(
                _root.transform,
                "GazeProgress3D",
                _ringProgressMaterial,
                0.0042f,
                false,
                out _ringProgress);

            var gazeBinding = CreateGazeSurface(_root.transform, name, gazeSurfaces);
            _button = gazeBinding.Button;
            _gazeFill = gazeBinding.ProgressFill;
            _gazeRegistration = gazeBinding.Registration;
            _button.onClick.AddListener(HandleSelected);
            (_focusLabelRoot, _focusLabel) = CreateFocusLabel(
                _root.transform,
                focusLabel.Trim(),
                sharedFont);

            if (_icon == PanoramaSpatialBubbleIcon.EnvironmentMoment)
                SetEnvironmentMoment(false, default);
            else
                ApplyAccent(_accent);
            UpdateProgressRing(0f);
        }

        public void SetEnvironmentMoment(bool active, Color accent)
        {
            if (_disposed || _icon != PanoramaSpatialBubbleIcon.EnvironmentMoment) return;
            _accent = active ? new Color(accent.r, accent.g, accent.b, 1f) : EnvironmentOffAccent;
            ApplyAccent(_accent);
        }

        public void Tick(float time)
        {
            if (_disposed || _root == null) return;

            var floatOffset = Mathf.Sin((time * 0.72f) + _floatPhase) * 0.012f;
            _root.transform.localPosition = _baseLocalPosition + (Vector3.up * floatOffset);

            var progress = _gazeFill != null ? Mathf.Clamp01(_gazeFill.fillAmount) : 0f;
            _focusLabelRoot.SetActive(_gazeRegistration != null && _gazeRegistration.IsFocused);
            SetMaterialFloat(_backShellMaterial, "_Focus", progress);
            SetMaterialFloat(_frontShellMaterial, "_Focus", progress);
            UpdateProgressRing(progress);
        }

        internal bool IsFocusLabelVisible => !_disposed && _focusLabelRoot != null && _focusLabelRoot.activeSelf;
        internal string FocusLabelText => _focusLabel != null ? _focusLabel.text : string.Empty;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_button != null) _button.onClick.RemoveListener(HandleSelected);
            _gazeRegistration?.Dispose();
            Destroy(_backShellMaterial);
            Destroy(_frontShellMaterial);
            Destroy(_iconMaterial);
            Destroy(_ringTrackMaterial);
            Destroy(_ringProgressMaterial);
            Destroy(_root);
        }

        void HandleSelected()
        {
            if (!_disposed) _selected();
        }

        void ApplyAccent(Color accent)
        {
            var shellMaterials = new[] { _backShellMaterial, _frontShellMaterial };
            foreach (var shellMaterial in shellMaterials)
            {
                SetMaterialColor(
                    shellMaterial,
                    "_BaseColor",
                    new Color(
                        Mathf.Lerp(0.76f, accent.r, 0.28f),
                        Mathf.Lerp(0.96f, accent.g, 0.3f),
                        Mathf.Lerp(1f, accent.b, 0.34f),
                        0.28f));
                SetMaterialColor(shellMaterial, "_RimColor", new Color(accent.r, accent.g, accent.b, 0.9f));
                SetMaterialColor(shellMaterial, "_HighlightColor", new Color(1f, 1f, 1f, 0.95f));
            }

            SetMaterialColor(_iconMaterial, "_BaseColor", new Color(accent.r, accent.g, accent.b, 0.96f));
            SetMaterialColor(_ringTrackMaterial, "_BaseColor", new Color(accent.r, accent.g, accent.b, 0.14f));
            SetMaterialColor(_ringProgressMaterial, "_BaseColor", new Color(accent.r, accent.g, accent.b, 0.96f));
        }

        void UpdateProgressRing(float progress)
        {
            if (_ringProgress == null) return;
            if (progress <= 0.001f)
            {
                _ringProgress.enabled = false;
                _ringProgress.positionCount = 0;
                return;
            }

            _ringProgress.enabled = true;
            var count = Mathf.Clamp(Mathf.CeilToInt(progress * RingSegments) + 1, 2, RingSegments + 1);
            _ringProgress.positionCount = count;
            for (var index = 0; index < count; index++)
            {
                var normalized = index / (float)RingSegments;
                var angle = normalized * Mathf.PI * 2f;
                _ringProgress.SetPosition(
                    index,
                    new Vector3(
                        Mathf.Sin(angle) * RingRadius,
                        Mathf.Cos(angle) * RingRadius,
                        RingDepth));
            }
        }

        static MeshRenderer CreateShell(
            Transform parent,
            string name,
            float diameter,
            Material material,
            int sortingOrder)
        {
            var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = name;
            shell.transform.SetParent(parent, false);
            shell.transform.localPosition = Vector3.zero;
            shell.transform.localRotation = Quaternion.identity;
            shell.transform.localScale = Vector3.one * diameter;
            RemoveCollider(shell);

            var renderer = shell.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureRenderer(renderer, sortingOrder);
            return renderer;
        }

        static void CreateExitIcon(Transform parent, Material material)
        {
            const float z = -0.078f;
            CreateRod(
                parent,
                "ChevronUpper",
                new Vector3(0.017f, 0.03f, z),
                new Vector3(-0.014f, 0f, z),
                0.0048f,
                material);
            CreateRod(
                parent,
                "ChevronLower",
                new Vector3(-0.014f, 0f, z),
                new Vector3(0.017f, -0.03f, z),
                0.0048f,
                material);
        }

        static void CreateEnvironmentMomentIcon(Transform parent, Material material)
        {
            const float z = -0.075f;
            CreateRod(parent, "MomentVertical", new Vector3(0f, 0.044f, z), new Vector3(0f, -0.044f, z), 0.0032f, material);
            CreateRod(parent, "MomentHorizontal", new Vector3(-0.038f, 0f, z), new Vector3(0.038f, 0f, z), 0.0032f, material);
            CreateEllipsoid(parent, "MomentCenter", new Vector3(0f, 0f, z), new Vector3(0.018f, 0.018f, 0.012f), material);
            CreateRod(parent, "MomentAccentA", new Vector3(0.029f, 0.034f, z), new Vector3(0.047f, 0.052f, z), 0.0024f, material);
            CreateRod(parent, "MomentAccentB", new Vector3(0.029f, 0.052f, z), new Vector3(0.047f, 0.034f, z), 0.0024f, material);
        }

        static void CreateImagesIcon(Transform parent, Material material)
        {
            CreateImageFrame(parent, "BackFrame", new Vector2(-0.014f, 0.011f), -0.068f, material);
            CreateImageFrame(parent, "FrontFrame", new Vector2(0.013f, -0.009f), -0.079f, material);
            CreateRod(
                parent,
                "ImageHorizon",
                new Vector3(-0.017f, -0.019f, -0.081f),
                new Vector3(0.031f, 0.008f, -0.081f),
                0.0027f,
                material);
        }

        static void CreateImageFrame(
            Transform parent,
            string name,
            Vector2 offset,
            float z,
            Material material)
        {
            var frame = new GameObject(name);
            frame.transform.SetParent(parent, false);
            var left = offset.x - 0.031f;
            var right = offset.x + 0.031f;
            var bottom = offset.y - 0.024f;
            var top = offset.y + 0.024f;
            CreateRod(frame.transform, "Top", new Vector3(left, top, z), new Vector3(right, top, z), 0.0028f, material);
            CreateRod(frame.transform, "Bottom", new Vector3(left, bottom, z), new Vector3(right, bottom, z), 0.0028f, material);
            CreateRod(frame.transform, "Left", new Vector3(left, bottom, z), new Vector3(left, top, z), 0.0028f, material);
            CreateRod(frame.transform, "Right", new Vector3(right, bottom, z), new Vector3(right, top, z), 0.0028f, material);
        }

        static void CreateEllipsoid(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            var ellipsoid = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ellipsoid.name = name;
            ellipsoid.transform.SetParent(parent, false);
            ellipsoid.transform.localPosition = localPosition;
            ellipsoid.transform.localRotation = Quaternion.identity;
            ellipsoid.transform.localScale = localScale;
            RemoveCollider(ellipsoid);
            var renderer = ellipsoid.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureRenderer(renderer, 340);
        }

        static void CreateRod(
            Transform parent,
            string name,
            Vector3 start,
            Vector3 end,
            float radius,
            Material material)
        {
            var direction = end - start;
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = name;
            rod.transform.SetParent(parent, false);
            rod.transform.localPosition = (start + end) * 0.5f;
            rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            rod.transform.localScale = new Vector3(radius, direction.magnitude * 0.5f, radius);
            RemoveCollider(rod);
            var renderer = rod.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            ConfigureRenderer(renderer, 340);
        }

        static void CreateRing(
            Transform parent,
            string name,
            Material material,
            float width,
            bool fullCircle,
            out LineRenderer line)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.TransformZ;
            line.loop = fullCircle;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 350;

            if (!fullCircle)
            {
                line.enabled = false;
                line.positionCount = 0;
                return;
            }

            line.positionCount = RingSegments;
            for (var index = 0; index < RingSegments; index++)
            {
                var angle = index / (float)RingSegments * Mathf.PI * 2f;
                line.SetPosition(
                    index,
                    new Vector3(
                        Mathf.Sin(angle) * RingRadius,
                        Mathf.Cos(angle) * RingRadius,
                        RingDepth));
            }
        }

        static GazeBinding CreateGazeSurface(
            Transform parent,
            string bubbleName,
            IFrontendGazeSurfaceRegistry gazeSurfaces)
        {
            var canvasRoot = new GameObject(
                "InvisibleGazeSurface",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            canvasRoot.transform.SetParent(parent, false);
            var canvasRect = (RectTransform)canvasRoot.transform;
            canvasRect.sizeDelta = Vector2.one * GazeSurfacePixels;
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * GazeCanvasScale;

            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;

            var canvasGroup = canvasRoot.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            var buttonRect = CreateRect(canvasRect, "SpatialButton", Vector2.one * GazeSurfacePixels);
            var button = buttonRect.gameObject.AddComponent<BotanicalGardenQR.FrontendShell.Contracts.NearOnlyButton>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var hitRect = CreateRect(buttonRect, "HitSurface", Vector2.one * GazeSurfacePixels);
            var hitImage = hitRect.gameObject.AddComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0.001f);
            hitImage.raycastTarget = true;
            button.targetGraphic = hitImage;

            var progressRect = CreateRect(buttonRect, "GazeProgress", Vector2.one * GazeSurfacePixels);
            var progressGroup = progressRect.gameObject.AddComponent<CanvasGroup>();
            progressGroup.alpha = 0f;
            progressGroup.interactable = false;
            progressGroup.blocksRaycasts = false;
            var progressImage = progressRect.gameObject.AddComponent<Image>();
            progressImage.raycastTarget = false;

            var fillRect = CreateRect(progressRect, "Fill", Vector2.one * GazeSurfacePixels);
            var fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillAmount = 0f;
            fillImage.raycastTarget = false;

            var registration = gazeSurfaces.RegisterGazeSurface(
                canvasRoot.transform,
                260,
                bubbleName);
            return new GazeBinding(button, fillImage, registration);
        }

        static (GameObject Root, TMP_Text Label) CreateFocusLabel(
            Transform parent,
            string copy,
            TMP_FontAsset sharedFont)
        {
            var root = new GameObject(
                "FocusLabel",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(360f, 64f);
            rect.localPosition = new Vector3(0f, -0.145f, -0.1f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * 0.001f;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 520;
            var group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var backgroundRect = CreateRect(rect, "Background", rect.sizeDelta);
            var background = backgroundRect.gameObject.AddComponent<Image>();
            background.color = new Color(0.025f, 0.07f, 0.07f, 0.88f);
            background.raycastTarget = false;

            var labelRect = CreateRect(rect, "Copy", rect.sizeDelta - new Vector2(28f, 12f));
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = sharedFont;
            label.fontSize = 23f;
            label.color = new Color(0.9f, 1f, 0.97f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = copy;
            root.SetActive(false);
            return (root, label);
        }

        static RectTransform CreateRect(Transform parent, string name, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        static Material CreateMaterial(
            string resourceName,
            string shaderName,
            string materialName,
            int renderQueue)
        {
            var shader = Resources.Load<Shader>(resourceName) ?? Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"Required Panorama shader is unavailable: {shaderName}");

            var material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave,
                renderQueue = renderQueue
            };
            return material;
        }

        static void ConfigureRenderer(Renderer renderer, int sortingOrder)
        {
            if (renderer == null) return;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingOrder = sortingOrder;
        }

        static void RemoveCollider(GameObject value)
        {
            var collider = value != null ? value.GetComponent<Collider>() : null;
            Destroy(collider);
        }

        static void SetMaterialColor(Material material, string property, Color color)
        {
            if (material != null && material.HasProperty(property))
                material.SetColor(property, color);
        }

        static void SetMaterialFloat(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property))
                material.SetFloat(property, value);
        }

        static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        readonly struct GazeBinding
        {
            public GazeBinding(
                Button button,
                Image progressFill,
                IFrontendGazeSurfaceRegistration registration)
            {
                Button = button;
                ProgressFill = progressFill;
                Registration = registration;
            }

            public Button Button { get; }
            public Image ProgressFill { get; }
            public IFrontendGazeSurfaceRegistration Registration { get; }
        }
    }
}
