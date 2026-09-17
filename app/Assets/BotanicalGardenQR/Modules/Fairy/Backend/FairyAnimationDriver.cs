using System;
using System.Collections.Generic;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Backend
{
    internal sealed class FairyAnimationDriver : MonoBehaviour
    {
        Animation[] _legacyAnimations = Array.Empty<Animation>();
        Animator[] _animators = Array.Empty<Animator>();
        readonly Dictionary<Animator, AnimatorParameters> _parameters = new();
        bool _moving;

        static readonly int WalkRate = Animator.StringToHash("WalkRate");
        static readonly int Running = Animator.StringToHash("Running");
        static readonly int Jumping = Animator.StringToHash("Jumping");
        static readonly int Wave = Animator.StringToHash("Wave");
        static readonly int Like = Animator.StringToHash("Like");
        static readonly int PowerUp = Animator.StringToHash("PowerUp");
        static readonly int Wonder = Animator.StringToHash("Wonder");
        static readonly int[] StandingTriggers = { Wave, Like, PowerUp, Jumping };

        internal bool HasPlayableAnimation { get; private set; }

        internal void Initialize()
        {
            _legacyAnimations = GetComponentsInChildren<Animation>(true);
            _animators = GetComponentsInChildren<Animator>(true);
            HasPlayableAnimation = PrepareLegacyAnimations() || PrepareAnimators();
            if (HasPlayableAnimation) Play();
        }

        internal void Play()
        {
            foreach (var animation in _legacyAnimations)
            {
                if (animation == null || animation.clip == null)
                {
                    continue;
                }

                animation.enabled = true;
                animation.Play(animation.clip.name);
            }

            foreach (var animator in _animators)
            {
                if (animator == null || animator.runtimeAnimatorController == null)
                {
                    continue;
                }

                animator.enabled = true;
                animator.speed = 1f;
                animator.Rebind();
                animator.Update(0f);
            }
        }

        internal void Stop()
        {
            foreach (var animation in _legacyAnimations)
            {
                if (animation == null)
                {
                    continue;
                }

                animation.Stop();
            }

            foreach (var animator in _animators)
            {
                if (animator == null)
                {
                    continue;
                }

                animator.speed = 0f;
            }
        }

        // Sampled approved Oppy feet at its authored 0.03 scale: 0.2445/0.2639 m/s.
        // Only locomotion states consume this multiplier; standing reactions keep their timing.
        internal void SetTravelSpeed(float metresPerSecond)
        {
            if (!(metresPerSecond > 0f) || float.IsInfinity(metresPerSecond)) return;
            foreach (var animator in _animators)
            {
                if (animator == null || !_parameters.TryGetValue(animator, out var parameters) || !parameters.HasWalkRate) continue;
                var scaleRatio = Mathf.Max(.01f, Mathf.Abs(animator.transform.lossyScale.x) / .03f);
                animator.SetFloat(WalkRate, Mathf.Clamp(metresPerSecond / (.2542f * scaleRatio), .1f, 6f));
            }
        }

        internal void SetMoving(bool moving)
        {
            _moving = moving;
            foreach (var animator in _animators)
            {
                if (animator == null || !_parameters.TryGetValue(animator, out var parameters) ||
                    !parameters.HasRunning)
                    continue;
                animator.SetBool(Running, moving);
                if (moving)
                {
                    foreach (var trigger in StandingTriggers)
                        if (parameters.HasTrigger(trigger)) animator.ResetTrigger(trigger);
                    if (parameters.HasWonder) animator.SetBool(Wonder, false);
                }
            }
        }

        internal void PlayDiscoveryReaction()
        {
            if (_moving) return;
            var first = UnityEngine.Random.value < 0.5f ? Like : Wave;
            var second = first == Like ? Wave : Like;
            if (!TrySetTrigger(first) && !TrySetTrigger(second) && HasPlayableAnimation)
                Play();
        }

        internal void PlayIdleReaction() => PlayDiscoveryReaction();

        internal void PlayCoachAttention()
        {
            if (_moving) return;
            if (!TrySetTrigger(Wave) && !TrySetTrigger(Like) && HasPlayableAnimation)
                Play();
        }

        internal void PlayWonder()
        {
            var applied = false;
            foreach (var animator in _animators)
            {
                if (animator == null || !_parameters.TryGetValue(animator, out var parameters) ||
                    !parameters.HasWonder)
                    continue;
                animator.SetBool(Wonder, true);
                applied = true;
            }
            if (!applied) PlayDiscoveryReaction();
        }

        internal void EndWonder()
        {
            foreach (var animator in _animators)
            {
                if (animator == null || !_parameters.TryGetValue(animator, out var parameters) ||
                    !parameters.HasWonder)
                    continue;
                animator.SetBool(Wonder, false);
            }
        }

        internal void PlayInteraction() => SetTrigger(Like);

        internal void PlayCelebrate() => SetTrigger(PowerUp);

        bool PrepareLegacyAnimations()
        {
            var prepared = false;
            foreach (var animation in _legacyAnimations)
            {
                if (animation == null)
                {
                    continue;
                }

                animation.cullingType = AnimationCullingType.AlwaysAnimate;
                animation.wrapMode = WrapMode.Loop;

                AnimationState firstState = null;
                foreach (AnimationState state in animation)
                {
                    if (state == null || state.clip == null)
                    {
                        continue;
                    }

                    state.wrapMode = WrapMode.Loop;
                    state.enabled = true;
                    if (firstState == null) firstState = state;
                }

                if (animation.clip == null && firstState != null)
                {
                    animation.clip = firstState.clip;
                }

                if (animation.clip != null)
                {
                    prepared = true;
                }
            }

            return prepared;
        }

        bool PrepareAnimators()
        {
            var prepared = false;
            _parameters.Clear();
            foreach (var animator in _animators)
            {
                if (animator == null)
                {
                    continue;
                }

                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.enabled = true;
                animator.speed = 1f;
                if (animator.runtimeAnimatorController != null)
                {
                    _parameters[animator] = AnimatorParameters.Capture(animator.parameters);
                    prepared = true;
                }
            }

            return prepared;
        }

        void SetTrigger(int parameter)
        {
            if (_moving) return;
            if (!TrySetTrigger(parameter) && HasPlayableAnimation)
                Play();
        }

        bool TrySetTrigger(int parameter)
        {
            var triggered = false;
            foreach (var animator in _animators)
            {
                if (animator == null || !_parameters.TryGetValue(animator, out var parameters) ||
                    !parameters.HasTrigger(parameter))
                    continue;
                animator.ResetTrigger(parameter);
                animator.SetTrigger(parameter);
                triggered = true;
            }
            return triggered;
        }

        readonly struct AnimatorParameters
        {
            AnimatorParameters(
                bool hasRunning,
                bool hasJumping,
                bool hasWave,
                bool hasLike,
                bool hasPowerUp,
                bool hasWonder, bool hasWalkRate)
            {
                HasRunning = hasRunning;
                HasJumping = hasJumping;
                HasWave = hasWave;
                HasLike = hasLike;
                HasPowerUp = hasPowerUp;
                HasWonder = hasWonder;
                HasWalkRate = hasWalkRate;
            }

            internal bool HasRunning { get; }
            bool HasJumping { get; }
            bool HasWave { get; }
            bool HasLike { get; }
            bool HasPowerUp { get; }
            internal bool HasWonder { get; }
            internal bool HasWalkRate { get; }

            internal bool HasTrigger(int parameter)
                => parameter == Jumping && HasJumping ||
                   parameter == Wave && HasWave ||
                   parameter == Like && HasLike ||
                   parameter == PowerUp && HasPowerUp;

            internal static AnimatorParameters Capture(AnimatorControllerParameter[] parameters)
            {
                var hasRunning = false;
                var hasJumping = false;
                var hasWave = false;
                var hasLike = false;
                var hasPowerUp = false;
                var hasWonder = false;
                var hasWalkRate = false;
                foreach (var parameter in parameters)
                {
                    if (parameter == null) continue;
                    if (parameter.nameHash == WalkRate && parameter.type == AnimatorControllerParameterType.Float)
                        hasWalkRate = true;
                    else if (parameter.nameHash == Running && parameter.type == AnimatorControllerParameterType.Bool)
                        hasRunning = true;
                    else if (parameter.nameHash == Jumping && parameter.type == AnimatorControllerParameterType.Trigger)
                        hasJumping = true;
                    else if (parameter.nameHash == Wave && parameter.type == AnimatorControllerParameterType.Trigger)
                        hasWave = true;
                    else if (parameter.nameHash == Like && parameter.type == AnimatorControllerParameterType.Trigger)
                        hasLike = true;
                    else if (parameter.nameHash == PowerUp && parameter.type == AnimatorControllerParameterType.Trigger)
                        hasPowerUp = true;
                    else if (parameter.nameHash == Wonder && parameter.type == AnimatorControllerParameterType.Bool)
                        hasWonder = true;
                }
                return new AnimatorParameters(hasRunning, hasJumping, hasWave, hasLike, hasPowerUp, hasWonder, hasWalkRate);
            }
        }
    }
}
