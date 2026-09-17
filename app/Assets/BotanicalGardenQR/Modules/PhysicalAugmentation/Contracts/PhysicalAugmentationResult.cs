using System;

namespace BotanicalGardenQR.PhysicalAugmentation.Contracts
{
    public enum PhysicalAugmentationFailureCode
    {
        None,
        AlreadyStarted,
        NotStarted,
        InvalidDefinition,
        UnconfiguredPoint,
        PointNotStable,
        Suppressed,
        StaleGeneration,
        CapabilityUnavailable,
        PreparationFailed,
        RuntimeFailed
    }

    public readonly struct PhysicalAugmentationResult
    {
        PhysicalAugmentationResult(
            bool succeeded,
            PhysicalAugmentationFailureCode failureCode,
            string diagnosticTag,
            long generation)
        {
            Succeeded = succeeded;
            FailureCode = failureCode;
            DiagnosticTag = diagnosticTag ?? string.Empty;
            Generation = generation;
        }

        public bool Succeeded { get; }
        public PhysicalAugmentationFailureCode FailureCode { get; }
        public string DiagnosticTag { get; }
        public long Generation { get; }

        public static PhysicalAugmentationResult Success(long generation)
        {
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            return new PhysicalAugmentationResult(true, PhysicalAugmentationFailureCode.None, string.Empty, generation);
        }

        public static PhysicalAugmentationResult Failure(
            PhysicalAugmentationFailureCode code,
            string diagnosticTag,
            long generation)
        {
            if (code == PhysicalAugmentationFailureCode.None) throw new ArgumentOutOfRangeException(nameof(code));
            if (generation < 0) throw new ArgumentOutOfRangeException(nameof(generation));
            return new PhysicalAugmentationResult(false, code, diagnosticTag, generation);
        }
    }
}
