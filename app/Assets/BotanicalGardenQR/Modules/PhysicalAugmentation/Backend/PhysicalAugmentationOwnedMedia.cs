using System;
using System.Collections.Generic;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Backend
{
    [DisallowMultipleComponent]
    public sealed class PhysicalAugmentationOwnedMedia : MonoBehaviour
    {
        [SerializeField] AudioSource[] _audioSources = Array.Empty<AudioSource>();
        [SerializeField] ParticleSystem[] _particles = Array.Empty<ParticleSystem>();

        bool _playing;
        int _playRevision;

        internal bool IsPlaying => _playing;
        internal int PlayRevision => _playRevision;

        internal void ResetOwnedMedia() => StopOwnedMedia();

        internal void PlayOwnedMedia()
        {
            StopOwnedMedia();
            ValidateAuthoring();
            try
            {
                for (var index = 0; index < (_audioSources?.Length ?? 0); index++)
                    _audioSources[index].Play();
                for (var index = 0; index < (_particles?.Length ?? 0); index++)
                    _particles[index].Play(true);
                _playRevision++;
                _playing = true;
            }
            catch
            {
                StopOwnedMedia();
                throw;
            }
        }

        internal void StopOwnedMedia()
        {
            for (var index = 0; index < (_audioSources?.Length ?? 0); index++)
                if (_audioSources[index] != null)
                    _audioSources[index].Stop();
            for (var index = 0; index < (_particles?.Length ?? 0); index++)
                if (_particles[index] != null)
                    _particles[index].Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
            _playing = false;
        }

        void ValidateAuthoring()
        {
            if ((_audioSources == null || _audioSources.Length == 0) &&
                (_particles == null || _particles.Length == 0))
                throw new InvalidOperationException(
                    "Owned media requires at least one AudioSource or ParticleSystem.");

            var seen = new HashSet<UnityEngine.Object>();
            for (var index = 0; index < (_audioSources?.Length ?? 0); index++)
            {
                var source = _audioSources[index];
                if (source == null ||
                    (source.transform != transform && !source.transform.IsChildOf(transform)) ||
                    source.clip == null || !source.enabled || !seen.Add(source))
                    throw new InvalidOperationException(
                        "Owned media AudioSources must be unique, enabled, clipped, and belong to the performance root.");
                source.playOnAwake = false;
            }
            for (var index = 0; index < (_particles?.Length ?? 0); index++)
            {
                var particle = _particles[index];
                if (particle == null ||
                    (particle.transform != transform && !particle.transform.IsChildOf(transform)) ||
                    !seen.Add(particle))
                    throw new InvalidOperationException(
                        "Owned media ParticleSystems must be unique and belong to the performance root.");
            }
        }

        void OnDisable() => StopOwnedMedia();
        void OnDestroy() => StopOwnedMedia();
    }
}
