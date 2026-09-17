using System;
using UnityEngine;

namespace BotanicalGardenQR.VisitorAtlasHub.Contracts
{
    public enum VisitorAtlasHubPhase
    {
        Uninitialized = 0,
        InitializingHidden = 1,
        ReadyHidden = 2,
        Revealing = 3,
        Visible = 4,
        Failed = 5,
        Disposed = 6
    }

    public enum VisitorAtlasHubBookState
    {
        ClosedInteractive = 0,
        Opening = 1,
        AwaitingBrowseConfirmation = 2,
        OpenLocked = 3
    }

    public enum VisitorAtlasHubPalmStage
    {
        Inactive = 0,
        NoReliableHand = 1,
        TurnPalmUp = 2,
        PositionAtChest = 3,
        ReadyToSummon = 4,
        Summoned = 5
    }

    public enum VisitorAtlasHubFailureStage
    {
        Configuration = 0,
        InitialPose = 1,
        MapLoad = 2
    }

    public readonly struct VisitorAtlasHubFailure
    {
        public VisitorAtlasHubFailure(VisitorAtlasHubFailureStage stage, string diagnostic)
        {
            Stage = stage;
            Diagnostic = diagnostic?.Trim() ?? string.Empty;
        }

        public VisitorAtlasHubFailureStage Stage { get; }
        public string Diagnostic { get; }
    }

    /// <summary>
    /// UI-neutral result of one frame of raw hand assessment. Raw Hand,
    /// Handedness and wrist transforms remain inside Presentation.
    /// </summary>
    public readonly struct VisitorAtlasHubPalmSample
    {
        public VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage stage, bool hasReliableTracking)
        {
            Stage = stage;
            HasReliableTracking = hasReliableTracking;
        }

        public VisitorAtlasHubPalmStage Stage { get; }
        public bool HasReliableTracking { get; }
        public bool IsCandidate => Stage == VisitorAtlasHubPalmStage.ReadyToSummon;
    }

    public readonly struct VisitorAtlasHubConfiguration
    {
        public VisitorAtlasHubConfiguration(
            float sessionRootWorldHeight,
            float poseAttemptTimeoutSeconds,
            float retryDelaySeconds,
            float palmHoldSeconds,
            float trackingGraceSeconds,
            float gestureReleaseSeconds,
            float revealSeconds,
            float bookOpenSeconds,
            float browseConfirmationTimeoutSeconds)
        {
            if (!IsFinite(sessionRootWorldHeight)) throw new ArgumentOutOfRangeException(nameof(sessionRootWorldHeight));
            PoseAttemptTimeoutSeconds = Positive(poseAttemptTimeoutSeconds, nameof(poseAttemptTimeoutSeconds));
            RetryDelaySeconds = NonNegative(retryDelaySeconds, nameof(retryDelaySeconds));
            PalmHoldSeconds = Positive(palmHoldSeconds, nameof(palmHoldSeconds));
            TrackingGraceSeconds = NonNegative(trackingGraceSeconds, nameof(trackingGraceSeconds));
            GestureReleaseSeconds = Positive(gestureReleaseSeconds, nameof(gestureReleaseSeconds));
            RevealSeconds = Positive(revealSeconds, nameof(revealSeconds));
            BookOpenSeconds = Positive(bookOpenSeconds, nameof(bookOpenSeconds));
            BrowseConfirmationTimeoutSeconds = Positive(
                browseConfirmationTimeoutSeconds,
                nameof(browseConfirmationTimeoutSeconds));
            SessionRootWorldHeight = sessionRootWorldHeight;
        }

        public float SessionRootWorldHeight { get; }
        public float PoseAttemptTimeoutSeconds { get; }
        public float RetryDelaySeconds { get; }
        public float PalmHoldSeconds { get; }
        public float TrackingGraceSeconds { get; }
        public float GestureReleaseSeconds { get; }
        public float RevealSeconds { get; }
        public float BookOpenSeconds { get; }
        public float BrowseConfirmationTimeoutSeconds { get; }

        static float Positive(float value, string name)
        {
            if (!(value > 0f) || !IsFinite(value)) throw new ArgumentOutOfRangeException(name);
            return value;
        }

        static float NonNegative(float value, string name)
        {
            if (value < 0f || !IsFinite(value)) throw new ArgumentOutOfRangeException(name);
            return value;
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Presentation seam. Unity objects are confined to this module-facing
    /// authoring contract and never appear in the application state/events.
    /// </summary>
    public interface IVisitorAtlasHubPresentation : IDisposable
    {
        Transform MapContentRoot { get; }
        string StreamingAssetsPath { get; }
        VisitorAtlasHubConfiguration Configuration { get; }
        event Action BookSelected;
        event Action MapSelected;
        event Action HideRequested;
        event Action<int> BookOpeningCompleted;
        void Configure(Transform viewer, Transform interactionRigRoot);
        VisitorAtlasHubPalmSample SamplePalmCandidate();
        void CommitSessionRoot(Pose sessionRootPose);
        void RefreshEntryChoicePose();
        void SetVisible(bool visible);
        void SetEntryLayerVisible(bool visible);
        void SetMapDisplay(bool requested, bool visible);
        void SetInteractionEnabled(
            bool bookEnabled,
            bool mapEnabled,
            bool hideEnabled);
        void PlayReveal();
        void BeginBookOpening(int generation);
        void SetBookOpenLocked();
        void ResetBook();
        void RejectBookSelection();
    }

    /// <summary>
    /// Visitor-global application-facing owner. It exposes only lifecycle and
    /// semantic intents; raw hand data, viewer transforms and GLB leases stay
    /// behind the module boundary.
    /// </summary>
    public interface IVisitorAtlasHubController : IDisposable
    {
        VisitorAtlasHubPhase Phase { get; }
        VisitorAtlasHubBookState BookState { get; }
        bool InteractionGateOpen { get; }
        bool IsVisible { get; }
        bool MapRequested { get; }
        bool IsMapVisible { get; }
        event Action<VisitorAtlasHubPhase> PhaseChanged;
        event Action<VisitorAtlasHubPalmStage> PalmStageChanged;
        event Action Summoned;
        event Action BookSelected;
        event Action<int> OpenCollectionRequested;
        event Action Hidden;
        event Action<VisitorAtlasHubFailure> Failed;
        void SetInteractionGate(bool open);
        void Tick(float unscaledDeltaSeconds);
        bool TryBeginBookOpening(out int generation);
        void ConfirmCollectionOpened(int generation);
        void CancelBookOpening(int generation = 0);
        void NotifyCollectionClosed();
        void RejectBookSelection();
        void Hide();
        void SetMapSuppressed(bool suppressed);
    }
}
