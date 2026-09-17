using System;
using System.Collections.Generic;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Contracts
{
    public enum FairyBehavior { Guide, Orbit }

    public sealed class FairyCompanionFeedbackDefinition
    {
        public FairyCompanionFeedbackDefinition(
            GameObject effectPrefab,
            Vector3 effectLocalOffset,
            float effectLocalScale,
            AudioClip coachAttentionClip,
            float coachAttentionVolume,
            float coachAttentionEffectSeconds,
            int coachAttentionParticleBurst,
            AudioClip artifactWaitClip,
            float artifactWaitVolume,
            float artifactWaitEffectSeconds,
            int artifactWaitParticleBurst,
            AudioClip artifactReturnClip,
            float artifactReturnVolume,
            float artifactReturnEffectSeconds,
            int artifactReturnParticleBurst,
            AudioClip celebrateClip,
            float celebrateVolume,
            float celebrateEffectSeconds,
            int celebrateParticleBurst,
            IReadOnlyList<AudioClip> idleLocomotionClips,
            float idleLocomotionVolume,
            float idleLocomotionMinIntervalSeconds,
            float idleLocomotionMaxIntervalSeconds,
            float idleLocomotionEffectSeconds,
            int idleLocomotionParticleBurst)
        {
            EffectPrefab = effectPrefab != null
                ? effectPrefab
                : throw new ArgumentNullException(nameof(effectPrefab));
            if (!IsFinite(effectLocalOffset))
                throw new ArgumentOutOfRangeException(nameof(effectLocalOffset));
            if (!(effectLocalScale > 0f) || float.IsInfinity(effectLocalScale))
                throw new ArgumentOutOfRangeException(nameof(effectLocalScale));
            CoachAttentionClip = coachAttentionClip != null
                ? coachAttentionClip
                : throw new ArgumentNullException(nameof(coachAttentionClip));
            CelebrateClip = celebrateClip != null
                ? celebrateClip
                : throw new ArgumentNullException(nameof(celebrateClip));
            if (!IsUnitInterval(coachAttentionVolume))
                throw new ArgumentOutOfRangeException(nameof(coachAttentionVolume));
            if (!IsUnitInterval(celebrateVolume))
                throw new ArgumentOutOfRangeException(nameof(celebrateVolume));
            if (!IsPositiveFinite(coachAttentionEffectSeconds))
                throw new ArgumentOutOfRangeException(nameof(coachAttentionEffectSeconds));
            if (!IsPositiveFinite(celebrateEffectSeconds))
                throw new ArgumentOutOfRangeException(nameof(celebrateEffectSeconds));
            if (!IsValidParticleBurst(coachAttentionParticleBurst))
                throw new ArgumentOutOfRangeException(nameof(coachAttentionParticleBurst));
            ArtifactWaitClip = artifactWaitClip != null
                ? artifactWaitClip
                : throw new ArgumentNullException(nameof(artifactWaitClip));
            ArtifactReturnClip = artifactReturnClip != null
                ? artifactReturnClip
                : throw new ArgumentNullException(nameof(artifactReturnClip));
            if (!IsUnitInterval(artifactWaitVolume))
                throw new ArgumentOutOfRangeException(nameof(artifactWaitVolume));
            if (!IsUnitInterval(artifactReturnVolume))
                throw new ArgumentOutOfRangeException(nameof(artifactReturnVolume));
            if (!IsPositiveFinite(artifactWaitEffectSeconds))
                throw new ArgumentOutOfRangeException(nameof(artifactWaitEffectSeconds));
            if (!IsPositiveFinite(artifactReturnEffectSeconds))
                throw new ArgumentOutOfRangeException(nameof(artifactReturnEffectSeconds));
            if (!IsValidParticleBurst(artifactWaitParticleBurst))
                throw new ArgumentOutOfRangeException(nameof(artifactWaitParticleBurst));
            if (!IsValidParticleBurst(artifactReturnParticleBurst))
                throw new ArgumentOutOfRangeException(nameof(artifactReturnParticleBurst));
            if (!IsValidParticleBurst(celebrateParticleBurst))
                throw new ArgumentOutOfRangeException(nameof(celebrateParticleBurst));
            if (idleLocomotionClips == null || idleLocomotionClips.Count == 0)
                throw new ArgumentException(
                    "At least one idle locomotion AudioClip is required.",
                    nameof(idleLocomotionClips));
            var copiedIdleLocomotionClips = new AudioClip[idleLocomotionClips.Count];
            for (var index = 0; index < idleLocomotionClips.Count; index++)
            {
                var clip = idleLocomotionClips[index];
                if (clip == null)
                    throw new ArgumentException(
                        "Idle locomotion AudioClips cannot contain null entries.",
                        nameof(idleLocomotionClips));
                copiedIdleLocomotionClips[index] = clip;
            }
            if (!IsUnitInterval(idleLocomotionVolume))
                throw new ArgumentOutOfRangeException(nameof(idleLocomotionVolume));
            if (!IsPositiveFinite(idleLocomotionMinIntervalSeconds) ||
                !IsPositiveFinite(idleLocomotionMaxIntervalSeconds) ||
                idleLocomotionMaxIntervalSeconds < idleLocomotionMinIntervalSeconds)
                throw new ArgumentOutOfRangeException(nameof(idleLocomotionMaxIntervalSeconds));
            if (!IsPositiveFinite(idleLocomotionEffectSeconds))
                throw new ArgumentOutOfRangeException(nameof(idleLocomotionEffectSeconds));
            if (!IsValidParticleBurst(idleLocomotionParticleBurst))
                throw new ArgumentOutOfRangeException(nameof(idleLocomotionParticleBurst));

            EffectLocalOffset = effectLocalOffset;
            EffectLocalScale = effectLocalScale;
            CoachAttentionVolume = coachAttentionVolume;
            CoachAttentionEffectSeconds = coachAttentionEffectSeconds;
            CoachAttentionParticleBurst = coachAttentionParticleBurst;
            ArtifactWaitVolume = artifactWaitVolume;
            ArtifactWaitEffectSeconds = artifactWaitEffectSeconds;
            ArtifactWaitParticleBurst = artifactWaitParticleBurst;
            ArtifactReturnVolume = artifactReturnVolume;
            ArtifactReturnEffectSeconds = artifactReturnEffectSeconds;
            ArtifactReturnParticleBurst = artifactReturnParticleBurst;
            CelebrateVolume = celebrateVolume;
            CelebrateEffectSeconds = celebrateEffectSeconds;
            CelebrateParticleBurst = celebrateParticleBurst;
            IdleLocomotionClips = Array.AsReadOnly(copiedIdleLocomotionClips);
            IdleLocomotionVolume = idleLocomotionVolume;
            IdleLocomotionMinIntervalSeconds = idleLocomotionMinIntervalSeconds;
            IdleLocomotionMaxIntervalSeconds = idleLocomotionMaxIntervalSeconds;
            IdleLocomotionEffectSeconds = idleLocomotionEffectSeconds;
            IdleLocomotionParticleBurst = idleLocomotionParticleBurst;
        }

        public GameObject EffectPrefab { get; }
        public Vector3 EffectLocalOffset { get; }
        public float EffectLocalScale { get; }
        public AudioClip CoachAttentionClip { get; }
        public float CoachAttentionVolume { get; }
        public float CoachAttentionEffectSeconds { get; }
        public int CoachAttentionParticleBurst { get; }
        public AudioClip ArtifactWaitClip { get; }
        public float ArtifactWaitVolume { get; }
        public float ArtifactWaitEffectSeconds { get; }
        public int ArtifactWaitParticleBurst { get; }
        public AudioClip ArtifactReturnClip { get; }
        public float ArtifactReturnVolume { get; }
        public float ArtifactReturnEffectSeconds { get; }
        public int ArtifactReturnParticleBurst { get; }
        public AudioClip CelebrateClip { get; }
        public float CelebrateVolume { get; }
        public float CelebrateEffectSeconds { get; }
        public int CelebrateParticleBurst { get; }
        public IReadOnlyList<AudioClip> IdleLocomotionClips { get; }
        public float IdleLocomotionVolume { get; }
        public float IdleLocomotionMinIntervalSeconds { get; }
        public float IdleLocomotionMaxIntervalSeconds { get; }
        public float IdleLocomotionEffectSeconds { get; }
        public int IdleLocomotionParticleBurst { get; }

        static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        static bool IsUnitInterval(float value)
            => IsFinite(value) && value >= 0f && value <= 1f;

        static bool IsPositiveFinite(float value) => IsFinite(value) && value > 0f;

        static bool IsValidParticleBurst(int value) => value >= 1 && value <= 32;

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class FairyDefinition
    {
        public FairyDefinition(
            GameObject prefab,
            FairyBehavior behavior,
            float scale,
            GameObject arrivalEffectPrefab,
            GameObject arrivalMaskPrefab,
            FairyCompanionFeedbackDefinition companionFeedback,
            float arrivalEffectDelaySeconds,
            float arrivalRevealDelaySeconds)
        {
            Prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            if (!(scale > 0f) || float.IsInfinity(scale))
                throw new ArgumentOutOfRangeException(nameof(scale));
            ArrivalEffectPrefab = arrivalEffectPrefab != null
                ? arrivalEffectPrefab
                : throw new ArgumentNullException(nameof(arrivalEffectPrefab));
            ArrivalMaskPrefab = arrivalMaskPrefab != null
                ? arrivalMaskPrefab
                : throw new ArgumentNullException(nameof(arrivalMaskPrefab));
            CompanionFeedback = companionFeedback ??
                                throw new ArgumentNullException(nameof(companionFeedback));
            if (arrivalEffectDelaySeconds < 0f ||
                float.IsNaN(arrivalEffectDelaySeconds) ||
                float.IsInfinity(arrivalEffectDelaySeconds))
                throw new ArgumentOutOfRangeException(nameof(arrivalEffectDelaySeconds));
            if (arrivalRevealDelaySeconds < 0f ||
                float.IsNaN(arrivalRevealDelaySeconds) ||
                float.IsInfinity(arrivalRevealDelaySeconds))
                throw new ArgumentOutOfRangeException(nameof(arrivalRevealDelaySeconds));

            Behavior = behavior;
            Scale = scale;
            ArrivalEffectDelaySeconds = arrivalEffectDelaySeconds;
            ArrivalRevealDelaySeconds = arrivalRevealDelaySeconds;
        }

        public GameObject Prefab { get; }
        public FairyBehavior Behavior { get; }
        public float Scale { get; }
        public GameObject ArrivalEffectPrefab { get; }
        public GameObject ArrivalMaskPrefab { get; }
        public FairyCompanionFeedbackDefinition CompanionFeedback { get; }
        public float ArrivalEffectDelaySeconds { get; }
        public float ArrivalRevealDelaySeconds { get; }
    }
}
