using BotanicalGardenQR.FrontendShell.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.VisitorCoach.Frontend
{
    public sealed class DialogueChoiceFeedback : MonoBehaviour, IFrontendGazeProgressPresenter
    {
        DialogueContractGraphic _graphic;
        Button _button;
        public void Initialize(DialogueContractGraphic graphic, Button button)
        { _graphic = graphic; _button = button; }
        public void PresentGazeProgress(float progress)
        { if (_graphic != null) _graphic.PresentGazeProgress(_button.interactable ? progress : 0f); }
        void OnDisable() { if (_graphic != null) _graphic.PresentGazeProgress(0f); }
    }
}
