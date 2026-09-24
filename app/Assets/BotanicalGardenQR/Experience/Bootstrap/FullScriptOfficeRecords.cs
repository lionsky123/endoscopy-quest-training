using System;
using System.Linq;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
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
        const float ComputerPanelDistance=.62f,ComputerPanelOffset=-.15f,ComputerPanelTilt=1f;
        const float DocumentPanelDistance=.55f,DocumentPanelOffset=-.10f,DocumentPanelTilt=7f;
        const float GalleryImageScale=.78f,GalleryImageCenterX=-145f,GalleryImageCenterY=20f;
        readonly FullScriptJourneyRuntime _owner;
        readonly FullScriptRoomVisit _visit;
        readonly Transform _viewer;
        readonly TMP_FontAsset _font;
        readonly Action _continue;
        readonly GameObject _terminal;
        readonly Texture2D _screen;
        readonly Material _textMaterial;
        readonly bool _sourceOnly;
        GameObject _panel;
        bool _active,_disposed,_documentZoomed,_documentFromRecords;
        int _date,_scope,_row,_field,_document=-1;
        int _lastDocument;
        string _linkedUseId;
        bool _leakAssessment,_leakSelected;
        bool LeakSourceAvailable
        {
            get
            {
                int document=ClinicalTrainingRecords.DocumentIndexForTask("OF-02");
                if(document<0 || !Resources.Load<Texture2D>("ClinicalCourse/FullScriptVisuals/storage-cleaning-register-v1"))return false;
                int count=ClinicalTrainingRecords.LeakEntries(document).Length;
                return count>0 && count<=3; // The published paper has three writable rows.
            }
        }
        static readonly Color Ink=ClinicalPanelStyle.TextPrimary, Blue=ClinicalPanelStyle.Accent, Paper=ClinicalPanelStyle.Surface;
        int FieldCount=>ClinicalTrainingRecords.FieldCount;
        int RowCount=>ClinicalTrainingRecords.Query().Length;
        internal bool IsOpen=>_active;
        internal GameObject Panel=>_panel;
        internal GameObject Terminal=>_terminal;
        bool Ready=>!_disposed && _active && _visit.InputAllowed;
        ClinicalTrainingRecords.Row[] Rows=>ClinicalTrainingRecords.Query(_date==0?null:_date==1?"2026-09-20":"2026-09-21",
            _scope==0?null:_scope==1?"DEMO-GI-001":"DEMO-RESP-001");

        internal FullScriptOfficeRecords(FullScriptJourneyRuntime owner,FullScriptRoomVisit visit,Transform viewer,TMP_FontAsset font,Action leave,bool sourceOnly=false)
        {
            _owner=owner;_visit=visit;_viewer=viewer;_font=font;_continue=leave;_sourceOnly=sourceOnly;
            _screen=Resources.Load<Texture2D>("ClinicalCourse/FullScriptVisuals/office-records-reference-v2");
            if(!_screen)throw new InvalidOperationException("Document-reference office screen is missing.");
            _textMaterial=new Material(font.material);
            _textMaterial.SetFloat("_OutlineWidth",0);_textMaterial.SetColor("_FaceColor",Color.white);_textMaterial.DisableKeyword("UNDERLAY_ON");
            if(_sourceOnly)return; // Shared data and screen only; never instantiate another room's terminal.
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
        void SetTerminalActive(bool active){if(_terminal)_terminal.SetActive(active);}
        internal void Begin(){_active=true;SetTerminalActive(true);if(!_sourceOnly)_owner.Session.TryBeginTask("OF-01");RefreshTerminal();}
        internal void BeginStationary(int document=-1)
        {
            _active=true;_document=document;
            _documentFromRecords=false;
            _leakAssessment=!_sourceOnly && document>=0 && ClinicalTrainingRecords.IsLeakDocument(document);
            _leakSelected=false;
            if(document<0 && !_sourceOnly)_owner.Session.TryBeginTask("OF-01");
            RefreshTerminal();Open();
        }
        void RefreshTerminal()
        {
            if(!_terminal)return;
            var area=_terminal.transform.Find("TerminalRows");
            var rows=ClinicalTrainingRecords.Query();float scale=780f/1100;
            for(int r=0;r<rows.Length;r++)for(int f=0;f<FieldCount;f++)
            {
                var value=rows[r].Field(f);
                var cellName="Cell"+r+"_"+f;
                var existing=area.Find(cellName);
                if(existing)
                {
                    var txt=existing.GetComponent<TMP_Text>();
                    if(txt)txt.text=string.IsNullOrEmpty(value)?"（空白）":value;
                }
                else
                {
                    var label=Text(area,cellName,string.IsNullOrEmpty(value)?"（空白）":value,
                        ColumnX(f,FieldCount)*scale,(82-r*52)*scale,ColumnWidth(FieldCount)*scale,47*scale,13);
                    label.alignment=TextAlignmentOptions.Center;
                }
            }
        }
        void Open()
        {
            if(!Ready || _panel)return;
            _panel=Canvas("OfficeRecordsExpanded",new Vector2(1100,900));
            bool documentView=_document>=0;
            var pose=WorldSurfacePlacement.CreateViewerReadingPose(_viewer,
                documentView?DocumentPanelDistance:ComputerPanelDistance,
                documentView?DocumentPanelOffset:ComputerPanelOffset,
                documentView?DocumentPanelTilt:ComputerPanelTilt);
            if(!_visit.Stationary)pose.position.y=Mathf.Max(pose.position.y,_visit.Room.Root.transform.position.y+1.10f);
            _panel.transform.SetPositionAndRotation(pose.position,pose.rotation);
            SetTerminalActive(false);BuildPage();
        }
        void BuildPage()
        {
            var learningTask=_document<0?(_leakAssessment || _sourceOnly?"OF-02":"OF-01"):
                new[]{"OF-02","OF-03","OF-04","OF-05"}.FirstOrDefault(task=>ClinicalTrainingRecords.DocumentIndexForTask(task)==_document);
            if(learningTask!=null)_owner.Session.TryRecordLearningAction(learningTask,ClinicalLearningAction.Opened,_document<0?"records":"document:"+_document);
            // Keep the world pose stable when changing tabs, filters or pages.
            for(int i=_panel.transform.childCount-1;i>=0;i--)
            {var old=_panel.transform.GetChild(i);old.SetParent(null,false);Destroy(old.gameObject);}
            var board=(RectTransform)_panel.transform;
            bool focusedImage=_document>=0 && _documentZoomed && IsImageDocument(_document) &&
                !ClinicalTrainingRecords.IsLeakDocument(_document);
            bool compactImage=_document>=0 && IsImageDocument(_document) &&
                !ClinicalTrainingRecords.IsLeakDocument(_document) && !focusedImage;
            board.sizeDelta=new Vector2(1100,compactImage?760:900);
            Frame(board);
            if(_document<0)
            {
                BuildRecords(board);
                Control(board,"ReturnToTerminal",_visit.Stationary?"返回本室检查":"收起到实体电脑",
                    _visit.Stationary?0:-264,-360,_visit.Stationary?620:490,64,()=>
                {
                    if(_visit.Stationary)_visit.QueueStationaryAction(()=>{ClosePanel();_active=false;SetTerminalActive(false);_visit.BeginStationary();});
                    else ClosePanel();
                });
                if(!_visit.Stationary)
                    Control(board,"LeaveOfficeRecords","保留本次记录 · 去房门",264,-360,490,64,
                        ()=>{ClosePanel();_active=false;SetTerminalActive(false);_continue();},true);
                ClinicalNearTouch.Bind(board,()=>Ready);
                Refresh();
                return;
            }

            _lastDocument=_document;
            var leak=ClinicalTrainingRecords.IsLeakDocument(_document);
            _leakAssessment=!_sourceOnly && leak;
            if(leak)BuildLeakPaper(board);
            else BuildDocumentPage(board);
            BuildDocumentGalleryControls(board);
            ClinicalNearTouch.Bind(board,()=>Ready);
        }

        void BuildDocumentPage(RectTransform board)
        {
            bool focusedImage=_documentZoomed && IsImageDocument(_document) &&
                !ClinicalTrainingRecords.IsLeakDocument(_document);
            float pageWidth=focusedImage?1000f:790f;
            float pageHeight=pageWidth*1024/1536;
            float pageX=focusedImage?0:GalleryImageCenterX,pageY=focusedImage?0:-20f;
            Text(board,"Title","资料查阅 · "+ClinicalTrainingRecords.DocumentTitle(_document),pageX,focusedImage?410:315,pageWidth,52,28);
            Text(board,"Provenance","模拟训练资料 · 非医院真实记录",pageX,focusedImage?360:265,pageWidth,38,19);
            var id=ClinicalTrainingRecords.DocumentId(_document);
            var resource=DocumentImageResource(id);
            var texture=string.IsNullOrEmpty(resource)?null:Resources.Load<Texture2D>(resource);
            if(texture)
            {
                var image=Rect(board,"DocumentImage",pageX,pageY,pageWidth,pageHeight).gameObject.AddComponent<RawImage>();
                image.texture=texture;
                image.raycastTarget=false;
            }
            else
            {
                var page=Rect(board,"DocumentUnavailablePage",pageX,15,pageWidth,600);
                Fill(page,new Color(.97f,.98f,.98f,1),4);
                Text(board,"DocumentUnavailable","本项暂不可用；可返回目录继续其他检查。",pageX,185,pageWidth-70,58,26,Blue);
                Text(board,"DocumentBody",ClinicalTrainingRecords.DocumentBody(_document),pageX,0,pageWidth-76,300,23);
            }
        }

        void BuildDocumentGalleryControls(RectTransform board)
        {
            if(_documentZoomed && IsImageDocument(_document) && !ClinicalTrainingRecords.IsLeakDocument(_document))
            {
                Control(board,"ZoomDocument","返回资料选择",0,-405,280,52,()=>
                {_documentZoomed=false;BuildPage();});
                return;
            }
            float compactShift=board.rect.height<800?-55f:0f;
            var gallery=Rect(board,"DocumentGalleryControls",420,0,220,board.rect.height-140);
            Fill(gallery,ClinicalPanelStyle.Shell,12);
            Text(gallery,"DocumentGalleryHeading",_sourceOnly?"本室资料":"资料",0,330+compactShift,188,40,22,
                ClinicalPanelStyle.ShellText).alignment=TextAlignmentOptions.Center;
            if(!_sourceOnly)
            {
                for(int i=0;i<ClinicalTrainingRecords.DocumentCount;i++)
                {
                    int document=i;
                    var button=Control(gallery,"Doc"+i,ClinicalTrainingRecords.DocumentTitle(i),0,270+compactShift-i*51,192,44,
                        ()=>SelectDocument(document),_document==i);
                    button.GetComponentInChildren<TMP_Text>().fontSize=18;
                }
            }
            bool canZoom=IsImageDocument(_document);
            if(canZoom)
                Control(gallery,"ZoomDocument",_documentZoomed?"缩小资料":"放大阅读",0,-90+compactShift,192,46,()=>
                {_documentZoomed=!_documentZoomed;BuildPage();});
            Control(gallery,"BackToRows",_sourceOnly?"对照使用记录":_documentFromRecords?"返回电脑记录":"返回办公室",
                0,(canZoom?-150:-90)+compactShift,192,46,()=>
            {
                if(!_sourceOnly && !_documentFromRecords && _visit.Stationary)
                {
                    _visit.QueueStationaryAction(()=>{ClosePanel();_active=false;SetTerminalActive(false);_visit.BeginStationary();});
                    return;
                }
                _document=-1;BuildPage();
            });
            if(_sourceOnly)
                Control(gallery,"ReturnToTerminal","返回本室检查",0,-220+compactShift,192,46,()=>
                    _visit.QueueStationaryAction(()=>{ClosePanel();_active=false;SetTerminalActive(false);_visit.BeginStationary();}));
            foreach(var label in gallery.GetComponentsInChildren<TMP_Text>())
                if(label.name=="Label")label.fontSize=18;
        }

        void SelectDocument(int document)
        {
            if(!Ready || document<0 || document>=ClinicalTrainingRecords.DocumentCount)return;
            _document=document;
            _documentZoomed=false;
            _leakSelected=false;
            BuildPage();
        }

        static string DocumentImageResource(string id)
        {
            switch(id)
            {
                case "disinfection":return "ClinicalCourse/FullScriptVisuals/office-disinfection-training-record-v1";
                case "leak":return "ClinicalCourse/FullScriptVisuals/storage-cleaning-register-v1";
                case "training":return "ClinicalCourse/FullScriptVisuals/office-staff-training-record-v1";
                default:return null;
            }
        }
        static bool IsImageDocument(int index)
        {
            if(index<0 || index>=ClinicalTrainingRecords.DocumentCount)return false;
            var resource=DocumentImageResource(ClinicalTrainingRecords.DocumentId(index));
            return !string.IsNullOrEmpty(resource) && Resources.Load<Texture2D>(resource);
        }
        void BuildLeakPaper(RectTransform board)
        {
            var paper=Resources.Load<Texture2D>("ClinicalCourse/FullScriptVisuals/storage-cleaning-register-v1");
            var entries=ClinicalTrainingRecords.LeakEntries(_document);
            float scale=_documentZoomed ? .84f : GalleryImageScale;
            float PX(float x)=>GalleryImageCenterX+x*scale;
            float PY(float y)=>GalleryImageCenterY+y*scale;
            float PS(float value)=>value*scale;
            if(paper)
            {
                var image=Rect(board,"LeakRegisterPaper",PX(0),PY(60),PS(1000),PS(1000f*1024/1536)).gameObject.AddComponent<RawImage>();
                image.texture=paper;image.raycastTarget=false;
            }
            TMP_Text InkText(string name,string copy,float x,float y,float w,float h,int size)
            {
                var label=Text(board,name,copy,PX(x),PY(y),PS(w),PS(h),Mathf.Max(14,size*.82f),new Color(.015f,.025f,.035f));
                label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;return label;
            }
            InkText("Title","模拟测漏登记表 · 逐次测漏",0,350,920,50,29);
            InkText("Provenance","模拟训练记录 · 非医院原表 / 非实拍",0,306,920,36,20);
            InkText("DocumentBody",ClinicalTrainingRecords.DocumentBody(_document),0,253,910,74,19);
            if(!paper || entries.Length==0 || entries.Length>3)
            {
                InkText("LeakRegisterUnavailable","测漏登记暂不可用，请重试；仍无法打开时返回本室目录。",0,20,900,100,26);
                Control(board,"RetryLeakRegister","重新打开测漏登记",0,-90,600,60,BuildPage,true);
                return;
            }
            float X(float pixel)=>-500+pixel*1000/1536;
            float Y(float pixel)=>60+1000f*512/1536-pixel*1000/1536;
            var columns=new[]{139f,388,635,883,1131,1386};
            var headings=new[]{"登记号","使用号 /\n内镜编号","测漏时间","操作人员","登记结果"};
            for(int c=0;c<5;c++)InkText("LeakHeading"+c,headings[c],X((columns[c]+columns[c+1])/2),Y(345),152,60,c==1?19:23);
            var rows=new[]{399f,506,613,721};
            for(int row=0;row<entries.Length;row++)
            {
                var entry=entries[row];var use=ClinicalTrainingRecords.Query().Single(r=>r.Id==entry.UseId);
                float y=Y((rows[row]+rows[row+1])/2);
                var link=Control(board,"LeakUseLink"+row,"",PX(X((139+1386)/2f)),PY(y),PS((1386-139)*1000f/1536),PS(65),()=>OpenLinkedUse(entry.UseId));
                link.GetComponent<Image>().color=entry.UseId==_linkedUseId?new Color(.25f,.60f,.85f,.14f):Color.clear;
                var values=new[]{entry.Id,entry.UseId+"\n"+use.Scope,entry.Time,entry.Operator,entry.Result};
                for(int c=0;c<5;c++)InkText("LeakCell"+row+"_"+c,values[c],X((columns[c]+columns[c+1])/2),y,152,62,c==1?19:23);
            }
            InkText("LeakLinkHint",string.IsNullOrEmpty(_linkedUseId)?"近触一条登记，定位对应使用记录；查阅不自动作答。":"刚才对照使用号："+_linkedUseId+" · 可近触另一条继续对照",0,-164,920,24,19);
            InkText("LeakPaperOrigin","教学示例；登记结果不证明实际检测效果。",0,-201,920,30,19);
        }
        void OpenLinkedUse(string useId)
        {
            var rows=ClinicalTrainingRecords.Query();
            int index=Array.FindIndex(rows,row=>row.Id==useId);
            if(index<0)return;
            // A user-requested evidence jump clears restrictive filters, not the
            // world pose or saved findings. Selecting a row is not assessment.
            _date=0;_scope=0;_row=index;_field=0;_linkedUseId=useId;
            SelectLeakUse();
            _document=-1;BuildPage();
        }
        void BuildRecords(RectTransform board)
        {
            ScreenImage(board,1100,110);
            Text(board,"Provenance","模拟训练数据 · 示例时长不是标准",230,347,520,42,20);
            Control(board,"FilterDate","日期："+(_date==0?"全部":_date==1?"09-20":"09-21"),120,286,235,45,()=>{_date=(_date+1)%3;_row=0;_leakSelected=false;BuildPage();});
            Control(board,"FilterScope","镜号："+(_scope==0?"全部":_scope==1?"GI-001":"RESP-001"),375,286,250,45,()=>{_scope=(_scope+1)%3;_row=0;_leakSelected=false;BuildPage();});
            var rows=Rows;
            Text(board,"StartEndColumns","开始       结束",208,225,188,22,15).alignment=TextAlignmentOptions.Center;
            for(int r=0;r<rows.Length;r++)for(int i=0;i<FieldCount;i++)
            {
                int field=i,rowIndex=r;
                var button=Control(board,FieldName(r,i),"",ColumnX(i,FieldCount),192-r*52,ColumnWidth(FieldCount),49,()=>
                {
                    _field=field;_row=rowIndex;
                    SelectLeakUse();
                    if(!_sourceOnly && !_leakAssessment && Rows.Length>0 && _owner.Session.CanEdit)
                    {
                        _owner.OfficeFieldsViewed.Add(ClinicalTrainingRecords.Criterion(field));
                        _owner.OfficeRowsViewed.Add(Rows[_row].Id);
                        _owner.Session.TryRecordLearningAction("OF-01",ClinicalLearningAction.Observed,Rows[_row].Id+":"+ClinicalTrainingRecords.Criterion(field));
                    }
                    Refresh();
                });
                button.GetComponent<Image>().color=Color.clear;
                var cellText=button.GetComponentInChildren<TMP_Text>();
                cellText.fontSize=19;
                cellText.enableAutoSizing=true;
                cellText.fontSizeMin=12;
                cellText.fontSizeMax=19;
                cellText.textWrappingMode=TextWrappingModes.NoWrap;
                cellText.overflowMode=TextOverflowModes.Ellipsis;
            }
            Text(board,"RowInfo","",-160,-6,680,48,21);
            Fill(Rect(board,"RowNavigationCover",-358,-76,365,52),Paper,0);
            Control(board,"OpenDocuments",_sourceOnly?"返回测漏登记":_linkedUseId!=null?"返回刚才登记":"查阅资料",320,-76,350,55,()=>{_document=_sourceOnly?ClinicalTrainingRecords.DocumentIndexForTask("OF-02"):_lastDocument;_documentFromRecords=true;if(_document>=0)BuildPage();},true);
            // Original screenshot navigation is a visual reference, not five fake working modules.
            Fill(Rect(board,"NavigationCover",0,-156,1040,65),Paper,0);
            Text(board,"NavigationScope","当前仅开放训练记录查询与资料查阅；不连接真实系统或导出患者资料。",0,-156,1010,55,21);
            Text(board,"ReadStatus","",0,-211,1030,48,21);
            if(_sourceOnly)
            {
                Text(board,"LinkedRecordScope","与办公室共用同一份模拟资料和对账结果；不替代实物检测。",0,-254,1030,38,21);
                Control(board,"PracticeLinkedLeak","判断这次使用的登记关联",0,-299,710,48,BeginSelectedPractice,true);
            }
            else if(_leakAssessment)
            {
                if(_owner.Session.Mode==ClinicalJourneyMode.IndependentCheck)
                {
                    Control(board,"LeakMatch","本次有对应登记",-265,-266,510,54,()=>RecordLeak(ClinicalJourneyJudgement.NoIssue));
                    Control(board,"LeakMissing","本次未找到对应登记",265,-266,510,54,()=>RecordLeak(ClinicalJourneyJudgement.IssueFound));
                }
                else
                {
                    Control(board,"CompleteLeakLearning","判断本次登记关联",-265,-266,510,54,BeginSelectedPractice,true);
                    Control(board,"SkipLeakLearning","明确跳过对账教学",265,-266,510,54,()=>{_owner.Session.TrySkipGuidedTask("OF-02");Refresh();});
                }
                Text(board,"LeakFindingStatus","",0,-319,1030,46,20);
            }
            else if(_owner.Session.Mode==ClinicalJourneyMode.IndependentCheck)
            {
                Control(board,"FindingNoIssue","此字段：未发现缺项",-265,-266,510,54,()=>Record(ClinicalJourneyJudgement.NoIssue));
                Control(board,"FindingIssue","此字段：发现缺项",265,-266,510,54,()=>Record(ClinicalJourneyJudgement.IssueFound));
                Text(board,"FindingStatus","",0,-319,1030,46,20);
            }
            else
            {
                Control(board,"CompleteOfficeLearning","判断当前字段",-265,-266,510,54,BeginSelectedPractice,true);
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
            if(_sourceOnly || _leakAssessment)return;
            var rows=Rows;if(rows.Length==0)return;
            _owner.Session.TryRecordFinding("OF-01",ClinicalTrainingRecords.Criterion(_field),judgement,new[]{rows[_row].Id});Refresh();
        }
        void Refresh()
        {
            if(!_panel || _document>=0)return;
            var rows=Rows;_row=rows.Length==0?0:Mathf.Clamp(_row,0,rows.Length-1);var row=rows.Length==0?null:rows[_row];
            Set("RowInfo",row==null?"无匹配记录，请调整筛选":
                (_sourceOnly || _leakAssessment)?_leakSelected && row.Id==_linkedUseId?
                    $"使用 {row.Id} · 已定位登记 {ClinicalTrainingRecords.LeakEntryForUse(row.Id)?.Id}":$"使用 {row.Id} · 请从原表选择对应登记":
                    $"共{rows.Length}条 · 当前引用 {row.Id} · 未使用表格行不属于记录范围");
            for(int r=0;r<rows.Length;r++)for(int i=0;i<FieldCount;i++)
            {
                var b=_panel.transform.Find(FieldName(r,i)).GetComponent<Button>();
                b.GetComponentInChildren<TMP_Text>().text=string.IsNullOrEmpty(rows[r].Field(i))?"（空白）":rows[r].Field(i);
                b.GetComponent<Image>().color=r==_row && i==_field?new Color(.40f,.70f,.95f,.18f):Color.clear;
            }
            var findings=_owner.Session.GetFindings("OF-01");
            if(_sourceOnly)
            {
                Set("ReadStatus",LeakSourceAvailable?"与办公室共用同一份模拟资料；逐条对照，查阅不自动完成检查。":"测漏原表或登记数据暂不可用，请返回本室目录，稍后重试。");
                _panel.transform.Find("PracticeLinkedLeak").GetComponent<Button>().interactable=LeakSourceAvailable && _owner.Session.CanEdit && _leakSelected && row!=null;
                return;
            }
            if(_leakAssessment)
            {
                Set("ReadStatus",LeakSourceAvailable?"按使用号逐次核对完整登记范围；需要时可请求提示或讲解。":"测漏原表或登记数据暂不可用，请返回本室目录，稍后重试。");
                var leakFindings=_owner.Session.GetFindings("OF-02");
                if(_owner.Session.Mode==ClinicalJourneyMode.IndependentCheck)
                {
                    var saved=leakFindings.FirstOrDefault(f=>row!=null && f.CriterionId==ClinicalRecordReview.LeakCriterion(row.Id));
                    Set("LeakFindingStatus",(row==null?"无匹配使用":row.Id)+" · 已答 "+leakFindings.Length+"/"+RowCount+" · "+(saved.Judgement==ClinicalJourneyJudgement.None?"本次未答":saved.Judgement==ClinicalJourneyJudgement.NoIssue?"已记有对应登记":"已记未找到登记")+(_owner.Session.IsFinished?" · 已提交只读":" · 可回查修改"));
                    foreach(var name in new[]{"LeakMatch","LeakMissing"})_panel.transform.Find(name).GetComponent<Button>().interactable=LeakSourceAvailable && _owner.Session.CanEdit && _leakSelected && row!=null;
                }
                else
                {
                    _owner.Session.TryGetTask("OF-02",out var leakTask);
                    Set("LeakFindingStatus","已对照 "+LeakViewed+"/"+RowCount+" · "+(leakTask.Status==ClinicalJourneyTaskStatus.Completed?"各次判断已核对":leakTask.Status==ClinicalJourneyTaskStatus.Skipped?"已明确跳过":"可逐次判断"));
                    _panel.transform.Find("CompleteLeakLearning").GetComponent<Button>().interactable=LeakSourceAvailable && _owner.Session.CanEdit && _leakSelected && row!=null;
                    _panel.transform.Find("SkipLeakLearning").GetComponent<Button>().interactable=_owner.Session.CanEdit;
                }
                return;
            }
            Set("ReadStatus",_owner.Session.Mode==ClinicalJourneyMode.GuidedLearning
                ?"逐项近触查看，并翻阅全部"+RowCount+"条。注意复合字段应分别核对，不用示例时长判断效果。"
                :"选择字段，再记录判断；引用当前行号。缺项应引用对应空白行；提交前不公布答案。");
            _owner.Session.TryGetTask("OF-01",out var task);
            if(_owner.Session.Mode==ClinicalJourneyMode.GuidedLearning)
            {
                _panel.transform.Find("CompleteOfficeLearning").GetComponent<Button>().interactable=_owner.Session.CanEdit && row!=null && _owner.OfficeFieldsViewed.Contains(ClinicalTrainingRecords.Criterion(_field));
                _panel.transform.Find("SkipOfficeLearning").GetComponent<Button>().interactable=_owner.Session.CanEdit;
                Set("FindingStatus","已查看字段 "+_owner.OfficeFieldsViewed.Count+"/"+FieldCount+" · 已判断 "+findings.Length+"/"+FieldCount+" · "+(task.Status==ClinicalJourneyTaskStatus.Completed?"各字段判断已核对":task.Status==ClinicalJourneyTaskStatus.Skipped?"已明确跳过":"可返回原表核对"));
            }
            else
            {
                var finding=findings.FirstOrDefault(f=>f.CriterionId==ClinicalTrainingRecords.Criterion(_field));
                var saved=finding.Judgement==ClinicalJourneyJudgement.None?"本字段未答":(finding.Judgement==ClinicalJourneyJudgement.IssueFound?"已记缺项":"已记未发现缺项")+" · "+string.Join(",",finding.EvidenceIds);
                Set("FindingStatus","当前："+ClinicalTrainingRecords.Heading(_field)+" · 已记录 "+findings.Length+"/"+FieldCount+Environment.NewLine+saved+(_owner.Session.IsSubmitted?" · 已提交，只读":" · 可修改"));
                foreach(var name in new[]{"FindingNoIssue","FindingIssue"})_panel.transform.Find(name).GetComponent<Button>().interactable=_owner.Session.CanEdit && row!=null;
            }
        }
        static float ColumnX(int index,int count,float halfWidth=410f)
            =>count<=1?0f:Mathf.Lerp(-halfWidth,halfWidth,index/(float)(count-1));
        static float ColumnWidth(int count)
            =>Mathf.Max(72f,Mathf.Min(188f,820f/Mathf.Max(1,count)-10f));
        static float DocumentWidth(int count)
            =>Mathf.Max(100f,Mathf.Min(167f,900f/Mathf.Max(1,count)-10f));
        void Set(string name,string value)=>_panel.transform.Find(name).GetComponent<TMP_Text>().text=value;
        int LeakViewed=>ClinicalTrainingRecords.Query().Count(r=>_owner.ScriptStepsViewed.Contains("OF-02:use:"+r.Id));
        void SelectLeakUse()
        {
            if((!_leakAssessment && !_sourceOnly) || Rows.Length==0)return;
            var useId=Rows[Mathf.Clamp(_row,0,Rows.Length-1)].Id;
            _leakSelected=LeakSourceAvailable && useId==_linkedUseId;
            if(!_leakSelected)return;
            if(_owner.Session.CanEdit && !_sourceOnly)_owner.ScriptStepsViewed.Add("OF-02:use:"+useId);
            _owner.Session.TryRecordLearningAction("OF-02",ClinicalLearningAction.Observed,useId);
        }
        void BeginSelectedPractice()
        {
            if(!Ready || Rows.Length==0)return;
            var row=Rows[Mathf.Clamp(_row,0,Rows.Length-1)];
            bool leak=_leakAssessment || _sourceOnly;
            string task=leak?"OF-02":"OF-01";
            string criterion=leak?ClinicalRecordReview.LeakCriterion(row.Id):ClinicalTrainingRecords.Criterion(_field);
            string[] evidence={row.Id};
            if(leak)
            {
                int doc=ClinicalTrainingRecords.DocumentIndexForTask("OF-02");
                if(doc<0 || !_leakSelected || !LeakSourceAvailable)return;
                var entry=ClinicalTrainingRecords.LeakEntryForUse(row.Id);
                if(entry!=null)evidence=new[]{row.Id,entry.Id};
            }
            var pose=new Pose(_panel.transform.position,_panel.transform.rotation);
            EndView();
            _visit.BeginRecordPractice(task,criterion,evidence,()=>
            {
                _visit.CloseLearningPractice();_active=true;Open();
                if(_panel)_panel.transform.SetPositionAndRotation(pose.position,pose.rotation);
            });
        }
        void RecordLeak(ClinicalJourneyJudgement judgement)
        {
            if(!_leakAssessment || _sourceOnly || !_leakSelected || !LeakSourceAvailable || Rows.Length==0)return;
            var use=Rows[Mathf.Clamp(_row,0,Rows.Length-1)];
            int doc=ClinicalTrainingRecords.DocumentIndexForTask("OF-02");if(doc<0)return;
            var entry=ClinicalTrainingRecords.LeakEntryForUse(use.Id);
            _owner.Session.TryRecordFinding("OF-02",ClinicalRecordReview.LeakCriterion(use.Id),judgement,entry==null?new[]{use.Id}:new[]{use.Id,entry.Id});
            Refresh();
        }
        TMP_Text Text(Transform parent,string name,string value,float x,float y,float w,float h,float size,Color? color=null)
        {var label=Label(parent,_font,name,x,y,w,h,size);label.fontSharedMaterial=_textMaterial;label.text=value;label.color=color??Ink;return label;}
        Button Control(Transform parent,string name,string caption,float x,float y,float w,float h,Action action,bool primary=false)
        {
            var rect=Rect(parent,name,x,y,w,h);var fill=Fill(rect,primary?Blue:ClinicalPanelStyle.SurfaceSubtle,12);fill.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=fill;button.navigation=new Navigation{mode=Navigation.Mode.None};
            var colors=button.colors;colors.pressedColor=new Color(.82f,.9f,.86f);colors.disabledColor=new Color(.65f,.69f,.73f);button.colors=colors;
            button.onClick.AddListener(()=>
            {
                if(!Ready)return;
                if(_visit.Stationary)_visit.QueueStationaryAction(()=>{if(Ready)action();});
                else action();
            });
            var text=Text(rect,"Label",caption,0,0,w-24,h-8,22,primary?ClinicalPanelStyle.ShellText:Ink);text.alignment=TextAlignmentOptions.Center;
            EmphasizeButton(button,primary:primary);
            return button;
        }
        GameObject Canvas(string name,Vector2 size)
        {
            var result=new GameObject(name,typeof(RectTransform),typeof(Canvas));var rect=(RectTransform)result.transform;
            rect.sizeDelta=size;rect.localScale=Vector3.one*.0006f;
            var canvas=result.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_viewer.GetComponent<Camera>();canvas.sortingOrder=130;return result;
        }
        void ClosePanel(){if(_panel){Destroy(_panel);_panel=null;}if(!_disposed && _active){RefreshTerminal();SetTerminalActive(true);}}
        internal void EndView(){_active=false;ClosePanel();SetTerminalActive(false);}
        static void Destroy(GameObject item){item.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(item);else UnityEngine.Object.DestroyImmediate(item);}
        public void Dispose(){if(_disposed)return;_disposed=true;_active=false;ClosePanel();if(_terminal)Destroy(_terminal);if(_textMaterial){if(Application.isPlaying)UnityEngine.Object.Destroy(_textMaterial);else UnityEngine.Object.DestroyImmediate(_textMaterial);}}
    }
}
