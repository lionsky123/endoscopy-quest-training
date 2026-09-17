using System;

namespace BotanicalGardenQR.Activation.Contracts
{
    public readonly struct RecognitionObservation
    {
        public RecognitionObservation(
            Guid observationId,
            long revision,
            DateTimeOffset observedAt,
            SourceKind sourceKind,
            string sourceValue,
            TrackingState trackingState,
            SpatialEvidence? spatialEvidence = null,
            float confirmationProgress = 1f,
            bool isConfirmation = true)
        {
            if (observationId == Guid.Empty)
                throw new ArgumentException("ObservationId must not be empty.", nameof(observationId));
            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));
            if (!sourceKind.IsValid)
                throw new ArgumentException("A valid SourceKind is required.", nameof(sourceKind));
            if (string.IsNullOrWhiteSpace(sourceValue) || !string.Equals(sourceValue, sourceValue.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("A non-empty canonical source value is required.", nameof(sourceValue));
            if (!Enum.IsDefined(typeof(TrackingState), trackingState))
                throw new ArgumentOutOfRangeException(nameof(trackingState));
            if (float.IsNaN(confirmationProgress) || float.IsInfinity(confirmationProgress) ||
                confirmationProgress < 0f || confirmationProgress > 1f)
                throw new ArgumentOutOfRangeException(nameof(confirmationProgress));

            ObservationId = observationId;
            Revision = revision;
            ObservedAt = observedAt;
            SourceKind = sourceKind;
            SourceValue = sourceValue;
            TrackingState = trackingState;
            SpatialEvidence = spatialEvidence;
            ConfirmationProgress = confirmationProgress;
            IsConfirmation = isConfirmation && trackingState != TrackingState.Lost;
        }

        public Guid ObservationId { get; }
        public long Revision { get; }
        public DateTimeOffset ObservedAt { get; }
        public SourceKind SourceKind { get; }
        public string SourceValue { get; }
        public TrackingState TrackingState { get; }
        public SpatialEvidence? SpatialEvidence { get; }
        public float ConfirmationProgress { get; }
        public bool IsConfirmation { get; }
    }
}
