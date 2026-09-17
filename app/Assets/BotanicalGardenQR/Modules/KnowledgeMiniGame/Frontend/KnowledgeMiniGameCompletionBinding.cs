using System;
using BotanicalGardenQR.Activation.Contracts;
using UnityEngine;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.KnowledgeMiniGame.Contracts;

namespace BotanicalGardenQR.KnowledgeMiniGame.Frontend
{
    /// <summary>
    /// Opens a knowledge quiz only after the matching content has closed.
    /// A request is an intent, while lifecycle facts retain the content identity.
    /// </summary>
    public sealed class KnowledgeMiniGameCompletionBinding : IContentLifecycleSink, IDisposable
    {
        readonly IKnowledgeMiniGameDefinitionSource _definitions;
        readonly IKnowledgeMiniGameController _controller;
        readonly KnowledgeMiniGameFrontend _frontend;
        readonly Action<ObservationCompletedFact> _completed;
        readonly IObservationCompletionRequestSource _requests;
        readonly IDisposable _contentSubscription;

        SessionToken _session;
        ContentOpenedFact _openedFact;
        ContentClosedFact _closedFact;
        IDisposable _stateSubscription;
        ObservationCompletedFact _pendingCompletion;
        bool _completedForClosedContent;
        bool _disposed;

        public KnowledgeMiniGameCompletionBinding(
            IContentLifecycleSource content,
            IObservationCompletionRequestSource requests,
            IKnowledgeMiniGameDefinitionSource definitions,
            IKnowledgeMiniGameController controller,
            KnowledgeMiniGameFrontend frontend,
            Action<ObservationCompletedFact> completed)
        {
            _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _frontend = frontend ?? throw new ArgumentNullException(nameof(frontend));
            _completed = completed ?? throw new ArgumentNullException(nameof(completed));
            _requests = requests ?? throw new ArgumentNullException(nameof(requests));
            _requests.ObservationCompletionRequested += HandleCompletionRequested;
            _frontend.ExitRequested += HandleExitRequested;
            _contentSubscription = (content ?? throw new ArgumentNullException(nameof(content))).Observe(this);
        }

        public void OnContentOpened(ContentOpenedFact fact)
        {
            if (_disposed) return;
            CloseSurface();
            _openedFact = fact;
            _closedFact = null;
            _completedForClosedContent = false;
        }

        public void OnContentClosed(ContentClosedFact fact)
        {
            if (_disposed || fact == null || _openedFact == null || _closedFact != null ||
                fact.ContentSession != _openedFact.ContentSession)
                return;
            CloseSurface();
            _closedFact = fact;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _requests.ObservationCompletionRequested -= HandleCompletionRequested;
            _frontend.ExitRequested -= HandleExitRequested;
            _contentSubscription.Dispose();
            CloseSurface();
            _openedFact = null;
            _closedFact = null;
            _frontend.Dispose();
        }

        void HandleCompletionRequested()
        {
            if (_disposed || _openedFact == null || _closedFact == null || _session.IsValid ||
                _completedForClosedContent || _closedFact.ContentSession != _openedFact.ContentSession)
                return;

            var definition = _definitions.TryGet(_openedFact.SceneId, out var configured) && configured != null
                ? configured
                : KnowledgeMiniGameDefinition.CreateConfirmation(
                    "确认完成本次观察？",
                    "观察完成了，准备好继续探索吧。");
            _session = _closedFact.ContentSession;
            _pendingCompletion = null;
            _frontend.Bind(_session, _controller);
            _stateSubscription = _controller.Observe(new CompletionStateSink(this));
            var opened = _controller.Open(_session, definition);
            Debug.Log($"[KnowledgeQuiz] open requested scene={_openedFact.SceneId} opened={opened.Succeeded}.");
            if (!opened.Succeeded)
            {
                CloseSurface();
                return;
            }
            _frontend.SetVisible(true);
        }

        void HandleExitRequested(ObservationCompletionExitKind exitKind)
        {
            if (_disposed || !_session.IsValid) return;
            if (exitKind == ObservationCompletionExitKind.AcknowledgeCompletion)
            {
                var completed = _pendingCompletion;
                if (completed == null || completed.ContentSession != _session || _completedForClosedContent)
                    return;
                _completedForClosedContent = true;
                // Release the quiz modal before Collection/Journey consume the fact.
                // The explanation stays readable until this explicit acknowledgement.
                CloseSurface();
                Debug.Log($"[KnowledgeQuiz] completion fact published scene={completed.SceneId} kind={completed.Kind}.");
                _completed(completed);
                return;
            }
            if (_pendingCompletion != null) return;
            CloseSurface();
        }

        void HandleState(KnowledgeMiniGameState state)
        {
            if (_disposed || state == null || state.Phase != KnowledgeMiniGamePhase.Completed ||
                state.Session != _session || _pendingCompletion != null || _completedForClosedContent ||
                _openedFact == null || _closedFact == null)
                return;

            var kind = state.Definition.Kind == KnowledgeMiniGameKind.Confirmation
                ? ObservationCompletionKind.Confirmation
                : ObservationCompletionKind.SingleChoice;
            _pendingCompletion = new ObservationCompletedFact(
                _closedFact.ContentSession,
                _closedFact.JourneySession,
                _closedFact.SceneId,
                kind,
                "observation:" + _closedFact.SceneId.Value,
                kind == ObservationCompletionKind.SingleChoice ? state.SelectedAnswerId : null);
        }

        void CloseSurface()
        {
            _stateSubscription?.Dispose();
            _stateSubscription = null;
            _frontend.SetVisible(false);
            if (_session.IsValid) _controller.Close(_session);
            _frontend.Unbind();
            _session = default;
            _pendingCompletion = null;
        }

        sealed class CompletionStateSink : IKnowledgeMiniGameStateSink
        {
            readonly KnowledgeMiniGameCompletionBinding _owner;

            public CompletionStateSink(KnowledgeMiniGameCompletionBinding owner)
            {
                _owner = owner;
            }

            public void Publish(KnowledgeMiniGameState state)
                => _owner?.HandleState(state);
        }
    }
}
