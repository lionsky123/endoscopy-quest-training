using System;
using System.Threading;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.SpatialHost.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.SpatialHost.Runtime
{
    public sealed class SpatialDisplayHost : ISpatialDisplayHost
    {
        readonly Transform _viewer;
        readonly Transform _root;
        readonly int _mainThreadId;
        SessionToken _currentSession;
        long _prepareGeneration;
        bool _hasPendingLease;
        DisplayProfile _pendingProfile;
        DisplayProfile _currentProfile;
        DateTimeOffset? _sourceLostDeadline;
        bool _hiddenBySourcePolicy;

        internal SpatialDisplayHost(Transform viewer, Transform root, int mainThreadId)
        {
            _viewer = viewer != null ? viewer : throw new ArgumentNullException(nameof(viewer));
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            if (_viewer == _root || _viewer.IsChildOf(_root))
                throw new ArgumentException("The display root must not own the viewer hierarchy.", nameof(root));
            if (mainThreadId <= 0)
                throw new ArgumentOutOfRangeException(nameof(mainThreadId));

            _mainThreadId = mainThreadId;
            _root.gameObject.SetActive(false);
        }

        public HostPrepareResult Prepare(
            SessionToken candidate,
            DisplayProfile profile,
            SpatialEvidence? spatialEvidence)
        {
            RequireMainThread();
            if (!candidate.IsValid)
                return HostPrepareResult.Reject(HostFailure.StaleSession);
            if (!IsValid(profile))
                return HostPrepareResult.Reject(HostFailure.InvalidProfile);

            PlacementCandidate placement;
            switch (profile.HostMode)
            {
                case HostMode.ViewerFront:
                    placement = ViewerFrontPlacement.Create(_viewer, profile, spatialEvidence);
                    break;
                case HostMode.WorldFixed:
                    placement = WorldFixedPlacement.Create(_viewer, profile, spatialEvidence);
                    break;
                case HostMode.AnchorFixed:
                    if (!spatialEvidence.HasValue)
                        return HostPrepareResult.Reject(HostFailure.SpatialEvidenceRequired);
                    if (!PlacementOrientation.IsUsable(spatialEvidence.Value))
                        return HostPrepareResult.Reject(HostFailure.SpatialEvidenceInvalid);
                    placement = AnchorFixedPlacement.Create(_viewer, profile, spatialEvidence.Value);
                    break;
                case HostMode.HeadFollow:
                    placement = HeadFollowPlacement.Create(_viewer, profile, spatialEvidence);
                    break;
                case HostMode.EnvironmentPlaced:
                    if (!EnvironmentPlacement.TryCreateFallback(
                            _viewer,
                            _root,
                            _currentSession.IsValid,
                            profile,
                            out placement))
                        return HostPrepareResult.Reject(HostFailure.EnvironmentUnavailable);
                    break;
                default:
                    return HostPrepareResult.Reject(HostFailure.InvalidProfile);
            }

            var generation = ++_prepareGeneration;
            _hasPendingLease = true;
            _pendingProfile = profile;
            return HostPrepareResult.Success(
                new PreparedHostLease(this, generation, candidate, placement));
        }

        public HostResult Close(SessionToken session)
        {
            RequireMainThread();
            if (!session.IsValid || session != _currentSession)
                return HostResult.Reject(HostFailure.StaleSession);

            ++_prepareGeneration;
            _hasPendingLease = false;
            _pendingProfile = null;
            _root.gameObject.SetActive(false);
            _currentSession = default;
            _currentProfile = null;
            _sourceLostDeadline = null;
            _hiddenBySourcePolicy = false;
            return HostResult.Success;
        }

        public HostResult UpdateSourceTracking(SessionToken session, TrackingState trackingState, DateTimeOffset observedAt)
        {
            RequireMainThread();
            if (!session.IsValid || session != _currentSession) return HostResult.Reject(HostFailure.StaleSession);
            if (trackingState == TrackingState.Lost)
                _sourceLostDeadline = observedAt.AddSeconds(_currentProfile.SourceLostGraceSeconds);
            else
            {
                _sourceLostDeadline = null;
                if (_hiddenBySourcePolicy) { _root.gameObject.SetActive(true); _hiddenBySourcePolicy = false; }
            }
            return HostResult.Success;
        }

        public HostSourcePolicyResult Tick(SessionToken session, DateTimeOffset now)
        {
            RequireMainThread();
            if (!session.IsValid || session != _currentSession) return HostSourcePolicyResult.Reject(HostFailure.StaleSession);
            if (!_sourceLostDeadline.HasValue || now < _sourceLostDeadline.Value) return HostSourcePolicyResult.NoChange;
            _sourceLostDeadline = null;
            switch (_currentProfile.SourceLost)
            {
                case SourceLostPolicy.KeepLastPose: return HostSourcePolicyResult.NoChange;
                case SourceLostPolicy.Hide:
                    _root.gameObject.SetActive(false); _hiddenBySourcePolicy = true;
                    return HostSourcePolicyResult.Changed(HostSourcePolicyAction.Hidden);
                case SourceLostPolicy.Close: return HostSourcePolicyResult.Changed(HostSourcePolicyAction.CloseRequested);
                default: return HostSourcePolicyResult.Reject(HostFailure.InvalidProfile);
            }
        }

        internal void Commit(long generation, SessionToken session, PlacementCandidate candidate)
        {
            RequireMainThread();
            if (!_hasPendingLease || generation != _prepareGeneration)
                throw new InvalidOperationException("A superseded host lease cannot be committed.");

            if (candidate.LocalSpace)
            {
                _root.SetParent(candidate.Parent, false);
                _root.localPosition = candidate.Position;
                _root.localRotation = candidate.Rotation;
            }
            else
            {
                _root.SetParent(null, true);
                _root.SetPositionAndRotation(candidate.Position, candidate.Rotation);
            }

            _root.localScale = Vector3.one * candidate.Scale;
            _root.gameObject.SetActive(true);
            _currentSession = session;
            _currentProfile = _pendingProfile;
            _pendingProfile = null;
            _sourceLostDeadline = null;
            _hiddenBySourcePolicy = false;
            _hasPendingLease = false;
        }

        internal void Cancel(long generation)
        {
            RequireMainThread();
            if (_hasPendingLease && generation == _prepareGeneration)
            {
                _hasPendingLease = false;
                _pendingProfile = null;
            }
        }

        void RequireMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("SpatialDisplayHost must be called on the configured Unity main thread.");
        }

        static bool IsValid(DisplayProfile profile)
            => profile != null &&
               profile.IsValid(out _) &&
               Enum.IsDefined(typeof(HostMode), profile.HostMode) &&
               Enum.IsDefined(typeof(HostOrientation), profile.Orientation) &&
               Enum.IsDefined(typeof(SourceLostPolicy), profile.SourceLost) &&
               Enum.IsDefined(typeof(EnvironmentFallback), profile.EnvironmentFallback);
    }
}
