using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.JourneyNavigation.Contracts;

namespace BotanicalGardenQR.Bootstrap
{
    /// <summary>
    /// Application-level seam for completing the actual just-closed QR content.
    /// It scopes close decisions to the just-closed content, coordinates direct
    /// collection for quiz skip, and records completion only for the matching content session.
    /// </summary>
    public sealed class JourneyCloseDecisionBinding : IContentLifecycleSink, IDisposable
    {
        readonly IJourneyNavigation _journey;
        readonly IClosedContentJourneyIntentSource _intents;
        readonly INextPointPromptPresenter _prompts;
        readonly ICollectionProgress _collection;
        readonly ICollectionCatalogSource _collectionCatalog;
        readonly bool _collectionsEnabled;
        readonly IDisposable _contentSubscription;
        ContentOpenedFact _lastOpened;
        ContentClosedFact _lastClosed;
        bool _disposed;

        public JourneyCloseDecisionBinding(
            IContentLifecycleSource content,
            JourneySessionId journeySession,
            IJourneyNavigation journey,
            IClosedContentJourneyIntentSource intents,
            INextPointPromptPresenter prompts,
            ICollectionProgress collection,
            ICollectionCatalogSource collectionCatalog,
            bool collectionsEnabled = true)
        {
            if (!journeySession.IsValid)
                throw new ArgumentException("A valid journey session is required.", nameof(journeySession));
            _journey = journey ?? throw new ArgumentNullException(nameof(journey));
            _intents = intents ?? throw new ArgumentNullException(nameof(intents));
            _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
            _collectionCatalog = collectionCatalog ?? throw new ArgumentNullException(nameof(collectionCatalog));
            _collectionsEnabled = collectionsEnabled;

            _journey.BeginJourney(journeySession);
            _intents.SkipQuizAndCollectRequested += HandleSkipQuizAndCollectRequested;
            _contentSubscription = (content ?? throw new ArgumentNullException(nameof(content))).Observe(this);
        }

        public void OnContentOpened(ContentOpenedFact fact)
        {
            if (_disposed || fact == null) return;
            _lastOpened = fact;
            _lastClosed = null;
            _prompts.SetClosedContentRequiresLearning(!_collectionsEnabled && ClinicalCourseScenes.Contains(fact.SceneId.Value));
            _prompts.SetClosedContentContext(string.Empty);
            _prompts.ClearJourneyPrompt();
            _journey.AcceptContentOpened(fact);
        }

        public void OnContentClosed(ContentClosedFact fact)
        {
            if (_disposed || fact == null) return;
            _journey.AcceptContentClosed(fact);
            if (IsSameContent(_lastOpened, fact))
            {
                _lastClosed = fact;
                _prompts.SetClosedContentContext(string.Empty);
            }
        }

        public void AcceptObservationCompleted(ObservationCompletedFact completed)
        {
            if (_disposed || completed == null) return;
            var state = _journey.CurrentState;
            if (!IsSameContent(_lastClosed, completed) || completed.JourneySession != state.Session)
                return;
            if (completed.SceneId != state.CurrentScene ||
                state.ProgressState != JourneyProgressState.AtSelectedPoint ||
                state.ContentState != JourneyContentState.Paused)
            {
                _lastClosed = null;
                PresentWithoutJourneyAdvance( "本次观察已完成。");
                return;
            }
            var result = _journey.Dispatch(JourneyIntent.ContinueToNext);
            if (result.Succeeded) _lastClosed = null;
            PresentDispatchResult(result);
        }

        public void AcceptClinicalLessonCompleted(SessionToken contentSession)
        {
            var closed = _lastClosed;
            if (_disposed || closed == null || closed.ContentSession != contentSession ||
                !ClinicalCourseScenes.Contains(closed.SceneId.Value) || !IsSameContent(_lastOpened, closed)) return;
            AcceptObservationCompleted(new ObservationCompletedFact(closed.ContentSession,
                closed.JourneySession, closed.SceneId, ObservationCompletionKind.Confirmation,
                "clinical-picture:" + closed.SceneId.Value));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _intents.SkipQuizAndCollectRequested -= HandleSkipQuizAndCollectRequested;
            _contentSubscription.Dispose();
            _lastOpened = null;
            _lastClosed = null;
        }

