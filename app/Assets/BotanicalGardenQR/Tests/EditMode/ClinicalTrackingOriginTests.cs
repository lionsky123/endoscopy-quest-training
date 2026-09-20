using System;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.MapNavigation.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalTrackingOriginTests
    {
        GameObject _root;
        OVRCameraRig _rig;
        VirtualRoomEnvironment _room;
        Transform _world;
        TestTiming _timing;

        [SetUp] public void SetUp()
        {
            _root = new GameObject("Boundary reset rig"); _root.SetActive(false);
            _root.transform.SetPositionAndRotation(new Vector3(2, 0, -3), Quaternion.Euler(0, 23, 0));
            var manager = _root.AddComponent<OVRManager>();
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("_trackingOriginType").intValue = (int)OVRManager.TrackingOrigin.Stage;
            serialized.FindProperty("AllowRecenter").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            _rig = _root.AddComponent<OVRCameraRig>(); _rig.EnsureGameObjectIntegrity();
            _rig.centerEyeAnchor.SetLocalPositionAndRotation(new Vector3(.5f, 1.6f, -.4f), Quaternion.Euler(12, 48, 0));
            _rig.leftHandAnchor.localPosition = new Vector3(.2f, 1.2f, .1f);
            _rig.rightHandAnchor.localPosition = new Vector3(.8f, 1.2f, .1f);
            var map = JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json").text);
            _timing = new TestTiming();
            _room = VirtualRoomEnvironment.Create(_root, null, map, _timing);
            _world = GameObject.Find("VirtualWashingRoom").transform;
            RaiseSamples(9);
        }

        [TearDown] public void TearDown() { _room?.Dispose(); Object.DestroyImmediate(_root); }

        [TestCase(-170f, -1.2f, .8f)]
        [TestCase(-70f, .5f, -.4f)]
        [TestCase(0f, 0f, 0f)]
        [TestCase(48f, .5f, -.4f)]
        [TestCase(155f, 1.1f, -.9f)]
        public void FirstTrackedHeadStartsAtTheAuthoredEntranceFacingDownTheAisle(float physicalYaw, float physicalX, float physicalZ)
        {
            var map = JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json").text);
            _room.Dispose();
            _rig.trackingSpace.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _rig.centerEyeAnchor.SetLocalPositionAndRotation(new Vector3(physicalX, 1.6f, physicalZ), Quaternion.Euler(12, physicalYaw, 0));
            _room = VirtualRoomEnvironment.Create(_root, null, map, _timing);
            _world = GameObject.Find("VirtualWashingRoom").transform;
            var roomPose = new Pose(_world.position, _world.rotation);
            RaiseSamples(9);
            var localHead = _world.InverseTransformPoint(_rig.centerEyeAnchor.position);
            Assert.That(Vector2.Distance(new Vector2(localHead.x, localHead.z), new Vector2(map.start.x, map.start.z)), Is.LessThan(.001f),
                "The first reliable head sample, not the XR root alone, must be aligned to the fixed entrance.");
            var forward = Vector3.ProjectOnPlane(_world.InverseTransformDirection(_rig.centerEyeAnchor.forward), Vector3.up).normalized;
            Assert.That(Vector3.Angle(forward, Vector3.left), Is.LessThan(.01f), "Every launch must face along the same model aisle regardless of the initial physical head yaw.");
            Assert.That(_world.position, Is.EqualTo(roomPose.position));
            Assert.That(_world.rotation, Is.EqualTo(roomPose.rotation));
            Assert.That(_rig.centerEyeAnchor.position.y, Is.EqualTo(1.6f).Within(.001f), "Yaw alignment must preserve standing height.");
            var space = new Pose(_rig.trackingSpace.position, _rig.trackingSpace.rotation);
            _rig.centerEyeAnchor.localRotation *= Quaternion.Euler(0, 35, 0);
            _rig.centerEyeAnchor.localPosition += Vector3.right * .2f;
            RaiseSamples(9.1);
            Assert.That(_rig.trackingSpace.position, Is.EqualTo(space.position));
            Assert.That(_rig.trackingSpace.rotation, Is.EqualTo(space.rotation), "Startup alignment must not recenter later head turns.");
        }

        [TestCase(95f)] [TestCase(-120f)]
        public void SystemStageResetPreservesRoomRelativeToHeadAndHands(float yaw)
        {
            var anchors = new[] { _rig.centerEyeAnchor, _rig.leftHandAnchor, _rig.rightHandAnchor };
            var poses = anchors.Select(t => new Pose(t.position, t.rotation)).ToArray();
            var worldPose = new Pose(_world.position, _world.rotation);
            var stationPositions = _world.Cast<Transform>().Where(t => t.name.StartsWith("Station_")).Select(t => t.position).ToArray();
            var delta = new OVRPose { position = new Vector3(1.2f, 0, -.75f), orientation = Quaternion.Euler(0, yaw, 0) };
            RaiseOriginChange(OVRManager.TrackingOrigin.Stage, delta);
            foreach (var anchor in anchors) ApplyNewTrackingCoordinates(anchor, delta);
            RaiseSamples(_timing.ChangeTime);
            Assert.That(Vector3.Distance(_rig.centerEyeAnchor.position, poses[0].position), Is.LessThan(.0001f), "A stationary user's world view jumps when Quest redefines its Stage origin.");
            for (int i = 0; i < anchors.Length; i++)
            {
                Assert.That(Vector3.Distance(anchors[i].position, poses[i].position), Is.LessThan(.0001f), "Head and hands must use the same corrected frame.");
                Assert.That(Quaternion.Angle(anchors[i].rotation, poses[i].rotation), Is.LessThan(.02f));
            }
            Assert.That(_world.position, Is.EqualTo(worldPose.position));
            Assert.That(_world.rotation, Is.EqualTo(worldPose.rotation));
            Assert.That(_world.Cast<Transform>().Where(t => t.name.StartsWith("Station_")).Select(t => t.position).ToArray(), Is.EqualTo(stationPositions));
        }

        [Test] public void PendingNotificationCannotMoveTheViewBeforeNewSamplesArrive()
        {
            var before = new Pose(_rig.centerEyeAnchor.position, _rig.centerEyeAnchor.rotation);
            var delta = new OVRPose { position = new Vector3(1.2f, 0, -.75f), orientation = Quaternion.Euler(0, 95, 0) };
            RaiseOriginChange(OVRManager.TrackingOrigin.Stage, delta);
            Assert.That(Vector3.Distance(_rig.centerEyeAnchor.position, before.position), Is.LessThan(.0001f),
                "A pending event must not change the world view while the headset still reports OLD coordinates.");
            Assert.That(Quaternion.Angle(_rig.centerEyeAnchor.rotation, before.rotation), Is.LessThan(.02f));
            RaiseSamples(_timing.ChangeTime - .02);
            Assert.That(Vector3.Distance(_rig.centerEyeAnchor.position, before.position), Is.LessThan(.0001f));
            Assert.That(_room.TrackingOrigin.CanInteract, Is.False);
            ApplyNewTrackingCoordinates(_rig.centerEyeAnchor, delta);
            RaiseSamples(_timing.ChangeTime);
            Assert.That(Vector3.Distance(_rig.centerEyeAnchor.position, before.position), Is.LessThan(.0001f));
            Assert.That(_room.TrackingOrigin.CanInteract, Is.True);
        }

        [Test] public void RepeatedResetsComposeAndPhysicalMovementStillWorks()
        {
            var head = _rig.centerEyeAnchor;
            var before = new Pose(head.position, head.rotation);
            for (int i = 0; i < 6; i++)
            {
                _timing.ChangeTime += 1;
                var delta = new OVRPose { position = new Vector3(.3f, 0, -.2f), orientation = Quaternion.Euler(0, 37, 0) };
                RaiseOriginChange(OVRManager.TrackingOrigin.Stage, delta);
                ApplyNewTrackingCoordinates(head, delta);
                RaiseSamples(_timing.ChangeTime);
                Assert.That(Vector3.Distance(head.position, before.position), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(head.rotation, before.rotation), Is.LessThan(.03f));
            }
            var step = new Vector3(.2f, 0, .1f);
            var expectedStep = _rig.trackingSpace.TransformVector(step);
            head.localPosition += step;
            head.localRotation *= Quaternion.Euler(0, 20, 0);
            Assert.That(Vector3.Distance(head.position, before.position + expectedStep), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(head.rotation, before.rotation), Is.EqualTo(20).Within(.03f));
        }

        [Test] public void UnrelatedAndDisposedEventsCannotMoveTrackingSpace()
        {
            var before = new Pose(_rig.trackingSpace.position, _rig.trackingSpace.rotation);
            var delta = new OVRPose { position = Vector3.one, orientation = Quaternion.Euler(0, 60, 0) };
            RaiseOriginChange(OVRManager.TrackingOrigin.FloorLevel, delta);
            Assert.That(_rig.trackingSpace.position, Is.EqualTo(before.position));
            Assert.That(_rig.trackingSpace.rotation, Is.EqualTo(before.rotation));
            _room.Dispose(); _room = null;
            RaiseOriginChange(OVRManager.TrackingOrigin.Stage, delta);
            Assert.That(_rig.trackingSpace.position, Is.EqualTo(before.position));
            Assert.That(_rig.trackingSpace.rotation, Is.EqualTo(before.rotation));
        }

        [Test] public void MissingRelationRequiresRecoveryWithoutGuessingFromHeadMovement()
        {
            var before = _rig.trackingSpace.position;
            LogAssert.Expect(LogType.Warning, new Regex("\\[VRTracking\\] alignment unavailable"));
            RaiseOriginChange(OVRManager.TrackingOrigin.Stage, null);
            _rig.centerEyeAnchor.localPosition += Vector3.right * 2;
            RaiseSamples(20);
            Assert.That(_room.TrackingOrigin.RecoveryRequired, Is.True);
            Assert.That(_room.TrackingOrigin.CanInteract, Is.False);
            Assert.That(_rig.trackingSpace.position, Is.EqualTo(before));
        }

        [Test] public void FirstReferenceSpaceWithoutPreviousPoseIsInitializationNotRecovery()
        {
            _room.Dispose(); _room = null;
            using var origin = new VirtualRoomTrackingOrigin(_rig, _root.GetComponent<OVRManager>(), _timing);
            Assert.That(origin.CanInteract, Is.False);
            RaiseOriginChange(OVRManager.TrackingOrigin.Stage, null);
            Assert.That(origin.RecoveryRequired, Is.False);
            RaiseSamples(9);
            Assert.That(origin.CanInteract, Is.True);
        }

        [Test] public void MissingTimestampCannotFallBackToImmediateCompensation()
        {
            _timing.HasChangeTime = false;
            var before = _rig.trackingSpace.position;
            LogAssert.Expect(LogType.Warning, new Regex("\\[VRTracking\\] alignment unavailable"));
            RaiseOriginChange(OVRManager.TrackingOrigin.Stage, new OVRPose { position = Vector3.one, orientation = Quaternion.identity });
            Assert.That(_room.TrackingOrigin.RecoveryRequired, Is.True);
            Assert.That(_rig.trackingSpace.position, Is.EqualTo(before));
        }

        [Test] public void DuplicateEventsAndRepeatedRenderSamplesDoNotAccumulateCorrection()
        {
            var before = new Pose(_rig.centerEyeAnchor.position, _rig.centerEyeAnchor.rotation);
            var delta = new OVRPose { position = new Vector3(.4f, 0, -.2f), orientation = Quaternion.Euler(0, 48, 0) };
            RaiseOriginChange(OVRManager.TrackingOrigin.Stage, delta);
            RaiseOriginChange(OVRManager.TrackingOrigin.Stage, delta);
            ApplyNewTrackingCoordinates(_rig.centerEyeAnchor, delta);
            for (int i = 0; i < 30; i++) RaiseSamples(_timing.ChangeTime + i * .01);
            Assert.That(Vector3.Distance(_rig.centerEyeAnchor.position, before.position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(_rig.centerEyeAnchor.rotation, before.rotation), Is.LessThan(.02f));
        }

        [Test] public void UpdateAndBeforeRenderCanSampleBothSidesOfTheBoundary()
        {
            var head = _rig.centerEyeAnchor;
            var local = new Pose(head.localPosition, head.localRotation);
            var world = new Pose(head.position, head.rotation);
            var delta = new OVRPose { position = Vector3.right, orientation = Quaternion.Euler(0, 65, 0) };
            RaiseOriginChange(OVRManager.TrackingOrigin.Stage, delta);
            ApplyNewTrackingCoordinates(head, delta); RaiseSamples(_timing.ChangeTime + .001);
            Assert.That(Vector3.Distance(head.position, world.position), Is.LessThan(.0001f));
            head.SetLocalPositionAndRotation(local.position, local.rotation); RaiseSamples(_timing.ChangeTime - .001);
            Assert.That(Vector3.Distance(head.position, world.position), Is.LessThan(.0001f));
            ApplyNewTrackingCoordinates(head, delta); RaiseSamples(_timing.ChangeTime + .01);
            Assert.That(Vector3.Distance(head.position, world.position), Is.LessThan(.0001f));
        }

        [Test] public void TrackingLossPausesInteractionUntilValidSamplesReturn()
        {
            _timing.Tracked = false; RaiseSamples(8);
            Assert.That(_room.TrackingOrigin.CanInteract, Is.False);
            Assert.That(_room.TrackingOrigin.RecoveryRequired, Is.False);
            _timing.Tracked = true; RaiseSamples(9);
            Assert.That(_room.TrackingOrigin.CanInteract, Is.True);
        }

        [Test] public void Meta205EventBufferRetainsNativeChangeTime()
        {
            Assert.That(MetaTrackingOriginTiming.LayoutSupported, Is.True);
            var field = typeof(OVRManager).GetField("eventDataBuffer", BindingFlags.Static | BindingFlags.NonPublic);
            var previous = field.GetValue(null);
            try
            {
                var data = new byte[4000];
                BitConverter.GetBytes((int)OVRManager.TrackingOrigin.Stage).CopyTo(data, 0);
                BitConverter.GetBytes(1234.56789).CopyTo(data, 4);
                field.SetValue(null, new OVRPlugin.EventDataBuffer { EventType = OVRPlugin.EventType.ReferenceSpaceChangePending, EventData = data });
                var timing = new MetaTrackingOriginTiming();
                Assert.That(timing.TryGetChangeTime(OVRManager.TrackingOrigin.Stage, out var time), Is.True);
                Assert.That(time, Is.EqualTo(1234.56789));
                Assert.That(timing.TryGetChangeTime(OVRManager.TrackingOrigin.FloorLevel, out _), Is.False);
            }
            finally { field.SetValue(null, previous); }
        }

        [Test] public void RecoveryGuardBlocksHandsShowsNoticeAndNeverDisablesHeadTracking()
        {
            var interaction = new GameObject("Hand interaction"); interaction.transform.SetParent(_root.transform, false);
            var defaults = AssetDatabase.LoadAssetAtPath<BotanicalGardenQR.Configuration.Runtime.GlobalUiDefaults>(
                "Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset");
            using var guard = new VirtualRoomTrackingGuard(_room.TrackingOrigin, _rig.centerEyeAnchor, interaction.transform, defaults.SharedFont);
            var cameraWasActive = _rig.centerEyeAnchor.gameObject.activeSelf;
            _timing.Tracked = false; RaiseSamples(9);
            Assert.That(interaction.activeSelf, Is.False);
            var notice = _rig.centerEyeAnchor.Find("VRTrackingRecoveryNotice");
            Assert.That(notice, Is.Not.Null);
            Assert.That(notice.gameObject.activeSelf, Is.True);
            _timing.Tracked = true; RaiseSamples(9.1);
            Assert.That(interaction.activeSelf, Is.True);
            Assert.That(notice.gameObject.activeSelf, Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("\\[VRTracking\\] alignment unavailable"));
            RaiseOriginChange(OVRManager.TrackingOrigin.Stage, null);
            Assert.That(interaction.activeSelf, Is.False);
            Assert.That(notice.gameObject.activeSelf, Is.True);
            Assert.That(notice.GetComponentInChildren<TMPro.TMP_Text>().text, Does.Contain("重新打开"));
            Assert.That(_rig.centerEyeAnchor.gameObject.activeSelf, Is.EqualTo(cameraWasActive));
            guard.Dispose();
            Assert.That(interaction.activeSelf, Is.True);
            Assert.That(_rig.centerEyeAnchor.Find("VRTrackingRecoveryNotice"), Is.Null);
        }

        [Test] public void PendingChangeDisablesHandsUntilCompensationIsApplied()
        {
            var interaction = new GameObject("Hand interaction"); interaction.transform.SetParent(_root.transform, false);
            using var guard = new VirtualRoomTrackingGuard(_room.TrackingOrigin, _rig.centerEyeAnchor, interaction.transform, null);
            var delta = new OVRPose { position = Vector3.right, orientation = Quaternion.identity };
            RaiseOriginChange(OVRManager.TrackingOrigin.Stage, delta);
            Assert.That(interaction.activeSelf, Is.False);
            Assert.That(_rig.centerEyeAnchor.Find("VRTrackingRecoveryNotice"), Is.Null);
            ApplyNewTrackingCoordinates(_rig.centerEyeAnchor, delta); RaiseSamples(10);
            Assert.That(interaction.activeSelf, Is.True);
        }

        void RaiseSamples(double time)
        {
            _timing.SampleTime = time;
            var field = typeof(OVRCameraRig).GetField("UpdatedAnchors", BindingFlags.Instance | BindingFlags.NonPublic);
            (field.GetValue(_rig) as Action<OVRCameraRig>)?.Invoke(_rig);
        }

        sealed class TestTiming : ITrackingOriginTiming
        {
            public double ChangeTime = 10, SampleTime = 9;
            public bool Tracked = true, HasChangeTime = true;
            public bool TryGetChangeTime(OVRManager.TrackingOrigin origin, out double time) { time = ChangeTime; return HasChangeTime; }
            public bool TryGetSampleTime(out double time) { time = SampleTime; return Tracked; }
        }

        static void ApplyNewTrackingCoordinates(Transform anchor, OVRPose delta)
        {
            var inverse = Quaternion.Inverse(delta.orientation);
            anchor.SetLocalPositionAndRotation(inverse * (anchor.localPosition - delta.position), inverse * anchor.localRotation);
        }
        static void RaiseOriginChange(OVRManager.TrackingOrigin origin, OVRPose? delta)
        {
            // Replay the actual SDK event used when Quest reports ReferenceSpaceChangePending.
            var field = typeof(OVRManager).GetField("TrackingOriginChangePending", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            (field.GetValue(null) as Action<OVRManager.TrackingOrigin, OVRPose?>)?.Invoke(origin, delta);
        }
    }
}
