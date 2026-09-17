using System;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Backend
{
    [DisallowMultipleComponent]
    public sealed class LegacyAnimationPhysicalAugmentationPerformance :
        PhysicalAugmentationPerformanceBehaviour
    {
        [SerializeField] string _clipName;
        [SerializeField, Min(0.1f)] float _fallbackDurationSeconds = 6.5f;
        [SerializeField] Material _materialOverride;

        Animation _animation;
        AnimationState _activeState;
        Renderer[] _renderers = Array.Empty<Renderer>();
        long _generation;
        float _startedAt;
        bool _initialized;
        bool _playing;

        public override void ResetPerformance()
        {
            ResetOwnedMedia();
            EnsureInitialized();
            if (_animation == null)
                throw new InvalidOperationException(
                    "Legacy Animation performance requires one child Animation component.");

            _playing = false;
            _generation = 0;
            _animation.playAutomatically = false;
            _animation.enabled = true;
            _animation.Stop();
            _activeState = ResolveState();
            if (_activeState == null) return;

            _activeState.wrapMode = WrapMode.Once;
            _activeState.speed = 0f;
            _activeState.time = 0f;
            _activeState.weight = 1f;
            _activeState.enabled = true;
            _animation.Sample();
            _activeState.enabled = false;
            _activeState.speed = 1f;
        }

        public override void Play(long generation)
        {
            if (generation <= 0) throw new ArgumentOutOfRangeException(nameof(generation));
            ValidateAuthoring();
            ResetPerformance();

            _activeState.speed = 1f;
            _activeState.wrapMode = WrapMode.Once;
            if (!_animation.Play(_activeState.name, PlayMode.StopAll))
                throw new InvalidOperationException(
                    $"Legacy Animation performance failed to play clip '{_activeState.name}'.");
            _generation = generation;
            _startedAt = Time.unscaledTime;
            _animation.Sample();
            PlayOwnedMedia();
            _playing = true;
        }

        public override void StopPerformance()
        {
            StopOwnedMedia();
            if (!_initialized) return;
            _playing = false;
            _generation = 0;
            _animation.Stop();
        }

        void Update()
        {
            if (!_playing) return;
            var elapsed = Mathf.Max(0f, Time.unscaledTime - _startedAt);
            if ((_activeState != null && _activeState.normalizedTime >= 1f) ||
                elapsed >= _fallbackDurationSeconds)
            {
                var generation = _generation;
                _playing = false;
                _animation.Stop();
                Complete(generation, true);
            }
        }

        void EnsureInitialized()
        {
            if (_initialized) return;
            _animation = GetComponentInChildren<Animation>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            if (_materialOverride != null)
            {
                for (var rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
                {
                    var renderer = _renderers[rendererIndex];
                    if (renderer == null) continue;
                    var materials = renderer.sharedMaterials;
                    for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                        materials[materialIndex] = _materialOverride;
                    renderer.sharedMaterials = materials;
                }
            }
            _initialized = true;
        }

        AnimationState ResolveState()
        {
            if (_animation == null) return null;
            if (!string.IsNullOrWhiteSpace(_clipName)) return _animation[_clipName];
            if (_animation.clip != null) return _animation[_animation.clip.name];
            foreach (AnimationState state in _animation) return state;
            return null;
        }

        void ValidateAuthoring()
        {
            EnsureInitialized();
            _activeState = ResolveState();
            if (_animation == null || _activeState == null)
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(_clipName)
                        ? "Legacy Animation performance requires at least one clip."
                        : $"Legacy Animation performance clip '{_clipName}' is missing.");
            if (!(_activeState.length > 0f) || float.IsNaN(_activeState.length) ||
                float.IsInfinity(_activeState.length))
                throw new InvalidOperationException(
                    "Legacy Animation performance requires a finite positive clip duration.");
            if (!(_fallbackDurationSeconds > 0f) || float.IsNaN(_fallbackDurationSeconds) ||
                float.IsInfinity(_fallbackDurationSeconds))
                throw new InvalidOperationException(
                    "Legacy Animation performance requires a finite positive fallback duration.");
            if (_fallbackDurationSeconds < _activeState.length)
                throw new InvalidOperationException(
                    "Legacy Animation fallback duration must not truncate the configured clip.");
        }
    }
}
