using System;
using BotanicalGardenQR.FrontendShell.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    public sealed class PointerRouter
    {
        readonly ShellFlowActionTarget[] _targets;
        readonly float _maximumDistance;
        ShellFlowActionTarget _pressedTarget;
        int _pressedPointerId = -1;

        public PointerRouter(ShellFlowActionTarget[] targets, float maximumDistance)
        {
            _targets = targets ?? throw new ArgumentNullException(nameof(targets));
            if (!(maximumDistance > 0f) || float.IsNaN(maximumDistance) || float.IsInfinity(maximumDistance))
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));
            _maximumDistance = maximumDistance;
        }

        public bool TryRoute(PointerSignal signal, out ShellFlowActionTarget selected)
        {
            selected = null;
            if (signal.Phase == PointerSignalPhase.Cancel)
            {
                if (signal.PointerId == _pressedPointerId) ClearPress();
                return false;
            }

            var target = HitTarget(signal);
            if (signal.Phase == PointerSignalPhase.Press)
            {
                _pressedPointerId = signal.PointerId;
                _pressedTarget = target;
                return false;
            }
            if (signal.Phase != PointerSignalPhase.Release || signal.PointerId != _pressedPointerId)
                return false;

            if (_pressedTarget != null && _pressedTarget == target && _pressedTarget.isActiveAndEnabled)
                selected = _pressedTarget;
            ClearPress();
            return selected != null;
        }

        ShellFlowActionTarget HitTarget(PointerSignal signal)
        {
            var ray = new Ray(signal.RayOrigin, signal.RayDirection);
            ShellFlowActionTarget selected = null;
            var selectedDistance = float.PositiveInfinity;
            for (var index = 0; index < _targets.Length; index++)
            {
                var candidate = _targets[index];
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsInteractable ||
                    !TryGetDistance(candidate.HitRect, ray, out var distance) ||
                    distance > _maximumDistance || distance >= selectedDistance)
                    continue;
                selected = candidate;
                selectedDistance = distance;
            }
            return selected;
        }

        static bool TryGetDistance(RectTransform rect, Ray ray, out float distance)
        {
            distance = 0f;
            if (rect == null) return false;
            var plane = new Plane(rect.forward, rect.position);
            if (!plane.Raycast(ray, out var enter) || enter <= 0f) return false;
            var localPoint = rect.InverseTransformPoint(ray.GetPoint(enter));
            if (!rect.rect.Contains(localPoint)) return false;
            distance = enter;
            return true;
        }

        void ClearPress()
        {
            _pressedPointerId = -1;
            _pressedTarget = null;
        }
    }
}
