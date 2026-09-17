using System;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;

namespace BotanicalGardenQR.Bootstrap
{
    internal interface IVisitorAtlasHubCollectionGateway
    {
        bool CanOpenBrowseMode { get; }
        CollectionPresentationSurfaceKind SurfaceKind { get; }
        event Action<CollectionPresentationSurfaceKind> SurfaceChanged;
        void OpenBrowseMode();
    }

    internal sealed class VisitorAtlasHubCollectionGateway : IVisitorAtlasHubCollectionGateway
    {
        readonly ICollectionBrowseMode _browseMode;
        readonly CollectionWorldFrontend _presentation;

        public VisitorAtlasHubCollectionGateway(
            ICollectionBrowseMode browseMode,
            CollectionWorldFrontend presentation)
        {
            _browseMode = browseMode ?? throw new ArgumentNullException(nameof(browseMode));
            _presentation = presentation != null
                ? presentation
                : throw new ArgumentNullException(nameof(presentation));
        }

        public bool CanOpenBrowseMode => _browseMode.CanOpenBrowseMode;
        public CollectionPresentationSurfaceKind SurfaceKind => _presentation.SurfaceKind;
        public event Action<CollectionPresentationSurfaceKind> SurfaceChanged
        {
            add => _presentation.SurfaceChanged += value;
            remove => _presentation.SurfaceChanged -= value;
        }
        public void OpenBrowseMode() => _browseMode.OpenBrowseMode();
    }

    /// <summary>
    /// The only cross-module seam between the movable Atlas Hub book entry and
    /// the existing Collection BrowseMode. Both select-time and animation-time
    /// commits are gated; Collection remains the sole catalog/state owner and
    /// never owns or repositions the entry model.
    /// </summary>
    internal sealed class VisitorAtlasHubCollectionBinding : IDisposable
    {
        readonly IVisitorAtlasHubController _hub;
        readonly IVisitorAtlasHubCollectionGateway _collection;
        int _pendingGeneration;
        bool _disposed;

        public VisitorAtlasHubCollectionBinding(
            IVisitorAtlasHubController hub,
            ICollectionBrowseMode browseMode,
            CollectionWorldFrontend collectionPresentation)
            : this(
                hub,
                new VisitorAtlasHubCollectionGateway(browseMode, collectionPresentation))
        {
        }

        internal VisitorAtlasHubCollectionBinding(
            IVisitorAtlasHubController hub,
            IVisitorAtlasHubCollectionGateway collection)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            _hub.BookSelected += HandleBookSelected;
            _hub.OpenCollectionRequested += HandleOpenCollectionRequested;
            _hub.Hidden += HandleHubHidden;
            _collection.SurfaceChanged += HandleCollectionSurfaceChanged;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _hub.BookSelected -= HandleBookSelected;
            _hub.OpenCollectionRequested -= HandleOpenCollectionRequested;
            _hub.Hidden -= HandleHubHidden;
            _collection.SurfaceChanged -= HandleCollectionSurfaceChanged;
            if (_pendingGeneration != 0) _hub.CancelBookOpening(_pendingGeneration);
            _pendingGeneration = 0;
        }

        internal static bool CanCommitBookOpen(bool hubGateOpen, bool collectionCanOpen)
            => hubGateOpen && collectionCanOpen;

        void HandleBookSelected()
        {
            if (_disposed) return;
            if (!CanCommitBookOpen(
                    _hub.InteractionGateOpen && _hub.IsVisible,
                    _collection.CanOpenBrowseMode) ||
                !_hub.TryBeginBookOpening(out _))
                _hub.RejectBookSelection();
        }

        void HandleOpenCollectionRequested(int generation)
        {
            if (_disposed) return;
            if (!CanCommitBookOpen(
                    _hub.InteractionGateOpen && _hub.IsVisible,
                    _collection.CanOpenBrowseMode))
            {
                _hub.CancelBookOpening(generation);
                return;
            }

            _pendingGeneration = generation;
            _collection.OpenBrowseMode();
            // Collection publishes Browse synchronously today. If an adapter
            // later becomes asynchronous, the Hub's bounded confirmation
            // timeout owns rollback instead of this binding queueing a retry.
            if (_collection.SurfaceKind == CollectionPresentationSurfaceKind.Browse)
                ConfirmPendingBrowse();
        }

        void HandleCollectionSurfaceChanged(CollectionPresentationSurfaceKind surface)
        {
            if (_disposed) return;
            switch (surface)
            {
                case CollectionPresentationSurfaceKind.Browse:
                    ConfirmPendingBrowse();
                    break;
                case CollectionPresentationSurfaceKind.Hidden:
                    if (_pendingGeneration != 0)
                        _hub.CancelBookOpening(_pendingGeneration);
                    _pendingGeneration = 0;
                    _hub.NotifyCollectionClosed();
                    break;
                default:
                    if (_pendingGeneration == 0) break;
                    _hub.CancelBookOpening(_pendingGeneration);
                    _pendingGeneration = 0;
                    break;
            }
        }

        void HandleHubHidden()
        {
            _pendingGeneration = 0;
        }

        void ConfirmPendingBrowse()
        {
            if (_pendingGeneration == 0) return;
            var generation = _pendingGeneration;
            _pendingGeneration = 0;
            _hub.ConfirmCollectionOpened(generation);
        }
    }
}
