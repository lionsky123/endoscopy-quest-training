using System;
using System.Collections.Generic;
using System.Diagnostics;
using BotanicalGardenQR.Activation.Contracts;

namespace BotanicalGardenQR.Activation.Runtime
{
    public sealed class RecognitionSourceRegistry : IDisposable
    {
        readonly List<IRecognitionSource> _sources;
        readonly List<IDisposable> _registrations = new List<IDisposable>();
        bool _started;

        public RecognitionSourceRegistry(IEnumerable<IRecognitionSource> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            _sources = new List<IRecognitionSource>();
            var kinds = new HashSet<SourceKind>();
            foreach (var source in sources)
            {
                if (source == null) throw new ArgumentException("Recognition sources must not contain null.", nameof(sources));
                if (!source.Kind.IsValid || !kinds.Add(source.Kind))
                    throw new ArgumentException("Recognition source kinds must be valid and unique.", nameof(sources));
                _sources.Add(source);
            }
        }

        public void Start(IRecognitionObservationSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (_started) throw new InvalidOperationException("Recognition sources are already started.");
            _started = true;
            try
            {
                foreach (var source in _sources)
                    _registrations.Add(source.Start(sink) ?? throw new InvalidOperationException($"Recognition source '{source.Kind}' returned no registration."));
            }
            catch
            {
                DisposeRegistrations();
                _started = false;
                throw;
            }
        }


        public bool TryGetLiveObservation(SourceKind kind, string sourceValue, out RecognitionObservation observation)
        {
            foreach (var source in _sources)
                if (source.Kind == kind && source is ILiveRecognitionEvidenceProvider provider &&
                    provider.TryGetLiveObservation(kind, sourceValue, out observation))
                    return true;
            observation = default;
            return false;
        }

        public bool TryRearm(RecognitionObservation observation)
        {
            if (observation.ObservationId == Guid.Empty) throw new ArgumentException("A constructed observation is required.", nameof(observation));
            foreach (var source in _sources)
                if (source.Kind == observation.SourceKind && source is IRecognitionRoundRearm rearm &&
                    rearm.TryRearm(observation))
                    return true;
            return false;
        }

        public void Dispose() { DisposeRegistrations(); _started = false; }
        void DisposeRegistrations()
        {
            for (var index = _registrations.Count - 1; index >= 0; index--)
            {
                try
                {
                    _registrations[index].Dispose();
                }
                catch (Exception exception)
                {
                    Trace.TraceError(
                        $"Recognition source registration cleanup failed at index {index}: {exception}");
                }
            }
            _registrations.Clear();
        }
    }
}
