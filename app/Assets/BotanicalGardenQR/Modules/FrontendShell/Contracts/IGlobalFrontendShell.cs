using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    public interface IClinicalLessonReview
    {
        FlowResult CloseClinicalReview(SessionToken session);
    }
    public interface IClinicalLessonCompletion
    {
        FlowResult CompleteClinicalLesson(SessionToken session);
    }

    public interface IGlobalFrontendShell : IDisposable
    {
        FlowResult Dispatch(FlowIntent intent);
        void ShowStatus(SessionToken session, UserFault fault);
        IDisposable BindFeaturePageAction(
            FeaturePageId feature,
            IFeaturePageActionSource source);
        FrontendSurfaceResult AcquireFeatureSurface(SessionToken session, FeaturePageId feature);
        FrontendNarrationDockLease AcquireNarrationDock(SessionToken session);
        FlowResult SetImmersiveFeature(SessionToken session, FeaturePageId feature, bool active);
    }

    public interface IFeaturePageActionSource
    {
        IDisposable Observe(IFeaturePageActionStateSink sink);
        void Invoke(SessionToken session);
        void ExitFocus(SessionToken session);
    }

    public interface IFeaturePageActionStateSink
    {
        void OnFeaturePageActionStateChanged(FeaturePageActionState state);
    }

    public readonly struct FeaturePageActionState
    {
        public FeaturePageActionState(
            bool visible,
            bool interactable,
            string label,
            string status,
            bool focusActive = false,
            string focusExitLabel = "")
        {
            if (interactable && !visible)
                throw new ArgumentException("A hidden feature-page action cannot be interactable.", nameof(interactable));
            if (visible && string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("A visible feature-page action requires a label.", nameof(label));
            if (focusActive && string.IsNullOrWhiteSpace(focusExitLabel))
                throw new ArgumentException(
                    "An active feature focus requires an exit label.",
                    nameof(focusExitLabel));
            Visible = visible;
            Interactable = interactable;
            Label = label ?? string.Empty;
            Status = status ?? string.Empty;
            FocusActive = focusActive;
            FocusExitLabel = focusExitLabel ?? string.Empty;
        }

        public bool Visible { get; }
        public bool Interactable { get; }
        public string Label { get; }
        public string Status { get; }
        public bool FocusActive { get; }
        public string FocusExitLabel { get; }
        public static FeaturePageActionState Hidden =>
            new FeaturePageActionState(false, false, string.Empty, string.Empty);
    }

    public readonly struct FrontendSurfaceResult
    {
        FrontendSurfaceResult(
            FrontendFeatureSurfaceLease lease,
            FrontendSurfaceFailure failure,
            UserFault fault)
        {
            Lease = lease;
            Failure = failure;
            Fault = fault;
        }

        public bool Succeeded => Lease != null;
        public FrontendFeatureSurfaceLease Lease { get; }
        public FrontendSurfaceFailure Failure { get; }
        public UserFault Fault { get; }

        public static FrontendSurfaceResult Success(FrontendFeatureSurfaceLease lease)
            => new FrontendSurfaceResult(
                lease ?? throw new ArgumentNullException(nameof(lease)),
                FrontendSurfaceFailure.None,
                null);

        public static FrontendSurfaceResult Reject(
            FrontendSurfaceFailure failure,
            UserFault fault = null)
        {
            if (failure == FrontendSurfaceFailure.None)
                throw new ArgumentException("A rejected surface request requires a failure code.", nameof(failure));
            return new FrontendSurfaceResult(null, failure, fault);
        }
    }

    public enum FrontendSurfaceFailure
    {
        None = 0,
        StaleSession = 1,
        PageUnavailable = 2,
        AlreadyLeased = 3,
        ShellUnavailable = 4
    }
}
