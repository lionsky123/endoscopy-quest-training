using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BotanicalGardenQR.Experience.Contracts.Flow
{
    public interface ISceneFlowDescriptorSource
    {
        bool TryGet(SceneId sceneId, out SceneFlowDescriptor descriptor);
    }

    public sealed class SceneFlowDescriptor
    {
        readonly ReadOnlyCollection<FeaturePageId> _availablePages;

        public SceneFlowDescriptor(
            SceneId sceneId,
            string title,
            string subtitle,
            string summary,
            IReadOnlyList<FeaturePageId> availablePages)
        {
            if (!sceneId.IsValid)
                throw new ArgumentException("A valid SceneId is required.", nameof(sceneId));
            if (availablePages == null)
                throw new ArgumentNullException(nameof(availablePages));

            var pages = new FeaturePageId[availablePages.Count];
            for (var index = 0; index < pages.Length; index++)
            {
                var page = availablePages[index];
                if (!Enum.IsDefined(typeof(FeaturePageId), page))
                    throw new ArgumentOutOfRangeException(nameof(availablePages));
                pages[index] = page;
            }

            SceneId = sceneId;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Summary = summary ?? string.Empty;
            _availablePages = Array.AsReadOnly(pages);
        }

        public SceneId SceneId { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public string Summary { get; }
        public IReadOnlyList<FeaturePageId> AvailablePages => _availablePages;
    }
}
