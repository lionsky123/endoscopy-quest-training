using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;

namespace BotanicalGardenQR.Experience.Flow
{
    internal sealed class PreparedFlowLease : IPreparedFlowLease
    {
        ExperienceFlow _owner;
        readonly long _generation;
        readonly SceneFlowDescriptor _descriptor;

        public PreparedFlowLease(
            ExperienceFlow owner,
            long generation,
            SessionToken session,
            SceneFlowDescriptor descriptor)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _generation = generation;
            Session = session;
            _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        }

        public SessionToken Session { get; }

        public void Commit()
        {
            var owner = _owner ?? throw new ObjectDisposedException(nameof(PreparedFlowLease));
            owner.Commit(_generation, Session, _descriptor);
            _owner = null;
        }

        public void Dispose() => _owner = null;
    }
}
