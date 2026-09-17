using System;
using BotanicalGardenQR.Experience.Contracts;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    public readonly struct FlowIntent
    {
        FlowIntent(SessionToken session, FlowIntentKind kind, FeaturePageId feature)
        {
            if (!session.IsValid)
                throw new ArgumentException("Flow intents require a valid session.", nameof(session));
            Session = session;
            Kind = kind;
            Feature = feature;
        }

        public SessionToken Session { get; }
        public FlowIntentKind Kind { get; }
        public FeaturePageId Feature { get; }

        public static FlowIntent EnterFeature(SessionToken session, FeaturePageId feature)
        {
            if (!Enum.IsDefined(typeof(FeaturePageId), feature))
                throw new ArgumentOutOfRangeException(nameof(feature));
            return new FlowIntent(session, FlowIntentKind.EnterFeature, feature);
        }

        public static FlowIntent BackToMain(SessionToken session)
            => new FlowIntent(session, FlowIntentKind.BackToMain, default);

        public static FlowIntent Close(SessionToken session)
            => new FlowIntent(session, FlowIntentKind.Close, default);
    }

    public enum FlowIntentKind
    {
        EnterFeature = 1,
        BackToMain = 2,
        Close = 3
    }
}
