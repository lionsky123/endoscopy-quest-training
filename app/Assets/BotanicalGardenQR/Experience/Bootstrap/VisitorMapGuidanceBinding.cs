using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.Fairy.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.JourneyNavigation.Contracts;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.MapNavigation.Frontend;
using BotanicalGardenQR.VisitorCoach.Frontend;
using UnityEngine;
using UnityEngine.XR;

namespace BotanicalGardenQR.Bootstrap
{
    /// <summary>Adapts explicit platform and module snapshots. Trip decisions live in the application coordinator.</summary>
    internal sealed class VisitorMapGuidanceBinding : IDisposable, IContentLifecycleSink
    {
        readonly IMapNavigation _navigation;
        readonly VisitorGuidanceCoordinator _guidance;
        readonly Transform _viewer, _floor;
        readonly IJourneyNavigation _journey;
        readonly ICollectionProgress _collection;
        readonly CollectionWorldFrontend _collectionSurface;
        readonly VisitorCoachPresenter _dialogue;
        readonly VisitorModalPresentationBinding _modal;
        readonly IMapRoutePresentation _route;
        readonly IDisposable _contentLease;
        readonly VisitorDialogueContextId _context = new VisitorDialogueContextId("map-guidance");
        bool _disposed, _lastTracked;
        public VisitorMapGuidanceBinding(IMapNavigation navigation, VisitorGuidanceCoordinator guidance, Transform viewer, Transform floor, IJourneyNavigation journey, ICollectionProgress collection, CollectionWorldFrontend collectionSurface, VisitorCoachPresenter dialogue, VisitorModalPresentationBinding modal, IMapRoutePresentation route, IContentLifecycleSource content)
        {
            _navigation = navigation;
            _guidance = guidance;
            _viewer = viewer;
            _floor = floor;
            _journey = journey;
            _collection = collection;
            _collectionSurface = collectionSurface;
            _dialogue = dialogue;
            _modal = modal;
            _route = route;
            _contentLease = content.Observe(this);
            _guidance.Changed += OnGuidanceChanged;
            _dialogue.IntentRequested += OnIntent;
            _modal.SetGuidanceBlocksFirstScan(_guidance.BlocksFirstScan);
        }

        public void Tick(float dt)
        {
            if (_disposed)
                return;
            var tracked = _viewer != null && _floor != null;
            // The XR node's tracked bit, not a retained Transform, qualifies a live device sample.
            if (!Application.isEditor || XRSettings.isDeviceActive)
            {
                var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                tracked = device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out var valid) && valid;
            }

            _lastTracked = tracked;
            var position = _viewer != null ? ToMap(_viewer.position) : default;
            if (tracked)
                _navigation.TryInitialize(position, _viewer.eulerAngles.y, _floor.position.y, dt);
            var journey = _journey.CurrentState;
            var facts = _modal.Current;
            var externalDialogue = facts.DialogueOwner.HasValue && facts.DialogueOwner != VisitorDialogueOwner.Guidance;
            var environment = new VisitorGuidanceEnvironment(!facts.ContentClosed, _collectionSurface.SurfaceKind != CollectionPresentationSurfaceKind.Hidden, _collection.CurrentState?.PendingPresentation != null, externalDialogue, facts.CompletionVisible, facts.CloseDecisionVisible);
            // Route geometry is ready as soon as the real floor/viewer frame is known.
            // It does not depend on opening a book, summoning a map, or loading a map GLB.
            _guidance.Refresh(journey?.CompletionRevision ?? 0, environment, _navigation.HasFrame,
                _navigation.State.Phase == MapNavigationPhase.Unavailable);
            _navigation.Tick(position, tracked, _guidance.IsPaused, dt);
            _guidance.TrackVisitorArrival(position, tracked, dt);
            _route?.Present(_navigation.CurrentWorldPath, _guidance.ShowRoute, _guidance.ShowTarget, _guidance.TargetTitle);
            _dialogue.PresentGuidance(_guidance.Surface);
        }

        public bool TryRecallFairy(IFairyRecovery fairy)
        {
            if (_disposed || !_lastTracked || _viewer == null || _guidance.IsPaused || fairy == null ||
                !(_navigation is IMapNavigationRecovery recovery) ||
                !recovery.TryGetRecoveryPosition(ToMap(_viewer.position), out var p)) return false;
            var target = new Vector3(p.x, p.y, p.z);
            // Conservative virtual-collider check. This does not claim real-world obstacle recognition.
            if (Physics.CheckCapsule(target + Vector3.up * .22f, target + Vector3.up * .55f,
                .16f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            if (!fairy.TryRecallMotion(target)) return false;
            recovery.ReacquireMotion();
            return true;
        }

        void OnGuidanceChanged()
        {
            _modal.SetGuidanceBlocksFirstScan(_guidance.BlocksFirstScan);
        }

        void OnIntent(VisitorDialogueIntent intent)
        {
            if (_disposed || intent.Context != _context || intent.Owner != VisitorDialogueOwner.Guidance)
                return;
            _dialogue.Hide(_context);
            _guidance.HandleIntent(intent.Kind);
        }

        public void OnContentOpened(ContentOpenedFact fact)
        {
            if (fact == null || fact.IsRecall || (fact.EntryKind != RecognitionSourceKinds.Qr && fact.EntryKind != RecognitionSourceKinds.Fieldbook))
                return;
            _guidance.TargetContentOpened();
        }

        public void OnContentClosed(ContentClosedFact fact)
        {
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _guidance.Changed -= OnGuidanceChanged;
            _dialogue.IntentRequested -= OnIntent;
            _contentLease.Dispose();
            _dialogue.Hide(_context);
        }

        static MapPosition ToMap(Vector3 p) => new MapPosition(p.x, p.y, p.z);
    }

    internal sealed class FairyMapMotionSink : IMapMotionSink
    {
        readonly IFairyMotion _motion;
        readonly float _entryRadius, _speed;
        public FairyMapMotionSink(IFairyMotion motion, MapDefinition definition)
        {
            _motion = motion;
            _entryRadius = definition?.departureRadius ?? 0;
            _speed = definition?.speed ?? 0;
        }

        public bool TryGetPosition(out MapPosition position)
        {
            position = default;
            if (_motion == null || !_motion.TryGetMotionPosition(out var p))
                return false;
            position = new MapPosition(p.x, p.y, p.z);
            return true;
        }

        public bool Apply(long id, MapPosition p, MapPosition forward, bool moving) => _motion != null && _motion.ApplyMotion(id, new Vector3(p.x, p.y, p.z), new Vector3(forward.x, forward.y, forward.z), moving, _entryRadius, _speed);
        public void Hold(long id) => _motion?.HoldMotion(id);
        public void Release(long id) => _motion?.ReleaseMotion(id);
    }
}
