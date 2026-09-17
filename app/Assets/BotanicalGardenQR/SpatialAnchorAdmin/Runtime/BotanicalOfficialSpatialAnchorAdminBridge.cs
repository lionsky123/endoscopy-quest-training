using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using UnityEngine;
using UnityEngine.Serialization;
using BotanicalGardenQR.SpatialAnchorAdmin.Persistence;

namespace BotanicalGardenQR.SpatialAnchorAdmin.Official
{
    /// <summary>
    /// Scene-local facade over the official Meta anchor manager.
    /// The official manager remains the lifecycle owner.
    /// </summary>
    public sealed class BotanicalOfficialSpatialAnchorAdminBridge : MonoBehaviour,
        IOfficialSpatialAnchorAdminCommands
    {
        const float MaximumCandidateFineTuneDistanceMeters = 0.2f;

        [SerializeField, FormerlySerializedAs("_manager")] MonoBehaviour _lifecycleSource;
        [SerializeField] PhysicalAugmentationCatalogAsset _physicalAugmentationCatalog;
        [SerializeField, Min(5f)] float _saveOutcomeWarningSeconds = 20f;

        IOfficialSpatialAnchorLifecycle _lifecycle;
        IOfficialSpatialAnchorHandle _guidedCandidate;
        IOfficialSpatialAnchorHandle _retuneOriginalAnchor;
        string _guidedPhysicalAugmentationPointId = string.Empty;
        Guid _retuneOriginalUuid;
        Pose _guidedAugmentationPoseInAnchorSpace = Pose.identity;
        float _guidedUniformScale = 1f;
        Pose _guidedCandidateInitialPose;
        Pose _guidedCandidatePose;
        int _retuneOriginalDisplayNumber;
        string _retuneOriginalCustomName = string.Empty;
        bool _hasGuidedCandidatePose;
        bool _commitCandidateWhenReady;
        bool _isWorkspaceVisible = true;
        readonly HashSet<Guid> _singleEraseInProgress = new HashSet<Guid>();
        float _saveOutcomeWarningAt = float.PositiveInfinity;

        public bool IsReady { get; private set; }
        public bool IsCreateMode => _lifecycle != null && _lifecycle.IsCreateMode;
        public bool IsWorkspaceVisible => _isWorkspaceVisible;
        public OfficialAnchorLoadState LoadState { get; private set; }
            = new OfficialAnchorLoadState(OfficialAnchorLoadPhase.Idle, string.Empty);
        public bool IsBulkEraseInProgress { get; private set; }
        public bool IsEraseMode { get; private set; }
        public bool IsSingleAnchorEraseInProgress => _singleEraseInProgress.Count > 0;
        public GuidedAnchorPlacementPhase GuidedPlacementPhase { get; private set; }
        public event Action<GuidedAnchorPlacementPhase, string> GuidedPlacementChanged;
        public event Action<OfficialAnchorLoadState> LoadStateChanged;
        public event Action<bool> WorkspaceVisibilityRequested;
        public event Action<OfficialSpatialAnchorSnapshot> GuidedAnchorSaved;
        public event Action SavedAnchorsChanged;
        public event Action<OfficialAnchorAdjustmentSnapshot> AnchorAdjustmentChanged;
        public event Action<bool, string> EraseModeChanged;
        public event Action<bool, string> SingleAnchorEraseCompleted;
        public event Action<bool, string> AllSavedAnchorsEraseCompleted;

        void Awake()
        {
            _lifecycle = _lifecycleSource as IOfficialSpatialAnchorLifecycle;
            if (_lifecycle == null)
            {
                Debug.LogError("[BotanicalAdminBridge] Required official anchor references are missing.");
                return;
            }

            IsReady = true;
            _lifecycle.SetEraseGuard(this);
            _lifecycle.AnchorPlaced += HandleAnchorPlaced;
            _lifecycle.CreateModeChanged += HandleModeChanged;
            _lifecycle.AnchorsChanged += HandleAnchorsChanged;
            _lifecycle.LoadStateChanged += HandleLoadStateChanged;
            RefreshLoadState(_lifecycle.LoadState, false);
            if (!TryReconcileConfiguredBindings(out var reconciliationError))
                Debug.LogError($"[PhysicalAnchorAdmin] {reconciliationError}", this);
            RefreshAnchorLabels();
        }

        void OnDestroy()
        {
            ReleaseCandidateSubscriptions();
            if (_lifecycle == null) return;
            _lifecycle.AnchorPlaced -= HandleAnchorPlaced;
            _lifecycle.CreateModeChanged -= HandleModeChanged;
            _lifecycle.AnchorsChanged -= HandleAnchorsChanged;
            _lifecycle.LoadStateChanged -= HandleLoadStateChanged;
        }

        void Update()
        {
            if (GuidedPlacementPhase != GuidedAnchorPlacementPhase.Saving ||
                _guidedCandidate == null ||
                Time.unscaledTime < _saveOutcomeWarningAt)
                return;

            _saveOutcomeWarningAt = float.PositiveInfinity;
            SetGuidedPhase(
                GuidedAnchorPlacementPhase.SaveOutcomeUnknown,
                "设备长时间未返回保存结果。不要重复保存；可返回管理员菜单，系统会继续等待原请求结果。" );
            SetWorkspaceVisible(true);
        }

        public bool ToggleCreateMode()
        {
            if (!IsReady) return false;
            if (IsCreateMode)
                return CancelGuidedAnchorPlacement(out _);
            return BeginGuidedAnchorPlacement(out _);
        }

        public bool LoadAnchors(out string error)
        {
            error = string.Empty;
            if (!IsReady)
            {
                error = "官方锚点管理尚未准备好。";
                return false;
            }
            if (IsEraseMode || IsSingleAnchorEraseInProgress)
            {
                error = "请先结束当前删除操作。";
                return false;
            }
            if (!TryReconcileConfiguredBindings(out var reconciliationError))
            {
                error = $"刷新代码配置关联失败：{reconciliationError}";
                return false;
            }
            _lifecycle.RequestLoad();
            return true;
        }

        public bool SetWorkspaceVisible(bool visible)
        {
            _isWorkspaceVisible = visible;
            WorkspaceVisibilityRequested?.Invoke(visible);
            return true;
        }

        public bool BeginGuidedAnchorPlacement(out string error)
        {
            error = string.Empty;
            if (!IsReady)
            {
                error = "官方锚点管理尚未准备好。";
                return false;
            }
            if (GuidedPlacementPhase != GuidedAnchorPlacementPhase.Idle)
            {
                error = "已有定位点候选流程正在进行。";
                return false;
            }
            if (IsEraseMode || IsSingleAnchorEraseInProgress)
            {
                error = "请先结束当前删除操作。";
                return false;
            }
            if (!PhysicalAnchorAdminBindingServices.Current.TryRead(out _, out var diagnosticTag))
            {
                error = $"设备锚点记录不可用：{diagnosticTag}";
                return false;
            }
            ClearGuidedCandidatePose();
            _guidedPhysicalAugmentationPointId = string.Empty;
            SetWorkspaceVisible(false);
            SetGuidedPhase(
                GuidedAnchorPlacementPhase.PlacingCandidate,
                "正在放置新锚点。射线命中现实表面后按 X 创建候选；创建后需在面板明确保存。" );
            _lifecycle.SetCreateMode(true);
            return true;
        }

        public bool PlaceGuidedAnchorCandidate(Pose worldPose, out string error)
        {
            error = string.Empty;
            if (!IsReady || GuidedPlacementPhase != GuidedAnchorPlacementPhase.PlacingCandidate ||
                !IsCreateMode)
            {
                error = "当前不在定位点候选放置步骤。";
                return false;
            }
            if (!IsFinite(worldPose.position) || !IsFinite(worldPose.rotation))
            {
                error = "定位点候选 Pose 无效。";
                return false;
            }
            _guidedCandidateInitialPose = worldPose;
            _guidedCandidatePose = worldPose;
            _hasGuidedCandidatePose = true;
            _guidedAugmentationPoseInAnchorSpace = Pose.identity;
            _guidedUniformScale = 1f;
            SetGuidedPhase(
                GuidedAnchorPlacementPhase.PreparingCandidate,
                "正在创建未保存锚点候选…");
            _lifecycle.SetCreateMode(false);
            _lifecycle.HideCandidateReviewPose();
            SetWorkspaceVisible(false);
            _commitCandidateWhenReady = false;
            if (_lifecycle.PlaceAnchorAtPose(worldPose)) return true;

            _commitCandidateWhenReady = false;
            SetGuidedPhase(
                GuidedAnchorPlacementPhase.PlacingCandidate,
                "官方锚点管理未能创建锚点，请重新瞄准现实表面后按 X。" );
            _lifecycle.SetCreateMode(true);
            error = "官方锚点管理未能创建锚点。";
            return false;
        }

        public bool CommitNewAnchorCandidate(out string error)
        {
            error = string.Empty;
            if (_retuneOriginalUuid != Guid.Empty)
            {
                error = "当前候选属于已保存锚点的位置微调。";
                return false;
            }
            if (GuidedPlacementPhase != GuidedAnchorPlacementPhase.ReviewingNewAnchor &&
                GuidedPlacementPhase != GuidedAnchorPlacementPhase.SaveFailed)
            {
                error = "当前没有等待确认保存的新锚点。";
                return false;
            }
            return BeginGuidedCandidateSave(out error);
        }

        public bool BeginRetuneHoveredAnchor(out string error)
        {
            error = string.Empty;
            if (!IsReady || GuidedPlacementPhase != GuidedAnchorPlacementPhase.Idle ||
                IsCreateMode || IsEraseMode || IsBulkEraseInProgress || IsSingleAnchorEraseInProgress ||
                (_lifecycle != null && _lifecycle.IsLoading))
            {
                error = "官方锚点管理当前不能开始位置微调。";
                return false;
            }

            var hovered = _lifecycle != null ? _lifecycle.HoveredAnchor : null;
            if (hovered == null)
            {
                error = "左射线未指向已保存锚点。";
                return false;
            }
            if (!hovered.IsSaved || !hovered.IsTracked || hovered.Uuid == Guid.Empty)
            {
                error = "只能微调当前已保存且已跟踪的锚点。";
                SetGuidedPhase(GuidedAnchorPlacementPhase.Idle, error);
                SetWorkspaceVisible(true);
                return false;
            }
            if (!PhysicalAnchorAdminBindingServices.Current.TryRead(out var installation, out var diagnosticTag))
            {
                error = $"设备安装记录不可用：{diagnosticTag}";
                SetGuidedPhase(GuidedAnchorPlacementPhase.Idle, error);
                SetWorkspaceVisible(true);
                return false;
            }

            var record = installation.Records.FirstOrDefault(candidate =>
                candidate.Uuid == hovered.Uuid &&
                candidate.Status != PhysicalAnchorAdminRecordStatus.PendingIndex);
            if (record.Uuid == Guid.Empty)
            {
                error = "该锚点尚未完成设备索引，当前不能微调。";
                SetGuidedPhase(GuidedAnchorPlacementPhase.Idle, error);
                SetWorkspaceVisible(true);
                return false;
            }

            _retuneOriginalAnchor = hovered;
            _retuneOriginalUuid = hovered.Uuid;
            _guidedPhysicalAugmentationPointId = record.PointId;
            _guidedAugmentationPoseInAnchorSpace = record.AugmentationPoseInAnchorSpace;
            _guidedUniformScale = record.UniformScale;
            _retuneOriginalDisplayNumber = record.DisplayNumber;
            _retuneOriginalCustomName = record.CustomName;
            _guidedCandidateInitialPose = hovered.WorldPose;
            _guidedCandidatePose = hovered.WorldPose;
            _hasGuidedCandidatePose = true;
            _lifecycle.ShowCandidateReviewPose(_guidedCandidatePose);
            SetGuidedPhase(
                GuidedAnchorPlacementPhase.AdjustingSavedAnchor,
                $"已选择 {record.DisplayLabel}。可修改名称；使用左摇杆微调位置后选择“重新保存位置”。" );
            SetWorkspaceVisible(true);
            PublishAnchorAdjustment();
            return true;
        }

        public bool TryGetAnchorEditIdentity(out OfficialAnchorIdentitySnapshot identity)
        {
            identity = null;
            if (_retuneOriginalUuid == Guid.Empty ||
                !PhysicalAnchorAdminBindingServices.Current.TryRead(out var installation, out _))
                return false;
            var record = installation.Records.FirstOrDefault(candidate =>
                candidate.Uuid == _retuneOriginalUuid &&
                candidate.Status != PhysicalAnchorAdminRecordStatus.PendingIndex);
            if (record.Uuid == Guid.Empty || record.DisplayNumber <= 0) return false;
            identity = new OfficialAnchorIdentitySnapshot(
                record.DisplayNumber,
                record.CustomName,
                record.DisplayLabel);
            return true;
        }

        public bool RenameAnchorBeingAdjusted(string customName, out string error)
        {
            error = string.Empty;
            if (GuidedPlacementPhase != GuidedAnchorPlacementPhase.AdjustingSavedAnchor ||
                _retuneOriginalUuid == Guid.Empty)
            {
                error = "请先用左射线点击一个已保存锚点。";
                return false;
            }

            var normalized = string.IsNullOrWhiteSpace(customName) ? string.Empty : customName.Trim();
            var hasControlCharacter = false;
            for (var index = 0; index < normalized.Length; index++)
                if (char.IsControl(normalized[index]))
                {
                    hasControlCharacter = true;
                    break;
                }
            if (normalized.Length > 24 || hasControlCharacter)
            {
                error = "名称最多 24 个字符，且不能包含换行或控制字符。";
                return false;
            }

            var result = PhysicalAnchorAdminBindingServices.Current.RenameAnchor(
                _retuneOriginalUuid,
                normalized);
            if (!result.Succeeded)
            {
                error = $"锚点名称未保存：{result.DiagnosticTag}";
                return false;
            }

            _retuneOriginalCustomName = normalized;
            RefreshAnchorLabels();
            SavedAnchorsChanged?.Invoke();
            var label = TryGetAnchorEditIdentity(out var identity)
                ? identity.DisplayLabel
                : "当前锚点";
            SetGuidedPhase(
                GuidedAnchorPlacementPhase.AdjustingSavedAnchor,
                $"名称已保存为“{label}”。可继续微调位置并重新保存，或取消返回。" );
            return true;
        }

        public bool TryGetAnchorAdjustment(out OfficialAnchorAdjustmentSnapshot adjustment)
        {
            adjustment = default;
            if (GuidedPlacementPhase != GuidedAnchorPlacementPhase.AdjustingSavedAnchor ||
                !_hasGuidedCandidatePose || _guidedCandidate != null)
                return false;
            adjustment = CreateAnchorAdjustmentSnapshot();
            return true;
        }

        public bool AdjustAnchorAdjustmentPose(
            Vector3 worldDelta,
            out Pose adjustedPose,
            out string error)
        {
            adjustedPose = _guidedCandidatePose;
            error = string.Empty;
            if (!TryGetAnchorAdjustment(out var currentAdjustment))
            {
                error = "当前没有可用摇杆微调的已保存锚点。";
                return false;
            }
            if (!IsFinite(worldDelta))
            {
                error = "摇杆微调位移无效。";
                return false;
            }

            adjustedPose = OfficialAnchorCandidatePoseReview.ResolveBoundedPosition(
                _guidedCandidateInitialPose,
                new Pose(
                    currentAdjustment.WorldPose.position + worldDelta,
                    currentAdjustment.WorldPose.rotation),
                MaximumCandidateFineTuneDistanceMeters);
            _guidedCandidatePose = adjustedPose;
            _lifecycle.ShowCandidateReviewPose(adjustedPose);
            PublishAnchorAdjustment();
            return true;
        }

        public bool CommitAnchorAdjustment(out string error)
        {
            error = string.Empty;
            if (GuidedPlacementPhase == GuidedAnchorPlacementPhase.PendingIndex ||
                GuidedPlacementPhase == GuidedAnchorPlacementPhase.PendingConfigurationRecord)
                return RetryGuidedAnchorRecord(out error);
            if (GuidedPlacementPhase == GuidedAnchorPlacementPhase.SaveFailed && _guidedCandidate != null)
                return BeginGuidedCandidateSave(out error);
            if (GuidedPlacementPhase != GuidedAnchorPlacementPhase.AdjustingSavedAnchor)
            {
                error = "当前没有可重新保存的位置微调。";
                return false;
            }
            if (!_hasGuidedCandidatePose)
            {
                error = "当前没有可重新保存的位置微调。";
                return false;
            }

            SetGuidedPhase(
                GuidedAnchorPlacementPhase.PreparingCandidate,
                "正在按微调后的位置创建替换锚点…");
            SetWorkspaceVisible(false);
            _lifecycle.HideCandidateReviewPose();
            _commitCandidateWhenReady = true;
            if (_lifecycle.PlaceAnchorAtPose(_guidedCandidatePose)) return true;

            _commitCandidateWhenReady = false;
            _lifecycle.ShowCandidateReviewPose(_guidedCandidatePose);
            SetWorkspaceVisible(true);
            SetGuidedPhase(
                GuidedAnchorPlacementPhase.AdjustingSavedAnchor,
                "官方锚点管理未能创建替换锚点；原锚点保持有效，可继续微调或取消。" );
            error = "官方锚点管理未能创建替换锚点。";
            return false;
        }

        bool BeginGuidedCandidateSave(out string error)
        {
            error = string.Empty;
            if (_guidedCandidate == null || !_guidedCandidate.IsCreated ||
                _guidedCandidate.Uuid == Guid.Empty || _guidedCandidate.IsSaved)
            {
                error = "当前没有可保存的未保存锚点候选。";
                return false;
            }

            SetGuidedPhase(
                GuidedAnchorPlacementPhase.Saving,
                _retuneOriginalUuid == Guid.Empty ? "正在保存新锚点…" : "正在保存微调后的替换锚点…");
            if (_guidedCandidate.RequestSaveLocal())
            {
                if (GuidedPlacementPhase == GuidedAnchorPlacementPhase.Saving && _guidedCandidate != null)
                    _saveOutcomeWarningAt = Time.unscaledTime + Mathf.Max(5f, _saveOutcomeWarningSeconds);
                return true;
            }

            _saveOutcomeWarningAt = float.PositiveInfinity;
            SetGuidedPhase(GuidedAnchorPlacementPhase.SaveFailed, "锚点尚未能开始保存；可重试或取消。" );
            SetWorkspaceVisible(true);
            error = "候选锚点当前不能保存。";
            return false;
        }

        bool CompleteAnchorRecord(out string error)
        {
            error = string.Empty;
            if (_guidedCandidate == null || !_guidedCandidate.IsSaved)
            {
                error = "当前没有可完成的锚点记录。";
                return false;
            }

            if (_retuneOriginalUuid != Guid.Empty)
            {
                var replacement = string.IsNullOrWhiteSpace(_guidedPhysicalAugmentationPointId)
                    ? PhysicalAnchorAdminBindingServices.Current.ReplaceUnassignedAnchorIdentity(
                        _retuneOriginalUuid,
                        _guidedCandidate.Uuid,
                        _retuneOriginalDisplayNumber,
                        _retuneOriginalCustomName)
                    : PhysicalAnchorAdminBindingServices.Current.AssignOrRecalibrate(
                        _guidedPhysicalAugmentationPointId,
                        _guidedCandidate.Uuid,
                        _guidedAugmentationPoseInAnchorSpace,
                        _guidedUniformScale);
                if (!replacement.Succeeded)
                {
                    error = $"空间锚点已保存，但替换记录写入失败：{replacement.DiagnosticTag}。可重试记录，不会重复保存 Meta 锚点。";
                    SetGuidedPhase(GuidedAnchorPlacementPhase.PendingConfigurationRecord, error);
                    SetWorkspaceVisible(true);
                    return false;
                }
            }

            if (!TryReconcileConfiguredBindings(out error))
            {
                error = $"空间锚点已保存，但代码配置关联失败：{error}。可重试记录，不会重复保存 Meta 锚点。";
                SetGuidedPhase(GuidedAnchorPlacementPhase.PendingConfigurationRecord, error);
                SetWorkspaceVisible(true);
                return false;
            }

            var snapshot = new OfficialSpatialAnchorSnapshot(
                _guidedCandidate.Uuid,
                _guidedCandidate.IsTracked,
                true,
                _guidedCandidate.WorldPose);
            var replacedUuid = _retuneOriginalUuid;
            var replacedAnchor = _retuneOriginalAnchor;
            ReleaseCandidateSubscriptions();
            _lifecycle.SetCreateMode(false);
            ClearGuidedTransaction();
            RefreshAnchorLabels();
            SavedAnchorsChanged?.Invoke();
            GuidedAnchorSaved?.Invoke(snapshot);
            if (replacedUuid != Guid.Empty)
            {
                SetGuidedPhase(
                    GuidedAnchorPlacementPhase.CleaningReplacedAnchor,
                    "新位置已保存并切换，正在清理旧锚点…");
                SetWorkspaceVisible(true);
                _ = CompleteRetunedAnchorReplacementAsync(replacedUuid, replacedAnchor);
                return true;
            }

            SetGuidedPhase(GuidedAnchorPlacementPhase.Idle, "锚点已明确保存；可继续放置更多锚点，对应内容只由项目配置决定。" );
            SetWorkspaceVisible(true);
            return true;
        }

        public bool RetryGuidedAnchorRecord(out string error)
        {
            error = string.Empty;
            if (_guidedCandidate == null)
            {
                error = "当前没有待重试的锚点记录。";
                return false;
            }
            if (GuidedPlacementPhase == GuidedAnchorPlacementPhase.PendingConfigurationRecord)
                return CompleteAnchorRecord(out error);
            if (GuidedPlacementPhase != GuidedAnchorPlacementPhase.PendingIndex)
            {
                error = "当前没有待重试的锚点记录。";
                return false;
            }
            if (!_guidedCandidate.RetryPendingIndex(out error))
            {
                SetGuidedPhase(GuidedAnchorPlacementPhase.PendingIndex, error);
                return false;
            }
            return GuidedPlacementPhase == GuidedAnchorPlacementPhase.Idle ||
                   GuidedPlacementPhase == GuidedAnchorPlacementPhase.CleaningReplacedAnchor;
        }

        public bool CancelGuidedAnchorPlacement(out string error)
        {
            error = string.Empty;
            if (GuidedPlacementPhase == GuidedAnchorPlacementPhase.Saving ||
                GuidedPlacementPhase == GuidedAnchorPlacementPhase.SaveOutcomeUnknown ||
                GuidedPlacementPhase == GuidedAnchorPlacementPhase.PreparingCandidate ||
                GuidedPlacementPhase == GuidedAnchorPlacementPhase.CleaningReplacedAnchor ||
                GuidedPlacementPhase == GuidedAnchorPlacementPhase.PendingIndex ||
                GuidedPlacementPhase == GuidedAnchorPlacementPhase.PendingConfigurationRecord)
            {
                error = "定位点事务仍在进行，当前不能取消或重复提交。";
                return false;
            }
            var canceledNewCandidate = _retuneOriginalUuid == Guid.Empty && _guidedCandidate != null;
            if (_guidedCandidate != null && !_guidedCandidate.DiscardIfUnsaved())
            {
                error = "当前候选不能取消。";
                return false;
            }

            ReleaseCandidateSubscriptions();
            _lifecycle?.SetCreateMode(false);
            ClearGuidedTransaction();
            SetGuidedPhase(
                GuidedAnchorPlacementPhase.Idle,
                canceledNewCandidate
                    ? "已取消未保存锚点；没有写入设备记录。"
                    : "已取消当前操作；原锚点与设备记录未改变。");
            SetWorkspaceVisible(true);
            return true;
        }

        public void ReportGuidedPlacementFailure(string error)
        {
            var message = string.IsNullOrWhiteSpace(error)
                ? "定位点候选放置失败，请移动左手柄后重试。"
                : error;
            SetGuidedPhase(GuidedPlacementPhase, message);
        }

        public IReadOnlyList<OfficialSpatialAnchorSnapshot> GetSavedAnchors()
        {
            if (!IsReady) return Array.Empty<OfficialSpatialAnchorSnapshot>();

            var loaded = _lifecycle.ActiveAnchors
                .Where(anchor => anchor != null && anchor.IsSaved && anchor.Uuid != Guid.Empty)
                .GroupBy(anchor => anchor.Uuid)
                .ToDictionary(group => group.Key, group => group.First());
            if (!PhysicalAnchorAdminBindingServices.Current.TryRead(out var installation, out var diagnosticTag))
            {
                Debug.LogError($"[PhysicalAnchorAdmin] Cannot read installation index: {diagnosticTag}", this);
                return Array.Empty<OfficialSpatialAnchorSnapshot>();
            }
            return installation.Records
                .Select(record => record.Uuid)
                .OrderBy(uuid => uuid)
                .Select(uuid => loaded.TryGetValue(uuid, out var anchor)
                    ? new OfficialSpatialAnchorSnapshot(
                        uuid,
                        anchor.IsTracked,
                        true,
                        anchor.WorldPose)
                    : new OfficialSpatialAnchorSnapshot(uuid, false, true, default))
                .ToArray();
        }

        public int GetLegacyMigrationCandidateCount()
            => new LegacyAnchorUuidMigrationSource().ReadCandidates().Length;

        public bool ImportLegacyAnchorsAsUnassigned(out int importedCount, out string error)
        {
            error = string.Empty;
            var result = PhysicalAnchorAdminBindingServices.Current.ImportLegacyAsUnassigned(
                new LegacyAnchorUuidMigrationSource(),
                out importedCount);
            if (!result.Succeeded)
            {
                error = $"旧 UUID 导入失败：{result.DiagnosticTag}。已导入的记录保持有效，可重试。";
                return false;
            }
            SavedAnchorsChanged?.Invoke();
            return true;
        }

        public bool RetryPendingAnchorIndex(Guid anchorUuid, out string error)
        {
            error = string.Empty;
            var anchor = _lifecycle.ActiveAnchors.FirstOrDefault(candidate =>
                candidate != null && candidate.Uuid == anchorUuid && candidate.IsPendingIndex);
            if (anchor == null)
            {
                error = "待索引锚点不在当前 Admin 会话中，不能猜测或自动重建。";
                return false;
            }
            if (!anchor.RetryPendingIndex(out error)) return false;
            SavedAnchorsChanged?.Invoke();
            return true;
        }

        public bool BeginErasePhysicalAnchor(Guid anchorUuid, out string error)
        {
            error = string.Empty;
            if (!IsReady || IsBulkEraseInProgress ||
                GuidedPlacementPhase != GuidedAnchorPlacementPhase.Idle || IsCreateMode ||
                (_lifecycle != null && _lifecycle.IsLoading))
            {
                error = "官方锚点管理当前不能执行单点擦除。";
                return false;
            }
            if (anchorUuid == Guid.Empty || IsSingleAnchorEraseInProgress)
            {
                error = "目标锚点无效或擦除已在进行。";
                return false;
            }
            var prepared = PhysicalAnchorAdminBindingServices.Current.PrepareErase(anchorUuid);
            if (!prepared.Succeeded)
            {
                error = $"无法先解除活动绑定：{prepared.DiagnosticTag}。未请求 Meta 擦除。";
                return false;
            }
            _singleEraseInProgress.Add(anchorUuid);
            if (IsEraseMode)
                SetEraseModeState(false, "正在删除锚点…");
            _ = ErasePhysicalAnchorAsync(anchorUuid);
            return true;
        }

        public bool SetEraseMode(bool active, out string error)
        {
            error = string.Empty;
            if (IsEraseMode == active) return true;
            if (!active)
            {
                SetEraseModeState(false, "已取消删除模式。");
                return true;
            }

            if (!IsReady || IsBulkEraseInProgress || IsSingleAnchorEraseInProgress ||
                GuidedPlacementPhase != GuidedAnchorPlacementPhase.Idle || IsCreateMode ||
                (_lifecycle != null && _lifecycle.IsLoading))
            {
                error = "当前不能进入删除模式；请等待放置、保存、加载或删除完成。";
                return false;
            }

            SetEraseModeState(true, "删除模式：左射线指向要删除的锚点，按左扳机确认。");
            return true;
        }

        public bool BeginEraseHoveredAnchor(out string error)
        {
            error = string.Empty;
            if (!IsEraseMode)
            {
                error = "请先在主面板进入删除模式。";
                return false;
            }

            var hovered = _lifecycle != null ? _lifecycle.HoveredAnchor : null;
            if (hovered == null || hovered.Uuid == Guid.Empty || !hovered.IsSaved)
            {
                error = "左射线未指向可删除的已保存锚点。";
                EraseModeChanged?.Invoke(true, error);
                return false;
            }

            if (!BeginErasePhysicalAnchor(hovered.Uuid, out error))
            {
                EraseModeChanged?.Invoke(true, error);
                return false;
            }

            return true;
        }

        public bool BeginEraseAllSavedAnchors(out string error)
        {
            error = string.Empty;
            if (!IsReady)
            {
                error = "官方锚点管理尚未准备好。";
                return false;
            }
            if (IsBulkEraseInProgress)
            {
                error = "全部定位点擦除仍在进行。";
                return false;
            }
            if (GuidedPlacementPhase != GuidedAnchorPlacementPhase.Idle || IsCreateMode || IsEraseMode ||
                IsSingleAnchorEraseInProgress)
            {
                error = "请先结束当前定位点候选或删除模式，再清空空间数据。";
                return false;
            }

            IsBulkEraseInProgress = true;
            if (!PhysicalAnchorAdminBindingServices.Current.TryGetEraseCandidates(out var uuids, out var diagnosticTag))
            {
                IsBulkEraseInProgress = false;
                error = $"设备安装索引不可用：{diagnosticTag}。未请求 Meta 擦除。";
                return false;
            }
            for (var index = 0; index < uuids.Length; index++)
            {
                var prepared = PhysicalAnchorAdminBindingServices.Current.PrepareErase(uuids[index]);
                if (prepared.Succeeded) continue;
                IsBulkEraseInProgress = false;
                error = $"无法先解除活动绑定：{prepared.DiagnosticTag}。未请求 Meta 擦除。";
                return false;
            }
            _ = EraseAllSavedAnchorsAsync(uuids);
            return true;
        }

        public bool TryPrepareEraseAnchor(Guid anchorUuid, out string reason)
        {
            reason = string.Empty;
            if (!IsReady)
            {
                reason = "官方锚点管理尚未准备好。";
                return false;
            }
            if (anchorUuid == Guid.Empty)
            {
                reason = "锚点 UUID 无效。";
                return false;
            }

            var result = PhysicalAnchorAdminBindingServices.Current.PrepareErase(anchorUuid);
            if (result.Succeeded) return true;
            reason = $"设备安装绑定未能安全解除：{result.DiagnosticTag}";
            return false;
        }

        async Task EraseAllSavedAnchorsAsync(Guid[] uuids)
        {
            var metaEraseCompleted = false;
            try
            {
                await Task.Yield();
                if (uuids.Length > 0)
                {
                    var result = await _lifecycle.EraseAnchorsAsync(uuids);
                    if (!result.Succeeded)
                    {
                        CompleteBulkErase(
                            false,
                            $"Meta 未能擦除 {uuids.Length} 个定位点：{result.PlatformStatus}。UUID 索引已保留，可重试。");
                        return;
                    }
                }

                metaEraseCompleted = true;
                var cleanupFailures = 0;
                for (var index = 0; index < uuids.Length; index++)
                    if (!PhysicalAnchorAdminBindingServices.Current.CompleteErase(uuids[index]).Succeeded)
                        cleanupFailures++;
                try
                {
                    _lifecycle.DestroyAllLoadedAnchors();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[BotanicalAdminBridge] Meta anchors were erased but loaded visuals could not all be removed: {exception.Message}",
                        this);
                }
                if (cleanupFailures > 0)
                {
                    CompleteBulkErase(
                        false,
                        $"已擦除 {uuids.Length} 个 Meta 定位点，但有 {cleanupFailures} 条待清理安装记录未能移除；请重试管理员清理。");
                    return;
                }
                CompleteBulkErase(true, $"已擦除 {uuids.Length} 个 Meta 定位点并清空设备安装索引。");
            }
            catch (Exception exception)
            {
                CompleteBulkErase(
                    false,
                    metaEraseCompleted
                        ? $"Meta 实体定位点已擦除，但 UUID 索引清理失败：{exception.Message} 请勿新建定位点，先重试清理。"
                        : $"擦除全部定位点失败：{exception.Message} UUID 索引已保留，可重试。");
            }
        }

