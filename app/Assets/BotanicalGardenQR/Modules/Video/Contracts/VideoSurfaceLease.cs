using System;
using UnityEngine;
namespace BotanicalGardenQR.Video.Contracts
{
    public sealed class VideoSurfaceLease : IDisposable
    {
        readonly Action _release;
        bool _disposed;
        public VideoSurfaceLease(IVideoSurfaceTarget target, bool isVisible, Action release)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            ContentRoot = target.ContentRoot;
            AvailableSize = target.AvailableSize;
            IsVisible = isVisible;
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public IVideoSurfaceTarget Target { get; }
        public Transform ContentRoot { get; }
        public Vector2 AvailableSize { get; }
        public bool IsVisible { get; }
        public bool IsDisposed => _disposed;
        public void Dispose() { if(_disposed) return; _disposed=true; _release?.Invoke(); }
    }
}
