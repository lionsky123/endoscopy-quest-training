using System;
using System.Collections.Generic;
using BotanicalGardenQR.MapNavigation.Contracts;

namespace BotanicalGardenQR.MapNavigation.Runtime
{
    public sealed class MapNavigationController : IMapNavigation, IMapNavigationRecovery
    {
        readonly MapDefinition _definition;
        readonly IMapMotionSink _motion;
        MapRouteGeometry _route;
        MapNavigationState _state;
        MapPosition _candidate, _lastViewer;
        float _candidateYaw, _stableTime, _entryProgress;
        int _index = -1;
        long _request;
        bool _disposed, _hasViewer, _waiting, _entryChosen, _motionAccepted;
        public MapNavigationController(MapDefinition definition, IMapMotionSink motion, MapFrame? fixedFrame = null)
        {
            MapDefinitionValidation.Validate(definition);
            _definition = definition.Snapshot();
            _motion = motion ?? throw new ArgumentNullException(nameof(motion));
            if (fixedFrame.HasValue)
            {
                var frame = fixedFrame.Value;
                if (!frame.Origin.IsFinite || float.IsNaN(frame.YawDegrees) || float.IsInfinity(frame.YawDegrees) ||
                    frame.Scale != _definition.scale)
                    throw new ArgumentException("Invalid fixed room frame.");
                Frame = frame;
                HasFrame = true;
                Publish(MapNavigationPhase.Ready, 0);
            }
        }

        public MapNavigationState State => _state;
        public bool HasFrame { get; private set; }
        public MapFrame Frame { get; private set; }
        public string NextPointId => _index + 1 < _definition.points.Length ? _definition.points[_index + 1].id : null;
        public string FirstPointId => _definition.points[0].id;
        public string PointForTarget => _index < 0 ? null : _definition.points[_index].id;
        public IReadOnlyList<MapPosition> CurrentWorldPath { get; private set; } = Array.Empty<MapPosition>();

        public event Action Changed;
        public bool TryInitialize(MapPosition viewer, float yawDegrees, float floorHeight, float deltaSeconds)
        {
            if (_disposed)
                return false;
            if (HasFrame)
                return true;
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                return false;
            if (!viewer.IsFinite || float.IsNaN(yawDegrees) || float.IsInfinity(yawDegrees) || float.IsNaN(floorHeight) || float.IsInfinity(floorHeight))
                return false;
            var flat = new MapPosition(viewer.x, 0, viewer.z);
            if (MapPosition.Distance(flat, _candidate) > 0.06f || Math.Abs(Angle(yawDegrees - _candidateYaw)) > 3)
            {
                _candidate = flat;
                _candidateYaw = yawDegrees;
                _stableTime = 0;
            }

            _stableTime += Math.Max(0, Math.Min(deltaSeconds, 0.1f));
            if (_stableTime < 0.2f)
                return false;
            var zero = new MapFrame(new MapPosition(0, 0, 0), yawDegrees, _definition.scale).Transform(_definition.start);
            Frame = new MapFrame(new MapPosition(viewer.x - zero.x, floorHeight - zero.y, viewer.z - zero.z), yawDegrees, _definition.scale);
            HasFrame = true;
            Publish(MapNavigationPhase.Ready, 0);
            return true;
        }

        public bool Begin(string pointId)
        {
            if (_disposed || !HasFrame)
                return false;
            var index = Array.FindIndex(_definition.points, p => p.id == pointId);
            if (index < 0)
                return false;
            _index = index;
            _request++;
            var local = _definition.routes[index].samples;
            var worldPath = new MapPosition[local.Length];
            for (int i = 0; i < local.Length; i++)
                worldPath[i] = Frame.Transform(local[i]);
            CurrentWorldPath = Array.AsReadOnly(worldPath);
            _route = new MapRouteGeometry(CurrentWorldPath);

            _hasViewer = false;
            _waiting = false;
            _entryChosen = false;
            _motionAccepted = false;
            if (_motion.TryGetPosition(out var initial) && initial.IsFinite)
                _motion.Apply(_request, initial, default, false);
            _motion.Hold(_request);
            Publish(MapNavigationPhase.WaitingForVisitor, 0);
            return true;
        }

        public void Tick(MapPosition viewer, bool tracked, bool paused, float deltaSeconds)
        {
            if (!HasFrame && !tracked)
                _stableTime = 0;
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0)
                return;
            if (_disposed || _route == null || _state.Phase == MapNavigationPhase.Unavailable)
                return;
            if (_state.Phase == MapNavigationPhase.Arrived)
            {
                // Arrival stays final, but temporary cue restoration still obeys application/tracking pauses.
                if (!tracked || !viewer.IsFinite || paused)
                    _motion.Hold(_request);
                else
                    _motion.Apply(_request, _route.Sample(_route.Length), default, false);
                return;
            }
            var dt = Math.Max(0, Math.Min(deltaSeconds, 0.1f));
            var s = _state.Progress;
            if (!tracked || !viewer.IsFinite)
            {

                Hold(MapNavigationPhase.Paused, s);
                return;
            }

