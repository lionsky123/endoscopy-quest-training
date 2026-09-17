using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.SpatialHost.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.SpatialHost.Runtime
{
    internal static class WorldFixedPlacement
    {
        public static PlacementCandidate Create(
            Transform viewer,
            DisplayProfile profile,
            SpatialEvidence? evidence)
        {
            if (!evidence.HasValue || !PlacementOrientation.IsUsable(evidence.Value))
                return ViewerFrontPlacement.Create(viewer, profile);

            var position = evidence.Value.Position + (Vector3.up * profile.Height);
            var rotation = PlacementOrientation.Resolve(profile, position, viewer, evidence);
            return PlacementCandidate.World(position, rotation, profile.Scale);
        }
    }
}
