using System;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using Oculus.Interaction.Input;
using UnityEngine;

namespace BotanicalGardenQR.VisitorPrologue.Frontend
{
    /// <summary>
    /// Bounded adapter over the explicitly configured current-SDK interaction
    /// rig. It reports hand facts only; the prologue owns all transitions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisitorHandReadinessAdapter : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] float _reliableHoldSeconds = 0.15f;
        [SerializeField, Min(0.05f)] float _lostGraceSeconds = 0.35f;

        IVisitorPrologue _prologue;
        Hand[] _hands = Array.Empty<Hand>();
        float _reliableDuration;
        float _lostDuration;
        float _noHandDuration;
        float _gazeFallbackDelay;
        bool _reportedReliable;
        bool _configured;

        public void Configure(
            Transform interactionRigRoot,
            IVisitorPrologue prologue,
            float gazeFallbackDelaySeconds)
        {
            if (_configured) throw new InvalidOperationException("Hand readiness adapter is already configured.");
            if (interactionRigRoot == null) throw new ArgumentNullException(nameof(interactionRigRoot));
            _prologue = prologue ?? throw new ArgumentNullException(nameof(prologue));
            if (gazeFallbackDelaySeconds <= 0f || float.IsNaN(gazeFallbackDelaySeconds) || float.IsInfinity(gazeFallbackDelaySeconds))
                throw new ArgumentOutOfRangeException(nameof(gazeFallbackDelaySeconds));
            _gazeFallbackDelay = gazeFallbackDelaySeconds;
            _hands = interactionRigRoot.GetComponentsInChildren<Hand>(true);
            if (_hands.Length == 0)
                throw new InvalidOperationException("The configured Interaction SDK rig contains no current-SDK Hand data sources.");
            _configured = true;
            enabled = true;
        }

        public void Unconfigure()
        {
            _configured = false;
            _prologue = null;
            _hands = Array.Empty<Hand>();
            _reliableDuration = 0f;
            _lostDuration = 0f;
            _noHandDuration = 0f;
            _reportedReliable = false;
            enabled = false;
        }

        void Update()
        {
            if (!_configured || _prologue == null) return;
            var reliableNow = false;
            for (var index = 0; index < _hands.Length; index++)
            {
                var hand = _hands[index];
                if (hand != null && hand.IsConnected && hand.IsTrackedDataValid && hand.IsHighConfidence)
                {
                    reliableNow = true;
                    break;
                }
            }

            if (reliableNow)
            {
                _reliableDuration += Time.unscaledDeltaTime;
                _lostDuration = 0f;
                _noHandDuration = 0f;
                // Retry starts a new prologue epoch with an intentionally empty
                // hand fact. Keep the current real hand state authoritative so
                // a hand that never left the camera can re-enable the new epoch.
                if (_reliableDuration >= _reliableHoldSeconds &&
                    (!_reportedReliable || !_prologue.CurrentState.HasReliableHand))
                {
                    _reportedReliable = true;
                    _prologue.ReportHandAvailability(true);
                }
            }
            else
            {
                _reliableDuration = 0f;
                _lostDuration += Time.unscaledDeltaTime;
                _noHandDuration += Time.unscaledDeltaTime;
                if (_reportedReliable && _lostDuration >= _lostGraceSeconds)
                {
                    _reportedReliable = false;
                    _prologue.ReportHandAvailability(false);
                }
                // Endoscopy retains real hands as the only invitation input.
            }

            if (_prologue.CurrentState.IsExplorationReady) enabled = false;
        }

        internal int FindPalmAtSeal(Transform seal, float radius, float alignment, int preferredHand)
        {
            if (!_configured || seal == null || !seal.gameObject.activeInHierarchy) return -1;
            var candidate = -1;
            for (int index = 0; index < _hands.Length; index++)
            {
                var hand = _hands[index];
                if (hand == null || !hand.IsConnected || !hand.IsTrackedDataValid || !hand.IsHighConfidence ||
                    hand.GetIndexFingerIsPinching() || !hand.GetJointPose(HandJointId.HandWristRoot, out var wrist)) continue;
                var palmar = wrist.rotation * (hand.Handedness == Handedness.Left ? Constants.LeftPalmar : Constants.RightPalmar);
                // The same SDK wrist orientation used by the Hub supplies the palmar normal.
                // Contact is spatial and hand-specific; tracking availability alone never accepts an invitation.
                if (!Finite(wrist.position) || !Finite(palmar) ||
                    Vector3.Distance(wrist.position, seal.position) > radius ||
                    Vector3.Dot(palmar, seal.forward) < alignment) continue;
                if (index == preferredHand) return index;
                if (candidate < 0) candidate = index;
            }
            return candidate;
        }

        public Vector3? GetArrivalHandPosition(Vector3 target)
        {
            if (!_configured) return null;
            Vector3? closest = null; var distance = float.PositiveInfinity;
            for (var index = 0; index < _hands.Length; index++)
            {
                var hand = _hands[index];
                if (hand == null || !hand.IsConnected || !hand.IsTrackedDataValid || !hand.IsHighConfidence ||
                    !hand.GetJointPose(HandJointId.HandWristRoot, out var wrist) || !Finite(wrist.position)) continue;
                var point = wrist.position;
                if (hand.GetJointPose(HandJointId.HandIndexTip, out var tip) && Finite(tip.position) &&
                    Vector3.Distance(target, tip.position) < Vector3.Distance(target, point)) point = tip.position;
                if (hand.GetJointPose(HandJointId.HandMiddleTip, out var middle) && Finite(middle.position) &&
                    Vector3.Distance(target, middle.position) < Vector3.Distance(target, point)) point = middle.position;
                var current = Vector3.Distance(target, point);
                if (current < distance) { closest = point; distance = current; }
            }
            // A touch has no held-hand owner. Either reliable hand may make contact.
            return closest;
        }

        static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) &&
            !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        void OnDestroy() => Unconfigure();
    }
}
