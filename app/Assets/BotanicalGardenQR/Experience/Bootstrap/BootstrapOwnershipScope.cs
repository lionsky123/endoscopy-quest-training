using System;
using System.Collections.Generic;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed class BootstrapOwnershipScope : IDisposable
    {
        readonly List<Action> _releaseActions = new List<Action>();
        bool _disposed;
        bool _transferred;

        public void Register(Action release)
        {
            if (release == null) throw new ArgumentNullException(nameof(release));
            ThrowIfUnavailable();
            _releaseActions.Add(release);
        }

        public void Adopt(BootstrapOwnershipScope child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (ReferenceEquals(child, this)) throw new ArgumentException("An ownership scope cannot adopt itself.", nameof(child));
            ThrowIfUnavailable();
            child.TransferTo(_releaseActions);
        }

        public void Dispose()
        {
            if (_disposed || _transferred) return;
            _disposed = true;
            for (var index = _releaseActions.Count - 1; index >= 0; index--)
            {
                try
                {
                    _releaseActions[index]();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            _releaseActions.Clear();
        }

        void TransferTo(List<Action> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            ThrowIfUnavailable();
            destination.AddRange(_releaseActions);
            _releaseActions.Clear();
            _transferred = true;
        }

        void ThrowIfUnavailable()
        {
            if (_disposed || _transferred)
                throw new ObjectDisposedException(nameof(BootstrapOwnershipScope));
        }
    }
}