        async Task ErasePhysicalAnchorAsync(Guid uuid)
        {
            var success = false;
            var message = string.Empty;
            try
            {
                var result = await _lifecycle.EraseAnchorsAsync(new[] { uuid });
                if (!result.Succeeded)
                {
                    message = $"锚点删除失败：{result.PlatformStatus}。设备记录保持为待清理，不会重新绑定，可重试。";
                    Debug.LogError($"[PhysicalAnchorAdmin] {message}", this);
                }
                else
                {
                    var cleanup = PhysicalAnchorAdminBindingServices.Current.CompleteErase(uuid);
                    if (!cleanup.Succeeded)
                    {
                        Debug.LogError(
                            $"[PhysicalAnchorAdmin] Meta erase succeeded but installation cleanup failed: {cleanup.DiagnosticTag}",
                            this);
                        message = "Meta 锚点已删除，但设备记录清理失败；请先重试管理员清理。";
                    }
                    else
                    {
                        success = true;
                        message = "锚点已删除。";
                    }
                    var loaded = _lifecycle.ActiveAnchors.FirstOrDefault(anchor => anchor != null && anchor.Uuid == uuid);
                    if (loaded != null)
                        loaded.Release();
                }
            }
            catch (Exception exception)
            {
                message = $"锚点删除失败：{exception.Message}。设备记录保持为待清理，不会重新绑定，可重试。";
                Debug.LogError($"[PhysicalAnchorAdmin] {message}", this);
            }
            finally
            {
                _singleEraseInProgress.Remove(uuid);
                SavedAnchorsChanged?.Invoke();
            }
            SingleAnchorEraseCompleted?.Invoke(success, message);
        }

