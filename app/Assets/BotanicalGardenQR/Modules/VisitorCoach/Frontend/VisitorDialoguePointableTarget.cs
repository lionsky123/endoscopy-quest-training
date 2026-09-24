using System;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.VisitorCoach.Frontend
{
    internal enum VisitorDialogueButtonVisualState { Resting, Approaching, Pressed, Triggered }

    [DisallowMultipleComponent]
    public sealed class VisitorDialoguePointableTarget : MonoBehaviour
    {
        [SerializeField, Interface(typeof(IPointable))] UnityEngine.Object _pointableObject;
        [SerializeField] Collider[] _hitVolumes = Array.Empty<Collider>();
        [SerializeField] Graphic _feedbackGraphic;

        IPointable _pointable;
        Color _restColor;
        bool _subscribed;
        bool _armed;
        bool _feedbackInitialized;
        VisitorDialogueButtonVisualState _visualState;
        IHand[] _trackedHands = Array.Empty<IHand>();
        bool[] _fingerPrimed = Array.Empty<bool>();
        bool[] _fingerCommitted = Array.Empty<bool>();
        bool[] _fingerPressed = Array.Empty<bool>();
        readonly VisitorDialoguePressCommitGate _pressCommitGate = new VisitorDialoguePressCommitGate();

        public event Action Selected;
        internal VisitorDialogueButtonVisualState VisualState => _visualState;

        public void BindTrackedHands(IHand[] hands)
        {
            _trackedHands = hands ?? Array.Empty<IHand>();
            _fingerPrimed = new bool[_trackedHands.Length];
            _fingerCommitted = new bool[_trackedHands.Length];
            _fingerPressed = new bool[_trackedHands.Length];
            PresentVisualState(VisitorDialogueButtonVisualState.Resting);
        }

        public void TickTrackedHands()
        {
            if (!Application.isPlaying || Application.platform != RuntimePlatform.Android ||
                !_armed || !isActiveAndEnabled || _trackedHands.Length == 0) return;
            for (var i = 0; i < _trackedHands.Length; i++)
            {
                var hand = _trackedHands[i];
                if (hand == null || !hand.IsConnected || !hand.IsTrackedDataValid ||
                    !hand.GetJointPose(HandJointId.HandIndexTip, out var tip) ||
                    !Finite(tip.position))
                {
                    ResetFinger(i);
                    continue;
                }
                SampleTrackedFinger(i, tip.position);
            }
        }

        internal void PresentTrackedFingerHover(bool hovering)
        {
            PresentVisualState(hovering
                ? VisitorDialogueButtonVisualState.Approaching
                : VisitorDialogueButtonVisualState.Resting);
        }

        internal bool SampleTrackedFinger(int index, Vector3 worldPoint)
        {
            if (!_armed || !isActiveAndEnabled || index < 0 || index >= _fingerPrimed.Length ||
                !Finite(worldPoint)) return false;
            var rect = (RectTransform)transform;
            var bounds = rect.rect;
            var scale = rect.lossyScale;
            if (Mathf.Abs(scale.x) < 0.000001f || Mathf.Abs(scale.y) < 0.000001f ||
                Mathf.Abs(scale.z) < 0.000001f)
            {
                ResetFinger(index);
                RefreshTrackedVisualState();
                return false;
            }
            var local = rect.InverseTransformPoint(worldPoint);
            // Authored dialogue surfaces face local -Z, toward the viewer.
            var depth = -local.z * scale.z;
            var marginX = 0.006f / Mathf.Abs(scale.x);
            var marginY = 0.006f / Mathf.Abs(scale.y);
            var inside = local.x >= bounds.xMin - marginX && local.x <= bounds.xMax + marginX &&
                         local.y >= bounds.yMin - marginY && local.y <= bounds.yMax + marginY;
            if (!inside || depth > 0.09f || depth < -0.06f)
            {
                ResetFinger(index);
                RefreshTrackedVisualState();
                return false;
            }
            if (depth >= 0.008f)
            {
                _fingerPrimed[index] = true;
                if (depth >= 0.025f)
                {
                    _fingerCommitted[index] = false;
                    _fingerPressed[index] = false;
                }
            }
            if (_fingerPrimed[index] && !_fingerCommitted[index] && depth <= -0.003f)
            {
                _fingerPrimed[index] = false;
                _fingerCommitted[index] = true;
                _fingerPressed[index] = false;
                PresentVisualState(VisitorDialogueButtonVisualState.Triggered);
                Selected?.Invoke();
            }
            else if (_fingerCommitted[index])
            {
                PresentVisualState(VisitorDialogueButtonVisualState.Triggered);
            }
            else
            {
                _fingerPressed[index] = _fingerPrimed[index] && depth <= 0.008f;
                RefreshTrackedVisualState();
            }
            return depth >= -0.06f && depth <= 0.09f;
        }

        void ResetFinger(int index)
        {
            _fingerPrimed[index] = false;
            _fingerCommitted[index] = false;
            _fingerPressed[index] = false;
        }

        void RefreshTrackedVisualState()
        {
            if (Array.Exists(_fingerCommitted, committed => committed))
                PresentVisualState(VisitorDialogueButtonVisualState.Triggered);
            else if (Array.Exists(_fingerPressed, pressed => pressed))
                PresentVisualState(VisitorDialogueButtonVisualState.Pressed);
            else if (Array.Exists(_fingerPrimed, primed => primed))
                PresentVisualState(VisitorDialogueButtonVisualState.Approaching);
            else
                PresentVisualState(VisitorDialogueButtonVisualState.Resting);
        }

        static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        public void SetFeedbackGraphic(Graphic graphic)
        {
            if (graphic == null) throw new ArgumentNullException(nameof(graphic));
            _feedbackGraphic = graphic;
            _feedbackInitialized = false;
            EnsureFeedbackInitialized();
        }

        void Awake()
        {
            _pointable = _pointableObject as IPointable;
            if (_pointable == null)
                throw new InvalidOperationException("Visitor dialogue target requires an authored IPointable.");
            if (_hitVolumes == null || _hitVolumes.Length == 0)
                throw new InvalidOperationException("Visitor dialogue target requires an authored hit volume.");
            if (_feedbackGraphic == null)
                throw new InvalidOperationException("Visitor dialogue target requires an authored feedback graphic.");
            EnsureFeedbackInitialized();
            // A presenter can configure an inactive prefab before its first Awake.
            // Preserve that requested state when the parent is finally activated.
            SetArmed(_armed);
        }

        void OnEnable()
        {
            ResetPress();
            Subscribe();
        }
        void OnDisable()
        {
            // The hand's Cancel can arrive after this target has unsubscribed.
            // A press must never survive a panel/application lifecycle boundary.
            ResetPress();
            Unsubscribe();
        }
        void OnDestroy() => Unsubscribe();

        void OnApplicationFocus(bool focused)
        {
            ResetPress();
        }

        void OnApplicationPause(bool paused)
        {
            ResetPress();
        }

        public void SetArmed(bool armed)
        {
            _armed = armed;
            ResetPress();
            if (_pointableObject is Behaviour behaviour) behaviour.enabled = armed;
            for (var index = 0; index < _hitVolumes.Length; index++)
                if (_hitVolumes[index] != null) _hitVolumes[index].enabled = armed;
        }

        void Subscribe()
        {
            if (_subscribed || _pointable == null) return;
            _pointable.WhenPointerEventRaised += HandlePointerEvent;
            _subscribed = true;
        }

        void Unsubscribe()
        {
            if (!_subscribed || _pointable == null) return;
            _pointable.WhenPointerEventRaised -= HandlePointerEvent;
            _subscribed = false;
        }

        void HandlePointerEvent(PointerEvent pointerEvent)
        {
            if (!_armed) return;
            switch (pointerEvent.Type)
            {
                case PointerEventType.Hover:
                    if (!_pressCommitGate.IsCommitted)
                        PresentVisualState(VisitorDialogueButtonVisualState.Approaching);
                    break;
                case PointerEventType.Select:
                    if (_pressCommitGate.IsCommitted) return;
                    PresentVisualState(VisitorDialogueButtonVisualState.Pressed);
                    if (_pressCommitGate.TryCommit(pointerEvent))
                    {
                        PresentVisualState(VisitorDialogueButtonVisualState.Triggered);
                        Selected?.Invoke();
                    }
                    break;
                case PointerEventType.Unselect:
                    _pressCommitGate.TryCommit(pointerEvent);
                    PresentVisualState(VisitorDialogueButtonVisualState.Approaching);
                    break;
                case PointerEventType.Unhover:
                case PointerEventType.Cancel:
                    _pressCommitGate.TryCommit(pointerEvent);
                    PresentVisualState(VisitorDialogueButtonVisualState.Resting);
                    break;
            }
        }

        void ResetPress()
        {
            _pressCommitGate.Reset();
            Array.Clear(_fingerPrimed, 0, _fingerPrimed.Length);
            Array.Clear(_fingerCommitted, 0, _fingerCommitted.Length);
            Array.Clear(_fingerPressed, 0, _fingerPressed.Length);
            PresentVisualState(VisitorDialogueButtonVisualState.Resting);
        }

        void PresentVisualState(VisitorDialogueButtonVisualState state)
        {
            _visualState = state;
            EnsureFeedbackInitialized();
            if (_feedbackGraphic is DialogueContractGraphic contract)
            {
                contract.PresentInteractionState(state);
                return;
            }
            if (_feedbackGraphic == null) return;
            var blend = state switch
            {
                VisitorDialogueButtonVisualState.Approaching => .16f,
                VisitorDialogueButtonVisualState.Pressed => .36f,
                VisitorDialogueButtonVisualState.Triggered => .62f,
                _ => 0f
            };
            _feedbackGraphic.color = Color.Lerp(_restColor, Color.white, blend);
        }

        void EnsureFeedbackInitialized()
        {
            if (_feedbackInitialized || _feedbackGraphic == null) return;
            _restColor = _feedbackGraphic.color;
            _feedbackInitialized = true;
        }
    }

    internal sealed class VisitorDialoguePressCommitGate
    {
        bool _committed;
        int _committedPointerId;
        public bool IsCommitted => _committed;

        public bool TryCommit(PointerEvent pointerEvent)
        {
            if (_committed)
            {
                if (pointerEvent.Identifier == _committedPointerId &&
                    (pointerEvent.Type == PointerEventType.Unselect ||
                     pointerEvent.Type == PointerEventType.Cancel))
                    Reset();
                return false;
            }
            if (pointerEvent.Type != PointerEventType.Select) return false;
            _committed = true;
            _committedPointerId = pointerEvent.Identifier;
            return true;
        }

        public void Reset()
        {
            _committed = false;
            _committedPointerId = 0;
        }
    }
}
