using System;
using System.Linq;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.VisitorPrologue.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed class FullScriptRoomVisit : IDisposable
    {
        readonly FullScriptJourneyRuntime _owner;
        readonly Transform _viewer;
        readonly TMP_FontAsset _font;
        GameObject _panel;
        readonly FullScriptOfficeRecords _office;
        bool _disposed, _door,_clinicalRecords;
        int _summaryPage=-1;
        int _recordReviewPage=-1;
        internal string RoomId { get; }
        internal MapDefinition Map { get; }
        internal VirtualRoomEnvironment Room { get; }
        internal VisitorPrologueController Prologue => _owner.Prologue;
        internal long CompletionRevision { get; private set; }
        internal void AcceptTeachingReviewClosed(SessionToken session){if(!_disposed)CompletionRevision++;}
        internal bool ContentOpen => (_panel && _panel.activeSelf) || _office?.IsOpen == true;
        internal bool DoorOpen => ContentOpen && _door;
        internal bool InputAllowed => !_disposed && _owner.InputAllowed;
        internal ClinicalCourseSession ResumeWashingLesson(ClinicalCourseLesson lesson) => _owner.ResumeWashingLesson(lesson);
        internal bool TeachingReadOnly => _owner.Session.IsFinished;
        internal BotanicalGardenQR.FrontendShell.Contracts.ClinicalObservationProgress ObservationProgress => _owner.ObservationProgress;
        internal GameObject Panel => _panel ? _panel : _office?.Panel;
        internal GameObject OfficeTerminal => _office?.Terminal;
        internal bool AtDoor
        {
            get
            {
                var p = Room.Frame.Transform(Map.start);
                var delta = _viewer.position - new Vector3(p.x, _viewer.position.y, p.z);
                return delta.sqrMagnitude <= .85f * .85f;
            }
        }

        internal FullScriptRoomVisit(FullScriptJourneyRuntime owner, string id, MapDefinition map,
            VirtualRoomEnvironment room, Transform viewer, TMP_FontAsset font)
        {
            _owner=owner; RoomId=id; Map=map; Room=room; _viewer=viewer; _font=font;
            if(id=="R01_OFFICE") _office=new FullScriptOfficeRecords(owner,this,viewer,font,LeaveIncompleteTasks);
            if(id=="R04_GI" || id=="R04_RESP")
            {
                var sign=new GameObject("ClinicalRoomIdentity",typeof(RectTransform),typeof(Canvas));
                sign.transform.SetParent(room.Root.transform,false);sign.transform.localPosition=new Vector3(-1.866f,2.4f,-1.82f);
                sign.transform.localRotation=Quaternion.Euler(0,180,0);sign.transform.localScale=Vector3.one*.0007f;
                var rect=(RectTransform)sign.transform;rect.sizeDelta=new Vector2(800,110);sign.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
                Fill(rect,Color.white,3);var label=Label(rect,font,"Identity",0,0,760,85,32);label.color=new Color(.08f,.23f,.37f);
                label.text=owner.Definition.FindRoom(id).displayName;label.alignment=TextAlignmentOptions.Center;
            }
            if(map.roomResource==FullScriptRoomCatalog.DevelopmentResource)
            {
                var sign=new GameObject("RoomName",typeof(RectTransform),typeof(Canvas));
                sign.transform.SetParent(room.Root.transform,false);
                sign.transform.localPosition=new Vector3(-4.9f,2.2f,0);
                sign.transform.localRotation=Quaternion.Euler(0,-90,0);
                sign.transform.localScale=Vector3.one*.002f;
                var rect=(RectTransform)sign.transform;rect.sizeDelta=new Vector2(950,170);
                sign.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
                Frame(rect);
                var text=Label(rect,font,"RoomNameText",0,0,910,140,38);
                text.alignment=TextAlignmentOptions.Center;
                text.text=owner.Definition.FindRoom(id).displayName+"\n<size=24>开发灰盒 · 非最终场地</size>";
            }
        }

        internal bool TryOpen(string point)
        {
            if (!InputAllowed) return false;
            if (point != FullScriptRoomCatalog.Overview && point != FullScriptRoomCatalog.Door) return false;
            if (point == FullScriptRoomCatalog.Door && !AtDoor) return false;
            if(point==FullScriptRoomCatalog.Overview && _office!=null && !_owner.Session.HasVisitedAllMainlineRooms)
            { _door=false;_office.Begin();return true; }
            Show(point == FullScriptRoomCatalog.Door);
            return true;
        }

        void Show(bool door)
        {
            ClosePanel(); _door=door;
            _panel = new GameObject("FullScriptRoomPanel",typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_panel.transform;
            board.localScale=Vector3.one*.00075f;
            board.sizeDelta=new Vector2(840,650);
            var canvas=_panel.GetComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace;
            canvas.worldCamera=_viewer.GetComponent<Camera>(); canvas.sortingOrder=120;
            if (door)
            {
                var p=Room.Frame.Transform(new MapPosition(Map.start.x+.45f,Map.start.y+1.25f,Map.start.z));
                board.SetPositionAndRotation(new Vector3(p.x,p.y,p.z),Quaternion.Euler(0,Room.Frame.YawDegrees+90,0));
            }
            else
                board.SetPositionAndRotation(_viewer.position+_viewer.forward*.55f-Vector3.up*.18f,_viewer.rotation);
            Frame(board);
            board.GetComponent<Image>().color=new Color(.055f,.075f,.073f,1);
            Label(board,_font,"RoomTitle",0,275,770,55,32).text=_owner.Definition.FindRoom(RoomId).displayName;
            var body=Label(board,_font,"RoomBrief",0,120,770,235,25);
            if (door)
            {
                body.text="请停在门口，近触选择目的房间。\n房间内请实际走动；转场期间请原地等待。";
                var targets=_owner.Destinations().ToArray();
                for(var i=0;i<targets.Length;i++)
                {
                    var target=targets[i]; var column=i%2; var row=i/2;
                    Button(board,_font,"Travel_"+target,"前往"+_owner.Definition.FindRoom(target).displayName,
                        column==0?-192:192,-45-row*75,360,62,()=>
                        { if(InputAllowed && AtDoor) _owner.RequestRoom(target); },true);
                }
            }
            else if (RoomId=="R00_LOBBY")
            {
                body.text="卫蓝行动 · 内镜中心监督检查\n大厅 → 办公室 → 储存库 → 候诊区 → 消化诊疗室 → 呼吸诊疗室 → 洗消室 → 办公室汇总\n模型与正式资料逐项接入中；缺失内容单列。";
                Button(board,_font,"ModeGuided","带教学习",-195,-60,360,70,()=>StartMode(ClinicalJourneyMode.GuidedLearning),true);
                Button(board,_font,"ModeIndependent","独立核查",195,-60,360,70,()=>StartMode(ClinicalJourneyMode.IndependentCheck));
            }
            else if(RoomId==_owner.Definition.summaryRoomId && _owner.Session.HasVisitedAllMainlineRooms)
            {
                body.fontSize=22;
                Button details=null,recordReview=null,submit=null;
                void RefreshSummary()
                {
                    var review=ClinicalRecordReview.AfterSubmission(_owner.Session);
                    body.text=_recordReviewPage>=0 && _recordReviewPage<review.Length
                        ? "模拟记录逐字段复盘 · "+(_recordReviewPage+1)+"/"+review.Length+"\n\n"+review[_recordReviewPage]+"\n\n仅核对模拟记录完整性，不代表医疗合规结论。"
                        : _summaryPage<0?Summary():SummaryRoom(_summaryPage);
                    recordReview.gameObject.SetActive(review.Length>0);
                    submit.interactable=!_owner.Session.IsFinished;
                    submit.GetComponentInChildren<TMP_Text>().text=_owner.Session.IsFinished?"已提交 · 只读":"确认结束本次检查";
                }
                details=Button(board,_font,"ResultDetails","逐项查看记录 →",-195,-35,370,60,()=>
                {if(InputAllowed){_recordReviewPage=-1;_summaryPage++;if(_summaryPage>=_owner.Definition.rooms.Sum(r=>r.taskIds.Length)+6)_summaryPage=-1;RefreshSummary();}});
                recordReview=Button(board,_font,"RecordReview","逐字段复盘 →",195,-35,370,60,()=>
                {
                    if(!InputAllowed)return;
                    var review=ClinicalRecordReview.AfterSubmission(_owner.Session);
                    if(review.Length==0)return;
                    _recordReviewPage=(_recordReviewPage+1)%review.Length;RefreshSummary();
                });
                submit=Button(board,_font,"SubmitJourney","确认结束本次检查",0,-110,600,65,()=>
                {
                    if(!InputAllowed || _owner.Session.IsFinished) return;
                    if(_owner.Session.Mode==ClinicalJourneyMode.IndependentCheck) _owner.Session.TrySubmitIndependentAtSummary();
                    else _owner.Session.TryFinishGuidedAtSummary();
                    RefreshSummary();
                },true);
                RefreshSummary();
                Button(board,_font,"ReviewRooms","前往门口回查",-195,-195,370,65,ContinueToDoor);
                Button(board,_font,"ReviewOfficeRecords","回看本室电脑",195,-195,370,65,()=>
                {if(InputAllowed){ClosePanel();_office.Begin();}});
            }
            else
            {
                body.text=Brief(RoomId)+"\n\n正式任务/证据尚未全部接入。缺失内容单列暂不可用；前往房门不会记成学员跳过或答错。";
                if(RoomId=="R04_GI" || RoomId=="R04_RESP")
                {
                    if(_clinicalRecords)
                    {
                        body.fontSize=22;
                        body.text="本室历史 · 模拟训练数据\n"+string.Join("\n",ClinicalTrainingRecords.Query(room:RoomId=="R04_GI"?"GI":"RESP").Select(r=>$"{r.Id} / {r.Scope}\n{r.Date}　{r.Start}—{r.End}　{r.Patient}"))+"\n与办公室同源；产品要求待核验，不按示例时长判定合规。";
                    }
                    Button(board,_font,"ClinicalHistory",_clinicalRecords?"返回本室检查范围":"查询本室历史记录",0,-35,690,60,()=>{if(InputAllowed){_clinicalRecords=!_clinicalRecords;Show(false);}});
                }
                Button(board,_font,"ContinueRoom","保留未完成项，前往房门",0,-100,690,75,LeaveIncompleteTasks,true);
            }
            Label(board,_font,"WalkingHint",0,-282,770,48,20).text=door?"走到门口才能切换 · 真实手部近触":"跟随精灵沿地面路线走到房门 · 每次重启新进度";
            ClinicalNearTouch.Bind(board,()=>InputAllowed && (!_door || AtDoor));
            foreach(var button in board.GetComponentsInChildren<Button>())
                EmphasizeButton(button,false,button.name.StartsWith("Travel_") || button.name=="SubmitJourney" || button.name=="ModeGuided");
        }

        void StartMode(ClinicalJourneyMode mode)
        {
            if(!InputAllowed) return;
            _owner.SelectMode(mode);
            _owner.Session.TryCompleteInstruction("N00");
            ContinueToDoor();
        }
        void ContinueToDoor()
        {
            if(!InputAllowed) return;
            ClosePanel(); CompletionRevision++;
        }
        void LeaveIncompleteTasks()
        {
            if(!InputAllowed)return;
            // Leaving a room is neither explicit task skipping nor a judgement.
            // Missing content is tracked by availability, independently of learner progress.
            ContinueToDoor();
        }
        string Summary()
        {
            int completed=0,skipped=0,unanswered=0,unavailable=0;
            foreach(var room in _owner.Definition.rooms)
                foreach(var id in room.taskIds)
                {
                    if(!_owner.Session.IsContentAvailable(id)){unavailable++;continue;}
                    _owner.Session.TryGetTask(id,out var state);
                    if(state.Status==ClinicalJourneyTaskStatus.Completed) completed++;
                    else if(state.Status==ClinicalJourneyTaskStatus.Skipped) skipped++;
                    else unanswered++;
                }
            return $"本次记录：完成 {completed} 项，主动跳过 {skipped} 项，未答/未开始 {unanswered} 项。\n暂不可用 {unavailable} 项（不计正确或错误）；不生成合规成绩。\n"+
                (_owner.Session.IsFinished?"已提交，可回看房间，不能修改本次记录。":"可继续到门口回查；确认结束后本次记录只读。");
        }
        string SummaryRoom(int index)
        {
            foreach(var room in _owner.Definition.rooms)
            {
                if(index>=room.taskIds.Length){index-=room.taskIds.Length;continue;}
                var id=room.taskIds[index];
                var definition=_owner.Definition.FindTask(id);
                var heading=room.displayName+" · "+(index+1)+"/"+room.taskIds.Length+"\n"+id+" · "+definition?.title+"\n\n";
                if(!_owner.Session.IsContentAvailable(id))return heading+"暂不可用："+definition?.unavailableReason+"\n不计作学员跳过、答错或完成。";
                _owner.Session.TryGetTask(id,out var task);
                string state=task.Status==ClinicalJourneyTaskStatus.Completed?"已完成":
                    task.Status==ClinicalJourneyTaskStatus.Skipped?"已跳过":
                    task.Status==ClinicalJourneyTaskStatus.Unanswered?"未答":
                    task.Status==ClinicalJourneyTaskStatus.InProgress?"进行中 / 未完成":"未开始";
                return heading+state+
                    (id=="OF-01"?"\n电脑六字段："+CountOfficeFields()+"/6 已查看，非任务通过":"")+
                    "\n不以到访或查阅代替合规成绩。";
            }
            return _owner.WashingLearningSummary(index);
        }
        int CountOfficeFields(){int count=0;for(int i=0;i<6;i++)if((_owner.OfficeFieldsViewed&(1<<i))!=0)count++;return count;}
        static string Brief(string id)
        {
            switch(id)
            {
                case "R01_OFFICE": return "资料核查：诊疗日期、患者标识与内镜编号、清洗消毒起止时间、操作人，以及其余资料。";
                case "R02_STORAGE": return "储存库：观察储存柜、内镜存放状态及相关记录。";
                case "R03_WAITING": return "候诊区：观察座椅、隔断与通道的位置关系。";
                case "R04_GI": return "消化内镜诊疗室：观察本室床台与设备，查询消化镜号历史；随后从门口前往另一间呼吸内镜诊疗室。";
                case "R04_RESP": return "呼吸内镜诊疗室：本室独立加载、独立记录；核对呼吸镜号历史，不与消化室记录混用。";
                default:return "独立核查：观察洗消现场并保留判断。本模式不打开带教答案；核查证据和作答界面尚待接入。";
            }
        }
        void ClosePanel()
        {
            if(!_panel) return;
            _panel.SetActive(false);
            if(Application.isPlaying) UnityEngine.Object.Destroy(_panel); else UnityEngine.Object.DestroyImmediate(_panel);
            _panel=null;
        }
        public void Dispose(){if(_disposed)return;_disposed=true;_office?.Dispose();ClosePanel();}
    }
}