        async Task CompleteRetunedAnchorReplacementAsync(
            Guid replacedUuid,
            IOfficialSpatialAnchorHandle replacedAnchor)
        {
            var message = "新位置已重新保存，旧锚点已清理。";
            try
            {
                var prepared = PhysicalAnchorAdminBindingServices.Current.PrepareErase(replacedUuid);
                if (!prepared.Succeeded)
                {
                    message = $"新位置已重新保存；旧锚点清理准备失败（{prepared.DiagnosticTag}），已保留为待清理记录。";
                }
                else
                {
                    var result = await _lifecycle.EraseAnchorsAsync(new[] { replacedUuid });
                    if (!result.Succeeded)
                    {
                        message = $"新位置已重新保存；旧 Meta 锚点清理失败（{result.PlatformStatus}），已保留为待清理记录且不会重新绑定，可在删除模式中重试。";
                    }
                    else
                    {
                        var cleanup = PhysicalAnchorAdminBindingServices.Current.CompleteErase(replacedUuid);
                        if (!cleanup.Succeeded)
                        {
                            message = $"新位置已重新保存且旧 Meta 锚点已擦除，但设备记录清理失败（{cleanup.DiagnosticTag}）。";
                        }
                        if (replacedAnchor != null)
                            replacedAnchor.Release();
                    }
                }
            }
            catch (Exception exception)
            {
                message = $"新位置已重新保存；旧锚点清理异常（{exception.Message}），新位置仍保持有效。";
                Debug.LogError($"[PhysicalAnchorAdmin] {message}", this);
            }
            finally
            {
                SetGuidedPhase(GuidedAnchorPlacementPhase.Idle, message);
                SetWorkspaceVisible(true);
                SavedAnchorsChanged?.Invoke();
            }
        }

