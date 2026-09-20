using System;
using System.Collections.Generic;

namespace BotanicalGardenQR.MapNavigation.Contracts
{
    [Serializable]
    public struct MapPosition
    {
        public float x, y, z;
        public MapPosition(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static MapPosition Lerp(MapPosition a, MapPosition b, float t) => new MapPosition(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        public static float Distance(MapPosition a, MapPosition b) => (float)Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) + (a.z - b.z) * (a.z - b.z));
        public bool IsFinite => Finite(x) && Finite(y) && Finite(z);

        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }

    [Serializable]
    public sealed class MapPoint
    {
        public string id;
        public MapPosition position;
    }

    [Serializable]
    public sealed class MapRoute
    {
        public string from;
        public string to;
        public MapPosition[] samples;
    }

    [Serializable]
    public sealed class MapDefinition
    {
        public string mapId, sourceDigest, modelDigest;
        public string roomResource;
        public float scale = 2f;
        public MapPosition start;
        public MapPoint[] points;
        public MapRoute[] routes;
        public float speed = 0.7f, waitDistance = 2.5f, resumeDistance = 2f;
        public float departureRadius = 1.5f;
        // Horizontal distance a displaced dialogue companion can walk back to the route.
        // Kept separate from the forward entry window and visitor arrival radius.
        public float fairyJoinRadius = 1.5f;
        public MapDefinition Snapshot()
        {
            var copy = (MapDefinition)MemberwiseClone();
            copy.points = Array.ConvertAll(points, p => new MapPoint { id = p.id, position = p.position });
            copy.routes = Array.ConvertAll(routes, r => new MapRoute { from = r.from, to = r.to, samples = (MapPosition[])r.samples.Clone() });
            return copy;
        }
    }

    public readonly struct MapFrame
    {
        public MapFrame(MapPosition origin, float yawDegrees, float scale)
        {
            Origin = origin;
            YawDegrees = yawDegrees;
            Scale = scale;
        }

        public MapPosition Origin { get; }
        public float YawDegrees { get; }
        public float Scale { get; }

        public MapPosition Transform(MapPosition p)
        {
            var a = YawDegrees * Math.PI / 180;
            var c = (float)Math.Cos(a);
            var s = (float)Math.Sin(a);
            return new MapPosition(Origin.x + Scale * (c * p.x + s * p.z), Origin.y + Scale * p.y, Origin.z + Scale * (-s * p.x + c * p.z));
        }
    }

    public enum MapNavigationPhase
    {
        Initializing,
        Ready,
        Moving,
        WaitingForVisitor,
        Paused,
        Arrived,
        Cancelled,
        Unavailable
    }

    public readonly struct MapNavigationState
    {
        public MapNavigationState(long requestId, MapNavigationPhase phase, string target, float progress, float length)
        {
            RequestId = requestId;
            Phase = phase;
            TargetPointId = target;
            Progress = progress;
            Length = length;
        }

        public long RequestId { get; }
        public MapNavigationPhase Phase { get; }
        public string TargetPointId { get; }
        public float Progress { get; }
        public float Length { get; }
    }

    /// <summary>The motion consumer acknowledges actual application; it never receives route/content identities.</summary>
    public interface IMapMotionSink
    {
        bool TryGetPosition(out MapPosition position);
        bool Apply(long requestId, MapPosition position, MapPosition forward, bool moving);
        void Hold(long requestId);
        void Release(long requestId);
    }

    public interface IMapNavigationRecovery
    {
        bool TryGetRecoveryPosition(MapPosition viewer, out MapPosition position);
        void ReacquireMotion();
    }

    public interface IMapNavigation : IDisposable
    {
        MapNavigationState State { get; }

        bool HasFrame { get; }

        MapFrame Frame { get; }

        string FirstPointId { get; }
        string NextPointId { get; }

        event Action Changed;
        bool TryInitialize(MapPosition viewer, float yawDegrees, float floorHeight, float deltaSeconds);
        bool Begin(string pointId);
        void Tick(MapPosition viewer, bool tracked, bool paused, float deltaSeconds);
        void Cancel();
        string PointForTarget { get; }

        IReadOnlyList<MapPosition> CurrentWorldPath { get; }
    }
}
