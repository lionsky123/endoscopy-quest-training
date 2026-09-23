using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BotanicalGardenQR.FrontendShell.Runtime.ClinicalPanelStyle;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed partial class FullScriptRoomVisit
    {
        Transform[] _storageLeaves;
        bool _storageObservation;
        bool _storageClose;
        float _storageOpenStable;

        void ChangeStorageObservation(bool close)
        {
            if(_owner.RequestStorageObservation(close,()=>ShowStorageCabinet(close)))ClosePanel();
        }

        void ReturnFromStorageObservation()
        {
            if(_owner.RequestStorageObservation(false,ShowScriptTask))ClosePanel();
        }

        void ShowStorageCabinet(bool close)
        {
            if(!InputAllowed || RoomId!="R02_STORAGE")return;
            ClosePanel();
            if(_storageLeaves==null)
            {
                var cabinet=Room.Root.GetComponentsInChildren<Transform>().Single(t=>t.name=="TrainingStorageCabinet");
                _storageLeaves=new[]{cabinet.Find("LeftDoor"),cabinet.Find("RightDoor")};
                for(int i=0;i<_storageLeaves.Length;i++)
                {
                    var leaf=_storageLeaves[i];leaf.gameObject.SetActive(false);
                    var handle=leaf.Find("HandleAnchor");
                    var collider=leaf.gameObject.AddComponent<BoxCollider>();collider.center=handle.localPosition;collider.size=new Vector3(.095f,.36f,.12f);
                    var body=leaf.gameObject.AddComponent<Rigidbody>();body.useGravity=false;body.isKinematic=true;
                    var grab=leaf.gameObject.AddComponent<Grabbable>();grab.InjectOptionalRigidbody(body);grab.InjectOptionalThrowWhenUnselected(false);
                    var pivot=new GameObject(leaf.name+"Pivot").transform;pivot.SetParent(leaf.parent,false);pivot.localPosition=leaf.localPosition;
                    var rotate=leaf.gameObject.AddComponent<OneGrabRotateTransformer>();rotate.InjectOptionalPivotTransform(pivot);
                    rotate.InjectOptionalConstraints(new OneGrabRotateTransformer.OneGrabRotateConstraints
                    {MinAngle=new FloatConstraint{Constrain=true,Value=i==0?0:-105},MaxAngle=new FloatConstraint{Constrain=true,Value=i==0?105:0}});
                    grab.InjectOptionalOneGrabTransformer(rotate);grab.InjectOptionalTwoGrabTransformer(rotate);
                    var hand=leaf.gameObject.AddComponent<HandGrabInteractable>();hand.InjectRigidbody(body);hand.InjectOptionalPointableElement(grab);hand.HandAlignment=HandAlignType.None;
                    leaf.gameObject.SetActive(true);
                }
            }
            _storageObservation=true;_storageClose=close;_storageOpenStable=0;
            foreach(var leaf in _storageLeaves)
            {
                leaf.GetComponent<Grabbable>().enabled=close && !_owner.Session.IsFinished;
                leaf.GetComponent<HandGrabInteractable>().enabled=close && !_owner.Session.IsFinished;
            }
            _panel=new GameObject("StorageCabinetObservation",typeof(RectTransform),typeof(Canvas));
            var board=(RectTransform)_panel.transform;board.sizeDelta=new Vector2(720,180);board.localScale=Vector3.one*.00065f;
            board.SetPositionAndRotation(_scriptPose.position-Vector3.up*.22f,_scriptPose.rotation);
            var canvas=_panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_viewer.GetComponent<Camera>();
            Fill(board,new Color(.96f,.98f,1),2);
            var state=Label(board,_font,"CabinetState",0,38,680,75,22);state.fontSharedMaterial=_scriptTextMaterial;state.color=new Color(.015f,.025f,.035f);
            state.text=(close?"握住左右把手展开柜门，观察内壁":"整体观察：柜体外壳、门与通风构造")+"\n柜体结构示教 · 悬挂镜体仍待补";
            var distance=Button(board,_font,"SwitchStorageDistance",close?"回到整体观察":"靠近柜门操作",-175,-48,330,60,()=>QueueStationaryAction(()=>ChangeStorageObservation(!_storageClose)),true);
            distance.GetComponentInChildren<TMP_Text>().fontSharedMaterial=_scriptTextMaterial;EmphasizeButton(distance,false,false);
            var back=Button(board,_font,"ReturnToInspection","返回检查说明",175,-48,330,60,()=>QueueStationaryAction(ReturnFromStorageObservation),true);
            back.GetComponentInChildren<TMP_Text>().fontSharedMaterial=_scriptTextMaterial;EmphasizeButton(back,false,false);
            BotanicalGardenQR.FrontendShell.Runtime.ClinicalNearTouch.Bind(board,()=>InputAllowed);
        }

        void TickStorageCabinet(float dt)
        {
            if(!_storageObservation || !_storageClose || _storageLeaves==null || !_owner.Session.CanEdit)return;
            bool open=_storageLeaves.All(t=>Quaternion.Angle(t.localRotation,Quaternion.identity)>=70);
            _storageOpenStable=open?_storageOpenStable+dt:0;
            if(_storageOpenStable>.4f && _owner.ScriptActions.Add("ST-01:cabinet-opened"))
                _owner.Session.TryRecordLearningAction("ST-01",BotanicalGardenQR.Experience.Application.ClinicalLearningAction.Operated,"cabinet-opened");
            var state=_panel?_panel.transform.Find("CabinetState"):null;
            if(state)state.GetComponent<TMP_Text>().text=(open?"双门已展开，可观察真实柜内空间":"握住左右把手展开柜门，观察内壁")+"\n柜体结构示教 · 悬挂镜体仍待补";
        }

        void DisableStorageCabinetHands()
        {
            _storageObservation=false;_storageOpenStable=0;
            if(_storageLeaves==null)return;
            foreach(var leaf in _storageLeaves)
            {
                if(!leaf)continue;
                leaf.GetComponent<HandGrabInteractable>().enabled=false;
                leaf.GetComponent<Grabbable>().enabled=false;
            }
        }
    }
}
