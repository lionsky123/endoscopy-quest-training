using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.KnowledgeMiniGame.Contracts;

namespace BotanicalGardenQR.KnowledgeMiniGame.Backend
{
    internal sealed class KnowledgeMiniGameController : IKnowledgeMiniGameController
    {
        readonly StateChannel<KnowledgeMiniGameState> _states;

        SessionToken _session;
        KnowledgeMiniGameDefinition _definition;
        long _version;
        int _questionIndex;
        int _attempts;
        bool _completed;
        bool _disposed;

        public KnowledgeMiniGameController()
        {
            _states = StateChannel<KnowledgeMiniGameState>.ForCurrentThread(
                new KnowledgeMiniGameState(default, 0, KnowledgeMiniGamePhase.Closed),
                state => state.Version);
        }

        public KnowledgeMiniGameResult Open(SessionToken session, KnowledgeMiniGameDefinition definition)
        {
            if (_disposed) return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.Disposed);
            if (!session.IsValid) return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.InvalidSession);
            if (definition == null) return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.InvalidDefinition);
            if (_session.IsValid) return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.AlreadyOpen);

            _session = session;
            _definition = definition;
            _questionIndex = 0;
            _attempts = 0;
            _completed = false;
            Publish(KnowledgeMiniGamePhase.Ready);
            return KnowledgeMiniGameResult.Success;
        }

        public KnowledgeMiniGameResult Submit(SessionToken session, string answerId)
        {
            if (_disposed) return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.Disposed);
            if (!session.IsValid) return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.InvalidSession);
            if (!_session.IsValid) return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.Closed);
            if (session != _session) return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.StaleSession);
            if (_completed) return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.AlreadyCompleted);
            if (string.IsNullOrWhiteSpace(answerId))
                return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.InvalidAnswer);
            var canonicalAnswerId = answerId.Trim();
            if (_definition.Kind == KnowledgeMiniGameKind.Confirmation)
            {
                if (!string.Equals(canonicalAnswerId, "confirm", StringComparison.Ordinal))
                    return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.InvalidAnswer);
                _attempts++;
                _completed = true;
                Publish(
                    KnowledgeMiniGamePhase.Completed,
                    canonicalAnswerId,
                    _definition.ConfirmationSuccessExplanation);
            }
            else
            {
                var question = _definition.GetQuestion(_questionIndex);
                if (!question.ContainsAnswer(canonicalAnswerId))
                    return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.InvalidAnswer);

                _attempts++;
                if (!question.IsCorrect(canonicalAnswerId))
                {
                    Publish(
                        KnowledgeMiniGamePhase.Incorrect,
                        canonicalAnswerId,
                        question.RetryHint);
                }
                else if (_questionIndex + 1 < _definition.QuestionCount)
                {
                    _questionIndex++;
                    Publish(
                        KnowledgeMiniGamePhase.Ready,
                        feedback: question.SuccessExplanation);
                }
                else
                {
                    _completed = true;
                    Publish(
                        KnowledgeMiniGamePhase.Completed,
                        canonicalAnswerId,
                        question.SuccessExplanation);
                }
            }
            return KnowledgeMiniGameResult.Success;
        }

        public KnowledgeMiniGameResult Close(SessionToken session)
        {
            if (_disposed || !_session.IsValid) return KnowledgeMiniGameResult.Success;
            if (session != _session) return KnowledgeMiniGameResult.Failure(KnowledgeMiniGameFailureCode.StaleSession);

            Publish(KnowledgeMiniGamePhase.Closed);
            _session = default;
            _definition = null;
            _questionIndex = 0;
            _attempts = 0;
            _completed = false;
            return KnowledgeMiniGameResult.Success;
        }

        public IDisposable Observe(IKnowledgeMiniGameStateSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (_disposed) throw new ObjectDisposedException(nameof(KnowledgeMiniGameController));
            return _states.Observe(sink.Publish);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _session = default;
            _definition = null;
            _questionIndex = 0;
            _states.Dispose();
        }

        void Publish(
            KnowledgeMiniGamePhase phase,
            string selectedAnswerId = null,
            string feedback = null)
        {
            _states.Publish(new KnowledgeMiniGameState(
                _session,
                ++_version,
                phase,
                phase == KnowledgeMiniGamePhase.Closed ? null : _definition,
                _questionIndex,
                selectedAnswerId,
                _attempts,
                feedback));
        }
    }

    public static class KnowledgeMiniGameModuleFactory
    {
        public static IKnowledgeMiniGameController Create()
            => new KnowledgeMiniGameController();
    }
}
