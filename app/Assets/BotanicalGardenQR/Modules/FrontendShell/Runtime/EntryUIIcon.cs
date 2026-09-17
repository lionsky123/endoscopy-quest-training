using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    public enum EntryUILineIconKind
    {
        Images,
        Video,
        Model,
        ReturnToObservation,
        ChevronLeft,
        ChevronRight,
        Close,
        Play,
        Pause,
        Replay,
        Volume,
        VolumeMuted,
        Rotate,
        Reset,
        BotanicalRadialFronds,
        BotanicalBroadCanopy
    }

    internal static class EntryUIIcon
    {
        public static RectTransform Draw(
            Transform parent,
            EntryUILineIconKind kind,
            Vector2 size,
            Vector2 anchoredPosition,
            Color color,
            float stroke = 2.2f)
        {
            var icon = CreateRectObject(parent, NameFor(kind), size, anchoredPosition);
            RedrawInto(icon, kind, size, color, stroke);
            return icon;
        }

        public static RectTransform Replace(
            Transform parent,
            EntryUILineIconKind kind,
            Vector2 size,
            Vector2 anchoredPosition,
            Color color,
            float stroke = 2.2f)
        {
            if (parent == null)
            {
                return null;
            }

            Clear(parent);
            var authoredOrCached = parent.Find(NameFor(kind)) as RectTransform;
            if (authoredOrCached != null)
            {
                EntryUILayout.SetCentered(authoredOrCached, size, anchoredPosition);
                RedrawInto(authoredOrCached, kind, size, color, stroke);
                return authoredOrCached;
            }

            return Draw(parent, kind, size, anchoredPosition, color, stroke);
        }

        public static void RefreshExisting(Transform root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var icon in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (icon == null || !TryKindForName(icon.name, out var kind))
                {
                    continue;
                }

                var size = ResolveIconSize(icon);
                RedrawInto(icon, kind, size, ResolveIconColor(icon), ResolveIconStroke(size));
            }
        }

        public static void Clear(Transform parent)
        {
            ClearLineIcons(parent);
        }

        private static void RedrawInto(RectTransform icon, EntryUILineIconKind kind, Vector2 size, Color color, float stroke)
        {
            if (icon == null)
            {
                return;
            }

            icon.localRotation = Quaternion.identity;
            icon.gameObject.SetActive(true);
            ClearChildren(icon);

            var image = icon.GetComponent<Image>();
            if (image == null)
            {
                image = icon.gameObject.AddComponent<Image>();
            }

            image.sprite = EntryUISdfIconAtlas.Get(kind, ResolveIconStroke(size, stroke));
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = color;
            image.raycastTarget = false;
        }

        private static RectTransform CreateRectObject(Transform parent, string name, Vector2 size, Vector2 anchoredPosition)
            => EntryUILayout.CreateRectObject(parent, name, size, anchoredPosition);

        private static void ClearLineIcons(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (!child.name.StartsWith("LineIcon_", StringComparison.Ordinal))
                {
                    continue;
                }

                child.gameObject.SetActive(false);
            }
        }

        private static void ClearChildren(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static Vector2 ResolveIconSize(RectTransform icon)
        {
            var size = icon.rect.size;
            if (Mathf.Abs(size.x) <= 1f || Mathf.Abs(size.y) <= 1f)
            {
                size = icon.sizeDelta;
            }

            return new Vector2(Mathf.Max(1f, Mathf.Abs(size.x)), Mathf.Max(1f, Mathf.Abs(size.y)));
        }

        private static float ResolveIconStroke(Vector2 size, float requestedStroke = 2.2f)
        {
            float min = Mathf.Min(size.x, size.y);
            float resolved = Mathf.Clamp(1.35f + min / 32f, 2.45f, 3.25f);
            return Mathf.Clamp(Mathf.Max(resolved, requestedStroke), 2.45f, 3.25f);
        }

        private static Color ResolveIconColor(RectTransform icon)
        {
            Color rootColor = Color.white;
            var hasRootColor = false;
            foreach (var graphic in icon.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null || graphic.color.a <= 0.01f)
                {
                    continue;
                }

                var color = BoostIconColor(graphic.color);
                if (graphic.transform == icon)
                {
                    rootColor = color;
                    hasRootColor = true;
                    continue;
                }

                return color;
            }

            return hasRootColor ? rootColor : Color.white;
        }

        private static Color BoostIconColor(Color color)
            => new Color(
                Mathf.Lerp(color.r, 1f, 0.24f),
                Mathf.Lerp(color.g, 1f, 0.24f),
                Mathf.Lerp(color.b, 1f, 0.24f),
                Mathf.Max(color.a, 0.94f));

        private static string NameFor(EntryUILineIconKind kind)
        {
            switch (kind)
            {
                case EntryUILineIconKind.Images:
                    return "LineIcon_Images";
                case EntryUILineIconKind.Video:
                    return "LineIcon_Video";
                case EntryUILineIconKind.Model:
                    return "LineIcon_Model";
                case EntryUILineIconKind.ReturnToObservation:
                    return "LineIcon_ReturnToObservation";
                case EntryUILineIconKind.ChevronLeft:
                    return "LineIcon_ChevronLeft";
                case EntryUILineIconKind.ChevronRight:
                    return "LineIcon_ChevronRight";
                case EntryUILineIconKind.Close:
                    return "LineIcon_Close";
                case EntryUILineIconKind.Play:
                    return "LineIcon_Play";
                case EntryUILineIconKind.Pause:
                    return "LineIcon_Pause";
                case EntryUILineIconKind.Replay:
                    return "LineIcon_Replay";
                case EntryUILineIconKind.Volume:
                    return "LineIcon_Volume";
                case EntryUILineIconKind.VolumeMuted:
                    return "LineIcon_VolumeMuted";
                case EntryUILineIconKind.Rotate:
                    return "LineIcon_Rotate";
                case EntryUILineIconKind.Reset:
                    return "LineIcon_Reset";
                case EntryUILineIconKind.BotanicalRadialFronds:
                    return "LineIcon_BotanicalRadialFronds";
                case EntryUILineIconKind.BotanicalBroadCanopy:
                    return "LineIcon_BotanicalBroadCanopy";
                default:
                    return "LineIcon";
            }
        }

        private static bool TryKindForName(string name, out EntryUILineIconKind kind)
        {
            switch (name)
            {
                case "LineIcon_Images":
                    kind = EntryUILineIconKind.Images;
                    return true;
                case "LineIcon_Video":
                    kind = EntryUILineIconKind.Video;
                    return true;
                case "LineIcon_Model":
                    kind = EntryUILineIconKind.Model;
                    return true;
                case "LineIcon_ReturnToObservation":
                    kind = EntryUILineIconKind.ReturnToObservation;
                    return true;
                case "LineIcon_ChevronLeft":
                    kind = EntryUILineIconKind.ChevronLeft;
                    return true;
                case "LineIcon_ChevronRight":
                    kind = EntryUILineIconKind.ChevronRight;
                    return true;
                case "LineIcon_Close":
                    kind = EntryUILineIconKind.Close;
                    return true;
                case "LineIcon_Play":
                    kind = EntryUILineIconKind.Play;
                    return true;
                case "LineIcon_Pause":
                    kind = EntryUILineIconKind.Pause;
                    return true;
                case "LineIcon_Replay":
                    kind = EntryUILineIconKind.Replay;
                    return true;
                case "LineIcon_Volume":
                    kind = EntryUILineIconKind.Volume;
                    return true;
                case "LineIcon_VolumeMuted":
                    kind = EntryUILineIconKind.VolumeMuted;
                    return true;
                case "LineIcon_Rotate":
                    kind = EntryUILineIconKind.Rotate;
                    return true;
                case "LineIcon_Reset":
                    kind = EntryUILineIconKind.Reset;
                    return true;
                case "LineIcon_BotanicalRadialFronds":
                    kind = EntryUILineIconKind.BotanicalRadialFronds;
                    return true;
                case "LineIcon_BotanicalBroadCanopy":
                    kind = EntryUILineIconKind.BotanicalBroadCanopy;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }
    }

    internal static class EntryUISdfIconAtlas
    {
        const int TextureSize = 256;
        const float IconUnits = 24f;
        static readonly Dictionary<IconKey, Sprite> Sprites = new Dictionary<IconKey, Sprite>();

        public static Sprite Get(EntryUILineIconKind kind, float stroke)
        {
            var key = new IconKey(kind, Mathf.RoundToInt(Mathf.Clamp(stroke, 2.45f, 3.25f) * 10f));
            if (Sprites.TryGetValue(key, out var sprite))
            {
                return sprite;
            }

            var raster = new IconRaster(TextureSize, IconUnits);
            DrawDesign(raster, kind, key.StrokeTenth / 10f);
            sprite = raster.CreateSprite("EntrySdfIcon_" + kind + "_" + key.StrokeTenth);
            Sprites[key] = sprite;
            return sprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAtlasCache()
        {
            foreach (var sprite in Sprites.Values)
                DestroySprite(sprite);
            Sprites.Clear();
        }

        internal static int CachedSpriteCount => Sprites.Count;

        internal static void ResetForTests() => ResetAtlasCache();

        static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(sprite);
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(sprite);
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static void DrawDesign(IconRaster raster, EntryUILineIconKind kind, float stroke)
        {
            switch (kind)
            {
                case EntryUILineIconKind.Images:
                    raster.AddRect(new Vector2(-1.5f, 1.5f), new Vector2(14f, 11f), stroke, 0.55f);
                    raster.AddRect(new Vector2(1.5f, -1.5f), new Vector2(15f, 12f), stroke, 1f);
                    raster.AddLine(new Vector2(-5f, -4f), new Vector2(-1f, -1f), stroke, 1f);
                    raster.AddLine(new Vector2(-1f, -1f), new Vector2(3f, -4f), stroke, 1f);
                    raster.AddCircle(new Vector2(4.5f, 1.5f), 2f, stroke, 1f);
                    break;
                case EntryUILineIconKind.Video:
                    raster.AddRect(Vector2.zero, new Vector2(16f, 12f), stroke, 1f);
                    raster.AddLine(new Vector2(-5f, 3.2f), new Vector2(5f, 3.2f), stroke * 0.72f, 0.56f);
                    raster.AddLine(new Vector2(-5f, -3.2f), new Vector2(5f, -3.2f), stroke * 0.72f, 0.42f);
                    raster.AddFilledPolygon(new[] { new Vector2(-2f, -4.2f), new Vector2(5.2f, 0f), new Vector2(-2f, 4.2f) }, 1f);
                    break;
                case EntryUILineIconKind.Model:
                    raster.AddRect(new Vector2(-2f, -2f), new Vector2(10f, 10f), stroke, 1f);
                    raster.AddRect(new Vector2(3f, 3f), new Vector2(10f, 10f), stroke, 0.72f);
                    raster.AddLine(new Vector2(3f, 8f), new Vector2(8f, 13f), stroke, 1f);
                    raster.AddLine(new Vector2(-7f, 8f), new Vector2(-2f, 13f), stroke, 1f);
                    raster.AddLine(new Vector2(3f, -2f), new Vector2(8f, 3f), stroke, 1f);
                    break;
                case EntryUILineIconKind.ReturnToObservation:
                    raster.AddCircle(Vector2.zero, 6.2f, stroke, 1f);
                    raster.AddFilledCircle(Vector2.zero, 1.45f, 1f);
                    raster.AddLine(new Vector2(-8f, 8f), new Vector2(-3f, 8f), stroke, 1f);
                    raster.AddLine(new Vector2(-8f, 8f), new Vector2(-8f, 3f), stroke, 1f);
                    raster.AddLine(new Vector2(8f, -8f), new Vector2(3f, -8f), stroke, 1f);
                    raster.AddLine(new Vector2(8f, -8f), new Vector2(8f, -3f), stroke, 1f);
                    break;
                case EntryUILineIconKind.ChevronLeft:
                    raster.AddLine(new Vector2(4f, 8f), new Vector2(-4f, 0f), stroke * 1.25f, 1f);
                    raster.AddLine(new Vector2(-4f, 0f), new Vector2(4f, -8f), stroke * 1.25f, 1f);
                    break;
                case EntryUILineIconKind.ChevronRight:
                    raster.AddLine(new Vector2(-4f, 8f), new Vector2(4f, 0f), stroke * 1.25f, 1f);
                    raster.AddLine(new Vector2(4f, 0f), new Vector2(-4f, -8f), stroke * 1.25f, 1f);
                    break;
                case EntryUILineIconKind.Close:
                    raster.AddLine(new Vector2(-6f, 6f), new Vector2(6f, -6f), stroke * 1.2f, 1f);
                    raster.AddLine(new Vector2(-6f, -6f), new Vector2(6f, 6f), stroke * 1.2f, 1f);
                    break;
                case EntryUILineIconKind.Play:
                    raster.AddFilledPolygon(new[] { new Vector2(-4f, -7.5f), new Vector2(7.5f, 0f), new Vector2(-4f, 7.5f) }, 1f);
                    break;
                case EntryUILineIconKind.Pause:
                    raster.AddLine(new Vector2(-4f, -7f), new Vector2(-4f, 7f), stroke * 1.55f, 1f);
                    raster.AddLine(new Vector2(4f, -7f), new Vector2(4f, 7f), stroke * 1.55f, 1f);
                    break;
                case EntryUILineIconKind.Replay:
                    AddReplay(raster, stroke, 1f);
                    break;
                case EntryUILineIconKind.Volume:
                    raster.AddPolyline(new[] { new Vector2(-9f, -4f), new Vector2(-5f, -4f), new Vector2(0f, -8f), new Vector2(0f, 8f), new Vector2(-5f, 4f), new Vector2(-9f, 4f), new Vector2(-9f, -4f) }, stroke, 1f);
                    raster.AddArc(new Vector2(1.5f, 0f), 6f, -42f, 42f, 8, stroke, 0.86f);
                    raster.AddArc(new Vector2(1.5f, 0f), 10f, -45f, 45f, 10, stroke, 0.62f);
                    break;
                case EntryUILineIconKind.VolumeMuted:
                    raster.AddPolyline(new[] { new Vector2(-9f, -4f), new Vector2(-5f, -4f), new Vector2(0f, -8f), new Vector2(0f, 8f), new Vector2(-5f, 4f), new Vector2(-9f, 4f), new Vector2(-9f, -4f) }, stroke, 1f);
                    raster.AddLine(new Vector2(5.5f, -6.5f), new Vector2(11f, 6.5f), stroke * 1.15f, 1f);
                    raster.AddLine(new Vector2(10.5f, -5.5f), new Vector2(5.5f, 5.5f), stroke * 1.15f, 1f);
                    break;
                case EntryUILineIconKind.Rotate:
                    AddReplay(raster, stroke, 1f);
                    raster.AddLine(new Vector2(-5f, 8f), new Vector2(5f, -8f), stroke * 0.8f, 0.55f);
                    break;
                case EntryUILineIconKind.Reset:
                    AddReplay(raster, stroke, 1f);
                    raster.AddLine(new Vector2(-6f, -7f), new Vector2(6f, -7f), stroke, 0.72f);
                    break;
                case EntryUILineIconKind.BotanicalRadialFronds:
                    raster.AddLine(new Vector2(0f, -9f), new Vector2(0f, 3f), stroke * 1.15f, 1f);
                    raster.AddLine(new Vector2(0f, 2f), new Vector2(-8f, 8f), stroke, 1f);
                    raster.AddLine(new Vector2(0f, 1f), new Vector2(-10f, 2f), stroke, 1f);
                    raster.AddLine(new Vector2(0f, -1f), new Vector2(-7f, -5f), stroke, 1f);
                    raster.AddLine(new Vector2(0f, 2f), new Vector2(8f, 8f), stroke, 1f);
                    raster.AddLine(new Vector2(0f, 1f), new Vector2(10f, 2f), stroke, 1f);
                    raster.AddLine(new Vector2(0f, -1f), new Vector2(7f, -5f), stroke, 1f);
                    raster.AddFilledCircle(new Vector2(0f, 2f), 1.7f, 1f);
                    break;
                case EntryUILineIconKind.BotanicalBroadCanopy:
                    raster.AddLine(new Vector2(0f, -10f), new Vector2(0f, -2f), stroke * 1.45f, 1f);
                    raster.AddLine(new Vector2(0f, -3f), new Vector2(-5f, 1f), stroke, 1f);
                    raster.AddLine(new Vector2(0f, -3f), new Vector2(5f, 1f), stroke, 1f);
                    raster.AddArc(new Vector2(0f, 1f), 9f, 18f, 162f, 14, stroke, 1f);
                    raster.AddArc(new Vector2(0f, 4f), 8f, 205f, 335f, 12, stroke, 0.82f);
                    break;
            }
        }

        static void AddReplay(IconRaster raster, float stroke, float opacity)
        {
            raster.AddArc(Vector2.zero, 8f, 118f, 382f, 22, stroke, opacity);
            var tip = PointOnArc(Vector2.zero, 8f, 118f);
            raster.AddLine(tip, tip + new Vector2(4.2f, 0.2f), stroke, opacity);
            raster.AddLine(tip, tip + new Vector2(1.1f, -4f), stroke, opacity);
        }

        static Vector2 PointOnArc(Vector2 center, float radius, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius);
        }

        readonly struct IconKey : IEquatable<IconKey>
        {
            public IconKey(EntryUILineIconKind kind, int strokeTenth)
            {
                Kind = kind;
                StrokeTenth = strokeTenth;
            }

            public EntryUILineIconKind Kind { get; }
            public int StrokeTenth { get; }

            public bool Equals(IconKey other) => Kind == other.Kind && StrokeTenth == other.StrokeTenth;
            public override bool Equals(object obj) => obj is IconKey other && Equals(other);
            public override int GetHashCode() => ((int)Kind * 397) ^ StrokeTenth;
        }

        sealed class IconRaster
        {
            readonly int _size;
            readonly float _units;
            readonly float _pixelUnits;
            readonly float[] _alpha;

            public IconRaster(int size, float units)
            {
                _size = size;
                _units = units;
                _pixelUnits = units / size;
                _alpha = new float[size * size];
            }

            public Sprite CreateSprite(string name)
            {
                var texture = new Texture2D(_size, _size, TextureFormat.RGBA32, false)
                {
                    name = name + "Texture",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave
                };

                var pixels = new Color32[_alpha.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(_alpha[i]) * 255f));
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, _size, _size),
                    new Vector2(0.5f, 0.5f),
                    _size / _units,
                    0,
                    SpriteMeshType.FullRect);
                sprite.name = name;
                sprite.hideFlags = HideFlags.DontSave;
                return sprite;
            }

            public void AddRect(Vector2 center, Vector2 size, float stroke, float opacity)
            {
                var half = size * 0.5f;
                AddLine(center + new Vector2(-half.x, half.y), center + new Vector2(half.x, half.y), stroke, opacity);
                AddLine(center + new Vector2(half.x, half.y), center + new Vector2(half.x, -half.y), stroke, opacity);
                AddLine(center + new Vector2(half.x, -half.y), center + new Vector2(-half.x, -half.y), stroke, opacity);
                AddLine(center + new Vector2(-half.x, -half.y), center + new Vector2(-half.x, half.y), stroke, opacity);
            }

            public void AddPolyline(Vector2[] points, float stroke, float opacity)
            {
                if (points == null) return;
                for (int i = 0; i < points.Length - 1; i++)
                {
                    AddLine(points[i], points[i + 1], stroke, opacity);
                }
            }

            public void AddArc(
                Vector2 center,
                float radius,
                float startDegrees,
                float endDegrees,
                int segments,
                float stroke,
                float opacity)
            {
                int segmentCount = Mathf.Max(1, segments);
                var previous = PointOnArc(center, radius, startDegrees);
                for (int i = 1; i <= segmentCount; i++)
                {
                    float t = i / (float)segmentCount;
                    float degrees = Mathf.Lerp(startDegrees, endDegrees, t);
                    var current = PointOnArc(center, radius, degrees);
                    AddLine(previous, current, stroke, opacity);
                    previous = current;
                }
            }

            public void AddLine(Vector2 a, Vector2 b, float stroke, float opacity)
            {
                var ab = b - a;
                float lengthSquared = Mathf.Max(0.0001f, Vector2.Dot(ab, ab));
                float halfStroke = Mathf.Max(_pixelUnits, stroke * 0.5f);
                RasterizeDistance(opacity, p =>
                {
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared);
                    var closest = a + ab * t;
                    return Vector2.Distance(p, closest) - halfStroke;
                });
            }

            public void AddCircle(Vector2 center, float radius, float stroke, float opacity)
            {
                float halfStroke = Mathf.Max(_pixelUnits, stroke * 0.5f);
                RasterizeDistance(opacity, p => Mathf.Abs(Vector2.Distance(p, center) - radius) - halfStroke);
            }

            public void AddFilledCircle(Vector2 center, float radius, float opacity)
            {
                RasterizeDistance(opacity, p => Vector2.Distance(p, center) - radius);
            }

            public void AddFilledPolygon(Vector2[] points, float opacity)
            {
                if (points == null || points.Length < 3) return;
                RasterizeDistance(opacity, p =>
                {
                    float distance = DistanceToPolygon(points, p);
                    return IsInsidePolygon(points, p) ? -distance : distance;
                });
            }

            void RasterizeDistance(float opacity, Func<Vector2, float> signedDistance)
            {
                float softness = _pixelUnits * 1.35f;
                for (int y = 0; y < _size; y++)
                {
                    for (int x = 0; x < _size; x++)
                    {
                        var point = PixelToIconPoint(x, y);
                        float distance = signedDistance(point);
                        float t = Mathf.Clamp01((softness - distance) / (softness * 2f));
                        float coverage = Mathf.SmoothStep(0f, 1f, t) * Mathf.Clamp01(opacity);
                        if (coverage <= 0f) continue;
                        int index = y * _size + x;
                        _alpha[index] = 1f - (1f - _alpha[index]) * (1f - coverage);
                    }
                }
            }

            Vector2 PixelToIconPoint(int x, int y)
                => new Vector2(
                    ((x + 0.5f) / _size - 0.5f) * _units,
                    ((y + 0.5f) / _size - 0.5f) * _units);

            static float DistanceToPolygon(Vector2[] points, Vector2 point)
            {
                float distance = float.PositiveInfinity;
                for (int i = 0; i < points.Length; i++)
                {
                    var a = points[i];
                    var b = points[(i + 1) % points.Length];
                    distance = Mathf.Min(distance, DistanceToSegment(a, b, point));
                }

                return distance;
            }

            static float DistanceToSegment(Vector2 a, Vector2 b, Vector2 point)
            {
                var ab = b - a;
                float lengthSquared = Mathf.Max(0.0001f, Vector2.Dot(ab, ab));
                float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
                return Vector2.Distance(point, a + ab * t);
            }

            static bool IsInsidePolygon(Vector2[] points, Vector2 point)
            {
                var inside = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    bool crosses = (points[i].y > point.y) != (points[j].y > point.y);
                    if (!crosses) continue;
                    float x = (points[j].x - points[i].x) * (point.y - points[i].y) /
                              Mathf.Max(0.0001f, points[j].y - points[i].y) + points[i].x;
                    if (point.x < x) inside = !inside;
                }

                return inside;
            }
        }
    }
}