        void CompleteBulkErase(bool success, string message)
        {
            IsBulkEraseInProgress = false;
            SavedAnchorsChanged?.Invoke();
            AllSavedAnchorsEraseCompleted?.Invoke(success, message ?? string.Empty);
        }

        void HandleAnchorsChanged()
        {
            RefreshAnchorLabels();
            RefreshLoadState(LoadState, true);
            SavedAnchorsChanged?.Invoke();
        }

        void HandleLoadStateChanged(OfficialAnchorLoadState state)
            => RefreshLoadState(state, true);

        void RefreshLoadState(OfficialAnchorLoadState state, bool publish)
        {
            var savedAnchors = GetSavedAnchors();
            var trackedCount = 0;
            for (var index = 0; index < savedAnchors.Count; index++)
                if (savedAnchors[index].IsTracked) trackedCount++;
            LoadState = state.WithCounts(savedAnchors.Count, trackedCount);
            if (publish) LoadStateChanged?.Invoke(LoadState);
        }

        void RefreshAnchorLabels()
        {
            if (_lifecycle == null) return;
            if (!PhysicalAnchorAdminBindingServices.Current.TryRead(out var installation, out _))
            {
                for (var index = 0; index < _lifecycle.ActiveAnchors.Count; index++)
                    _lifecycle.ActiveAnchors[index]?.SetAdminDisplayIdentity(
                        string.Empty,
                        "设备记录不可用");
                return;
            }

            var records = new Dictionary<Guid, PhysicalAnchorAdminRecordSnapshot>();
            for (var index = 0; index < installation.Records.Count; index++)
                records[installation.Records[index].Uuid] = installation.Records[index];
            for (var index = 0; index < _lifecycle.ActiveAnchors.Count; index++)
            {
                var anchor = _lifecycle.ActiveAnchors[index];
                if (anchor == null || anchor.Uuid == Guid.Empty) continue;
                if (!records.TryGetValue(anchor.Uuid, out var record))
                {
                    anchor.SetAdminDisplayIdentity(
                        string.Empty,
                        anchor.IsPendingIndex ? "待写入设备记录" : "未索引");
                    continue;
                }

                var status = record.Status switch
                {
                    PhysicalAnchorAdminRecordStatus.Assigned => "已分配",
                    PhysicalAnchorAdminRecordStatus.Unassigned => "未分配",
                    PhysicalAnchorAdminRecordStatus.PendingErase => "待清理",
                    _ => "待写入设备记录"
                };
                anchor.SetAdminDisplayIdentity(record.DisplayLabel, status);
            }
        }

