using System;
using UnityEngine;
namespace BotanicalGardenQR.Model.Contracts
{
    public sealed class ModelSurfaceLease : IDisposable
    {
        readonly Action _release;
        bool _disposed;
        public ModelSurfaceLease(Transform contentRoot,Vector3 availableSize,bool isVisible,Action release) { if(contentRoot==null) throw new ArgumentNullException(nameof(contentRoot)); if(!(availableSize.x>0)||!(availableSize.y>0)||!(availableSize.z>0)||float.IsInfinity(availableSize.x)||float.IsInfinity(availableSize.y)||float.IsInfinity(availableSize.z)) throw new ArgumentOutOfRangeException(nameof(availableSize)); ContentRoot=contentRoot; AvailableSize=availableSize; IsVisible=isVisible; _release=release; }
        public Transform ContentRoot { get; } public Vector3 AvailableSize { get; } public bool IsVisible { get; }
        public bool IsDisposed => _disposed;
        public void Dispose() { if(_disposed) return; _disposed=true; _release?.Invoke(); }
    }
}
