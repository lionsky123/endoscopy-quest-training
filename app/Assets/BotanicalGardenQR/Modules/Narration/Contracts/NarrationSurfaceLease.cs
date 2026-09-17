using System;
using UnityEngine;
namespace BotanicalGardenQR.Narration.Contracts
{
    public sealed class NarrationSurfaceLease : IDisposable
    {
        readonly Action _release;
        bool _disposed;
        public NarrationSurfaceLease(Transform contentRoot,Vector2 availableSize,bool isVisible,Action release) { if(contentRoot==null) throw new ArgumentNullException(nameof(contentRoot)); if(!(availableSize.x>0)||!(availableSize.y>0)||float.IsInfinity(availableSize.x)||float.IsInfinity(availableSize.y)) throw new ArgumentOutOfRangeException(nameof(availableSize)); ContentRoot=contentRoot; AvailableSize=availableSize; IsVisible=isVisible; _release=release; }
        public Transform ContentRoot { get; } public Vector2 AvailableSize { get; } public bool IsVisible { get; }
        public bool IsDisposed => _disposed;
        public void Dispose() { if(_disposed) return; _disposed=true; _release?.Invoke(); }
    }
}
