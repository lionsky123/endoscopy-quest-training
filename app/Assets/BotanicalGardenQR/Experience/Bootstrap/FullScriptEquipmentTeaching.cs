using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.FrontendShell.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Bootstrap
{
    // Coordinates are normalized against V09 and the source photograph. The latter
    // has the same 3:4 framing. These are visible objects, not a complete inventory.
    internal readonly struct EquipmentImageHotspot
    {
        internal readonly string Key, Name, Prompt;
        internal readonly Rect Area;
        internal EquipmentImageHotspot(string key,string name,string prompt,Rect area)
        {Key=key;Name=name;Prompt=prompt;Area=area;}
    }

    internal sealed partial class FullScriptRoomVisit
    {
        internal static readonly EquipmentImageHotspot[] EquipmentImageHotspots =
        {
            new EquipmentImageHotspot("air-gun","气枪","看透明软管与枪头；用途及压力参数仍需核对实物和说明书。",new Rect(.17f,.46f,.13f,.09f)),
            new EquipmentImageHotspot("leak-tester","测漏仪","查看左侧显示盒；型号、校验状态和测漏记录仍需查证。",new Rect(.31f,.35f,.13f,.09f)),
            new EquipmentImageHotspot("water-gun","水枪","看右侧黑色盘管与手持枪；水路和参数仍需核对实物。",new Rect(.77f,.43f,.13f,.09f))
        };

        int _equipmentSelected = -1;

        void ShowEquipmentTeaching()
        {
            if(!InputAllowed || RoomId!=FullScriptRoomCatalog.Washing || ScriptTaskId!="RE-02" || _detailIndex!=0)return;
            ClosePanel();EnsureScriptTextMaterial();EnsureScriptPose();
            _panel=new GameObject("EquipmentTeachingPanel",typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_panel.transform;
            board.sizeDelta=new Vector2(1000,930);board.localScale=Vector3.one*.00065f;
            var canvas=_panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            canvas.worldCamera=_viewer.GetComponent<Camera>();canvas.sortingOrder=130;
            board.SetPositionAndRotation(_scriptPose.position,_scriptPose.rotation);
            Frame(board);
            TMP_Text Copy(string name,string copy,float x,float y,float width,float height,int size)
            {
                var label=Label(board,_font,name,x,y,width,height,size);
                label.fontSharedMaterial=_scriptTextMaterial;label.color=new Color(.015f,.025f,.035f);
                label.text=copy;return label;
            }
            Copy("EquipmentTitle","逐件查看图中设备",0,404,910,58,29);
            var path="ClinicalCourse/FullScriptVisuals/"+(_showScriptSource?"script-equipment":"equipment-hotspot-teaching-v1");
            var texture=Resources.Load<Texture2D>(path);
            if(texture)
            {
                var picture=Rect(board,"EquipmentReference",-225,-15,500,667).gameObject.AddComponent<RawImage>();
                picture.texture=texture;picture.raycastTarget=false;
                for(int i=0;i<EquipmentImageHotspots.Length;i++)
                {
                    int index=i;
                    var hotspot=EquipmentImageHotspots[i];var area=hotspot.Area;
                    var marker=Button(picture.transform,_font,"EquipmentHotspot_"+hotspot.Key,(i+1).ToString(),
                        (area.center.x-.5f)*500,(area.center.y-.5f)*667,
                        area.width*500,area.height*667,()=>QueueStationaryAction(()=>SelectEquipmentHotspot(index)));
                    EmphasizeButton(marker,_equipmentSelected==i,false);
                }
            }
            else Copy("EquipmentMissing","图片加载失败，请返回检查说明后重试。",-225,0,500,100,23);
            Copy("EquipmentOrigin",_showScriptSource?"剧本原图 · 图中文字仅供定位":"生成教学图 · 非实拍记录",245,303,410,62,21);
            Copy("EquipmentInstruction",texture?"轻触图上编号，逐件查看。":"图片暂不可用，请切换图片或返回重试。",245,241,410,54,21);
            for(int i=0;texture && i<EquipmentImageHotspots.Length;i++)
            {
                int index=i;var item=EquipmentImageHotspots[i];
                var viewed=_owner.ScriptActions.Contains("RE-02:equipment:"+item.Key);
                var choice=Button(board,_font,"EquipmentChoice_"+item.Key,
                    (i+1)+"  "+item.Name+(viewed?" · 已查阅":" · 待查阅"),245,160-i*82,385,66,
                    ()=>QueueStationaryAction(()=>SelectEquipmentHotspot(index)));
                EmphasizeButton(choice,_equipmentSelected==i,false);
            }
            Copy("EquipmentPrompt",!texture?"":_equipmentSelected<0?"请选择一件设备。":EquipmentImageHotspots[_equipmentSelected].Prompt,
                245,-119,410,125,21);
            Copy("EquipmentBoundary","图中其他物件仍需现场核对；逐件查阅不代表设备核查完成。",
                245,-265,410,80,19);
            var compare=Button(board,_font,"CompareEquipmentSource",_showScriptSource?"返回教学图":"对照原图",
                -225,-407,360,62,()=>QueueStationaryAction(()=>{_showScriptSource=!_showScriptSource;ShowEquipmentTeaching();}));
            var back=Button(board,_font,"ReturnFromEquipment","返回检查说明",245,-407,385,62,
                ()=>QueueStationaryAction(ShowScriptTask));
            foreach(var button in new[]{compare,back})EmphasizeButton(button,false,false);
            ClinicalNearTouch.Bind(board,()=>InputAllowed);
        }

        void SelectEquipmentHotspot(int index)
        {
            if(index<0 || index>=EquipmentImageHotspots.Length || !InputAllowed)return;
            _equipmentSelected=index;
            if(_owner.Session.CanEdit)
            {
                var key="equipment:"+EquipmentImageHotspots[index].Key;
                _owner.Session.TryRecordLearningAction("RE-02",ClinicalLearningAction.Observed,key);
                _owner.ScriptActions.Add("RE-02:"+key);
            }
            ShowEquipmentTeaching();
        }
    }
}
