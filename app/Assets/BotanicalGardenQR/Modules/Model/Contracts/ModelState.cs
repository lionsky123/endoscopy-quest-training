using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Model.Contracts
{
    public enum ModelPhase { Closed, Loading, Ready, Failed }

    public readonly struct ModelState
    {
        public ModelState(
            SessionToken session,
            long version,
            ModelPhase phase,
            bool canToggleAutoMotion,
            bool isAutoMotionActive,
            bool canResetView,
            bool canPlayAnimation,
            bool isAnimationPlaying,
            UserFault fault = default)
        {
            if (version < 0)
                throw new System.ArgumentOutOfRangeException(nameof(version));
            Session = session;
            Version = version;
            Phase = phase;
            CanToggleAutoMotion = canToggleAutoMotion;
            IsAutoMotionActive = isAutoMotionActive;
            CanResetView = canResetView;
            CanPlayAnimation = canPlayAnimation;
            IsAnimationPlaying = isAnimationPlaying;
            Fault = fault;
        }

        public SessionToken Session { get; }
        public long Version { get; }
        public ModelPhase Phase { get; }
        public bool CanToggleAutoMotion { get; }
        public bool IsAutoMotionActive { get; }
        public bool CanResetView { get; }
        public bool CanPlayAnimation { get; }
        public bool IsAnimationPlaying { get; }
        public UserFault Fault { get; }
    }
}
