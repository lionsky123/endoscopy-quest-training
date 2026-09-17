using System;
using System.Diagnostics;
using System.Threading;

namespace BotanicalGardenQR.Experience.Contracts
{
    public sealed class StateSubscription : IDisposable
    {
        readonly object _gate = new object();
        readonly int _callbackThreadId;
        Action _unsubscribe;
        long _lastVersion = -1;

        public StateSubscription(Action unsubscribe, int requiredThreadId)
        {
            _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
            if (requiredThreadId <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredThreadId));
            _callbackThreadId = requiredThreadId;
        }

        public static StateSubscription ForCurrentThread(Action unsubscribe)
            => new StateSubscription(unsubscribe, Thread.CurrentThread.ManagedThreadId);

        public bool IsDisposed => Volatile.Read(ref _unsubscribe) == null;

        public bool Publish(long version, Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            lock (_gate)
            {
                if (_unsubscribe == null)
                    return false;
                if (Thread.CurrentThread.ManagedThreadId != _callbackThreadId)
                    throw new InvalidOperationException("State callbacks must run on the configured Unity main thread.");
                if (version <= _lastVersion)
                    throw new InvalidOperationException("State versions must be strictly increasing.");

                _lastVersion = version;
            }

            try
            {
                callback();
                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError($"State subscriber callback failed and was removed: {exception}");
                Dispose();
                return false;
            }
        }

        public void Dispose()
        {
            Action unsubscribe;
            lock (_gate)
                unsubscribe = Interlocked.Exchange(ref _unsubscribe, null);
            if (unsubscribe == null) return;
            try
            {
                unsubscribe();
            }
            catch (Exception exception)
            {
                Trace.TraceError($"State subscriber cleanup failed: {exception}");
            }
        }
    }
}
