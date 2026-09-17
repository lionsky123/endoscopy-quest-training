using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Configuration.Runtime;

namespace BotanicalGardenQR.Activation.Runtime
{
    public sealed class RouteResolver
    {
        readonly ContentEntryCatalog _catalog;
        public RouteResolver(ContentEntryCatalog catalog) => _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));

        public bool TryResolve(RecognitionObservation observation, out ResolvedActivation activation)
        {
            if (observation.ObservationId == Guid.Empty) throw new ArgumentException("A constructed observation is required.", nameof(observation));
            if (_catalog.TryResolve(observation.SourceKind, observation.SourceValue, out var route) &&
                route.DisplayProfile != null)
            {
                activation = new ResolvedActivation(
                    route.TargetSceneId,
                    route.DisplayProfile,
                    observation,
                    route.EntryRouteId);
                return true;
            }
            activation = null;
            return false;
        }
    }
}
