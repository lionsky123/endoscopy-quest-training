namespace BotanicalGardenQR.Experience.Application
{
    /// <summary>Module facts adapted at composition; the application decides their guidance effect.</summary>
    public readonly struct VisitorGuidanceEnvironment
    {
        public VisitorGuidanceEnvironment(bool contentOpen, bool collectionVisible, bool rewardPending, bool otherDialogue, bool completionVisible, bool closeDecisionVisible)
        {
            ContentOpen = contentOpen;
            CollectionVisible = collectionVisible;
            RewardPending = rewardPending;
            OtherDialogue = otherDialogue;
            CompletionVisible = completionVisible;
            CloseDecisionVisible = closeDecisionVisible;
        }

        public bool ContentOpen { get; }
        public bool CollectionVisible { get; }
        public bool RewardPending { get; }
        public bool OtherDialogue { get; }
        public bool CompletionVisible { get; }
        public bool CloseDecisionVisible { get; }
    }
}
