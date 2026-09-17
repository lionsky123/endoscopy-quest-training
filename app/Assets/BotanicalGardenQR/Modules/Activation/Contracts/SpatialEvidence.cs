using System;
using UnityEngine;

namespace BotanicalGardenQR.Activation.Contracts
{
    public readonly struct SpatialEvidence : IEquatable<SpatialEvidence>
    {
        public SpatialEvidence(Vector3 position, Quaternion rotation, bool poseIsValid)
        {
            if (!IsFinite(position) || !IsFinite(rotation))
                throw new ArgumentException("SpatialEvidence pose must contain finite values.");
            var rotationMagnitude =
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w;
            if (poseIsValid && rotationMagnitude <= 0.000001f)
                throw new ArgumentException("A valid SpatialEvidence pose requires a usable rotation.", nameof(rotation));

            Position = position;
            Rotation = rotation;
            PoseIsValid = poseIsValid;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public bool PoseIsValid { get; }

        public bool Equals(SpatialEvidence other)
            => Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && PoseIsValid == other.PoseIsValid;

        public override bool Equals(object obj) => obj is SpatialEvidence other && Equals(other);
        public override int GetHashCode() => ((Position.GetHashCode() * 397) ^ Rotation.GetHashCode()) * 397 ^ PoseIsValid.GetHashCode();

        static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        static bool IsFinite(Quaternion value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
