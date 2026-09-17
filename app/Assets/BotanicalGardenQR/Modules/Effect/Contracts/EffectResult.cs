namespace BotanicalGardenQR.Effect.Contracts
{
    public enum EffectFailureCode { None, InvalidDefinition, UnsupportedResource, LoadFailed, StaleSession, InvalidIntent, NotOpen }
    public readonly struct EffectResult
    {
        EffectResult(bool succeeded,EffectFailureCode failureCode,string diagnosticTag) { Succeeded=succeeded; FailureCode=failureCode; DiagnosticTag=diagnosticTag??string.Empty; }
        public bool Succeeded { get; } public EffectFailureCode FailureCode { get; } public string DiagnosticTag { get; }
        public static EffectResult Success()=>new EffectResult(true,EffectFailureCode.None,string.Empty);
        public static EffectResult Failure(EffectFailureCode code,string tag) { if(code==EffectFailureCode.None) throw new System.ArgumentOutOfRangeException(nameof(code)); return new EffectResult(false,code,tag); }
    }
}
