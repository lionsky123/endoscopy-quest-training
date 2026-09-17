using System;
using System.Collections.Generic;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;

namespace BotanicalGardenQR.Experience.Flow
{
    public sealed class FeaturePageRegistry
    {
        readonly Dictionary<FeaturePageId, IFeaturePageLifecycle> _pages;

        public FeaturePageRegistry(IEnumerable<IFeaturePageLifecycle> pages)
        {
            if (pages == null)
                throw new ArgumentNullException(nameof(pages));

            _pages = new Dictionary<FeaturePageId, IFeaturePageLifecycle>();
            foreach (var page in pages)
            {
                if (page == null)
                    throw new ArgumentException("Feature page registry must not contain null entries.", nameof(pages));
                if (!_pages.TryAdd(page.PageId, page))
                    throw new ArgumentException($"Feature page '{page.PageId}' is registered more than once.", nameof(pages));
            }
        }

        public bool TryGet(FeaturePageId pageId, out IFeaturePageLifecycle lifecycle)
            => _pages.TryGetValue(pageId, out lifecycle);
    }
}
