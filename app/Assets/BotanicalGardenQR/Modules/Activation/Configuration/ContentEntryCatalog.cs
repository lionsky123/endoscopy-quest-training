using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [CreateAssetMenu(menuName = "Botanical Garden QR/Content Entry Catalog", fileName = "ContentEntryCatalog")]
    public sealed class ContentEntryCatalog : ScriptableObject
    {
        [SerializeField] ContentEntryRoute[] _routes = Array.Empty<ContentEntryRoute>();

        public IReadOnlyList<ContentEntryRoute> Routes => _routes;

        public bool TryResolveMapPoint(string pointId, out ContentEntryRoute route)
        {
            route = null;
            if (string.IsNullOrWhiteSpace(pointId)) return false;
            foreach (var candidate in _routes)
            {
                if (candidate == null || !candidate.Enabled ||
                    candidate.SerializedEntryKind != RecognitionSourceKinds.Fieldbook.Value ||
                    !string.Equals(candidate.MapPointId, pointId, StringComparison.Ordinal)) continue;
                // Ambiguous configuration must never silently select a different plant.
                if (route != null) { route = null; return false; }
                route = candidate;
            }
            return route != null;
        }

        public bool TryResolve(SourceKind kind, string sourceValue, out ContentEntryRoute route)
        {
            if (!kind.IsValid) throw new ArgumentException("A valid entry kind is required.", nameof(kind));
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                route = null;
                return false;
            }

            foreach (var candidate in _routes)
            {
                if (candidate != null && candidate.Enabled && candidate.EntryKind == kind &&
                    string.Equals(candidate.EntryValue, sourceValue, StringComparison.Ordinal))
                {
                    route = candidate;
                    return true;
                }
            }

            route = null;
            return false;
        }
    }
}
