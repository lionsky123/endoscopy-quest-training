using System;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;
using Oculus.Interaction.Input;
using UnityEngine;

namespace BotanicalGardenQR.VisitorAtlasHub.Frontend
{
    /// <summary>
    /// Reads the explicitly injected Interaction SDK hands and emits one
    /// UI-neutral palm assessment. Timing and one-shot commit state belong to
    /// the Hub controller, not this raw-input adapter.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AtlasHubPalmCandidateSource : MonoBehaviour, IDisposable
    {
        [SerializeField, Range(0.25f, 0.95f)] float _minimumPalmUpAlignment = 0.75f;
        [SerializeField, Min(0.1f)] float _minimumHandDrop = 0.12f;

        Hand[] _hands = Array.Empty<Hand>();
        bool _configured;
        bool _disposed;

        public float MinimumPalmUpAlignment => _minimumPalmUpAlignment;
        public float MinimumHandDrop => _minimumHandDrop;

        public void Configure(Transform interactionRigRoot)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(AtlasHubPalmCandidateSource));
            if (_configured) throw new InvalidOperationException("Atlas Hub palm source is already configured.");
            if (interactionRigRoot == null) throw new ArgumentNullException(nameof(interactionRigRoot));
            if (!IsFinite(_minimumPalmUpAlignment) || _minimumPalmUpAlignment < 0.25f ||
                _minimumPalmUpAlignment > 0.95f || !IsPositiveFinite(_minimumHandDrop))
                throw new InvalidOperationException("Atlas Hub palm geometry thresholds are invalid.");
            _hands = interactionRigRoot.GetComponentsInChildren<Hand>(true);
            if (_hands.Length == 0)
                throw new InvalidOperationException(
                    "Atlas Hub palm source found no current-SDK Hand data sources in the configured rig.");
            _configured = true;
        }

        public VisitorAtlasHubPalmSample Sample(float viewerHeight)
        {
            if (_disposed || !_configured || !IsFinite(viewerHeight))
                return new VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage.NoReliableHand, false);

            var best = new VisitorAtlasHubPalmSample(
                VisitorAtlasHubPalmStage.NoReliableHand,
                false);
            for (var index = 0; index < _hands.Length; index++)
            {
                var hand = _hands[index];
                if (hand == null) continue;
                var hasWristPose = hand.GetJointPose(HandJointId.HandWristRoot, out var wristPose);
                var candidate = Evaluate(
                    hand.IsConnected,
                    hand.IsTrackedDataValid,
                    hand.IsHighConfidence,
                    hand.GetIndexFingerIsPinching(),
                    hasWristPose,
                    hand.Handedness,
                    wristPose,
                    viewerHeight,
                    _minimumHandDrop,
                    _minimumPalmUpAlignment);
                best = MoreAdvanced(best, candidate);
            }
            return best;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _configured = false;
            _hands = Array.Empty<Hand>();
        }

        void OnDestroy() => Dispose();

        internal static VisitorAtlasHubPalmSample Evaluate(
            bool connected,
            bool trackedDataValid,
            bool highConfidence,
            bool indexPinching,
            bool hasWristPose,
            Handedness handedness,
            Pose wristPose,
            float viewerHeight,
            float minimumHandDrop,
            float minimumPalmUpAlignment)
        {
            if (!connected || !trackedDataValid || !highConfidence || !hasWristPose ||
                !IsFinite(wristPose.position) || !IsFinite(wristPose.rotation))
                return new VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage.NoReliableHand, false);

            // Pinch is valid tracked input, but explicitly disqualifies the
            // summon pose. Reporting it as reliable makes the backend clear
            // any accumulated hold instead of applying tracking-loss grace.
            if (indexPinching)
                return new VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage.TurnPalmUp, true);

            var localPalmar = handedness == Handedness.Left
                ? Constants.LeftPalmar
                : Constants.RightPalmar;
            if (Vector3.Dot(wristPose.rotation * localPalmar, Vector3.up) < minimumPalmUpAlignment)
                return new VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage.TurnPalmUp, true);

            if (wristPose.position.y > viewerHeight - minimumHandDrop)
                return new VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage.PositionAtChest, true);

            return new VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage.ReadyToSummon, true);
        }

        internal static VisitorAtlasHubPalmSample MoreAdvanced(
            VisitorAtlasHubPalmSample current,
            VisitorAtlasHubPalmSample candidate)
            => candidate.Stage > current.Stage ? candidate : current;

        static bool IsPositiveFinite(float value) => value > 0f && IsFinite(value);
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        static bool IsFinite(Quaternion value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }
}
