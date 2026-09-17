using System;

namespace BotanicalGardenQR.ApplicationMode.Contracts
{
    public enum ApplicationModeRole
    {
        Visitor,
        Administrator
    }

    public enum ApplicationModePhase
    {
        Idle,
        Holding,
        Prompt,
        LoadingScene
    }

    public enum ApplicationModePromptKind
    {
        None,
        ReturnToVisitor
    }

    public enum ApplicationModeFault
    {
        None,
        SceneLoadFailed
    }

    public enum ApplicationModeIntentKind
    {
        Cancel,
        Confirm
    }

    public readonly struct ApplicationModeIntent
    {
        ApplicationModeIntent(ApplicationModeIntentKind kind)
        {
            Kind = kind;
        }

        public ApplicationModeIntentKind Kind { get; }

        public static ApplicationModeIntent Cancel =>
            new ApplicationModeIntent(ApplicationModeIntentKind.Cancel);

        public static ApplicationModeIntent Confirm =>
            new ApplicationModeIntent(ApplicationModeIntentKind.Confirm);
    }

    public readonly struct ApplicationModeState
    {
        public ApplicationModeState(
            long version,
            ApplicationModeRole role,
            ApplicationModePhase phase,
            ApplicationModePromptKind prompt,
            ApplicationModeFault fault,
            float holdProgress)
        {
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (holdProgress < 0f || holdProgress > 1f ||
                float.IsNaN(holdProgress) || float.IsInfinity(holdProgress))
                throw new ArgumentOutOfRangeException(nameof(holdProgress));
            Version = version;
            Role = role;
            Phase = phase;
            Prompt = prompt;
            Fault = fault;
            HoldProgress = holdProgress;
        }

        public long Version { get; }
        public ApplicationModeRole Role { get; }
        public ApplicationModePhase Phase { get; }
        public ApplicationModePromptKind Prompt { get; }
        public ApplicationModeFault Fault { get; }
        public float HoldProgress { get; }

        public bool IsPromptVisible => Prompt != ApplicationModePromptKind.None;
        public bool IsInteractionBlocked => IsPromptVisible ||
                                            Phase == ApplicationModePhase.LoadingScene;
        public bool CanConfirm => Phase == ApplicationModePhase.Prompt &&
                                  Prompt == ApplicationModePromptKind.ReturnToVisitor;
        public bool CanCancel => Phase == ApplicationModePhase.Prompt;
    }

    public readonly struct ApplicationModeLoadResult
    {
        ApplicationModeLoadResult(bool succeeded) { Succeeded = succeeded; }

        public bool Succeeded { get; }
        public static ApplicationModeLoadResult Success => new ApplicationModeLoadResult(true);
        public static ApplicationModeLoadResult Failed => new ApplicationModeLoadResult(false);
    }

    public interface IApplicationModeSceneLoader
    {
        void Load(
            ApplicationModeRole targetRole,
            Action<ApplicationModeLoadResult> completed);
    }

    public interface IApplicationModeController
    {
        ApplicationModeState CurrentState { get; }
        void Advance(float unscaledDeltaSeconds, bool isLeftMenuPressed);
        void Dispatch(ApplicationModeIntent intent);
        IDisposable Observe(IApplicationModeStateSink sink);
    }

    public interface IApplicationModeStateSink
    {
        void OnApplicationModeStateChanged(ApplicationModeState state);
    }
}
