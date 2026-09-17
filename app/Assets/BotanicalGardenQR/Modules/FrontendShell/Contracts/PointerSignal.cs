using System;
using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    public readonly struct PointerSignal
    {
        public PointerSignal(
            int pointerId,
            PointerSignalPhase phase,
            Vector3 rayOrigin,
            Vector3 rayDirection,
            double observedAtSeconds)
        {
            if (pointerId < 0)
                throw new ArgumentOutOfRangeException(nameof(pointerId));
            if (!Enum.IsDefined(typeof(PointerSignalPhase), phase))
                throw new ArgumentOutOfRangeException(nameof(phase));
            if (!IsFinite(rayOrigin))
                throw new ArgumentOutOfRangeException(nameof(rayOrigin));
            if (!IsFinite(rayDirection) || rayDirection.sqrMagnitude <= 0f)
                throw new ArgumentOutOfRangeException(nameof(rayDirection));
            if (double.IsNaN(observedAtSeconds) || double.IsInfinity(observedAtSeconds) || observedAtSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(observedAtSeconds));

            PointerId = pointerId;
            Phase = phase;
            RayOrigin = rayOrigin;
            RayDirection = rayDirection.normalized;
            ObservedAtSeconds = observedAtSeconds;
        }

        public int PointerId { get; }
        public PointerSignalPhase Phase { get; }
        public Vector3 RayOrigin { get; }
        public Vector3 RayDirection { get; }
        public double ObservedAtSeconds { get; }

        static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        static bool IsFinite(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum PointerSignalPhase
    {
        Move = 1,
        Press = 2,
        Release = 3,
        Cancel = 4
    }
}
