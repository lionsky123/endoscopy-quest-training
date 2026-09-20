using UnityEngine;

namespace BotanicalGardenQR.Fairy.Contracts
{
    /// <summary>The room owns every permitted guide position and connecting path.</summary>
    public interface IFairyWalkSpace
    {
        Vector3 StartPosition { get; }
        Vector3 ArrivalPoint(float progress);
        Vector3 Project(Vector3 desired);
        Vector3 NextWaypoint(Vector3 current, Vector3 desired);
        bool CanStep(Vector3 from, Vector3 to);
    }
}
