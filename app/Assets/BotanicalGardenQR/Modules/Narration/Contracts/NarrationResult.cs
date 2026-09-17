namespace BotanicalGardenQR.Narration.Contracts
{
    public enum NarrationFailureCode { None, InvalidDefinition, SurfaceUnavailable, UnsupportedResource, LoadFailed, PlaybackFailed, StaleSession, InvalidIntent, NotOpen }
    public readonly struct NarrationResult
    {
        NarrationResult(bool succeeded,NarrationFailureCode failureCode,string diagnosticTag) { Succeeded=succeeded; FailureCode=failureCode; DiagnosticTag=diagnosticTag??string.Empty; }
        public bool Succeeded { get; } public NarrationFailureCode FailureCode { get; } public string DiagnosticTag { get; }
        public static NarrationResult Success()=>new NarrationResult(true,NarrationFailureCode.None,string.Empty);
        public static NarrationResult Failure(NarrationFailureCode code,string tag) { if(code==NarrationFailureCode.None) throw new System.ArgumentOutOfRangeException(nameof(code)); return new NarrationResult(false,code,tag); }
    }
}
