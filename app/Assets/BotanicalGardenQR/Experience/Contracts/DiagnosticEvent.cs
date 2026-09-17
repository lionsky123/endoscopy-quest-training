using System;

namespace BotanicalGardenQR.Experience.Contracts
{
    public sealed class DiagnosticEvent
    {
        public DiagnosticEvent(
            string code,
            string message,
            string owningLine,
            string stage,
            DateTimeOffset occurredAt,
            ActivationId activationId = default,
            SessionToken sessionToken = default,
            SceneId sceneId = default,
            string sourceKey = null,
            string assetPath = null)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("A diagnostic code is required.", nameof(code));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("A diagnostic message is required.", nameof(message));

            Code = code;
            Message = message;
            OwningLine = owningLine ?? string.Empty;
            Stage = stage ?? string.Empty;
            OccurredAt = occurredAt;
            ActivationId = activationId;
            SessionToken = sessionToken;
            SceneId = sceneId;
            SourceKey = sourceKey ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
        }

        public string Code { get; }
        public string Message { get; }
        public string OwningLine { get; }
        public string Stage { get; }
        public DateTimeOffset OccurredAt { get; }
        public ActivationId ActivationId { get; }
        public SessionToken SessionToken { get; }
        public SceneId SceneId { get; }
        public string SourceKey { get; }
        public string AssetPath { get; }
    }
}
