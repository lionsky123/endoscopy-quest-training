using System;
using UnityEngine;

namespace BotanicalGardenQR.SpatialHost.Contracts
{
    [CreateAssetMenu(menuName = "Botanical Garden QR/Display Profile", fileName = "DisplayProfile")]
    public sealed class DisplayProfile : ScriptableObject
    {
        [SerializeField] HostMode _hostMode = HostMode.ViewerFront;
        [SerializeField, Min(0.1f)] float _distance = 1.65f;
        [SerializeField] float _height;
        [SerializeField, Min(0.01f)] float _scale = 1f;
        [SerializeField] HostOrientation _orientation = HostOrientation.FaceViewerUpright;
        [SerializeField] SourceLostPolicy _sourceLost = SourceLostPolicy.KeepLastPose;
        [SerializeField, Min(0f)] float _sourceLostGraceSeconds = 1.5f;
        [SerializeField] RecallPolicy _recall = new RecallPolicy(true, false);
        [SerializeField] EnvironmentFallback _environmentFallback = EnvironmentFallback.ViewerFront;

        public HostMode HostMode => _hostMode;
        public float Distance => _distance;
        public float Height => _height;
        public float Scale => _scale;
        public HostOrientation Orientation => _orientation;
        public SourceLostPolicy SourceLost => _sourceLost;
        public float SourceLostGraceSeconds => _sourceLostGraceSeconds;
        public RecallPolicy Recall => _recall;
        public EnvironmentFallback EnvironmentFallback => _environmentFallback;

        public bool IsValid(out string reason)
        {
            if (_distance <= 0f || !IsFinite(_distance))
            {
                reason = "Distance must be finite and greater than zero.";
                return false;
            }
            if (_scale <= 0f || !IsFinite(_scale) || !IsFinite(_height) ||
                !IsFinite(_sourceLostGraceSeconds) || _sourceLostGraceSeconds < 0f)
            {
                reason = "Display profile numeric values are invalid.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public enum HostMode
    {
        ViewerFront = 1,
        WorldFixed = 2,
        AnchorFixed = 3,
        HeadFollow = 4,
        EnvironmentPlaced = 5
    }

    public enum HostOrientation
    {
        FaceViewerUpright = 1,
        PreserveEvidenceRotation = 2
    }

    public enum SourceLostPolicy
    {
        KeepLastPose = 1,
        Hide = 2,
        Close = 3
    }

    public enum EnvironmentFallback
    {
        None = 0,
        ViewerFront = 1,
        KeepLastPose = 2
    }

    [Serializable]
    public struct RecallPolicy
    {
        [SerializeField] bool _allowRecall;
        [SerializeField] bool _requireLiveSpatialEvidence;

        public RecallPolicy(bool allowRecall, bool requireLiveSpatialEvidence)
        {
            _allowRecall = allowRecall;
            _requireLiveSpatialEvidence = requireLiveSpatialEvidence;
        }

        public bool AllowRecall => _allowRecall;
        public bool RequireLiveSpatialEvidence => _requireLiveSpatialEvidence;
    }
}