        void HandleModeChanged(bool createMode)
        {
            if (createMode)
            {
                if (GuidedPlacementPhase == GuidedAnchorPlacementPhase.Idle)
                {
                    _lifecycle.SetCreateMode(false);
                    Debug.LogWarning(
                        "[PhysicalAnchorAdmin] Use the workspace placement action so every anchor follows the explicit save and numbering transaction.",
                        this);
                    return;
                }
                SetWorkspaceVisible(false);
                return;
            }

            if (GuidedPlacementPhase == GuidedAnchorPlacementPhase.PlacingCandidate && _guidedCandidate == null)
                SetGuidedPhase(GuidedAnchorPlacementPhase.Idle, "已退出候选放置。");
        }

        void HandleAnchorPlaced(IOfficialSpatialAnchorHandle anchor)
        {
            if (GuidedPlacementPhase != GuidedAnchorPlacementPhase.PreparingCandidate ||
                anchor == null)
                return;

            ReleaseCandidateSubscriptions();
            _guidedCandidate = anchor;
            _guidedCandidate.Ready += HandleCandidateReady;
            _guidedCandidate.SaveSucceeded += HandleCandidateSaveSucceeded;
            _guidedCandidate.SaveFailed += HandleCandidateSaveFailed;
            _guidedCandidate.IndexingFailed += HandleCandidateIndexingFailed;
        }

