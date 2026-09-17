using System;
using System.Collections.Generic;
using BotanicalGardenQR.MapNavigation.Contracts;

namespace BotanicalGardenQR.MapNavigation.Runtime
{
    public sealed class MapRouteGeometry
    {
        readonly MapPosition[] _points;
        readonly float[] _distances;
        public MapRouteGeometry(IReadOnlyList<MapPosition> points)
        {
            if (points == null || points.Count < 2)
                throw new ArgumentException("A path requires at least two positions.", nameof(points));
            _points = new MapPosition[points.Count];
            for (int i = 0; i < points.Count; i++)
                _points[i] = points[i];
            _distances = new float[points.Count];
            for (int i = 1; i < points.Count; i++)
                _distances[i] = _distances[i - 1] + MapPosition.Distance(points[i - 1], points[i]);
        }

        public float Length => _distances[_distances.Length - 1];

        public MapPosition Sample(float distance)
        {
            if (distance <= 0)
                return _points[0];
            for (int i = 1; i < _points.Length; i++)
                if (distance <= _distances[i])
                    return MapPosition.Lerp(_points[i - 1], _points[i], (distance - _distances[i - 1]) / Math.Max(0.00001f, _distances[i] - _distances[i - 1]));
            return _points[_points.Length - 1];
        }

        // Stop reacquisition at the first authored bend: no direct shortcut around a corner.
        public float ForwardEntryLimit(float minimum, float maximum)
        {
            var limit = Math.Min(Length, maximum);
            for (var i = 1; i < _points.Length - 1; i++)
            {
                if (_distances[i] <= minimum + .001f || _distances[i] >= limit) continue;
                var a = _points[i - 1]; var b = _points[i]; var c = _points[i + 1];
                var ux = b.x - a.x; var uz = b.z - a.z;
                var vx = c.x - b.x; var vz = c.z - b.z;
                var lengths = Math.Sqrt((ux * ux + uz * uz) * (vx * vx + vz * vz));
                if (lengths > .000001 && (ux * vx + uz * vz) / lengths < .8660254)
                    return _distances[i];
            }
            return limit;
        }

        public float Project(MapPosition p, float minimum, float maximum, out float offset)
        {
            var best = float.MaxValue;
            var result = minimum;
            for (int i = 1; i < _points.Length; i++)
            {
                if (_distances[i] < minimum || _distances[i - 1] > maximum)
                    continue;
                var a = _points[i - 1];
                var b = _points[i];
                var dx = b.x - a.x;
                var dy = b.y - a.y;
                var dz = b.z - a.z;
                var sq = dx * dx + dy * dy + dz * dz;
                if (sq < 0.000001f)
                    continue;
                var t = ((p.x - a.x) * dx + (p.y - a.y) * dy + (p.z - a.z) * dz) / sq;
                var length = (float)Math.Sqrt(sq);
                var s = Math.Max(minimum, Math.Min(maximum, _distances[i - 1] + Math.Max(0, Math.Min(1, t)) * length));
                var distance = MapPosition.Distance(p, MapPosition.Lerp(a, b, (s - _distances[i - 1]) / length));
                if (distance < best)
                {
                    best = distance;
                    result = s;
                }
            }

            offset = best;
            return result;
        }
    }
}
