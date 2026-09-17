using System;
using UnityEngine;

namespace BotanicalGardenQR.Activation.Runtime
{
    public sealed class ActivationCoordinatorDriver : MonoBehaviour
    {
        ActivationCoordinator _coordinator;

        public void Configure(ActivationCoordinator coordinator)
            => _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

        public void Unconfigure(ActivationCoordinator coordinator)
        {
            if (ReferenceEquals(_coordinator, coordinator)) _coordinator = null;
        }

        void Update() => _coordinator?.Tick(DateTimeOffset.UtcNow);
        void OnDestroy() => _coordinator = null;
    }
}
