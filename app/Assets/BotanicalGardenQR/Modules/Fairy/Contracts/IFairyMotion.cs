using UnityEngine;

namespace BotanicalGardenQR.Fairy.Contracts
{
    /// <summary>Resolved, request-scoped motion. No route, point, content or source identity crosses this seam.</summary>
    public interface IFairyRecovery
    {
        bool TryRecallMotion(Vector3 position);
    }

    public interface IFairyMotion
    {
        bool TryGetMotionPosition(out Vector3 position);
        bool ApplyMotion(long requestId, Vector3 position, Vector3 forward, bool moving, float entryRadius, float speed);
        void HoldMotion(long requestId);
        void ReleaseMotion(long requestId);
    }
}
