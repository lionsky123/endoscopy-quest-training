using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Effect.Contracts
{
    public enum EffectPhase { Closed, Loading, Ready, Active, Failed }
    public readonly struct EffectState
    {
        public EffectState(SessionToken session,long version,EffectPhase phase,UserFault fault=default) { if(version<0) throw new System.ArgumentOutOfRangeException(nameof(version)); Session=session; Version=version; Phase=phase; Fault=fault; }
        public SessionToken Session { get; } public long Version { get; } public EffectPhase Phase { get; } public UserFault Fault { get; }
    }
}
