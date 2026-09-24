using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.FrontendShell.Runtime
{
    /// <summary>Describes the accepted hand-touch action so feedback can match its purpose.</summary>
    public enum ClinicalTouchFeedbackKind { Confirm, Page, RoomChange }

    // Reuse the template's real OVR hand PokeInteractors; no synthetic or alternate input source.
    public sealed class ClinicalNearTouch:MonoBehaviour
    {
        public static ClinicalNearTouch Focused {get;private set;}
        /// <summary>
        /// Raised after a tracked poke passes the touch, timing, and input gates and the button action is invoked.
        /// Hover, gaze, and rejected or repeated touches do not raise this event.
        /// </summary>
        public static event System.Action<ClinicalTouchFeedbackKind> ActionAccepted;
        public Button Button=>button;
        public event System.Action<ClinicalNearTouch,bool> ContactChanged;
        readonly System.Collections.Generic.HashSet<int> contacts=new System.Collections.Generic.HashSet<int>();
        bool preserveGaze;
        Button button;RectTransform rect;BoundsClipper clip;PokeInteractable poke;Graphic graphic;Color rest;
        bool committed;int pointer;float readyAt;static float lastCommit;
        readonly ClinicalPressHoldState pressHold = new ClinicalPressHoldState();
        float minimumHoverSeconds, holdSeconds;
        System.Func<float> timeProvider;
        System.Func<bool> inputAllowed;
        float Now => timeProvider?.Invoke() ?? Time.unscaledTime;
        bool CanPress=>button&&button.IsActive()&&button.IsInteractable()&&(inputAllowed==null||inputAllowed());
        public void RequireIntentionalPress(float hoverSeconds, float pressSeconds)
        {
            minimumHoverSeconds = Mathf.Max(0, hoverSeconds);
            holdSeconds = Mathf.Max(0, pressSeconds);
            pressHold.Cancel();
        }
        internal void SetTimeProvider(System.Func<float> provider) => timeProvider = provider;
        public static void Bind(Transform root,System.Func<bool> inputAllowed=null,bool preserveGaze=false)
        {
            foreach(var button in root.GetComponentsInChildren<Button>(true))
            {
                button.navigation=new Navigation{mode=Navigation.Mode.None};
                if(!preserveGaze)foreach(var graphic in button.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
                // Authored dialogue controls already own their Select handlers and surface.
                var existing=button.GetComponent<ClinicalNearTouch>();
                if(existing)
                {
                    existing.inputAllowed=inputAllowed;existing.preserveGaze=preserveGaze;
                    if(button.GetComponent<ClinicalChoiceVisual>())existing.RequireIntentionalPress(.09f,.22f);
                    continue;
                }
                if(button.GetComponentInChildren<PokeInteractable>(true))continue;
                var touch=button.gameObject.AddComponent<ClinicalNearTouch>();
                touch.inputAllowed=inputAllowed;touch.preserveGaze=preserveGaze;touch.Initialize(button);
                if(button.GetComponent<ClinicalChoiceVisual>())touch.RequireIntentionalPress(.09f,.22f);
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
        void LateUpdate()
        {
            Resize();
            if(!CanPress)ClearContacts();
            if(poke)poke.enabled=CanPress;
            if(holdSeconds<=0 || !CanPress || !pressHold.IsSelected)return;
            RefreshVisual(Mathf.Lerp(.5f,1f,pressHold.Progress(Now,holdSeconds)));
            if(Now>=readyAt && Now-lastCommit>=.35f && pressHold.TryCommit(Now,holdSeconds))Commit();
        }
        void OnEnable(){_lastRect=default;Resize();readyAt=Now+.35f;committed=false;pressHold.Cancel();}
        void OnPointer(PointerEvent e)
        {
            // Releases can arrive after a page has hidden/disabled this button.
            // Always rearm on withdrawal, before testing whether a new press is allowed.
            if(pointer==e.Identifier&&(e.Type==PointerEventType.Cancel||e.Type==PointerEventType.Unselect||e.Type==PointerEventType.Unhover))committed=false;
            if(holdSeconds>0)
            {
                if(e.Type==PointerEventType.Hover)pressHold.Hover(e.Identifier,Now);
                else if(e.Type==PointerEventType.Select)pressHold.Select(e.Identifier,Now,minimumHoverSeconds);
                else if(e.Type==PointerEventType.Unselect)pressHold.Unselect(e.Identifier);
                else if(e.Type==PointerEventType.Cancel||e.Type==PointerEventType.Unhover)pressHold.Unhover(e.Identifier);
            }
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
            if(holdSeconds>0)
            {
                if(e.Type==PointerEventType.Unselect && !preserveGaze)RefreshVisual(.5f);
                return;
            }
            if(committed)return;
            if(e.Type!=PointerEventType.Select||Now<readyAt||Now-lastCommit<.35f)return;
            pointer=e.Identifier;
            Commit();
        }
        void Commit()
        {
            committed=true;lastCommit=Now;button.onClick.Invoke();
            ActionAccepted?.Invoke(FeedbackKind(button.name));
        }
        internal static ClinicalTouchFeedbackKind FeedbackKind(string buttonName)
        {
            if(buttonName.StartsWith("Travel_",System.StringComparison.Ordinal))return ClinicalTouchFeedbackKind.RoomChange;
            if(buttonName.IndexOf("Page",System.StringComparison.OrdinalIgnoreCase)>=0 ||
               buttonName.IndexOf("Next",System.StringComparison.OrdinalIgnoreCase)>=0 ||
               buttonName.IndexOf("Previous",System.StringComparison.OrdinalIgnoreCase)>=0 ||
               buttonName.IndexOf("Back",System.StringComparison.OrdinalIgnoreCase)>=0 ||
               buttonName.IndexOf("Return",System.StringComparison.OrdinalIgnoreCase)>=0 ||
               buttonName.IndexOf("Close",System.StringComparison.OrdinalIgnoreCase)>=0)
                return ClinicalTouchFeedbackKind.Page;
            return ClinicalTouchFeedbackKind.Confirm;
        }
        void ClearContacts(){pressHold.Cancel();if(contacts.Count==0)return;contacts.Clear();ContactChanged?.Invoke(this,false);}
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

    internal sealed class ClinicalPressHoldState
    {
        int hoverPointer = int.MinValue, selectedPointer = int.MinValue;
        float hoveredAt, selectedAt;
        bool committed;
        internal bool IsSelected => selectedPointer != int.MinValue;
        internal void Hover(int pointer, float now)
        {
            if(hoverPointer==pointer)return;
            hoverPointer=pointer;hoveredAt=now;
        }
        internal bool Select(int pointer, float now, float minimumHoverSeconds)
        {
            if(IsSelected || hoverPointer!=pointer)return false;
            selectedPointer=pointer;
            selectedAt=Mathf.Max(now,hoveredAt+minimumHoverSeconds);
            committed=false;
            return true;
        }
        internal float Progress(float now, float holdSeconds)
            => IsSelected ? Mathf.Clamp01((now-selectedAt)/holdSeconds) : 0;
        internal bool TryCommit(float now, float holdSeconds)
        {
            if(!IsSelected || committed || now-selectedAt<holdSeconds)return false;
            committed=true;return true;
        }
        internal void Unselect(int pointer)
        {
            if(selectedPointer!=pointer)return;
            Cancel();
        }
        internal void Unhover(int pointer)
        {
            if(hoverPointer==pointer)Cancel();
        }
        internal void Cancel()
        {
            hoverPointer=selectedPointer=int.MinValue;committed=false;
        }
    }
}
