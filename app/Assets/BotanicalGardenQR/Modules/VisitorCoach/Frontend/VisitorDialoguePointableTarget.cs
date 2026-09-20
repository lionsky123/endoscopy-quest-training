using System;
using Oculus.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.VisitorCoach.Frontend
{
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
        readonly VisitorDialoguePressCommitGate _pressCommitGate = new VisitorDialoguePressCommitGate();

        public event Action Selected;

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
            RestoreFeedback();
            Unsubscribe();
        }
        void OnDestroy() => Unsubscribe();

        void OnApplicationFocus(bool focused)
        {
            ResetPress();
            RestoreFeedback();
        }

        void OnApplicationPause(bool paused)
        {
            ResetPress();
            RestoreFeedback();
        }

        public void SetArmed(bool armed)
        {
            _armed = armed;
            ResetPress();
            RestoreFeedback();
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
            PresentFeedback(pointerEvent.Type);
            if (!_pressCommitGate.TryCommit(pointerEvent)) return;
            Selected?.Invoke();
        }

        void ResetPress() => _pressCommitGate.Reset();

        void PresentFeedback(PointerEventType type)
        {
            switch (type)
            {
                case PointerEventType.Hover:
                    BlendFeedback(0.16f);
                    break;
                case PointerEventType.Select:
                    BlendFeedback(0.34f);
                    break;
                case PointerEventType.Unselect:
                    BlendFeedback(0.16f);
                    break;
                case PointerEventType.Unhover:
                case PointerEventType.Cancel:
                    RestoreFeedback();
                    break;
            }
        }

        void BlendFeedback(float whiteBlend)
        {
            EnsureFeedbackInitialized();
            if (_feedbackGraphic != null)
                _feedbackGraphic.color = Color.Lerp(_restColor, Color.white, whiteBlend);
        }

        void RestoreFeedback()
        {
            EnsureFeedbackInitialized();
            if (_feedbackGraphic != null) _feedbackGraphic.color = _restColor;
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