        void HandleSkipQuizAndCollectRequested()
        {
            if (_disposed) return;
            var closed = _lastClosed;
            if (!_collectionsEnabled && ClinicalCourseScenes.Contains(closed?.SceneId.Value)) return;
            var state = _journey.CurrentState;
            if (closed == null || !IsSameContent(_lastOpened, closed) ||
                closed.JourneySession != state.Session)
            {
                _prompts.ShowJourneyMessage("本次关闭内容已失效，请重新打开后再选择。");
                return;
            }

            if (_collectionsEnabled && !_collectionCatalog.TryGetArtifactId(closed.SceneId, out _))
            {
                _prompts.ShowJourneyMessage("本次内容尚未配置发现物，无法直接收藏。");
                return;
            }

            if (_collectionsEnabled)
            {
                _collectionCatalog.TryGetArtifactId(closed.SceneId, out var artifactId);
                var grant = _collection.GrantArtifact(closed.JourneySession, artifactId);
                if (!grant.Succeeded)
                {
                    _prompts.ShowJourneyMessage(CollectionFailureMessage(grant.FailureCode));
                    return;
                }
            }

            _lastClosed = null;
            var isCurrentPoint = state.ProgressState == JourneyProgressState.AtSelectedPoint &&
                                 state.ContentState == JourneyContentState.Paused &&
                                 closed.SceneId == state.CurrentScene;
            if (!isCurrentPoint)
            {
                PresentWithoutJourneyAdvance(_collectionsEnabled ? "发现物已收入收藏。" : "本次学习已完成。");
                return;
            }

            PresentDispatchResult(
                _journey.Dispatch(JourneyIntent.ContinueToNext));
        }

        void PresentDispatchResult(JourneyCommandResult result)
        {
            if (!result.Succeeded)
            {
                _prompts.ShowJourneyMessage(MessageFor(result.Failure));
                return;
            }

            // The guidance owner presents the next geometric leg. No plant is selected here.
            _prompts.CompleteClosedContent();
            _prompts.ClearJourneyPrompt();
        }

        static string MessageFor(JourneyCommandFailure failure)
        {
            switch (failure)
            {
                case JourneyCommandFailure.NoActiveJourney:
                    return "学习尚未开始，请返回当前点位触碰“开启发现”。";
                case JourneyCommandFailure.CurrentPointUnavailable:
                case JourneyCommandFailure.InvalidState:
                    return "请先关闭当前内容，再选择下一步。";
                default:
                    return "当前无法前往下一站。";
            }
        }

        void PresentWithoutJourneyAdvance(string outcome)
        {
            _prompts.ShowJourneyStatus(outcome);
        }

        static string CollectionFailureMessage(CollectionMutationFailureCode failure)
        {
            switch (failure)
            {
                case CollectionMutationFailureCode.NoSession:
                case CollectionMutationFailureCode.InvalidSession:
                    return "本次收藏会话已失效，请重新打开内容后再试。";
                case CollectionMutationFailureCode.InvalidArtifact:
                    return "本次内容的发现物配置不可用。";
                default:
                    return "当前无法直接收藏本次发现物。";
            }
        }

        static bool IsSameContent(ContentOpenedFact opened, ContentClosedFact closed)
            => opened != null && closed != null &&
               opened.ContentSession == closed.ContentSession &&
               opened.JourneySession == closed.JourneySession &&
               opened.SceneId == closed.SceneId;

        static bool IsSameContent(ContentClosedFact closed, ObservationCompletedFact completed)
            => closed != null && completed != null &&
               closed.ContentSession == completed.ContentSession &&
               closed.JourneySession == completed.JourneySession &&
               closed.SceneId == completed.SceneId;
    }
}
