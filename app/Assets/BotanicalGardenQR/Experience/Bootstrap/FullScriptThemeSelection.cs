using System;
using BotanicalGardenQR.FrontendShell.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed partial class FullScriptRoomVisit
    {
        bool _themeChosen;
        readonly struct RoomTheme
        {
            internal readonly string Title;
            internal readonly string[] Tasks;
            internal RoomTheme(string title,params string[] tasks){Title=title;Tasks=tasks;}
        }
        RoomTheme[] RoomThemes()
        {
            if(RoomId=="R01_OFFICE")return new[]{
                new RoomTheme("观察办公现场","OF-00"),new RoomTheme("查询电子记录","OF-01","OF-02"),
                new RoomTheme("查阅纸质资料","OF-03","OF-04","OF-05")};
            if(RoomId=="R04_GI" || RoomId=="R04_RESP")
            {
                string suffix=RoomId=="R04_GI"?".GI":".RESP";
                return new[]{new RoomTheme("用途与设备","CL-01"+suffix),
                    new RoomTheme("报告与历史","CL-02"+suffix,"CL-03"+suffix),new RoomTheme("台面与废物","CL-04"+suffix)};
            }
            if(RoomId==FullScriptRoomCatalog.Washing)return new[]{
                new RoomTheme("空间、设备与水路","RE-01","RE-02","RE-03"),
                new RoomTheme("防护、产品与附件","RE-06","RE-04","RE-05")};
            return Array.Empty<RoomTheme>();
        }

        RectTransform ThemeBoard(string name,string heading)
        {
            ClosePanel();_door=false;EnsureScriptTextMaterial();EnsureScriptPose();
            _panel=new GameObject(name,typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_panel.transform;
            board.sizeDelta=new Vector2(860,350);board.localScale=Vector3.one*.00065f;
            board.SetPositionAndRotation(_scriptPose.position,_scriptPose.rotation);
            var canvas=_panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            canvas.worldCamera=_viewer.GetComponent<Camera>();canvas.sortingOrder=130;
            ShellFrame(board);
            var text=Label(board,_font,"ThemeHeading",0,119,740,72,30);
            text.fontSharedMaterial=_scriptTextMaterial;text.text=heading;text.color=ClinicalPanelStyle.ShellText;
            text.alignment=TextAlignmentOptions.Center;
            Fill(Rect(board,"ThemeDivider",0,76,690,2),new Color(1f,1f,1f,.16f),1);
            return board;
        }
        void ThemeChoice(RectTransform board,string name,string text,int index,int count,Action action)
        {
            float x=count==1?0:count==2?(index==0?-200:200):(index-1)*275;
            float y=6;
            var button=Button(board,_font,name,text,x,y,count==2?350:250,100,()=>QueueStationaryAction(action));
            button.GetComponentInChildren<TMP_Text>().fontSharedMaterial=_scriptTextMaterial;
            EmphasizeButton(button);
        }
        void FinishThemeBoard(RectTransform board,Action back,string backLabel)
        {
            if(back!=null)
            {
                var backButton=Button(board,_font,"ThemeBack",backLabel,-205,-121,360,58,()=>QueueStationaryAction(back));
                backButton.GetComponentInChildren<TMP_Text>().fontSharedMaterial=_scriptTextMaterial;
            }
            var leave=Button(board,_font,"ThemeLeave","选择房间",back==null?0:205,-121,360,58,()=>QueueStationaryAction(ContinueToDoor));
            leave.GetComponentInChildren<TMP_Text>().fontSharedMaterial=_scriptTextMaterial;
            ClinicalNearTouch.Bind(board,()=>InputAllowed);
        }
        void ShowRoomThemeSelector(int group=-1)
        {
            if(!InputAllowed)return;
            if(RoomId=="R02_STORAGE"){ShowStorageSampleSelector();return;}
            if(RoomId=="R03_WAITING"){ShowWaitingSelector();return;}
            var themes=RoomThemes();
            if(themes.Length==0){ShowScriptTask();return;}
            if(group<0 || group>=themes.Length)
            {
                var board=ThemeBoard("RoomThemeSelector",DisplayName+" · 选择检查内容");
                for(int i=0;i<themes.Length;i++)
                {
                    int selected=i;
                    ThemeChoice(board,"Theme_"+i,themes[i].Title,i,themes.Length,()=>
                    {
                        if(RoomId=="R01_OFFICE" && selected==1)
                        {SelectThemeTask("OF-01");return;}
                        if(RoomId=="R01_OFFICE" && selected==2)
                        {SelectThemeTask("OF-05");return;}
                        SelectThemeTask(themes[selected].Tasks[0]);
                    });
                }
                FinishThemeBoard(board,_themeChosen?()=>{_themeChosen=true;ShowScriptTask();}:null,"返回当前检查");
            }
            else
            {
                var theme=themes[group];var board=ThemeBoard("RoomThemeTasks",theme.Title);
                for(int i=0;i<theme.Tasks.Length;i++)
                {
                    var id=theme.Tasks[i];
                    ThemeChoice(board,"ThemeTask_"+id,_owner.Definition.FindTask(id).title,i,theme.Tasks.Length,()=>SelectThemeTask(id));
                }
                FinishThemeBoard(board,()=>ShowRoomThemeSelector(),"返回主题");
            }
        }
        void SelectThemeTask(string taskId)
        {
            int index=Array.IndexOf(ScriptTasks,taskId);
            if(!InputAllowed || index<0)return;
            _themeChosen=true;
            if(index==_scriptIndex){ShowScriptTask();return;}
            ChangeScriptTask(index);
        }
        void ShowWaitingSelector()
        {
            if(!InputAllowed)return;
            var board=ThemeBoard("WaitingObservationSelector","候诊区 · 选择观察方向");
            ThemeChoice(board,"WaitingSeats","观察座椅与隔断",0,2,()=>{_themeChosen=true;ChangeWaitingObservation(false);});
            ThemeChoice(board,"WaitingCorridor","观察诊疗通道侧",1,2,()=>{_themeChosen=true;ChangeWaitingObservation(true);});
            FinishThemeBoard(board,()=>{_themeChosen=true;ShowScriptTask();},"查看检查提示");
        }
    }
}
