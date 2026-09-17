using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.SpatialHost.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.SpatialHost.Runtime
{
    internal static class HeadFollowPlacement
    {
        public static PlacementCandidate Create(
            Transform viewer,
            DisplayProfile profile,
            SpatialEvidence? evidence)
        {
            var localPosition = new Vector3(0f, profile.Height, profile.Distance);
            var worldPosition = viewer.TransformPoint(localPosition);
            var worldRotation = PlacementOrientation.Resolve(profile, worldPosition, viewer, evidence);
            var localRotation = Quaternion.Inverse(viewer.rotation) * worldRotation;
            return PlacementCandidate.RelativeTo(viewer, localPosition, localRotation, profile.Scale);
        }
    }
}
