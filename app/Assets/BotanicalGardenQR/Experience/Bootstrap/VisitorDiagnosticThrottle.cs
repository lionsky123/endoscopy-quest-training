using System;
using System.Collections.Generic;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed class VisitorDiagnosticThrottle
    {
        readonly object _sync = new object();
        readonly TimeSpan _repeatInterval;
        readonly int _capacity;
        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        internal VisitorDiagnosticThrottle(TimeSpan repeatInterval, int capacity)
        {
            if (repeatInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(repeatInterval));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _repeatInterval = repeatInterval;
            _capacity = capacity;
        }

        internal bool TryAccept(string signature, DateTimeOffset timestamp, out int suppressedRepeats)
        {
            signature = signature ?? string.Empty;
            lock (_sync)
            {
                if (_entries.TryGetValue(signature, out var entry))
                {
                    if (timestamp - entry.LastAcceptedAt < _repeatInterval)
                    {
                        if (entry.SuppressedRepeats < int.MaxValue) entry.SuppressedRepeats++;
                        suppressedRepeats = 0;
                        return false;
                    }

                    suppressedRepeats = entry.SuppressedRepeats;
                    entry.LastAcceptedAt = timestamp;
                    entry.SuppressedRepeats = 0;
                    return true;
                }

                if (_entries.Count >= _capacity) RemoveOldest();
                _entries.Add(signature, new Entry(timestamp));
                suppressedRepeats = 0;
                return true;
            }
        }

        void RemoveOldest()
        {
            string oldestSignature = null;
            var oldestTimestamp = DateTimeOffset.MaxValue;
            foreach (var pair in _entries)
            {
                if (pair.Value.LastAcceptedAt >= oldestTimestamp) continue;
                oldestTimestamp = pair.Value.LastAcceptedAt;
                oldestSignature = pair.Key;
            }
            if (oldestSignature != null) _entries.Remove(oldestSignature);
        }

        sealed class Entry
        {
            internal Entry(DateTimeOffset lastAcceptedAt) => LastAcceptedAt = lastAcceptedAt;
            internal DateTimeOffset LastAcceptedAt;
            internal int SuppressedRepeats;
        }
    }
}
