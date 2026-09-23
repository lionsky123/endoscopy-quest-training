using System;
using System.Linq;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Bootstrap
{
    // The latest script owns progression. C09's walking courses remain intact but
    // cannot replace these tasks or automatically sign off their missing evidence.
    internal sealed partial class FullScriptRoomVisit
    {
        int _scriptIndex, _detailIndex;
        GameObject _scriptObject;
        Grabbable _learningGrab;
        Action<PointerEvent> _learningGrabHandler;
        Pose _scriptObjectHome;
        bool _stationaryStarted;
        bool _storageSampleChosen;
        Pose _scriptPose;
        bool _hasScriptPose;
        Transform _doorLeaf;
        float _doorStable;
        System.Action _pendingScriptAction;
        bool _imageExpanded;
        bool _showScriptSource;
        int _brushView;
        bool _waitingCorridor;
        Material _scriptTextMaterial, _doorMaterial;
        internal void QueueStationaryAction(System.Action action)
        {
            if(InputAllowed && _pendingScriptAction==null)_pendingScriptAction=action;
        }
        internal int ScriptIndex => _scriptIndex;
        internal bool ShouldShowEntryGuide => RoomId != _owner.Definition.startRoomId || _owner.Session.MainlineIndex == 0;
        internal bool IsFinalSummaryVisit => ClinicalActSelection.OfficeVisitStage(_owner.Definition,_owner.Session)==ClinicalOfficeVisitStage.FinalSummary;
        internal string ScriptTaskId => ScriptTasks[Mathf.Clamp(_scriptIndex,0,ScriptTasks.Length-1)];
        string[] ScriptTasks => RoomId==FullScriptRoomCatalog.Washing
            ? new[]{"RE-01","RE-02","RE-03","RE-06","RE-04","RE-05"}
            : _owner.Definition.FindRoom(RoomId).taskIds;

        internal void BeginStationary()
        {
            if(!InputAllowed)return;
            _stationaryStarted=true;
            TryOpen(FullScriptRoomCatalog.Overview);
        }
        internal void BeginFromEntryGuide()
        {
            if(!InputAllowed)return;
            if(RoomId==_owner.Definition.startRoomId && _owner.Session.MainlineIndex==0)
            {
                if(_stationaryStarted)return;
                _stationaryStarted=true;
                _owner.SelectMode(ClinicalJourneyMode.GuidedLearning);
                _owner.Session.TryCompleteInstruction("N00");
                ContinueToDoor();
                return;
            }
            BeginStationary();
        }
        internal void TickStationary(float dt)
        {
            if(!_stationaryStarted || !InputAllowed)return;
            if(_pendingScriptAction!=null)
            {
                var action=_pendingScriptAction;_pendingScriptAction=null;action();
                return;
            }
            TickStorageCabinet(dt);
            if(_doorLeaf && _owner.Session.CanEdit)
            {
                _doorStable=Quaternion.Angle(_doorLeaf.localRotation,Quaternion.identity)<=5 ? _doorStable+dt : 0;
                if(_doorStable>.4f && _owner.ScriptActions.Add("RE-01:door-closed"))
                    _owner.Session.TryRecordLearningAction("RE-01",ClinicalLearningAction.Operated,"door-closed");
                var label=_panel?_panel.transform.Find("DoorState"):null;
                if(label)label.GetComponent<TMP_Text>().text=_owner.ScriptActions.Contains("RE-01:door-closed")?"门体已闭合 · 已记录操作":"握住门把手，沿铰链转动关门";
            }
            // No distance gate, guide route tick or implicit object/camera movement.
            // Objects are recovered only by the explicit near-touch action.
        }
        internal void ShowScriptTask()
        {
            if(!InputAllowed)return;
            if(!_themeChosen && RoomId!="R02_STORAGE" && (RoomThemes().Length>0 || RoomId=="R03_WAITING"))
            {ShowRoomThemeSelector();return;}
            if(RoomId=="R02_STORAGE" && !_storageSampleChosen)
            {
                ShowStorageSampleSelector();
                return;
            }
            ClosePanel();_door=false;
            EnsureScriptTextMaterial();
            _scriptIndex=Mathf.Clamp(_scriptIndex,0,ScriptTasks.Length-1);
            EnsureStorageRegister();
            var id=ScriptTaskId;
            _owner.Session.TryRecordLearningAction(id,ClinicalLearningAction.Opened,"task");
            var task=_owner.Definition.FindTask(id);
            var details=Details(id,_owner.Session.Mode==ClinicalJourneyMode.GuidedLearning);
            _detailIndex=Mathf.Clamp(_detailIndex,0,details.Length-1);
            _owner.ScriptPositions[RoomId]=new Vector2Int(_scriptIndex,_detailIndex);
            _panel=new GameObject("StationaryScriptPanel",typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_panel.transform;
            board.sizeDelta=new Vector2(860,760);board.localScale=Vector3.one*.00065f;
            var canvas=_panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_viewer.GetComponent<Camera>();canvas.sortingOrder=130;
            EnsureScriptPose();
            board.SetPositionAndRotation(_scriptPose.position,_scriptPose.rotation);
            Fill(board,new Color(.96f,.98f,1),3);
            TMP_Text Text(string name,string copy,float y,float height,int size)
            {
                var t=Label(board,_font,name,0,y,800,height,size);t.fontSharedMaterial=_scriptTextMaterial;t.text=copy;t.color=new Color(.015f,.025f,.035f);return t;
            }
            Text("ScriptTitle",$"{_owner.Definition.FindRoom(RoomId).displayName}  {_scriptIndex+1}/{ScriptTasks.Length}\n{task.title}",304,100,29);
            var selectionAction=id=="OF-01"?"选择字段查看":id=="OF-02"?"选择使用记录查看":id=="ST-02"?"选择周记录查看":null;
            var status=selectionAction??(_owner.ScriptStepsViewed.Contains(id+":"+_detailIndex)?"已查阅":"待查阅");
            Text("ScriptStep",$"细项 {_detailIndex+1}/{details.Length} · {status}",225,42,21);
            var image=ImageFor(id,_detailIndex,_showScriptSource);
            if(id=="RE-03" && _detailIndex==2)
                Button(board,_font,"WatchSinkVideo","观看水槽示范 · 20秒",0,-80,500,70,()=>QueueStationaryAction(ShowSinkVideo),true);
            bool brushReference=id=="RE-02" && _detailIndex==2;
            if(id=="ST-01")
                Button(board,_font,"InspectStorageCabinet","检查柜体",165,225,130,52,()=>QueueStationaryAction(()=>ChangeStorageObservation(false)));
            if(id=="ST-02")
                Button(board,_font,"InspectStorageRegister","查看柜侧位置",270,225,250,52,()=>QueueStationaryAction(ChangeStorageRegisterObservation));
            if(id=="ST-02")
                Button(board,_font,"OpenStorageRecords","打开模拟登记表",0,-80,500,70,()=>QueueStationaryAction(ShowStorageRecords),true);
            if(id=="RE-05" && _detailIndex==1)
            {
                int document=ClinicalTrainingRecords.DocumentIndexForTask("OF-02");
                var linked=Button(board,_font,"OpenLinkedLeakRecords",document>=0?"对照办公室同源测漏资料":"同源测漏资料待补",0,-80,620,70,()=>QueueStationaryAction(()=>
                {
                    if(document<0)return;
                    ClosePanel();
                    if(_linkedRecords==null)_linkedRecords=new FullScriptOfficeRecords(_owner,this,_viewer,_font,LeaveIncompleteTasks,true);
                    _linkedRecords.BeginStationary(document);
                }),true);
                linked.interactable=document>=0;
            }
            if(id=="WT-01")
                Button(board,_font,"InspectEnvironment","收起面板观察环境",270,225,250,52,()=>QueueStationaryAction(()=>ChangeWaitingObservation(false)));
            bool useImage=!string.IsNullOrEmpty(image) && _owner.Session.Mode==ClinicalJourneyMode.GuidedLearning;
            var model=ModelFor(id);
            bool door=id=="RE-01" && _detailIndex==0;
            if(door){useImage=false;model=null;}
            Text("ScriptBody",_imageExpanded&&useImage?"":details[_detailIndex],useImage||model!=null||door?145:105,useImage||model!=null||door?105:270,24);
            if(useImage)
            {
                var texture=Resources.Load<Texture2D>(image);
                if(texture)
                {
                    bool generatedTeaching=id=="ST-01" || brushReference || !_showScriptSource && (id=="RE-02" || id=="RE-06");
                    float imageHeight=_imageExpanded?(generatedTeaching?380:430):245;
                    var picture=Rect(board,"ScriptReference",0,_imageExpanded?(generatedTeaching?-8:32):-28,760,imageHeight).gameObject.AddComponent<RawImage>();
                    picture.texture=texture;picture.raycastTarget=false;
                    // Crop the existing teaching photograph in the UI; do not redraw
                    // either brush or replace the original script's equipment image.
                    if(brushReference)
                    {
                        picture.uvRect=_brushView==1?new Rect(.77f,.18f,.22f,.55f):_brushView==2?new Rect(.43f,.20f,.40f,.53f):new Rect(0,0,1,1);
                        Button(board,_font,"BrushFocus",_brushView==0?"查看刷毛":_brushView==1?"查看芯线":"返回全图",165,225,130,52,()=>QueueStationaryAction(()=>{_brushView=(_brushView+1)%3;ShowScriptTask();}));
                    }
                    var ratio=texture.width*picture.uvRect.width/(texture.height*picture.uvRect.height);
                    picture.rectTransform.sizeDelta=ratio>760f/imageHeight?new Vector2(760,760/ratio):new Vector2(imageHeight*ratio,imageHeight);
                    if(generatedTeaching && _imageExpanded)Text("GeneratedReferenceOrigin","生成教学情境图 · 非实拍 / 非核查答案",194,22,17);
                    if(id=="RE-06")
                    {
                        var areas=_showScriptSource
                            ?new[]{new Rect(.445f,.26f,.115f,.385f),new Rect(.482f,.673f,.052f,.056f),new Rect(.478f,.705f,.055f,.035f),new Rect(.477f,.744f,.055f,.047f),new Rect(.445f,.424f,.115f,.078f),new Rect(.478f,.222f,.05f,.05f)}
                            :new[]{new Rect(.29f,.12f,.42f,.70f),new Rect(.41f,.828f,.15f,.065f),new Rect(.405f,.882f,.16f,.045f),new Rect(.395f,.925f,.19f,.06f),new Rect(.255f,.41f,.47f,.14f),new Rect(.38f,.025f,.23f,.09f)};
                        var area=areas[_detailIndex];var size=picture.rectTransform.sizeDelta;
                        var highlight=Rect(picture.transform,"PPEFocus",(area.center.x-.5f)*size.x,(area.center.y-.5f)*size.y,area.width*size.x,area.height*size.y);
                        Fill(highlight,new Color(1,.8f,0,.18f),1);var outline=highlight.gameObject.AddComponent<Outline>();outline.effectColor=new Color(1,.72f,0);outline.effectDistance=new Vector2(2,2);
                    }
                    Button(board,_font,"ExpandReference",_imageExpanded?"返回图解":"放大查看",322,225,150,52,()=>QueueStationaryAction(()=>
                    {
                        _owner.Session.TryRecordLearningAction(id,ClinicalLearningAction.Observed,"image:"+_detailIndex);
                        _imageExpanded=!_imageExpanded;ShowScriptTask();
                    }));
                    if((id=="RE-02" && !brushReference) || id=="RE-06")
                    {
                        Button(board,_font,"CompareScriptSource",_showScriptSource?"查看教学图":"对照原图",-292,225,180,52,
                            ()=>QueueStationaryAction(()=>{_showScriptSource=!_showScriptSource;ShowScriptTask();}));
                        var step=(RectTransform)board.Find("ScriptStep");step.anchoredPosition=new Vector2(0,225);step.sizeDelta=new Vector2(380,42);
                    }
                    if(id=="RE-02" && _detailIndex==0 && !_imageExpanded)
                        Button(board,_font,"InspectEquipmentImage","逐件查看图中设备",0,-180,500,52,
                            ()=>QueueStationaryAction(ShowEquipmentTeaching),true);
                }
                else Text("MissingReference","参考图暂不可用；可继续其余检查。",-20,170,24);
            }
            if(model!=null)
                Button(board,_font,"InspectObject","收起说明，检查实物",0,-28,500,90,()=>QueueStationaryAction(ShowScriptObjectInspection),true);
            if(door)
            {
                OpenScriptDoor();
                var label=Text("DoorState","握住门把手，沿铰链转动关门",-30,130,23);
                label.rectTransform.anchoredPosition=new Vector2(175,-30);label.rectTransform.sizeDelta=new Vector2(380,130);
            }
            if((RoomId=="R04_GI" || RoomId=="R04_RESP") && id.StartsWith("CL-03"))
            {
                var rows=ClinicalTrainingRecords.Query(room:RoomId=="R04_GI"?"GI":"RESP");
                Text("SameSourceHistory",string.Join("\n",rows.Select(r=>$"{r.Id}  {r.Date}  {r.Scope}\n{r.Start}—{r.End}  {r.Patient}")),15,180,23);
            }
            var reason=_owner.Session.IsContentAvailable(id)?"":"本项资料尚不齐全，可先查看已有内容。";
            Text("Availability",((id=="ST-01" || brushReference) && useImage && _imageExpanded) || id=="RE-02" && _detailIndex==0?"":reason,-173,75,19);
            if(id=="ST-02" || useImage && (id=="ST-01" || brushReference))
            {
                var step=(RectTransform)board.Find("ScriptStep");step.anchoredPosition=new Vector2(-150,225);step.sizeDelta=new Vector2(480,42);
            }
            void Action(string name,string caption,float x,float y,float width,System.Action action)
                => Button(board,_font,name,caption,x,y,width,58,()=>QueueStationaryAction(action),true);
            bool selectionTracksView=id=="OF-01" || id=="OF-02" || id=="ST-02";
            float detailActionX=selectionTracksView?185f:275f;
            float detailActionWidth=selectionTracksView?340f:240f;
            Action("PreviousDetail","上一细项",-detailActionX,-239,detailActionWidth,()=>{_detailIndex=Mathf.Max(0,_detailIndex-1);_brushView=0;_imageExpanded=false;ShowScriptTask();});
            if(!selectionTracksView)
                Action("ReadDetail","请安小卫提示",0,-239,240,()=>
                {
                    _owner.Session.TryRecordLearningAction(id,ClinicalLearningAction.Hint,"detail:"+_detailIndex);
                    ShowTeaching(new FullScriptGuideCopy(task.title,details[_detailIndex],"返回检查"),ShowScriptTask);
                });
            Action("NextDetail","下一细项",detailActionX,-239,detailActionWidth,()=>{_detailIndex=Mathf.Min(details.Length-1,_detailIndex+1);_brushView=0;_imageExpanded=false;ShowScriptTask();});
            if(RoomId=="R02_STORAGE")Action("PreviousTask","上一检查点",-275,-310,240,()=>ChangeScriptTask(Mathf.Max(0,_scriptIndex-1)));
            else Action("ChooseRoomTheme","选择检查内容",-275,-310,240,()=>ShowRoomThemeSelector());
            Action("NextTask",_scriptIndex==ScriptTasks.Length-1?"本室小结 / 切房":"下一检查点",275,-310,240,()=>
            {
                if(_scriptIndex==ScriptTasks.Length-1)ContinueToDoor();
                else ChangeScriptTask(_scriptIndex+1);
            });
            if(_office!=null)
            {
                var document=ClinicalTrainingRecords.DocumentIndexForTask(id);
                bool available=id=="OF-01" || document>=0;
                Action("OpenCurrentDocument",id=="OF-01"?"打开电脑记录":available?"打开对应资料":"对应资料待补",0,-310,240,()=>
                {if(!available)return;ClosePanel();_office.BeginStationary(id=="OF-01"?-1:document);});
                board.Find("OpenCurrentDocument").GetComponent<Button>().interactable=available;
            }
            else if(RoomId=="R02_STORAGE")
                Action("ChooseStorageSample","选择检查内容",0,-310,240,ShowStorageSampleSelector);
            else Action("SwitchRoom","暂留 / 选择房间",0,-310,240,ContinueToDoor);
            ClinicalNearTouch.Bind(board,()=>InputAllowed);
            var documentButton=board.Find("OpenCurrentDocument")?.GetComponent<Button>();
            var primaryActionName=documentButton!=null && documentButton.interactable?documentButton.name:"NextTask";
            foreach(var button in board.GetComponentsInChildren<Button>())
            {
                var label=button.GetComponentInChildren<TMP_Text>();label.fontSharedMaterial=_scriptTextMaterial;
                EmphasizeButton(button,false,button.name==primaryActionName);
            }
        }
        void ShowStorageSampleSelector()
        {
            if(!InputAllowed || RoomId!="R02_STORAGE")return;
            ClosePanel();_door=false;
            EnsureScriptTextMaterial();EnsureScriptPose();
            _panel=new GameObject("StorageSampleSelector",typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_panel.transform;
            board.sizeDelta=new Vector2(860,760);board.localScale=Vector3.one*.00065f;
            var canvas=_panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_viewer.GetComponent<Camera>();canvas.sortingOrder=130;
            board.SetPositionAndRotation(_scriptPose.position,_scriptPose.rotation);
            Fill(board,new Color(.96f,.98f,1),3);
            TMP_Text Copy(string name,string copy,float x,float y,float width,float height,int size)
            {
                var t=Label(board,_font,name,x,y,width,height,size);t.fontSharedMaterial=_scriptTextMaterial;
                t.text=copy;t.color=new Color(.015f,.025f,.035f);return t;
            }
            Copy("StorageSampleTitle","储存库 · 选择检查内容",0,290,780,76,31);
            Copy("StorageSampleIntro","可以先从柜体开始，也可以直接查看登记表或胃镜副本。",0,198,760,62,23);
            Button(board,_font,"StorageSample_ST-01","检查柜体",0,78,240,90,
                ()=>QueueStationaryAction(()=>SelectStorageSample("ST-01")),true);
            Button(board,_font,"StorageSample_ST-02","查看登记表",-278,48,240,90,
                ()=>QueueStationaryAction(()=>SelectStorageSample("ST-02")));
            Button(board,_font,"StorageSample_ST-03","观察胃镜副本",278,48,240,90,
                ()=>QueueStationaryAction(()=>SelectStorageSample("ST-03")));
            Copy("StorageSampleCabinetCopy","打开柜门，观察现有柜体",0,-5,250,54,19);
            Copy("StorageSampleRegisterCopy","逐周查看模拟清洁登记",-278,-38,250,62,19);
            Copy("StorageSampleGastroscopeCopy","胃镜为平放副本；悬挂检查暂不可用。",278,-48,250,78,18);
            Copy("StorageSampleAvailability","选项只打开相应内容；查看不会自动完成检查。",0,-134,760,44,20);
            Button(board,_font,"StorageSampleReturn","暂留 / 选择房间",0,-310,260,60,
                ()=>QueueStationaryAction(ContinueToDoor));
            ClinicalNearTouch.Bind(board,()=>InputAllowed);
            foreach(var button in board.GetComponentsInChildren<Button>())
            {
                var label=button.GetComponentInChildren<TMP_Text>();label.fontSharedMaterial=_scriptTextMaterial;
                EmphasizeButton(button,false,button.name=="StorageSample_ST-01");
            }
        }
        void SelectStorageSample(string taskId)
        {
            if(!InputAllowed || RoomId!="R02_STORAGE")return;
            int index=Array.IndexOf(ScriptTasks,taskId);
            if(index<0)return;
            _storageSampleChosen=true;
            ChangeScriptTask(index);
        }
        void EnsureScriptTextMaterial()
        {
            if(_scriptTextMaterial)return;
            _scriptTextMaterial=new Material(_font.material);
            _scriptTextMaterial.SetFloat("_OutlineWidth",0);_scriptTextMaterial.SetColor("_FaceColor",Color.white);
            _scriptTextMaterial.DisableKeyword("UNDERLAY_ON");
        }
        void EnsureScriptPose()
        {
            if(_hasScriptPose)return;
            var forward=Vector3.ProjectOnPlane(_viewer.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
            _scriptPose=new Pose(_viewer.position+forward*.52f-Vector3.up*.15f,Quaternion.LookRotation(forward));
            _hasScriptPose=true;
        }
        void ChangeScriptTask(int index)
        {
            var id=ScriptTasks[index];
            if(_owner.RequestInspectionPoint(id,()=>{_scriptIndex=index;_detailIndex=0;_imageExpanded=false;_brushView=0;ShowScriptTask();}))ClosePanel();
        }
        void ShowEnvironmentView()
        {
            if(!InputAllowed)return;
            ClosePanel();
            _panel=new GameObject("StationaryRoomObservation",typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_panel.transform;
            board.sizeDelta=new Vector2(800,150);board.localScale=Vector3.one*.00065f;
            board.SetPositionAndRotation(_scriptPose.position-Vector3.up*.22f,_scriptPose.rotation);
            var canvas=_panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_viewer.GetComponent<Camera>();
            Fill(board,new Color(.96f,.98f,1),2);
            var caption=Label(board,_font,"ObservationSide",0,47,760,36,23);
            caption.fontSharedMaterial=_scriptTextMaterial;caption.color=new Color(.015f,.025f,.035f);
            caption.text=_waitingCorridor?"诊疗通道侧 · 原位转头观察":"候诊侧 · 原位转头观察";
            var switchButton=Button(board,_font,"SwitchObservationSide",_waitingCorridor?"切到候诊侧":"切到诊疗通道侧",-193,-22,370,70,()=>QueueStationaryAction(()=>ChangeWaitingObservation(!_waitingCorridor)),true);
            var returnButton=Button(board,_font,"ReturnToInspection","返回检查说明",193,-22,370,70,()=>QueueStationaryAction(ShowScriptTask),true);
            switchButton.GetComponentInChildren<TMP_Text>().fontSharedMaterial=_scriptTextMaterial;
            returnButton.GetComponentInChildren<TMP_Text>().fontSharedMaterial=_scriptTextMaterial;
            EmphasizeButton(switchButton,false,false);
            EmphasizeButton(returnButton,false,false);
            ClinicalNearTouch.Bind(board,()=>InputAllowed);
        }
        void ChangeWaitingObservation(bool corridor)
        {
            if(_owner.RequestWaitingObservation(corridor,()=>{_waitingCorridor=corridor;ShowEnvironmentView();}))ClosePanel();
        }
        void ShowScriptObjectInspection()
        {
            if(!InputAllowed)return;
            var resource=ModelFor(ScriptTaskId);if(resource==null)return;
            ClosePanel();
            OpenScriptModel(resource);
            _panel=new GameObject("ScriptObjectObservation",typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_panel.transform;board.sizeDelta=new Vector2(760,180);board.localScale=Vector3.one*.00065f;
            board.SetPositionAndRotation(_scriptPose.position-Vector3.up*.17f,_scriptPose.rotation);
            var canvas=_panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_viewer.GetComponent<Camera>();
            Fill(board,new Color(.96f,.98f,1),2);
            var caption=Label(board,_font,"ObjectObservationStatus",0,40,720,75,22);caption.fontSharedMaterial=_scriptTextMaterial;caption.color=new Color(.015f,.025f,.035f);
            caption.text=!_scriptObject?"模型暂不可用，请返回继续其他检查":
                (_owner.Session.IsFinished?"只读回看 · ":"可用手拿起转看 · ")+
                (ScriptTaskId=="ST-03"?"原胃镜观察副本，非悬挂状态":"原瓶体与标签，非产品合规结论")+"\n展示尺寸不代表实物尺寸；观察不自动完成核查";
            var recover=Button(board,_font,"RecoverObject","取回观察物",-185,-48,350,60,()=>QueueStationaryAction(RecoverScriptObject),true);
            var back=Button(board,_font,"ReturnToInspection","返回检查说明",185,-48,350,60,()=>QueueStationaryAction(ShowScriptTask),true);
            foreach(var button in new[]{recover,back})
            {button.GetComponentInChildren<TMP_Text>().fontSharedMaterial=_scriptTextMaterial;EmphasizeButton(button,false,false);}
            recover.interactable=_scriptObject && !_owner.Session.IsFinished;
            ClinicalNearTouch.Bind(board,()=>InputAllowed);
        }
        void RecoverScriptObject()
        {
            if(!InputAllowed || !_scriptObject || _owner.Session.IsFinished)return;
            _scriptObject.SetActive(false);
            _scriptObject.transform.SetPositionAndRotation(_scriptObjectHome.position,_scriptObjectHome.rotation);
            _scriptObject.SetActive(true);
        }
        void OpenScriptModel(string resource)
        {
            var asset=Resources.Load<GameObject>(resource);
            if(!asset)return;
            _scriptObject=new GameObject("ScriptInspectableModel");_scriptObject.SetActive(false);
            var visual=UnityEngine.Object.Instantiate(asset,_scriptObject.transform,false);
            var renderers=visual.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0){CloseScriptObject();return;}
            var bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            float scale=.38f/Mathf.Max(.001f,size);
            visual.transform.localScale*=scale;
            visual.transform.localPosition-=bounds.center*scale;
            _scriptObjectHome=new Pose(_scriptPose.position+_scriptPose.rotation*new Vector3(0,.17f,-.04f),
                _scriptPose.rotation*(ScriptTaskId=="ST-03"?Quaternion.Euler(-90,0,0):Quaternion.Euler(0,180,0)));
            _scriptObject.transform.SetPositionAndRotation(_scriptObjectHome.position,_scriptObjectHome.rotation);
            var collider=_scriptObject.AddComponent<BoxCollider>();collider.size=bounds.size*scale;
            var body=_scriptObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
            var grab=_scriptObject.AddComponent<Grabbable>();grab.InjectOptionalRigidbody(body);grab.InjectOptionalThrowWhenUnselected(false);
            var task=ScriptTaskId;
            _learningGrab=grab;
            _learningGrabHandler=evt=>
            {
                if(evt.Type==PointerEventType.Select && InputAllowed && ScriptTaskId==task)
                    _owner.Session.TryRecordLearningAction(task,ClinicalLearningAction.Operated,"grab-object");
            };
            grab.WhenPointerEventRaised+=_learningGrabHandler;
            var hand=_scriptObject.AddComponent<HandGrabInteractable>();hand.InjectRigidbody(body);hand.InjectOptionalPointableElement(grab);hand.HandAlignment=HandAlignType.None;
            hand.enabled=grab.enabled=!_owner.Session.IsFinished;
            _scriptObject.SetActive(true);
        }
        void CloseScriptObject()
        {
            if(_learningGrab && _learningGrabHandler!=null)_learningGrab.WhenPointerEventRaised-=_learningGrabHandler;
            _learningGrab=null;_learningGrabHandler=null;
            _doorLeaf=null;_doorStable=0;
            if(_doorMaterial){DestroyScriptAsset(_doorMaterial);_doorMaterial=null;}
            if(!_scriptObject)return;
            _scriptObject.SetActive(false);
            if(UnityEngine.Application.isPlaying)UnityEngine.Object.Destroy(_scriptObject);else UnityEngine.Object.DestroyImmediate(_scriptObject);
            _scriptObject=null;
        }
        void DisposeScript()
        {
            _pendingScriptAction=null;CloseScriptObject();DisposeStorageRegister();
            if(_scriptTextMaterial){DestroyScriptAsset(_scriptTextMaterial);_scriptTextMaterial=null;}
        }
        static void DestroyScriptAsset(UnityEngine.Object asset)
        {if(UnityEngine.Application.isPlaying)UnityEngine.Object.Destroy(asset);else UnityEngine.Object.DestroyImmediate(asset);}
        void OpenScriptDoor()
        {
            var prefab=Resources.Load<GameObject>("FullScriptRooms/Stationary/Door342041");
            if(!prefab)return;
            _scriptObject=UnityEngine.Object.Instantiate(prefab);_scriptObject.SetActive(false);
            _scriptObject.transform.SetPositionAndRotation(_scriptPose.position+_scriptPose.rotation*new Vector3(-.08f,-.12f,-.08f),_scriptPose.rotation);
            _scriptObject.transform.localScale=Vector3.one*.09f;
            _doorLeaf=_scriptObject.transform.Find("Leaf");
            _doorMaterial=new Material(Resources.Load<Material>("EndoscopyRoom/RoomSurface"));
            _doorMaterial.color=Color.white;_doorMaterial.mainTexture=Resources.Load<Texture2D>("ClinicalCourse/FullScriptVisuals/office-oak-v1");
            var doorRenderer=_doorLeaf.GetComponent<MeshRenderer>();doorRenderer.sharedMaterials=Enumerable.Repeat(_doorMaterial,doorRenderer.sharedMaterials.Length).ToArray();
            _doorLeaf.localRotation=Quaternion.Euler(0,_owner.ScriptActions.Contains("RE-01:door-closed")?0:45,0);
            if(!_owner.Session.IsFinished)
            {
                var handle=_doorLeaf.gameObject.AddComponent<BoxCollider>();handle.center=new Vector3(-.78f,1.1f,-.06f);handle.size=new Vector3(.3f,.32f,.3f);
                var body=_doorLeaf.gameObject.AddComponent<Rigidbody>();body.useGravity=false;body.isKinematic=true;
                var grab=_doorLeaf.gameObject.AddComponent<Grabbable>();grab.InjectOptionalRigidbody(body);grab.InjectOptionalThrowWhenUnselected(false);
                var rotate=_doorLeaf.gameObject.AddComponent<OneGrabRotateTransformer>();
                rotate.InjectOptionalPivotTransform(_scriptObject.transform);
                float initial=_doorLeaf.localEulerAngles.y;
                rotate.InjectOptionalConstraints(new OneGrabRotateTransformer.OneGrabRotateConstraints
                {MinAngle=new FloatConstraint{Constrain=true,Value=-initial},MaxAngle=new FloatConstraint{Constrain=true,Value=100-initial}});
                grab.InjectOptionalOneGrabTransformer(rotate);grab.InjectOptionalTwoGrabTransformer(rotate);
                var hand=_doorLeaf.gameObject.AddComponent<HandGrabInteractable>();hand.InjectRigidbody(body);hand.InjectOptionalPointableElement(grab);hand.HandAlignment=HandAlignType.None;
            }
            _scriptObject.SetActive(true);
        }
        static string ModelFor(string id)=>id=="ST-03"?"FullScriptRooms/Stationary/Gastroscope":id=="RE-04"?"ClinicalCourse/Disinfectant/disinfectant-labeled":null;
        static string ImageFor(string id,int detail,bool source)=>id=="ST-01"?"ClinicalCourse/FullScriptVisuals/storage-cabinet-teaching-v1":id=="RE-01"?"ClinicalEvidence/door":id=="RE-02"?(detail==2?"ClinicalCourse/brush-inspection":"ClinicalCourse/FullScriptVisuals/"+(source?"script-equipment":"equipment-hotspot-teaching-v1")):id=="RE-06"?"ClinicalCourse/FullScriptVisuals/"+(source?"script-ppe":"ppe-teaching-plate-v2"):null;
        internal static string[] Details(string id,bool guided=true)
        {
            switch(id)
            {
                case "OF-00":return new[]{"先观察办公室的桌、电脑、文件及柜门状态。现在在固定工位原地检查；需要阅读时近触打开资料。家具外观不代表资料齐全。"};
                case "OF-01":return new[]{"近触打开电脑，以日期、镜号查询完整记录。逐项核对诊疗日期、患者标识、内镜编号、开始时间、结束时间、操作人员。","同时查看完整样例和待核查记录；从全部记录范围确认空白和关联关系，记录自己的判断，最后回办公室统一提交。"};
                case "OF-02":return new[]{"打开测漏资料，将给定使用清单与逐次登记对应。核对镜号、日期、操作与结果；找不到正文的条目保持待补，不把打开目录当核查完成。"};
                case "OF-03":return new[]{"打开生物学监测资料。核对对象、日期、结果和适用依据，关联现场设备。现有模拟资料用于字段阅读，不证明真实监测合格。"};
                case "OF-04":return new[]{"打开消毒剂监测和产品档案，与洗消室同一产品的瓶体、说明书核对。身份、用途、有效期、监测资料分别查；参数未核验时不下合规结论。"};
                case "OF-05":return new[]{"检查培训档案及六类资料目录，逐类打开正文。区分资料存在、内容完整、对象匹配及依据有效；未提供的正文保留为待补内容。"};
                case "ST-01":return guided
                    ?new[]{"生成教学图（非实拍）：观察柜门、密封边与通风开孔。近触“检查柜体”可握住三维柜门把手展开，查看内壁；外观不证明通风性能或管腔干燥。","生成教学情境图（非实拍）：观察三支内镜分别悬挂、插入管向下且末端离开柜底。三维悬挂镜体仍待补，盘绕模型不作为竖直悬挂示范。"}
                    :new[]{"近触“检查柜体”，握住把手展开柜门，观察内壁和通风构造。悬挂镜体尚未齐备，不以开柜动作代替完整判断。","核对内镜的悬挂状态。现有盘绕胃镜只用于表面特写，不作为竖直悬挂示范。"};
                case "ST-02":return new[]{"打开模拟柜体清洁登记，逐周核对完整范围、日期、对象、登记内容和操作人。登记缺项不等于已证实未清洁；本训练的每周要求来自剧本情境，不作为临床标准。"};
                case "ST-03":return new[]{"近触“检查实物”收起说明，可用手拿起原胃镜观察副本，转看表面和连接部；移远时主动取回。展示比例不是实物尺寸，不代表悬挂状态，也没有已核验的污渍情境。"};
                case "WT-01":return new[]{"先收起面板，保持原位，转头观察座椅、实体隔断和两侧入口。","查看隔断是否连续、座椅是否占用通道，再比较两侧入口的关系。需要换一侧观察时，轻触对应按钮，无需走动。"};
                case "RE-01":return new[]{"先看空间隔断，再看关闭措施。前方为本室门体操作模型。握住把手转动门扇；只有实际闭合才记录操作。","核对清洗、漂洗、消毒、终末漂洗、干燥的工位关系与单向衔接。"};
                case "RE-02":return new[]{"在教学图上逐件查看气枪、测漏仪和水枪，并对照剧本原图。其他设备及型号、参数须另查实物与原厂资料；图片不能证明配置齐全。","接着查看运送容器与毛刷，核对用途、外观及对应说明。图中未展示的器械仍需现场确认。",guided?"生成教学图（非实拍）：比较上下两把毛刷的刷毛与芯线，可切换局部特写或放大。它不是本次现场实物，也不能证明工具配置齐全。":"检查现场毛刷的刷毛与芯线，分别记录观察依据。现场实物与独立情境仍待补，不展示带教示范图作为本次核查证据。"};
                case "RE-03":return new[]{"沿水源、供水管道、滤膜外壳、设备型号及说明书核查。剧本的 ≤0.2 μm 参数仍需结合适用依据和具体产品资料核验。","打开参数和更换记录，核对设备编号、孔径、日期与人员。尚缺的实际外壳和完整记录不会自动打勾。","观看水槽操作示范，留意操作顺序。视频保留原声，可暂停或重看；随后继续核对本室供水与设备资料。"};
                case "RE-06":return new[]{"生成教学示意图：观察躯干防护衣的覆盖范围；是否需要防水围裙还应结合具体操作与用品资料。可切换剧本原图对照。","生成教学示意图：观察口罩对口鼻的覆盖；型号与适用性应查产品资料。","生成教学示意图：观察护目镜对眼部的覆盖，与口罩分开检查。","生成教学示意图：观察帽子对头发的覆盖。","生成教学示意图：分别查看双手手套和袖口衔接。","生成教学示意图：观察足部覆盖，并对照现场专用鞋要求；图示不能替代实物核对。"};
                case "RE-04":return new[]{"近触“检查实物”收起说明，再抓取已有带标签消毒剂模型，转看瓶体与标签；移远时主动取回。保留原材质和贴图，展示比例不是实物尺寸；模型外观不是备案或适用性证明。","回查办公室同源产品档案，核对身份、用途、说明与监测资料。未核验的内容不转成合规成绩。"};
                case "RE-05":return new[]{"附件包装：核对存放盒、附件、包装与完整标识。已有胃镜本体不能替代附件资产。","逐次测漏：对应给定使用清单、镜号与本次登记；与办公室关联。附件和记录是两个子项，不能一次确认同时签收。"};
                default:
                    if(id.StartsWith("CL-01"))return new[]{"观察本室用途、床、设备台与内镜主机。消化和呼吸诊疗室分别加载、分别记录；同源模型不代表共用设备或相同核查结论。"};
                    if(id.StartsWith("CL-02"))return new[]{"核对本室环境监测报告的场所、日期、结果及适用依据。实际报告尚未提供，不能用另一间报告或空白封面代替。"};
                    if(id.StartsWith("CL-03"))return new[]{"下方为本室历史模拟记录，与办公室查询同源。核对镜号和起止时间；具体产品条件未核验，不用通用分钟数判定合格。"};
                    return new[]{"分别检查台面耗材 / 器械分区与废物桶盖状态。两项分别记录；对象或状态未绑定的部分明确待补。"};
            }
        }
    }
}
