using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Backend;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed class PhysicalAugmentationVisitorBinding :
        IFlowStateSink,
        IPhysicalAugmentationStateSink,
        ISpatialDataPermissionStateSink,
        IFeaturePageActionSource,
        IDisposable
    {
        const string ActionLabel = "现实演示";
        const string RetryStartLabel = "重试定位";
        const string RetryPlaybackLabel = "重试演示";
        const string FocusExitLabel = "返回面板";

        readonly IPhysicalAugmentationSceneAssociationSource _associations;
        readonly List<IFeaturePageActionStateSink> _actionObservers =
            new List<IFeaturePageActionStateSink>();
        IPhysicalAugmentationController _controller;
        IDisposable _flowSubscription;
        IDisposable _controllerSubscription;
        IDisposable _permissionSubscription;
        ExperienceFlowState _flowState;
        PhysicalAugmentationState _physicalState;
        FeaturePageActionState _actionState = FeaturePageActionState.Hidden;
        PhysicalAugmentationFailureCode _startFailureCode;
        PhysicalAugmentationFailureCode _invocationFailureCode;
        string _invocationFault = string.Empty;
        string _localizationRetryFault = string.Empty;
        string _batchStatus = string.Empty;
        bool _hasPhysicalState;
        bool _focusActive;
        bool _startAttempted;
        bool _controllerStarted;
        bool _localizationRetryAvailable;
        bool _disposed;
        SpatialDataPermissionState _permissionState = SpatialDataPermissionState.Unknown;

        public PhysicalAugmentationVisitorBinding(
            IExperienceFlow flow,
            IPhysicalAugmentationSceneAssociationSource associations,
            IPhysicalAugmentationDefinitionSource definitions,
            PhysicalAugmentationRuntimeHost runtimeHost,
            Func<bool> modalSuppressed,
            ISpatialDataPermissionGate permissionGate)
            : this(
                flow,
                associations,
                ConfigureController(definitions, runtimeHost, modalSuppressed),
                permissionGate)
        {
        }

        internal PhysicalAugmentationVisitorBinding(
            IExperienceFlow flow,
            IPhysicalAugmentationSceneAssociationSource associations,
            IPhysicalAugmentationController controller,
            ISpatialDataPermissionGate permissionGate = null)
        {
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            _associations = associations ?? throw new ArgumentNullException(nameof(associations));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            try
            {
                _flowSubscription = flow.Observe(this);
                _controllerSubscription = _controller.Observe(this);
                if (permissionGate == null)
                {
                    _permissionState = SpatialDataPermissionState.Granted;
                    TryStartController();
                }
                else
                {
                    _permissionSubscription = permissionGate.Observe(this) ??
                                              throw new InvalidOperationException(
                                                  "Physical Augmentation permission observation returned no lease.");
                }
                RefreshActionState();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void OnStateChanged(ExperienceFlowState state)
        {
            if (_disposed || state == null) return;
            var wasModelPage = IsModelPage(_flowState);
            var isModelPage = IsModelPage(state);
            var sameModelContext = wasModelPage && isModelPage &&
                                   _flowState.Session == state.Session &&
                                   _flowState.SceneId == state.SceneId;
            if (!sameModelContext && (_focusActive || HasActivePerformance()))
                StopRealityPerformances(isModelPage ? "stop_on_model_context_change" : "stop_on_model_exit");
            _focusActive = _focusActive &&
                           _flowState != null &&
                           _flowState.Session == state.Session &&
                           _flowState.SceneId == state.SceneId &&
                           isModelPage;
            _flowState = state;
            ClearInvocationFailure();
            _batchStatus = string.Empty;
            RefreshActionState();
        }

        public void OnPhysicalAugmentationStateChanged(PhysicalAugmentationState state)
        {
            if (_disposed) return;
            _physicalState = state;
            _hasPhysicalState = true;
            if (state.Phase != PhysicalAugmentationPhase.Failed)
            {
                _localizationRetryAvailable = false;
                _localizationRetryFault = string.Empty;
            }
            ClearInvocationFailure();
            RefreshActionState();
        }

        public void OnSpatialDataPermissionStateChanged(SpatialDataPermissionState state)
        {
            if (_disposed) return;
            _permissionState = state;
            if (state == SpatialDataPermissionState.Granted && !_startAttempted)
                TryStartController();
            RefreshActionState();
        }

        public IDisposable Observe(IFeaturePageActionStateSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (_disposed) throw new ObjectDisposedException(nameof(PhysicalAugmentationVisitorBinding));
            if (!_actionObservers.Contains(sink)) _actionObservers.Add(sink);
            TryNotify(sink, _actionState);
            return new ActionObservation(this, sink);
        }

        public void Invoke(SessionToken session)
        {
            if (_disposed || _controller == null || _flowState == null ||
                session != _flowState.Session ||
                _flowState.Page.Kind != FlowPageKind.Feature ||
                _flowState.Page.Feature != FeaturePageId.Model)
            {
                Debug.LogWarning("[PhysicalAugmentation] action rejected before point resolution: stale or inactive Model session.");
                return;
            }

            var pointIds = _associations.GetPhysicalAugmentationPoints(_flowState.SceneId);
            if (pointIds == null || pointIds.Count == 0)
                return;
            if (IsRetryableStartFailure(_startFailureCode))
            {
                TryStartController(true);
                RefreshActionState();
                return;
            }
            if (_permissionState != SpatialDataPermissionState.Granted || !_controllerStarted)
            {
                var current = BuildActionState();
                SetInvocationFailure(
                    PhysicalAugmentationFailureCode.None,
                    string.IsNullOrWhiteSpace(current.Status)
                        ? "现实演示暂不可用，请稍候重试。"
                        : current.Status);
                RefreshActionState();
                return;
            }
            if (_localizationRetryAvailable)
            {
                RetryLocalization();
                return;
            }
            if (!string.IsNullOrWhiteSpace(_invocationFault) &&
                !IsRetryableActivationFailure(_invocationFailureCode))
            {
                RefreshActionState();
                return;
            }
            if (HasActivePointOutside(pointIds))
            {
                var current = BuildActionState();
                SetInvocationFailure(
                    PhysicalAugmentationFailureCode.None,
                    string.IsNullOrWhiteSpace(current.Status)
                        ? "现实演示暂不可用，请稍候重试。"
                        : current.Status);
                Debug.LogWarning(
                    $"[PhysicalAugmentation] action blocked before activation: {_invocationFault}");
                RefreshActionState();
                return;
            }

            var candidates = new List<PhysicalAugmentationPointState>(pointIds.Count);
            for (var index = 0; index < pointIds.Count; index++)
                if (TryFindPoint(pointIds[index], out var point) &&
                    (point.CanActivate || IsRetryablePerformanceFailure(point)))
                    candidates.Add(point);
            if (candidates.Count == 0)
            {
                if (HasRetryableLocalizationFailure(pointIds))
                {
                    RetryLocalization();
                    return;
                }
                SetInvocationFailure(
                    PhysicalAugmentationFailureCode.None,
                    BuildUnavailableStatus(pointIds));
                RefreshActionState();
                return;
            }

            var started = 0;
            var lastFailure = PhysicalAugmentationFailureCode.None;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                PhysicalAugmentationResult result;
                try
                {
                    result = _controller.Activate(candidate.PointId, candidate.Generation);
                }
                catch (Exception)
                {
                    result = PhysicalAugmentationResult.Failure(
                        PhysicalAugmentationFailureCode.RuntimeFailed,
                        "physical_augmentation.activate_exception",
                        candidate.Generation);
                }
                LogResult("activate", result, candidate.PointId);
                if (result.Succeeded) started++;
                else
                {
                    lastFailure = result.FailureCode;
                }
            }
            if (started == 0)
            {
                SetInvocationFailure(
                    lastFailure,
                    lastFailure == PhysicalAugmentationFailureCode.None
                        ? "现实演示暂不可用，请稍候重试。"
                        : MessageForFailure(lastFailure));
                RefreshActionState();
                return;
            }

            ClearInvocationFailure();
            _batchStatus = started == pointIds.Count
                ? "现实演示已开始。"
                : $"现实演示已开始（{started}/{pointIds.Count}）。";
            _focusActive = true;
            RefreshActionState();
        }

        public void ExitFocus(SessionToken session)
        {
            if (_disposed || _flowState == null || session != _flowState.Session ||
                _flowState.Page.Kind != FlowPageKind.Feature ||
                _flowState.Page.Feature != FeaturePageId.Model || !_focusActive)
                return;

            StopRealityPerformances("stop_on_focus_exit");
            _focusActive = false;
            _batchStatus = string.Empty;
            RefreshActionState();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _flowSubscription?.Dispose();
            _flowSubscription = null;
            _controllerSubscription?.Dispose();
            _controllerSubscription = null;
            _permissionSubscription?.Dispose();
            _permissionSubscription = null;
            if (_controllerStarted) _controller?.Stop();
            _controller = null;
            _actionObservers.Clear();
            _flowState = null;
            _focusActive = false;
            _controllerStarted = false;
            _startAttempted = false;
            _startFailureCode = PhysicalAugmentationFailureCode.None;
            _invocationFailureCode = PhysicalAugmentationFailureCode.None;
            _localizationRetryAvailable = false;
            _localizationRetryFault = string.Empty;
            _actionState = FeaturePageActionState.Hidden;
        }

        static IPhysicalAugmentationController ConfigureController(
            IPhysicalAugmentationDefinitionSource definitions,
            PhysicalAugmentationRuntimeHost runtimeHost,
            Func<bool> modalSuppressed)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (runtimeHost == null) throw new ArgumentNullException(nameof(runtimeHost));
            if (modalSuppressed == null) throw new ArgumentNullException(nameof(modalSuppressed));
            return runtimeHost.Configure(definitions, () =>
            {
                try { return modalSuppressed(); }
                catch (Exception) { return true; }
            });
        }

        void RefreshActionState()
        {
            var next = BuildActionState();
            if (Same(_actionState, next)) return;
            _actionState = next;
            Debug.Log(
                $"[PhysicalAugmentation] action state visible={next.Visible} interactable={next.Interactable} " +
                $"focus={next.FocusActive} status='{next.Status}'");
            var observers = _actionObservers.ToArray();
            for (var index = 0; index < observers.Length; index++)
                TryNotify(observers[index], next);
        }

        FeaturePageActionState BuildActionState()
        {
            if (_flowState == null ||
                _flowState.Page.Kind != FlowPageKind.Feature ||
                _flowState.Page.Feature != FeaturePageId.Model)
                return FeaturePageActionState.Hidden;
            var pointIds = _associations.GetPhysicalAugmentationPoints(_flowState.SceneId);
            if (pointIds == null || pointIds.Count == 0) return FeaturePageActionState.Hidden;

            if (_focusActive)
                return new FeaturePageActionState(
                    true,
                    false,
                    ActionLabel,
                    string.IsNullOrWhiteSpace(_batchStatus) ? "现实演示进行中…" : _batchStatus,
                    true,
                    FocusExitLabel);

            if (_permissionState != SpatialDataPermissionState.Granted)
                return Unavailable(SpatialPermissionUnavailableMessage(_permissionState));
            if (_startFailureCode != PhysicalAugmentationFailureCode.None)
                return IsRetryableStartFailure(_startFailureCode)
                    ? Retryable(RetryStartLabel, MessageForStartFailure(_startFailureCode))
                    : Unavailable(MessageForStartFailure(_startFailureCode));
            if (!_controllerStarted)
                return Unavailable(_startAttempted
                    ? "现实演示暂不可用，请联系工作人员。"
                    : "正在准备现实演示…");
            if (_localizationRetryAvailable)
                return Retryable(
                    RetryStartLabel,
                    string.IsNullOrWhiteSpace(_localizationRetryFault)
                        ? "现实位置重试未成功，可以再次重试。"
                        : _localizationRetryFault);
            if (!string.IsNullOrWhiteSpace(_invocationFault))
                return IsRetryableActivationFailure(_invocationFailureCode)
                    ? Retryable(RetryPlaybackLabel, _invocationFault)
                    : Unavailable(_invocationFault);
            if (!_hasPhysicalState)
                return Unavailable("正在准备现实演示…");
            if (_physicalState.Phase == PhysicalAugmentationPhase.Failed)
                return Unavailable("现实演示启动失败，请重试。");
            if (HasActivePointOutside(pointIds))
                return Unavailable("另一段现实演示正在播放或已呈现。");

            var activatableCount = 0;
            var retryableFailureCount = 0;
            var retryableLocalizationCount = 0;
            var playingCount = 0;
            var presentedCount = 0;
            for (var index = 0; index < pointIds.Count; index++)
            {
                if (!TryFindPoint(pointIds[index], out var point)) continue;
                if (point.CanActivate)
                {
                    activatableCount++;
                    if (point.ActivityPhase == PhysicalAugmentationPointActivityPhase.Playing)
                        playingCount++;
                    else if (point.ActivityPhase == PhysicalAugmentationPointActivityPhase.Presented)
                        presentedCount++;
                }
                else if (IsRetryablePerformanceFailure(point))
                {
                    retryableFailureCount++;
                }
                else if (IsRetryableLocalizationFailure(point))
                {
                    retryableLocalizationCount++;
                }
            }
            if (activatableCount == 0 && retryableFailureCount > 0)
                return Retryable(
                    RetryPlaybackLabel,
                    retryableFailureCount == 1
                        ? "上次演示未完成，可以重新播放。"
                        : $"{retryableFailureCount} 个演示上次未完成，可以重新播放。");
            if (activatableCount == 0 && retryableLocalizationCount > 0)
                return Retryable(
                    RetryStartLabel,
                    retryableLocalizationCount == 1
                        ? "现实位置暂时不可用，可以重试定位。"
                        : $"{retryableLocalizationCount} 个现实位置暂时不可用，可以重试定位。");
            if (activatableCount > 0)
            {
                string status;
                if (playingCount > 0 && presentedCount > 0)
                    status = "现实演示进行中或已完成。";
                else if (playingCount > 0)
                    status = "现实演示进行中…";
                else if (presentedCount > 0)
                    status = "演示已完成，点击可重新播放。";
                else if (activatableCount == pointIds.Count)
                    status = pointIds.Count == 1
                        ? "位置已准备好，可以开始现实演示。"
                        : $"{pointIds.Count} 个现实位置已准备好。";
                else
                    status = $"{activatableCount}/{pointIds.Count} 个现实位置已准备好。";
                return new FeaturePageActionState(true, true, ActionLabel, status);
            }
            return Unavailable(BuildUnavailableStatus(pointIds));
        }

        string BuildUnavailableStatus(IReadOnlyList<PhysicalAugmentationPointId> pointIds)
        {
            for (var index = 0; index < pointIds.Count; index++)
            {
                if (!TryFindPoint(pointIds[index], out var point)) continue;
                switch (point.ActivityPhase)
                {
                    case PhysicalAugmentationPointActivityPhase.Suppressed:
                        return "请先完成当前提示，再开始现实演示。";
                    case PhysicalAugmentationPointActivityPhase.Failed:
                        return MessageForDiagnostic(point.DiagnosticTag);
                }
                switch (point.Phase)
                {
                    case PhysicalAugmentationLocalizationPhase.Loading:
                    case PhysicalAugmentationLocalizationPhase.Localizing:
                    case PhysicalAugmentationLocalizationPhase.Stabilizing:
                        return "正在准备现实演示…";
                    case PhysicalAugmentationLocalizationPhase.Lost:
                        return "现实位置暂时不可用，请稍候。";
                    case PhysicalAugmentationLocalizationPhase.Failed:
                        return "现实位置准备失败，请重试。";
                }
            }
            return "现实演示尚未配置，请联系工作人员。";
        }

        bool HasActivePointOutside(IReadOnlyList<PhysicalAugmentationPointId> pointIds)
        {
            if (!_hasPhysicalState) return false;
            for (var stateIndex = 0; stateIndex < _physicalState.Points.Count; stateIndex++)
            {
                var point = _physicalState.Points[stateIndex];
                if (!IsActivePerformance(point)) continue;
                var belongsToScene = false;
                for (var pointIndex = 0; pointIndex < pointIds.Count; pointIndex++)
                    if (point.PointId == pointIds[pointIndex])
                    {
                        belongsToScene = true;
                        break;
                    }
                if (!belongsToScene) return true;
            }
            return false;
        }

        bool HasActivePerformance()
        {
            if (!_hasPhysicalState) return false;
            for (var index = 0; index < _physicalState.Points.Count; index++)
                if (IsActivePerformance(_physicalState.Points[index]))
                    return true;
            return false;
        }

        static bool IsActivePerformance(PhysicalAugmentationPointState point)
            => point.ActivityPhase == PhysicalAugmentationPointActivityPhase.Playing ||
               point.ActivityPhase == PhysicalAugmentationPointActivityPhase.Presented;

        static bool IsModelPage(ExperienceFlowState state)
            => state != null &&
               state.Page.Kind == FlowPageKind.Feature &&
               state.Page.Feature == FeaturePageId.Model;

        void StopRealityPerformances(string operation)
        {
            if (_controller == null) return;
            try
            {
                var result = _controller.StopPerformances();
                LogResult(operation, result);
            }
            catch (Exception)
            {
                Debug.LogWarning($"[PhysicalAugmentation] {operation} threw while releasing reality performances.");
            }
        }

        bool HasRetryableLocalizationFailure(IReadOnlyList<PhysicalAugmentationPointId> pointIds)
        {
            for (var index = 0; index < pointIds.Count; index++)
                if (TryFindPoint(pointIds[index], out var point) && IsRetryableLocalizationFailure(point))
                    return true;
            return false;
        }

        bool TryFindPoint(
            PhysicalAugmentationPointId pointId,
            out PhysicalAugmentationPointState point)
        {
            if (!_hasPhysicalState)
            {
                point = default;
                return false;
            }
            for (var index = 0; index < _physicalState.Points.Count; index++)
            {
                var candidate = _physicalState.Points[index];
                if (candidate.PointId != pointId) continue;
                point = candidate;
                return true;
            }
            point = default;
            return false;
        }

        static FeaturePageActionState Unavailable(string status)
            => new FeaturePageActionState(true, false, ActionLabel, status);

        static FeaturePageActionState Retryable(string label, string status)
            => new FeaturePageActionState(true, true, label, status);

        void TryStartController(bool explicitRetry = false)
        {
            if (_disposed || _controllerStarted || _controller == null ||
                _permissionState != SpatialDataPermissionState.Granted ||
                (_startAttempted && !explicitRetry))
                return;
            _startAttempted = true;
            PhysicalAugmentationResult start;
            try
            {
                start = _controller.Start();
            }
            catch (Exception)
            {
                start = PhysicalAugmentationResult.Failure(
                    PhysicalAugmentationFailureCode.RuntimeFailed,
                    "physical_augmentation.start_exception",
                    _hasPhysicalState ? _physicalState.Generation : 0);
            }
            LogResult("start", start);
            _controllerStarted = start.Succeeded;
            if (start.Succeeded)
            {
                _startFailureCode = PhysicalAugmentationFailureCode.None;
                return;
            }
            _startFailureCode = start.FailureCode;
        }

        void RetryLocalization()
        {
            PhysicalAugmentationResult retry;
            try
            {
                retry = _controller.RetryLocalization();
            }
            catch (Exception)
            {
                retry = PhysicalAugmentationResult.Failure(
                    PhysicalAugmentationFailureCode.RuntimeFailed,
                    "physical_augmentation.localization_retry_exception",
                    _hasPhysicalState ? _physicalState.Generation : 0);
            }
            LogResult("retry_localization", retry);
            _localizationRetryAvailable = !retry.Succeeded &&
                                          IsRetryableLocalizationCommandFailure(retry.FailureCode);
            _localizationRetryFault = _localizationRetryAvailable
                ? "现实位置重试未成功，可以再次重试。"
                : string.Empty;
            _batchStatus = retry.Succeeded ? "正在重新准备现实演示…" : string.Empty;
            RefreshActionState();
        }

        void SetInvocationFailure(
            PhysicalAugmentationFailureCode code,
            string message)
        {
            _invocationFailureCode = code;
            _invocationFault = message ?? string.Empty;
        }

        void ClearInvocationFailure()
        {
            _invocationFailureCode = PhysicalAugmentationFailureCode.None;
            _invocationFault = string.Empty;
        }

        static string SpatialPermissionUnavailableMessage(SpatialDataPermissionState state)
        {
            switch (state)
            {
                case SpatialDataPermissionState.Requesting:
                case SpatialDataPermissionState.Unknown:
                    return "正在等待空间准备…";
                case SpatialDataPermissionState.Denied:
                case SpatialDataPermissionState.PermanentlyDenied:
                    return "空间权限未开启，现实演示暂不可用。";
                case SpatialDataPermissionState.Unsupported:
                    return "当前设备暂不支持现实演示。";
                default:
                    return "现实演示暂不可用。";
            }
        }

        static string MessageForDiagnostic(string diagnosticTag)
        {
            if (string.Equals(
                    diagnosticTag,
                    "physical_augmentation.environment_depth_unavailable",
                    StringComparison.Ordinal))
                return "设备空间数据暂不可用，无法安全播放现实演示。";
            if (string.Equals(
                    diagnosticTag,
                    "physical_augmentation.application_suppressed",
                    StringComparison.Ordinal))
                return "请先完成当前提示，再开始现实演示。";
            return "现实演示暂不可用，请联系工作人员。";
        }

        static string MessageForStartFailure(PhysicalAugmentationFailureCode code)
        {
            switch (code)
            {
                case PhysicalAugmentationFailureCode.CapabilityUnavailable:
                case PhysicalAugmentationFailureCode.RuntimeFailed:
                    return "现实演示启动失败，可以重试。";
                case PhysicalAugmentationFailureCode.InvalidDefinition:
                    return "现实演示配置无效，请联系工作人员。";
                default:
                    return "现实演示状态异常，请联系工作人员。";
            }
        }

        static string MessageForFailure(PhysicalAugmentationFailureCode code)
        {
            switch (code)
            {
                case PhysicalAugmentationFailureCode.PointNotStable:
                    return "现实位置还未准备好，请稍候。";
                case PhysicalAugmentationFailureCode.Suppressed:
                    return "请先完成当前提示，再开始现实演示。";
                case PhysicalAugmentationFailureCode.CapabilityUnavailable:
                    return "设备空间数据暂不可用，无法安全播放现实演示。";
                default:
                    return "现实演示启动失败，请重试。";
            }
        }

        static bool IsRetryableStartFailure(PhysicalAugmentationFailureCode code)
            => code == PhysicalAugmentationFailureCode.CapabilityUnavailable ||
               code == PhysicalAugmentationFailureCode.RuntimeFailed;

        static bool IsRetryableActivationFailure(PhysicalAugmentationFailureCode code)
            => code == PhysicalAugmentationFailureCode.PreparationFailed ||
               code == PhysicalAugmentationFailureCode.RuntimeFailed;

        static bool IsRetryablePerformanceFailure(PhysicalAugmentationPointState point)
        {
            if (point.Phase != PhysicalAugmentationLocalizationPhase.Stable ||
                !point.HasResolvedPose ||
                point.ActivityPhase != PhysicalAugmentationPointActivityPhase.Failed)
                return false;
            var tag = point.DiagnosticTag ?? string.Empty;
            return tag.IndexOf("depth", StringComparison.OrdinalIgnoreCase) < 0 &&
                   tag.IndexOf("capability", StringComparison.OrdinalIgnoreCase) < 0 &&
                   tag.IndexOf("suppress", StringComparison.OrdinalIgnoreCase) < 0 &&
                   tag.IndexOf("definition", StringComparison.OrdinalIgnoreCase) < 0 &&
                   tag.IndexOf("unconfigured", StringComparison.OrdinalIgnoreCase) < 0 &&
                   tag.IndexOf("point_unknown", StringComparison.OrdinalIgnoreCase) < 0;
        }

        static bool IsRetryableLocalizationFailure(PhysicalAugmentationPointState point)
        {
            if (point.Phase != PhysicalAugmentationLocalizationPhase.Failed) return false;
            switch (point.DiagnosticTag ?? string.Empty)
            {
                case "binding_store.read_unavailable":
                case "binding_store.read_failed":
                case "physical_locator.platform_exception":
                case "physical_locator.batch_failed":
                case "physical_locator.anchor_missing":
                case "physical_locator.meta_load_failed":
                case "physical_locator.meta_exception":
                    return true;
                default:
                    return false;
            }
        }

        static bool IsRetryableLocalizationCommandFailure(PhysicalAugmentationFailureCode code)
            => code == PhysicalAugmentationFailureCode.CapabilityUnavailable ||
               code == PhysicalAugmentationFailureCode.RuntimeFailed;

        static bool Same(FeaturePageActionState left, FeaturePageActionState right)
            => left.Visible == right.Visible &&
               left.Interactable == right.Interactable &&
               string.Equals(left.Label, right.Label, StringComparison.Ordinal) &&
               string.Equals(left.Status, right.Status, StringComparison.Ordinal) &&
               left.FocusActive == right.FocusActive &&
               string.Equals(left.FocusExitLabel, right.FocusExitLabel, StringComparison.Ordinal);

        static void TryNotify(IFeaturePageActionStateSink sink, FeaturePageActionState state)
        {
            try { sink.OnFeaturePageActionStateChanged(state); }
            catch (Exception) { }
        }

        static void LogResult(
            string operation,
            PhysicalAugmentationResult result,
            PhysicalAugmentationPointId pointId = default)
        {
            var point = pointId.IsValid ? $" point={pointId.Value}" : string.Empty;
            var message =
                $"[PhysicalAugmentation] {operation}{point} succeeded={result.Succeeded} " +
                $"generation={result.Generation} failure={result.FailureCode} diagnostic={result.DiagnosticTag}";
            if (result.Succeeded) Debug.Log(message);
            else Debug.LogWarning(message);
        }

        void RemoveObserver(IFeaturePageActionStateSink sink) => _actionObservers.Remove(sink);

        sealed class ActionObservation : IDisposable
        {
            PhysicalAugmentationVisitorBinding _owner;
            IFeaturePageActionStateSink _sink;

            public ActionObservation(
                PhysicalAugmentationVisitorBinding owner,
                IFeaturePageActionStateSink sink)
            {
                _owner = owner;
                _sink = sink;
            }

            public void Dispose()
            {
                var owner = _owner;
                var sink = _sink;
                _owner = null;
                _sink = null;
                if (owner != null && sink != null) owner.RemoveObserver(sink);
            }
        }
    }
}
