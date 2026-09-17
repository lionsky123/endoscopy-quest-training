using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.SpatialHost.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.SpatialHost.Runtime
{
    internal static class ViewerFrontPlacement
    {
        const float SourceSurfaceClearanceMeters = 0.12f;
        const float MinimumPlaneAlignment = 0.15f;

        public static PlacementCandidate Create(
            Transform viewer,
            DisplayProfile profile,
            SpatialEvidence? evidence = null)
        {
            var origin = viewer.position + (Vector3.up * profile.Height);
            var direction = NormalizeOrForward(viewer.forward);
            var distance = ConstrainBeforeSourcePlane(origin, direction, profile.Distance, evidence);
            var position = origin + (direction * distance);
            var rotation = PlacementOrientation.Resolve(profile, position, viewer, evidence);
            return PlacementCandidate.World(position, rotation, profile.Scale);
        }

        static float ConstrainBeforeSourcePlane(
            Vector3 origin,
            Vector3 direction,
            float preferredDistance,
            SpatialEvidence? evidence)
        {
            if (!evidence.HasValue || !PlacementOrientation.IsUsable(evidence.Value))
                return preferredDistance;

            var planeNormal = evidence.Value.Rotation * Vector3.forward;
            if (!IsFinite(planeNormal) || planeNormal.sqrMagnitude < 0.000001f)
                return preferredDistance;
            planeNormal.Normalize();

            var alignment = Vector3.Dot(direction, planeNormal);
            if (Mathf.Abs(alignment) < MinimumPlaneAlignment)
                return preferredDistance;

            var hitDistance = Vector3.Dot(evidence.Value.Position - origin, planeNormal) / alignment;
            if (hitDistance <= 0f || hitDistance >= preferredDistance)
                return preferredDistance;

            var clearanceAlongRay = SourceSurfaceClearanceMeters / Mathf.Abs(alignment);
            var constrainedDistance = hitDistance - clearanceAlongRay;
            if (constrainedDistance <= 0f)
                constrainedDistance = hitDistance * 0.5f;
            return Mathf.Max(0f, constrainedDistance);
        }

        static Vector3 NormalizeOrForward(Vector3 value)
        {
            if (!IsFinite(value) || value.sqrMagnitude < 0.000001f)
                return Vector3.forward;
            return value.normalized;
        }

        static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    internal static class PlacementOrientation
    {
        public static Quaternion Resolve(
            DisplayProfile profile,
            Vector3 position,
            Transform viewer,
            SpatialEvidence? evidence)
        {
            if (profile.Orientation == HostOrientation.PreserveEvidenceRotation &&
                evidence.HasValue && evidence.Value.PoseIsValid)
                return Normalize(evidence.Value.Rotation);

            var towardViewer = viewer.position - position;
            towardViewer.y = 0f;
            if (towardViewer.sqrMagnitude < 0.000001f)
                towardViewer = -viewer.forward;
            towardViewer.y = 0f;
            // World-space Unity Canvas graphics face their local -Z side. The
            // display root is placed in front of the viewer, so its local +Z
            // must point away from the viewer for the Canvas front to face in.
            return Quaternion.LookRotation(-towardViewer.normalized, Vector3.up);
        }

        public static bool IsUsable(SpatialEvidence evidence)
        {
            var rotation = evidence.Rotation;
            var squareMagnitude =
                (rotation.x * rotation.x) +
                (rotation.y * rotation.y) +
                (rotation.z * rotation.z) +
                (rotation.w * rotation.w);
            return evidence.PoseIsValid && squareMagnitude > 0.000001f;
        }

        static Quaternion Normalize(Quaternion rotation)
        {
            var magnitude = Mathf.Sqrt(
                (rotation.x * rotation.x) +
                (rotation.y * rotation.y) +
                (rotation.z * rotation.z) +
                (rotation.w * rotation.w));
            return new Quaternion(
                rotation.x / magnitude,
                rotation.y / magnitude,
                rotation.z / magnitude,
                rotation.w / magnitude);
        }
    }
}
