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
        public event System.Action<ClinicalNearTouch,bool> ContactChanged;
        readonly System.Collections.Generic.HashSet<int> contacts=new System.Collections.Generic.HashSet<int>();
        bool preserveGaze;
        Button button;RectTransform rect;BoundsClipper clip;PokeInteractable poke;Graphic graphic;Color rest;
        bool committed;int pointer;float readyAt;static float lastCommit;
        System.Func<bool> inputAllowed;
        bool CanPress=>button&&button.IsActive()&&button.IsInteractable()&&(inputAllowed==null||inputAllowed());
        public static void Bind(Transform root,System.Func<bool> inputAllowed=null,bool preserveGaze=false)
        {
            foreach(var button in root.GetComponentsInChildren<Button>(true))
            {
                button.navigation=new Navigation{mode=Navigation.Mode.None};
                if(!preserveGaze)foreach(var graphic in button.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                // Authored dialogue controls already own their Select handlers and surface.
                var existing=button.GetComponent<ClinicalNearTouch>();
                if(existing){existing.inputAllowed=inputAllowed;existing.preserveGaze=preserveGaze;continue;}
                if(button.GetComponentInChildren<PokeInteractable>(true))continue;
                var touch=button.gameObject.AddComponent<ClinicalNearTouch>();
                touch.inputAllowed=inputAllowed;touch.preserveGaze=preserveGaze;touch.Initialize(button);
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
        Rect _lastRect;
        void Resize()
        {
            if(clip&&rect)
            {
                var current=rect.rect;
                if(current!=_lastRect)
                {
                    _lastRect=current;
                    clip.Position=current.center;
                    clip.Size=new Vector3(current.width,current.height,1);
                }
            }
        }
        void OnRectTransformDimensionsChange()=>Resize();
        void LateUpdate(){Resize();if(!CanPress)ClearContacts();if(poke)poke.enabled=CanPress;}
        void OnEnable(){_lastRect=default;Resize();readyAt=Time.unscaledTime+.35f;committed=false;}
        void OnPointer(PointerEvent e)
        {
            // Releases can arrive after a page has hidden/disabled this button.
            // Always rearm on withdrawal, before testing whether a new press is allowed.
            if(pointer==e.Identifier&&(e.Type==PointerEventType.Cancel||e.Type==PointerEventType.Unselect||e.Type==PointerEventType.Unhover))committed=false;
            if(e.Type==PointerEventType.Cancel||e.Type==PointerEventType.Unhover)
            {
                contacts.Remove(e.Identifier);
                if(contacts.Count==0)ContactChanged?.Invoke(this,false);
                if(Focused==this)Focused=null;
            }
            if(!CanPress)return;
            if(e.Type==PointerEventType.Hover||e.Type==PointerEventType.Select)
            {
                if(contacts.Add(e.Identifier)&&contacts.Count==1)ContactChanged?.Invoke(this,true);
            }
            if(e.Type==PointerEventType.Hover||e.Type==PointerEventType.Select)Focused=this;
            if((e.Type==PointerEventType.Cancel||e.Type==PointerEventType.Unhover)&&Focused==this)Focused=null;
            if(!preserveGaze&&(e.Type==PointerEventType.Hover||e.Type==PointerEventType.Select))RefreshVisual(.5f);
            if(!preserveGaze&&(e.Type==PointerEventType.Cancel||e.Type==PointerEventType.Unhover))RefreshVisual(0);
            if(committed)return;
            if(e.Type!=PointerEventType.Select||Time.unscaledTime<readyAt||Time.unscaledTime-lastCommit<.35f)return;
            committed=true;pointer=e.Identifier;lastCommit=Time.unscaledTime;button.onClick.Invoke();
        }
        void ClearContacts(){if(contacts.Count==0)return;contacts.Clear();ContactChanged?.Invoke(this,false);}
        void RefreshVisual(float hover)
        {
            // Retain the session's selected/incorrect state after the finger withdraws.
            foreach(var component in GetComponents<MonoBehaviour>())
                if(component is BotanicalGardenQR.FrontendShell.Contracts.IFrontendGazeProgressPresenter visual) { visual.PresentGazeProgress(hover); return; }
            if(graphic)graphic.color=Color.Lerp(rest,Color.white,hover*.32f);
        }
        void OnDisable(){committed=false;ClearContacts();if(Focused==this)Focused=null;if(!preserveGaze)RefreshVisual(0);}
        void OnDestroy(){if(poke)poke.WhenPointerEventRaised-=OnPointer;}
    }
}
