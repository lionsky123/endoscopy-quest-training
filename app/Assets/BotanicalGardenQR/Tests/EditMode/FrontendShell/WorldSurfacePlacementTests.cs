using BotanicalGardenQR.FrontendShell.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.FrontendShell
{
    public sealed class WorldSurfacePlacementTests
    {
        [Test]
        public void ViewerFrontPoseFacesCanvasBackTowardViewerWithoutMirroring()
        {
            var viewerObject = new GameObject("Viewer");
            try
            {
                viewerObject.transform.SetPositionAndRotation(
                    new Vector3(0.4f, 1.6f, -0.2f),
                    Quaternion.Euler(12f, 31f, 0f));

                var pose = WorldSurfacePlacement.CreateViewerFrontPose(
                    viewerObject.transform,
                    1.2f,
                    -0.15f);
                var towardViewer = viewerObject.transform.position - pose.position;
                towardViewer.y = 0f;
                towardViewer.Normalize();
                var canvasFront = pose.rotation * Vector3.back;

                Assert.That(Vector3.Dot(canvasFront, towardViewer), Is.GreaterThan(0.999f));
                Assert.That(pose.position.y, Is.EqualTo(1.45f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(viewerObject);
            }
        }

        [Test]
        public void PositiveScaleCheckRejectsAnyMirroredAncestor()
        {
            var parent = new GameObject("Parent");
            var surface = new GameObject("Surface");
            try
            {
                surface.transform.SetParent(parent.transform, false);
                Assert.That(WorldSurfacePlacement.HasPositiveScaleChain(surface.transform), Is.True);

                parent.transform.localScale = new Vector3(-1f, 1f, 1f);
                Assert.That(WorldSurfacePlacement.HasPositiveScaleChain(surface.transform), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void ViewerFrontAtMinimumHeight_PreservesTheHigherStandingViewerDerivedHeight()
        {
            var viewer = new GameObject("StandingViewer");
            var surface = new GameObject("StandingSurface");
            try
            {
                viewer.transform.SetPositionAndRotation(
                    new Vector3(0.2f, 1.72f, -0.4f),
                    Quaternion.Euler(0f, 18f, 0f));

                WorldSurfacePlacement.PlaceViewerFrontAtMinimumHeight(
                    surface.transform,
                    viewer.transform,
                    distance: 1.14f,
                    verticalOffset: -0.18f,
                    minimumWorldHeight: 1.35f);

                Assert.That(surface.transform.position.y, Is.EqualTo(1.54f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(surface);
                Object.DestroyImmediate(viewer);
            }
        }

        [TestCase("Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/VisitorProloguePresentation.prefab")]
        [TestCase("Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/ObservationCompletionSurface.prefab")]
        [TestCase("Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/CollectionWorldPresentation.prefab")]
        public void AuthoredPresentationCanvasChainsUseOnlyPositiveScale(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            var canvases = prefab.GetComponentsInChildren<Canvas>(true);
            Assert.That(canvases, Is.Not.Empty, prefabPath);

            foreach (var canvas in canvases)
                Assert.That(
                    WorldSurfacePlacement.HasPositiveScaleChain(canvas.transform),
                    Is.True,
                    $"{prefabPath}: Canvas '{canvas.name}' has a mirrored or invalid ancestor scale.");
        }
    }
}
