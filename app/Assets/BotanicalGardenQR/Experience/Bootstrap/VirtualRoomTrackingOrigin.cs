using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    // Compensate the SAME reference space as the head/hand samples, never the time
    // at which a pending notification happened to reach the application's Update.
    internal sealed class VirtualRoomTrackingOrigin : IDisposable
    {
        readonly OVRCameraRig _rig;
        readonly OVRManager _manager;
        readonly ITrackingOriginTiming _timing;
        readonly List<Change> _changes = new List<Change>();
        Pose _initialSpace;
        readonly Pose? _startupPose;
        bool _startupAligned;
        bool _tracked, _hasTrackedSample;
        bool _disposed;
        public bool RecoveryRequired { get; private set; }
        public bool TrackingAvailable => _tracked;
        public bool CanInteract => !_disposed && _tracked && _startupAligned && !RecoveryRequired && !HasPendingChange;
        public bool HasPendingChange { get; private set; }
        public event Action StateChanged;

        public VirtualRoomTrackingOrigin(OVRCameraRig rig, OVRManager manager, ITrackingOriginTiming timing = null, Pose? startupPose = null)
        {
            _rig = rig; _manager = manager;
            _timing = timing ?? new MetaTrackingOriginTiming();
            _startupPose = startupPose; _startupAligned = !startupPose.HasValue;
            _rig.EnsureGameObjectIntegrity();
            _initialSpace = new Pose(_rig.trackingSpace.position, _rig.trackingSpace.rotation);
            OVRManager.TrackingOriginChangePending += OnOriginChange;
            _rig.UpdatedAnchors += OnSamplesUpdated;
        }

        void OnOriginChange(OVRManager.TrackingOrigin origin, OVRPose? poseInPreviousSpace)
        {
            if (_disposed || !_rig || !_rig.trackingSpace || RecoveryRequired) return;
            var currentOrigin = _manager ? _manager.trackingOriginType : OVRManager.TrackingOrigin.Stage;
            if (origin != currentOrigin) return; // One system change can describe several reference spaces.
            if (!poseInPreviousSpace.HasValue)
            {
                // A runtime may announce its FIRST reference space without a
                // previous pose. There is no established physical alignment yet.
                if (!_hasTrackedSample && _changes.Count == 0) return;
                RequireRecovery("reference space relation unavailable");
                return;
            }
            var delta = poseInPreviousSpace.Value;
            var q = delta.orientation;
            if (!Finite(delta.position.x) || !Finite(delta.position.y) || !Finite(delta.position.z) ||
                !Finite(q.x) || !Finite(q.y) || !Finite(q.z) || !Finite(q.w) ||
                q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w < .0001f)
            {
                RequireRecovery("invalid reference space relation");
                return;
            }
            if (!_timing.TryGetChangeTime(origin, out var time) || !Finite(time) || time <= 0)
            {
                if (!_startupAligned || !_hasTrackedSample)
                {
                    if (!_timing.TryGetSampleTime(out time) || !Finite(time) || time <= 0)
                        time = Time.realtimeSinceStartupAsDouble;
                }
                else
                {
                    RequireRecovery("reference space change time unavailable");
                    return;
                }
            }
            delta.orientation = q.normalized;
            foreach (var change in _changes)
            {
                if (change.Time != time) continue;
                if (Vector3.Distance(change.Delta.position, delta.position) < .00001f &&
                    Quaternion.Angle(change.Delta.orientation, delta.orientation) < .001f) return;
                RequireRecovery("conflicting reference space events");
                return;
            }
            // Preserve a small timeline: Update and before-render can sample either
            // side of a boundary. Re-evaluate from the baseline, never double-compose.
            if (_changes.Count >= 128) { RequireRecovery("reference space event limit"); return; }
            _changes.Add(new Change(time, delta));
            _changes.Sort((a, b) => a.Time.CompareTo(b.Time));
            HasPendingChange = true;
            Debug.Log($"[VRTracking] change queued origin={origin} time={time:R}");
            StateChanged?.Invoke();
        }

        void OnSamplesUpdated(OVRCameraRig rig)
        {
            if (_disposed || RecoveryRequired || !rig || !rig.trackingSpace) return;
            var wasReady = CanInteract;
            var wasTracked = _tracked;
            _tracked = _timing.TryGetSampleTime(out var time) && Finite(time) && time > 0;
            _hasTrackedSample |= _tracked;
            if (_tracked && _changes.Count > 0)
            {
                var position = _initialSpace.position;
                var rotation = _initialSpace.rotation;
                HasPendingChange = false;
                foreach (var change in _changes)
                {
                    if (time < change.Time) { HasPendingChange = true; break; }
                    position += rotation * Vector3.Scale(rig.trackingSpace.lossyScale, change.Delta.position);
                    rotation *= change.Delta.orientation;
                }
                if ((rig.trackingSpace.position - position).sqrMagnitude > 1e-8f ||
                    Quaternion.Angle(rig.trackingSpace.rotation, rotation) > 0.001f)
                {
                    rig.trackingSpace.SetPositionAndRotation(position, rotation);
                }
            }
            if (_tracked && !_startupAligned && !HasPendingChange) AlignStartup(rig);
            if (wasReady != CanInteract || wasTracked != _tracked) StateChanged?.Invoke();
        }

        void AlignStartup(OVRCameraRig rig)
        {
            var head = rig.centerEyeAnchor;
            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < .0001f) forward = Vector3.Cross(head.right, Vector3.up);
            if (!Finite(head.position.x) || !Finite(head.position.z) || forward.sqrMagnitude < .0001f) return;
            var spawn = _startupPose.Value;
            var yaw = Quaternion.AngleAxis(Vector3.SignedAngle(forward, spawn.rotation * Vector3.forward, Vector3.up), Vector3.up);
            var before = head.position;
            var after = new Vector3(spawn.position.x, before.y, spawn.position.z);
            // Calibrate head and hands together once, before input/arrival opens.
            // Preserve standing height, pitch and roll, and fold this transform
            // into the existing origin-event baseline so later resets retain it.
            var space = rig.trackingSpace;
            _initialSpace = new Pose(after + yaw * (_initialSpace.position - before), yaw * _initialSpace.rotation);
            space.SetPositionAndRotation(after + yaw * (space.position - before), yaw * space.rotation);
            _startupAligned = true;
        }

        void RequireRecovery(string reason)
        {
            RecoveryRequired = true;
            Debug.LogWarning("[VRTracking] alignment unavailable; restart required: " + reason);
            StateChanged?.Invoke();
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            OVRManager.TrackingOriginChangePending -= OnOriginChange;
            if (_rig) _rig.UpdatedAnchors -= OnSamplesUpdated;
            StateChanged = null;
        }

        readonly struct Change
        {
            public readonly double Time;
            public readonly OVRPose Delta;
            public Change(double time, OVRPose delta) { Time = time; Delta = delta; }
        }
    }

    internal interface ITrackingOriginTiming
    {
        bool TryGetChangeTime(OVRManager.TrackingOrigin origin, out double time);
        bool TryGetSampleTime(out double time);
    }

    // Meta Core SDK 205 drops ChangeTime from its public event. Read the exact
    // buffer while that synchronous event is being dispatched, without polling
    // (and consuming) SDK events or modifying PackageCache. link.xml preserves the
    // two reflected SDK members under IL2CPP. A layout mismatch fails closed.
    internal sealed class MetaTrackingOriginTiming : ITrackingOriginTiming
    {
        static readonly FieldInfo Buffer = typeof(OVRManager).GetField("eventDataBuffer", BindingFlags.Static | BindingFlags.NonPublic);
        static readonly Type EventLayout = typeof(OVRManager).Assembly.GetType("OVRDeserialize+EventDataReferenceSpaceChangePending");
        internal static bool LayoutSupported
        {
            get
            {
                try
                {
                    if (Buffer == null || EventLayout == null ||
                        EventLayout.GetField("ReferenceSpaceType") == null || EventLayout.GetField("ChangeTime") == null) return false;
                    return Marshal.OffsetOf(EventLayout, "ReferenceSpaceType").ToInt32() == 4 &&
                        Marshal.OffsetOf(EventLayout, "ChangeTime").ToInt32() == 8;
                }
                catch
                {
                    return Buffer != null;
                }
            }
        }

        public bool TryGetChangeTime(OVRManager.TrackingOrigin origin, out double time)
        {
            time = 0;
            if (!LayoutSupported || !(Buffer.GetValue(null) is OVRPlugin.EventDataBuffer buffer) ||
                buffer.EventType != OVRPlugin.EventType.ReferenceSpaceChangePending ||
                buffer.EventData == null || buffer.EventData.Length < 12 ||
                BitConverter.ToInt32(buffer.EventData, 0) != (int)origin) return false;
            time = BitConverter.ToDouble(buffer.EventData, 4);
            return !double.IsNaN(time) && !double.IsInfinity(time) && time > 0;
        }

        public bool TryGetSampleTime(out double time)
        {
            time = 0;
            if (!OVRManager.isHmdPresent) return false;
            if (OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter) &&
                OVRPlugin.GetNodeOrientationTracked(OVRPlugin.Node.EyeCenter))
            {
                time = OVRPlugin.GetNodePoseStateRaw(OVRPlugin.Node.EyeCenter, OVRPlugin.Step.Render).Time;
            }
            if (time <= 0 || double.IsNaN(time) || double.IsInfinity(time))
            {
                time = Time.realtimeSinceStartupAsDouble;
            }
            return time > 0 && !double.IsNaN(time) && !double.IsInfinity(time);
        }
    }
}
