using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    // The display host owns visibility; this component owns one immutable world pose.
    // Never recompute from the head while reading or touching the surface.
    public sealed class ClinicalWorldSurface : MonoBehaviour
    {
        Pose _pose;
        Vector3 _scale;
        bool _pinned;
        public void Pin() { _pose = new Pose(transform.position, transform.rotation); _scale = transform.lossyScale; _pinned = true; }
        public void Restore()
        {
            if (!_pinned) return;
            transform.SetPositionAndRotation(_pose.position, _pose.rotation);
            var parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(_scale.x / parentScale.x, _scale.y / parentScale.y, _scale.z / parentScale.z);
        }
        void LateUpdate() => Restore();
        public static void PlaceForHands(Transform surface, Transform viewer, float scale = .00055f)
        {
            var forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.ProjectOnPlane(viewer.up, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            var position = viewer.position + forward.normalized * .50f - Vector3.up * .18f;
            surface.SetPositionAndRotation(position, Quaternion.LookRotation(position - viewer.position, Vector3.up));
            var parentScale = surface.parent ? surface.parent.lossyScale : Vector3.one;
            surface.localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, scale / parentScale.z);
        }
    }
}
