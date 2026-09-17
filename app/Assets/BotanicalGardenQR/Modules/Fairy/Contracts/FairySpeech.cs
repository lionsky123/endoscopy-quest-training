using System;
using UnityEngine;

namespace BotanicalGardenQR.Fairy.Contracts
{
    /// <summary>
    /// One already-resolved voice clip for the application-global Fairy.
    /// Bootstrap resolves the clip from the owning tutorial theme; Fairy owns
    /// the spatial playback and the single voice channel.
    /// </summary>
    public readonly struct FairySpeech
    {
        public FairySpeech(AudioClip clip, float volume = 0.82f)
        {
            RequestId = Guid.NewGuid();
            Clip = clip ?? throw new ArgumentNullException(nameof(clip));
            if (float.IsNaN(volume) || float.IsInfinity(volume) || volume < 0f || volume > 1f)
                throw new ArgumentOutOfRangeException(nameof(volume));
            Volume = volume;
        }

        public AudioClip Clip { get; }
        public float Volume { get; }
        public Guid RequestId { get; }
    }
}
