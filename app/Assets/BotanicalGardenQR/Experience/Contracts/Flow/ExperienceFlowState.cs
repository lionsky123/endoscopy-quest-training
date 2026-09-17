using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BotanicalGardenQR.Experience.Contracts.Flow
{
    public sealed class ExperienceFlowState
    {
        readonly ReadOnlyCollection<FeaturePageId> _availablePages;

        public ExperienceFlowState(
            SessionToken session,
            long version,
            SceneId sceneId,
            string title,
            string subtitle,
            string summary,
            FlowPage page,
            IReadOnlyList<FeaturePageId> availablePages,
            string message = null,
            UserFault fault = null)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));
            if (availablePages == null)
                throw new ArgumentNullException(nameof(availablePages));

            var pages = new FeaturePageId[availablePages.Count];
            for (var index = 0; index < pages.Length; index++)
                pages[index] = availablePages[index];

            Session = session;
            Version = version;
            SceneId = sceneId;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Summary = summary ?? string.Empty;
            Page = page;
            _availablePages = Array.AsReadOnly(pages);
            Message = message ?? string.Empty;
            Fault = fault;
        }

        public SessionToken Session { get; }
        public long Version { get; }
        public SceneId SceneId { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string Summary { get; }
        public FlowPage Page { get; }
        public IReadOnlyList<FeaturePageId> AvailablePages => _availablePages;
        public string Message { get; }
        public UserFault Fault { get; }
    }

    public readonly struct FlowPage : IEquatable<FlowPage>
    {
        FlowPage(FlowPageKind kind, FeaturePageId feature)
        {
            Kind = kind;
            Feature = feature;
        }

        public FlowPageKind Kind { get; }
        public FeaturePageId Feature { get; }
        public static FlowPage Closed => new FlowPage(FlowPageKind.Closed, default);
        public static FlowPage Main => new FlowPage(FlowPageKind.Main, default);
        public static FlowPage ForFeature(FeaturePageId feature)
        {
            if (!Enum.IsDefined(typeof(FeaturePageId), feature))
                throw new ArgumentOutOfRangeException(nameof(feature));
            return new FlowPage(FlowPageKind.Feature, feature);
        }

        public bool Equals(FlowPage other) => Kind == other.Kind && Feature == other.Feature;
        public override bool Equals(object obj) => obj is FlowPage other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ (int)Feature;
        public static bool operator ==(FlowPage left, FlowPage right) => left.Equals(right);
        public static bool operator !=(FlowPage left, FlowPage right) => !left.Equals(right);
    }

    public enum FlowPageKind
    {
        Closed = 0,
        Main = 1,
        Feature = 2
    }
}
