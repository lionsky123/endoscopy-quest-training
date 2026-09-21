using System;
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
    // Physical monitor and expanded view share the same rows and never connect to a hospital system.
    internal sealed class FullScriptOfficeRecords : IDisposable
    {
        readonly FullScriptJourneyRuntime _owner;
        readonly FullScriptRoomVisit _visit;
        readonly Transform _viewer;
        readonly TMP_FontAsset _font;
        readonly Action _continue;
        readonly GameObject _terminal;
        readonly Texture2D _screen;
        readonly Material _textMaterial;
        GameObject _panel;
        bool _active,_disposed;
        int _date,_scope,_row,_field,_document=-1;
        static readonly Color Ink=new Color(.10f,.19f,.28f), Blue=new Color(.05f,.34f,.59f), Paper=new Color(.96f,.98f,1);
        internal bool IsOpen=>_active;
        internal GameObject Panel=>_panel;
        internal GameObject Terminal=>_terminal;
        bool Ready=>!_disposed && _active && _visit.InputAllowed;
        ClinicalTrainingRecords.Row[] Rows=>ClinicalTrainingRecords.Query(_date==0?null:_date==1?"2026-09-20":"2026-09-21",
            _scope==0?null:_scope==1?"DEMO-GI-001":"DEMO-RESP-001");

        internal FullScriptOfficeRecords(FullScriptJourneyRuntime owner,FullScriptRoomVisit visit,Transform viewer,TMP_FontAsset font,Action leave)
        {
            _owner=owner;_visit=visit;_viewer=viewer;_font=font;_continue=leave;
            _screen=Resources.Load<Texture2D>("ClinicalCourse/FullScriptVisuals/office-records-reference-v2");
            if(!_screen)throw new InvalidOperationException("Document-reference office screen is missing.");
            _textMaterial=new Material(font.material);
            _textMaterial.SetFloat("_OutlineWidth",0);_textMaterial.SetColor("_FaceColor",Color.white);_textMaterial.DisableKeyword("UNDERLAY_ON");
            _terminal=Canvas("OfficeRecordTerminal",new Vector2(780,450));
            _terminal.transform.SetParent(visit.Room.Root.transform,false);
            _terminal.transform.localScale=Vector3.one*.00082f;
            _terminal.transform.localPosition=visit.Room.TerminalPosition??new Vector3(-3.48f,1.25f,0);
            _terminal.transform.localRotation=Quaternion.Euler(0,visit.Room.TerminalPosition.HasValue?90:-90,0);
            var board=(RectTransform)_terminal.transform;
            ScreenImage(board,780,0);
            Rect(board,"TerminalRows",0,0,780,440);
            Control(board,"OpenOfficeRecords","近触屏幕 · 放大查询（模拟训练）",0,-143,710,53,Open,true);
            ClinicalNearTouch.Bind(board,()=>Ready && !_panel);
            RefreshTerminal();_terminal.SetActive(false);
        }
        internal void Begin(){_active=true;_terminal.SetActive(true);_owner.Session.TryBeginTask("OF-01");RefreshTerminal();}
        void RefreshTerminal()
        {
            var area=_terminal.transform.Find("TerminalRows");
            for(int i=area.childCount-1;i>=0;i--){var child=area.GetChild(i);child.SetParent(null,false);Destroy(child.gameObject);}
            var rows=ClinicalTrainingRecords.Query();float scale=780f/1100;
            float[] positions={-405,-200,4,158,255,410};
            for(int r=0;r<rows.Length;r++)for(int f=0;f<6;f++)
            {
                var value=rows[r].Field(f);
                var label=Text(area,"Cell"+r+"_"+f,string.IsNullOrEmpty(value)?"（空白）":value,positions[f]*scale,(82-r*52)*scale,(f==3||f==4?90:182)*scale,47*scale,13);
                label.alignment=TextAlignmentOptions.Center;
            }
        }
        void Open()
        {
            if(!Ready || _panel)return;
            _panel=Canvas("OfficeRecordsExpanded",new Vector2(1100,900));
            var position=_viewer.position+_viewer.forward*.55f-Vector3.up*.18f;
            position.y=Mathf.Max(position.y,_visit.Room.Root.transform.position.y+1.10f);
            _panel.transform.SetPositionAndRotation(position,_viewer.rotation);
            _terminal.SetActive(false);BuildPage();
        }
        void BuildPage()
        {
            // Keep the world pose stable when changing tabs, filters or pages.
            for(int i=_panel.transform.childCount-1;i>=0;i--)
            {var old=_panel.transform.GetChild(i);old.SetParent(null,false);Destroy(old.gameObject);}
            var board=(RectTransform)_panel.transform;
            if(!board.GetComponent<Image>())Fill(board,Paper,3);
            if(_document<0)BuildRecords(board);
            else
            {
                Text(board,"Title","资料查阅 · "+ClinicalTrainingRecords.DocumentTitle(_document),0,385,1030,65,30);
                Text(board,"Provenance","模拟训练资料 · 非医院真实记录",0,323,1030,45,22);
                for(int i=0;i<6;i++){int d=i;Control(board,"Doc"+i,ClinicalTrainingRecords.DocumentTitle(i),-450+i*180,260,167,58,()=>{_document=d;BuildPage();});}
                Text(board,"DocumentBody",ClinicalTrainingRecords.DocumentBody(_document),0,40,1015,360,27);
                Control(board,"BackToRows","返回电子记录与六字段核查",0,-210,780,65,()=>{_document=-1;BuildPage();},true);
            }
            Control(board,"ReturnToTerminal","收起到实体电脑",-264,-360,490,64,ClosePanel);
            Control(board,"LeaveOfficeRecords","保留本次记录 · 去房门",264,-360,490,64,()=>{ClosePanel();_active=false;_terminal.SetActive(false);_continue();},true);
            Text(board,"Footer","仅真实手部近触  |  途中可回查修改  |  最后回办公室统一提交",0,-418,1030,40,20);
            ClinicalNearTouch.Bind(board,()=>Ready);
            if(_document<0)Refresh();
        }
        void BuildRecords(RectTransform board)
        {
            ScreenImage(board,1100,110);
            Text(board,"Provenance","模拟训练数据 · 示例时长不是标准",230,347,520,42,20);
            Control(board,"FilterDate","日期："+(_date==0?"全部":_date==1?"09-20":"09-21"),120,286,235,45,()=>{_date=(_date+1)%3;_row=0;BuildPage();});
            Control(board,"FilterScope","镜号："+(_scope==0?"全部":_scope==1?"GI-001":"RESP-001"),375,286,250,45,()=>{_scope=(_scope+1)%3;_row=0;BuildPage();});
            var rows=Rows;
            Text(board,"StartEndColumns","开始       结束",208,225,188,22,15).alignment=TextAlignmentOptions.Center;
            for(int r=0;r<rows.Length;r++)for(int i=0;i<6;i++)
            {
                int field=i,rowIndex=r;
                float[] x={-405,-200,4,158,255,410};
                float width=i==3||i==4?94:188;
                var button=Control(board,FieldName(r,i),"",x[i],192-r*52,width,49,()=>
                {
                    _field=field;_row=rowIndex;
                    if(Rows.Length>0 && _owner.Session.CanEdit)
                    {
                        _owner.OfficeFieldsViewed|=1<<field;
                        _owner.OfficeRowsViewed|=1<<Array.FindIndex(ClinicalTrainingRecords.Query(),r=>r.Id==Rows[_row].Id);
                    }
                    Refresh();
                });
                button.GetComponent<Image>().color=Color.clear;
                button.GetComponentInChildren<TMP_Text>().fontSize=19;
            }
            Text(board,"RowInfo","",-160,-6,680,48,21);
            Control(board,"PreviousRow","上一条",-450,-76,165,49,()=>{var count=Rows.Length;if(count>0)_row=(_row+count-1)%count;Refresh();});
            Control(board,"NextRow","下一条",-265,-76,165,49,()=>{var count=Rows.Length;if(count>0)_row=(_row+1)%count;Refresh();});
            Control(board,"OpenDocuments","查阅资料",320,-76,350,55,()=>{_document=0;BuildPage();},true);
            // Original screenshot navigation is a visual reference, not five fake working modules.
            Fill(Rect(board,"NavigationCover",0,-156,1040,65),Paper,0);
            Text(board,"NavigationScope","当前仅开放训练记录查询与资料查阅；不连接真实系统或导出患者资料。",0,-156,1010,55,21);
            Text(board,"ReadStatus","",0,-211,1030,48,21);
            if(_owner.Session.Mode==ClinicalJourneyMode.IndependentCheck)
            {
                Control(board,"FindingNoIssue","此字段：未发现缺项",-265,-266,510,54,()=>Record(ClinicalJourneyJudgement.NoIssue));
                Control(board,"FindingIssue","此字段：发现缺项",265,-266,510,54,()=>Record(ClinicalJourneyJudgement.IssueFound));
                Text(board,"FindingStatus","",0,-319,1030,46,20);
            }
            else
            {
                Control(board,"CompleteOfficeLearning","确认已学六字段核查方法",-265,-266,510,54,()=>{if(_owner.OfficeFieldsViewed==63 && _owner.OfficeRowsViewed==7)_owner.Session.TryCompleteGuidedTask("OF-01");Refresh();},true);
                Control(board,"SkipOfficeLearning","明确跳过本项教学",265,-266,510,54,()=>{_owner.Session.TrySkipGuidedTask("OF-01");Refresh();});
                Text(board,"FindingStatus","",0,-319,1030,40,20);
            }
        }
        static string FieldName(int row,int field)=>row==0?"RecordField"+field:"Row"+row+"Field"+field;
        void ScreenImage(Transform parent,float width,float y)
        {
            var image=Rect(parent,"DocumentReferenceScreen",0,y,width,width*_screen.height/_screen.width).gameObject.AddComponent<RawImage>();
            image.texture=_screen;image.raycastTarget=false;
        }
        void Record(ClinicalJourneyJudgement judgement)
        {
            var rows=Rows;if(rows.Length==0)return;
            _owner.Session.TryRecordFinding("OF-01",ClinicalTrainingRecords.Criterion(_field),judgement,new[]{rows[_row].Id});Refresh();
        }
        void Refresh()
        {
            if(!_panel || _document>=0)return;
            var rows=Rows;_row=rows.Length==0?0:Mathf.Clamp(_row,0,rows.Length-1);var row=rows.Length==0?null:rows[_row];
            Set("RowInfo",row==null?"无匹配记录，请调整筛选":$"共{rows.Length}条 · 当前引用 {row.Id} · 未使用表格行不属于记录范围");
            for(int r=0;r<rows.Length;r++)for(int i=0;i<6;i++)
            {
                var b=_panel.transform.Find(FieldName(r,i)).GetComponent<Button>();
                b.GetComponentInChildren<TMP_Text>().text=string.IsNullOrEmpty(rows[r].Field(i))?"（空白）":rows[r].Field(i);
                b.GetComponent<Image>().color=r==_row && i==_field?new Color(.40f,.70f,.95f,.18f):Color.clear;
            }
            var findings=_owner.Session.GetFindings("OF-01");
            Set("ReadStatus",_owner.Session.Mode==ClinicalJourneyMode.GuidedLearning
                ?"逐项近触查看，并翻阅全部3条。注意复合字段应分别核对，不用示例时长判断效果。"
                :"选择字段，再记录判断；引用当前行号。缺项应引用对应空白行；提交前不公布答案。");
            _owner.Session.TryGetTask("OF-01",out var task);
            if(_owner.Session.Mode==ClinicalJourneyMode.GuidedLearning)
            {
                _panel.transform.Find("CompleteOfficeLearning").GetComponent<Button>().interactable=_owner.Session.CanEdit && _owner.OfficeFieldsViewed==63 && _owner.OfficeRowsViewed==7;
                _panel.transform.Find("SkipOfficeLearning").GetComponent<Button>().interactable=_owner.Session.CanEdit;
                Set("FindingStatus",$"已查看字段 {Count(_owner.OfficeFieldsViewed)}/6 · 记录 {Count(_owner.OfficeRowsViewed)}/3 · "+(task.Status==ClinicalJourneyTaskStatus.Completed?"已学（非合规成绩）":task.Status==ClinicalJourneyTaskStatus.Skipped?"已明确跳过":"待确认"));
            }
            else
            {
                var finding=findings.FirstOrDefault(f=>f.CriterionId==ClinicalTrainingRecords.Criterion(_field));
                var saved=finding.Judgement==ClinicalJourneyJudgement.None?"本字段未答":(finding.Judgement==ClinicalJourneyJudgement.IssueFound?"已记缺项":"已记未发现缺项")+" · "+string.Join(",",finding.EvidenceIds);
                Set("FindingStatus",$"当前：{ClinicalTrainingRecords.Heading(_field)} · 已记录 {findings.Length}/6\n{saved}"+(_owner.Session.IsSubmitted?" · 已提交，只读":" · 可修改"));
                foreach(var name in new[]{"FindingNoIssue","FindingIssue"})_panel.transform.Find(name).GetComponent<Button>().interactable=_owner.Session.CanEdit && row!=null;
            }
        }
        static int Count(int mask){int n=0;while(mask>0){n+=mask&1;mask>>=1;}return n;}
        void Set(string name,string value)=>_panel.transform.Find(name).GetComponent<TMP_Text>().text=value;
        TMP_Text Text(Transform parent,string name,string value,float x,float y,float w,float h,float size,Color? color=null)
        {var label=Label(parent,_font,name,x,y,w,h,size);label.fontSharedMaterial=_textMaterial;label.text=value;label.color=color??Ink;return label;}
        Button Control(Transform parent,string name,string caption,float x,float y,float w,float h,Action action,bool primary=false)
        {
            var rect=Rect(parent,name,x,y,w,h);var fill=Fill(rect,primary?Blue:Color.white,3);fill.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=fill;button.navigation=new Navigation{mode=Navigation.Mode.None};
            var colors=button.colors;colors.pressedColor=new Color(.70f,.84f,.98f);colors.disabledColor=new Color(.65f,.69f,.73f);button.colors=colors;
            button.onClick.AddListener(()=>{if(Ready)action();});
            var text=Text(rect,"Label",caption,0,0,w-24,h-8,22,primary?Color.white:Ink);text.alignment=TextAlignmentOptions.Center;
            return button;
        }
        GameObject Canvas(string name,Vector2 size)
        {
            var result=new GameObject(name,typeof(RectTransform),typeof(Canvas));var rect=(RectTransform)result.transform;
            rect.sizeDelta=size;rect.localScale=Vector3.one*.0006f;
            var canvas=result.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_viewer.GetComponent<Camera>();canvas.sortingOrder=130;return result;
        }
        void ClosePanel(){if(_panel){Destroy(_panel);_panel=null;}if(!_disposed && _active){RefreshTerminal();_terminal.SetActive(true);}}
        static void Destroy(GameObject item){item.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(item);else UnityEngine.Object.DestroyImmediate(item);}
        public void Dispose(){if(_disposed)return;_disposed=true;_active=false;ClosePanel();if(_terminal)Destroy(_terminal);if(_textMaterial){if(Application.isPlaying)UnityEngine.Object.Destroy(_textMaterial);else UnityEngine.Object.DestroyImmediate(_textMaterial);}}
    }
}
