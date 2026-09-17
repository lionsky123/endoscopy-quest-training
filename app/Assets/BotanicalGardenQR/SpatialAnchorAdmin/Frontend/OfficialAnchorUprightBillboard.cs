using UnityEngine;

namespace BotanicalGardenQR.SpatialAnchorAdmin.Official
{
    /// <summary>Keeps official anchor controls upright and facing the viewer.</summary>
    public sealed class OfficialAnchorUprightBillboard : MonoBehaviour
    {
        [SerializeField] Transform _viewer;
        [SerializeField] Transform[] _additionalTargets;

        void LateUpdate()
        {
            if (!_viewer)
            {
                var mainCamera = Camera.main;
                _viewer = mainCamera ? mainCamera.transform : null;
            }
            if (!_viewer) return;

            ApplyUprightRotation(transform);
            if (_additionalTargets == null) return;
            foreach (var target in _additionalTargets)
            {
                if (target && target != transform) ApplyUprightRotation(target);
            }
        }

        void ApplyUprightRotation(Transform target)
            => target.rotation = CalculateTargetRotation(_viewer.position, _viewer.rotation, target.position);

        public static Quaternion CalculateTargetRotation(
            Vector3 viewerPosition,
            Quaternion viewerRotation,
            Vector3 targetPosition)
        {
            var horizontalDirection = Vector3.ProjectOnPlane(targetPosition - viewerPosition, Vector3.up);
            if (horizontalDirection.sqrMagnitude < 0.0001f)
            {
                horizontalDirection = Vector3.ProjectOnPlane(viewerRotation * Vector3.forward, Vector3.up);
            }
            if (horizontalDirection.sqrMagnitude < 0.0001f) horizontalDirection = Vector3.forward;
            return Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
        }
    }
}
