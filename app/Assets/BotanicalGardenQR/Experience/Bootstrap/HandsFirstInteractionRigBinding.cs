using System;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    public sealed class HandsFirstInteractionRigBinding : MonoBehaviour
    {
        [Serializable]
        public sealed class TrackerBinding
        {
            [SerializeField] ActiveStateTracker _tracker;
            [SerializeField] HandRef _handAvailability;

            public TrackerBinding(ActiveStateTracker tracker, HandRef handAvailability)
            {
                _tracker = tracker;
                _handAvailability = handAvailability;
            }

            public ActiveStateTracker Tracker => _tracker;
            public HandRef HandAvailability => _handAvailability;
        }

        [SerializeField] TrackerBinding[] _bindings = Array.Empty<TrackerBinding>();

        public IReadOnlyList<TrackerBinding> Bindings => _bindings;

        void Awake()
        {
            if (!Application.isPlaying && (_bindings == null || _bindings.Length == 0))
                return;
            ApplyHandsFirstPolicy();
        }

        public void InjectBindings(params TrackerBinding[] bindings)
        {
            _bindings = bindings == null
                ? null
                : (TrackerBinding[])bindings.Clone();
            ValidateBindings();
        }

        public void ApplyHandsFirstPolicy()
        {
            ValidateBindings();
            foreach (var binding in _bindings)
                binding.Tracker.InjectActiveState(binding.HandAvailability);
        }

        void ValidateBindings()
        {
            if (_bindings == null || _bindings.Length == 0)
                throw new InvalidOperationException(
                    "Hands-first interaction requires at least one authored tracker/HandRef binding.");

            var trackers = new HashSet<ActiveStateTracker>();
            var hands = new HashSet<HandRef>();
            for (var index = 0; index < _bindings.Length; index++)
            {
                var binding = _bindings[index];
                if (binding == null)
                    throw new InvalidOperationException(
                        $"Hands-first interaction binding {index} is empty.");
                if (binding.Tracker == null)
                    throw new InvalidOperationException(
                        $"Hands-first interaction binding {index} has no ActiveStateTracker.");
                if (binding.HandAvailability == null)
                    throw new InvalidOperationException(
                        $"Hands-first interaction binding {index} has no HandRef.");
                if (!trackers.Add(binding.Tracker))
                    throw new InvalidOperationException(
                        $"Hands-first interaction duplicates tracker '{binding.Tracker.name}'.");
                if (!hands.Add(binding.HandAvailability))
                    throw new InvalidOperationException(
                        $"Hands-first interaction duplicates HandRef '{binding.HandAvailability.name}'.");
            }
        }
    }
}
