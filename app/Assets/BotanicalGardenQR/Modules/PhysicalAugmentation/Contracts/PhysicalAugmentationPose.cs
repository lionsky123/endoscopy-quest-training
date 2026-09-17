using System;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Contracts
{
    public static class PhysicalAugmentationPose
    {
        public static Pose Resolve(Pose anchorWorldPose, Pose augmentationPoseInAnchorSpace)
        {
            if (!IsFinite(anchorWorldPose.position) || !IsFinite(anchorWorldPose.rotation) ||
                !IsFinite(augmentationPoseInAnchorSpace.position) || !IsFinite(augmentationPoseInAnchorSpace.rotation))
                throw new ArgumentException("Anchor and augmentation poses must be finite.");
            return new Pose(
                anchorWorldPose.position + anchorWorldPose.rotation * augmentationPoseInAnchorSpace.position,
                anchorWorldPose.rotation * augmentationPoseInAnchorSpace.rotation);
        }

        static bool IsFinite(Vector3 value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        static bool IsFinite(Quaternion value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) &&
               float.IsFinite(value.z) && float.IsFinite(value.w);
    }
}
