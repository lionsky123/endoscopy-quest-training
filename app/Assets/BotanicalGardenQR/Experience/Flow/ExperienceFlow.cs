using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;

namespace BotanicalGardenQR.Experience.Flow
{
    public sealed class ExperienceFlow : IExperienceFlow
    {
        readonly ISceneFlowDescriptorSource _descriptors;
        readonly FeaturePageRegistry _pages;
        readonly int _mainThreadId;
        readonly Dictionary<long, Subscription> _subscriptions = new Dictionary<long, Subscription>();

        ExperienceFlowState _state;
        IFeaturePageLifecycle _activeLifecycle;
        IMainSurfaceLifecycle _mainSurfaceLifecycle;
        long _nextSubscriptionId;
        long _prepareGeneration;

        public ExperienceFlow(ISceneFlowDescriptorSource descriptors, FeaturePageRegistry pages, int mainThreadId)
        {
            _descriptors = descriptors ?? throw new ArgumentNullException(nameof(descriptors));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            if (mainThreadId <= 0)
                throw new ArgumentOutOfRangeException(nameof(mainThreadId));
            _mainThreadId = mainThreadId;
            _state = ClosedState(0);
        }

        public static ExperienceFlow CreateOnCurrentThread(
            ISceneFlowDescriptorSource descriptors,
            FeaturePageRegistry pages)
            => new ExperienceFlow(descriptors, pages, Thread.CurrentThread.ManagedThreadId);

        public void SetMainSurfaceLifecycle(IMainSurfaceLifecycle lifecycle)
        {
            RequireMainThread();
            _mainSurfaceLifecycle = lifecycle;
        }

        public FlowPrepareResult Prepare(SessionToken candidate, SceneId sceneId)
        {
            RequireMainThread();
            if (!candidate.IsValid || !sceneId.IsValid)
                return FlowPrepareResult.Failure(new UserFault("体验配置无效，暂时无法打开。"));
            if (!_descriptors.TryGet(sceneId, out var descriptor) || descriptor == null)
                return FlowPrepareResult.Failure(new UserFault("未找到该体验内容。"));

            var generation = ++_prepareGeneration;
            return FlowPrepareResult.Success(
                new PreparedFlowLease(this, generation, candidate, descriptor));
        }

        public FlowResult EnterFeature(SessionToken session, FeaturePageId page)
        {
            RequireMainThread();
            if (!IsCurrent(session))
                return FlowResult.Reject(FlowFailure.StaleSession);
            if (_state.Page.Kind != FlowPageKind.Main)
                return FlowResult.Reject(FlowFailure.InvalidTransition);
            if (!Contains(_state.AvailablePages, page) || !_pages.TryGet(page, out var lifecycle))
                return FlowResult.Reject(FlowFailure.PageUnavailable);

            _mainSurfaceLifecycle?.BeforeFeatureEnter(session);
            var prepared = lifecycle.Prepare(session, _state.SceneId);
            if (!prepared.Succeeded)
            {
                RestoreMainAfterFailedFeature(prepared.Fault);
                return prepared;
            }

            var activated = lifecycle.Activate(session);
            if (!activated.Succeeded)
            {
                var released = InvokeLifecycle(
                    () => lifecycle.Release(session),
                    "release after activation failure");
                RestoreMainAfterFailedFeature(released.Fault ?? activated.Fault);
                return released.Succeeded ? activated : released;
            }

            _activeLifecycle = lifecycle;
            Publish(CloneWithPage(FlowPage.ForFeature(page), null, null));
            return FlowResult.Success;
        }

        public FlowResult BackToMain(SessionToken session)
        {
            RequireMainThread();
            if (!IsCurrent(session))
                return FlowResult.Reject(FlowFailure.StaleSession);
            if (_state.Page.Kind != FlowPageKind.Feature || _activeLifecycle == null)
                return FlowResult.Reject(FlowFailure.InvalidTransition);

            var released = ReleaseActive(session);
            Publish(CloneWithPage(
                FlowPage.Main,
                null,
                released.Succeeded
                    ? null
                    : released.Fault ?? new UserFault("功能关闭时发生错误，已返回主界面。")));
            return released;
        }

        public FlowResult Close(SessionToken session)
        {
            RequireMainThread();
            if (!IsCurrent(session))
                return FlowResult.Reject(FlowFailure.StaleSession);
            if (_state.Page.Kind == FlowPageKind.Closed)
                return FlowResult.Reject(FlowFailure.InvalidTransition);

            var released = _activeLifecycle == null
                ? FlowResult.Success
                : ReleaseActive(session);

            Publish(ClosedState(_state.Version + 1));
            return released;
        }

        public IDisposable Observe(IFlowStateSink sink)
        {
            RequireMainThread();
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));

