using System;
using BotanicalGardenQR.ApplicationMode.Adapters;
using BotanicalGardenQR.ApplicationMode.Frontend;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.ApplicationMode.Editor
{
    public static class ApplicationModeGatewayPrefabPublisher
    {
        public const string OptionsPath =
            "Assets/BotanicalGardenQR/Content/Authoring/ApplicationModeOptions.asset";
        public const string PrefabPath =
            "Assets/BotanicalGardenQR/Modules/ApplicationMode/Frontend/Prefabs/ApplicationModeGateway.prefab";
        const string FontPath =
            "Assets/BotanicalGardenQR/Resources/BotanicalGardenQR/Fonts/SourceHanSansSC-Regular SDF.asset";

        static readonly Color Ink = new Color(0.012f, 0.035f, 0.031f, 0.985f);
        static readonly Color Surface = new Color(0.028f, 0.085f, 0.073f, 0.98f);
        static readonly Color SurfaceRaised = new Color(0.055f, 0.145f, 0.124f, 0.98f);
        static readonly Color Mint = new Color(0.2f, 0.95f, 0.76f, 1f);
        static readonly Color Amber = new Color(1f, 0.82f, 0.4f, 1f);
        static readonly Color Paper = new Color(0.96f, 0.985f, 0.975f, 1f);
        static readonly Color Muted = new Color(0.69f, 0.79f, 0.75f, 1f);

        [MenuItem("Tools/Botanical Garden/Application Mode/Publish Gateway Prefab")]
        public static void Publish()
        {
            var options = AssetDatabase.LoadAssetAtPath<ApplicationModeOptionsAsset>(OptionsPath);
            if (options == null)
                throw new InvalidOperationException(
                    $"ApplicationMode options asset is missing at '{OptionsPath}'.");

            EnsureFolder("Assets/BotanicalGardenQR/Modules/ApplicationMode/Frontend/Prefabs");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                throw new InvalidOperationException(
                    $"ApplicationMode Chinese TMP font is missing at '{FontPath}'.");

            var root = new GameObject("ApplicationModeGateway");
            try
            {
                var host = root.AddComponent<ApplicationModeControllerHost>();
                var input = root.AddComponent<MetaLeftMenuHoldAdapter>();
                var presenter = root.AddComponent<ApplicationModePromptPresenter>();

                var poseRoot = CreateRect("ApplicationModePromptPose", root.transform);
                poseRoot.sizeDelta = Vector2.zero;

                var promptRoot = CreateRect("ApplicationModePrompt", poseRoot);
                promptRoot.sizeDelta = new Vector2(560f, 420f);
                promptRoot.localScale = Vector3.one * 0.001f;
                var canvas = promptRoot.gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 400;
                var scaler = promptRoot.gameObject.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 18f;
                promptRoot.gameObject.AddComponent<GraphicRaycaster>();
                promptRoot.gameObject.AddComponent<CanvasGroup>();

                var panel = CreateImage(
                    "GlassPanel",
                    promptRoot,
                    Vector2.zero,
                    promptRoot.sizeDelta,
                    Ink,
                    true);
                var outline = panel.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(Mint.r, Mint.g, Mint.b, 0.26f);
                outline.effectDistance = new Vector2(1.4f, -1.4f);

                CreateImage(
                    "TopAccent",
                    promptRoot,
                    new Vector2(0f, 194f),
                    new Vector2(462f, 2f),
                    new Color(Mint.r, Mint.g, Mint.b, 0.78f),
                    false);
                var modeChip = CreateImage(
                    "ModeChip",
                    promptRoot,
                    new Vector2(-202f, 158f),
                    new Vector2(96f, 27f),
                    new Color(Mint.r, Mint.g, Mint.b, 0.13f),
                    false);
                CreateText(
                    "ModeChipLabel",
                    modeChip,
                    Vector2.zero,
                    modeChip.sizeDelta,
                    "STAFF",
                    13f,
                    Mint,
                    font,
                    FontStyles.Bold);

                var title = CreateText(
                    "Title",
                    promptRoot,
                    new Vector2(0f, 150f),
                    new Vector2(430f, 42f),
                    "返回游客模式",
                    30f,
                    Paper,
                    font,
                    FontStyles.Bold);
                var instruction = CreateText(
                    "Instruction",
                    promptRoot,
                    new Vector2(0f, 103f),
                    new Vector2(440f, 48f),
                    "未完成的锚点操作可能丢失；请使用左手柄射线和左扳机选择",
                    17f,
                    Muted,
                    font,
                    FontStyles.Normal);
                var message = CreateText(
                    "Message",
                    promptRoot,
                    new Vector2(0f, 62f),
                    new Vector2(440f, 32f),
                    string.Empty,
                    15f,
                    Amber,
                    font,
                    FontStyles.Normal);

                var returnRoot = CreateRect("ReturnPrompt", promptRoot);
                returnRoot.sizeDelta = promptRoot.sizeDelta;
                var returnCard = CreateImage(
                    "ReturnWarning",
                    returnRoot,
                    new Vector2(0f, -10f),
                    new Vector2(438f, 108f),
                    new Color(Amber.r, Amber.g, Amber.b, 0.1f),
                    false);
                CreateText(
                    "ReturnWarningText",
                    returnCard,
                    Vector2.zero,
                    new Vector2(390f, 78f),
                    "返回前请确认当前锚点操作已经完成。",
                    18f,
                    Paper,
                    font,
                    FontStyles.Normal);
                returnRoot.gameObject.SetActive(false);

                var cancelButton = CreateButton(
                    "Cancel",
                    promptRoot,
                    new Vector2(-105f, -154f),
                    new Vector2(188f, 62f),
                    "取消",
                    font,
                    true);
                var confirmButton = CreateButton(
                    "Confirm",
                    promptRoot,
                    new Vector2(105f, -154f),
                    new Vector2(188f, 62f),
                    "确认",
                    font,
                    false);

                var busyRoot = CreateRect("BusyOverlay", promptRoot);
                busyRoot.sizeDelta = promptRoot.sizeDelta;
                CreateImage(
                    "BusyShade",
                    busyRoot,
                    Vector2.zero,
                    promptRoot.sizeDelta,
                    new Color(0.006f, 0.018f, 0.016f, 0.78f),
                    true);
                CreateText(
                    "BusyLabel",
                    busyRoot,
                    Vector2.zero,
                    new Vector2(390f, 52f),
                    "切换中，请稍候…",
                    21f,
                    Paper,
                    font,
                    FontStyles.Bold);
                busyRoot.gameObject.SetActive(false);

                SetObjectReference(host, "_options", options);
                SetObjectReference(input, "_controllerSource", host);
                var presenterSerialized = new SerializedObject(presenter);
                presenterSerialized.FindProperty("_controllerSource").objectReferenceValue = host;
                presenterSerialized.FindProperty("_promptPoseRoot").objectReferenceValue = poseRoot;
                presenterSerialized.FindProperty("_promptRoot").objectReferenceValue = promptRoot.gameObject;
                presenterSerialized.FindProperty("_returnPromptRoot").objectReferenceValue = returnRoot.gameObject;
                presenterSerialized.FindProperty("_busyRoot").objectReferenceValue = busyRoot.gameObject;
                presenterSerialized.FindProperty("_titleText").objectReferenceValue = title;
                presenterSerialized.FindProperty("_instructionText").objectReferenceValue = instruction;
                presenterSerialized.FindProperty("_messageText").objectReferenceValue = message;
                presenterSerialized.FindProperty("_cancelButton").objectReferenceValue = cancelButton;
                presenterSerialized.FindProperty("_confirmButton").objectReferenceValue = confirmButton;
                presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

                SetLayerRecursively(promptRoot.gameObject, 5);
                promptRoot.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ApplicationMode] Published static gateway Prefab: {PrefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            return rect;
        }

        static RectTransform CreateImage(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color,
            bool raycastTarget)
        {
            var rect = CreateRect(name, parent);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return rect;
        }

        static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            string copy,
            float fontSize,
            Color color,
            TMP_FontAsset font,
            FontStyles fontStyle)
        {
            var rect = CreateRect(name, parent);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.text = copy;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        static Button CreateButton(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            string label,
            TMP_FontAsset font,
            bool quiet)
        {
            var rect = CreateImage(
                name,
                parent,
                position,
                size,
                quiet ? Surface : SurfaceRaised,
                true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.72f, 0.96f, 0.88f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.45f, 0.52f, 0.49f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var aura = CreateImage(
                $"{name}FocusAura",
                rect,
                Vector2.zero,
                size + new Vector2(10f, 10f),
                new Color(Mint.r, Mint.g, Mint.b, 0f),
                false);
            aura.SetAsFirstSibling();
            CreateText(
                $"{name}Label",
                rect,
                Vector2.zero,
                size - new Vector2(12f, 8f),
                label,
                quiet ? 17f : 21f,
                quiet ? Muted : Paper,
                font,
                FontStyles.Bold);

            var progress = CreateImage(
                $"{name}GazeProgress",
                rect,
                new Vector2(0f, -size.y * 0.5f + 6f),
                new Vector2(size.x - 18f, 3f),
                new Color(1f, 1f, 1f, 0.09f),
                false);
            var fill = CreateImage(
                $"{name}GazeProgressFill",
                progress,
                new Vector2(-(size.x - 18f) * 0.5f, 0f),
                new Vector2(0f, 3f),
                quiet ? Amber : Mint,
                false);
            fill.anchorMin = fill.anchorMax = fill.pivot = new Vector2(0f, 0.5f);
            return button;
        }

        static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
