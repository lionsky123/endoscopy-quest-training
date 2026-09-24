using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed class FullScriptRoomGalleryDragState
    {
        internal const float HandleRadius = .19f;
        int _activeHand = -1;
        Vector3 _lastPoint;

        internal bool IsDragging => _activeHand >= 0;
        internal int ActiveHand => _activeHand;

        internal bool TryBegin(int hand, bool tracked, bool pinching, Vector3 pinchPoint, Vector3 handlePoint)
        {
            if (IsDragging || hand < 0 || !tracked || !pinching || !Finite(pinchPoint) || !Finite(handlePoint) ||
                (pinchPoint - handlePoint).sqrMagnitude > HandleRadius * HandleRadius)
                return false;

            _activeHand = hand;
            _lastPoint = pinchPoint;
            return true;
        }

        internal bool Sample(int hand, bool tracked, bool pinching, Vector3 pinchPoint,
            Vector3 horizontalAxis, out float rotationDegrees)
        {
            rotationDegrees = 0;
            if (_activeHand < 0 || hand != _activeHand) return false;
            if (!tracked || !pinching || !Finite(pinchPoint))
            {
                Cancel();
                return false;
            }

            var axis = Vector3.ProjectOnPlane(horizontalAxis, Vector3.up).normalized;
            if (axis.sqrMagnitude < .5f)
            {
                Cancel();
                return false;
            }

            var horizontalDelta = Vector3.Dot(pinchPoint - _lastPoint, axis);
            _lastPoint = pinchPoint;
            rotationDegrees = -horizontalDelta * 120f;
            return true;
        }

        internal void Cancel()
        {
            _activeHand = -1;
            _lastPoint = default;
        }

        static bool Finite(Vector3 value)
            => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
