using System;
using BotanicalGardenQR.Fairy.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [CreateAssetMenu(
        menuName = "Botanical Garden QR/Fairy Application Configuration",
        fileName = "FairyApplicationConfiguration")]
    public sealed class FairyApplicationConfiguration : ScriptableObject, IFairyDefinitionSource
    {
        [SerializeField] GameObject _prefab;
        [SerializeField] FairyBehavior _behavior = FairyBehavior.Guide;
        [SerializeField, Min(0.01f)] float _scale = 1f;
        [SerializeField] GameObject _arrivalEffectPrefab;
        [SerializeField] GameObject _arrivalMaskPrefab;
        [Header("Companion feedback")]
        [SerializeField] GameObject _companionCueEffectPrefab;
        [SerializeField] Vector3 _companionCueEffectLocalOffset = new Vector3(0f, 0.4f, 0f);
        [SerializeField, Min(0.01f)] float _companionCueEffectLocalScale = 1f;
        [SerializeField] AudioClip _coachAttentionClip;
        [SerializeField, Range(0f, 1f)] float _coachAttentionVolume = 0.42f;
        [SerializeField, Min(0.1f)] float _coachAttentionEffectSeconds = 0.55f;
        [SerializeField, Range(1, 32)] int _coachAttentionParticleBurst = 6;
        [SerializeField] AudioClip _artifactWaitClip;
        [SerializeField, Range(0f, 1f)] float _artifactWaitVolume = 0.28f;
        [SerializeField, Min(0.1f)] float _artifactWaitEffectSeconds = 0.35f;
        [SerializeField, Range(1, 32)] int _artifactWaitParticleBurst = 4;
        [SerializeField] AudioClip _artifactReturnClip;
        [SerializeField, Range(0f, 1f)] float _artifactReturnVolume = 0.52f;
        [SerializeField, Min(0.1f)] float _artifactReturnEffectSeconds = 0.65f;
        [SerializeField, Range(1, 32)] int _artifactReturnParticleBurst = 8;
        [SerializeField] AudioClip _celebrateClip;
        [SerializeField, Range(0f, 1f)] float _celebrateVolume = 0.82f;
        [SerializeField, Min(0.1f)] float _celebrateEffectSeconds = 1.2f;
        [SerializeField, Range(1, 32)] int _celebrateParticleBurst = 12;
        [Header("Idle / locomotion feedback")]
        [SerializeField] AudioClip[] _idleLocomotionClips = Array.Empty<AudioClip>();
        [SerializeField, Range(0f, 1f)] float _idleLocomotionVolume = 0.24f;
        [SerializeField, Min(0.1f)] float _idleLocomotionMinIntervalSeconds = 3f;
        [SerializeField, Min(0.1f)] float _idleLocomotionMaxIntervalSeconds = 7f;
        [SerializeField, Min(0.1f)] float _idleLocomotionEffectSeconds = 0.25f;
        [SerializeField, Range(1, 32)] int _idleLocomotionParticleBurst = 2;
        [SerializeField, Min(0f)] float _arrivalEffectDelaySeconds = 1.6f;
        [SerializeField, Min(0f)] float _arrivalRevealDelaySeconds = 1.1f;

        public GameObject Prefab => _prefab;
        public FairyBehavior Behavior => _behavior;
        public float Scale => _scale;
        public GameObject ArrivalEffectPrefab => _arrivalEffectPrefab;
        public GameObject ArrivalMaskPrefab => _arrivalMaskPrefab;
        public GameObject CompanionCueEffectPrefab => _companionCueEffectPrefab;
        public Vector3 CompanionCueEffectLocalOffset => _companionCueEffectLocalOffset;
        public float CompanionCueEffectLocalScale => _companionCueEffectLocalScale;
        public AudioClip CoachAttentionClip => _coachAttentionClip;
        public float CoachAttentionVolume => _coachAttentionVolume;
        public float CoachAttentionEffectSeconds => _coachAttentionEffectSeconds;
        public int CoachAttentionParticleBurst => _coachAttentionParticleBurst;
        public AudioClip ArtifactWaitClip => _artifactWaitClip;
        public float ArtifactWaitVolume => _artifactWaitVolume;
        public float ArtifactWaitEffectSeconds => _artifactWaitEffectSeconds;
        public int ArtifactWaitParticleBurst => _artifactWaitParticleBurst;
        public AudioClip ArtifactReturnClip => _artifactReturnClip;
        public float ArtifactReturnVolume => _artifactReturnVolume;
        public float ArtifactReturnEffectSeconds => _artifactReturnEffectSeconds;
        public int ArtifactReturnParticleBurst => _artifactReturnParticleBurst;
        public AudioClip CelebrateClip => _celebrateClip;
        public float CelebrateVolume => _celebrateVolume;
        public float CelebrateEffectSeconds => _celebrateEffectSeconds;
        public int CelebrateParticleBurst => _celebrateParticleBurst;
        public AudioClip[] IdleLocomotionClips => _idleLocomotionClips;
        public float IdleLocomotionVolume => _idleLocomotionVolume;
        public float IdleLocomotionMinIntervalSeconds => _idleLocomotionMinIntervalSeconds;
        public float IdleLocomotionMaxIntervalSeconds => _idleLocomotionMaxIntervalSeconds;
        public float IdleLocomotionEffectSeconds => _idleLocomotionEffectSeconds;
        public int IdleLocomotionParticleBurst => _idleLocomotionParticleBurst;
        public float ArrivalEffectDelaySeconds => _arrivalEffectDelaySeconds;
        public float ArrivalRevealDelaySeconds => _arrivalRevealDelaySeconds;

        public bool TryGet(out FairyDefinition definition)
        {
            if (!IsValid(out _))
            {
                definition = null;
                return false;
            }

            definition = new FairyDefinition(
                _prefab,
                _behavior,
                _scale,
                _arrivalEffectPrefab,
                _arrivalMaskPrefab,
                new FairyCompanionFeedbackDefinition(
                    _companionCueEffectPrefab,
                    _companionCueEffectLocalOffset,
                    _companionCueEffectLocalScale,
                    _coachAttentionClip,
                    _coachAttentionVolume,
                    _coachAttentionEffectSeconds,
                    _coachAttentionParticleBurst,
                    _artifactWaitClip,
                    _artifactWaitVolume,
                    _artifactWaitEffectSeconds,
                    _artifactWaitParticleBurst,
                    _artifactReturnClip,
                    _artifactReturnVolume,
                    _artifactReturnEffectSeconds,
                    _artifactReturnParticleBurst,
                    _celebrateClip,
                    _celebrateVolume,
                    _celebrateEffectSeconds,
                    _celebrateParticleBurst,
                    _idleLocomotionClips,
                    _idleLocomotionVolume,
                    _idleLocomotionMinIntervalSeconds,
                    _idleLocomotionMaxIntervalSeconds,
                    _idleLocomotionEffectSeconds,
                    _idleLocomotionParticleBurst),
                _arrivalEffectDelaySeconds,
                _arrivalRevealDelaySeconds);
            return true;
        }

        public bool IsValid(out string reason)
        {
            if (_prefab == null)
            {
                reason = "Global Fairy configuration requires a prefab.";
                return false;
            }
            if (!Enum.IsDefined(typeof(FairyBehavior), _behavior))
            {
                reason = $"Global Fairy behavior '{_behavior}' is invalid.";
                return false;
            }
            if (!(_scale > 0f) || float.IsInfinity(_scale))
            {
                reason = "Global Fairy scale must be positive and finite.";
                return false;
            }
            if (_arrivalEffectPrefab == null)
            {
                reason = "Global Fairy configuration requires an arrival effect prefab.";
                return false;
            }
            if (_arrivalEffectPrefab.GetComponent<ParticleSystem>() == null)
            {
                reason = "Global Fairy arrival effect prefab requires a root ParticleSystem.";
                return false;
            }
            if (_arrivalMaskPrefab == null)
            {
                reason = "Global Fairy configuration requires an arrival mask prefab.";
                return false;
            }
            if (_arrivalMaskPrefab.GetComponentInChildren<Renderer>(true) == null)
            {
                reason = "Global Fairy arrival mask prefab requires a Renderer.";
                return false;
            }
            if (_companionCueEffectPrefab == null)
            {
                reason = "Global Fairy configuration requires one companion cue effect prefab.";
                return false;
            }
            if (_companionCueEffectPrefab.GetComponent<ParticleSystem>() == null)
            {
                reason = "Global Fairy companion cue effect prefab requires a root ParticleSystem.";
                return false;
            }
            if (!IsFinite(_companionCueEffectLocalOffset) ||
                !IsPositiveFinite(_companionCueEffectLocalScale))
            {
                reason = "Global Fairy companion cue effect placement must be finite and positively scaled.";
                return false;
            }
            if (_coachAttentionClip == null || _artifactWaitClip == null ||
                _artifactReturnClip == null || _celebrateClip == null)
            {
                reason = "Global Fairy configuration requires coach-attention, artifact-wait, artifact-return, and celebration AudioClips.";
                return false;
            }
            if (!IsUnitInterval(_coachAttentionVolume) ||
                !IsUnitInterval(_artifactWaitVolume) ||
                !IsUnitInterval(_artifactReturnVolume) ||
                !IsUnitInterval(_celebrateVolume) ||
                !IsPositiveFinite(_coachAttentionEffectSeconds) ||
                !IsPositiveFinite(_artifactWaitEffectSeconds) ||
                !IsPositiveFinite(_artifactReturnEffectSeconds) ||
                !IsPositiveFinite(_celebrateEffectSeconds))
            {
                reason = "Global Fairy companion cue volumes and effect durations are invalid.";
                return false;
            }
            if (!IsValidParticleBurst(_coachAttentionParticleBurst) ||
                !IsValidParticleBurst(_artifactWaitParticleBurst) ||
                !IsValidParticleBurst(_artifactReturnParticleBurst) ||
                !IsValidParticleBurst(_celebrateParticleBurst))
            {
                reason = "Global Fairy companion cue particle bursts must be between 1 and 32.";
                return false;
            }
            if (_idleLocomotionClips == null || _idleLocomotionClips.Length == 0)
            {
                reason = "Global Fairy configuration requires at least one idle locomotion AudioClip.";
                return false;
            }
            for (var index = 0; index < _idleLocomotionClips.Length; index++)
            {
                if (_idleLocomotionClips[index] == null)
                {
                    reason = $"Global Fairy idle locomotion AudioClip {index} is missing.";
                    return false;
                }
            }
            if (!IsUnitInterval(_idleLocomotionVolume) ||
                !IsPositiveFinite(_idleLocomotionMinIntervalSeconds) ||
                !IsPositiveFinite(_idleLocomotionMaxIntervalSeconds) ||
                _idleLocomotionMaxIntervalSeconds < _idleLocomotionMinIntervalSeconds ||
                !IsPositiveFinite(_idleLocomotionEffectSeconds))
            {
                reason = "Global Fairy idle locomotion volume, interval, or effect duration is invalid.";
                return false;
            }
            if (!IsValidParticleBurst(_idleLocomotionParticleBurst))
            {
                reason = "Global Fairy idle locomotion particle burst must be between 1 and 32.";
                return false;
            }
            if (_arrivalEffectDelaySeconds < 0f ||
                float.IsNaN(_arrivalEffectDelaySeconds) ||
                float.IsInfinity(_arrivalEffectDelaySeconds))
            {
                reason = "Global Fairy arrival effect delay must be non-negative and finite.";
                return false;
            }
            if (_arrivalRevealDelaySeconds < 0f ||
                float.IsNaN(_arrivalRevealDelaySeconds) ||
                float.IsInfinity(_arrivalRevealDelaySeconds))
            {
                reason = "Global Fairy arrival reveal delay must be non-negative and finite.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        static bool IsFinite(Vector3 value)
            => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        static bool IsUnitInterval(float value)
            => IsFinite(value) && value >= 0f && value <= 1f;

        static bool IsPositiveFinite(float value) => IsFinite(value) && value > 0f;

        static bool IsValidParticleBurst(int value) => value >= 1 && value <= 32;

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
