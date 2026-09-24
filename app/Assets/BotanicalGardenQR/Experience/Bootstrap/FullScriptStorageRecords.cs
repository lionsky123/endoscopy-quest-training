using System.Linq;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed partial class FullScriptRoomVisit
    {
        int _storageRecordWeek=-1;
        const string StorageRecordImage="ClinicalCourse/FullScriptVisuals/storage-cleaning-register-v1";
        Texture2D BuildStoragePaper(RectTransform board,bool interactive)
        {
            TMP_Text Text(string name,string copy,float x,float y,float w,float h,int size)
            {
                var text=Label(board,_font,name,x,y,w,h,size);text.fontSharedMaterial=_scriptTextMaterial;
                text.color=new Color(.015f,.025f,.035f);text.text=copy;text.alignment=TextAlignmentOptions.Center;
                text.raycastTarget=false;return text;
            }
            var texture=Resources.Load<Texture2D>(StorageRecordImage);
            bool independent=true; // One published scenario for guided practice and archived assessment.
            if(texture)
            {
                var paper=Rect(board,"StorageRegisterPaper",0,72,800,800f*1024/1536).gameObject.AddComponent<RawImage>();
                paper.texture=texture;paper.raycastTarget=false;
                Text("RegisterTitle","储存柜清洁消毒登记 · 模拟训练记录",0,278,735,45,28);
                Text("RegisterScope",StorageCleaningRecords.Range+"\n"+StorageCleaningRecords.Scope,0,231,735,52,19);
                // Measured coordinates of V08's actual grid; data never lives in pixels.
                float X(float pixel)=>-400+pixel*800/1536;
                float Y(float pixel)=>72+800f*512/1536-pixel*800/1536;
                var columns=new[]{139f,388,635,883,1131,1386};
                for(int column=0;column<5;column++)
                    Text("RegisterHeading"+column,StorageCleaningRecords.Heading(column),X((columns[column]+columns[column+1])/2),Y(345),120,42,21);
                var rows=new[]{399f,506,613,721,827};
                for(int week=0;week<StorageCleaningRecords.Count;week++)
                {
                    int selected=week;float y=Y((rows[week]+rows[week+1])/2);
                    if(interactive)
                    {
                        var row=Button(board,_font,"StorageWeek"+week,"",X((139+1386)/2f),y,(1386-139)*800f/1536,51,()=>QueueStationaryAction(()=>
                        {
                            _storageRecordWeek=selected;
                            if(_owner.Session.CanEdit)_owner.ScriptStepsViewed.Add("ST-02:week:"+selected);
                            _owner.Session.TryRecordLearningAction("ST-02",ClinicalLearningAction.Observed,StorageCleaningRecords.Evidence(selected));
                            ShowStorageRecords();
                        }));
                        row.GetComponent<Image>().color=_storageRecordWeek==week?new Color(.25f,.6f,.85f,.15f):Color.clear;
                    }
                    for(int column=0;column<5;column++)
                        Text("StorageCell"+week+"_"+column,StorageCleaningRecords.Cell(week,column,independent),X((columns[column]+columns[column+1])/2),y,120,43,column==0?16:18).raycastTarget=false;
                }
                Text("RegisterProvenance","生成空白纸面 + 程序模拟数据 · 非医院原表 / 非实拍\n每周登记为剧本情境要求；只核对记录，不验证实际清洁效果。",0,-138,740,65,18);
            }
            else Text("RegisterMissing","登记表底图暂不可用，请返回继续其他检查。",0,80,740,150,24);
            return texture;
        }
        void ShowStorageRecords()
        {
            if(!InputAllowed || ScriptTaskId!="ST-02")return;
            ClosePanel();
            _panel=new GameObject("StorageCleaningRecords",typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_panel.transform;
            board.sizeDelta=new Vector2(860,760);board.localScale=Vector3.one*.00065f;
            board.SetPositionAndRotation(_scriptPose.position,_scriptPose.rotation);
            var canvas=_panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_viewer.GetComponent<Camera>();canvas.sortingOrder=130;
            Frame(board);
            TMP_Text Text(string name,string copy,float x,float y,float w,float h,int size)
            {
                var text=Label(board,_font,name,x,y,w,h,size);text.fontSharedMaterial=_scriptTextMaterial;
                text.color=new Color(.015f,.025f,.035f);text.text=copy;text.alignment=TextAlignmentOptions.Center;return text;
            }
            Button Control(string name,string copy,float x,float y,float w,System.Action action)
            {
                var button=Button(board,_font,name,copy,x,y,w,54,()=>QueueStationaryAction(action),true);
                button.GetComponentInChildren<TMP_Text>().fontSharedMaterial=_scriptTextMaterial;
                EmphasizeButton(button,false,false);return button;
            }
            var texture=BuildStoragePaper(board,true);
            bool independent=_owner.Session.Mode==ClinicalJourneyMode.IndependentCheck;
            _owner.Session.TryGetTask("ST-02",out var task);
            var findings=_owner.Session.GetFindings("ST-02");
            var selectedFinding=findings.FirstOrDefault(f=>f.CriterionId==StorageCleaningRecords.Criterion(_storageRecordWeek));
            string state=selectedFinding.Judgement==ClinicalJourneyJudgement.None?"未答":selectedFinding.Judgement==ClinicalJourneyJudgement.IssueFound?"已记缺项":"已记字段完整";
            int viewed=Enumerable.Range(0,4).Count(w=>_owner.ScriptStepsViewed.Contains("ST-02:week:"+w));
            Text("StorageRecordStatus",_storageRecordWeek<0?"近触表内一周，查看并引用该周记录；打开表格不会自动完成。":
                StorageCleaningRecords.Evidence(_storageRecordWeek)+" · "+StorageCleaningRecords.Period(_storageRecordWeek)+" · "+
                (independent?state+" / 已答 "+findings.Length+"/4":"已查看 "+viewed+"/4 · "+(task.Status==ClinicalJourneyTaskStatus.Completed?"已确认学完":task.Status==ClinicalJourneyTaskStatus.Skipped?"已明确跳过":"待明确确认"))+
                (_owner.Session.IsFinished?" · 已提交，只读":" · 可回查"),0,-207,800,55,20);
            if(independent)
            {
                void Record(ClinicalJourneyJudgement value)
                {
                    if(_storageRecordWeek<0 || !texture)return;
                    _owner.Session.TryRecordFinding("ST-02",StorageCleaningRecords.Criterion(_storageRecordWeek),value,new[]{StorageCleaningRecords.Evidence(_storageRecordWeek)});
                    ShowStorageRecords();
                }
                foreach(var button in new[]{Control("StorageNoIssue","本周登记字段完整",-200,-266,380,()=>Record(ClinicalJourneyJudgement.NoIssue)),
                    Control("StorageIssue","本周登记有缺项",200,-266,380,()=>Record(ClinicalJourneyJudgement.IssueFound))})
                    button.interactable=texture && _owner.Session.CanEdit && _storageRecordWeek>=0;
            }
            else
            {
                Control("CompleteStorageLearning","判断本周登记",-200,-266,380,()=>
                {
                    if(!texture || _storageRecordWeek<0)return;
                    BeginRecordPractice("ST-02",StorageCleaningRecords.Criterion(_storageRecordWeek),
                        new[]{StorageCleaningRecords.Evidence(_storageRecordWeek)},ShowStorageRecords);
                }).interactable=texture && _owner.Session.CanEdit && _storageRecordWeek>=0;
                Control("SkipStorageLearning","明确跳过本项教学",200,-266,380,()=>
                {_owner.Session.TrySkipGuidedTask("ST-02");ShowStorageRecords();}).interactable=_owner.Session.CanEdit;
            }
            Control("ReturnToInspection","返回储存库",0,-330,520,ReturnFromStorageRegister);
            ClinicalNearTouch.Bind(board,()=>InputAllowed);
        }
    }
}
