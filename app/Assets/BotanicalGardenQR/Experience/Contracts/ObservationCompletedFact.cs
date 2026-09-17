using System;

namespace BotanicalGardenQR.Experience.Contracts
{
    public enum ObservationCompletionKind
    {
        SingleChoice = 0,
        Confirmation = 1
    }

    /// <summary>
    /// An application-owned proof that the visitor performed the configured
    /// explicit observation-completion interaction. It never claims that a
    /// visitor read every word or mastered the content.
    /// </summary>
    public sealed class ObservationCompletedFact
    {
        public ObservationCompletedFact(
            SessionToken contentSession,
            JourneySessionId journeySession,
            SceneId sceneId,
            ObservationCompletionKind kind,
            string completionId,
            string answerId = null)
        {
            if (!contentSession.IsValid)
                throw new ArgumentException("A valid content session is required.", nameof(contentSession));
            if (!journeySession.IsValid)
                throw new ArgumentException("A valid journey session is required.", nameof(journeySession));
            if (!sceneId.IsValid)
                throw new ArgumentException("A valid SceneId is required.", nameof(sceneId));
            if (!Enum.IsDefined(typeof(ObservationCompletionKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(completionId) ||
                !string.Equals(completionId, completionId.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("A canonical completion ID is required.", nameof(completionId));
            if (kind == ObservationCompletionKind.SingleChoice && string.IsNullOrWhiteSpace(answerId))
                throw new ArgumentException("A completed choice requires an answer ID.", nameof(answerId));

            ContentSession = contentSession;
            JourneySession = journeySession;
            SceneId = sceneId;
            Kind = kind;
            CompletionId = completionId;
            AnswerId = answerId?.Trim() ?? string.Empty;
        }

        public SessionToken ContentSession { get; }
        public JourneySessionId JourneySession { get; }
        public SceneId SceneId { get; }
        public ObservationCompletionKind Kind { get; }
        public string CompletionId { get; }
        public string AnswerId { get; }
    }
}
