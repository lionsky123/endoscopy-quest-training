using System;
using UnityEngine;

namespace BotanicalGardenQR.Video.Contracts
{
    public interface IVideoSurfaceTarget : IDisposable
    {
        Transform ContentRoot { get; }
        Vector2 AvailableSize { get; }

        void SetTexture(Texture texture, float aspectRatio);
        void SetVisible(bool visible);
        void Clear();
    }
}