            viewer.y = Frame.Origin.y;
            if (_hasViewer && MapPosition.Distance(viewer, _lastViewer) > 2.5f)
            {
                _motion.Release(_request);
                Publish(MapNavigationPhase.Unavailable, s);
                return;
            }

            _lastViewer = viewer;
            _hasViewer = true;
            if (paused)
            {

                Hold(MapNavigationPhase.Paused, s);
                return;
            }

            // The authored curve constrains the Fairy, not the visitor's footsteps.
            var fairyPosition = _route.Sample(s);
            if (_motion.TryGetPosition(out var actual) && actual.IsFinite)
                fairyPosition = new MapPosition(actual.x, Frame.Origin.y, actual.z);
            var gap = MapPosition.Distance(viewer, fairyPosition);
            if (gap >= _definition.waitDistance)
                _waiting = true;
            else if (gap <= _definition.resumeDistance)
                _waiting = false;
            if (_waiting)
            {

                Hold(MapNavigationPhase.WaitingForVisitor, s);
                return;
            }

            if (!_motionAccepted)
            {
                if (!_entryChosen)
                {
                    _entryProgress = s;
                    if (_motion.TryGetPosition(out var entry) && entry.IsFinite)
                    {
                        entry.y = Frame.Origin.y;
                        var limit = _route.ForwardEntryLimit(s, s + _definition.departureRadius);
                        var projected = _route.Project(entry, s, limit, out var offset);
                        if (offset <= _definition.fairyJoinRadius) _entryProgress = projected;
                    }
                    _entryChosen = true;
                }
                _motionAccepted = _motion.Apply(_request, _route.Sample(_entryProgress), default, false);
                if (!_motionAccepted)
                {

                    Publish(MapNavigationPhase.Paused, s);
                    return;
                }
            }
            s = Math.Max(s, _entryProgress);
            var next = Math.Min(_route.Length, s + _definition.speed * dt);
            var p = _route.Sample(next);
            var ahead = _route.Sample(Math.Min(_route.Length, next + 0.05f));
            var forward = new MapPosition(ahead.x - p.x, ahead.y - p.y, ahead.z - p.z);
            if (!_motion.Apply(_request, p, forward, next > s))
            {

                Publish(MapNavigationPhase.Paused, s);
                return;
            }

            if (next >= _route.Length - 0.001f)
            {
                _motion.Hold(_request);
                Publish(MapNavigationPhase.Arrived, next);
                return;
            }
            Publish(next <= s && next < _route.Length ? MapNavigationPhase.WaitingForVisitor : MapNavigationPhase.Moving, next);
        }

        void Hold(MapNavigationPhase phase, float s)
        {
            if (phase == MapNavigationPhase.Paused)
            {
                if (_state.Phase != MapNavigationPhase.Paused) _entryChosen = false;
                _motionAccepted = false;
            }
            _motion.Hold(_request);
            Publish(phase, s);
        }

        public bool TryGetRecoveryPosition(MapPosition viewer, out MapPosition position)
        {
            position = default;
            if (_disposed || !HasFrame || _route == null || !viewer.IsFinite ||
                _state.Phase == MapNavigationPhase.Unavailable) return false;
            viewer.y = Frame.Origin.y;
            var s = _state.Progress;
            var limit = _route.ForwardEntryLimit(s, s + _definition.departureRadius);
            var projected = _route.Project(viewer, s, limit, out var distance);
            if (distance > _definition.departureRadius) return false;
            position = _route.Sample(projected);
            return true;
        }

        public void ReacquireMotion()
        {
            if (_disposed || _route == null) return;
            _entryChosen = false; _motionAccepted = false;
        }

        public void Cancel()
        {
            if (_disposed)
                return;
            _motion.Release(_request);
            _route = null;
            CurrentWorldPath = Array.Empty<MapPosition>();

            Publish(MapNavigationPhase.Cancelled, _state.Progress);
        }

        void Publish(MapNavigationPhase phase, float progress)
        {
            _state = new MapNavigationState(_request, phase, _index < 0 ? null : _definition.points[_index].id, progress, _route?.Length ?? 0);
            Changed?.Invoke();
        }

        static float Angle(float angle)
        {
            while (angle > 180)
                angle -= 360;
            while (angle < -180)
                angle += 360;
            return angle;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            Cancel();
            _disposed = true;
            Changed = null;
        }
    }
}
