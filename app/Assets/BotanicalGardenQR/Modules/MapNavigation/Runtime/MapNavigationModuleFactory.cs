using System;
using System.Collections.Generic;
using BotanicalGardenQR.MapNavigation.Contracts;

namespace BotanicalGardenQR.MapNavigation.Runtime
{
    public static class MapNavigationModuleFactory
    {
        public static IMapNavigation Create(MapDefinition definition, IMapMotionSink motion, MapFrame? fixedFrame = null)
        {
            try
            {
                return new MapNavigationController(definition, motion, fixedFrame);
            }
            catch (ArgumentException)
            {
                return new UnavailableMapNavigation();
            }
        }

        sealed class UnavailableMapNavigation : IMapNavigation
        {
            public MapNavigationState State => new MapNavigationState(0, MapNavigationPhase.Unavailable, null, 0, 0);
            public bool HasFrame => false;
            public MapFrame Frame => default;
            public string FirstPointId => null;
            public string NextPointId => null;
            public string PointForTarget => null;
            public IReadOnlyList<MapPosition> CurrentWorldPath => Array.Empty<MapPosition>();

            public event Action Changed
            {
                add
                {
                }

                remove
                {
                }
            }

            public bool TryInitialize(MapPosition viewer, float yawDegrees, float floorHeight, float deltaSeconds) => false;
            public bool Begin(string pointId) => false;
            public void Tick(MapPosition viewer, bool tracked, bool paused, float deltaSeconds)
            {
            }

            public void Cancel()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
