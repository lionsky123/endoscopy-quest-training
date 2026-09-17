namespace BotanicalGardenQR.Model.Contracts
{
    public enum ModelIntentKind { ToggleAutoMotion, ResetView, PlayAnimation }

    public readonly struct ModelIntent
    {
        public ModelIntent(ModelIntentKind kind) { Kind = kind; }
        public ModelIntentKind Kind { get; }
    }
}
