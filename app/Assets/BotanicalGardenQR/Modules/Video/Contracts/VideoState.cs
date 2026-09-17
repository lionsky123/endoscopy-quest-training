using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Video.Contracts
{
    public enum VideoPhase { Closed, Loading, Playing, Paused, Completed, Failed }

    public readonly struct VideoState
    {
        public VideoState(
            SessionToken session,
            long version,
            VideoPhase phase,
            double positionSeconds,
            double durationSeconds,
            bool isMuted,
            bool canPauseResume,
            bool canReplay,
            bool canMute,
            UserFault fault = default)
        {
            if (version < 0)
                throw new System.ArgumentOutOfRangeException(nameof(version));
            if (double.IsNaN(positionSeconds) || double.IsInfinity(positionSeconds) || positionSeconds < 0d)
                throw new System.ArgumentOutOfRangeException(nameof(positionSeconds));
            if (double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds < 0d)
                throw new System.ArgumentOutOfRangeException(nameof(durationSeconds));

            Session = session;
            Version = version;
            Phase = phase;
            PositionSeconds = positionSeconds;
            DurationSeconds = durationSeconds;
            Progress = durationSeconds > 0d
                ? (float)System.Math.Max(0d, System.Math.Min(1d, positionSeconds / durationSeconds))
                : 0f;
            IsMuted = isMuted;
            CanPauseResume = canPauseResume;
            CanReplay = canReplay;
            CanMute = canMute;
            Fault = fault;
        }

        public SessionToken Session { get; }
        public long Version { get; }
        public VideoPhase Phase { get; }
        public double PositionSeconds { get; }
        public double DurationSeconds { get; }
        public float Progress { get; }
        public bool IsMuted { get; }
        public bool CanPauseResume { get; }
        public bool CanReplay { get; }
        public bool CanMute { get; }
        public UserFault Fault { get; }
    }
}
