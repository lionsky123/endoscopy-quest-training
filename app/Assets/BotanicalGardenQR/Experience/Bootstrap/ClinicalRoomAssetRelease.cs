using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    internal interface IClinicalRoomAssetRelease
    {
        void Begin();
        bool IsComplete { get; }
    }

    // Called after the old visit is disposed and its deferred Unity destruction has had a frame.
    // No new room may be loaded until this operation finishes. Shared live references remain valid.
    internal sealed class ClinicalRoomAssetRelease : IClinicalRoomAssetRelease
    {
        AsyncOperation _operation;
        public void Begin() { _operation = Resources.UnloadUnusedAssets(); }
        public bool IsComplete => _operation == null || _operation.isDone;
    }
}
