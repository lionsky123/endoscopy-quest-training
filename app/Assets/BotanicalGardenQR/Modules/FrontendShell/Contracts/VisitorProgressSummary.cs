using System;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    /// <summary>
    /// UI-neutral projection of the two existing progress authorities. It is
    /// presentation state only and never owns or persists progress.
    /// </summary>
    public sealed class VisitorProgressSummary
    {
        public VisitorProgressSummary(
            int mainCompleted,
            int mainTotal,
            int collectionCompleted,
            int collectionTotal)
        {
            MainCompleted = RequireCount(mainCompleted, mainTotal, nameof(mainCompleted));
            MainTotal = RequireTotal(mainTotal, nameof(mainTotal));
            CollectionCompleted = RequireCount(
                collectionCompleted,
                collectionTotal,
                nameof(collectionCompleted));
            CollectionTotal = RequireTotal(collectionTotal, nameof(collectionTotal));
        }

        public int MainCompleted { get; }
        public int MainTotal { get; }
        public int CollectionCompleted { get; }
        public int CollectionTotal { get; }
        public bool HasProgress => MainTotal > 0 || CollectionTotal > 0;
        public bool IsMainComplete => MainTotal > 0 && MainCompleted >= MainTotal;
        public bool IsCollectionComplete =>
            CollectionTotal > 0 && CollectionCompleted >= CollectionTotal;
        public bool IsAllComplete => IsMainComplete && IsCollectionComplete;

        static int RequireCount(int value, int total, string parameterName)
        {
            if (value < 0 || total < 0 || value > total)
                throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }

        static int RequireTotal(int value, string parameterName)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(parameterName);
            return value;
        }
    }

    public interface IVisitorProgressSummaryPresenter
    {
        void SetVisitorProgressSummary(VisitorProgressSummary summary);
    }
}
