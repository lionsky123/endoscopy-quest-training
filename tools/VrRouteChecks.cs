using System;
using System.IO;
using System.Text.Json;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.MapNavigation.Runtime;

static class VrRouteChecks
{
    static void Require(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    static void Main(string[] args)
    {
        var d = JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(args[0]), new JsonSerializerOptions { IncludeFields = true });
        MapDefinitionValidation.Validate(d);
        Require(d.points.Length == 6 && d.scale == 1 && d.roomResource == "EndoscopyRoom", "Room configuration");
        var rotation = new MapFrame(default, 90, 1);
        var zero = rotation.Transform(d.start);
        var frame = new MapFrame(new MapPosition(-zero.x, 0, -zero.z), 90, 1);
        Require(MapPosition.Distance(frame.Transform(d.start), default) < .001f, "Start must be at physical origin");
        var motion = new Motion { Position = frame.Transform(d.start) };
        using var n = new MapNavigationController(d, motion, frame);
        Require(n.HasFrame && n.State.Phase == MapNavigationPhase.Ready, "VR must not wait for MR calibration");
        n.TryInitialize(new MapPosition(8, 1.7f, 2), -133, 1, .1f);
        Require(n.Frame.YawDegrees == frame.YawDegrees && n.Frame.Origin.x == frame.Origin.x, "Head turns must not move room frame");
        for (int leg = 0; leg < d.points.Length; leg++)
        {
            Require(n.Begin(d.points[leg].id), "Leg start " + leg);
            var route = new MapRouteGeometry(n.CurrentWorldPath);
            var target = frame.Transform(d.points[leg].position);
            Require(MapPosition.Distance(n.CurrentWorldPath[n.CurrentWorldPath.Count-1], target) < .001f, "Station alignment");
            for (int i = 0; i < 20000 && n.State.Phase != MapNavigationPhase.Arrived; i++)
                n.Tick(route.Sample(n.State.Progress), true, false, .02f);
            Require(n.State.Phase == MapNavigationPhase.Arrived, "Six-station walk must arrive: " + leg);
            Require(MapPosition.Distance(motion.Position, target) < .05f, "Guide final position");
        }
        Require(n.NextPointId == null, "Exactly six stops");
        using var pause = new MapNavigationController(d, new Motion(), frame);
        pause.Begin("P01");
        pause.Tick(default, false, false, .1f);
        Require(pause.State.Phase == MapNavigationPhase.Paused && pause.State.Progress == 0, "Tracking loss pauses route");
        var rejected = false;
        try { new MapNavigationController(d, new Motion(), new MapFrame(default, 90, 2)); }
        catch (ArgumentException) { rejected = true; }
        Require(rejected, "Mismatched map/room scale must fail");
        Console.WriteLine("PASS: fixed VR frame, exact spawn/6 station alignment, all 6 real route legs, tracking pause, scale rejection.");
    }
    sealed class Motion : IMapMotionSink
    {
        public MapPosition Position;
        public bool TryGetPosition(out MapPosition p) { p=Position; return true; }
        public bool Apply(long id, MapPosition p, MapPosition forward, bool moving) { Position=p; return true; }
        public void Hold(long id) { }
        public void Release(long id) { }
    }
}
