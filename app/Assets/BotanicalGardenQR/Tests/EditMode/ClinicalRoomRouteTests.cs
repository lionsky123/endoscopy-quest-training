using System.Linq;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Fairy.Backend;
using BotanicalGardenQR.Fairy.Contracts;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.MapNavigation.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalRoomRouteTests
    {
        [TestCase(0f)]
        [TestCase(73f)]
        public void TurningAtEntranceCannotPullFairyThroughTheRoomWall(float roomYaw)
        {
            var rig = new GameObject("Route test rig");
            rig.transform.SetPositionAndRotation(new Vector3(2, 0, -3), Quaternion.Euler(0, roomYaw, 0));
            var viewer = new GameObject("Route viewer"); viewer.transform.SetParent(rig.transform, false);
            viewer.transform.localPosition = Vector3.up * 1.65f;
            var actor = new GameObject("Route fairy");
            var definition = JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json").text);
            using var room = VirtualRoomEnvironment.Create(rig, null, definition);
            try
            {
                var world = GameObject.Find("VirtualWashingRoom").transform;
                var obstacles = world.GetComponentsInChildren<MeshFilter>().Where(m =>
                    m.sharedMesh.bounds.max.y > .15f && m.sharedMesh.bounds.min.y < 1.8f).ToArray();
                var driver = actor.AddComponent<FairyOrbitDriver>();
                driver.Initialize(viewer.transform, rig.transform, FairyBehavior.Guide, walkSpace: room.GuidePath);
                viewer.transform.localRotation = Quaternion.Euler(0, 90, 0);
                for (int frame = 0; frame < 300; frame++)
                {
                    driver.Tick(.02f);
                    var local = world.InverseTransformPoint(actor.transform.position);
                    foreach (var obstacle in obstacles)
                    {
                        var bounds = obstacle.sharedMesh.bounds;
                        var closest = bounds.ClosestPoint(new Vector3(local.x, bounds.center.y, local.z));
                        var gap = Vector2.Distance(new Vector2(local.x, local.z), new Vector2(closest.x, closest.z));
                        Assert.That(gap, Is.GreaterThanOrEqualTo(.2f),
                            $"Fairy crosses model obstacle {obstacle.name} at room {local}, frame {frame}, clearance {gap:F3}m");
                    }
                }
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(rig); }
        }

        [Test] public void EveryStationRecoveryUsesModelAislesInsteadOfStraightLineShortcuts()
        {
            var rig = new GameObject("Route network rig");
            var definition = JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json").text);
            using var room = VirtualRoomEnvironment.Create(rig, null, definition);
            Vector3 World(MapPosition p) { var value = room.Frame.Transform(p); return new Vector3(value.x, value.y, value.z); }
            try
            {
                foreach (var from in definition.points)
                    foreach (var to in definition.points)
                    {
                        var position = World(from.position); var target = World(to.position); float travelled = 0;
                        for (int i = 0; i < 2000 && Vector3.Distance(position, target) > .001f; i++)
                        {
                            var waypoint = room.GuidePath.NextWaypoint(position, target);
                            var next = Vector3.MoveTowards(position, waypoint, .04f);
                            Assert.That(room.GuidePath.CanStep(position, next), Is.True, from.id + " -> " + to.id);
                            travelled += Vector3.Distance(position, next); position = next;
                        }
                        Assert.That(Vector3.Distance(position, target), Is.LessThan(.001f), "Recovery must reach " + from.id + " -> " + to.id);
                        if (from.id == "P03" && to.id == "P04")
                            Assert.That(travelled, Is.GreaterThan(6f), "The sink row requires walking around its end, not a direct crossing.");
                    }
                for (int i = 1; i <= 100; i++)
                    Assert.That(room.GuidePath.CanStep(room.GuidePath.ArrivalPoint((i - 1) / 100f), room.GuidePath.ArrivalPoint(i / 100f)), Is.True);
            }
            finally { Object.DestroyImmediate(rig); }
        }

        [Test] public void SixLegsAndDialogueCuesKeepTheActualGuideOnTheSharedRoomPath()
        {
            var rig = new GameObject("Six-leg rig"); var viewer = new GameObject("Six-leg viewer");
            viewer.transform.position = Vector3.up * 1.65f;
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/BotanicalGardenQR/Content/Shared/Fairy/Models/Oppy/OppyFairyGuide.prefab"));
            var definition = JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json").text);
            using var room = VirtualRoomEnvironment.Create(rig, null, definition);
            var driver = actor.AddComponent<FairyOrbitDriver>();
            driver.Initialize(viewer.transform, rig.transform, FairyBehavior.Guide, walkSpace: room.GuidePath);
            using var navigation = new MapNavigationController(definition, new DriverMotion(driver, definition), room.Frame);
            try
            {
                foreach (var point in definition.points)
                {
                    Assert.That(navigation.Begin(point.id), Is.True);
                    var route = new MapRouteGeometry(navigation.CurrentWorldPath);
                    for (int i = 0; i < 3000 && navigation.State.Phase != MapNavigationPhase.Arrived; i++)
                    {
                        var before = actor.transform.position;
                        navigation.Tick(route.Sample(navigation.State.Progress), true, false, .02f); driver.Tick(.02f);
                        Assert.That(room.GuidePath.CanStep(before, actor.transform.position), Is.True, "Movement left model-bound rails at " + point.id);
                    }
                    Assert.That(navigation.State.Phase, Is.EqualTo(MapNavigationPhase.Arrived),
                        $"{point.id}: progress {navigation.State.Progress:F3}/{navigation.State.Length:F3}, fairy {actor.transform.position}, route {route.Sample(navigation.State.Progress).x:F3},{route.Sample(navigation.State.Progress).z:F3}");
                    var arrived = actor.transform.position;
                    driver.PresentCue(new FairyCompanionCue(FairyCompanionCueKind.DialogueFocus, arrived + Vector3.right * 2, 0));
                    for (int frame = 0; frame < 100; frame++) driver.Tick(.02f);
                    Assert.That(actor.transform.position, Is.EqualTo(arrived), "A dialogue anchor beyond a wall cannot pull the guide off its station.");
                    driver.PresentCue(FairyCompanionCue.Idle);
                }
            }
            finally { navigation.Dispose(); Object.DestroyImmediate(actor); Object.DestroyImmediate(viewer); Object.DestroyImmediate(rig); }
        }

        [Test] public void ExplicitRecoveryWalksAroundTheSinkAndHoldsWhenPaused()
        {
            var rig = new GameObject("Recovery rig"); var viewer = new GameObject("Recovery viewer");
            var actor = new GameObject("Recovery guide");
            var definition = JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json").text);
            using var room = VirtualRoomEnvironment.Create(rig, null, definition);
            Vector3 World(MapPosition p) { var value = room.Frame.Transform(p); return new Vector3(value.x, value.y, value.z); }
            try
            {
                var driver = actor.AddComponent<FairyOrbitDriver>();
                driver.Initialize(viewer.transform, rig.transform, FairyBehavior.Guide, walkSpace: room.GuidePath);
                actor.transform.position = World(definition.points[2].position);
                var target = World(definition.points[3].position); var before = actor.transform.position;
                Assert.That(driver.TryRecall(target), Is.True);
                Assert.That(driver.TryRecall(target), Is.False, "Only one recovery request may own the path.");
                driver.HoldMotion(1);
                for (int i = 0; i < 50; i++) driver.Tick(.02f);
                Assert.That(actor.transform.position, Is.EqualTo(before));
                // The actual navigation sink must be able to resume a held recall.
                driver.ApplyMotion(1, target, Vector3.zero, false, 1.5f, .7f);
                float distance = 0;
                for (int i = 0; i < 4000 && Vector3.Distance(actor.transform.position, target) > .001f; i++)
                {
                    before = actor.transform.position; driver.Tick(.02f);
                    Assert.That(room.GuidePath.CanStep(before, actor.transform.position), Is.True);
                    distance += Vector3.Distance(before, actor.transform.position);
                }
                Assert.That(Vector3.Distance(actor.transform.position, target), Is.LessThan(.001f));
                Assert.That(distance, Is.GreaterThan(6f));
                driver.Tick(.1f);
                Assert.That(Vector3.Distance(actor.transform.position, target), Is.LessThan(.001f), "Finishing recovery must not resume head-relative following.");
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(viewer); Object.DestroyImmediate(rig); }
        }

        [Test] public void ARoutePublishedAgainstAnotherModelIsRejected()
        {
            var rig = new GameObject("Mismatched room rig");
            var definition = JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json").text);
            definition.modelDigest = "outdated-model";
            try { Assert.Throws<System.InvalidOperationException>(() => VirtualRoomEnvironment.Create(rig, null, definition)); }
            finally { Object.DestroyImmediate(rig); }
        }

        [TestCase(0f)] [TestCase(90f)] [TestCase(180f)]
        public void RuntimeFactoryKeepsTheOpeningGuideAtTheSameRoomPosition(float headYaw)
        {
            var rig = new GameObject("Opening rig"); var viewer = new GameObject("Opening viewer", typeof(Camera));
            viewer.transform.SetPositionAndRotation(Vector3.up * 1.65f, Quaternion.Euler(0, headYaw, 0));
            var runtime = new GameObject("Opening runtime"); var lighting = new GameObject("Opening light", typeof(Light));
            var definition = JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json").text);
            using var room = VirtualRoomEnvironment.Create(rig, null, definition);
            var source = AssetDatabase.LoadAssetAtPath<FairyApplicationConfiguration>("Assets/BotanicalGardenQR/Content/Authoring/FairyApplicationConfiguration.asset");
            Assert.That(source.TryGet(out var authored), Is.True);
            var instant = new FairyDefinition(authored.Prefab, authored.Behavior, authored.Scale, authored.ArrivalEffectPrefab,
                authored.ArrivalMaskPrefab, authored.CompanionFeedback, 0, 0);
            var controller = FairyModuleFactory.Create(runtime.transform, viewer.transform, rig.transform, null,
                lighting.GetComponent<Light>(), walkSpace: room.GuidePath);
            using var binding = FairyModuleFactory.BindAsCompanion(controller, instant, initiallyVisible: false);
            try
            {
                Assert.That(binding.Show().Succeeded, Is.True);
                var actor = runtime.GetComponentsInChildren<FairyOrbitDriver>().Single();
                Assert.That(Vector3.Distance(actor.transform.position, room.GuidePath.StartPosition), Is.LessThan(.001f));
                for (int i = 0; i < 100; i++) actor.Tick(.02f);
                Assert.That(Vector3.Distance(actor.transform.position, room.GuidePath.StartPosition), Is.LessThan(.001f), "Turning before invitation must not change the entry/landing point.");
            }
            finally { binding.Dispose(); Object.DestroyImmediate(runtime); Object.DestroyImmediate(lighting); Object.DestroyImmediate(viewer); Object.DestroyImmediate(rig); }
        }

        sealed class DriverMotion : IMapMotionSink
        {
            readonly FairyOrbitDriver _driver; readonly MapDefinition _definition;
            public DriverMotion(FairyOrbitDriver driver, MapDefinition definition) { _driver = driver; _definition = definition; }
            public bool TryGetPosition(out MapPosition p) { var v = _driver.transform.position; p = new MapPosition(v.x, v.y, v.z); return true; }
            public bool Apply(long id, MapPosition p, MapPosition direction, bool moving) => _driver.ApplyMotion(id,
                new Vector3(p.x, p.y, p.z), new Vector3(direction.x, direction.y, direction.z), moving, _definition.fairyJoinRadius, _definition.speed);
            public void Hold(long id) => _driver.HoldMotion(id);
            public void Release(long id) => _driver.ReleaseMotion(id);
        }
    }
}
