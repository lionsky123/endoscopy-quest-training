using System;
using UnityEngine;

namespace BotanicalGardenQR.Model.Contracts
{
    public sealed class ModelPresentationSpec
    {
        public ModelPresentationSpec(
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale,
            Material materialOverride = null,
            bool allowRotation = true,
            float rotationDegreesPerSecond = 24f,
            float bobAmplitude = 0.01f,
            float bobFrequency = 0.5f)
        {
            if (!IsFinite(localPosition) || !IsFinite(localEulerAngles) || !IsPositiveFinite(localScale))
                throw new ArgumentException("Model presentation transform contains invalid values.");
            if (!IsFinite(rotationDegreesPerSecond) || rotationDegreesPerSecond < 0f)
                throw new ArgumentOutOfRangeException(nameof(rotationDegreesPerSecond));
            if (!IsFinite(bobAmplitude) || bobAmplitude < 0f)
                throw new ArgumentOutOfRangeException(nameof(bobAmplitude));
            if (!IsFinite(bobFrequency) || bobFrequency < 0f)
                throw new ArgumentOutOfRangeException(nameof(bobFrequency));

            LocalPosition = localPosition;
            LocalEulerAngles = localEulerAngles;
            LocalScale = localScale;
            MaterialOverride = materialOverride;
            AllowRotation = allowRotation;
            RotationDegreesPerSecond = rotationDegreesPerSecond;
            BobAmplitude = bobAmplitude;
            BobFrequency = bobFrequency;
        }

        public Vector3 LocalPosition { get; }
        public Vector3 LocalEulerAngles { get; }
        public Vector3 LocalScale { get; }
        public Material MaterialOverride { get; }
        public bool AllowRotation { get; }
        public float RotationDegreesPerSecond { get; }
        public float BobAmplitude { get; }
        public float BobFrequency { get; }

        public static ModelPresentationSpec Default { get; } =
            new ModelPresentationSpec(Vector3.zero, Vector3.zero, Vector3.one);

        static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        static bool IsPositiveFinite(Vector3 value)
            => value.x > 0f && value.y > 0f && value.z > 0f && IsFinite(value);

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
