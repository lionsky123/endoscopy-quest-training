using System;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Activation.Contracts
{
    /// <summary>
    /// A content session was closed. Closing never implies journey progression.
    /// </summary>
    public sealed class ContentClosedFact
    {
        public ContentClosedFact(
            SessionToken contentSession,
            JourneySessionId journeySession,
            SceneId sceneId,
            bool isExplicit)
        {
            if (!contentSession.IsValid) throw new ArgumentException("A valid content session is required.", nameof(contentSession));
            if (!journeySession.IsValid) throw new ArgumentException("A valid journey session is required.", nameof(journeySession));
            if (!sceneId.IsValid) throw new ArgumentException("A valid SceneId is required.", nameof(sceneId));
            ContentSession = contentSession;
            JourneySession = journeySession;
            SceneId = sceneId;
            IsExplicit = isExplicit;
        }

        public SessionToken ContentSession { get; }
        public JourneySessionId JourneySession { get; }
        public SceneId SceneId { get; }
        public bool IsExplicit { get; }
    }
}
