using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    // Reuse the template's real OVR hand PokeInteractors; no synthetic or alternate input source.
    public sealed class ClinicalNearTouch:MonoBehaviour
    {
        public static ClinicalNearTouch Focused {get;private set;}
        public Button Button=>button;
        Button button;RectTransform rect;BoundsClipper clip;PokeInteractable poke;Graphic graphic;Color rest;
        bool committed;int pointer;float readyAt;static float lastCommit;
        System.Func<bool> inputAllowed;
        bool CanPress=>button&&button.IsActive()&&button.IsInteractable()&&(inputAllowed==null||inputAllowed());
        public static void Bind(Transform root,System.Func<bool> inputAllowed=null)
        {
            foreach(var button in root.GetComponentsInChildren<Button>(true))
            {
                button.navigation=new Navigation{mode=Navigation.Mode.None};
                foreach(var graphic in button.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                // Authored dialogue controls already own their Select handlers and surface.
                var existing=button.GetComponent<ClinicalNearTouch>();
                if(existing){existing.inputAllowed=inputAllowed;continue;}
                if(button.GetComponentInChildren<PokeInteractable>(true))continue;
                var touch=button.gameObject.AddComponent<ClinicalNearTouch>();
                touch.inputAllowed=inputAllowed;touch.Initialize(button);
            }
        }
        void Initialize(Button target)
        {
            button=target;rect=target.transform as RectTransform;graphic=target.targetGraphic;if(graphic)rest=graphic.color;
            var plane=gameObject.AddComponent<PlaneSurface>();plane.InjectAllPlaneSurface(PlaneSurface.NormalFacing.Backward,false);
            clip=gameObject.AddComponent<BoundsClipper>();Resize();
            var surface=gameObject.AddComponent<ClippedPlaneSurface>();surface.InjectAllClippedPlaneSurface(plane,new[]{clip});
            poke=gameObject.AddComponent<PokeInteractable>();poke.InjectAllPokeInteractable(surface);poke.WhenPointerEventRaised+=OnPointer;
        }
        void Resize(){if(clip&&rect){clip.Position=rect.rect.center;clip.Size=new Vector3(rect.rect.width,rect.rect.height,1);}}
        void LateUpdate(){Resize();if(poke)poke.enabled=CanPress;}
        void OnEnable(){readyAt=Time.unscaledTime+.35f;committed=false;}
        void OnPointer(PointerEvent e)
        {
            if(!CanPress)return;
            if(e.Type==PointerEventType.Hover||e.Type==PointerEventType.Select)Focused=this;
            if((e.Type==PointerEventType.Cancel||e.Type==PointerEventType.Unhover)&&Focused==this)Focused=null;
            if(graphic&&(e.Type==PointerEventType.Hover||e.Type==PointerEventType.Select))graphic.color=Color.Lerp(rest,Color.white,e.Type==PointerEventType.Select?.34f:.16f);
            if(graphic&&(e.Type==PointerEventType.Cancel||e.Type==PointerEventType.Unhover))graphic.color=rest;
            if(committed){if(pointer==e.Identifier&&(e.Type==PointerEventType.Cancel||e.Type==PointerEventType.Unselect))committed=false;return;}
            if(e.Type!=PointerEventType.Select||Time.unscaledTime<readyAt||Time.unscaledTime-lastCommit<.35f)return;
            committed=true;pointer=e.Identifier;lastCommit=Time.unscaledTime;button.onClick.Invoke();
        }
        void OnDisable(){committed=false;if(Focused==this)Focused=null;if(graphic)graphic.color=rest;}
        void OnDestroy(){if(poke)poke.WhenPointerEventRaised-=OnPointer;}
    }
}
