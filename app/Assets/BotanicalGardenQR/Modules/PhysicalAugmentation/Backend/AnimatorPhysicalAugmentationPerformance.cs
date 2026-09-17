using System;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Backend
{
    [DisallowMultipleComponent]
    public sealed class AnimatorPhysicalAugmentationPerformance :
        PhysicalAugmentationPerformanceBehaviour
    {
        [SerializeField] string _stateName;
        [SerializeField, Min(0.1f)] float _fallbackDurationSeconds = 6.5f;
        [SerializeField] Material _materialOverride;

        Animator _animator;
        Renderer[] _renderers = Array.Empty<Renderer>();
        long _generation;
        float _startedAt;
        bool _initialized;
        bool _playing;

        public override void ResetPerformance()
        {
            ResetOwnedMedia();
            EnsureInitialized();
            if (_animator == null)
                throw new InvalidOperationException(
                    "Animator performance requires one child Animator.");
            _playing = false;
            _generation = 0;
            _animator.enabled = true;
            _animator.speed = 0f;
            _animator.Rebind();
            _animator.Update(0f);
        }

        public override void Play(long generation)
        {
            if (generation <= 0) throw new ArgumentOutOfRangeException(nameof(generation));
            ValidateAuthoring();
            ResetPerformance();
            _generation = generation;
            _startedAt = Time.unscaledTime;
            _animator.speed = 1f;
            if (string.IsNullOrWhiteSpace(_stateName))
                _animator.Play(0, 0, 0f);
            else
                _animator.Play(Animator.StringToHash(_stateName), 0, 0f);
            _animator.Update(0f);
            PlayOwnedMedia();
            _playing = true;
        }

        public override void StopPerformance()
        {
            StopOwnedMedia();
            if (!_initialized) return;
            _playing = false;
            _generation = 0;
            _animator.speed = 0f;
        }

        void Update()
        {
            if (!_playing) return;
            var elapsed = Mathf.Max(0f, Time.unscaledTime - _startedAt);
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            var expectedState = string.IsNullOrWhiteSpace(_stateName) || state.IsName(_stateName);
            if ((expectedState && !state.loop && state.normalizedTime >= 1f) ||
                elapsed >= _fallbackDurationSeconds)
            {
                var generation = _generation;
                _playing = false;
                _animator.speed = 0f;
                Complete(generation, true);
            }
        }

        void EnsureInitialized()
        {
            if (_initialized) return;
            _animator = GetComponentInChildren<Animator>(true);
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

        void ValidateAuthoring()
        {
            EnsureInitialized();
            if (_animator == null || _animator.runtimeAnimatorController == null)
                throw new InvalidOperationException(
                    "Animator performance requires one child Animator with a controller.");
            if (!(_fallbackDurationSeconds > 0f) || float.IsNaN(_fallbackDurationSeconds) ||
                float.IsInfinity(_fallbackDurationSeconds))
                throw new InvalidOperationException(
                    "Animator performance requires a finite positive fallback duration.");
            if (!string.IsNullOrWhiteSpace(_stateName) &&
                !_animator.HasState(0, Animator.StringToHash(_stateName)))
                throw new InvalidOperationException(
                    $"Animator performance state '{_stateName}' is missing.");
        }
    }
}
