using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.ImageRing.Contracts
{
    public enum ImageRingPhase
    {
        Closed,
        Opening,
        Visible,
        Failed
    }

    public readonly struct ImageRingState
    {
        public ImageRingState(
            SessionToken session,
            long version,
            ImageRingPhase phase,
            UserFault fault = default)
        {
            if (version < 0) throw new System.ArgumentOutOfRangeException(nameof(version));
            Session = session;
            Version = version;
            Phase = phase;
            Fault = fault;
        }

        public SessionToken Session { get; }
        public long Version { get; }
        public ImageRingPhase Phase { get; }
        public UserFault Fault { get; }
    }
}
