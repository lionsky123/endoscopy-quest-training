using System;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.MapNavigation.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BotanicalGardenQR.VisitorAtlasHub.Frontend;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.MapNavigation.Frontend;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class VisitorMapGuidanceIntegrationTests
    {
        [Test]
        public void PublishedSixLegTourRequiresCompletionAndExplicitDepartureWithoutPlantOrder()
        {
            var definition = VisitorMapConfiguration.Resolve(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json"));
            using var navigation = new MapNavigationController(definition, new Motion());
            for (int i = 0; i < 12; i++) navigation.TryInitialize(default, 0, 0, .02f);
            var scanStarts = 0;
            using var app = new VisitorGuidanceCoordinator(navigation, () => scanStarts++);
            app.Begin(); app.Refresh(0, default, true, false);
            for (int point = 0; point < definition.points.Length; point++)
            {
                var route = new MapRouteGeometry(navigation.CurrentWorldPath);
                for (int tick = 0; tick < 2000 && navigation.State.Phase != MapNavigationPhase.Arrived; tick++)
                    navigation.Tick(route.Sample(navigation.State.Progress), true, false, .05f);
                Assert.That(navigation.State.Phase, Is.EqualTo(MapNavigationPhase.Arrived));
                for (var stable = 0; stable < 4; stable++) app.TrackVisitorArrival(route.Sample(route.Length), true, .1f);
                Assert.That(scanStarts, Is.EqualTo(1));
                Assert.That(app.TargetTitle, Is.EqualTo(definition.points[point].id));
                Assert.That(app.ShowRoute, Is.False);
                Assert.That(app.ShowTarget, Is.True);
                app.TargetContentOpened();
                Assert.That(app.ShowTarget, Is.False);
                var request = navigation.State.RequestId;
                app.Refresh(point + 1, default, true, false);
                Assert.That(navigation.State.RequestId, Is.EqualTo(request));
                if (point + 1 < definition.points.Length)
                {
                    Assert.That(app.Surface.Kind, Is.EqualTo(VisitorGuidanceSurfaceKind.Departure));
                    app.HandleIntent(VisitorDialogueIntentKind.Advance);
                    Assert.That(navigation.CurrentWorldPath[0], Is.EqualTo(navigation.Frame.Transform(definition.points[point].position)));
                }
            }
            Assert.That(app.Phase, Is.EqualTo(VisitorGuidancePhase.Ended));
        }

        const string AtlasPrefab = "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/VisitorAtlasHubPresentation.prefab";

        [Test]
        public void ProductionMapSurvivesToolHidingAndViewerMovementAtFixedWorldCoordinates()
        {
            var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AtlasPrefab));
            var viewer = new GameObject("MapViewer");
            var rig = new GameObject("MapTestRig");
            rig.SetActive(false);
            rig.AddComponent<Oculus.Interaction.Input.Hand>();
            var presentation = instance.GetComponent<VisitorAtlasHubPresentation>();
            try
            {
                presentation.BindGazeInput(new GazeRegistry());
                presentation.Configure(viewer.transform, rig.transform);
                presentation.CommitSessionRoot(new Pose(new Vector3(3, .15f, 4), Quaternion.Euler(0, 35, 0)));
                var landmark = new GameObject("AuthoredPoint").transform;
                landmark.SetParent(presentation.MapContentRoot, false);
                landmark.localPosition = new Vector3(2, 0, 3);
                var expected = landmark.position;
                for (int i = 0; i < 4; i++)
                {
                    viewer.transform.SetPositionAndRotation(new Vector3(i * 2, 1.6f, -i), Quaternion.Euler(0, i * 90, 0));
                    presentation.SetMapDisplay(true, true);
                    presentation.SetVisible(true);
                    presentation.RefreshEntryChoicePose();
                    presentation.SetVisible(false);
                    Assert.That(landmark.gameObject.activeInHierarchy, Is.True, "Hiding tools must retain the map.");
                    Assert.That(landmark.position, Is.EqualTo(expected));
                    Assert.That(instance.transform.Find("VisualRoot/EntryChoices").gameObject.activeInHierarchy, Is.False);
                    presentation.SetMapDisplay(true, false);
                    Assert.That(landmark.gameObject.activeInHierarchy, Is.False);
                    presentation.SetMapDisplay(true, true);
                    Assert.That(landmark.position, Is.EqualTo(expected));
                }
                presentation.SetMapDisplay(false, false);
                Assert.That(presentation.MapContentRoot.gameObject.activeInHierarchy, Is.False);
            }
            finally
            {
                presentation.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
                UnityEngine.Object.DestroyImmediate(rig);
            }
        }

        [Test]
        public void GlobalRouteAndDestinationRemainVisibleWhileContentHostIsHidden()
        {
            var runtime = new GameObject("VisitorRuntime");
            var content = new GameObject("SpatialDisplayRoot");
            content.transform.SetParent(runtime.transform, false);
            content.SetActive(false);
            var atlas = AssetDatabase.LoadAssetAtPath<GameObject>(AtlasPrefab).GetComponent<VisitorAtlasHubPresentation>();
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/BotanicalGardenQR/Content/Shared/MapNavigation/GuidanceRoute.mat");
            var path = new[] {new MapPosition(), new MapPosition(1, 0, 2)};
            try
            {
                using var route = MapRoutePresentationFactory.Create(runtime.transform, material, atlas.MapLabelFont);
                route.Present(path, true, true, "巨人柱");
                foreach (var line in runtime.GetComponentsInChildren<LineRenderer>(true))
                    Assert.That(line.gameObject.activeInHierarchy, Is.True);
                var label = runtime.GetComponentInChildren<TMPro.TextMeshPro>(true);
                Assert.That(label.text, Is.EqualTo("巨人柱"));
                var target = label.transform.position;
                content.transform.position = new Vector3(7, 1, 4);
                route.Present(path, false, true, "巨人柱");
                Assert.That(label.gameObject.activeInHierarchy, Is.True);
                Assert.That(label.transform.position, Is.EqualTo(target));
                route.Present(path, false, false, "");
                Assert.That(label.gameObject.activeInHierarchy, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(runtime); }
        }

        sealed class GazeRegistry : IFrontendGazeSurfaceRegistry
        {
            public IDisposable SuspendPanelInput() => new Registration();
            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform root, int priority, string label) => new Registration();
            sealed class Registration : IFrontendGazeSurfaceRegistration
            {
                public bool IsFocused => false;
                public void Invalidate() { }
                public void Dispose() { }
            }
        }

        sealed class Motion : IMapMotionSink
        {
            MapPosition _position;
            bool _applied;
            public bool TryGetPosition(out MapPosition position)
            {
                position = _position;
                return _applied;
            }

            public bool Apply(long requestId, MapPosition position, MapPosition forward, bool moving)
            {
                _position = position;
                _applied = true;
                return true;
            }

            public void Hold(long requestId)
            {
            }

            public void Release(long requestId)
            {
            }
        }
    }
}