            var id = ++_nextSubscriptionId;
            var subscription = new Subscription(this, id, sink, _mainThreadId);
            _subscriptions.Add(id, subscription);
            try
            {
                subscription.Publish(_state);
                return subscription;
            }
            catch
            {
                subscription.Dispose();
                throw;
            }
        }

        internal void Commit(long generation, SessionToken session, SceneFlowDescriptor descriptor)
        {
            RequireMainThread();
            if (generation != _prepareGeneration)
                throw new InvalidOperationException("A superseded flow lease cannot be committed.");

            if (_activeLifecycle != null)
            {
                var released = ReleaseActive(_state.Session);
                if (!released.Succeeded)
                    Trace.TraceError(
                        $"ExperienceFlow continued a prepared commit after best-effort release failed: {released.Failure}.");
            }

            Publish(new ExperienceFlowState(
                session,
                _state.Version + 1,
                descriptor.SceneId,
                descriptor.Title,
                descriptor.Subtitle,
                descriptor.Summary,
                FlowPage.Main,
                descriptor.AvailablePages));
        }

        internal void RemoveSubscription(long id) => _subscriptions.Remove(id);

        FlowResult ReleaseActive(SessionToken session)
        {
            var lifecycle = _activeLifecycle;
            if (lifecycle == null)
                return FlowResult.Success;

            var deactivated = InvokeLifecycle(
                () => lifecycle.Deactivate(session),
                "deactivate");
            var released = InvokeLifecycle(
                () => lifecycle.Release(session),
                "release");

            // Release is an ownership boundary, not a retry loop. Once it has
            // been attempted, retaining this binding would preserve a session
            // that may already have irreversibly disposed its resources.
            _activeLifecycle = null;
            return deactivated.Succeeded ? released : deactivated;
        }

        static FlowResult InvokeLifecycle(Func<FlowResult> operation, string operationName)
        {
            try
            {
                return operation();
            }
            catch (Exception exception)
            {
                Trace.TraceError($"ExperienceFlow feature {operationName} threw: {exception}");
                return FlowResult.Reject(
                    FlowFailure.LifecycleFailed,
                    new UserFault("功能关闭时发生错误，已安全退出该页面。"));
            }
        }

        void Publish(ExperienceFlowState next)
        {
            _state = next ?? throw new ArgumentNullException(nameof(next));
            if (_subscriptions.Count == 0)
                return;

            var snapshot = new Subscription[_subscriptions.Count];
            _subscriptions.Values.CopyTo(snapshot, 0);
            foreach (var subscription in snapshot)
            {
                try
                {
                    subscription.Publish(next);
                }
                catch (Exception exception)
                {
                    Trace.TraceError(
                        $"ExperienceFlow state sink failed while publishing version {next.Version}: {exception}");
                }
            }
        }

        ExperienceFlowState CloneWithPage(FlowPage page, string message, UserFault fault)
            => new ExperienceFlowState(
                _state.Session,
                _state.Version + 1,
                _state.SceneId,
                _state.Title,
                _state.Subtitle,
                _state.Summary,
                page,
                _state.AvailablePages,
                message,
                fault);

        void RestoreMainAfterFailedFeature(UserFault fault)
            => Publish(CloneWithPage(
                FlowPage.Main,
                null,
                fault ?? new UserFault("功能暂时无法打开，请重试。")));

        bool IsCurrent(SessionToken session) => session.IsValid && session == _state.Session;

        void RequireMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("ExperienceFlow must be called on the configured Unity main thread.");
        }

        static bool Contains(IReadOnlyList<FeaturePageId> pages, FeaturePageId page)
        {
            for (var index = 0; index < pages.Count; index++)
                if (pages[index] == page)
                    return true;
            return false;
        }

        static ExperienceFlowState ClosedState(long version)
            => new ExperienceFlowState(
                default,
                version,
                default,
                string.Empty,
                string.Empty,
                string.Empty,
                FlowPage.Closed,
                Array.Empty<FeaturePageId>());

        sealed class Subscription : IDisposable
        {
            readonly ExperienceFlow _owner;
            readonly long _id;
            readonly IFlowStateSink _sink;
            readonly StateSubscription _subscription;

            public Subscription(ExperienceFlow owner, long id, IFlowStateSink sink, int mainThreadId)
            {
                _owner = owner;
                _id = id;
                _sink = sink;
                _subscription = new StateSubscription(Unsubscribe, mainThreadId);
            }

            public void Publish(ExperienceFlowState state)
                => _subscription.Publish(state.Version, () => _sink.OnStateChanged(state));

            public void Dispose() => _subscription.Dispose();
            void Unsubscribe() => _owner.RemoveSubscription(_id);
        }
    }
}
