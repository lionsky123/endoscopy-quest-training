namespace BotanicalGardenQR.Panorama.Contracts
{
    public enum PanoramaIntentKind { RotateByDegrees, ResetView }
    public readonly struct PanoramaIntent
    {
        public PanoramaIntent(PanoramaIntentKind kind, float degrees = 0) { Kind = kind; Degrees = degrees; }
        public PanoramaIntentKind Kind { get; } public float Degrees { get; }
    }
}
