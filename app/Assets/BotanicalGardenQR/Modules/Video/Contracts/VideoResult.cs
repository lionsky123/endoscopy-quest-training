namespace BotanicalGardenQR.Video.Contracts
{
    public enum VideoFailureCode { None, InvalidDefinition, SurfaceUnavailable, UnsupportedResource, LoadFailed, PlaybackFailed, StaleSession, InvalidIntent, NotOpen }
    public readonly struct VideoResult
    {
        VideoResult(bool succeeded, VideoFailureCode failureCode, string diagnosticTag) { Succeeded = succeeded; FailureCode = failureCode; DiagnosticTag = diagnosticTag ?? string.Empty; }
        public bool Succeeded { get; }
        public VideoFailureCode FailureCode { get; }
        public string DiagnosticTag { get; }
        public static VideoResult Success() => new VideoResult(true, VideoFailureCode.None, string.Empty);
        public static VideoResult Failure(VideoFailureCode code, string diagnosticTag) { if(code==VideoFailureCode.None) throw new System.ArgumentOutOfRangeException(nameof(code)); return new VideoResult(false, code, diagnosticTag); }
    }
}
