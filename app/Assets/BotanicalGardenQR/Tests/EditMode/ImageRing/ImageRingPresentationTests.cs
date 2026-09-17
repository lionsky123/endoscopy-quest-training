using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.ImageRing.Contracts;
using BotanicalGardenQR.ImageRing.Frontend;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.ImageRing
{
    public sealed class ImageRingPresentationTests
    {
        const string SharedFontAssetPath =
            "Assets/BotanicalGardenQR/Resources/BotanicalGardenQR/Fonts/SourceHanSansSC-Regular SDF.asset";

        readonly List<Texture2D> _textures = new List<Texture2D>();
        readonly List<AudioClip> _audioClips = new List<AudioClip>();
        GameObject _runtimeRoot;
        GameObject _viewer;
        IImageRingRuntime _runtime;
        FakeGazeSurfaceRegistry _gazeSurfaces;

        [SetUp]
        public void SetUp()
        {
            _runtimeRoot = new GameObject("TestRuntimeRoot");
            _viewer = new GameObject("TestViewer");
            _viewer.transform.rotation = Quaternion.identity;
            _gazeSurfaces = new FakeGazeSurfaceRegistry();
            _runtime = ImageRingFrontend.Create(
                _runtimeRoot.transform,
                _viewer.transform,
                _gazeSurfaces,
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SharedFontAssetPath));
        }

        [TearDown]
        public void TearDown()
        {
            _runtime?.Dispose();
            _runtime = null;
            if (_runtimeRoot != null) UnityEngine.Object.DestroyImmediate(_runtimeRoot);
            if (_viewer != null) UnityEngine.Object.DestroyImmediate(_viewer);
            foreach (var texture in _textures)
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            _textures.Clear();
            foreach (var clip in _audioClips)
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
            _audioClips.Clear();
        }

        [Test]
        public void OpenKeepsAnEvenFullRingAndRevealsOneCardPerTick()
        {
            const int itemCount = 8;
            _runtime.Open(
                SessionToken.CreateNew(),
                Definition(itemCount),
                _ => { },
                () => { });

            var world = Find("ImageRingSpatialWorld");
            var cards = CardCanvases(world);
            Assert.That(cards, Has.Count.EqualTo(itemCount));
            Assert.That(cards.Count(card => card.gameObject.activeSelf), Is.EqualTo(1));
            Assert.That(
                cards.SelectMany(card => card.GetComponentsInChildren<RawImage>(true))
                    .Count(image => image.texture != null),
                Is.EqualTo(1));

            var radius = HorizontalRadius(cards[0].transform.localPosition);
            Assert.That(radius, Is.InRange(1.41f, 1.59f));
            var cardType = typeof(ImageRingFrontend).Assembly.GetType(
                "BotanicalGardenQR.ImageRing.Frontend.ImageRingSpatialCard");
            var focusScale = cardType?.GetField(
                "FocusScale",
                BindingFlags.Static | BindingFlags.NonPublic)?.GetRawConstantValue();
            Assert.That(focusScale, Is.TypeOf<float>());
            Assert.That((float)focusScale, Is.GreaterThanOrEqualTo(1.6f));
            Assert.That(cards[0].transform.localPosition.z, Is.GreaterThan(0f));
            var expectedNeighbourDot = Mathf.Cos(Mathf.PI * 2f / itemCount);
            for (var index = 0; index < cards.Count; index++)
            {
                var current = HorizontalDirection(cards[index].transform.localPosition);
                var next = HorizontalDirection(cards[(index + 1) % cards.Count].transform.localPosition);
                Assert.That(HorizontalRadius(cards[index].transform.localPosition), Is.EqualTo(radius).Within(0.0001f));
                Assert.That(Vector3.Dot(current, next), Is.EqualTo(expectedNeighbourDot).Within(0.0001f));
                Assert.That(Vector3.Dot(cards[index].transform.forward, current), Is.GreaterThan(0.999f));
            }

            Tick(world, itemCount - 1);

            Assert.That(cards.Count(card => card.gameObject.activeSelf), Is.EqualTo(itemCount));
            var images = cards.SelectMany(card => card.GetComponentsInChildren<RawImage>(true)).ToArray();
            Assert.That(images, Has.Length.EqualTo(itemCount));
            Assert.That(images.All(image => image.texture != null), Is.True);
            Assert.That(_gazeSurfaces.Registrations, Has.Count.EqualTo(itemCount + 1));
        }

        [Test]
        public void CardUsesExternalLinearProgressWithoutSharedFocusHalo()
        {
            _runtime.Open(
                SessionToken.CreateNew(),
                Definition(3),
                _ => { },
                () => { });

            var card = CardCanvases(Find("ImageRingSpatialWorld"))[0];
            var button = card.GetComponentInChildren<Button>(true);
            var image = card.GetComponentInChildren<RawImage>(true);
            var track = button.transform.Find("DwellProgressTrack") as RectTransform;
            Assert.That(track, Is.Not.Null);
            Assert.That(
                card.GetComponentsInChildren<Transform>(true).Any(value => value.name == "GazeProgress"),
                Is.False);
            Assert.That(
                track.anchoredPosition.y,
                Is.LessThan(ImageBottom(image.rectTransform)));

            var hitSurface = button.GetComponent<Image>();
            var initialHitColor = hitSurface.color;
            var setProgress = typeof(HeadGazeDwellController).GetMethod(
                "SetButtonProgress",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(setProgress, Is.Not.Null);
            setProgress.Invoke(null, new object[] { button, 0.5f });

            var fill = track.Find("Fill").GetComponent<Image>();
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Horizontal));
            Assert.That(fill.fillAmount, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(hitSurface.color, Is.EqualTo(initialHitColor));
            Assert.That(button.GetComponent<IFrontendGazeProgressPresenter>(), Is.Not.Null);
        }

        [Test]
        public void FocusHidesChromeAndKeepsPlaybackHitOnTheExpandedImage()
        {
            var playback = new List<ImageRingItemPlaybackIntent>();
            _runtime.Open(
                SessionToken.CreateNew(),
                Definition(3),
                playback.Add,
                () => { });

            var world = Find("ImageRingSpatialWorld");
            var card = CardCanvases(world)[0];
            var panel = FindIn(card.transform, "CardSurface").GetComponent<Image>();
            var imageWell = FindIn(card.transform, "ImageWell").GetComponent<Image>();
            var mainImage = card.GetComponentInChildren<RawImage>(true);
            var title = FindIn(card.transform, "Title");
            var description = FindIn(card.transform, "Description");
            var indexLabel = FindIn(card.transform, "ItemIndex");
            var frame = FindIn(card.transform, "ImageFocusFrame").GetComponent<Graphic>();
            var halo = FindIn(card.transform, "ImageFocusHalo").GetComponent<Graphic>();
            var expandedHit = FindIn(card.transform, "ExpandedImageHitSurface").GetComponent<Image>();
            var button = card.GetComponentInChildren<Button>(true);

            Assert.That(panel.color.b, Is.GreaterThanOrEqualTo(panel.color.g));
            Assert.That(Mathf.Max(panel.color.r, panel.color.g, panel.color.b) -
                        Mathf.Min(panel.color.r, panel.color.g, panel.color.b), Is.LessThan(0.02f));
            Assert.That(imageWell.color.b, Is.GreaterThanOrEqualTo(imageWell.color.g));
            Assert.That(mainImage.rectTransform.pivot.y, Is.Zero);
            Assert.That(frame.color.g, Is.GreaterThan(frame.color.r));
            Assert.That(frame.raycastTarget, Is.False);
            Assert.That(halo.raycastTarget, Is.False);
            Assert.That(expandedHit.gameObject.activeSelf, Is.False);
            var idleFrameAlpha = frame.color.a;

            var cardState = FirstSpatialCard(world);
            var focusBlend = cardState.GetType().GetField(
                "_focusBlend",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(focusBlend, Is.Not.Null);
            focusBlend.SetValue(cardState, 1f);
            _gazeSurfaces.Registrations.Single(value => value.Label == "image-ring-card-1").IsFocused = true;
            cardState.GetType().GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(cardState, null);

            Assert.That(mainImage.rectTransform.localScale.x, Is.EqualTo(1.65f).Within(0.001f));
            Assert.That(panel.rectTransform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(title.localScale, Is.EqualTo(Vector3.one));
            Assert.That(description.localScale, Is.EqualTo(Vector3.one));
            Assert.That(indexLabel.localScale, Is.EqualTo(Vector3.one));
            Assert.That(panel.color.a, Is.Zero.Within(0.001f));
            Assert.That(imageWell.color.a, Is.Zero.Within(0.001f));
            Assert.That(title.GetComponent<TextMeshProUGUI>().color.a, Is.Zero.Within(0.001f));
            Assert.That(description.GetComponent<TextMeshProUGUI>().color.a, Is.Zero.Within(0.001f));
            Assert.That(indexLabel.GetComponent<TextMeshProUGUI>().color.a, Is.Zero.Within(0.001f));
            Assert.That(frame.color.a, Is.GreaterThan(idleFrameAlpha));
            Assert.That(frame.rectTransform.localScale.x, Is.EqualTo(1.65f).Within(0.001f));
            Assert.That(halo.rectTransform.localScale.x, Is.EqualTo(1.65f).Within(0.001f));
            Assert.That(expandedHit.gameObject.activeSelf, Is.True);
            Assert.That(expandedHit.raycastTarget, Is.True);
            Assert.That(expandedHit.GetComponentInParent<Button>(), Is.SameAs(button));
            Assert.That(
                expandedHit.rectTransform.rect.width * expandedHit.rectTransform.localScale.x,
                Is.GreaterThan(mainImage.rectTransform.rect.width * mainImage.rectTransform.localScale.x));

            var focusedFrameAlpha = frame.color.a;
            button.onClick.Invoke();
            cardState.GetType().GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public)
                ?.Invoke(cardState, null);
            Assert.That(playback, Has.Count.EqualTo(1));
            Assert.That(playback[0].Kind, Is.EqualTo(ImageRingItemPlaybackIntentKind.Play));
            Assert.That(frame.color.a, Is.GreaterThan(focusedFrameAlpha));
            Assert.That(halo.rectTransform.localScale.x, Is.GreaterThan(mainImage.rectTransform.localScale.x));
        }

        [Test]
        public void CardAndCloseButtonsKeepPlaybackAndReturnIntents()
        {
            var playback = new List<ImageRingItemPlaybackIntent>();
            var closeRequests = 0;
            _runtime.Open(
                SessionToken.CreateNew(),
                Definition(3),
                playback.Add,
                () => closeRequests++);

            var world = Find("ImageRingSpatialWorld");
            var firstCard = CardCanvases(world)[0];
            firstCard.GetComponentInChildren<Button>(true).onClick.Invoke();
            Assert.That(playback, Has.Count.EqualTo(1));
            Assert.That(playback[0].ItemIndex, Is.Zero);
            Assert.That(playback[0].Kind, Is.EqualTo(ImageRingItemPlaybackIntentKind.Play));

            Find("CloseButton").GetComponent<Button>().onClick.Invoke();
            Assert.That(closeRequests, Is.EqualTo(1));

            _runtime.Close();
            Assert.That(_gazeSurfaces.Registrations.All(value => value.Disposed), Is.True);
        }

        ImageRingDefinition Definition(int count)
        {
            var items = new ImageRingItemDefinition[count];
            for (var index = 0; index < count; index++)
            {
                var texture = new Texture2D(
                    index % 2 == 0 ? 400 + index * 10 : 260 + index * 5,
                    index % 2 == 0 ? 240 + index * 5 : 420 + index * 8);
                var audio = AudioClip.Create($"Audio {index}", 32, 1, 24000, false);
                _textures.Add(texture);
                _audioClips.Add(audio);
                items[index] = new ImageRingItemDefinition(
                    texture,
                    audio,
                    $"Title {index}",
                    $"Description {index}");
            }
            return new ImageRingDefinition(items);
        }

        GameObject Find(string name)
            => _runtimeRoot.GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == name)
                .gameObject;

        static Transform FindIn(Transform root, string name)
            => root.GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == name);

        static List<Canvas> CardCanvases(GameObject world)
            => world.GetComponentsInChildren<Canvas>(true)
                .Where(value => value.name.StartsWith("ImageRingCard_", StringComparison.Ordinal))
                .OrderBy(value => value.name, StringComparer.Ordinal)
                .ToList();

        static void Tick(GameObject world, int count)
        {
            var spatialWorld = SpatialWorld(world);
            var tick = spatialWorld.GetType().GetMethod("Tick", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(tick, Is.Not.Null);
            for (var index = 0; index < count; index++)
                tick.Invoke(spatialWorld, null);
        }

        static object FirstSpatialCard(GameObject world)
        {
            var spatialWorld = SpatialWorld(world);
            var cards = spatialWorld.GetType().GetField(
                "_cards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(cards, Is.Not.Null);
            return ((IList)cards.GetValue(spatialWorld))[0];
        }

        static object SpatialWorld(GameObject world)
        {
            var ticker = world.GetComponents<MonoBehaviour>()
                .Single(value => value.GetType().Name == "ImageRingSpatialWorldTicker");
            var worldField = ticker.GetType().GetField(
                "_world",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(worldField, Is.Not.Null);
            return worldField.GetValue(ticker);
        }

        static float ImageBottom(RectTransform image)
            => image.anchoredPosition.y - image.sizeDelta.y * image.pivot.y;

        static float HorizontalRadius(Vector3 value)
            => new Vector2(value.x, value.z).magnitude;

        static Vector3 HorizontalDirection(Vector3 value)
            => new Vector3(value.x, 0f, value.z).normalized;

        sealed class FakeGazeSurfaceRegistry : IFrontendGazeSurfaceRegistry
        {
            public System.IDisposable SuspendPanelInput() => new Suspension();
            sealed class Suspension : System.IDisposable { public void Dispose() { } }
            public List<FakeRegistration> Registrations { get; } = new List<FakeRegistration>();

            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(
                Transform surfaceRoot,
                int priority,
                string label)
            {
                var registration = new FakeRegistration(surfaceRoot, priority, label);
                Registrations.Add(registration);
                return registration;
            }
        }

        sealed class FakeRegistration : IFrontendGazeSurfaceRegistration
        {
            public void Invalidate() { }

            public FakeRegistration(Transform surfaceRoot, int priority, string label)
            {
                SurfaceRoot = surfaceRoot;
                Priority = priority;
                Label = label;
            }

            public Transform SurfaceRoot { get; }
            public int Priority { get; }
            public string Label { get; }
            public bool IsFocused { get; set; }
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }
    }
}
