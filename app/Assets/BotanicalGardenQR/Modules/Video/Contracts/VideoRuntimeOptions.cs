using System;

namespace BotanicalGardenQR.Video.Contracts
{
    public sealed class VideoRuntimeOptions
    {
        public VideoRuntimeOptions(float prepareTimeoutSeconds, bool ignoreListenerSilence = false)
        {
            if (!(prepareTimeoutSeconds > 0f) || float.IsInfinity(prepareTimeoutSeconds))
                throw new ArgumentOutOfRangeException(nameof(prepareTimeoutSeconds));
            PrepareTimeoutSeconds = prepareTimeoutSeconds;
            IgnoreListenerSilence = ignoreListenerSilence;
        }

        public float PrepareTimeoutSeconds { get; }
        public bool IgnoreListenerSilence { get; }
    }
}
