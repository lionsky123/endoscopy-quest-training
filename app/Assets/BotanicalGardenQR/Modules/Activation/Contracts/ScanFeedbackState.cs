using System;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Activation.Contracts
{
    public sealed class ScanFeedbackState
    {
        public ScanFeedbackState(long version, float progress, bool isConfirmation, UserFault fault = null)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));
            if (float.IsNaN(progress) || float.IsInfinity(progress) || progress < 0f || progress > 1f)
                throw new ArgumentOutOfRangeException(nameof(progress));

            Version = version;
            Progress = progress;
            IsConfirmation = isConfirmation;
            Fault = fault;
        }

        public long Version { get; }
        public float Progress { get; }
        public bool IsConfirmation { get; }
        public UserFault Fault { get; }
    }
}
