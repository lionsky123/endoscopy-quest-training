using System;
using System.Collections.Generic;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.JourneyNavigation.Contracts;

namespace BotanicalGardenQR.Bootstrap
{
    /// <summary>
    /// Application projection over Journey and Collection. The two source
    /// modules remain authoritative; this binding owns no mutable progress.
    /// </summary>
    public sealed class VisitorProgressSummaryBinding :
        IJourneyNavigationStateSink,
        ICollectionProgressStateSink,
        IDisposable
    {
        readonly IVisitorProgressSummaryPresenter _presenter;
        readonly int _mainTotal;
        IDisposable _journeySubscription;
        IDisposable _collectionSubscription;
        JourneyViewState _journeyState;
        CollectionProgressViewState _collectionState;
        VisitorProgressSummary _lastPublished;
        bool _disposed;

        public VisitorProgressSummaryBinding(
            IJourneyNavigation journey,
            ICollectionProgress collection,
            int contentTotal,
            IVisitorProgressSummaryPresenter presenter)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _mainTotal = contentTotal;

            try
            {
                _journeySubscription = (journey ?? throw new ArgumentNullException(nameof(journey)))
                    .Observe(this);
                _collectionSubscription = (collection ?? throw new ArgumentNullException(nameof(collection)))
                    .Observe(this);
            }
            catch
            {
                _journeySubscription?.Dispose();
                _collectionSubscription?.Dispose();
                throw;
            }
        }

        public void OnJourneyStateChanged(JourneyViewState state)
        {
            if (_disposed || state == null) return;
            _journeyState = state;
            PublishIfReady();
        }

        public void OnCollectionProgressStateChanged(CollectionProgressViewState state)
        {
            if (_disposed || state == null) return;
            _collectionState = state;
            PublishIfReady();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _collectionSubscription?.Dispose();
            _journeySubscription?.Dispose();
            _collectionSubscription = null;
            _journeySubscription = null;
            _journeyState = null;
            _collectionState = null;
            _lastPublished = null;
        }

        void PublishIfReady()
        {
            if (_journeyState == null || _collectionState == null) return;
            if (_journeyState.Session.IsValid && _collectionState.JourneySession.IsValid &&
                _journeyState.Session != _collectionState.JourneySession)
                return;

            var summary = new VisitorProgressSummary(
                ResolveMainCompleted(_journeyState),
                _mainTotal,
                _collectionState.CollectedCount,
                _collectionState.TotalCount);
            if (_lastPublished != null &&
                _lastPublished.MainCompleted == summary.MainCompleted &&
                _lastPublished.MainTotal == summary.MainTotal &&
                _lastPublished.CollectionCompleted == summary.CollectionCompleted &&
                _lastPublished.CollectionTotal == summary.CollectionTotal)
                return;
            _lastPublished = summary;
            _presenter.SetVisitorProgressSummary(summary);
        }

        int ResolveMainCompleted(JourneyViewState state)
        {
            return Math.Min(_mainTotal, state.CompletedCount);
        }
    }
}
