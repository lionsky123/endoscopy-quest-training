using BotanicalGardenQR.Model.Contracts;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BotanicalGardenQR.Model.Backend
{
    internal sealed class AnimatorModelDriver : IModelDriver
    {
        readonly ModelAnimationSpec _spec;
        Animator _animator;
        PlayableGraph _graph;
        AnimationClipPlayable _clipPlayable;
        bool _hasGraph;
        bool _isPlaying;

        public AnimatorModelDriver(ModelAnimationSpec spec) => _spec = spec;
        public bool CanPlayAnimation => true;
        public bool IsAnimationPlaying => _isPlaying;

        public ModelResult Attach(GameObject instance)
        {
            _animator = instance.GetComponentInChildren<Animator>(true);
            if (_animator == null)
                _animator = instance.AddComponent<Animator>();

            _isPlaying = false;
            if (_spec.Driver == ModelAnimationDriver.AnimatorController)
            {
                _animator.runtimeAnimatorController = _spec.Controller;
                _animator.speed = 0f;
                _animator.Play(_spec.InitialState, 0, 0f);
                _animator.Update(0f);
                return ModelResult.Success();
            }

            if (_spec.Driver != ModelAnimationDriver.AnimationClip || _spec.Clip == null)
                return ModelResult.Failure(ModelFailureCode.UnsupportedAnimation, "model.animation.driver_unsupported");

            _graph = PlayableGraph.Create("ModelAnimation");
            _clipPlayable = AnimationClipPlayable.Create(_graph, _spec.Clip);
            _clipPlayable.SetTime(0d);
            var output = AnimationPlayableOutput.Create(_graph, "ModelAnimation", _animator);
            output.SetSourcePlayable(_clipPlayable);
            _hasGraph = true;
            _graph.Evaluate(0f);
            _graph.Stop();
            return ModelResult.Success();
        }

        public ModelResult PlayAnimation()
        {
            if (_spec.Driver == ModelAnimationDriver.AnimatorController)
            {
                if (_animator == null)
                    return ModelResult.Failure(ModelFailureCode.UnsupportedAnimation, "model.animation.animator_missing");
                _animator.speed = 1f;
                _animator.Play(_spec.InitialState, 0, 0f);
                _animator.Update(0f);
                _isPlaying = true;
                return ModelResult.Success();
            }

            if (!_hasGraph)
                return ModelResult.Failure(ModelFailureCode.UnsupportedAnimation, "model.animation.graph_missing");
            _clipPlayable.SetTime(0d);
            _graph.Evaluate(0f);
            _graph.Play();
            _isPlaying = true;
            return ModelResult.Success();
        }

        public bool Tick(float deltaTime)
        {
            if (!_isPlaying) return false;
            if (_spec.Driver == ModelAnimationDriver.AnimatorController)
            {
                if (_animator == null) return false;
                var state = _animator.GetCurrentAnimatorStateInfo(0);
                if (!state.loop && state.normalizedTime >= 1f)
                {
                    _animator.speed = 0f;
                    _isPlaying = false;
                    return true;
                }
                return false;
            }

            if (!_hasGraph || !_clipPlayable.IsValid()) return false;
            if (_clipPlayable.GetTime() >= _spec.Clip.length)
            {
                _graph.Stop();
                _clipPlayable.SetTime(_spec.Clip.length);
                _graph.Evaluate(0f);
                _isPlaying = false;
                return true;
            }
            return false;
        }

        public void StopAnimation()
        {
            if (_animator != null)
                _animator.speed = 0f;
            if (_hasGraph && _graph.IsValid())
                _graph.Stop();
            _isPlaying = false;
        }

        public void Dispose()
        {
            StopAnimation();
            if (_hasGraph && _graph.IsValid())
                _graph.Destroy();
            _hasGraph = false;
            _animator = null;
        }
    }
}
