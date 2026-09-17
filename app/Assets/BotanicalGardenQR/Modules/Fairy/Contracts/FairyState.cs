using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Fairy.Contracts
{
    public enum FairyPhase { Closed, Loading, Active, Hidden, Failed, Arriving }
    public readonly struct FairyState
    {
        public FairyState(SessionToken session,long version,FairyPhase phase,UserFault fault=default) { if(version<0) throw new System.ArgumentOutOfRangeException(nameof(version)); Session=session; Version=version; Phase=phase; Fault=fault; }
        public SessionToken Session { get; } public long Version { get; } public FairyPhase Phase { get; } public UserFault Fault { get; }
    }
}
