using BotanicalGardenQR.Collection.Contracts;
using System;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode.Bootstrap
{
    public sealed class VisitorAtlasHubBindingPolicyTests
    {
        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        public void BookCommitRequiresHubGateAndCollectionGate(
            bool hubGateOpen,
            bool collectionCanOpen,
            bool expected)
            => Assert.That(
                VisitorAtlasHubCollectionBinding.CanCommitBookOpen(
                    hubGateOpen,
                    collectionCanOpen),
                Is.EqualTo(expected));

        [Test]
        public void BookSelectionAndAnimationCompletionEachRecheckTheirGate()
        {
            var hub = new RecordingHub { GateOpen = false, Visible = true };
            var collection = new RecordingCollectionGateway { CanOpen = true };
            using var binding = new VisitorAtlasHubCollectionBinding(hub, collection);

            hub.PublishBookSelected();
            Assert.That(hub.BeginCalls, Is.Zero);
            Assert.That(hub.RejectCalls, Is.EqualTo(1));

            hub.GateOpen = true;
            collection.CanOpen = false;
            hub.PublishBookSelected();
            Assert.That(hub.BeginCalls, Is.Zero);
            Assert.That(hub.RejectCalls, Is.EqualTo(2));

            collection.CanOpen = true;
            hub.PublishBookSelected();
            Assert.That(hub.BeginCalls, Is.EqualTo(1));

            hub.GateOpen = false;
            hub.PublishOpenCollectionRequested(hub.LastGeneration);
            Assert.That(collection.OpenCalls, Is.Zero);
            Assert.That(hub.LastCanceledGeneration, Is.EqualTo(hub.LastGeneration));
        }

        [Test]
        public void ActualBrowseConfirmationLocksTheBookAndHiddenResetsIt()
        {
            var hub = new RecordingHub { GateOpen = true, Visible = true };
            var collection = new RecordingCollectionGateway
            {
                CanOpen = true,
                PublishBrowseSynchronously = true
            };
            using var binding = new VisitorAtlasHubCollectionBinding(hub, collection);

            hub.PublishBookSelected();
            hub.PublishOpenCollectionRequested(hub.LastGeneration);

            Assert.That(collection.OpenCalls, Is.EqualTo(1));
            Assert.That(hub.LastConfirmedGeneration, Is.EqualTo(hub.LastGeneration));
            Assert.That(hub.ConfirmCalls, Is.EqualTo(1));

            collection.Publish(CollectionPresentationSurfaceKind.Hidden);
            Assert.That(hub.CollectionClosedCalls, Is.EqualTo(1));
        }

        [Test]
        public void HubHideClearsPendingBrowseAndLateSurfaceCallbackCannotConfirm()
        {
            var hub = new RecordingHub { GateOpen = true, Visible = true };
            var collection = new RecordingCollectionGateway { CanOpen = true };
            using var binding = new VisitorAtlasHubCollectionBinding(hub, collection);

            hub.PublishBookSelected();
            hub.PublishOpenCollectionRequested(hub.LastGeneration);
            Assert.That(collection.OpenCalls, Is.EqualTo(1));

            hub.PublishHidden();
            collection.Publish(CollectionPresentationSurfaceKind.Browse);

            Assert.That(hub.ConfirmCalls, Is.Zero,
                "A Browse callback arriving after explicit Hub hide must not lock or reopen the book.");
        }

        sealed class RecordingCollectionGateway : IVisitorAtlasHubCollectionGateway
        {
            public bool CanOpen { get; set; }
            public bool PublishBrowseSynchronously { get; set; }
            public int OpenCalls { get; private set; }
            public bool CanOpenBrowseMode => CanOpen;
            public CollectionPresentationSurfaceKind SurfaceKind { get; private set; } =
                CollectionPresentationSurfaceKind.Hidden;
            public event Action<CollectionPresentationSurfaceKind> SurfaceChanged;

            public void OpenBrowseMode()
            {
                OpenCalls++;
                if (PublishBrowseSynchronously) Publish(CollectionPresentationSurfaceKind.Browse);
            }

            public void Publish(CollectionPresentationSurfaceKind surface)
            {
                SurfaceKind = surface;
                SurfaceChanged?.Invoke(surface);
            }
        }

        sealed class RecordingHub : IVisitorAtlasHubController
        {
            bool _opening;
            int _generation;

            public bool GateOpen { get; set; }
            public bool Visible { get; set; }
            public int BeginCalls { get; private set; }
            public int RejectCalls { get; private set; }
            public int ConfirmCalls { get; private set; }
            public int CollectionClosedCalls { get; private set; }
            public int LastGeneration { get; private set; }
            public int LastCanceledGeneration { get; private set; }
            public int LastConfirmedGeneration { get; private set; }
            public VisitorAtlasHubPhase Phase => Visible
                ? VisitorAtlasHubPhase.Visible
                : VisitorAtlasHubPhase.ReadyHidden;
            public bool MapRequested => false;
            public bool IsMapVisible => false;
            public void SetMapSuppressed(bool suppressed) { }
            public VisitorAtlasHubBookState BookState => _opening
                ? VisitorAtlasHubBookState.Opening
                : VisitorAtlasHubBookState.ClosedInteractive;
            public bool InteractionGateOpen => GateOpen;
            public bool IsVisible => Visible;
            public event Action<VisitorAtlasHubPhase> PhaseChanged { add { } remove { } }
            public event Action<VisitorAtlasHubPalmStage> PalmStageChanged { add { } remove { } }
            public event Action Summoned { add { } remove { } }
            public event Action BookSelected;
            public event Action<int> OpenCollectionRequested;
            public event Action Hidden;
            public event Action<VisitorAtlasHubFailure> Failed { add { } remove { } }

            public void PublishBookSelected() => BookSelected?.Invoke();
            public void PublishOpenCollectionRequested(int generation) =>
                OpenCollectionRequested?.Invoke(generation);
            public void PublishHidden() => Hidden?.Invoke();
            public void SetInteractionGate(bool open) => GateOpen = open;
            public void Tick(float unscaledDeltaSeconds) { }
            public bool TryBeginBookOpening(out int generation)
            {
                BeginCalls++;
                if (_opening)
                {
                    generation = _generation;
                    return false;
                }
                _opening = true;
                generation = ++_generation;
                LastGeneration = generation;
                return true;
            }
            public void ConfirmCollectionOpened(int generation)
            {
                ConfirmCalls++;
                LastConfirmedGeneration = generation;
            }
            public void CancelBookOpening(int generation = 0)
            {
                LastCanceledGeneration = generation;
                _opening = false;
            }
            public void NotifyCollectionClosed()
            {
                CollectionClosedCalls++;
                _opening = false;
            }
            public void RejectBookSelection() => RejectCalls++;
            public void Hide() { }
            public void Dispose() { }
        }
    }
}
