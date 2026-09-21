using System;
using System.Collections.Generic;
using BotanicalGardenQR.Fairy.Contracts;
using BotanicalGardenQR.MapNavigation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    // A single immutable network, derived from the same published geometry/frame
    // as the room. Dynamic recovery uses shortest paths on this network, never
    // a direct line to a head-relative target on the other side of a wall.
    internal sealed class VirtualRoomGuidePath : IFairyWalkSpace
    {
        const float BodyRadius = .25f;
        const float RailTolerance = .06f;
        readonly Vector3 _origin;
        readonly Quaternion _rotation, _inverse;
        readonly float _scale;
        readonly List<Vector3> _nodes = new List<Vector3>();
        readonly List<List<int>> _links = new List<List<int>>();
        readonly List<(int a, int b)> _segments = new List<(int, int)>();
        readonly List<Bounds> _obstacles = new List<Bounds>();
        readonly List<Vector3> _plan = new List<Vector3>();
        readonly Vector3[] _entrance;
        readonly float _landingDistance;
        Vector3 _goal;
        int _next;
        public Vector3 StartPosition => ArrivalPoint(1);

        public VirtualRoomGuidePath(MapDefinition definition, MapFrame frame, MeshFilter[] geometry, Bounds[] publishedObstacles = null)
        {
            _origin = V(frame.Origin); _rotation = Quaternion.Euler(0, frame.YawDegrees, 0);
            _inverse = Quaternion.Inverse(_rotation); _scale = frame.Scale;
            // Stay inside the resume radius so the first departure does not
            // immediately wait for a visitor who is still at the fixed spawn.
            _landingDistance = Mathf.Min(.8f, definition.resumeDistance * .9f);
            var boundsList = new List<Bounds>();
            if(publishedObstacles!=null)boundsList.AddRange(publishedObstacles);
            else foreach(var mesh in geometry)
            {
                if(!mesh || !mesh.sharedMesh)continue;
                var local=mesh.sharedMesh.bounds;
                var bounds=new Bounds(Local(mesh.transform.TransformPoint(local.min)),Vector3.zero);
                for(int corner=0;corner<8;corner++)bounds.Encapsulate(Local(mesh.transform.TransformPoint(new Vector3(
                    (corner&1)==0?local.min.x:local.max.x,(corner&2)==0?local.min.y:local.max.y,(corner&4)==0?local.min.z:local.max.z))));
                boundsList.Add(bounds);
            }
            foreach(var sourceBounds in boundsList)
            {
                var bounds=sourceBounds;
                if (bounds.max.y <= .15f || bounds.min.y >= 1.8f) continue;
                bounds.Expand(new Vector3(BodyRadius * 2, 0, BodyRadius * 2));
                _obstacles.Add(bounds);
            }
            foreach (var route in definition.routes)
            {
                int previous = -1;
                foreach (var sample in route.samples)
                {
                    var point = V(sample);
                    int node = _nodes.FindIndex(p => (p - point).sqrMagnitude < .000001f);
                    if (node < 0) { node = _nodes.Count; _nodes.Add(point); _links.Add(new List<int>()); }
                    if (previous >= 0 && previous != node)
                    {
                        if (!Clear(_nodes[previous], point)) throw new InvalidOperationException("Published fairy route intersects the room geometry: " + route.to);
                        Link(previous, node); _segments.Add((previous, node));
                    }
                    previous = node;
                }
            }
            // Independently sampled overlapping legs may not share exact vertices.
            // Join only neighbours inside the authored rail and the model clearance.
            for (int a = 0; a < _nodes.Count; a++)
                for (int b = a + 1; b < _nodes.Count; b++)
                    if ((_nodes[a] - _nodes[b]).sqrMagnitude <= .055f * .055f && Clear(_nodes[a], _nodes[b])) Link(a, b);
            _entrance = Array.ConvertAll(definition.routes[0].samples, V);
            if (PathLength(_entrance) < _landingDistance + 1.5f) throw new InvalidOperationException("The room entrance is too short for its authored approach path.");
        }

        void Link(int a, int b)
        {
            if (!_links[a].Contains(b)) _links[a].Add(b);
            if (!_links[b].Contains(a)) _links[b].Add(a);
        }
        public Vector3 ArrivalPoint(float progress) => World(Sample(_entrance, Mathf.Lerp(_landingDistance + 1.5f, _landingDistance, Mathf.Clamp01(progress))));
        public Vector3 Project(Vector3 desired) => World(Closest(Local(desired), out _));

        public bool CanStep(Vector3 from, Vector3 to)
        {
            var a = Local(from); var b = Local(to);
            if (Mathf.Abs(a.y) > .02f || Mathf.Abs(b.y) > .02f || !Clear(a, b)) return false;
            // Include the swept segment, so even two valid endpoints cannot cut a bend.
            int count = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .04f));
            for (int i = 0; i <= count; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)count);
                if ((Closest(p, out _) - p).sqrMagnitude > RailTolerance * RailTolerance) return false;
            }
            return true;
        }

        public Vector3 NextWaypoint(Vector3 current, Vector3 desired)
        {
            var start = Local(current);
            var goal = Closest(Local(desired), out var goalEdge);
            if (_plan.Count == 0 || (goal - _goal).sqrMagnitude > .0001f)
                Plan(start, goal, goalEdge);
            while (_next < _plan.Count && Vector3.Distance(start, _plan[_next]) < .001f) _next++;
            if (_next >= _plan.Count) return World(goal);
            var waypoint = World(_plan[_next]);
            if (!CanStep(current, waypoint))
            {
                Plan(start, goal, goalEdge);
                while (_next < _plan.Count && Vector3.Distance(start, _plan[_next]) < .001f) _next++;
                waypoint = _next < _plan.Count ? World(_plan[_next]) : current;
            }
            return waypoint;
        }

        void Plan(Vector3 current, Vector3 goal, int goalEdge)
        {
            _plan.Clear(); _next = 0; _goal = goal;
            var start = Closest(current, out var startEdge);
            // Unexpected external displacement is not permission to walk through
            // architecture or teleport. Hold until a valid path exists.
            if (!CanStep(World(current), World(start))) { _plan.Add(current); return; }
            if (startEdge == goalEdge && Clear(start, goal)) { _plan.Add(goal); return; }
            var count = _nodes.Count;
            var distance = new float[count]; var previous = new int[count]; var visited = new bool[count];
            for (int i = 0; i < count; i++) { distance[i] = float.PositiveInfinity; previous[i] = -1; }
            var startSegment = _segments[startEdge]; var endSegment = _segments[goalEdge];
            distance[startSegment.a] = Vector3.Distance(start, _nodes[startSegment.a]);
            distance[startSegment.b] = Vector3.Distance(start, _nodes[startSegment.b]);
            for (int step = 0; step < count; step++)
            {
                int best = -1;
                for (int i = 0; i < count; i++) if (!visited[i] && (best < 0 || distance[i] < distance[best])) best = i;
                if (best < 0 || float.IsInfinity(distance[best])) break;
                visited[best] = true;
                foreach (var next in _links[best])
                {
                    var candidate = distance[best] + Vector3.Distance(_nodes[best], _nodes[next]);
                    if (candidate >= distance[next]) continue;
                    distance[next] = candidate; previous[next] = best;
                }
            }
            int end = distance[endSegment.a] + Vector3.Distance(_nodes[endSegment.a], goal) <=
                      distance[endSegment.b] + Vector3.Distance(_nodes[endSegment.b], goal) ? endSegment.a : endSegment.b;
            if (float.IsInfinity(distance[end])) { _plan.Add(current); return; }
            for (int node = end; node >= 0; node = previous[node]) _plan.Add(_nodes[node]);
            _plan.Reverse(); _plan.Insert(0, start); _plan.Add(goal);
        }

        Vector3 Closest(Vector3 point, out int edge)
        {
            point.y = 0; float best = float.PositiveInfinity; var result = _nodes[0]; edge = 0;
            for (int i = 0; i < _segments.Count; i++)
            {
                var segment = _segments[i]; var a = _nodes[segment.a]; var delta = _nodes[segment.b] - a;
                var projected = a + delta * Mathf.Clamp01(Vector3.Dot(point - a, delta) / delta.sqrMagnitude);
                var distance = (point - projected).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; result = projected; edge = i;
            }
            return result;
        }
        bool Clear(Vector3 a, Vector3 b)
        {
            foreach (var bounds in _obstacles)
            {
                var start = new Vector3(a.x, bounds.center.y, a.z);
                var end = new Vector3(b.x, bounds.center.y, b.z);
                if (bounds.Contains(start) || bounds.Contains(end)) return false;
                var delta = end - start;
                if (delta.sqrMagnitude > .00000001f && bounds.IntersectRay(new Ray(start, delta.normalized), out var hit) && hit <= delta.magnitude) return false;
            }
            return true;
        }
        Vector3 World(Vector3 p) => _origin + _rotation * p * _scale;
        Vector3 Local(Vector3 p) => _inverse * (p - _origin) / _scale;
        static Vector3 V(MapPosition p) => new Vector3(p.x, p.y, p.z);
        static float PathLength(Vector3[] points) { float length = 0; for (int i = 1; i < points.Length; i++) length += Vector3.Distance(points[i - 1], points[i]); return length; }
        static Vector3 Sample(Vector3[] points, float distance)
        {
            for (int i = 1; i < points.Length; i++)
            {
                var length = Vector3.Distance(points[i - 1], points[i]);
                if (distance <= length) return Vector3.Lerp(points[i - 1], points[i], distance / length);
                distance -= length;
            }
            return points[points.Length - 1];
        }
    }
}
