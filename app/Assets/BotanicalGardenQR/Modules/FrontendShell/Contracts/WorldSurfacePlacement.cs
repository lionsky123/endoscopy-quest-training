using System;
using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    /// <summary>
    /// Shared orientation convention for world-space presentation surfaces.
    /// Canvas content faces local -Z, so the transform's +Z points away from the viewer.
    /// </summary>
    public static class WorldSurfacePlacement
    {
        public static Pose CreateViewerFrontPose(
            Transform viewer,
            float distance,
            float verticalOffset = 0f)
        {
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));
            if (!IsPositiveFinite(distance))
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (!IsFinite(verticalOffset))
                throw new ArgumentOutOfRangeException(nameof(verticalOffset));

            var horizontalForward = viewer.forward;
            horizontalForward.y = 0f;
            if (horizontalForward.sqrMagnitude < 0.000001f)
            {
                horizontalForward = viewer.up;
                horizontalForward.y = 0f;
            }
            horizontalForward = horizontalForward.sqrMagnitude < 0.000001f
                ? Vector3.forward
                : horizontalForward.normalized;

            var position = viewer.position + (horizontalForward * distance) + (Vector3.up * verticalOffset);
            var towardViewer = viewer.position - position;
            towardViewer.y = 0f;
            if (towardViewer.sqrMagnitude < 0.000001f) towardViewer = -horizontalForward;
            return new Pose(
                position,
                Quaternion.LookRotation(-towardViewer.normalized, Vector3.up));
        }

        public static void PlaceViewerFront(
            Transform surface,
            Transform viewer,
            float distance,
            float verticalOffset = 0f)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            var pose = CreateViewerFrontPose(viewer, distance, verticalOffset);
            surface.SetPositionAndRotation(pose.position, pose.rotation);
        }

        /// <summary>
        /// Places a viewer-front surface while respecting a configured standing
        /// comfort height. This is for floor-level venues where a transient or
        /// unavailable eye-height pose must never leave an interactive panel on
        /// the physical floor.
        /// </summary>
        public static void PlaceViewerFrontAtMinimumHeight(
            Transform surface,
            Transform viewer,
            float distance,
            float verticalOffset,
            float minimumWorldHeight)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (!IsPositiveFinite(minimumWorldHeight))
                throw new ArgumentOutOfRangeException(nameof(minimumWorldHeight));

            var pose = CreateViewerFrontPose(viewer, distance, verticalOffset);
            var position = pose.position;
            position.y = Mathf.Max(position.y, minimumWorldHeight);
            surface.SetPositionAndRotation(position, pose.rotation);
        }

        public static bool HasPositiveScaleChain(Transform surface)
        {
            for (var current = surface; current != null; current = current.parent)
            {
                var scale = current.localScale;
                if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f ||
                    !IsFinite(scale.x) || !IsFinite(scale.y) || !IsFinite(scale.z))
                    return false;
            }
            return true;
        }

        static bool IsPositiveFinite(float value) => value > 0f && IsFinite(value);
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
