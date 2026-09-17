using System;
using BotanicalGardenQR.Experience.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.ImageRing.Contracts
{
    public enum ImageRingItemPlaybackIntentKind
    {
        Play,
        Stop
    }

    public readonly struct ImageRingItemPlaybackIntent
    {
        public ImageRingItemPlaybackIntent(int itemIndex, ImageRingItemPlaybackIntentKind kind)
        {
            if (itemIndex < 0) throw new ArgumentOutOfRangeException(nameof(itemIndex));
            ItemIndex = itemIndex;
            Kind = kind;
        }

        public int ItemIndex { get; }
        public ImageRingItemPlaybackIntentKind Kind { get; }
    }

    public interface IImageRingRuntime : IDisposable
    {
        void Open(
            SessionToken session,
            ImageRingDefinition definition,
            Action<ImageRingItemPlaybackIntent> dispatchPlayback,
            Action requestClose);
        void PlayAudio(AudioClip clip);
        void StopAudio(bool immediate);
        void Close();
    }
}
