namespace BotanicalGardenQR.Fairy.Contracts
{
    public enum FairyFailureCode
    {
        None, InvalidDefinition, UnsupportedResource, LoadFailed, StaleSession, InvalidIntent, NotOpen, InvalidSpeech,
        NotVisible, AudioUnavailable, SpeechCancelled
    }
    public readonly struct FairyResult
    {
        FairyResult(bool succeeded,FairyFailureCode failureCode,string diagnosticTag) { Succeeded=succeeded; FailureCode=failureCode; DiagnosticTag=diagnosticTag??string.Empty; }
        public bool Succeeded { get; } public FairyFailureCode FailureCode { get; } public string DiagnosticTag { get; }
        public static FairyResult Success()=>new FairyResult(true,FairyFailureCode.None,string.Empty);
        public static FairyResult Failure(FairyFailureCode code,string tag) { if(code==FairyFailureCode.None) throw new System.ArgumentOutOfRangeException(nameof(code)); return new FairyResult(false,code,tag); }
    }
}
