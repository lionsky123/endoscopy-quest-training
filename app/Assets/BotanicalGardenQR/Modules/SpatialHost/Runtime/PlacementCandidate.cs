using UnityEngine;

namespace BotanicalGardenQR.SpatialHost.Runtime
{
    internal readonly struct PlacementCandidate
    {
        PlacementCandidate(Transform parent, Vector3 position, Quaternion rotation, float scale, bool localSpace)
        {
            Parent = parent;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            LocalSpace = localSpace;
        }

        public Transform Parent { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float Scale { get; }
        public bool LocalSpace { get; }

        public static PlacementCandidate World(Vector3 position, Quaternion rotation, float scale)
            => new PlacementCandidate(null, position, rotation, scale, false);

        public static PlacementCandidate RelativeTo(
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            float scale)
            => new PlacementCandidate(parent, localPosition, localRotation, scale, true);
    }
}
