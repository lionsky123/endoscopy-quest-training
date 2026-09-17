namespace BotanicalGardenQR.Video.Contracts
{
    public enum VideoIntentKind { TogglePlayback, Replay, ToggleMute }
    public readonly struct VideoIntent
    {
        public VideoIntent(VideoIntentKind kind) { Kind = kind; }
        public VideoIntentKind Kind { get; }
    }
}
