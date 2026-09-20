using System;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.Panorama.Backend;
using BotanicalGardenQR.SpatialHost.Contracts;
using BotanicalGardenQR.SpatialHost.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalWorldPlacementTests
    {
        [Test] public void RoomAndSixAnchorsRemainFixedAcrossWalkingAndPanoramaReturn()
        {
            var rig = new GameObject("XR test origin"); var camera = new GameObject("XR head", typeof(Camera)); camera.transform.SetParent(rig.transform, false);
            var definition = JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json").text);
            using var room = VirtualRoomEnvironment.Create(rig, null, definition);
            try
            {
                var world = GameObject.Find("VirtualWashingRoom").transform;
                Assert.That(world.parent, Is.Null);
                var pose = new Pose(world.position, world.rotation);
                var anchors = world.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Station_")).ToArray();
                Assert.That(anchors.Length, Is.EqualTo(6)); var positions = anchors.Select(t => t.position).ToArray();
                var token = SessionToken.CreateNew();
                for (int i = 0; i < 30; i++)
                {
                    camera.transform.localPosition = new Vector3(i * .05f, 1.65f, -.2f);
                    rig.transform.SetPositionAndRotation(new Vector3(.2f, 0, i * .02f), Quaternion.Euler(0, i * 2, 0));
                    room.Publish(new PanoramaState(token, i * 2, PanoramaPhase.Active, 0));
                    room.Publish(new PanoramaState(token, i * 2 + 1, PanoramaPhase.Closed, 0));
                    Assert.That(world.position, Is.EqualTo(pose.position)); Assert.That(world.rotation, Is.EqualTo(pose.rotation));
                    Assert.That(anchors.Select(t => t.position).ToArray(), Is.EqualTo(positions));
                }
            }
            finally { Object.DestroyImmediate(rig); }
        }
        [Test] public void PanoramaDoesNotTranslateWhenViewerWalks()
        {
            var root = new GameObject("Panorama root"); var viewer = new GameObject("Viewer", typeof(Camera));
            var controller = PanoramaModuleFactory.Create(root.transform, viewer.transform);
            var resolver = new PublishedSceneResolver(AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>("Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset"));
            ((IPanoramaDefinitionSource)resolver).TryGet(new SceneId("giant_saguaro"), out var definition);
            var token = SessionToken.CreateNew();
            try
            {
                controller.Open(token, definition, new PanoramaSurfaceLease(root.transform, Vector2.one, true, () => { }));
                var renderer = root.GetComponentsInChildren<MonoBehaviour>().Single(item => item.GetType().Name == "PanoramaRenderer");
                var position = renderer.transform.position;
                for (int frame = 0; frame < 60; frame++)
                {
                    viewer.transform.position = new Vector3(frame * .01f, .2f, -.3f);
                    renderer.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(renderer, null);
                    Assert.That(Vector3.Distance(position, renderer.transform.position), Is.LessThan(.0001f), "VR panorama must retain its initial world centre while the visitor walks.");
                }
            }
            finally { controller.Close(token); Object.DestroyImmediate(root); Object.DestroyImmediate(viewer); }
        }

        [Test] public void CoursePanelFacesViewerAfterDisplayHostCommitsItsPose()
        {
            var hostRoot = new GameObject("Display host"); var viewer = new GameObject("Viewer", typeof(Camera)); viewer.tag = "MainCamera";
            viewer.transform.SetPositionAndRotation(new Vector3(2, 1.65f, 3), Quaternion.Euler(15, 65, 8));
            var host = SpatialHostModuleFactory.Create(viewer.transform, hostRoot.transform);
            var profile = AssetDatabase.LoadAssetAtPath<DisplayProfile>("Assets/BotanicalGardenQR/Content/Authoring/DisplayProfiles/ViewerFront.asset");
            var font = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>("Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset").SharedFont;
            using var panel = new ClinicalCoursePanel(hostRoot.transform, font, new Registry(viewer.GetComponent<Camera>()), _ => true);
            var token = SessionToken.CreateNew();
            try
            {
                panel.Present(token, "baobab"); panel.SetVisible(true);
                var board = hostRoot.GetComponentsInChildren<Canvas>(true).Single(item => item.name == "ClinicalCoursePanel").transform;
                var prepared = host.Prepare(token, profile, null); Assert.That(prepared.Succeeded, Is.True); prepared.Lease.Commit();
                panel.SetVisible(true); panel.Tick(.02f);
                var towardBoard = (board.position - viewer.transform.position).normalized;
                Assert.That(Vector3.Angle(board.forward, towardBoard), Is.LessThan(1), "Panel must face the viewer after the host's independent placement commits.");
                Assert.That(Vector3.Distance(board.position, viewer.transform.position), Is.LessThan(.85f), "Hand panel must stay within reach.");
            }
            finally { panel.Dispose(); Object.DestroyImmediate(hostRoot); Object.DestroyImmediate(viewer); }
        }
        sealed class Registry : IFrontendGazeSurfaceRegistry
        {
            readonly Camera _camera;
            public Registry(Camera camera) { _camera = camera; }
            public IDisposable SuspendPanelInput() => new Lease();
            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform root, int priority, string label) { root.GetComponent<Canvas>().worldCamera = _camera; return new Lease(); }
            sealed class Lease : IFrontendGazeSurfaceRegistration { public bool IsFocused => false; public void Invalidate() { } public void Dispose() { } }
        }
    }
}
