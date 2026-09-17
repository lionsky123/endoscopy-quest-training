using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.KnowledgeMiniGame.Contracts
{
    public enum KnowledgeMiniGameKind
    {
        SingleChoice = 0,
        Confirmation = 1
    }

    public sealed class KnowledgeMiniGameOptionDefinition
    {
        public KnowledgeMiniGameOptionDefinition(string answerId, string text)
        {
            if (string.IsNullOrWhiteSpace(answerId))
                throw new ArgumentException("A stable answer ID is required.", nameof(answerId));
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Answer text is required.", nameof(text));

            AnswerId = answerId.Trim();
            Text = text.Trim();
        }

        public string AnswerId { get; }
        public string Text { get; }
    }

    public sealed class KnowledgeMiniGameQuestionDefinition
    {
        readonly ReadOnlyCollection<KnowledgeMiniGameOptionDefinition> _options;
        readonly HashSet<string> _answerIds;

        public KnowledgeMiniGameQuestionDefinition(
            string question,
            IReadOnlyList<KnowledgeMiniGameOptionDefinition> options,
            string correctAnswerId,
            string successExplanation,
            string retryHint)
        {
            if (string.IsNullOrWhiteSpace(question))
                throw new ArgumentException("A knowledge mini-game question is required.", nameof(question));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.Count < KnowledgeMiniGameDefinition.MinimumOptionCount ||
                options.Count > KnowledgeMiniGameDefinition.MaximumOptionCount)
                throw new ArgumentOutOfRangeException(
                    nameof(options),
                    $"KnowledgeMiniGame requires {KnowledgeMiniGameDefinition.MinimumOptionCount} to {KnowledgeMiniGameDefinition.MaximumOptionCount} options.");
            if (string.IsNullOrWhiteSpace(correctAnswerId))
                throw new ArgumentException("A correct answer ID is required.", nameof(correctAnswerId));
            if (string.IsNullOrWhiteSpace(successExplanation))
                throw new ArgumentException("A success explanation is required.", nameof(successExplanation));
            if (string.IsNullOrWhiteSpace(retryHint))
                throw new ArgumentException("A retry hint is required.", nameof(retryHint));

            var copy = new KnowledgeMiniGameOptionDefinition[options.Count];
            _answerIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < copy.Length; index++)
            {
                var option = options[index] ?? throw new ArgumentException(
                    $"Knowledge mini-game option {index} is null.",
                    nameof(options));
                if (!_answerIds.Add(option.AnswerId))
                    throw new ArgumentException(
                        $"Answer ID '{option.AnswerId}' is duplicated.",
                        nameof(options));
                copy[index] = option;
            }

            var canonicalCorrectAnswerId = correctAnswerId.Trim();
            if (!_answerIds.Contains(canonicalCorrectAnswerId))
                throw new ArgumentException(
                    $"Correct answer ID '{canonicalCorrectAnswerId}' is not one of the configured options.",
                    nameof(correctAnswerId));

            Question = question.Trim();
            _options = Array.AsReadOnly(copy);
            CorrectAnswerId = canonicalCorrectAnswerId;
            SuccessExplanation = successExplanation.Trim();
            RetryHint = retryHint.Trim();
        }

        public string Question { get; }
        public IReadOnlyList<KnowledgeMiniGameOptionDefinition> Options => _options;
        public string CorrectAnswerId { get; }
        public string SuccessExplanation { get; }
        public string RetryHint { get; }
        public bool ContainsAnswer(string answerId)
            => !string.IsNullOrWhiteSpace(answerId) && _answerIds.Contains(answerId.Trim());
        public bool IsCorrect(string answerId)
            => string.Equals(CorrectAnswerId, answerId?.Trim(), StringComparison.Ordinal);
    }

    public sealed class KnowledgeMiniGameDefinition
    {
        public const int MinimumOptionCount = 2;
        public const int MaximumOptionCount = 4;
        public const int MinimumQuestionCount = 1;
        public const int MaximumQuestionCount = 4;

        readonly ReadOnlyCollection<KnowledgeMiniGameQuestionDefinition> _questions;

        public KnowledgeMiniGameDefinition(IReadOnlyList<KnowledgeMiniGameQuestionDefinition> questions)
        {
            if (questions == null) throw new ArgumentNullException(nameof(questions));
            if (questions.Count < MinimumQuestionCount || questions.Count > MaximumQuestionCount)
                throw new ArgumentOutOfRangeException(
                    nameof(questions),
                    $"KnowledgeMiniGame requires {MinimumQuestionCount} to {MaximumQuestionCount} questions.");

            var copy = new KnowledgeMiniGameQuestionDefinition[questions.Count];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = questions[index] ?? throw new ArgumentException(
                    $"Knowledge mini-game question {index} is null.",
                    nameof(questions));

            Kind = KnowledgeMiniGameKind.SingleChoice;
            _questions = Array.AsReadOnly(copy);
            ConfirmationPrompt = string.Empty;
            ConfirmationSuccessExplanation = string.Empty;
        }

        KnowledgeMiniGameDefinition(string confirmationPrompt, string successExplanation)
        {
            if (string.IsNullOrWhiteSpace(confirmationPrompt))
                throw new ArgumentException("A confirmation prompt is required.", nameof(confirmationPrompt));
            if (string.IsNullOrWhiteSpace(successExplanation))
                throw new ArgumentException("A confirmation explanation is required.", nameof(successExplanation));

            Kind = KnowledgeMiniGameKind.Confirmation;
            _questions = Array.AsReadOnly(Array.Empty<KnowledgeMiniGameQuestionDefinition>());
            ConfirmationPrompt = confirmationPrompt.Trim();
            ConfirmationSuccessExplanation = successExplanation.Trim();
        }

        public KnowledgeMiniGameKind Kind { get; }
        public IReadOnlyList<KnowledgeMiniGameQuestionDefinition> Questions => _questions;
        public int QuestionCount => Kind == KnowledgeMiniGameKind.Confirmation ? 1 : _questions.Count;
        public string ConfirmationPrompt { get; }
        public string ConfirmationSuccessExplanation { get; }

        public KnowledgeMiniGameQuestionDefinition GetQuestion(int questionIndex)
        {
            if (Kind != KnowledgeMiniGameKind.SingleChoice)
                throw new InvalidOperationException("Confirmation mini-games do not expose selectable questions.");
            if (questionIndex < 0 || questionIndex >= _questions.Count)
                throw new ArgumentOutOfRangeException(nameof(questionIndex));
            return _questions[questionIndex];
        }

        public static KnowledgeMiniGameDefinition CreateConfirmation(
            string confirmationPrompt,
            string successExplanation)
            => new KnowledgeMiniGameDefinition(confirmationPrompt, successExplanation);
    }

    public enum KnowledgeMiniGamePhase
    {
        Closed = 0,
        Ready = 1,
        Incorrect = 2,
        Completed = 3
    }

    public sealed class KnowledgeMiniGameState
    {
        public KnowledgeMiniGameState(
            SessionToken session,
            long version,
            KnowledgeMiniGamePhase phase,
            KnowledgeMiniGameDefinition definition = null,
            int questionIndex = 0,
            string selectedAnswerId = null,
            int attempts = 0,
            string feedback = null)
        {
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (attempts < 0) throw new ArgumentOutOfRangeException(nameof(attempts));
            if (phase != KnowledgeMiniGamePhase.Closed && definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (phase != KnowledgeMiniGamePhase.Closed &&
                (questionIndex < 0 || questionIndex >= definition.QuestionCount))
                throw new ArgumentOutOfRangeException(nameof(questionIndex));

            Session = session;
            Version = version;
            Phase = phase;
            Definition = definition;
            QuestionIndex = questionIndex;
            SelectedAnswerId = selectedAnswerId ?? string.Empty;
            Attempts = attempts;
            Feedback = feedback ?? string.Empty;
        }

        public SessionToken Session { get; }
        public long Version { get; }
        public KnowledgeMiniGamePhase Phase { get; }
        public KnowledgeMiniGameDefinition Definition { get; }
        public int QuestionIndex { get; }
        public int QuestionCount => Definition?.QuestionCount ?? 0;
        public KnowledgeMiniGameQuestionDefinition CurrentQuestion
            => Definition != null && Definition.Kind == KnowledgeMiniGameKind.SingleChoice
                ? Definition.GetQuestion(QuestionIndex)
                : null;
        public string SelectedAnswerId { get; }
        public int Attempts { get; }
        public string Feedback { get; }
        public bool CanSubmit => Phase == KnowledgeMiniGamePhase.Ready || Phase == KnowledgeMiniGamePhase.Incorrect;
        public bool IsCompleted => Phase == KnowledgeMiniGamePhase.Completed;
    }

    public enum KnowledgeMiniGameFailureCode
    {
        None = 0,
        InvalidSession = 1,
        InvalidDefinition = 2,
        AlreadyOpen = 3,
        Closed = 4,
        StaleSession = 5,
        InvalidAnswer = 6,
        AlreadyCompleted = 7,
        Disposed = 8
    }

    public readonly struct KnowledgeMiniGameResult
    {
        KnowledgeMiniGameResult(bool succeeded, KnowledgeMiniGameFailureCode failureCode)
        {
            Succeeded = succeeded;
            FailureCode = failureCode;
        }

        public bool Succeeded { get; }
        public KnowledgeMiniGameFailureCode FailureCode { get; }
        public static KnowledgeMiniGameResult Success => new KnowledgeMiniGameResult(true, KnowledgeMiniGameFailureCode.None);
        public static KnowledgeMiniGameResult Failure(KnowledgeMiniGameFailureCode code)
        {
            if (code == KnowledgeMiniGameFailureCode.None)
                throw new ArgumentOutOfRangeException(nameof(code));
            return new KnowledgeMiniGameResult(false, code);
        }
    }

    public interface IKnowledgeMiniGameController : IDisposable
    {
        KnowledgeMiniGameResult Open(SessionToken session, KnowledgeMiniGameDefinition definition);
        KnowledgeMiniGameResult Submit(SessionToken session, string answerId);
        KnowledgeMiniGameResult Close(SessionToken session);
        IDisposable Observe(IKnowledgeMiniGameStateSink sink);
    }

    public interface IKnowledgeMiniGameStateSink
    {
        void Publish(KnowledgeMiniGameState state);
    }

    public interface IKnowledgeMiniGameDefinitionSource
    {
        bool TryGet(SceneId sceneId, out KnowledgeMiniGameDefinition definition);
    }
}
