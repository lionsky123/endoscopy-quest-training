using System;
using System.Collections.Generic;

namespace BotanicalGardenQR.MapNavigation.Contracts
{
    public static class MapDefinitionValidation
    {
        public static void Validate(MapDefinition d)
        {
            if (d == null || string.IsNullOrWhiteSpace(d.mapId) || !d.start.IsFinite || !Positive(d.scale) || !Positive(d.speed) || !Positive(d.waitDistance) || !Positive(d.resumeDistance) || d.resumeDistance >= d.waitDistance || !Positive(d.departureRadius) || d.points == null || d.points.Length == 0 || d.routes == null || d.routes.Length != d.points.Length)
                throw new ArgumentException("Invalid map definition or motion limits.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < d.points.Length; i++)
            {
                var p = d.points[i];
                var route = d.routes[i];
                if (p == null || string.IsNullOrWhiteSpace(p.id) || !ids.Add(p.id) || !p.position.IsFinite || route == null || route.to != p.id || route.from != (i == 0 ? "Start" : d.points[i - 1].id) || route.samples == null || route.samples.Length < 2)
                    throw new ArgumentException("Missing, duplicate or disconnected map point/route.");
                var start = i == 0 ? d.start : d.points[i - 1].position;
                if (MapPosition.Distance(start, route.samples[0]) > 0.001f || MapPosition.Distance(p.position, route.samples[route.samples.Length - 1]) > 0.001f)
                    throw new ArgumentException("Map route endpoints disagree with points.");
                foreach (var sample in route.samples)
                    if (!sample.IsFinite)
                        throw new ArgumentException("Non-finite map sample.");
                var total = 0f;
                for (int n = 1; n < route.samples.Length; n++)
                    total += MapPosition.Distance(route.samples[n - 1], route.samples[n]);
                if (total < 0.01f)
                    throw new ArgumentException("Degenerate map route.");
            }
        }

        static bool Positive(float v) => v > 0 && !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
