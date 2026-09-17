using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.JourneyNavigation.Contracts
{
    public enum JourneyPointResult
    {
        Unvisited,
        Visited,
        Explored
    }

    public enum JourneyContentState
    {
        NotOpen,
        Viewing,
        Paused
    }

    /// <summary>Actual QR content progress; no route or preselected content sequence.</summary>
    public enum JourneyProgressState
    {
        Unavailable = 0,
        AtSelectedPoint = 1,
        AwaitingQr = 2
    }

    public enum JourneyIntentKind
    {
        RecallCurrentPoint,
        ContinueToNext
    }

    public readonly struct JourneyIntent
    {
        JourneyIntent(JourneyIntentKind kind) { Kind = kind; }
        public JourneyIntentKind Kind { get; }
        public static JourneyIntent RecallCurrentPoint => new JourneyIntent(JourneyIntentKind.RecallCurrentPoint);
        public static JourneyIntent ContinueToNext => new JourneyIntent(JourneyIntentKind.ContinueToNext);
    }

    public enum JourneyCommandFailure
    {
        None,
        NoActiveJourney,
        CurrentPointUnavailable,
        InvalidState
    }

    public readonly struct JourneyCommandResult
    {
        JourneyCommandResult(
            bool succeeded,
            JourneyCommandFailure failure,
            bool requiresActivationRecall,
            JourneyViewState state)
        {
            Succeeded = succeeded;
            Failure = failure;
            RequiresActivationRecall = requiresActivationRecall;
            State = state;
        }

        public bool Succeeded { get; }
        public JourneyCommandFailure Failure { get; }
        public bool RequiresActivationRecall { get; }
        public JourneyViewState State { get; }

        public static JourneyCommandResult Success(
            JourneyViewState state,
            bool requiresActivationRecall = false)
            => new JourneyCommandResult(true, JourneyCommandFailure.None, requiresActivationRecall, state);

        public static JourneyCommandResult Reject(JourneyCommandFailure failure, JourneyViewState state)
        {
            if (failure == JourneyCommandFailure.None) throw new ArgumentException("A rejected command requires a failure.", nameof(failure));
            return new JourneyCommandResult(false, failure, false, state);
        }
    }

    public sealed class JourneyViewState
    {
        public JourneyViewState(
            long version,
            JourneySessionId session,
            SceneId currentScene,
            JourneyPointResult pointResult,
            JourneyContentState contentState,
            JourneyProgressState progressState,
            bool canContinue,
            string message,
            int completedCount = 0,
            long completionRevision = 0)
        {
            if (completedCount < 0) throw new ArgumentOutOfRangeException(nameof(completedCount));
            if (completionRevision < 0) throw new ArgumentOutOfRangeException(nameof(completionRevision));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (!Enum.IsDefined(typeof(JourneyPointResult), pointResult)) throw new ArgumentOutOfRangeException(nameof(pointResult));
            if (!Enum.IsDefined(typeof(JourneyContentState), contentState)) throw new ArgumentOutOfRangeException(nameof(contentState));
            if (!Enum.IsDefined(typeof(JourneyProgressState), progressState)) throw new ArgumentOutOfRangeException(nameof(progressState));
            Version = version;
            Session = session;
            CurrentScene = currentScene;
            PointResult = pointResult;
            ContentState = contentState;
            ProgressState = progressState;
            CanContinue = canContinue;
            Message = message ?? string.Empty;
            CompletedCount = completedCount;
            CompletionRevision = completionRevision;
        }

        public long Version { get; }
        public JourneySessionId Session { get; }
        public SceneId CurrentScene { get; }
        public JourneyPointResult PointResult { get; }
        public JourneyContentState ContentState { get; }
        public JourneyProgressState ProgressState { get; }
        public bool CanContinue { get; }
        public string Message { get; }
        public int CompletedCount { get; }
        public long CompletionRevision { get; }
    }

    public interface IJourneyNavigation : IDisposable
    {
        JourneyViewState CurrentState { get; }
        void BeginJourney(JourneySessionId session);
        bool AcceptContentOpened(ContentOpenedFact fact);
        bool AcceptContentClosed(ContentClosedFact fact);
        JourneyCommandResult Dispatch(JourneyIntent intent);
        IDisposable Observe(IJourneyNavigationStateSink sink);
    }

    public interface IJourneyNavigationStateSink
    {
        void OnJourneyStateChanged(JourneyViewState state);
    }
}
