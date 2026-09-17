using System;
using UnityEngine;

namespace BotanicalGardenQR.Effect.Backend
{
    internal sealed class EffectImplementationSelector
    {
        internal IEffectRuntime Create(GameObject instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
            return particles.Length > 0
                ? (IEffectRuntime)new ParticleEffectRuntime(instance, particles)
                : new ActiveObjectEffectRuntime(instance);
        }
    }

    internal interface IEffectRuntime : IDisposable
    {
        void Trigger();
        void Stop();
        void Reset();
    }

    internal sealed class ParticleEffectRuntime : IEffectRuntime
    {
        readonly GameObject _instance;
        readonly ParticleSystem[] _particles;
        internal ParticleEffectRuntime(GameObject instance, ParticleSystem[] particles)
        {
            _instance = instance;
            _particles = particles;
        }
        public void Trigger()
        {
            _instance.SetActive(true);
            foreach (var particle in _particles) particle.Play(true);
        }
        public void Stop()
        {
            foreach (var particle in _particles) particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _instance.SetActive(false);
        }
        public void Reset()
        {
            _instance.SetActive(true);
            foreach (var particle in _particles) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _instance.SetActive(false);
        }
        public void Dispose() { }
    }

    internal sealed class ActiveObjectEffectRuntime : IEffectRuntime
    {
        readonly GameObject _instance;
        internal ActiveObjectEffectRuntime(GameObject instance) => _instance = instance;
        public void Trigger() => _instance.SetActive(true);
        public void Stop() => _instance.SetActive(false);
        public void Reset() => _instance.SetActive(false);
        public void Dispose() { }
    }
}
