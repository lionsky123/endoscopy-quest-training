using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Contracts
{
    // Endoscopy input constraint. SDK near-touch invokes the existing onClick listeners.
    public sealed class NearOnlyButton:Button
    {
        public override void OnPointerClick(PointerEventData eventData){}
        public override void OnPointerDown(PointerEventData eventData){}
        public override void OnPointerUp(PointerEventData eventData){}
        public override void OnSubmit(BaseEventData eventData){}
    }
}
