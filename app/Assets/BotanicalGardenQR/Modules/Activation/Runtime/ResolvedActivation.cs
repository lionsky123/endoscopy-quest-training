using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.SpatialHost.Contracts;

namespace BotanicalGardenQR.Activation.Runtime
{
    public sealed class ResolvedActivation
    {
        public ResolvedActivation(
            SceneId sceneId,
            DisplayProfile displayProfile,
            RecognitionObservation observation,
            string entryRouteId = null)
        {
            if (!sceneId.IsValid) throw new ArgumentException("A valid scene name is required.", nameof(sceneId));
            SceneId = sceneId;
            DisplayProfile = displayProfile != null ? displayProfile : throw new ArgumentNullException(nameof(displayProfile));
            if (observation.ObservationId == Guid.Empty) throw new ArgumentException("A constructed observation is required.", nameof(observation));
            Observation = observation;
            EntryRouteId = entryRouteId ?? string.Empty;
        }
        public SceneId SceneId { get; }
        public DisplayProfile DisplayProfile { get; }
        public string EntryRouteId { get; }
        internal RecognitionObservation Observation { get; }
    }
}
