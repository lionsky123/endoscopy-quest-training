using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Configuration.Runtime;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    internal static class RuntimeBindingValidator
    {
        public static IReadOnlyList<IRecognitionSource> RecognitionSources(
            MonoBehaviour[] adapters,
            IReadOnlyList<RecognitionAdapterOption> configuredAdapters, bool confirmedFieldbookEntry = false)
        {
            if (configuredAdapters == null)
                throw new InvalidOperationException("VisitorInstaller requires recognition adapter configuration.");

            var enabledKinds = new HashSet<SourceKind>();
            foreach (var option in configuredAdapters)
            {
                if (option == null)
                    throw new InvalidOperationException("RuntimeEnvironmentOptions has an empty recognition adapter entry.");
                if (option.Enabled && !enabledKinds.Add(option.SourceKind))
                    throw new InvalidOperationException(
                        $"RuntimeEnvironmentOptions duplicates enabled SourceKind '{option.SerializedSourceKind}'.");
            }
            if (confirmedFieldbookEntry)
            {
                if (enabledKinds.Count != 0) throw new InvalidOperationException("Fieldbook test entry and live recognition adapters are mutually exclusive.");
                return Array.Empty<IRecognitionSource>();
            }
            if (enabledKinds.Count == 0)
                throw new InvalidOperationException("RuntimeEnvironmentOptions requires at least one enabled recognition adapter.");

            if (adapters == null || adapters.Length == 0)
                throw new InvalidOperationException("VisitorInstaller requires at least one recognition source adapter.");

            var sources = new List<IRecognitionSource>(adapters.Length);
            var kinds = new HashSet<SourceKind>();
            foreach (var adapter in adapters)
            {
                if (adapter == null)
                    throw new InvalidOperationException("VisitorInstaller has an empty recognition source adapter reference.");
                if (!(adapter is IRecognitionSourceProvider provider))
                    throw new InvalidOperationException($"Recognition adapter '{adapter.name}' must implement IRecognitionSourceProvider.");
                var source = provider.CreateSource();
                if (source == null)
                    throw new InvalidOperationException($"Recognition adapter '{adapter.name}' returned no source.");
                if (!source.Kind.IsValid || !kinds.Add(source.Kind))
                    throw new InvalidOperationException($"Recognition adapter '{adapter.name}' has an invalid or duplicate SourceKind.");
                if (!enabledKinds.Contains(source.Kind))
                    throw new InvalidOperationException(
                        $"Recognition adapter '{adapter.name}' produced SourceKind '{source.Kind}', which is not enabled in RuntimeEnvironmentOptions.");
                sources.Add(source);
            }

            if (!kinds.SetEquals(enabledKinds))
            {
                var missing = new List<string>();
                foreach (var kind in enabledKinds)
                    if (!kinds.Contains(kind)) missing.Add(kind.Value);
                missing.Sort(StringComparer.Ordinal);
                throw new InvalidOperationException(
                    $"VisitorInstaller is missing recognition source adapter(s) for enabled SourceKind: {string.Join(", ", missing)}.");
            }

            return sources;
        }

        public static void Required(UnityEngine.Object value, string fieldName)
        {
            if (value == null)
                throw new InvalidOperationException($"VisitorInstaller requires '{fieldName}'.");
        }
    }
}
