using System;
using System.Collections.Generic;
using System.Threading;

namespace BotanicalGardenQR.Experience.Contracts
{
    public sealed class StateChannel<TState> : IDisposable
    {
        readonly object _gate = new object();
        readonly Dictionary<long, Subscription> _subscriptions = new Dictionary<long, Subscription>();
        readonly Func<TState, long> _versionSelector;
        readonly int _requiredThreadId;

        TState _current;
        long _currentVersion;
        long _nextSubscriptionId;
        bool _disposed;

        public StateChannel(
            TState initialState,
            Func<TState, long> versionSelector,
            int requiredThreadId)
        {
            _versionSelector = versionSelector ?? throw new ArgumentNullException(nameof(versionSelector));
            if (requiredThreadId <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredThreadId));
            _requiredThreadId = requiredThreadId;
            _current = initialState;
            _currentVersion = versionSelector(initialState);
            if (_currentVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(initialState), "Initial state version cannot be negative.");
        }

        public static StateChannel<TState> ForCurrentThread(
            TState initialState,
            Func<TState, long> versionSelector)
            => new StateChannel<TState>(
                initialState,
                versionSelector,
                Thread.CurrentThread.ManagedThreadId);

        public IDisposable Observe(Action<TState> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            RequireThread();

            Subscription subscription;
            TState current;
            long currentVersion;
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(StateChannel<TState>));
                var id = ++_nextSubscriptionId;
                subscription = new Subscription(this, id, callback, _requiredThreadId);
                _subscriptions.Add(id, subscription);
                current = _current;
                currentVersion = _currentVersion;
            }

            subscription.Publish(currentVersion, current);
            return subscription;
        }

        public void Publish(TState state)
        {
            RequireThread();
            var version = _versionSelector(state);
            Subscription[] snapshot;
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(StateChannel<TState>));
                if (version <= _currentVersion)
                    throw new InvalidOperationException("State versions must be strictly increasing.");

                _current = state;
                _currentVersion = version;
                snapshot = new Subscription[_subscriptions.Count];
                _subscriptions.Values.CopyTo(snapshot, 0);
            }

            foreach (var subscription in snapshot)
                subscription.Publish(version, state);
        }

        public void Dispose()
        {
            RequireThread();
            Subscription[] snapshot;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                snapshot = new Subscription[_subscriptions.Count];
                _subscriptions.Values.CopyTo(snapshot, 0);
                _subscriptions.Clear();
            }

            foreach (var subscription in snapshot)
                subscription.Dispose();
        }

        void Remove(long id)
        {
            lock (_gate)
                _subscriptions.Remove(id);
        }

        void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _requiredThreadId)
                throw new InvalidOperationException("State channels must run on the configured Unity main thread.");
        }

        sealed class Subscription : IDisposable
        {
            readonly Action<TState> _callback;
            readonly StateSubscription _state;

            public Subscription(
                StateChannel<TState> owner,
                long id,
                Action<TState> callback,
                int requiredThreadId)
            {
                _callback = callback;
                _state = new StateSubscription(() => owner.Remove(id), requiredThreadId);
            }

            public void Publish(long version, TState state)
                => _state.Publish(version, () => _callback(state));

            public void Dispose() => _state.Dispose();
        }
    }
}
