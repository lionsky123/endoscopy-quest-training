using System;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace BotanicalGardenQR.VisitorAtlasHub.Frontend
{
    /// <summary>Converts one real Poke Select into one semantic Hub intent.</summary>
    [DisallowMultipleComponent]
    public sealed class VisitorAtlasHubPointableRelay : MonoBehaviour, IDisposable
    {
        [SerializeField, Interface(typeof(IPointable))] UnityEngine.Object _pointableObject;
        [SerializeField] Collider[] _hitVolumes = Array.Empty<Collider>();

        IPointable _pointable;
        int _committedPointer;
        bool _hasCommittedPointer;
        bool _subscribed;
        bool _armed;
        bool _configured;
        bool _disposed;
        readonly HashSet<int> _hovering = new HashSet<int>();
        readonly HashSet<int> _selecting = new HashSet<int>();
        public bool IsHandEngaged => _hovering.Count != 0 || _selecting.Count != 0;
        public event Action HandEngagementChanged;

        public event Action Selected;

        public void Configure()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisitorAtlasHubPointableRelay));
            if (_configured) throw new InvalidOperationException("Atlas Hub pointable relay is already configured.");
            _pointable = _pointableObject as IPointable;
            if (_pointable == null)
                throw new InvalidOperationException("Atlas Hub pointable relay requires an Interaction SDK IPointable.");
            if (_hitVolumes == null || _hitVolumes.Length != 1 || _hitVolumes[0] == null)
                throw new InvalidOperationException("Atlas Hub pointable relay requires exactly one authored hit volume.");
            _pointable.WhenPointerEventRaised += HandlePointerEvent;
            _subscribed = true;
            _configured = true;
            SetArmed(false);
        }

        public void SetArmed(bool armed)
        {
            if (_disposed) return;
            _armed = armed;
            if (!armed) { ResetPress(); ClearHandEngagement(); }
            if (_pointableObject is Behaviour behaviour) behaviour.enabled = armed;
            if (_hitVolumes != null)
                for (var index = 0; index < _hitVolumes.Length; index++)
                    if (_hitVolumes[index] != null) _hitVolumes[index].enabled = armed;
        }

        public void Dispose()
        {
            if (_disposed) return;
            SetArmed(false);
            _disposed = true;
            if (_subscribed && _pointable != null)
                _pointable.WhenPointerEventRaised -= HandlePointerEvent;
            _subscribed = false;
            _configured = false;
            _pointable = null;
            Selected = null;
            HandEngagementChanged = null;
        }

        void OnDestroy() => Dispose();
        void OnDisable() { ResetPress(); ClearHandEngagement(); }

        void HandlePointerEvent(PointerEvent pointerEvent)
        {
            if (!_armed) return;
            var wasEngaged = IsHandEngaged;
            switch (pointerEvent.Type)
            {
                case PointerEventType.Hover: _hovering.Add(pointerEvent.Identifier); break;
                case PointerEventType.Select: _selecting.Add(pointerEvent.Identifier); break;
                case PointerEventType.Unhover: _hovering.Remove(pointerEvent.Identifier); break;
                case PointerEventType.Unselect: _selecting.Remove(pointerEvent.Identifier); break;
                case PointerEventType.Cancel:
                    _hovering.Remove(pointerEvent.Identifier);
                    _selecting.Remove(pointerEvent.Identifier);
                    break;
            }
            if (wasEngaged != IsHandEngaged) HandEngagementChanged?.Invoke();
            if (!_armed || _disposed) return;
            if (_hasCommittedPointer)
            {
                if (pointerEvent.Identifier == _committedPointer &&
                    (pointerEvent.Type == PointerEventType.Unselect || pointerEvent.Type == PointerEventType.Cancel))
                    ResetPress();
                return;
            }
            if (pointerEvent.Type != PointerEventType.Select) return;
            _hasCommittedPointer = true;
            _committedPointer = pointerEvent.Identifier;
            Selected?.Invoke();
        }

        void ResetPress()
        {
            _hasCommittedPointer = false;
            _committedPointer = 0;
        }

        void ClearHandEngagement()
        {
            var wasEngaged = IsHandEngaged;
            _hovering.Clear();
            _selecting.Clear();
            if (wasEngaged) HandEngagementChanged?.Invoke();
        }
    }
}
