using System;
using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    public sealed class GlobalFrontendShell : MonoBehaviour, IGlobalFrontendShell, IFlowStateSink,
        IFeaturePageActionStateSink, IClinicalLessonCompletion
    {
        [SerializeField] ShellSlotReferences _slots = new ShellSlotReferences();
        [SerializeField, Min(0.01f)] float _pointerMaximumDistance = 20f;

        IExperienceFlow _flow;
        IDisposable _flowSubscription;
        IFeaturePageActionSource _featurePageActionSource;
        IDisposable _featurePageActionSubscription;
        FeaturePageId _featurePageActionFeature;
        PointerRouter _pointerRouter;
        ShellTransitionPlayer _transition;
        ExperienceFlowState _state;
        PresentationSpec _presentation;
        FrontendFeatureSurfaceLease _featureLease;
        FrontendNarrationDockLease _narrationDockLease;
        SessionToken _immersiveSession;
        FeaturePageId _immersiveFeature;
        bool _configured;
        bool _applicationSurfaceSuppressed;
        bool _hasRenderedPage;
        int _lastPageKey;
        string _startupHint;
        string _statusOverride = string.Empty;
        FeaturePageActionState _featurePageActionState = FeaturePageActionState.Hidden;
        ClinicalLessonPanel _clinicalPanel;
        bool _clinicalActive;
        public event Action ClinicalCompletionRequested;

        public Transform FrontendRoot => _slots.ShellRoot;

        public FlowResult CompleteClinicalLesson(SessionToken session)
        {
            if (_state == null || _state.Session != session || _state.SceneId.Value != "giant_saguaro")
                return FlowResult.Reject(FlowFailure.StaleSession);
            var result = Dispatch(FlowIntent.Close(session));
            if (result.Succeeded) ClinicalCompletionRequested?.Invoke();
            return result;
        }

        public void PresentClinicalLesson(ExperienceFlowState state, PublishedSceneResolver definitions,
            IFrontendGazeSurfaceRegistry surfaces)
        {
            if (state.SceneId.Value != "giant_saguaro") return;
            if (_clinicalPanel == null)
                _clinicalPanel = new ClinicalLessonPanel(_slots.ShellRoot.parent, _slots.Title.font, surfaces,
                    () => { if (_state != null) Dispatch(FlowIntent.EnterFeature(_state.Session, FeaturePageId.Panorama)); });
            definitions.TryGetLearningImages(state.SceneId, out var images);
            _clinicalPanel.Present(state, images, definitions.IsPanoramaReadyForTeaching(state.SceneId));
        }

        public void SetApplicationSurfaceSuppressed(bool suppressed)
        {
            if (_applicationSurfaceSuppressed == suppressed) return;
            _applicationSurfaceSuppressed = suppressed;
            if (_configured) RenderPage();
            else if (_slots?.ShellRoot != null) _slots.ShellRoot.gameObject.SetActive(!suppressed);
        }

        public void Configure(IExperienceFlow flow, PresentationSpec presentation, string startupHint)
        {
            if (_configured) throw new InvalidOperationException("GlobalFrontendShell is already configured.");
            ValidateConfiguration();
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _startupHint = startupHint ?? string.Empty;
            PresentationSpecApplier.Apply(_slots, _presentation);
            _pointerRouter = new PointerRouter(_slots.FlowTargets, _pointerMaximumDistance);
            _transition = new ShellTransitionPlayer(this, _slots.ShellCanvasGroup, _slots.ShellRoot);
            foreach (var target in _slots.FlowTargets)
                if (target != null) target.Bind(HandleTargetSelected);
            _configured = true;
            _flowSubscription = _flow.Observe(this);
        }

        internal void ValidateConfiguration() => _slots.Validate();

        public void ApplyPresentation(PresentationSpec presentation)
        {
            if (!_configured) throw new InvalidOperationException("GlobalFrontendShell is not configured.");
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            PresentationSpecApplier.Apply(_slots, _presentation);
            RenderPage();
        }

        public FlowResult Dispatch(FlowIntent intent)
        {
            if (!_configured || _flow == null)
                return FlowResult.Reject(FlowFailure.PreparationFailed, new UserFault("界面尚未准备好。"));
            switch (intent.Kind)
            {
                case FlowIntentKind.EnterFeature: return _flow.EnterFeature(intent.Session, intent.Feature);
                case FlowIntentKind.BackToMain: return _flow.BackToMain(intent.Session);
                case FlowIntentKind.Close: return _flow.Close(intent.Session);
                default: return FlowResult.Reject(FlowFailure.InvalidTransition);
            }
        }

        public void ShowStatus(SessionToken session, UserFault fault)
        {
            if (!_configured || _state == null || session != _state.Session ||
                _state.Page.Kind != FlowPageKind.Main || fault == null)
                return;

            _statusOverride = fault.Message;
            RenderPage();
        }

        public IDisposable BindFeaturePageAction(
            FeaturePageId feature,
            IFeaturePageActionSource source)
        {
            if (!Enum.IsDefined(typeof(FeaturePageId), feature))
                throw new ArgumentOutOfRangeException(nameof(feature));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (_featurePageActionSource != null)
                throw new InvalidOperationException("GlobalFrontendShell already has a feature-page action source.");
            if (!_slots.FlowTargets.Any(target =>
                    target != null &&
                    target.Action == ShellFlowAction.FeaturePageAction &&
                    target.Feature == feature))
                throw new InvalidOperationException(
                    $"GlobalFrontendShell has no authored feature-page action target for '{feature}'.");
            if (!_slots.FlowTargets.Any(target =>
                    target != null &&
                    target.Action == ShellFlowAction.FeaturePageActionExitFocus &&
                    target.Feature == feature))
                throw new InvalidOperationException(
                    $"GlobalFrontendShell has no authored feature focus-exit target for '{feature}'.");
            _featurePageActionFeature = feature;
            _featurePageActionSource = source;
            try
            {
                _featurePageActionSubscription = source.Observe(this) ??
                                                 throw new InvalidOperationException("The feature-page action source returned no observation lease.");
                return new FeaturePageActionLease(this, source);
            }
            catch
            {
                _featurePageActionFeature = default;
                _featurePageActionSource = null;
                _featurePageActionSubscription = null;
                throw;
            }
        }

        public void OnFeaturePageActionStateChanged(FeaturePageActionState state)
        {
            _featurePageActionState = state;
            if (_configured) RenderPage();
        }

        public void ReceivePointerSignal(PointerSignal signal)
        {
            if (!_configured || _state == null || !_state.Session.IsValid) return;
            if (_pointerRouter.TryRoute(signal, out var target)) HandleTargetSelected(target);
        }

        public FrontendSurfaceResult AcquireFeatureSurface(SessionToken session, FeaturePageId feature)
        {
            if (!_configured || _state == null)
                return FrontendSurfaceResult.Reject(FrontendSurfaceFailure.ShellUnavailable);
            if (session != _state.Session)
                return FrontendSurfaceResult.Reject(FrontendSurfaceFailure.StaleSession);
            if (feature == FeaturePageId.Panorama)
                return FrontendSurfaceResult.Reject(FrontendSurfaceFailure.PageUnavailable);
            if (!IsAvailable(feature))
                return FrontendSurfaceResult.Reject(FrontendSurfaceFailure.PageUnavailable);
            if (_state.Page.Kind != FlowPageKind.Main &&
                !(_state.Page.Kind == FlowPageKind.Feature && _state.Page.Feature == feature))
                return FrontendSurfaceResult.Reject(FrontendSurfaceFailure.PageUnavailable);
            if (_featureLease != null && !_featureLease.IsDisposed)
                return FrontendSurfaceResult.Reject(FrontendSurfaceFailure.AlreadyLeased);
            if (!_slots.TryGetFeatureSurface(feature, out var stageRoot, out var stageSize, out var controlRoot, out var controlSize) ||
                stageRoot == null || controlRoot == null)
                return FrontendSurfaceResult.Reject(FrontendSurfaceFailure.PageUnavailable);

            FrontendFeatureSurfaceLease lease = null;
            lease = new FrontendFeatureSurfaceLease(
                session,
                feature,
                stageRoot,
                stageSize,
                controlRoot,
                controlSize,
                () => ReleaseFeatureSurface(lease));
            _featureLease = lease;
            RenderPage();
            return FrontendSurfaceResult.Success(lease);
        }

        public FrontendNarrationDockLease AcquireNarrationDock(SessionToken session)
        {
            if (!_configured || _state == null || session != _state.Session ||
                _state.Page.Kind != FlowPageKind.Main || _immersiveSession.IsValid)
                return null;
            if (_narrationDockLease != null && !_narrationDockLease.IsDisposed)
                return _narrationDockLease;

            FrontendNarrationDockLease lease = null;
            lease = new FrontendNarrationDockLease(
                session,
                _slots.NarrationDockSlot,
                _slots.NarrationDockSize,
                () => ReleaseNarrationDock(lease));
            _narrationDockLease = lease;
            RenderPage();
            return lease;
        }

        public FlowResult SetImmersiveFeature(SessionToken session, FeaturePageId feature, bool active)
        {
            if (!_configured || _state == null)
                return FlowResult.Reject(FlowFailure.PreparationFailed, new UserFault("界面尚未准备好。"));
            if (session != _state.Session)
                return FlowResult.Reject(FlowFailure.StaleSession);
            if (feature != FeaturePageId.Panorama)
                return FlowResult.Reject(FlowFailure.PageUnavailable);

            if (active)
            {
                _immersiveSession = session;
                _immersiveFeature = feature;
            }
            else if (_immersiveSession == session)
            {
                _immersiveSession = default;
                _immersiveFeature = default;
            }
            RenderPage();
            return FlowResult.Success;
        }

        public void OnStateChanged(ExperienceFlowState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            _state = state;
            _clinicalActive = state.SceneId.Value == "giant_saguaro";
            _statusOverride = string.Empty;
            _slots.Title.text = state.Title;
            _slots.Subtitle.text = state.Subtitle;
            _slots.Status.text = state.Fault?.Message ?? state.Message;
            RenderPage();
        }

        public void Dispose()
        {
            _clinicalPanel?.Dispose();
            _clinicalPanel = null;
            ClinicalCompletionRequested = null;
            _flowSubscription?.Dispose();
            _flowSubscription = null;
            ReleaseFeaturePageAction(_featurePageActionSource);
            foreach (var target in _slots.FlowTargets)
                if (target != null) target.Unbind();
            _flow = null;
            _state = null;
            _featureLease?.Dispose();
            _featureLease = null;
            _narrationDockLease?.Dispose();
            _narrationDockLease = null;
            _immersiveSession = default;
            _hasRenderedPage = false;
            _lastPageKey = 0;
            _configured = false;
            _applicationSurfaceSuppressed = false;
            _statusOverride = string.Empty;
            _featurePageActionState = FeaturePageActionState.Hidden;
            _featurePageActionFeature = default;
        }

        void OnDestroy() => Dispose();

        void ReleaseFeatureSurface(FrontendFeatureSurfaceLease lease)
        {
            if (!ReferenceEquals(_featureLease, lease)) return;
            _featureLease = null;
            RenderPage();
        }

        void ReleaseNarrationDock(FrontendNarrationDockLease lease)
        {
            if (!ReferenceEquals(_narrationDockLease, lease)) return;
            _narrationDockLease = null;
            RenderPage();
        }

        void RenderPage()
        {
            if (_state == null) return;
            var closed = _state.Page.Kind == FlowPageKind.Closed;
            var immersive = _immersiveSession.IsValid && _immersiveSession == _state.Session &&
                            _immersiveFeature == FeaturePageId.Panorama;
            var main = _state.Page.Kind == FlowPageKind.Main && !immersive;
            var feature = _state.Page.Kind == FlowPageKind.Feature && !immersive;
            var featureFocus = feature &&
                               _state.Page.Feature == _featurePageActionFeature &&
                               _featurePageActionState.FocusActive;

            var pageKey = ((int)_state.Page.Kind << 1) | (immersive ? 1 : 0);
            var pageChanged = _hasRenderedPage && pageKey != _lastPageKey;
            _hasRenderedPage = true;
            _lastPageKey = pageKey;

            if (pageChanged)
            {
                _transition.Transition(
                    () => ApplyPageVisibility(closed, immersive, main, feature, featureFocus),
                    _presentation.Animation.TransitionSeconds);
                return;
            }

            ApplyPageVisibility(closed, immersive, main, feature, featureFocus);
            // The CanvasGroup belongs to the shared parent of both presentations.
            // Hide the legacy Viewport above, while keeping the clinical sibling visible.
            _transition.SetVisible(!_applicationSurfaceSuppressed, 0f);
        }

        void ApplyPageVisibility(
            bool closed,
            bool immersive,
            bool main,
            bool feature,
            bool featureFocus)
        {
            _slots.ShellRoot.gameObject.SetActive(
                !immersive && !featureFocus && !_applicationSurfaceSuppressed && !(main && _clinicalActive && _clinicalPanel != null));
            _clinicalPanel?.SetVisible(main && _clinicalActive && !_applicationSurfaceSuppressed);
            ApplyFeatureModeTitles(closed);
            _slots.HeaderSlot.gameObject.SetActive(main);
            _slots.ActionSlot.gameObject.SetActive(main);
            _slots.NarrationDockSlot.gameObject.SetActive(main && _narrationDockLease != null && !_narrationDockLease.IsDisposed);
            _slots.PanoramaExitSlot.gameObject.SetActive(false);
            _slots.FeatureFocusExitSlot.gameObject.SetActive(false);
            ApplyStatus(closed, main, feature);
            _slots.StatusSlot.gameObject.SetActive(
                closed || main ||
                (!immersive &&
                 (!string.IsNullOrEmpty(_state.Message) || _state.Fault != null ||
                  HasVisibleFeaturePageActionStatus(feature))));
            _slots.CloseSlot.gameObject.SetActive(main);
            _slots.OverlaySlot.gameObject.SetActive(closed);
            UpdateTargets(main, feature, featureFocus);
        }

        void UpdateTargets(bool main, bool feature, bool featureFocus)
        {
            foreach (var target in _slots.FlowTargets)
            {
                if (target == null) continue;
                var isPanoramaExit = target.transform == _slots.PanoramaExitSlot;
                var visible = (target.Action == ShellFlowAction.Close && main) ||
                              (target.Action == ShellFlowAction.BackToMain && !isPanoramaExit && feature) ||
                              (target.Action == ShellFlowAction.EnterFeature && main && IsAvailable(target.Feature)) ||
                              (target.Action == ShellFlowAction.FeaturePageAction &&
                               feature &&
                               !featureFocus &&
                               target.Feature == _featurePageActionFeature &&
                               _state.Page.Feature == target.Feature &&
                               _featurePageActionState.Visible) ||
                              (target.Action == ShellFlowAction.FeaturePageActionExitFocus &&
                               featureFocus &&
                               !_applicationSurfaceSuppressed &&
                               target.Feature == _featurePageActionFeature &&
                               _state.Page.Feature == target.Feature);
                if (target.Action == ShellFlowAction.FeaturePageAction)
                    target.ApplyFeaturePageAction(
                        _featurePageActionState.Interactable,
                        _featurePageActionState.Label);
                else if (target.Action == ShellFlowAction.FeaturePageActionExitFocus)
                    target.ApplyFeaturePageAction(
                        true,
                        _featurePageActionState.FocusExitLabel);
                target.gameObject.SetActive(visible);
            }
        }

        void ApplyStatus(bool closed, bool main, bool feature)
        {
            if (closed)
            {
                _slots.Status.text = _startupHint;
                return;
            }
            if (main && !string.IsNullOrWhiteSpace(_statusOverride))
            {
                _slots.Status.text = _statusOverride;
                return;
            }
            if (HasVisibleFeaturePageActionStatus(feature))
            {
                _slots.Status.text = _featurePageActionState.Status;
                return;
            }
            _slots.Status.text = _state.Fault?.Message ?? _state.Message;
            if (main && string.IsNullOrWhiteSpace(_slots.Status.text))
                _slots.Status.text = "识别完成";
        }

        void ApplyFeatureModeTitles(bool closed)
        {
            var title = closed
                ? string.Empty
                : (string.IsNullOrWhiteSpace(_state.Title) ? "当前内容" : _state.Title);
            _slots.VideoModeTitle.text = title;
            _slots.ModelModeTitle.text = title;
        }

        bool HasVisibleFeaturePageActionStatus(bool feature)
            => feature &&
               _state.Page.Feature == _featurePageActionFeature &&
               _featurePageActionState.Visible &&
               !string.IsNullOrWhiteSpace(_featurePageActionState.Status);

        bool IsAvailable(FeaturePageId feature)
        {
            if (_state == null) return false;
            for (var index = 0; index < _state.AvailablePages.Count; index++)
                if (_state.AvailablePages[index] == feature) return true;
            return false;
        }

        void HandleTargetSelected(ShellFlowActionTarget target)
        {
            if (target == null || _state == null || !_state.Session.IsValid) return;
            if (target.Action == ShellFlowAction.CompleteObservation) return;
            if (target.Action == ShellFlowAction.FeaturePageAction)
            {
                if (_state.Page.Kind == FlowPageKind.Feature &&
                    _state.Page.Feature == _featurePageActionFeature &&
                    target.Feature == _featurePageActionFeature &&
                    _featurePageActionState.Visible &&
                    _featurePageActionState.Interactable)
                    _featurePageActionSource?.Invoke(_state.Session);
                return;
            }
            if (target.Action == ShellFlowAction.FeaturePageActionExitFocus)
            {
                if (_state.Page.Kind == FlowPageKind.Feature &&
                    _state.Page.Feature == _featurePageActionFeature &&
                    target.Feature == _featurePageActionFeature &&
                    _featurePageActionState.FocusActive)
                    _featurePageActionSource?.ExitFocus(_state.Session);
                return;
            }
            Dispatch(target.CreateIntent(_state.Session));
        }

        void ReleaseFeaturePageAction(IFeaturePageActionSource source)
        {
            if (!ReferenceEquals(_featurePageActionSource, source)) return;
            _featurePageActionSubscription?.Dispose();
            _featurePageActionSubscription = null;
            _featurePageActionSource = null;
            _featurePageActionFeature = default;
            _featurePageActionState = FeaturePageActionState.Hidden;
            if (_configured) RenderPage();
        }

        sealed class FeaturePageActionLease : IDisposable
        {
            GlobalFrontendShell _owner;
            IFeaturePageActionSource _source;

            public FeaturePageActionLease(
                GlobalFrontendShell owner,
                IFeaturePageActionSource source)
            {
                _owner = owner;
                _source = source;
            }

            public void Dispose()
            {
                var owner = _owner;
                var source = _source;
                _owner = null;
                _source = null;
                owner?.ReleaseFeaturePageAction(source);
            }
        }
    }
}
