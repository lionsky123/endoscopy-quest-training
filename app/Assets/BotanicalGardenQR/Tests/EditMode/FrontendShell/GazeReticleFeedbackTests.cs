using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.FrontendShell
{
    public sealed class GazeReticleFeedbackTests
    {
        const string VisitorRuntimePath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab";

        [Test]
        public void ProductionReticlePassesItsSemanticConfigurationContract()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorRuntimePath);
            Assert.That(prefab, Is.Not.Null, VisitorRuntimePath);

            var reticle = prefab.GetComponentInChildren<GazeReticlePresenter>(true);
            Assert.That(reticle, Is.Not.Null);
            Assert.That(() => reticle.ValidateConfiguration(), Throws.Nothing);
        }

        [Test]
        public void Prime_PreparesButDoesNotExposeTheReticleBeforeThePrologueAllowsIt()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorRuntimePath);
            Assert.That(prefab, Is.Not.Null, VisitorRuntimePath);

            var instance = Object.Instantiate(prefab);
            var viewer = new GameObject("GazeReticleTestViewer");
            var reticle = instance.GetComponentInChildren<GazeReticlePresenter>(true);
            try
            {
                viewer.AddComponent<Camera>();
                Assert.That(reticle, Is.Not.Null);

                reticle.Prime(viewer.transform);
                Assert.That(
                    () => reticle.Prime(viewer.transform),
                    Throws.Nothing,
                    "Visitor startup primes the reticle before module configuration primes it again.");

                var serialized = new SerializedObject(reticle);
                var property = serialized.FindProperty("_reticleRoot");
                Assert.That(property, Is.Not.Null);
                var reticleRoot = property.objectReferenceValue as RectTransform;
                Assert.That(reticleRoot, Is.Not.Null);
                Assert.That(reticleRoot.gameObject.activeInHierarchy, Is.False);

                reticle.SetPresentationEnabled(true);
                Assert.That(reticleRoot.gameObject.activeInHierarchy, Is.True);

                reticle.SetPresentationEnabled(false);
                Assert.That(reticleRoot.gameObject.activeInHierarchy, Is.False);
            }
            finally
            {
                if (reticle != null) reticle.Unconfigure();
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void ActivationClear_DoesNotCancelActiveSuccessFeedback()
        {
            const float confirmationTime = 10f;
            var confirmed = new ScanFeedbackState(1, 1f, true);
            var successUntil = GazeReticlePresenter.ResolveSuccessUntil(0f, confirmed, confirmationTime);

            var clearedByActivation = new ScanFeedbackState(2, 0f, false);
            var retainedUntil = GazeReticlePresenter.ResolveSuccessUntil(
                successUntil,
                clearedByActivation,
                confirmationTime + 0.01f);

            Assert.That(retainedUntil, Is.EqualTo(successUntil));
            Assert.That(retainedUntil, Is.EqualTo(confirmationTime + 0.25f));
        }

        [Test]
        public void ClearAfterSuccessWindow_EndsSuccessFeedback()
        {
            var cleared = new ScanFeedbackState(2, 0f, false);
            var result = GazeReticlePresenter.ResolveSuccessUntil(10.25f, cleared, 10.26f);

            Assert.That(result, Is.Zero);
        }

        [Test]
        public void RepeatedGazeFocus_PreservesStandardButtonProgressFeedback()
        {
            var root = new GameObject(
                "GazeProgressButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(180f, 56f);
                var button = root.GetComponent<Button>();
                button.targetGraphic = root.GetComponent<Image>();
                CreateImageChild(root.transform, "ButtonOutline", new Vector2(180f, 56f));
                CreateImageChild(root.transform, "FocusAura", new Vector2(190f, 66f));
                var track = CreateImageChild(root.transform, "GazeProgress", new Vector2(186f, 62f));
                var fill = CreateImageChild(track.transform, "Fill", new Vector2(186f, 62f));

                EntryUIButtonVisual.Apply(button, Color.cyan);
                var spriteCountAfterSetup = EntryUIShapes.SizedRoundedSpriteCount;
                EntryUIButtonVisual.ApplyGazeFocus(button, 0.25f);
                EntryUIButtonVisual.ApplyGazeFocus(button, 0.75f);

                Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
                Assert.That(fill.fillAmount, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(track.color.a, Is.GreaterThan(0f));
                Assert.That(EntryUIShapes.SizedRoundedSpriteCount, Is.EqualTo(spriteCountAfterSetup));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static Image CreateImageChild(Transform parent, string name, Vector2 size)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            child.GetComponent<RectTransform>().sizeDelta = size;
            return child.GetComponent<Image>();
        }
    }
}
