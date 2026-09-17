using BotanicalGardenQR.SpatialHost.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.SpatialHost.Runtime
{
    internal static class EnvironmentPlacement
    {
        public static bool TryCreateFallback(
            Transform viewer,
            Transform currentRoot,
            bool hasCommittedSession,
            DisplayProfile profile,
            out PlacementCandidate candidate)
        {
            switch (profile.EnvironmentFallback)
            {
                case EnvironmentFallback.ViewerFront:
                    candidate = ViewerFrontPlacement.Create(viewer, profile);
                    return true;

                case EnvironmentFallback.KeepLastPose when hasCommittedSession:
                    candidate = PlacementCandidate.World(
                        currentRoot.position,
                        currentRoot.rotation,
                        profile.Scale);
                    return true;

                default:
                    candidate = default;
                    return false;
            }
        }
    }
}