        void HandleCandidateReady(IOfficialSpatialAnchorHandle anchor)
        {
            if (anchor == null || anchor != _guidedCandidate) return;
            if (!anchor.IsCreated || anchor.Uuid == Guid.Empty)
            {
                var wasRetuning = _retuneOriginalUuid != Guid.Empty;
                anchor.DiscardIfUnsaved();
                ReleaseCandidateSubscriptions();
                _commitCandidateWhenReady = false;
                if (wasRetuning)
                {
                    SetGuidedPhase(
                        GuidedAnchorPlacementPhase.AdjustingSavedAnchor,
                        "替换锚点创建失败；原锚点保持有效，可继续微调或取消。" );
                    _lifecycle.ShowCandidateReviewPose(_guidedCandidatePose);
                    SetWorkspaceVisible(true);
                }
                else
                {
                    SetGuidedPhase(
                        GuidedAnchorPlacementPhase.PlacingCandidate,
                        "Meta 锚点创建失败；请重新瞄准现实表面后按 X。" );
                    _lifecycle.SetCreateMode(true);
                }
                return;
            }

            if (_commitCandidateWhenReady)
            {
                _commitCandidateWhenReady = false;
                if (!BeginGuidedCandidateSave(out var error))
                {
                    SetWorkspaceVisible(true);
                    Debug.LogError($"[PhysicalAnchorAdmin] {error}", this);
                }
                return;
            }

            SetGuidedPhase(
                GuidedAnchorPlacementPhase.ReviewingNewAnchor,
                "新锚点候选已创建但尚未保存。请选择“保存锚点”确认，或取消且不写入设备记录。" );
            SetWorkspaceVisible(true);
        }

