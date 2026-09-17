using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.SpatialHost.Contracts;

namespace BotanicalGardenQR.SpatialHost.Runtime
{
    internal sealed class PreparedHostLease : IPreparedHostLease
    {
        SpatialDisplayHost _owner;
        readonly long _generation;
        readonly PlacementCandidate _candidate;

        public PreparedHostLease(
            SpatialDisplayHost owner,
            long generation,
            SessionToken session,
            PlacementCandidate candidate)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _generation = generation;
            Session = session;
            _candidate = candidate;
        }

        public SessionToken Session { get; }

        public void Commit()
        {
            var owner = _owner ?? throw new ObjectDisposedException(nameof(PreparedHostLease));
            owner.Commit(_generation, Session, _candidate);
            _owner = null;
        }

        public void Dispose()
        {
            var owner = _owner;
            _owner = null;
            owner?.Cancel(_generation);
        }
    }
}
