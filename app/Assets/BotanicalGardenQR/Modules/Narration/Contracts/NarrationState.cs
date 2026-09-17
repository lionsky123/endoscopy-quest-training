using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Narration.Contracts
{
    public enum NarrationPhase { Closed, Loading, Ready, Playing, Paused, Completed, Failed }

    public readonly struct NarrationState
    {
        public NarrationState(
            SessionToken session,
            long version,
            NarrationPhase phase,
            double positionSeconds,
            double durationSeconds,
            bool isMuted,
            bool canPauseResume,
            bool canReplay,
            bool canMute,
            UserFault fault = default)
        {
            if (version < 0) throw new System.ArgumentOutOfRangeException(nameof(version));
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
        public NarrationPhase Phase { get; }
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