        void HandleCandidateSaveSucceeded(IOfficialSpatialAnchorHandle anchor)
        {
            if (anchor == null || anchor != _guidedCandidate) return;
            _saveOutcomeWarningAt = float.PositiveInfinity;
            if (!CompleteAnchorRecord(out var error))
                Debug.LogError($"[PhysicalAnchorAdmin] {error}", this);
        }

        void HandleCandidateSaveFailed(IOfficialSpatialAnchorHandle anchor, string message)
        {
            if (anchor == null || anchor != _guidedCandidate) return;
            _saveOutcomeWarningAt = float.PositiveInfinity;
            if (_retuneOriginalUuid != Guid.Empty)
            {
                anchor.DiscardIfUnsaved();
                ReleaseCandidateSubscriptions();
                _lifecycle.ShowCandidateReviewPose(_guidedCandidatePose);
                SetGuidedPhase(
                    GuidedAnchorPlacementPhase.AdjustingSavedAnchor,
                    $"替换锚点保存失败，原锚点保持有效：{message} 可继续微调、重新保存或取消。" );
                SetWorkspaceVisible(true);
                return;
            }
            SetGuidedPhase(
                GuidedAnchorPlacementPhase.SaveFailed,
                $"锚点保存失败，当前 Anchor 仍未保存：{message} 可重试保存或取消。" );
            SetWorkspaceVisible(true);
        }

