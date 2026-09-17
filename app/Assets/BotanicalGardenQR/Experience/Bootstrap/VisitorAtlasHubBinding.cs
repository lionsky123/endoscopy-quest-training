using System;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;

namespace BotanicalGardenQR.Bootstrap
{
    /// <summary>Applies the sole modal owner's derived Hub gate; never reconstructs application facts.</summary>
    internal sealed class VisitorAtlasHubBinding : IDisposable
    {
        readonly IVisitorAtlasHubController _hub;
        readonly VisitorModalCoordinator _modal;
        bool _disposed;

        public VisitorAtlasHubBinding(IVisitorAtlasHubController hub, VisitorModalCoordinator modal)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _modal = modal ?? throw new ArgumentNullException(nameof(modal));
            _modal.PolicyChanged += Apply;
            try { Apply(_modal.Current); }
            catch { _modal.PolicyChanged -= Apply; throw; }
        }

        void Apply(VisitorModalPolicy policy)
        {
            if (!_disposed) _hub.SetInteractionGate(policy.HubInteractionAllowed);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _modal.PolicyChanged -= Apply;
            _hub.SetInteractionGate(false);
        }
    }
}
