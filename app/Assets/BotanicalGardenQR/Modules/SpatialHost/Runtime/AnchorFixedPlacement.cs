using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.SpatialHost.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.SpatialHost.Runtime
{
    internal static class AnchorFixedPlacement
    {
        public static PlacementCandidate Create(
            Transform viewer,
            DisplayProfile profile,
            SpatialEvidence evidence)
        {
            var position = evidence.Position + (Vector3.up * profile.Height);
            var rotation = PlacementOrientation.Resolve(profile, position, viewer, evidence);
            return PlacementCandidate.World(position, rotation, profile.Scale);
        }
    }
}
