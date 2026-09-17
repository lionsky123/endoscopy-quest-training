using System;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;

namespace BotanicalGardenQR.Experience.Application
{
    public readonly struct VisitorModalFacts
    {
        public VisitorModalFacts(long revision, bool contentClosed, bool explorationReady,
            bool prologueVisible = false, bool completionVisible = false, bool closeDecisionVisible = false,
            VisitorDialogueOwner? dialogueOwner = null,
            CollectionPresentationSurfaceKind collectionSurface = CollectionPresentationSurfaceKind.Hidden,
            bool toolPreparationExited = true, bool guidanceBlocksFirstScan = false)
        {
            Revision = revision;
            ContentClosed = contentClosed;
            ExplorationReady = explorationReady;
            PrologueVisible = prologueVisible;
            CompletionVisible = completionVisible;
            CloseDecisionVisible = closeDecisionVisible;
            DialogueOwner = dialogueOwner;
            CollectionSurface = collectionSurface;
            ToolPreparationExited = toolPreparationExited;
            GuidanceBlocksFirstScan = guidanceBlocksFirstScan;
        }
        public long Revision { get; }
        public bool ContentClosed { get; }
        public bool ExplorationReady { get; }
        public bool ToolPreparationExited { get; }
        public bool GuidanceBlocksFirstScan { get; }
        public bool PrologueVisible { get; }
        public bool CompletionVisible { get; }
        public bool CloseDecisionVisible { get; }
        public VisitorDialogueOwner? DialogueOwner { get; }
        public CollectionPresentationSurfaceKind CollectionSurface { get; }
    }

    public readonly struct VisitorModalPolicy
    {
        public VisitorModalPolicy(bool shellSuppressed, bool coachCuesSuppressed, bool surfaceSuppressed,
            bool browseOpeningAllowed, bool recognitionAllowed, bool hubInteractionAllowed, bool closeBrowse)
        {
            ShellSuppressed = shellSuppressed;
            CoachCuesSuppressed = coachCuesSuppressed;
            SurfaceSuppressed = surfaceSuppressed;
            BrowseOpeningAllowed = browseOpeningAllowed;
            RecognitionAllowed = recognitionAllowed;
            HubInteractionAllowed = hubInteractionAllowed;
            CloseBrowse = closeBrowse;
        }
        public bool ShellSuppressed { get; }
        public bool CoachCuesSuppressed { get; }
        public bool SurfaceSuppressed { get; }
        public bool BrowseOpeningAllowed { get; }
        public bool RecognitionAllowed { get; }
        public bool HubInteractionAllowed { get; }
        public bool CloseBrowse { get; }
        public static VisitorModalPolicy Inactive => new VisitorModalPolicy(false, false, false, false, false, false, false);
    }

    /// <summary>One snapshot/command boundary for application modal coordination.</summary>
    public interface IVisitorModalEnvironment
    {
        VisitorModalFacts Current { get; }
        IDisposable Observe(Action changed);
        void Apply(VisitorModalPolicy policy, long expectedRevision);
    }
}
