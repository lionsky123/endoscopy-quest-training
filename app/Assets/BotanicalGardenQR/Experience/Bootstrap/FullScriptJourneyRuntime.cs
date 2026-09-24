using System;
using System.Collections.Generic;
using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.VisitorPrologue.Runtime;
using Oculus.Interaction;
using Oculus.Interaction.Input;
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
        readonly IHand[] _trackedHands;
        VisitorRuntimeComposition _composition;
        FullScriptRoomVisit _visit;
        VirtualRoomEnvironment _room;
        VirtualRoomEnvironment.BuildOperation _build;
        MapDefinition _buildMap;
        string _buildRoomId;
        ClinicalRoomTransitionCoordinator _transitions;
        ClinicalRoomTransitionRequest _request;
        readonly GameObject _curtain;
        readonly Image _shade;
        readonly Texture2D _transitionAtlas;
        readonly RectTransform _transitionPreviewFrameRect;
        readonly RectTransform _transitionPreviewRect;
        readonly RawImage _transitionPreview;
        readonly TMP_Text _transitionPreviewCaption;
        readonly TMP_Text _message;
        readonly GameObject _recover;
        readonly float _audioListenerVolume;
        readonly bool _audioListenerPaused;
        readonly FullScriptGalleryTutorial _galleryTutorial = new FullScriptGalleryTutorial();
        enum Stage { Initial, Active, FadeOut, Unload, BeginRelease, WaitRelease, Load, Build, WaitTracking, FadeIn, Failed, FocusOut, FocusMove, FocusIn }
        Action _focusComplete;
        BotanicalGardenQR.MapNavigation.Contracts.MapPosition _focusPoint;
        Vector3? _focusForward;
        Stage _stage;
        float _alpha=1, _wait;
        bool _started, _disposed, _restoring, _viewAligned, _initialRecovery;
        readonly bool _editorPreview;
        internal ClinicalJourneyDefinition Definition { get; }
        internal ClinicalJourneySession Session { get; private set; }
        internal VisitorPrologueController Prologue { get; }
        internal VisitorCoachThemeAsset CoachTheme => _bindings.Configuration.VisitorCoachTheme;
        internal IHand[] TrackedHands => _trackedHands;
        internal string LoadingStage => _stage.ToString();
        internal FullScriptRoomVisit Visit => _visit;
        internal FullScriptAudioRuntime Audio { get; private set; }
        internal FullScriptGalleryTutorial GalleryTutorial => _galleryTutorial;
        internal bool Stationary { get; }
        internal readonly HashSet<string> ScriptStepsViewed = new HashSet<string>(StringComparer.Ordinal);
        internal readonly Dictionary<string,Vector2Int> ScriptPositions = new Dictionary<string,Vector2Int>(StringComparer.Ordinal);
        internal readonly HashSet<string> ScriptActions = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> OfficeFieldsViewed = new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> OfficeRowsViewed = new HashSet<string>(StringComparer.Ordinal);
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
            ITrackingOriginTiming timing = null, IClinicalRoomAssetRelease assetRelease = null, bool stationary = true, bool editorPreview = false)
        {
            _bindings=bindings; _diagnostics=diagnostics; Stationary=stationary;
            var trackedHands=new List<IHand>();
            foreach(var interactor in bindings.Platform.InteractionRigRoot.GetComponentsInChildren<PokeInteractor>(true))
            {
                var hand=interactor.GetComponent<HandRef>();
                if(hand!=null)trackedHands.Add(hand);
            }
            _trackedHands=trackedHands.ToArray();
#if UNITY_EDITOR
            _editorPreview=editorPreview;
#endif
            if(!stationary) Prologue=VisitorPrologueModuleFactory.Create();
            ClinicalTrainingRecordsConfiguration.Load();
            _audioListenerVolume=AudioListener.volume;
            _audioListenerPaused=AudioListener.pause;
            AudioListener.volume=0f;
            AudioListener.pause=true;
            _assetRelease=assetRelease ?? new ClinicalRoomAssetRelease();
            Definition=ClinicalJourneyConfiguration.Load() ?? throw new InvalidOperationException("Full-script journey configuration is missing or invalid.");
            Session=new ClinicalJourneySession(Definition,ClinicalJourneyMode.GuidedLearning);
            _transitions=new ClinicalRoomTransitionCoordinator(Session);
            var rig=bindings.Platform.XrRigRoot.GetComponentInChildren<OVRCameraRig>(true);
            var manager=bindings.Platform.XrRigRoot.GetComponentInChildren<OVRManager>(true);
            if(rig && !_editorPreview)
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
            _shade.color=new Color(.16f,.15f,.21f,1f);
            _transitionAtlas=Resources.Load<Texture2D>("FullScriptRooms/RoomGallery/room-preview-atlas-v1");
            // A destination illustration and plain-language status stay in view
            // while the old room releases and the new room is loaded.
            var notice=ClinicalPanelStyle.Rect(_curtain.transform,"TransitionStatus",0,0,1080,390);
            notice.localScale=Vector3.one*.0007f;notice.localPosition=new Vector3(0,0,-.20f);
            ClinicalPanelStyle.Frame(notice);
            var previewFrame=ClinicalPanelStyle.Rect(notice,"TransitionPreviewFrame",-365,20,270,300);
            _transitionPreviewFrameRect=previewFrame;
            ClinicalPanelStyle.Fill(previewFrame,Color.white,10);
            _transitionPreviewRect=ClinicalPanelStyle.Rect(previewFrame,"TransitionPreview",0,50,220,220);
            _transitionPreview=_transitionPreviewRect.gameObject.AddComponent<RawImage>();
            _transitionPreview.raycastTarget=false;
            _transitionPreview.texture=_transitionAtlas;
            _transitionPreview.uvRect=TransitionPreviewUv(0);
            _transitionPreviewCaption=ClinicalPanelStyle.Label(previewFrame,bindings.Configuration.UiDefaults.SharedFont,
                "TransitionPreviewCaption",0,-100,254,36,16);
            _transitionPreviewCaption.alignment=TextAlignmentOptions.Center;
            _transitionPreviewCaption.text="训练场景示意";
            _transitionPreviewCaption.color=bindings.Configuration.VisitorCoachTheme.DetailTextColor;
            _message=ClinicalPanelStyle.Label(notice,bindings.Configuration.UiDefaults.SharedFont,"Message",235,0,610,230,27);
            _message.alignment=TextAlignmentOptions.Center;
            _message.color=ClinicalPanelStyle.TextPrimary;
            var retry=ClinicalPanelStyle.Button(notice,bindings.Configuration.UiDefaults.SharedFont,"RetryRoom","恢复当前房间",230,-130,620,66,Recover,true);
            ClinicalPanelStyle.EmphasizeButton(retry,false,true);
            _recover=retry.gameObject;
            ClinicalNearTouch.Bind(retry.transform,()=>_stage==Stage.Failed && (_tracking==null || _tracking.CanInteract));
            _recover.SetActive(false);
            Audio=FullScriptAudioRuntime.Create(viewer,bindings.Configuration.UiDefaults.SharedFont,()=>InputAllowed);
            SetTransitionPreview(Session.CurrentRoomId);
            try { LoadRoom(Session.CurrentRoomId); }
            catch(Exception e)
            {
                Debug.LogWarning("[FullScript] Initial room load failed: "+e.Message);
                ReleaseVisit();
                _initialRecovery=true;
                Fail("大厅暂时无法加载。\n请轻触按钮重试。");
            }
        }

        internal void StartExperience(){_started=true;Audio?.StartMusic();}

        internal void SelectMode(ClinicalJourneyMode mode)
        {
            if (Stationary && mode != ClinicalJourneyMode.GuidedLearning) return;
            if(!InputAllowed || Session.CurrentRoomId!=Definition.startRoomId || Session.MainlineIndex!=0) return;
            Session=new ClinicalJourneySession(Definition,mode);
            OfficeFieldsViewed.Clear();
            OfficeRowsViewed.Clear();
            ScriptStepsViewed.Clear();
            ScriptActions.Clear();
            _washingLessons.Clear();
            ObservationProgress=new BotanicalGardenQR.FrontendShell.Contracts.ClinicalObservationProgress();
            _transitions=new ClinicalRoomTransitionCoordinator(Session);
        }

        internal IEnumerable<ClinicalActDestination> Destinations()
            => ClinicalActSelection.AvailableDestinations(Definition, Session, roomGallery: Stationary);

        internal bool RequestRoom(string target)
        {
            if(!InputAllowed || (!_visit.Stationary && !_visit.AtDoor) || !_visit.DoorOpen) return false;
            var result=Stationary ? _transitions.TryBeginStationary(target,true) : _transitions.TryBeginAtDoor(target,true,true);
            if(!result.Accepted) return false;
            _request=result.Request;_stage=Stage.FadeOut;_wait=0;_alpha=0;
            _curtain.SetActive(true);_recover.SetActive(false);
            SetTransitionPreview(target);
            _message.text="请原地等待\n正在前往"+Definition.FindRoom(target).displayName;
            return true;
        }

        internal bool RequestInspectionPoint(string taskId, Action after)
        {
            if(!Stationary || !InputAllowed || after==null)return false;
            var configured=InspectionViewConfiguration.Load()?.Find(_visit.RoomId,taskId);
            if(configured!=null)
            {
                _focusPoint=new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(configured.position.x,configured.position.y,configured.position.z);
                _focusForward=configured.Forward;
                return BeginObservationTransition(after);
            }
            var points=_bindings.Configuration.MapDefinition.points;
            int index=taskId=="RE-01"?0:taskId=="RE-02"?2:taskId=="RE-03"||taskId=="RE-06"?3:taskId=="RE-04"?4:5;
            _focusPoint=_visit.RoomId==FullScriptRoomCatalog.Washing ? points[index].position : _visit.Map.points[0].position;
            _focusForward=null;
            return BeginObservationTransition(after);
        }

        internal bool RequestWaitingObservation(bool corridor, Action after)
        {
            if(!Stationary || !InputAllowed || after==null || _visit?.RoomId!="R03_WAITING")return false;
            var name=corridor?"ClinicalCorridorObservation":"WaitingObservation";
            var anchor=_room.Root.GetComponentsInChildren<Transform>().SingleOrDefault(t=>t.name==name);
            if(!anchor)return false;
            var point=_room.Root.transform.InverseTransformPoint(anchor.position);
            _focusPoint=new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(point.x,point.y,point.z);
            // Oblique views retain the divider while exposing seats/aisle beside it.
            // Straight-on framing at seated eye height hid the floor behind the base.
            _focusForward=corridor?new Vector3(.6f,0,-.8f):new Vector3(-.6f,0,.8f);
            return BeginObservationTransition(after);
        }

        bool BeginObservationTransition(Action after)
        {
            _focusComplete=after;_stage=Stage.FocusOut;_alpha=0;
            _curtain.SetActive(true);_recover.SetActive(false);SetTransitionPreview(_visit.RoomId);_message.text="请留在原位\n正在切换检查点";
            return true;
        }

        internal bool RequestStorageRegisterObservation(Transform register,Action after)
        {
            if(!Stationary || !InputAllowed || after==null || _visit?.RoomId!="R02_STORAGE" || !register || !register.IsChildOf(_room.Root.transform))return false;
            // Keep the fixed .52m reading panel/tool bar in front of the cabinet
            // surface, not inside it. The paper remains within near-hand reach.
            var point=_room.Root.transform.InverseTransformPoint(register.position-register.forward*.60f);
            _focusPoint=new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(point.x,0,point.z);
            _focusForward=_room.Root.transform.InverseTransformDirection(register.forward);
            return BeginObservationTransition(after);
        }

        internal bool RequestStorageObservation(bool close,Action after)
        {
            if(!Stationary || !InputAllowed || after==null || _visit?.RoomId!="R02_STORAGE")return false;
            var anchor=_room.Root.GetComponentsInChildren<Transform>().SingleOrDefault(t=>t.name=="StorageObservation");
            if(!anchor)return false;
            var point=_room.Root.transform.InverseTransformPoint(anchor.position);
            _focusPoint=close?new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(point.x,point.y,point.z):_visit.Map.points[0].position;
            _focusForward=Vector3.forward;
            return BeginObservationTransition(after);
        }

        internal void Tick(float dt)
        {
            if(_disposed || !_started) return;
            dt=Mathf.Clamp(dt,0,.1f);
            bool tracked=_tracking==null || _tracking.CanInteract;
            switch(_stage)
            {
                case Stage.FocusOut:
                    _alpha=Mathf.Min(1,_alpha+dt*4);
                    if(_alpha>=1 && tracked){_stage=Stage.FocusMove;_wait=0;}
                    else if(_alpha>=1 && !tracked)
                    {
                        _message.text="请保持原位，等待头显恢复追踪。";
                    }
                    break;
                case Stage.FocusMove:
                    if(!tracked)
                    {
                        _message.text="请保持原位，等待头显恢复追踪。";
                        break;
                    }
                    _room.AlignStationaryView(_visit.Map,_bindings.Platform.Viewer,_bindings.Platform.XrRigRoot.transform.position.y,_focusPoint,_focusForward);
                    _stage=Stage.FocusIn;_wait=0;
                    break;
                case Stage.FocusIn:
                    if(!tracked)
                    {
                        _alpha=1;
                        _message.text="请保持原位，等待头显恢复追踪。";
                        break;
                    }
                    _alpha=Mathf.Max(0,_alpha-dt*4);
                    if(_alpha<=0)
                    {
                        _curtain.SetActive(false);_stage=Stage.Active;
                        var completed=_focusComplete;_focusComplete=null;completed?.Invoke();
                    }
                    break;
                case Stage.Initial:
                    _wait += dt;
                    _message.text="正在准备医院大厅\n请原地等待定位";
                    if(tracked){AlignReadyView();_stage=Stage.FadeIn;_wait=0;}
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
                    try
                    {
                        var id=_initialRecovery?Session.CurrentRoomId:_restoring?_request.FromRoomId:_request.TargetRoomId;
                        if(Application.isPlaying)
                        {
                            BeginRoomBuild(id);
                            _stage=Stage.Build;
                        }
                        else
                        {
                            LoadRoom(id);
                            _stage=_initialRecovery?Stage.Initial:Stage.WaitTracking;
                            _initialRecovery=false;_wait=0;
                        }
                    }
                    catch(Exception e) { HandleRoomLoadFailure(e); }
                    break;
                case Stage.Build:
                    try
                    {
                        if(_build.Tick())
                        {
                            FinishRoomBuild();
                            _stage=_initialRecovery?Stage.Initial:Stage.WaitTracking;
                            _initialRecovery=false;_wait=0;
                        }
                    }
                    catch(Exception e) { HandleRoomLoadFailure(e); }
                    break;
                case Stage.WaitTracking:
                    _wait+=dt;
                    if(tracked)
                    {
                        AlignReadyView();
                        if(_restoring)
                        {
                            _transitions.Complete(_request,false);
                            Fail("已恢复原房间，进度保留。\n近触继续后可重新选择目的房间。");
                        }
                        else if(_transitions.Complete(_request,true).Succeeded) _stage=Stage.FadeIn;
                        else {_restoring=true;_stage=Stage.Unload;}
                    }
                    else
                    {
                        _message.text="请停止走动，等待头显恢复追踪。";
                        if(_wait>15 && !_restoring)
                        {
                            _restoring=true;_stage=Stage.Unload;SetTransitionPreview(_request.FromRoomId);
                            _message.text="正在恢复原房间\n请等待追踪恢复";
                        }
                    }
                    break;
                case Stage.FadeIn:
                    _wait+=dt;
                    if(!tracked){_message.text="请保持原位，等待头显恢复追踪。";break;}
                    _alpha=Mathf.Max(0,_alpha-dt*4);
                    if(_alpha<=0)
                    {
                        _curtain.SetActive(false);_stage=Stage.Active;
                        _composition.StartExperience();
                    }
                    break;
            }
            _shade.color=new Color(.16f,.15f,.21f,_alpha);
            if(_transitionPreviewFrameRect)_transitionPreviewFrameRect.localScale=Vector3.one*(1f+.45f*_alpha);
        }

        void Fail(string text){_stage=Stage.Failed;_alpha=1;_message.text=text;_recover.SetActive(true);}
        void Recover()
        {
            if(_stage!=Stage.Failed || (_tracking!=null && !_tracking.CanInteract)) return;
            _recover.SetActive(false);
            if(_initialRecovery)
            {
                _message.text="正在重新准备医院大厅\n请保持原位";
                _stage=Stage.BeginRelease;return;
            }
            if(_room==null){_stage=Stage.BeginRelease;_restoring=true;return;}
            _transitions.RecoverAfterFailure();_restoring=false;_stage=Stage.FadeIn;
        }
        void LoadRoom(string id)
        {
            if(_room!=null || _visit!=null || _composition!=null)
                throw new InvalidOperationException("Release the current room before loading another room.");
            var map=MapFor(id);
            _room=VirtualRoomEnvironment.Create(_bindings.Platform.XrRigRoot,_bindings.Platform.MrukRoot,map,sharedTracking:_tracking,trackHead:!_editorPreview);
            _viewAligned=false;
            _visit=new FullScriptRoomVisit(this,id,map,_room,_bindings.Platform.Viewer,_bindings.Configuration.UiDefaults.SharedFont);
            _composition=VisitorRuntimeComposition.Create(_bindings,_diagnostics,_visit);
        }

        MapDefinition MapFor(string id) => FullScriptRoomCatalog.Map(id,_bindings.Configuration.MapDefinition,
            !Stationary && Session.Mode==ClinicalJourneyMode.GuidedLearning && !Session.IsFinished, Stationary);

        void BeginRoomBuild(string id)
        {
            if(_room!=null || _visit!=null || _composition!=null || _build!=null)
                throw new InvalidOperationException("Release the current room before loading another room.");
            _buildMap=MapFor(id);
            _buildRoomId=id;
            _build=VirtualRoomEnvironment.BeginBuild(_bindings.Platform.XrRigRoot,_bindings.Platform.MrukRoot,
                _buildMap,sharedTracking:_tracking,trackHead:!_editorPreview);
        }

        void FinishRoomBuild()
        {
            _room=_build.TakeRoom();
            _build.Dispose();_build=null;
            _viewAligned=false;
            _visit=new FullScriptRoomVisit(this,_buildRoomId,_buildMap,_room,_bindings.Platform.Viewer,
                _bindings.Configuration.UiDefaults.SharedFont);
            _composition=VisitorRuntimeComposition.Create(_bindings,_diagnostics,_visit);
            _buildMap=null;_buildRoomId=null;
        }

        void HandleRoomLoadFailure(Exception error)
        {
            Debug.LogWarning("[FullScript] Room load failed: "+error.Message);
            ReleaseVisit();
            if(_initialRecovery)
            {
                Fail("大厅暂时无法加载。\n请轻触按钮重试。");
                return;
            }
            if(!_restoring)
            {
                _restoring=true;_stage=Stage.BeginRelease;SetTransitionPreview(_request.FromRoomId);
                _message.text="正在恢复原房间\n请保持原位";
            }
            else Fail("房间暂时无法加载。\n请近触重试，或退出后重新开始。");
        }

        void SetTransitionPreview(string roomId)
        {
            if(!_transitionPreview || !_transitionAtlas)return;
            var index=roomId switch
            {
                "R00_LOBBY"=>0,
                "R01_OFFICE"=>1,
                "R02_STORAGE"=>2,
                "R03_WAITING"=>3,
                "R04_GI"=>4,
                "R04_RESP"=>5,
                "R05_REPROCESSING"=>6,
                _=>0
            };
            _transitionPreview.uvRect=TransitionPreviewUv(index);
        }

        static Rect TransitionPreviewUv(int index)
        {
            const float size=1254f;
            var column=index%3;var row=index/3;
            var left=new[]{20f,435f,849f}[column]/size;
            var right=new[]{403f,817f,1233f}[column]/size;
            var top=new[]{20f,435f,850f}[row]/size;
            var bottom=new[]{403f,817f,1233f}[row]/size;
            return new Rect(left,1f-bottom,right-left,bottom-top);
        }
        void AlignReadyView()
        {
            if(_viewAligned || !Stationary)return;
            var configured=InspectionViewConfiguration.Load()?.Find(_visit.RoomId)?.initial;
            BotanicalGardenQR.MapNavigation.Contracts.MapPosition? point=null;
            Vector3? forward=null;
            if(configured!=null)
            {
                point=new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(configured.position.x,configured.position.y,configured.position.z);
                forward=configured.Forward;
            }
            _room.AlignStationaryView(_visit.Map,_bindings.Platform.Viewer,_bindings.Platform.XrRigRoot.transform.position.y,point,forward);
            _viewAligned=true;
        }
        void ReleaseVisit()
        {
            _build?.Dispose();_build=null;_buildMap=null;_buildRoomId=null;
            _visit?.Dispose();_visit=null;
            _composition?.Dispose();_composition=null;
            _room?.Dispose();_room=null;
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;
            ReleaseVisit();Prologue?.Dispose();_guard?.Dispose();_tracking?.Dispose();
            _focusComplete=null;
            _washingLessons.Clear();
            Audio?.Dispose();Audio=null;
            AudioListener.pause=_audioListenerPaused;
            AudioListener.volume=_audioListenerVolume;
            if(_curtain){_curtain.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(_curtain);else UnityEngine.Object.DestroyImmediate(_curtain);}
        }
    }
}
