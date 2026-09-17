using System;
using UnityEngine;

namespace BotanicalGardenQR.Activation.Contracts
{
    /// <summary>
    /// One immutable head-focus query. Angular padding expands each edge of a
    /// source-provided physical surface; it is not a center-angle scan cone.
    /// </summary>
    public readonly struct RecognitionFocusQuery
    {
        public RecognitionFocusQuery(Ray ray, float angularPaddingDegrees)
        {
            if (!IsFinite(ray.origin) || !IsUsableDirection(ray.direction))
                throw new ArgumentException("Recognition focus ray must contain finite, usable values.", nameof(ray));
            if (float.IsNaN(angularPaddingDegrees) || float.IsInfinity(angularPaddingDegrees) ||
                angularPaddingDegrees < 0f || angularPaddingDegrees > 10f)
                throw new ArgumentOutOfRangeException(nameof(angularPaddingDegrees));

            Ray = new Ray(ray.origin, ray.direction.normalized);
            AngularPaddingDegrees = angularPaddingDegrees;
        }

        public Ray Ray { get; }
        public float AngularPaddingDegrees { get; }

        static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        static bool IsUsableDirection(Vector3 value)
            => IsFinite(value) && value.sqrMagnitude > 0.000001f;

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public interface IRecognitionFocusQueryProvider
    {
        bool TryGetQuery(out RecognitionFocusQuery query);
    }

    public interface IRecognitionFocusQueryConsumer
    {
        void ConfigureFocusQueryProvider(IRecognitionFocusQueryProvider provider);
    }
}
