using System.Collections.Generic;
using BotanicalGardenQR.Bootstrap;
using NUnit.Framework;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Bootstrap
{
    public sealed class HandsFirstInteractionRigBindingTests
    {
        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void ComprehensiveHandGateDisablesRealHandBranchWhileControllerRefIsConnected()
        {
            _root = new GameObject("Meta205ControllerGate");
            var handState = _root.AddComponent<MutableActiveState>();
            var controller = _root.AddComponent<MutableController>();
            var controllerRef = _root.AddComponent<ControllerRef>();
            var noController = _root.AddComponent<ActiveStateNot>();
            var comprehensiveHandGate = _root.AddComponent<ActiveStateGroup>();

            handState.Value = true;
            controllerRef.InjectController(controller);
            noController.InjectActiveState(controllerRef);
            comprehensiveHandGate.InjectActiveStates(new List<IActiveState>
            {
                handState,
                noController
            });
            comprehensiveHandGate.InjectOptionalLogicOperator(
                ActiveStateGroup.ActiveStateGroupLogicOperator.AND);

            controller.Connected = true;
            Assert.That(controllerRef.Active, Is.True,
                "Meta 205 ControllerRef maps controller connectivity to IActiveState.Active.");
            Assert.That(comprehensiveHandGate.Active, Is.False,
                "The official Comprehensive 'Hand and No Controller' branch is disabled while a controller remains connected.");

            controller.Connected = false;
            Assert.That(controllerRef.Active, Is.False);
            Assert.That(comprehensiveHandGate.Active, Is.True,
                "The official Comprehensive real-hand branch becomes active after controller connectivity clears.");
        }

        [Test]
        public void ApplyHandsFirstPolicyInjectsAuthoredHandRefsThroughMetaPublicApi()
        {
            _root = new GameObject("HandsFirstBinding");
            var left = CreateTrackerAndHand("Left");
            var right = CreateTrackerAndHand("Right");
            var binding = _root.AddComponent<HandsFirstInteractionRigBinding>();
            binding.InjectBindings(
                new HandsFirstInteractionRigBinding.TrackerBinding(left.Tracker, left.Hand),
                new HandsFirstInteractionRigBinding.TrackerBinding(right.Tracker, right.Hand));

            binding.ApplyHandsFirstPolicy();
            binding.ApplyHandsFirstPolicy();

            Assert.That(binding.Bindings.Count, Is.EqualTo(2));
            Assert.That(binding.Bindings[0].Tracker, Is.SameAs(left.Tracker));
            Assert.That(binding.Bindings[0].HandAvailability, Is.SameAs(left.Hand));
            Assert.That(binding.Bindings[1].Tracker, Is.SameAs(right.Tracker));
            Assert.That(binding.Bindings[1].HandAvailability, Is.SameAs(right.Hand));
            AssertTrackerUsesHand(left.Tracker, left.Hand);
            AssertTrackerUsesHand(right.Tracker, right.Hand);
        }

        [Test]
        public void InjectBindingsRejectsDuplicateTrackers()
        {
            _root = new GameObject("InvalidHandsFirstBinding");
            var left = CreateTrackerAndHand("Left");
            var right = CreateTrackerAndHand("Right");
            var binding = _root.AddComponent<HandsFirstInteractionRigBinding>();

            var exception = Assert.Throws<System.InvalidOperationException>(() =>
                binding.InjectBindings(
                    new HandsFirstInteractionRigBinding.TrackerBinding(left.Tracker, left.Hand),
                    new HandsFirstInteractionRigBinding.TrackerBinding(left.Tracker, right.Hand)));

            Assert.That(exception.Message, Does.Contain("duplicates tracker"));
        }

        (ActiveStateTracker Tracker, HandRef Hand) CreateTrackerAndHand(string side)
        {
            var child = new GameObject(side);
            child.transform.SetParent(_root.transform, false);
            return (
                child.AddComponent<ActiveStateTracker>(),
                child.AddComponent<HandRef>());
        }

        static void AssertTrackerUsesHand(ActiveStateTracker tracker, HandRef hand)
        {
            var activeState = new SerializedObject(tracker).FindProperty("_activeState");
            Assert.That(activeState.objectReferenceValue, Is.SameAs(hand));
        }

        sealed class MutableActiveState : MonoBehaviour, IActiveState
        {
            public bool Value { get; set; }
            public bool Active => Value;
        }

        sealed class MutableController : Controller
        {
            public bool Connected { get; set; }
            public override bool IsConnected => Connected;
        }
    }
}
