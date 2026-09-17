using System;
using BotanicalGardenQR.Experience.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    public sealed class FrontendNarrationDockLease : IDisposable
    {
        readonly Action _release;
        bool _disposed;

        public FrontendNarrationDockLease(SessionToken session, Transform root, Vector2 size, Action release)
        {
            if (!session.IsValid) throw new ArgumentException("Narration docks require a valid session.", nameof(session));
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (size.x <= 0f || size.y <= 0f || float.IsNaN(size.x) || float.IsNaN(size.y) ||
                float.IsInfinity(size.x) || float.IsInfinity(size.y))
                throw new ArgumentOutOfRangeException(nameof(size));
            Session = session;
            Root = root;
            Size = size;
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public SessionToken Session { get; }
        public Transform Root { get; }
        public Vector2 Size { get; }
        public bool IsDisposed => _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _release();
        }
    }
}
