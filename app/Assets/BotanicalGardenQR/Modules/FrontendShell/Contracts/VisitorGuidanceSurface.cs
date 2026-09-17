namespace BotanicalGardenQR.FrontendShell.Contracts
{
    public enum VisitorGuidanceSurfaceKind
    {
        Hidden,
        Departure,
        Unavailable,
        Discovery,
        DiscoveryFailed
    }

    /// <summary>Semantic guidance state; copy, placement and controls belong to presentation.</summary>
    public readonly struct VisitorGuidanceSurface
    {
        public VisitorGuidanceSurface(VisitorGuidanceSurfaceKind kind, string targetTitle)
        {
            Kind = kind;
            TargetTitle = targetTitle ?? string.Empty;
        }

        public VisitorGuidanceSurfaceKind Kind { get; }
        public string TargetTitle { get; }
    }
}
