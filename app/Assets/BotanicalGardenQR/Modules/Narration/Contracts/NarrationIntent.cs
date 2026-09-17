namespace BotanicalGardenQR.Narration.Contracts
{
    public enum NarrationIntentKind { TogglePlayback, Replay, ToggleMute }
    public readonly struct NarrationIntent
    {
        public NarrationIntent(NarrationIntentKind kind) { Kind=kind; }
        public NarrationIntentKind Kind { get; }
    }
}
