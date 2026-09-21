using System;
using System.Collections.Generic;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.VisitorPrologue.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BotanicalGardenQR.Bootstrap
{
    // Owns the session for one application process. Each visit reuses the Visitor composition.
    internal sealed class FullScriptJourneyRuntime : IDisposable
    {
        readonly VisitorRuntimeBindings _bindings;
        readonly Action<DiagnosticEvent> _diagnostics;
        readonly VirtualRoomTrackingOrigin _tracking;
        readonly VirtualRoomTrackingGuard _guard;
        readonly IClinicalRoomAssetRelease _assetRelease;
        VisitorRuntimeComposition _composition;
        FullScriptRoomVisit _visit;
        VirtualRoomEnvironment _room;
        ClinicalRoomTransitionCoordinator _transitions;
        ClinicalRoomTransitionRequest _request;
        readonly GameObject _curtain;
        readonly Image _shade;
        readonly TMP_Text _message;
        readonly GameObject _recover;
        enum Stage { Initial, Active, FadeOut, Unload, BeginRelease, WaitRelease, Load, WaitTracking, FadeIn, Failed }
        Stage _stage;
        float _alpha=1, _wait;
        bool _started, _disposed, _restoring;
        internal ClinicalJourneyDefinition Definition { get; }
        internal ClinicalJourneySession Session { get; private set; }
        internal VisitorPrologueController Prologue { get; } = VisitorPrologueModuleFactory.Create();
        internal FullScriptRoomVisit Visit => _visit;
        internal int OfficeFieldsViewed { get; set; }
        internal int OfficeRowsViewed { get; set; }
        internal BotanicalGardenQR.FrontendShell.Contracts.ClinicalObservationProgress ObservationProgress { get; private set; }
            = new BotanicalGardenQR.FrontendShell.Contracts.ClinicalObservationProgress();
        readonly Dictionary<string,ClinicalCourseSession> _washingLessons = new Dictionary<string,ClinicalCourseSession>();
        internal ClinicalCourseSession ResumeWashingLesson(ClinicalCourseLesson lesson)
        {
            if(!_washingLessons.TryGetValue(lesson.sceneId,out var session))
                _washingLessons.Add(lesson.sceneId,session=new ClinicalCourseSession(lesson));
            return session;
        }
        internal string WashingLearningSummary(int index)
        {
            const string boundary="\n旧课学习记录不代替RE现场核查，也不计作合规成绩。";
            if(index==0)return $"旧洗消 P01 · 三处观察\n已学 {ObservationProgress.CompletedCount} 处，主动跳过 {ObservationProgress.SkippedCount} 处\n未完成 {3-ObservationProgress.CompletedCount-ObservationProgress.SkippedCount} 处。"+boundary;
            var ids=new[]{"baobab","bottle_tree","ceiba","macrozamia","welwitschia"};
            if(index<1 || index>ids.Length)return "";
            if(!_washingLessons.TryGetValue(ids[index-1],out var lesson))return $"旧洗消 P{index+1:00}\n未开始。"+boundary;
            return $"旧洗消 P{index+1:00} · {lesson.Lesson.title}\n答对 {lesson.CorrectCount} 项，主动跳过 {lesson.SkippedCount} 项\n"+
                (lesson.Phase==ClinicalCoursePhase.Finished?"本课流程已完成。":"本课尚未完成。")+boundary;
        }
        internal bool InputAllowed => !_disposed && _stage==Stage.Active && (_tracking==null || _tracking.CanInteract);

        internal FullScriptJourneyRuntime(VisitorRuntimeBindings bindings, Action<DiagnosticEvent> diagnostics,
            ITrackingOriginTiming timing = null, IClinicalRoomAssetRelease assetRelease = null)
        {
            _bindings=bindings; _diagnostics=diagnostics;
            _assetRelease=assetRelease ?? new ClinicalRoomAssetRelease();
            Definition=ClinicalJourneyConfiguration.Load() ?? throw new InvalidOperationException("Full-script journey configuration is missing or invalid.");
            Session=new ClinicalJourneySession(Definition,ClinicalJourneyMode.GuidedLearning);
            _transitions=new ClinicalRoomTransitionCoordinator(Session);
            var rig=bindings.Platform.XrRigRoot.GetComponentInChildren<OVRCameraRig>(true);
            var manager=bindings.Platform.XrRigRoot.GetComponentInChildren<OVRManager>(true);
            if(rig)
            {
                var start=new Pose(bindings.Platform.XrRigRoot.transform.position,bindings.Platform.XrRigRoot.transform.rotation);
                _tracking=new VirtualRoomTrackingOrigin(rig,manager,timing,start);
                _guard=new VirtualRoomTrackingGuard(_tracking,bindings.Platform.Viewer,bindings.Platform.InteractionRigRoot,bindings.Configuration.UiDefaults.SharedFont);
            }
            var viewer=bindings.Platform.Viewer;
            _curtain=new GameObject("FullScriptTransitionCurtain",typeof(RectTransform),typeof(Canvas),typeof(Image));
            _curtain.transform.SetParent(viewer,false);
            _curtain.transform.localPosition=new Vector3(0,0,.75f);
            ((RectTransform)_curtain.transform).sizeDelta=new Vector2(4,4);
            var canvas=_curtain.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=31000;
            canvas.worldCamera=viewer.GetComponent<Camera>();
            _shade=_curtain.GetComponent<Image>();_shade.raycastTarget=false;
            _shade.color=Color.black;
            // Status is on a separate readable surface in front of the opaque cover.
            var notice=ClinicalPanelStyle.Rect(_curtain.transform,"TransitionStatus",0,0,800,250);
            notice.localScale=Vector3.one*.00065f;notice.localPosition=new Vector3(0,0,-.20f);
            _message=ClinicalPanelStyle.Label(notice,bindings.Configuration.UiDefaults.SharedFont,"Message",0,25,780,150,27);
            _message.alignment=TextAlignmentOptions.Center;
            var retry=ClinicalPanelStyle.Button(notice,bindings.Configuration.UiDefaults.SharedFont,"RetryRoom","恢复当前房间",0,-90,720,70,Recover,true);
            _recover=retry.gameObject;
            ClinicalNearTouch.Bind(retry.transform,()=>_stage==Stage.Failed && (_tracking==null || _tracking.CanInteract));
            _recover.SetActive(false);
            try { LoadRoom(Session.CurrentRoomId); }
            catch { Dispose();throw; }
        }

        internal void StartExperience(){_started=true;}

        internal void SelectMode(ClinicalJourneyMode mode)
        {
            if(!InputAllowed || Session.CurrentRoomId!=Definition.startRoomId || Session.MainlineIndex!=0) return;
            Session=new ClinicalJourneySession(Definition,mode);
            OfficeFieldsViewed=0;
            OfficeRowsViewed=0;
            _washingLessons.Clear();
            ObservationProgress=new BotanicalGardenQR.FrontendShell.Contracts.ClinicalObservationProgress();
            _transitions=new ClinicalRoomTransitionCoordinator(Session);
        }

        internal IEnumerable<string> Destinations()
        {
            var next=Session.MainlineIndex+1;
            if(next<Definition.mainlineRoomIds.Length && Definition.mainlineRoomIds[next]!=Session.CurrentRoomId)
                yield return Definition.mainlineRoomIds[next];
            foreach(var room in Definition.rooms)
                if(room.id!=Session.CurrentRoomId && Session.HasVisited(room.id) &&
                   (next>=Definition.mainlineRoomIds.Length || room.id!=Definition.mainlineRoomIds[next])) yield return room.id;
        }

        internal bool RequestRoom(string target)
        {
            if(!InputAllowed || !_visit.AtDoor || !_visit.DoorOpen) return false;
            var result=_transitions.TryBeginAtDoor(target,true,true);
            if(!result.Accepted) return false;
            _request=result.Request;_stage=Stage.FadeOut;_wait=0;_alpha=0;
            _curtain.SetActive(true);_recover.SetActive(false);
            _message.text="请原地等待\n正在前往"+Definition.FindRoom(target).displayName;
            return true;
        }

        internal void Tick(float dt)
        {
            if(_disposed || !_started) return;
            dt=Mathf.Clamp(dt,0,.1f);
            bool tracked=_tracking==null || _tracking.CanInteract;
            switch(_stage)
            {
                case Stage.Initial:
                    _message.text="正在准备医院大厅\n请原地等待定位";
                    if(tracked){_stage=Stage.FadeIn;_wait=0;}
                    break;
                case Stage.Active:
                    _composition.Tick(dt);
                    return;
                case Stage.FadeOut:
                    _alpha=Mathf.Min(1,_alpha+dt*4);
                    _composition?.Tick(dt); // sends Hold to the guide while input is locked
                    if(_alpha>=1) _stage=Stage.Unload;
                    break;
                case Stage.Unload:
                    ReleaseVisit();
                    _stage=Stage.BeginRelease;
                    break;
                case Stage.BeginRelease:
                    _assetRelease.Begin();
                    _stage=Stage.WaitRelease;
                    break;
                case Stage.WaitRelease:
                    if(_assetRelease.IsComplete) _stage=Stage.Load;
                    break;
                case Stage.Load:
                    try { LoadRoom(_restoring?_request.FromRoomId:_request.TargetRoomId);_stage=Stage.WaitTracking;_wait=0; }
                    catch(Exception e)
                    {
                        Debug.LogWarning("[FullScript] Room load failed: "+e.Message);
                        ReleaseVisit();
                        if(!_restoring){_restoring=true;_stage=Stage.BeginRelease;}
                        else Fail("房间暂时无法加载。\n请近触重试，或退出后重新开始。");
                    }
                    break;
                case Stage.WaitTracking:
                    _wait+=dt;
                    if(tracked)
                    {
                        if(_restoring)
                        {
                            _transitions.Complete(_request,false);
                            Fail("已恢复原房间，进度保留。\n近触继续后可再次从房门切换。");
                        }
                        else if(_transitions.Complete(_request,true).Succeeded) _stage=Stage.FadeIn;
                        else {_restoring=true;_stage=Stage.Unload;}
                    }
                    else
                    {
                        _message.text="请停止走动，等待头显恢复追踪。";
                        if(_wait>15 && !_restoring){_restoring=true;_stage=Stage.Unload;}
                    }
                    break;
                case Stage.FadeIn:
                    if(!tracked){_message.text="请停止走动，等待头显恢复追踪。";break;}
                    _alpha=Mathf.Max(0,_alpha-dt*4);
                    if(_alpha<=0)
                    {
                        _curtain.SetActive(false);_stage=Stage.Active;
                        _composition.StartExperience();
                    }
                    break;
            }
            _shade.color=new Color(0,0,0,_alpha);
        }

        void Fail(string text){_stage=Stage.Failed;_alpha=1;_message.text=text;_recover.SetActive(true);}
        void Recover()
        {
            if(_stage!=Stage.Failed || (_tracking!=null && !_tracking.CanInteract)) return;
            _recover.SetActive(false);
            if(_room==null){_stage=Stage.BeginRelease;_restoring=true;return;}
            _transitions.RecoverAfterFailure();_restoring=false;_stage=Stage.FadeIn;
        }
        void LoadRoom(string id)
        {
            if(_room!=null || _visit!=null || _composition!=null)
                throw new InvalidOperationException("Release the current room before loading another room.");
            var map=FullScriptRoomCatalog.Map(id,_bindings.Configuration.MapDefinition,
                Session.Mode==ClinicalJourneyMode.GuidedLearning && !Session.IsFinished);
            _room=VirtualRoomEnvironment.Create(_bindings.Platform.XrRigRoot,_bindings.Platform.MrukRoot,map,sharedTracking:_tracking);
            _visit=new FullScriptRoomVisit(this,id,map,_room,_bindings.Platform.Viewer,_bindings.Configuration.UiDefaults.SharedFont);
            _composition=VisitorRuntimeComposition.Create(_bindings,_diagnostics,_visit);
        }
        void ReleaseVisit()
        {
            _visit?.Dispose();_visit=null;
            _composition?.Dispose();_composition=null;
            _room?.Dispose();_room=null;
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;
            ReleaseVisit();Prologue.Dispose();_guard?.Dispose();_tracking?.Dispose();
            _washingLessons.Clear();
            if(_curtain){_curtain.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(_curtain);else UnityEngine.Object.DestroyImmediate(_curtain);}
        }
    }
}
