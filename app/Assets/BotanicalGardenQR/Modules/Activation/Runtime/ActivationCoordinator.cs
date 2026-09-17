using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.SpatialHost.Contracts;

namespace BotanicalGardenQR.Activation.Runtime
{
    public sealed class ActivationCoordinator : IRecognitionObservationSink, IRecallController, IScanFeedbackSource,
        IConfirmedContentEntry, IFlowStateSink, IContentLifecycleSource, INewRecognitionActivationAvailabilitySink, IDisposable
    {
        readonly RouteResolver _routes;
        readonly ISpatialDisplayHost _host;
        readonly IExperienceFlow _flow;
        readonly RecognitionSourceRegistry _sources;
        readonly RecallPolicyEvaluator _recallPolicy;
        readonly ObservationGate _observations;
        readonly Action<DiagnosticEvent> _diagnostics;
        readonly JourneySessionId _journeySession;
        readonly int _mainThreadId;
        readonly Dictionary<long, RecallSubscription> _subscriptions = new Dictionary<long, RecallSubscription>();
        readonly Dictionary<long, ScanSubscription> _scanSubscriptions = new Dictionary<long, ScanSubscription>();
        readonly Dictionary<long, ContentSubscription> _contentSubscriptions = new Dictionary<long, ContentSubscription>();
        readonly IDisposable _flowSubscription;
        ResolvedActivation _lastResolved;
        UserFault _recallFault;
        Guid _currentObservationId;
        Guid _scanObservationId;
        SessionToken _activeSession;
        bool _replacementInProgress;
        bool _closingForSourceLoss;
        bool _flowIsClosed = true;
        bool _newRecognitionActivationAllowed = true;
        RecognitionObservation? _suppressedObservation;
        bool _disposed;
        long _recallVersion;
        long _scanVersion;
        long _nextSubscriptionId;
        long _nextScanSubscriptionId;
        long _nextContentSubscriptionId;
        ScanFeedbackState _scanFeedback = new ScanFeedbackState(0, 0f, false);

        public ActivationCoordinator(RouteResolver routes, ISpatialDisplayHost host, IExperienceFlow flow,
            RecognitionSourceRegistry sources, TimeSpan debounce,
            int mainThreadId, JourneySessionId journeySession, Action<DiagnosticEvent> diagnostics = null)
        {
            _routes = routes ?? throw new ArgumentNullException(nameof(routes));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _sources = sources ?? throw new ArgumentNullException(nameof(sources));
            if (!journeySession.IsValid) throw new ArgumentException("A valid journey session is required.", nameof(journeySession));
            _journeySession = journeySession;
            if (mainThreadId <= 0) throw new ArgumentOutOfRangeException(nameof(mainThreadId));
            _mainThreadId = mainThreadId;
            _diagnostics = diagnostics;
            _observations = new ObservationGate(debounce);
            _recallPolicy = new RecallPolicyEvaluator(sources);
            IDisposable flowSubscription = null;
            try
            {
                flowSubscription = flow.Observe(this)
                    ?? throw new InvalidOperationException("Experience flow returned no state subscription.");
            }
            catch
            {
                flowSubscription?.Dispose();
                _sources.Dispose();
                throw;
            }

            _flowSubscription = flowSubscription;
        }

        public void Start() { RequireAvailable(); _sources.Start(this); }

        public void SetNewRecognitionActivationAllowed(bool allowed)
        {
            RequireAvailable();
            if (_newRecognitionActivationAllowed == allowed) return;
            _newRecognitionActivationAllowed = allowed;
            if (!allowed || !_suppressedObservation.HasValue) return;

            var observation = _suppressedObservation.Value;
            _suppressedObservation = null;
            ClearScanFeedback(observation.ObservationId);
            var rearmed = _sources.TryRearm(observation);
            _observations.BeginFreshRound(observation);
            Diagnose(
                rearmed ? "ACT_SUPPRESSED_SCAN_REARMED" : "ACT_SUPPRESSED_SCAN_REARM_UNAVAILABLE",
                rearmed
                    ? "Recognition source accepted a fresh confirmation round after the visitor decision surface closed."
                    : "Recognition source does not support active rearm; a new source observation is required after the visitor decision surface closed.",
                "modal_rearm",
                observation,
                default);
        }

        public void Publish(RecognitionObservation observation)
        {
            RequireAvailable();
            if (!_observations.TryAccept(observation)) return;
            var active = _activeSession.IsValid && !_flowIsClosed;
            var sameActiveSource = active && IsSameActiveSource(observation);

            // A source can be represented by a new trackable/ObservationId
            // after a brief occlusion. Refresh the existing host lease for a
            // tracked revision without reopening the content page. Lost
            // revisions only affect the lease that currently owns the source;
            // a late lost event from an older observation must not close a
            // newer tracked observation.
            var ownsObservation = _activeSession.IsValid && _currentObservationId == observation.ObservationId;
            var refreshesSameSource = sameActiveSource && observation.TrackingState == TrackingState.Tracked;
            if (ownsObservation || refreshesSameSource)
            {
                _host.UpdateSourceTracking(_activeSession, observation.TrackingState, observation.ObservedAt);
                if (refreshesSameSource) _currentObservationId = observation.ObservationId;
            }

            if (observation.TrackingState == TrackingState.Lost)
            {
                if (_suppressedObservation.HasValue &&
                    _suppressedObservation.Value.ObservationId == observation.ObservationId)
                    _suppressedObservation = null;
                ClearScanFeedback(observation.ObservationId);
                Diagnose("ACT_SCAN_CANDIDATE_LOST", "Recognition candidate was lost after its grace period.", "candidate_lost", observation, _activeSession);
                return;
            }
            // The current source is not a new candidate. Its routine tracked
            // refreshes must not leave a 100% scan ring for Frontend to hide by
            // inspecting Flow or Recall state.
            if (sameActiveSource)
            {
                ClearScanFeedback(observation.ObservationId);
                return;
            }


            if (!_newRecognitionActivationAllowed)
            {
                _suppressedObservation = observation;
                ClearScanFeedback(observation.ObservationId);
                if (observation.IsConfirmation)
                    Diagnose(
                        "ACT_NEW_ACTIVATION_SUPPRESSED",
                        "A confirmed recognition candidate was ignored while a higher-priority visitor decision surface was visible.",
                        "modal_gate",
                        observation,
                        default);
                return;
            }

            if (observation.ConfirmationProgress <= 0f)
                Diagnose("ACT_SCAN_CANDIDATE_SELECTED", "Recognition candidate selected.", "candidate_selected", observation, default);
            if (observation.IsConfirmation)
                Diagnose("ACT_SCAN_CANDIDATE_CONFIRMED", "Recognition candidate confirmed.", "candidate_confirmed", observation, default);

            PublishScanFeedback(observation.ObservationId, observation.ConfirmationProgress, observation.IsConfirmation);
            if (!observation.IsConfirmation) return;
            if (!_routes.TryResolve(observation, out var resolved))
            {
                Diagnose("ACT_ROUTE_NOT_FOUND", "No enabled recognition route matched the observation.", "resolve", observation, default);
                return;
            }
            Activate(resolved, false);
        }

        public bool TryOpenConfirmedEntry(string entryValue)
        {
            RequireAvailable();
            if (!_flowIsClosed || _replacementInProgress || !_newRecognitionActivationAllowed ||
                string.IsNullOrWhiteSpace(entryValue)) return false;
            var confirmation = new RecognitionObservation(Guid.NewGuid(), 0, DateTimeOffset.UtcNow,
                RecognitionSourceKinds.Fieldbook, entryValue, TrackingState.Tracked);
            return _routes.TryResolve(confirmation, out var resolved) && Activate(resolved, false);
        }

        public RecallResult Recall()
        {
            RequireAvailable();
            if (!_flowIsClosed) return RecallResult.Reject(RecallFailure.AlreadyActive);
            var decision = _recallPolicy.Evaluate(_lastResolved);
            if (!decision.Allowed)
            {
                _recallFault = RecallFault(decision.Failure);
                PublishRecallState();
                return RecallResult.Reject(decision.Failure, _recallFault);
            }
            if (Activate(decision.Activation, true)) return RecallResult.Success;
            _recallFault = new UserFault("暂时无法重新打开体验，请稍后再试。");
            PublishRecallState();
            return RecallResult.Reject(RecallFailure.PreparationFailed, _recallFault);
        }

        public void Tick(DateTimeOffset now)
        {
            RequireAvailable();
            if (!_activeSession.IsValid) return;
            var result = _host.Tick(_activeSession, now);
            if (!result.Succeeded || result.Action != HostSourcePolicyAction.CloseRequested) return;
            var session = _activeSession;
            _closingForSourceLoss = true;
            try
            {
                _flow.Close(session);
            }
            finally
            {
                _closingForSourceLoss = false;
            }
        }

        public IDisposable Observe(IRecallStateSink sink)
        {
            RequireAvailable();
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            var id = ++_nextSubscriptionId;
            var subscription = new RecallSubscription(this, id, sink, _mainThreadId);
            _subscriptions.Add(id, subscription);
            try
            {
                subscription.Publish(CurrentRecallState());
                return subscription;
            }
            catch
            {
                subscription.Dispose();
                throw;
            }
        }

        public IDisposable ObserveScan(IScanFeedbackSink sink)
        {
            RequireAvailable();
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            var id = ++_nextScanSubscriptionId;
            var subscription = new ScanSubscription(this, id, sink, _mainThreadId);
            _scanSubscriptions.Add(id, subscription);
            try
            {
                subscription.Publish(CurrentScanFeedback());
                return subscription;
            }
            catch
            {
                subscription.Dispose();
                throw;
            }
        }

        public IDisposable ObserveContent(IContentLifecycleSink sink)
        {
            RequireAvailable();
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            var id = ++_nextContentSubscriptionId;
            var subscription = new ContentSubscription(this, id, sink);
            _contentSubscriptions.Add(id, subscription);
            return subscription;
        }

        IDisposable IContentLifecycleSource.Observe(IContentLifecycleSink sink)
            => ObserveContent(sink);

        public void OnStateChanged(ExperienceFlowState state)
        {
            RequireAvailable();
            var wasClosed = _flowIsClosed;
            _flowIsClosed = state.Page.Kind == FlowPageKind.Closed;
            if (_flowIsClosed && !wasClosed)
            {
                _recallFault = null;
                if (!_replacementInProgress)
                {
                    var closedForSourceLoss = _closingForSourceLoss;
                    var explicitCloseObservation = closedForSourceLoss ? null : _lastResolved?.Observation;
                    ClearScanFeedback();
                    var closingSession = _activeSession;
                    if (closingSession.IsValid)
                    {
                        PublishContentClosed(closingSession, _lastResolved, !closedForSourceLoss);
                        var closeResult = _host.Close(closingSession);
                        _activeSession = default;
                        _currentObservationId = Guid.Empty;
                        if (!closeResult.Succeeded)
                            Diagnose("ACT_HOST_CLOSE_FAILED", closeResult.Failure.ToString(), "close_host", _lastResolved?.Observation, closingSession);
                        else
                            Diagnose("ACT_CLOSED", "Activation closed.", "close", _lastResolved?.Observation, closingSession);
                    }
                    if (explicitCloseObservation is { } rearmObservation)
                    {
                        var rearmed = _sources.TryRearm(rearmObservation);
                        if (rearmed)
                        {
                            // Rearm can fall back to another physical trackable with
                            // the same payload. Let its authored 0% frame through even
                            // when it arrives inside the normal logical-source debounce.
                            _observations.BeginFreshRound(rearmObservation);
                        }
                        Diagnose(
                            rearmed ? "ACT_SCAN_REARMED" : "ACT_SCAN_REARM_UNAVAILABLE",
                            rearmed
                                ? "Recognition source accepted a fresh confirmation round after explicit close."
                                : "Recognition source could not accept a fresh confirmation round (unsupported or no eligible observation).",
                            "close_rearm",
                            rearmObservation,
                            default);
                    }
                }
            }
            if (wasClosed != _flowIsClosed && !_replacementInProgress) PublishRecallState();
        }

        bool Activate(ResolvedActivation resolved, bool isRecall)
        {
            _recallFault = null;
            var replacedExistingSession = _activeSession.IsValid && !_flowIsClosed;
            var candidate = SessionToken.CreateNew();
            var hostResult = _host.Prepare(candidate, resolved.DisplayProfile, resolved.Observation.SpatialEvidence);
            if (!hostResult.Succeeded)
            {
                Diagnose("ACT_HOST_PREPARE_FAILED", hostResult.Failure.ToString(), "prepare_host", resolved.Observation, candidate);
                return false;
            }
            using (var hostLease = hostResult.Lease)
            {
                var flowResult = _flow.Prepare(candidate, resolved.SceneId);
                if (!flowResult.Succeeded)
                {
                    Diagnose("ACT_FLOW_PREPARE_FAILED", flowResult.Fault?.Message ?? "Flow preparation failed.", "prepare_flow", resolved.Observation, candidate);
                    return false;
                }
                using (var flowLease = flowResult.Lease)
                {
                    var previousSession = _activeSession;
                    var previousResolved = _lastResolved;
                    var replacing = replacedExistingSession;
                    var previousFlowClosed = false;
                    var hostCommitted = false;
                    _replacementInProgress = replacing;
                    try
                    {
                        if (replacing)
                        {
                            var closed = _flow.Close(previousSession);
                            if (!closed.Succeeded)
                            {
                                Diagnose("ACT_REPLACEMENT_CLOSE_FAILED",
                                    closed.Fault?.Message ?? closed.Failure.ToString(),
                                    "close_previous_flow", resolved.Observation, previousSession);
                                if (_flowIsClosed && !TryRestorePreviousActivation(
                                        previousSession,
                                        previousResolved,
                                        false,
                                        resolved.Observation))
                                    ClearFailedReplacement(previousSession, resolved.Observation);
                                ClearScanFeedback(resolved.Observation.ObservationId);
                                PublishRecallState();
                                return false;
                            }

                            previousFlowClosed = true;
                        }

                        // Replacement has already released the previous feature page.
                        // Both prepared commits are now mutation-only invariants; publish
                        // the new Flow state only after the Host is at the candidate pose.
                        hostLease.Commit();
                        hostCommitted = true;
                        flowLease.Commit();
                    }
                    catch (Exception exception)
                    {
                        Diagnose(
                            "ACT_COMMIT_FAILED",
                            exception.Message,
                            "commit",
                            resolved.Observation,
                            candidate);

                        var candidateHostClosed = !hostCommitted;
                        if (hostCommitted)
                        {
                            var closeResult = _host.Close(candidate);
                            candidateHostClosed = closeResult.Succeeded;
                            if (!closeResult.Succeeded)
                                Diagnose(
                                    "ACT_COMMIT_ROLLBACK_HOST_CLOSE_FAILED",
                                    closeResult.Failure.ToString(),
                                    "rollback_host",
                                    resolved.Observation,
                                    candidate);
                        }

                        if (previousFlowClosed)
                        {
                            var restored = TryRestorePreviousActivation(
                                previousSession,
                                previousResolved,
                                hostCommitted,
                                resolved.Observation);
                            if (!restored)
                                ClearFailedReplacement(
                                    hostCommitted
                                        ? candidateHostClosed ? default : candidate
                                        : previousSession,
                                    resolved.Observation);
                        }

                        ClearScanFeedback(resolved.Observation.ObservationId);
                        PublishRecallState();
                        return false;
                    }
                    finally
                    {
                        _replacementInProgress = false;
                    }
                }
            }

            _activeSession = candidate;
            _currentObservationId = resolved.Observation.ObservationId;
            _lastResolved = resolved;
            _recallFault = null;
            ClearScanFeedback(resolved.Observation.ObservationId);
            PublishContentOpened(resolved, candidate, isRecall);
            PublishRecallState();
            Diagnose(isRecall ? "ACT_RECALL_COMMITTED" : replacedExistingSession ? "ACT_REPLACED" : "ACT_COMMITTED",
                isRecall ? "Recall committed." : replacedExistingSession ? "Activation replaced the active experience." : "Activation committed.",
                "commit", resolved.Observation, candidate);
            return true;
        }

        bool TryRestorePreviousActivation(
            SessionToken previousSession,
            ResolvedActivation previousResolved,
            bool restoreHost,
            RecognitionObservation failedObservation)
        {
            if (!previousSession.IsValid || previousResolved == null)
                return false;

            IPreparedHostLease restoreHostLease = null;
            var restoredHostCommitted = false;
            try
            {
                if (restoreHost)
                {
                    var hostResult = _host.Prepare(
                        previousSession,
                        previousResolved.DisplayProfile,
                        previousResolved.Observation.SpatialEvidence);
                    if (!hostResult.Succeeded)
                    {
                        Diagnose(
                            "ACT_REPLACEMENT_RESTORE_HOST_PREPARE_FAILED",
                            hostResult.Failure.ToString(),
                            "restore_previous_host",
                            failedObservation,
                            previousSession);
                        return false;
                    }

                    restoreHostLease = hostResult.Lease;
                }

                var flowResult = _flow.Prepare(previousSession, previousResolved.SceneId);
                if (!flowResult.Succeeded)
                {
                    Diagnose(
                        "ACT_REPLACEMENT_RESTORE_FLOW_PREPARE_FAILED",
                        flowResult.Fault?.Message ?? "Flow restoration preparation failed.",
                        "restore_previous_flow",
                        failedObservation,
                        previousSession);
                    return false;
                }

                using (var flowLease = flowResult.Lease)
                {
                    restoreHostLease?.Commit();
                    restoredHostCommitted = restoreHost;
                    flowLease.Commit();
                }

                _activeSession = previousSession;
                _currentObservationId = previousResolved.Observation.ObservationId;
                _lastResolved = previousResolved;
                _recallFault = null;
                _flowIsClosed = false;
                Diagnose(
                    "ACT_REPLACEMENT_RESTORED",
                    "Previous activation was restored after replacement failure.",
                    "restore_previous",
                    previousResolved.Observation,
                    previousSession);
                return true;
            }
            catch (Exception exception)
            {
                Diagnose(
                    "ACT_REPLACEMENT_RESTORE_FAILED",
                    exception.Message,
                    "restore_previous",
                    failedObservation,
                    previousSession);
                if (restoredHostCommitted)
                {
                    var closeResult = _host.Close(previousSession);
                    if (!closeResult.Succeeded)
                        Diagnose(
                            "ACT_REPLACEMENT_RESTORE_ROLLBACK_FAILED",
                            closeResult.Failure.ToString(),
                            "rollback_restored_host",
                            failedObservation,
                            previousSession);
                }
                return false;
            }
            finally
            {
                restoreHostLease?.Dispose();
            }
        }

        void ClearFailedReplacement(SessionToken hostSession, RecognitionObservation failedObservation)
        {
            if (hostSession.IsValid)
            {
                var closeResult = _host.Close(hostSession);
                if (!closeResult.Succeeded)
                    Diagnose(
                        "ACT_REPLACEMENT_FAILED_HOST_CLOSE_FAILED",
                        closeResult.Failure.ToString(),
                        "close_failed_previous_host",
                        failedObservation,
                        hostSession);
            }

            _activeSession = default;
            _currentObservationId = Guid.Empty;
            _flowIsClosed = true;
        }

        bool IsSameActiveSource(RecognitionObservation observation)
            => _lastResolved != null && IsSameSource(_lastResolved.Observation, observation);

        static bool IsSameSource(RecognitionObservation left, RecognitionObservation right)
            => left.SourceKind == right.SourceKind &&
               string.Equals(left.SourceValue, right.SourceValue, StringComparison.Ordinal);

        public void Dispose()
        {
            if (_disposed) return;
            RequireMainThread();
            _disposed = true;
            _sources.Dispose();
            _flowSubscription.Dispose();
            var subscriptions = new RecallSubscription[_subscriptions.Count];
            _subscriptions.Values.CopyTo(subscriptions, 0);
            foreach (var subscription in subscriptions) subscription.Dispose();
            var scanSubscriptions = new ScanSubscription[_scanSubscriptions.Count];
            _scanSubscriptions.Values.CopyTo(scanSubscriptions, 0);
            foreach (var subscription in scanSubscriptions) subscription.Dispose();
            var contentSubscriptions = new ContentSubscription[_contentSubscriptions.Count];
            _contentSubscriptions.Values.CopyTo(contentSubscriptions, 0);
            foreach (var subscription in contentSubscriptions) subscription.Dispose();
        }

        RecallState CurrentRecallState()
        {
            var decision = _flowIsClosed ? _recallPolicy.Evaluate(_lastResolved) : RecallDecision.Reject(RecallFailure.AlreadyActive);
            return new RecallState(
                _recallVersion,
                _flowIsClosed,
                _flowIsClosed && decision.Allowed,
                _recallFault,
                _lastResolved != null);
        }
        void PublishRecallState()
        {
            _recallVersion++;
            var state = CurrentRecallState();
            var snapshot = new RecallSubscription[_subscriptions.Count];
            _subscriptions.Values.CopyTo(snapshot, 0);
            foreach (var subscription in snapshot)
            {
                try
                {
                    subscription.Publish(state);
                }
                catch (Exception exception)
                {
                    Trace.TraceError(
                        $"Activation recall sink failed while publishing version {state.Version}: {exception}");
                }
            }
        }
        ScanFeedbackState CurrentScanFeedback() => _scanFeedback;
        void PublishScanFeedback(Guid observationId, float progress, bool isConfirmation)
        {
            // The QR adapter emits normal tracked refreshes after its one-shot
            // confirmation. Keep the success visual until the candidate commits
            // or clears rather than instantly turning it back into a plain ring.
            if (_scanObservationId == observationId && _scanFeedback.IsConfirmation &&
                !isConfirmation && progress >= 1f)
                return;

            _scanObservationId = observationId;
            var state = new ScanFeedbackState(++_scanVersion, progress, isConfirmation);
            _scanFeedback = state;
            var snapshot = new ScanSubscription[_scanSubscriptions.Count];
            _scanSubscriptions.Values.CopyTo(snapshot, 0);
            foreach (var subscription in snapshot)
            {
                try
                {
                    subscription.Publish(state);
                }
                catch (Exception exception)
                {
                    Trace.TraceError(
                        $"Activation scan sink failed while publishing version {state.Version}: {exception}");
                }
            }
        }
        void ClearScanFeedback(Guid observationId = default)
        {
            if (observationId != Guid.Empty && _scanObservationId != observationId) return;
            if (_scanObservationId == Guid.Empty && _scanFeedback.Progress <= 0f && !_scanFeedback.IsConfirmation) return;
            _scanObservationId = Guid.Empty;
            var state = new ScanFeedbackState(++_scanVersion, 0f, false);
            _scanFeedback = state;
            var snapshot = new ScanSubscription[_scanSubscriptions.Count];
            _scanSubscriptions.Values.CopyTo(snapshot, 0);
            foreach (var subscription in snapshot)
            {
                try
                {
                    subscription.Publish(state);
                }
                catch (Exception exception)
                {
                    Trace.TraceError(
                        $"Activation scan sink failed while clearing version {state.Version}: {exception}");
                }
            }
        }
        void Diagnose(string code, string message, string stage, RecognitionObservation? observation, SessionToken session)
            => _diagnostics?.Invoke(new DiagnosticEvent(code, message, "Activation", stage, DateTimeOffset.UtcNow,
                sessionToken: session, sourceKey: observation.HasValue ? observation.Value.SourceKind.ToString() : null));
        static UserFault RecallFault(RecallFailure failure)
        {
            switch (failure)
            {
                case RecallFailure.EvidenceRequired: return new UserFault("空间位置尚未恢复，请重新对准标记或稍后再试。");
                case RecallFailure.Unavailable: return new UserFault("当前没有可重新打开的体验。");
                default: return new UserFault("暂时无法重新打开体验，请稍后再试。");
            }
        }
        void RemoveSubscription(long id) => _subscriptions.Remove(id);
        void RemoveScanSubscription(long id) => _scanSubscriptions.Remove(id);
        void RemoveContentSubscription(long id) => _contentSubscriptions.Remove(id);
        void PublishContentOpened(ResolvedActivation resolved, SessionToken session, bool isRecall)
        {
            ContentOpenedFact fact;
            try
            {
                fact = new ContentOpenedFact(
                    session,
                    _journeySession,
                    resolved.SceneId,
                    resolved.Observation.SourceKind,
                    resolved.EntryRouteId,
                    isRecall);
            }
            catch (Exception exception)
            {
                Diagnose("ACT_CONTENT_FACT_FAILED", exception.Message, "content_fact", resolved.Observation, session);
                return;
            }

            var snapshot = new ContentSubscription[_contentSubscriptions.Count];
            _contentSubscriptions.Values.CopyTo(snapshot, 0);
            foreach (var subscription in snapshot)
            {
                try
                {
                    subscription.Publish(fact);
                }
                catch (Exception exception)
                {
                    Trace.TraceError($"Activation content sink failed while publishing '{resolved.EntryRouteId}': {exception}");
                }
            }
        }

        void PublishContentClosed(SessionToken session, ResolvedActivation resolved, bool isExplicit)
        {
            if (resolved == null) return;
            ContentClosedFact fact;
            try
            {
                fact = new ContentClosedFact(session, _journeySession, resolved.SceneId, isExplicit);
            }
            catch (Exception exception)
            {
                Diagnose("ACT_CONTENT_CLOSE_FACT_FAILED", exception.Message, "content_close_fact", resolved.Observation, session);
                return;
            }

            var snapshot = new ContentSubscription[_contentSubscriptions.Count];
            _contentSubscriptions.Values.CopyTo(snapshot, 0);
            foreach (var subscription in snapshot)
            {
                try
                {
                    subscription.PublishClosed(fact);
                }
                catch (Exception exception)
                {
                    Trace.TraceError($"Activation content sink failed while publishing close for '{resolved.EntryRouteId}': {exception}");
                }
            }
        }
        void RequireAvailable() { RequireMainThread(); if (_disposed) throw new ObjectDisposedException(nameof(ActivationCoordinator)); }
        void RequireMainThread() { if (Thread.CurrentThread.ManagedThreadId != _mainThreadId) throw new InvalidOperationException("Activation must run on the configured Unity main thread."); }

        sealed class RecallSubscription : IDisposable
        {
            readonly ActivationCoordinator _owner; readonly long _id; readonly IRecallStateSink _sink; readonly StateSubscription _state;
            public RecallSubscription(ActivationCoordinator owner, long id, IRecallStateSink sink, int threadId)
            { _owner = owner; _id = id; _sink = sink; _state = new StateSubscription(Unsubscribe, threadId); }
            public void Publish(RecallState state) => _state.Publish(state.Version, () => _sink.OnStateChanged(state));
            public void Dispose() => _state.Dispose();
            void Unsubscribe() => _owner.RemoveSubscription(_id);
        }

        sealed class ScanSubscription : IDisposable
        {
            readonly ActivationCoordinator _owner;
            readonly long _id;
            readonly IScanFeedbackSink _sink;
            readonly StateSubscription _state;

            public ScanSubscription(ActivationCoordinator owner, long id, IScanFeedbackSink sink, int threadId)
            {
                _owner = owner;
                _id = id;
                _sink = sink;
                _state = new StateSubscription(Unsubscribe, threadId);
            }

            public void Publish(ScanFeedbackState state)
                => _state.Publish(state.Version, () => _sink.OnScanFeedbackChanged(state));
            public void Dispose() => _state.Dispose();
            void Unsubscribe() => _owner.RemoveScanSubscription(_id);
        }

        sealed class ContentSubscription : IDisposable
        {
            readonly ActivationCoordinator _owner;
            readonly long _id;
            readonly IContentLifecycleSink _sink;
            bool _disposed;

            public ContentSubscription(ActivationCoordinator owner, long id, IContentLifecycleSink sink)
            {
                _owner = owner;
                _id = id;
                _sink = sink;
            }

            public void Publish(ContentOpenedFact fact)
            {
                if (_disposed) return;
                _sink.OnContentOpened(fact);
            }

            public void PublishClosed(ContentClosedFact fact)
            {
                if (_disposed) return;
                _sink.OnContentClosed(fact);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner.RemoveContentSubscription(_id);
            }
        }

    }
}
