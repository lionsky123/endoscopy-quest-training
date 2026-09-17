using System;
using System.Collections.Generic;
using System.Threading;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.SpatialHost.Contracts;

namespace BotanicalGardenQR.Activation.Runtime
{
    public static class ActivationModuleFactory
    {
        public static ActivationCoordinator CreateOnCurrentThread(ContentEntryCatalog routes,
            ISpatialDisplayHost host, IExperienceFlow flow, IEnumerable<IRecognitionSource> sources,
            RuntimeEnvironmentOptions options, IRecognitionFocusQueryProvider focusQueryProvider,
            JourneySessionId journeySession,
            Action<DiagnosticEvent> diagnostics = null)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            var enabledKinds = new HashSet<SourceKind>();
            foreach (var option in options.RecognitionAdapters)
                if (option != null && option.Enabled) enabledKinds.Add(option.SourceKind);
            var enabledSources = new List<IRecognitionSource>();
            foreach (var source in sources)
            {
                if (source == null || !enabledKinds.Contains(source.Kind)) continue;
                if (source is IRecognitionConfirmationTiming timing)
                    timing.ConfigureConfirmationTiming(
                        options.ScanConfirmationSeconds,
                        options.ScanLostGraceSeconds,
                        options.ScanGazeLostGraceSeconds);
                if (source is IRecognitionFocusQueryConsumer focusConsumer)
                {
                    if (focusQueryProvider == null)
                        throw new InvalidOperationException($"Recognition source '{source.Kind}' requires an explicit focus query provider.");
                    focusConsumer.ConfigureFocusQueryProvider(focusQueryProvider);
                }
                enabledSources.Add(source);
            }
            return new ActivationCoordinator(new RouteResolver(routes), host, flow,
                new RecognitionSourceRegistry(enabledSources), TimeSpan.FromSeconds(options.RecognitionDebounceSeconds),
                Thread.CurrentThread.ManagedThreadId, journeySession, diagnostics);
        }
    }
}
