using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.Fairy.Backend;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed class PanoramaFairyVisibilityBinding : IFlowStateSink, IDisposable
    {
        readonly FairyCompanionBinding _fairy;
        IDisposable _subscription;
        bool _suppressed;
        bool _disposed;

        internal PanoramaFairyVisibilityBinding(
            IExperienceFlow flow,
            FairyCompanionBinding fairy)
        {
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            _fairy = fairy ?? throw new ArgumentNullException(nameof(fairy));
            _subscription = flow.Observe(this);
        }

        public void OnStateChanged(ExperienceFlowState state)
        {
            if (_disposed || state == null) return;

            var shouldSuppress = state.Page.Kind == FlowPageKind.Feature &&
                                 state.Page.Feature == FeaturePageId.Panorama;
            if (_suppressed == shouldSuppress) return;

            var result = _fairy.SetPresentationSuppressed(shouldSuppress);
            if (result.Succeeded)
            {
                _suppressed = shouldSuppress;
                return;
            }

            Debug.LogWarning(
                $"Fairy visibility could not follow Panorama state: " +
                $"{result.FailureCode} ({result.DiagnosticTag}).");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _subscription?.Dispose();
            _subscription = null;
        }
    }
}
