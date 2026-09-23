using UnityEditor;
using UnityEngine.SceneManagement;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // A dedicated Scene view keeps the current room separate from any open legacy scene.
    public sealed class InspectionSceneView : SceneView
    {
        public void ShowRoom(Scene room) => customScene=room;
    }
}
