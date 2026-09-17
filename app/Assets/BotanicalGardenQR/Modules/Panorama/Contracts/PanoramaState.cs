using BotanicalGardenQR.Experience.Contracts;
namespace BotanicalGardenQR.Panorama.Contracts
{
    public enum PanoramaPhase { Closed, Loading, Ready, Active, Failed }
    public readonly struct PanoramaState
    {
        public PanoramaState(SessionToken session, long version, PanoramaPhase phase, float yawDegrees, UserFault fault = default)
        { if(version<0) throw new System.ArgumentOutOfRangeException(nameof(version)); Session = session; Version = version; Phase = phase; YawDegrees = yawDegrees; Fault = fault; }
        public SessionToken Session { get; } public long Version { get; } public PanoramaPhase Phase { get; } public float YawDegrees { get; } public UserFault Fault { get; }
    }
}
