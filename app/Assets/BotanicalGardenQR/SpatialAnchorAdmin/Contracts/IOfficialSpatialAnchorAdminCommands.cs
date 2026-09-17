using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace BotanicalGardenQR.SpatialAnchorAdmin.Official
{
    public static class OfficialAnchorCandidatePoseReview
    {
        public static Pose ResolveBoundedPosition(
            Pose initialPose,
            Pose requestedPose,
            float maximumDistance)
        {
            if (!IsFinite(initialPose.position) || !IsFinite(initialPose.rotation) ||
                !IsFinite(requestedPose.position) || !float.IsFinite(maximumDistance) ||
                maximumDistance < 0f)
                return initialPose;
            var boundedOffset = Vector3.ClampMagnitude(
                requestedPose.position - initialPose.position,
                maximumDistance);
            return new Pose(initialPose.position + boundedOffset, initialPose.rotation);
        }

        static bool IsFinite(Vector3 value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        static bool IsFinite(Quaternion value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) &&
               float.IsFinite(value.z) && float.IsFinite(value.w);
    }

    public enum GuidedAnchorPlacementPhase
    {
        Idle = 0,
        PlacingCandidate = 1,
        PreparingCandidate = 2,
        ReviewingNewAnchor = 3,
        AdjustingSavedAnchor = 4,
        Saving = 5,
        SaveOutcomeUnknown = 6,
        PendingIndex = 7,
        PendingConfigurationRecord = 8,
        SaveFailed = 9,
        CleaningReplacedAnchor = 10
    }

    public enum OfficialAnchorLoadPhase
    {
        Idle = 0,
        Loading = 1,
        Completed = 2,
        Failed = 3
    }

    public readonly struct OfficialAnchorLoadState
    {
        public OfficialAnchorLoadState(
            OfficialAnchorLoadPhase phase,
            string message,
            int savedAnchorCount = 0,
            int trackedAnchorCount = 0)
        {
            Phase = phase;
            Message = message ?? string.Empty;
            SavedAnchorCount = Math.Max(0, savedAnchorCount);
            TrackedAnchorCount = Math.Clamp(trackedAnchorCount, 0, SavedAnchorCount);
        }

        public OfficialAnchorLoadPhase Phase { get; }
        public string Message { get; }
        public int SavedAnchorCount { get; }
        public int TrackedAnchorCount { get; }
        public bool IsLoading => Phase == OfficialAnchorLoadPhase.Loading;

        public OfficialAnchorLoadState WithCounts(int savedAnchorCount, int trackedAnchorCount)
            => new OfficialAnchorLoadState(Phase, Message, savedAnchorCount, trackedAnchorCount);
    }

    public readonly struct OfficialAnchorEraseResult
    {
        public OfficialAnchorEraseResult(bool succeeded, string platformStatus)
        {
            Succeeded = succeeded;
            PlatformStatus = platformStatus ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string PlatformStatus { get; }
    }

    /// <summary>
    /// UI-neutral handle over one official Meta anchor instance. The concrete Meta
    /// component and its visual implementation remain inside the Frontend adapter.
    /// </summary>
    public interface IOfficialSpatialAnchorHandle
    {
        Guid Uuid { get; }
        bool IsCreated { get; }
        bool IsTracked { get; }
        bool IsSaved { get; }
        bool IsSaveInProgress { get; }
        bool IsPendingIndex { get; }
        Pose WorldPose { get; }
        event Action<IOfficialSpatialAnchorHandle> Ready;
        event Action<IOfficialSpatialAnchorHandle> SaveSucceeded;
        event Action<IOfficialSpatialAnchorHandle, string> SaveFailed;
        event Action<IOfficialSpatialAnchorHandle, string> IndexingFailed;
        bool RequestSaveLocal();
        bool RetryPendingIndex(out string error);
        void SetAdminDisplayIdentity(string displayLabel, string recordStatus);
        bool DiscardIfUnsaved();
        void Release();
    }

    /// <summary>
    /// Port used by the Admin runtime coordinator to control the official Meta
    /// lifecycle without depending on its sample UI or concrete components.
    /// </summary>
    public interface IOfficialSpatialAnchorLifecycle
    {
        bool IsCreateMode { get; }
        bool IsLoading { get; }
        OfficialAnchorLoadState LoadState { get; }
        IOfficialSpatialAnchorHandle HoveredAnchor { get; }
        IReadOnlyList<IOfficialSpatialAnchorHandle> ActiveAnchors { get; }
        event Action<IOfficialSpatialAnchorHandle> AnchorPlaced;
        event Action<bool> CreateModeChanged;
        event Action AnchorsChanged;
        event Action<OfficialAnchorLoadState> LoadStateChanged;
        void SetEraseGuard(IOfficialSpatialAnchorAdminCommands eraseGuard);
        void RequestLoad();
        void SetCreateMode(bool createMode);
        void ShowCandidateReviewPose(Pose worldPose);
        void HideCandidateReviewPose();
        bool PlaceAnchorAtPose(Pose worldPose);
        Task<OfficialAnchorEraseResult> EraseAnchorsAsync(IReadOnlyList<Guid> anchorUuids);
        void DestroyAllLoadedAnchors();
    }

    /// <summary>
    /// Scene-local commands exposed by the official spatial-anchor administrator.
    /// This interface deliberately contains no content routing or visitor concepts.
    /// </summary>
    public interface IOfficialSpatialAnchorAdminCommands
    {
        bool IsReady { get; }
        bool IsCreateMode { get; }
        bool IsWorkspaceVisible { get; }
        bool IsBulkEraseInProgress { get; }
        bool IsEraseMode { get; }
        bool IsSingleAnchorEraseInProgress { get; }
        GuidedAnchorPlacementPhase GuidedPlacementPhase { get; }
        OfficialAnchorLoadState LoadState { get; }
        event Action<GuidedAnchorPlacementPhase, string> GuidedPlacementChanged;
        event Action<OfficialAnchorLoadState> LoadStateChanged;
        event Action<bool> WorkspaceVisibilityRequested;
        event Action<OfficialSpatialAnchorSnapshot> GuidedAnchorSaved;
        event Action SavedAnchorsChanged;
        event Action<OfficialAnchorAdjustmentSnapshot> AnchorAdjustmentChanged;
        event Action<bool, string> EraseModeChanged;
        event Action<bool, string> SingleAnchorEraseCompleted;
        event Action<bool, string> AllSavedAnchorsEraseCompleted;
        bool ToggleCreateMode();
        bool LoadAnchors(out string error);
        bool SetWorkspaceVisible(bool visible);
        bool BeginGuidedAnchorPlacement(out string error);
        bool PlaceGuidedAnchorCandidate(Pose worldPose, out string error);
        bool CommitNewAnchorCandidate(out string error);
        bool BeginRetuneHoveredAnchor(out string error);
        bool TryGetAnchorEditIdentity(out OfficialAnchorIdentitySnapshot identity);
        bool RenameAnchorBeingAdjusted(string customName, out string error);
        bool TryGetAnchorAdjustment(out OfficialAnchorAdjustmentSnapshot adjustment);
        bool AdjustAnchorAdjustmentPose(Vector3 worldDelta, out Pose adjustedPose, out string error);
        bool CommitAnchorAdjustment(out string error);
        bool RetryGuidedAnchorRecord(out string error);
        bool CancelGuidedAnchorPlacement(out string error);
        void ReportGuidedPlacementFailure(string error);
        IReadOnlyList<OfficialSpatialAnchorSnapshot> GetSavedAnchors();
        int GetLegacyMigrationCandidateCount();
        bool ImportLegacyAnchorsAsUnassigned(out int importedCount, out string error);
        bool RetryPendingAnchorIndex(Guid anchorUuid, out string error);
        bool SetEraseMode(bool active, out string error);
        bool BeginEraseHoveredAnchor(out string error);
        bool BeginErasePhysicalAnchor(Guid anchorUuid, out string error);
        bool BeginEraseAllSavedAnchors(out string error);
        bool TryPrepareEraseAnchor(Guid anchorUuid, out string reason);
    }

    public sealed class OfficialAnchorIdentitySnapshot
    {
        public OfficialAnchorIdentitySnapshot(int displayNumber, string customName, string displayLabel)
        {
            DisplayNumber = displayNumber;
            CustomName = customName ?? string.Empty;
            DisplayLabel = displayLabel ?? string.Empty;
        }

        public int DisplayNumber { get; }
        public string CustomName { get; }
        public string DisplayLabel { get; }
    }

    public readonly struct OfficialAnchorAdjustmentSnapshot
    {
        public OfficialAnchorAdjustmentSnapshot(
            Pose worldPose,
            float horizontalOffsetMeters,
            float verticalOffsetMeters)
        {
            WorldPose = worldPose;
            HorizontalOffsetMeters = horizontalOffsetMeters;
            VerticalOffsetMeters = verticalOffsetMeters;
        }

        public Pose WorldPose { get; }
        public float HorizontalOffsetMeters { get; }
        public float VerticalOffsetMeters { get; }
    }

    public sealed class OfficialSpatialAnchorSnapshot
    {
        public OfficialSpatialAnchorSnapshot(Guid uuid, bool isTracked, bool isSaved, Pose worldPose)
        {
            Uuid = uuid;
            IsTracked = isTracked;
            IsSaved = isSaved;
            WorldPose = worldPose;
        }

        public Guid Uuid { get; }
        public bool IsTracked { get; }
        public bool IsSaved { get; }
        public Pose WorldPose { get; }
    }
}
