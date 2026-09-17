using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    internal static class EntryUIShapes
    {
        private const int SpriteSize = 64;
        private const float TargetGeneratedTextureDensity = 3f;
        private const int CoverageSampleSteps = 3;
        private const float CanonicalStraightSegment = 4f;
        private const float MaxGeneratedCornerRadius = 96f;
        private const float ParameterStepsPerPixel = 4f;
        private const int MaxProgressTextureEdge = 1024;
        private const int MaxProgressTexturePixels = 524288;
        private const float TargetProgressTextureDensity = 2f;
        private const float RoundedCornerRadius = 18f;
        private const float SoftRectCornerRadius = 10f;
        private const float OutlineWidth = 2f;
        internal const int MaxSizedRoundedSpriteCount = 32;
        internal const int MaxSizedRoundedTexturePixels = 8388608;
        private static Sprite s_RoundedFillSprite;
        private static Sprite s_RoundedOutlineSprite;
        private static Sprite s_SoftRectFillSprite;
        private static Sprite s_SoftRectOutlineSprite;
        private static readonly Dictionary<string, Sprite> s_SizedRoundedSprites = new Dictionary<string, Sprite>();
        private static int s_SizedRoundedTexturePixels;

        internal static int SizedRoundedSpriteCount => s_SizedRoundedSprites.Count;
        internal static int SizedRoundedTexturePixels => s_SizedRoundedTexturePixels;

        public static void ApplyRoundedFill(Image image)
        {
            ApplySprite(image, RoundedFillSprite);
        }

        public static void ApplyRoundedOutline(Image image)
        {
            ApplySprite(image, RoundedOutlineSprite);
        }

        public static void ApplySoftRectFill(Image image)
        {
            ApplySprite(image, SoftRectFillSprite);
        }

        public static void ApplySoftRectOutline(Image image)
        {
            ApplySprite(image, SoftRectOutlineSprite);
        }

        public static void ApplySizedRoundedFill(Image image, Vector2 size, float cornerRadius)
        {
            var sprite = GetSizedRoundedSprite("fill", size, cornerRadius, 0f);
            ApplySprite(image, sprite ?? RoundedFillSprite, Image.Type.Sliced);
        }

        public static void ApplySizedRoundedOutline(Image image, Vector2 size, float cornerRadius, float outlineWidth = 1.2f)
        {
            var sprite = GetSizedRoundedSprite("outline", size, cornerRadius, outlineWidth);
            ApplySprite(image, sprite ?? RoundedOutlineSprite, Image.Type.Sliced);
        }

        public static void ApplySizedRoundedProgressOutline(
            Image image,
            Vector2 size,
            float cornerRadius,
            float outlineWidth = 1.2f)
        {
            var sprite = GetProgressRoundedSprite(size, cornerRadius, outlineWidth);
            ApplySprite(image, sprite ?? RoundedOutlineSprite, Image.Type.Filled);
        }

        private static Sprite RoundedFillSprite
        {
            get
            {
                if (s_RoundedFillSprite == null)
                {
                    s_RoundedFillSprite = CreateRoundedSprite("XREALRoundedFill", RoundedCornerRadius);
                }

                return s_RoundedFillSprite;
            }
        }

        private static Sprite RoundedOutlineSprite
        {
            get
            {
                if (s_RoundedOutlineSprite == null)
                {
                    s_RoundedOutlineSprite = CreateRoundedSprite("XREALRoundedOutline", RoundedCornerRadius, true);
                }

                return s_RoundedOutlineSprite;
            }
        }

        private static Sprite SoftRectFillSprite
        {
            get
            {
                if (s_SoftRectFillSprite == null)
                {
                    s_SoftRectFillSprite = CreateRoundedSprite("XREALSoftRectFill", SoftRectCornerRadius);
                }

                return s_SoftRectFillSprite;
            }
        }

        private static Sprite SoftRectOutlineSprite
        {
            get
            {
                if (s_SoftRectOutlineSprite == null)
                {
                    s_SoftRectOutlineSprite = CreateRoundedSprite("XREALSoftRectOutline", SoftRectCornerRadius, true);
                }

                return s_SoftRectOutlineSprite;
            }
        }

        private static void ApplySprite(Image image, Sprite sprite, Image.Type imageType = Image.Type.Sliced)
        {
            if (image == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = imageType;
        }

        private static Sprite CreateRoundedSprite(string name, float cornerRadius, bool outline = false)
        {
            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
            {
                name = name + "Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            for (int y = 0; y < SpriteSize; y++)
            {
                for (int x = 0; x < SpriteSize; x++)
                {
                    float alpha = outline
                        ? RoundedOutlineAlpha(x, y, cornerRadius)
                        : RoundedAlpha(x, y, SpriteSize, cornerRadius);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, SpriteSize, SpriteSize),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius));
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static Sprite GetSizedRoundedSprite(string mode, Vector2 size, float cornerRadius, float outlineWidth)
        {
            var maximumRadius = Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.5f;
            var displayRadius = Quantize(Mathf.Clamp(cornerRadius, 0f, Mathf.Min(maximumRadius, MaxGeneratedCornerRadius)));
            var displayStroke = Quantize(Mathf.Clamp(outlineWidth, 0f, displayRadius));
            var radius = displayRadius * TargetGeneratedTextureDensity;
            var stroke = displayStroke * TargetGeneratedTextureDensity;
            var displayEdge = Mathf.Max(2f, displayRadius * 2f + CanonicalStraightSegment);
            var textureEdge = Mathf.Max(2, Mathf.CeilToInt(displayEdge * TargetGeneratedTextureDensity));
            var key = mode + ":" + Mathf.RoundToInt(displayRadius * ParameterStepsPerPixel) + ":" +
                      Mathf.RoundToInt(displayStroke * ParameterStepsPerPixel);
            if (s_SizedRoundedSprites.TryGetValue(key, out var sprite))
            {
                return sprite;
            }

            var texturePixels = textureEdge * textureEdge;
            if (s_SizedRoundedSprites.Count >= MaxSizedRoundedSpriteCount ||
                (long)s_SizedRoundedTexturePixels + texturePixels > MaxSizedRoundedTexturePixels)
                return null;

            sprite = CreateSizedRoundedSprite(
                "XREALSlicedRounded_" + key,
                textureEdge,
                radius,
                stroke,
                TargetGeneratedTextureDensity);
            s_SizedRoundedSprites[key] = sprite;
            s_SizedRoundedTexturePixels += texturePixels;
            return sprite;
        }

        private static float Quantize(float value)
            => Mathf.Round(value * ParameterStepsPerPixel) / ParameterStepsPerPixel;

        private static Sprite GetProgressRoundedSprite(
            Vector2 size,
            float cornerRadius,
            float outlineWidth)
        {
            var displayWidth = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(size.x)), 1, MaxProgressTextureEdge);
            var displayHeight = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(size.y)), 1, MaxProgressTextureEdge);
            var edgeLimited = MaxProgressTextureEdge / (float)Mathf.Max(displayWidth, displayHeight);
            var areaLimited = Mathf.Sqrt(MaxProgressTexturePixels / Mathf.Max(1f, displayWidth * displayHeight));
            var density = Mathf.Clamp(
                Mathf.Min(TargetProgressTextureDensity, edgeLimited, areaLimited),
                1f,
                TargetProgressTextureDensity);
            var textureWidth = Mathf.Max(1, Mathf.CeilToInt(displayWidth * density));
            var textureHeight = Mathf.Max(1, Mathf.CeilToInt(displayHeight * density));
            var actualDensity = Mathf.Min(
                textureWidth / (float)displayWidth,
                textureHeight / (float)displayHeight);
            var radius = Mathf.Clamp(
                cornerRadius * actualDensity,
                0f,
                Mathf.Min(textureWidth, textureHeight) * 0.5f);
            var stroke = Mathf.Clamp(outlineWidth * actualDensity, 0f, radius);
            var key = "progress:" + displayWidth + "x" + displayHeight + ":" +
                      Mathf.RoundToInt(radius * ParameterStepsPerPixel) + ":" +
                      Mathf.RoundToInt(stroke * ParameterStepsPerPixel);
            if (s_SizedRoundedSprites.TryGetValue(key, out var sprite)) return sprite;

            var texturePixels = textureWidth * textureHeight;
            if (s_SizedRoundedSprites.Count >= MaxSizedRoundedSpriteCount ||
                (long)s_SizedRoundedTexturePixels + texturePixels > MaxSizedRoundedTexturePixels)
                return null;

            sprite = CreateFullSizedRoundedSprite(
                "XREALProgressRounded_" + key,
                textureWidth,
                textureHeight,
                radius,
                stroke,
                actualDensity);
            s_SizedRoundedSprites[key] = sprite;
            s_SizedRoundedTexturePixels += texturePixels;
            return sprite;
        }

        private static Sprite CreateSizedRoundedSprite(
            string name,
            int edge,
            float radius,
            float outlineWidth,
            float pixelsPerUnit)
        {
            var texture = new Texture2D(edge, edge, TextureFormat.RGBA32, false)
            {
                name = name + "Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[edge * edge];
            bool outline = outlineWidth > 0f;
            for (int y = 0; y < edge; y++)
            {
                for (int x = 0; x < edge; x++)
                {
                    float alpha = SampleRoundedRectAlpha(x, y, edge, edge, radius, outline ? outlineWidth : 0f);
                    pixels[y * edge + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, edge, edge),
                new Vector2(0.5f, 0.5f),
                Mathf.Max(1f, pixelsPerUnit * 100f),
                0,
                SpriteMeshType.FullRect,
                Vector4.one * Mathf.Clamp(Mathf.CeilToInt(radius), 1, (edge - 1) / 2));
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static Sprite CreateFullSizedRoundedSprite(
            string name,
            int width,
            int height,
            float radius,
            float outlineWidth,
            float pixelsPerUnit)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name + "Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var alpha = SampleRoundedRectAlpha(x, y, width, height, radius, outlineWidth);
                    pixels[y * width + x] =
                        new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                Mathf.Max(1f, pixelsPerUnit),
                0,
                SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGeneratedSprites()
        {
            DestroySprite(ref s_RoundedFillSprite);
            DestroySprite(ref s_RoundedOutlineSprite);
            DestroySprite(ref s_SoftRectFillSprite);
            DestroySprite(ref s_SoftRectOutlineSprite);
            foreach (var sprite in s_SizedRoundedSprites.Values)
                DestroySprite(sprite);
            s_SizedRoundedSprites.Clear();
            s_SizedRoundedTexturePixels = 0;
        }

        internal static void ResetForTests() => ResetGeneratedSprites();

        private static void DestroySprite(ref Sprite sprite)
        {
            var current = sprite;
            sprite = null;
            DestroySprite(current);
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            if (Application.isPlaying)
            {
                Object.Destroy(sprite);
                if (texture != null) Object.Destroy(texture);
            }
            else
            {
                Object.DestroyImmediate(sprite);
                if (texture != null) Object.DestroyImmediate(texture);
            }
        }

        private static float SampleRoundedRectAlpha(
            int x,
            int y,
            float width,
            float height,
            float radius,
            float outlineWidth)
        {
            float alpha = 0f;
            for (int sampleY = 0; sampleY < CoverageSampleSteps; sampleY++)
            {
                for (int sampleX = 0; sampleX < CoverageSampleSteps; sampleX++)
                {
                    float px = x + (sampleX + 0.5f) / CoverageSampleSteps;
                    float py = y + (sampleY + 0.5f) / CoverageSampleSteps;
                    float outer = RoundedRectAlpha(px, py, width, height, radius);
                    if (outlineWidth <= 0f)
                    {
                        alpha += outer;
                        continue;
                    }

                    float inner = RoundedRectAlpha(
                        px - outlineWidth,
                        py - outlineWidth,
                        width - outlineWidth * 2f,
                        height - outlineWidth * 2f,
                        Mathf.Max(0f, radius - outlineWidth));
                    alpha += Mathf.Clamp01(outer - inner);
                }
            }

            return alpha / (CoverageSampleSteps * CoverageSampleSteps);
        }

        private static float RoundedOutlineAlpha(float x, float y, float cornerRadius)
        {
            float outer = RoundedAlpha(x, y, SpriteSize, cornerRadius);
            float inner = RoundedAlpha(
                x - OutlineWidth,
                y - OutlineWidth,
                SpriteSize - OutlineWidth * 2f,
                cornerRadius - OutlineWidth);
            return Mathf.Clamp01(outer - inner);
        }

        private static float RoundedAlpha(float x, float y, float size, float radius)
        {
            if (x < 0f || y < 0f || x > size - 1f || y > size - 1f || radius <= 0f)
            {
                return 0f;
            }

            float min = radius - 0.5f;
            float max = size - radius - 0.5f;
            float dx = x < min ? min - x : x > max ? x - max : 0f;
            float dy = y < min ? min - y : y > max ? y - max : 0f;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(radius + 0.5f - distance);
        }

        private static float RoundedRectAlpha(float x, float y, float width, float height, float radius)
        {
            if (width <= 0f || height <= 0f || x < 0f || y < 0f || x > width || y > height)
            {
                return 0f;
            }

            radius = Mathf.Clamp(radius, 0f, Mathf.Min(width, height) * 0.5f);
            if (radius <= 0f)
            {
                return 1f;
            }

            float minX = radius;
            float maxX = width - radius;
            float minY = radius;
            float maxY = height - radius;
            float dx = x < minX ? minX - x : x > maxX ? x - maxX : 0f;
            float dy = y < minY ? minY - y : y > maxY ? y - maxY : 0f;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(radius + 0.5f - distance);
        }
    }

    public static class EntryUILayout
    {
        public static RectTransform CreateRectObject(
            Transform parent,
            string name,
            Vector2 size,
            Vector2 anchoredPosition)
        {
            var rectObject = new GameObject(name);
            rectObject.transform.SetParent(parent, false);
            var rect = rectObject.AddComponent<RectTransform>();
            SetCentered(rect, size, anchoredPosition);
            return rect;
        }

        public static void SetCentered(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        public static void Stretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }
    }

}
