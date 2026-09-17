using BotanicalGardenQR.FrontendShell.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.FrontendShell
{
    public sealed class EntryUIShapesTests
    {
        [SetUp]
        public void SetUp()
        {
            EntryUIShapes.ResetForTests();
            EntryUISdfIconAtlas.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            EntryUIShapes.ResetForTests();
            EntryUISdfIconAtlas.ResetForTests();
        }

        [Test]
        public void SdfIconAtlas_ResetDestroysCachedSpriteAndTexture()
        {
            var sprite = EntryUISdfIconAtlas.Get(EntryUILineIconKind.Play, 2.6f);
            var texture = sprite.texture;

            Assert.That(
                EntryUISdfIconAtlas.Get(EntryUILineIconKind.Play, 2.6f),
                Is.SameAs(sprite));
            Assert.That(EntryUISdfIconAtlas.CachedSpriteCount, Is.EqualTo(1));

            EntryUISdfIconAtlas.ResetForTests();

            Assert.That(EntryUISdfIconAtlas.CachedSpriteCount, Is.Zero);
            Assert.That(sprite == null, Is.True);
            Assert.That(texture == null, Is.True);
        }

        [Test]
        public void SameCornerParameters_ReuseNineSliceAcrossDifferentSurfaceSizes()
        {
            var first = CreateImage("LargeSurface");
            var second = CreateImage("SmallSurface");
            try
            {
                EntryUIShapes.ApplySizedRoundedFill(first, new Vector2(1080f, 608f), 14f);
                EntryUIShapes.ApplySizedRoundedFill(second, new Vector2(168f, 36f), 14f);

                Assert.That(first.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(second.type, Is.EqualTo(Image.Type.Sliced));
                Assert.That(second.sprite, Is.SameAs(first.sprite));
                Assert.That(first.sprite.texture.width, Is.LessThan(128));
                Assert.That(EntryUIShapes.SizedRoundedSpriteCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(first.gameObject);
                Object.DestroyImmediate(second.gameObject);
            }
        }

        [Test]
        public void NineSliceBorder_PreservesRequestedCornerRadiusAtDefaultCanvasScale()
        {
            var image = CreateImage("ScaledBorderSurface");
            try
            {
                const float requestedRadius = 14f;
                EntryUIShapes.ApplySizedRoundedFill(image, new Vector2(1080f, 608f), requestedRadius);

                var displayedBorder = image.sprite.border.x * 100f / image.sprite.pixelsPerUnit;
                Assert.That(displayedBorder, Is.EqualTo(requestedRadius).Within(0.34f));
            }
            finally
            {
                Object.DestroyImmediate(image.gameObject);
            }
        }

        [Test]
        public void ParameterizedCache_NeverExceedsCountOrPixelBudget()
        {
            var image = CreateImage("BudgetSurface");
            try
            {
                for (var index = 1; index <= 48; index++)
                    EntryUIShapes.ApplySizedRoundedFill(
                        image,
                        new Vector2(512f, 256f),
                        index);

                Assert.That(
                    EntryUIShapes.SizedRoundedSpriteCount,
                    Is.LessThanOrEqualTo(EntryUIShapes.MaxSizedRoundedSpriteCount));
                Assert.That(
                    EntryUIShapes.SizedRoundedTexturePixels,
                    Is.LessThanOrEqualTo(EntryUIShapes.MaxSizedRoundedTexturePixels));
                Assert.That(image.sprite, Is.Not.Null);
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            }
            finally
            {
                Object.DestroyImmediate(image.gameObject);
            }
        }

        static Image CreateImage(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            return root.GetComponent<Image>();
        }
    }
}
