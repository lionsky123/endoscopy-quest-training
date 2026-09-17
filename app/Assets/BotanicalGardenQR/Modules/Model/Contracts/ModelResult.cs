namespace BotanicalGardenQR.Model.Contracts
{
    public enum ModelFailureCode { None, InvalidDefinition, SurfaceUnavailable, UnsupportedSource, UnsupportedAnimation, LoadFailed, StaleSession, InvalidIntent, NotOpen }
    public readonly struct ModelResult
    {
        ModelResult(bool succeeded,ModelFailureCode failureCode,string diagnosticTag) { Succeeded=succeeded; FailureCode=failureCode; DiagnosticTag=diagnosticTag??string.Empty; }
        public bool Succeeded { get; } public ModelFailureCode FailureCode { get; } public string DiagnosticTag { get; }
        public static ModelResult Success()=>new ModelResult(true,ModelFailureCode.None,string.Empty);
        public static ModelResult Failure(ModelFailureCode code,string tag) { if(code==ModelFailureCode.None) throw new System.ArgumentOutOfRangeException(nameof(code)); return new ModelResult(false,code,tag); }
    }
}
