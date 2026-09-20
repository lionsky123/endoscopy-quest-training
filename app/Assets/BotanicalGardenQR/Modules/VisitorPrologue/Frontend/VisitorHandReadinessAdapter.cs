using System;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using Oculus.Interaction.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        Transform _viewer;
        TMP_FontAsset _font;
        GameObject _trackingNotice;
        bool _reportedReliable;
        bool _configured;

        public void Configure(
            Transform interactionRigRoot,
            IVisitorPrologue prologue,
            float gazeFallbackDelaySeconds,
            Transform viewer = null,
            TMP_FontAsset font = null)
        {
            if (_configured) throw new InvalidOperationException("Hand readiness adapter is already configured.");
            if (interactionRigRoot == null) throw new ArgumentNullException(nameof(interactionRigRoot));
            _prologue = prologue ?? throw new ArgumentNullException(nameof(prologue));
            if (gazeFallbackDelaySeconds <= 0f || float.IsNaN(gazeFallbackDelaySeconds) || float.IsInfinity(gazeFallbackDelaySeconds))
                throw new ArgumentOutOfRangeException(nameof(gazeFallbackDelaySeconds));
            _viewer = viewer;
            _font = font;
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
            _viewer = null;
            _font = null;
            if (_trackingNotice)
            {
                if (Application.isPlaying) Destroy(_trackingNotice);
                else DestroyImmediate(_trackingNotice);
                _trackingNotice = null;
            }
            enabled = false;
        }

        void Update()
        {
            if (!_configured || _prologue == null) return;
            var reliableNow = false;
            var sourcesActive = false;
            for (var index = 0; index < _hands.Length; index++)
            {
                var hand = _hands[index];
                sourcesActive |= hand != null && hand.isActiveAndEnabled;
                if (hand != null && hand.isActiveAndEnabled && hand.IsConnected && hand.IsTrackedDataValid && hand.IsHighConfidence)
                {
                    reliableNow = true;
                    break;
                }
            }

            // The world-tracking guard disables the interaction rig and owns its
            // recovery notice. Do not stack a hand notice over that interruption.
            if (!sourcesActive)
            {
                _noHandDuration = _reliableDuration = _lostDuration = 0f;
                SetTrackingNoticeVisible(false);
                return;
            }
            UpdateAvailability(reliableNow, Time.unscaledDeltaTime);
        }

        void UpdateAvailability(bool reliableNow, float seconds)
        {
            seconds = Mathf.Max(0, seconds);
            if (reliableNow)
            {
                _reliableDuration += seconds;
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
                _lostDuration += seconds;
                _noHandDuration += seconds;
                if (_reportedReliable && _lostDuration >= _lostGraceSeconds)
                {
                    _reportedReliable = false;
                    _prologue.ReportHandAvailability(false);
                }
                // VR uses tracked hands throughout; loss of tracking must never enable a second input mode.
            }

            var phase = _prologue.CurrentState.Phase;
            // Invitation already carries this instruction. Continue monitoring after
            // the encounter, when every lesson and departure still depends on hands.
            var showNotice = !reliableNow && _noHandDuration >= 2f &&
                (phase == VisitorProloguePhase.Encounter || phase == VisitorProloguePhase.ExplorationIdle);
            SetTrackingNoticeVisible(showNotice);
        }

        void SetTrackingNoticeVisible(bool visible)
        {
            if (!visible) { if (_trackingNotice) _trackingNotice.SetActive(false); return; }
            if (!_viewer || !_font || (_trackingNotice && _trackingNotice.activeSelf)) return;
            if (!_trackingNotice)
            {
                _trackingNotice = new GameObject("HandTrackingRecoveryNotice", typeof(RectTransform), typeof(Canvas), typeof(Image));
                var rect = (RectTransform)_trackingNotice.transform;
                rect.sizeDelta = new Vector2(800, 110);
                var canvas = _trackingNotice.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace; canvas.overrideSorting = true; canvas.sortingOrder = 600;
                canvas.worldCamera = _viewer.GetComponent<Camera>();
                var background = _trackingNotice.GetComponent<Image>();
                background.color = new Color(.025f, .04f, .06f, .96f); background.raycastTarget = false;
                var label = new GameObject("RecoveryMessage", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(rect, false);
                ((RectTransform)label.transform).sizeDelta = new Vector2(760, 90);
                var text = label.GetComponent<TextMeshProUGUI>();
                text.font = _font; text.fontSize = 28; text.color = Color.white;
                text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
                text.text = "暂未识别到双手\n请把双手放到视野前方，恢复后继续。";
            }
            var forward = Vector3.ProjectOnPlane(_viewer.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f) forward = Vector3.forward;
            var position = _viewer.position + forward.normalized * .55f + Vector3.up * .26f;
            _trackingNotice.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - _viewer.position, Vector3.up));
            _trackingNotice.transform.localScale = Vector3.one * .0005f;
            _trackingNotice.SetActive(true);
        }

        void OnDisable() => SetTrackingNoticeVisible(false);

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
