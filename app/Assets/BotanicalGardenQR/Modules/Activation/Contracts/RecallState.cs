using System;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Activation.Contracts
{
    public sealed class RecallState
    {
        public RecallState(
            long version,
            bool isClosed,
            bool canRecall,
            UserFault fault = null,
            bool hasPreviousContent = false)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));
            Version = version;
            IsClosed = isClosed;
            CanRecall = canRecall;
            Fault = fault;
            HasPreviousContent = hasPreviousContent;
        }

        public long Version { get; }
        public bool IsClosed { get; }
        public bool CanRecall { get; }
        public UserFault Fault { get; }
        public bool HasPreviousContent { get; }
    }
}
