using System;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Activation.Contracts
{
    /// <summary>
    /// A committed content-open event. Consumers must not infer it from UI, media, or navigation state.
    /// </summary>
    public sealed class ContentOpenedFact
    {
        public ContentOpenedFact(
            SessionToken contentSession,
            JourneySessionId journeySession,
            SceneId sceneId,
            SourceKind entryKind,
            string entryRouteId,
            bool isRecall = false)
        {
            if (!contentSession.IsValid) throw new ArgumentException("A valid content session is required.", nameof(contentSession));
            if (!journeySession.IsValid) throw new ArgumentException("A valid journey session is required.", nameof(journeySession));
            if (!sceneId.IsValid) throw new ArgumentException("A valid SceneId is required.", nameof(sceneId));
            if (!entryKind.IsValid) throw new ArgumentException("A valid entry kind is required.", nameof(entryKind));
            if (string.IsNullOrWhiteSpace(entryRouteId) || !string.Equals(entryRouteId, entryRouteId.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("A canonical entry route ID is required.", nameof(entryRouteId));

            ContentSession = contentSession;
            JourneySession = journeySession;
            SceneId = sceneId;
            EntryKind = entryKind;
            EntryRouteId = entryRouteId;
            IsRecall = isRecall;
        }

        public SessionToken ContentSession { get; }
        public JourneySessionId JourneySession { get; }
        public SceneId SceneId { get; }
        public SourceKind EntryKind { get; }
        public string EntryRouteId { get; }
        public bool IsRecall { get; }
    }
}
