using System;
using BotanicalGardenQR.Activation.Contracts;

namespace BotanicalGardenQR.Activation.Runtime
{
    internal sealed class RecallPolicyEvaluator
    {
        readonly RecognitionSourceRegistry _sources;
        public RecallPolicyEvaluator(RecognitionSourceRegistry sources) => _sources = sources ?? throw new ArgumentNullException(nameof(sources));

        public RecallDecision Evaluate(ResolvedActivation last)
        {
            if (last == null || !last.DisplayProfile.Recall.AllowRecall)
                return RecallDecision.Reject(RecallFailure.Unavailable);
            if (!last.DisplayProfile.Recall.RequireLiveSpatialEvidence)
                return RecallDecision.Allow(last);
            if (!_sources.TryGetLiveObservation(last.Observation.SourceKind, last.Observation.SourceValue, out var live) ||
                !live.SpatialEvidence.HasValue || !live.SpatialEvidence.Value.PoseIsValid || live.TrackingState == TrackingState.Lost)
                return RecallDecision.Reject(RecallFailure.EvidenceRequired);
            return RecallDecision.Allow(new ResolvedActivation(
                last.SceneId,
                last.DisplayProfile,
                live,
                last.EntryRouteId));
        }
    }

    internal readonly struct RecallDecision
    {
        RecallDecision(ResolvedActivation activation, RecallFailure failure) { Activation = activation; Failure = failure; }
        public bool Allowed => Activation != null;
        public ResolvedActivation Activation { get; }
        public RecallFailure Failure { get; }
        public static RecallDecision Allow(ResolvedActivation activation) => new RecallDecision(activation ?? throw new ArgumentNullException(nameof(activation)), RecallFailure.None);
        public static RecallDecision Reject(RecallFailure failure) => new RecallDecision(null, failure);
    }
}
