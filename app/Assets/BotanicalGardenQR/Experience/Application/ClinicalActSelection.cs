using System;
using System.Collections.Generic;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.Experience.Application
{
    public enum ClinicalActDestinationKind
    {
        ContinueMainline,
        ReviewVisitedChapter,
        ReturnToLobby
    }

    public enum ClinicalOfficeVisitStage
    {
        NotOffice,
        FirstVisit,
        Review,
        FinalSummary
    }

    public readonly struct ClinicalActDestination
    {
        readonly string[] _taskIds;

        public ClinicalActDestination(string roomId, ClinicalActDestinationKind kind, string[] taskIds)
        {
            RoomId = roomId;
            Kind = kind;
            _taskIds = taskIds == null ? new string[0] : (string[])taskIds.Clone();
        }

        public string RoomId { get; }
        public ClinicalActDestinationKind Kind { get; }
        public bool IsChapter => Kind != ClinicalActDestinationKind.ReturnToLobby;
        public string[] TaskIds => (string[])_taskIds.Clone();
    }

    /// <summary>
    /// Builds room choices from the existing journey and session. Lobby navigation is
    /// kept separate from business chapters, and only the next ordered visit advances.
    /// </summary>
    public static class ClinicalActSelection
    {
        public static ClinicalActDestination[] AvailableDestinations(
            ClinicalJourneyDefinition definition, ClinicalJourneySession session)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (session == null) throw new ArgumentNullException(nameof(session));

            var options = new List<ClinicalActDestination>();
            var added = new HashSet<string>(StringComparer.Ordinal);
            var currentRoomId = session.CurrentRoomId;
            var nextMainlineIndex = session.MainlineIndex + 1;

            if (!session.IsFinished && nextMainlineIndex < definition.mainlineRoomIds.Length)
            {
                var nextRoomId = definition.mainlineRoomIds[nextMainlineIndex];
                if (nextRoomId != definition.startRoomId && nextRoomId != currentRoomId &&
                    definition.CanTransfer(currentRoomId, nextRoomId) && added.Add(nextRoomId))
                    options.Add(Chapter(definition, nextRoomId, ClinicalActDestinationKind.ContinueMainline));
            }

            foreach (var room in definition.rooms)
            {
                if (room == null || string.IsNullOrWhiteSpace(room.id) || room.id == currentRoomId ||
                    room.id == definition.startRoomId || !session.HasVisited(room.id) ||
                    !definition.CanTransfer(currentRoomId, room.id) || !added.Add(room.id))
                    continue;

                options.Add(Chapter(definition, room.id, ClinicalActDestinationKind.ReviewVisitedChapter));
            }

            var lobbyId = definition.startRoomId;
            if (currentRoomId != lobbyId && session.HasVisited(lobbyId) &&
                definition.CanTransfer(currentRoomId, lobbyId) && added.Add(lobbyId))
                options.Add(new ClinicalActDestination(lobbyId, ClinicalActDestinationKind.ReturnToLobby, null));

            return options.ToArray();
        }

        public static ClinicalOfficeVisitStage OfficeVisitStage(
            ClinicalJourneyDefinition definition, ClinicalJourneySession session)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.CurrentRoomId != definition.summaryRoomId) return ClinicalOfficeVisitStage.NotOffice;

            if (session.IsAtFinalSummary) return ClinicalOfficeVisitStage.FinalSummary;
            return session.RoomVisitCount(definition.summaryRoomId) <= 1
                ? ClinicalOfficeVisitStage.FirstVisit
                : ClinicalOfficeVisitStage.Review;
        }

        static ClinicalActDestination Chapter(
            ClinicalJourneyDefinition definition, string roomId, ClinicalActDestinationKind kind)
        {
            var room = definition.FindRoom(roomId);
            return new ClinicalActDestination(roomId, kind, room?.taskIds);
        }
    }
}
