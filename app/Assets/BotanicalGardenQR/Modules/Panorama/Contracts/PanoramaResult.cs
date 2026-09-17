namespace BotanicalGardenQR.Panorama.Contracts
{
    public enum PanoramaFailureCode { None, InvalidDefinition, SurfaceUnavailable, UnsupportedResource, LoadFailed, RenderFailed, StaleSession, InvalidIntent, NotOpen }
    public readonly struct PanoramaResult
    {
        PanoramaResult(bool succeeded, PanoramaFailureCode failureCode, string diagnosticTag) { Succeeded=succeeded; FailureCode=failureCode; DiagnosticTag=diagnosticTag??string.Empty; }
        public bool Succeeded { get; } public PanoramaFailureCode FailureCode { get; } public string DiagnosticTag { get; }
        public static PanoramaResult Success()=>new PanoramaResult(true,PanoramaFailureCode.None,string.Empty);
        public static PanoramaResult Failure(PanoramaFailureCode code,string tag) { if(code==PanoramaFailureCode.None) throw new System.ArgumentOutOfRangeException(nameof(code)); return new PanoramaResult(false,code,tag); }
    }
}