        void HandleCandidateIndexingFailed(IOfficialSpatialAnchorHandle anchor, string message)
        {
            if (anchor == null || anchor != _guidedCandidate) return;
            _saveOutcomeWarningAt = float.PositiveInfinity;
            SetGuidedPhase(
                GuidedAnchorPlacementPhase.PendingIndex,
                $"{message} 选择“重试设备记录”不会重复保存 Meta 锚点。");
            SetWorkspaceVisible(true);
        }

        void ReleaseCandidateSubscriptions()
        {
            _saveOutcomeWarningAt = float.PositiveInfinity;
            var candidate = _guidedCandidate;
            _guidedCandidate = null;
            if (candidate == null) return;
            candidate.Ready -= HandleCandidateReady;
            candidate.SaveSucceeded -= HandleCandidateSaveSucceeded;
            candidate.SaveFailed -= HandleCandidateSaveFailed;
            candidate.IndexingFailed -= HandleCandidateIndexingFailed;
        }

        void ClearGuidedTransaction()
        {
            _guidedPhysicalAugmentationPointId = string.Empty;
            _retuneOriginalAnchor = null;
            _retuneOriginalUuid = Guid.Empty;
            _retuneOriginalDisplayNumber = 0;
            _retuneOriginalCustomName = string.Empty;
            _guidedAugmentationPoseInAnchorSpace = Pose.identity;
            _guidedUniformScale = 1f;
            ClearGuidedCandidatePose();
        }

        void ClearGuidedCandidatePose()
        {
            _guidedCandidateInitialPose = default;
            _guidedCandidatePose = default;
            _hasGuidedCandidatePose = false;
            _commitCandidateWhenReady = false;
            _lifecycle?.HideCandidateReviewPose();
        }

        OfficialAnchorAdjustmentSnapshot CreateAnchorAdjustmentSnapshot()
        {
            var offset = _guidedCandidatePose.position - _guidedCandidateInitialPose.position;
            var right = Vector3.ProjectOnPlane(
                _guidedCandidateInitialPose.rotation * Vector3.right,
                Vector3.up);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            return new OfficialAnchorAdjustmentSnapshot(
                _guidedCandidatePose,
                Vector3.Dot(offset, right.normalized),
                offset.y);
        }

        void PublishAnchorAdjustment()
        {
            if (TryGetAnchorAdjustment(out var adjustment))
                AnchorAdjustmentChanged?.Invoke(adjustment);
        }

        bool TryReconcileConfiguredBindings(out string error)
        {
            error = string.Empty;
            if (_physicalAugmentationCatalog == null)
                return true;

            IReadOnlyList<PhysicalAugmentationDefinition> definitions;
            try
            {
                definitions = _physicalAugmentationCatalog.Definitions;
            }
            catch (Exception exception)
            {
                error = $"实物增效配置无效：{exception.Message}";
                return false;
            }
            var configuredBindings = new PhysicalAnchorAdminConfiguredBinding[definitions.Count];
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                configuredBindings[index] = new PhysicalAnchorAdminConfiguredBinding(
                    definition.PointId.Value,
                    definition.InstallationAnchorNumber);
            }
            var result = PhysicalAnchorAdminBindingServices.Current.ReconcileConfiguredBindings(configuredBindings);
            if (result.Succeeded) return true;
            error = result.DiagnosticTag;
            return false;
        }

        void SetGuidedPhase(GuidedAnchorPlacementPhase phase, string message)
        {
            GuidedPlacementPhase = phase;
            GuidedPlacementChanged?.Invoke(phase, message ?? string.Empty);
        }

        void SetEraseModeState(bool active, string message)
        {
            IsEraseMode = active;
            EraseModeChanged?.Invoke(active, message ?? string.Empty);
        }

        static bool IsFinite(Vector3 value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        static bool IsFinite(Quaternion value)
            => float.IsFinite(value.x) && float.IsFinite(value.y) &&
               float.IsFinite(value.z) && float.IsFinite(value.w);

    }
}
