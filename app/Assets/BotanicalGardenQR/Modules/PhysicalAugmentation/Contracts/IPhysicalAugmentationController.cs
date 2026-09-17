using System;

namespace BotanicalGardenQR.PhysicalAugmentation.Contracts
{
    public interface IPhysicalAugmentationController
    {
        PhysicalAugmentationResult Start();
        PhysicalAugmentationResult RetryLocalization();
        PhysicalAugmentationResult Activate(PhysicalAugmentationPointId pointId, long generation);
        PhysicalAugmentationResult StopPerformances();
        PhysicalAugmentationResult Stop();
        IDisposable Observe(IPhysicalAugmentationStateSink sink);
    }

    public interface IPhysicalAugmentationStateSink
    {
        void OnPhysicalAugmentationStateChanged(PhysicalAugmentationState state);
    }

    public interface IPhysicalAugmentationIntentSink
    {
        void Submit(PhysicalAugmentationIntent intent);
    }
}
