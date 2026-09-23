using System.Linq;
using BotanicalGardenQR.FrontendShell.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed partial class FullScriptRoomVisit
    {
        GameObject _storageRegister;
        bool _storageRegisterActive, _storageRegisterAtSide;
        void EnsureStorageRegister()
        {
            if(RoomId!="R02_STORAGE" || _storageRegister)return;
            var cabinet=Room.Root.GetComponentsInChildren<Transform>().SingleOrDefault(t=>t.name=="TrainingStorageCabinet");
            if(!cabinet)return;
            _storageRegister=new GameObject("CabinetSideRegister",typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_storageRegister.transform;board.SetParent(cabinet,false);
            // Left outer wall ends at x=-.6175m. The right aisle is crossed by
            // the preserved source corridor door; do not hide that geometry.
            board.localPosition=new Vector3(-.622f,1.2f,0);board.localRotation=Quaternion.Euler(0,90,0);
            board.localScale=Vector3.one*.00045f;board.sizeDelta=new Vector2(800,800f*1024/1536);
            var canvas=_storageRegister.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_viewer.GetComponent<Camera>();
            var content=Rect(board,"RegisterContent",0,-72,800,800f*1024/1536);
            BuildStoragePaper(content,false);
            var button=Button(board,_font,"OpenMountedRegister","",0,0,800,800f*1024/1536,()=>QueueStationaryAction(()=>
            {if(_storageRegisterActive)ShowStorageRecords();}));
            button.GetComponent<Image>().color=Color.clear;
            ClinicalNearTouch.Bind(board,()=>InputAllowed && _storageRegisterActive);
        }
        void ChangeStorageRegisterObservation()
        {
            EnsureStorageRegister();
            if(_storageRegister && _owner.RequestStorageRegisterObservation(_storageRegister.transform,ShowStorageRegisterObservation))ClosePanel();
        }
        void ShowStorageRegisterObservation()
        {
            if(!InputAllowed)return;
            ClosePanel();_storageRegisterAtSide=true;_storageRegisterActive=true;
            _panel=new GameObject("StorageRegisterObservation",typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_panel.transform;board.sizeDelta=new Vector2(760,150);board.localScale=Vector3.one*.00065f;
            board.SetPositionAndRotation(_scriptPose.position-Vector3.up*.20f,_scriptPose.rotation);
            var canvas=_panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_viewer.GetComponent<Camera>();
            Fill(board,new Color(.96f,.98f,1),2);
            var text=Label(board,_font,"RegisterObservationHint",0,42,720,45,22);text.fontSharedMaterial=_scriptTextMaterial;text.color=new Color(.015f,.025f,.035f);
            text.text="柜侧模拟登记 · 近触纸面放大阅读";
            var back=Button(board,_font,"ReturnToInspection","返回检查说明",0,-25,700,60,()=>QueueStationaryAction(ReturnFromStorageRegister),true);
            back.GetComponentInChildren<TMP_Text>().fontSharedMaterial=_scriptTextMaterial;EmphasizeButton(back,false,false);
            ClinicalNearTouch.Bind(board,()=>InputAllowed);
        }
        void ReturnFromStorageRegister()
        {
            if(!_storageRegisterAtSide){ShowScriptTask();return;}
            if(_owner.RequestStorageObservation(false,()=>{_storageRegisterAtSide=false;ShowScriptTask();}))ClosePanel();
        }
        void DisposeStorageRegister()
        {
            _storageRegisterActive=false;
            if(_storageRegister){_storageRegister.SetActive(false);DestroyScriptAsset(_storageRegister);_storageRegister=null;}
        }
    }
}
