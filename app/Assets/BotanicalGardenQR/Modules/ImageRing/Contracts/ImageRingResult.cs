namespace BotanicalGardenQR.ImageRing.Contracts
{
    public enum ImageRingFailureCode
    {
        None,
        InvalidSession,
        InvalidDefinition,
        AlreadyOpen,
        StaleSession,
        RuntimeFailed
    }

    public readonly struct ImageRingResult
    {
        ImageRingResult(bool succeeded, ImageRingFailureCode failureCode, string diagnosticTag)
        {
            Succeeded = succeeded;
            FailureCode = failureCode;
            DiagnosticTag = diagnosticTag ?? string.Empty;
        }

        public bool Succeeded { get; }
        public ImageRingFailureCode FailureCode { get; }
        public string DiagnosticTag { get; }

        public static ImageRingResult Success()
            => new ImageRingResult(true, ImageRingFailureCode.None, string.Empty);

        public static ImageRingResult Failure(ImageRingFailureCode code, string tag)
        {
            if (code == ImageRingFailureCode.None)
                throw new System.ArgumentOutOfRangeException(nameof(code));
            return new ImageRingResult(false, code, tag);
        }
    }
}
