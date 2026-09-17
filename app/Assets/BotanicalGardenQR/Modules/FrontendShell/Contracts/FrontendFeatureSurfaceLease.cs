using System;
using BotanicalGardenQR.Experience.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    public sealed class FrontendFeatureSurfaceLease : IDisposable
    {
        readonly Action _release;
        bool _disposed;

        public FrontendFeatureSurfaceLease(
            SessionToken session,
            FeaturePageId feature,
            Transform mediaStageRoot,
            Vector3 mediaStageSize,
            Transform controlRoot,
            Vector2 controlSize,
            Action release)
        {
            if (!session.IsValid)
                throw new ArgumentException("Feature surfaces require a valid session.", nameof(session));
            if (!Enum.IsDefined(typeof(FeaturePageId), feature))
                throw new ArgumentOutOfRangeException(nameof(feature));
            if (mediaStageRoot == null)
                throw new ArgumentNullException(nameof(mediaStageRoot));
            if (!IsPositiveFinite(mediaStageSize))
                throw new ArgumentOutOfRangeException(nameof(mediaStageSize));
            if (controlRoot == null)
                throw new ArgumentNullException(nameof(controlRoot));
            if (!IsPositiveFinite(controlSize))
                throw new ArgumentOutOfRangeException(nameof(controlSize));

            Session = session;
            Feature = feature;
            MediaStageRoot = mediaStageRoot;
            MediaStageSize = mediaStageSize;
            ControlRoot = controlRoot;
            ControlSize = controlSize;
            _release = release ?? throw new ArgumentNullException(nameof(release));
        }

        public SessionToken Session { get; }
        public FeaturePageId Feature { get; }
        public Transform MediaStageRoot { get; }
        public Vector3 MediaStageSize { get; }
        public Transform ControlRoot { get; }
        public Vector2 ControlSize { get; }
        public bool IsDisposed => _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _release();
        }

        static bool IsPositiveFinite(Vector2 value)
            => IsPositiveFinite(value.x) && IsPositiveFinite(value.y);

        static bool IsPositiveFinite(Vector3 value)
            => IsPositiveFinite(value.x) && IsPositiveFinite(value.y) && IsPositiveFinite(value.z);

        static bool IsPositiveFinite(float value)
            => value > 0f && !float.IsInfinity(value);
    }
}
