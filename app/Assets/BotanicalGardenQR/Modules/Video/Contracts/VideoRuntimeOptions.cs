using System;

namespace BotanicalGardenQR.Video.Contracts
{
    public sealed class VideoRuntimeOptions
    {
        public VideoRuntimeOptions(float prepareTimeoutSeconds)
        {
            if (!(prepareTimeoutSeconds > 0f) || float.IsInfinity(prepareTimeoutSeconds))
                throw new ArgumentOutOfRangeException(nameof(prepareTimeoutSeconds));
            PrepareTimeoutSeconds = prepareTimeoutSeconds;
        }

        public float PrepareTimeoutSeconds { get; }
    }
}
