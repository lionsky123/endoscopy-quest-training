using System;
using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.MapNavigation.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Disposable editor view of the production room loader, not a baked room copy.
    public sealed class InspectionWorkspace : EditorWindow
    {
        const string ConfigPath="Assets/EndoscopyTheme/Resources/ClinicalCourse/inspection-views.asset";
        const string ScenePath="Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity";
        InspectionViewConfiguration _configuration;
        VirtualRoomEnvironment _room;
        GameObject _rig;
        Camera _camera;
        Scene _previewScene;
        InspectionSceneView _sceneView;
        string[] _ids,_names;
        int _selected,_viewIndex;
        bool _standing;
        bool _refresh;
        Vector2 _scroll;

        [MenuItem("Endoscopy/当前游戏工作区",false,0)]
        public static void Open()=>GetWindow<InspectionWorkspace>("内镜检查工作区").Show();

        [MenuItem("Endoscopy/运行模式/编辑器预览（无需头显）",false,1)]
        public static void UsePreview()=>EditorPrefs.SetBool(InspectionEditorPreview.Preference,false);
        [MenuItem("Endoscopy/运行模式/真实头显",false,2)]
        public static void UseHeadset()=>EditorPrefs.SetBool(InspectionEditorPreview.Preference,true);

        public static void Prepare()
        {
            EnsureConfiguration();
            // Host editor API only: retain Android as the active build target.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Vulkan});
            UsePreview();
            AssetDatabase.SaveAssets();
            Debug.Log("Inspection authoring ready. Android target retained; no player build.");
        }

        static InspectionViewConfiguration EnsureConfiguration()
        {
            var config=AssetDatabase.LoadAssetAtPath<InspectionViewConfiguration>(ConfigPath);
            if(config)return config;
            var definition=ClinicalJourneyConfiguration.Load();
            var washing=VisitorMapConfiguration.Resolve(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json"));
            config=CreateInstance<InspectionViewConfiguration>();
            config.rooms=definition.rooms.Select(r=>
            {
                var map=FullScriptRoomCatalog.Map(r.id,washing,false,true);
                var a=map.start;var b=map.routes[0].samples[1];
                float yaw=Mathf.Atan2(b.x-a.x,b.z-a.z)*Mathf.Rad2Deg;
                var initial=map.points[0].position;
                return new InspectionViewConfiguration.Room {
                    id=r.id,initial=View("Initial",initial,yaw),
                    inspections=r.taskIds.Select(task=> {
                        int index=task=="RE-01"?0:task=="RE-02"?2:task=="RE-03"||task=="RE-06"?3:task=="RE-04"?4:5;
                        return View(task,r.id==FullScriptRoomCatalog.Washing?washing.points[index].position:initial,yaw);
                    }).ToArray()
                };
            }).ToArray();
            AssetDatabase.CreateAsset(config,ConfigPath);AssetDatabase.SaveAssets();
            return config;
        }
        static InspectionViewConfiguration.View View(string id,MapPosition p,float yaw)
            =>new InspectionViewConfiguration.View{id=id,position=new Vector3(p.x,p.y,p.z),yaw=yaw};

        void OnEnable()
        {
            EditorApplication.playModeStateChanged+=ModeChanged;
            EditorApplication.projectChanged+=RequestRefresh;
            Undo.undoRedoPerformed+=RequestRefresh;
            SceneView.duringSceneGui+=SceneGui;
            _refresh=true;
        }
        void OnDisable()
        {
            EditorApplication.playModeStateChanged-=ModeChanged;
            EditorApplication.projectChanged-=RequestRefresh;
            Undo.undoRedoPerformed-=RequestRefresh;
            SceneView.duringSceneGui-=SceneGui;
            Release();
            if(_sceneView)_sceneView.Close();
        }
        void ModeChanged(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingEditMode)Release();
            if(state==PlayModeStateChange.EnteredEditMode)_refresh=true;
        }
        void RequestRefresh(){_refresh=true;Repaint();}
        void OnInspectorUpdate()
        {
            if(_refresh && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                _refresh=false;
                try{RefreshRoom();}catch(Exception e){Release();Debug.LogException(e);}
            }
            Repaint();
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("当前游戏 · 同源房间与检查点",EditorStyles.boldLabel);
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
                EditorGUILayout.HelpBox("当前为无图形模式，无法在 Scene 视图取景。请以图形编辑器打开工作区。",MessageType.Info);
            bool headset=EditorPrefs.GetBool(InspectionEditorPreview.Preference,false);
            bool next=EditorGUILayout.Toggle("Play使用真实头显",headset);
            if(next!=headset)EditorPrefs.SetBool(InspectionEditorPreview.Preference,next);
            EditorGUILayout.HelpBox(headset?"真实头显模式：等待可靠头手追踪。":"编辑器预览：同一业务流程、固定观察相机；下方调试控件不进入Quest，不作为手部验收。",MessageType.Info);
            if(EditorApplication.isPlaying){DrawRuntime();return;}
            if(_ids==null || !_configuration){if(GUILayout.Button("加载当前配置"))RequestRefresh();return;}
            EditorGUI.BeginChangeCheck();
            _selected=EditorGUILayout.Popup("当前房间",_selected,_names);
            if(EditorGUI.EndChangeCheck()){_viewIndex=0;RequestRefresh();}
            var data=_configuration.Find(_ids[_selected]);
            if(data==null)return;
            var views=new[]{data.initial}.Concat(data.inspections).ToArray();
            _viewIndex=Mathf.Clamp(_viewIndex,0,views.Length-1);
            EditorGUI.BeginChangeCheck();
            _viewIndex=EditorGUILayout.Popup("观察位置",_viewIndex,views.Select(v=>v.id).ToArray());
            _standing=EditorGUILayout.Toggle("站姿取景（否则坐姿）",_standing);
            if(EditorGUI.EndChangeCheck())FocusView();
            var view=views[_viewIndex];
            EditorGUI.BeginChangeCheck();
            var position=EditorGUILayout.Vector3Field("房间内位置",view.position);
            var yaw=EditorGUILayout.FloatField("观察朝向",view.yaw);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_configuration,"Move inspection viewpoint");
                view.position=position;view.yaw=yaw;EditorUtility.SetDirty(_configuration);FocusView();
            }
            if(GUILayout.Button("保存观察配置并刷新")){AssetDatabase.SaveAssets();RequestRefresh();}
            if(GUILayout.Button("Scene视图对齐当前观察位"))FocusView();
            if(GUILayout.Button("打开正式游戏并Play"))
            {
                if(!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
                Release();UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
                EditorApplication.EnterPlaymode();
            }
            EditorGUILayout.LabelField("房间加载器",_room?.Root?_room.Root.name:"未加载");
            EditorGUILayout.HelpBox("只实例化所选房间。资源导入或配置变化后自动刷新；场景标记写回同一观察配置。原始模型与历史洗消工作区保留。",MessageType.None);
        }
        void RefreshRoom()
        {
            Release();
            _configuration=EnsureConfiguration();
            var definition=ClinicalJourneyConfiguration.Load();
            _ids=definition.rooms.Select(r=>r.id).ToArray();_names=definition.rooms.Select(r=>r.displayName+" · "+r.id).ToArray();
            _selected=Mathf.Clamp(_selected,0,_ids.Length-1);
            var map=FullScriptRoomCatalog.Map(_ids[_selected],VisitorMapConfiguration.Resolve(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json")),false,true);
            _previewScene=EditorSceneManager.NewPreviewScene();
            _rig=new GameObject("InspectionEditorPreviewRig"){hideFlags=HideFlags.DontSave};
            SceneManager.MoveGameObjectToScene(_rig,_previewScene);
            _room=VirtualRoomEnvironment.Create(_rig,null,map,trackHead:false);
            SceneManager.MoveGameObjectToScene(_room.Root,_previewScene);
            foreach(var t in _room.Root.GetComponentsInChildren<Transform>(true))t.gameObject.hideFlags=HideFlags.DontSave;
            var cameraObject=new GameObject("InspectionEditorView",typeof(Camera)){hideFlags=HideFlags.DontSave};
            cameraObject.transform.SetParent(_rig.transform,false);_camera=cameraObject.GetComponent<Camera>();
            _camera.nearClipPlane=.03f;_camera.fieldOfView=78;_camera.enabled=false;
            _camera.scene=_previewScene;
            _camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=new Color(.16f,.2f,.24f);
            FocusView();
        }
        void FocusView()
        {
            if(!_room?.Root || !_camera || !_configuration || _ids==null)return;
            var data=_configuration.Find(_ids[_selected]);var views=new[]{data.initial}.Concat(data.inspections).ToArray();
            var view=views[Mathf.Clamp(_viewIndex,0,views.Length-1)];
            _camera.transform.SetPositionAndRotation(_room.Root.transform.TransformPoint(view.position)+Vector3.up*(_standing?1.65f:1.2f),_room.Root.transform.rotation*Quaternion.Euler(0,view.yaw,0));
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            if(!_sceneView)_sceneView=GetWindow<InspectionSceneView>("当前检查房间");
            var scene=_sceneView;
            scene.ShowRoom(_previewScene);
            scene.in2DMode=false;scene.orthographic=false;scene.sceneLighting=true;scene.drawGizmos=true;
            scene.AlignViewToObject(_camera.transform);scene.Repaint();
        }
        void SceneGui(SceneView scene)
        {
            if(scene!=_sceneView || EditorApplication.isPlaying || !_room?.Root || !_configuration || _ids==null)return;
            var data=_configuration.Find(_ids[_selected]);if(data==null)return;
            var views=new[]{data.initial}.Concat(data.inspections).ToArray();
            for(int i=0;i<views.Length;i++)
            {
                var view=views[i];var root=_room.Root.transform;var point=root.TransformPoint(view.position);
                Handles.color=i==_viewIndex?Color.yellow:Color.cyan;
                Handles.DrawWireDisc(point,Vector3.up,.18f);Handles.Label(point+Vector3.up*.15f,view.id);
                Handles.ArrowHandleCap(0,point,root.rotation*Quaternion.Euler(0,view.yaw,0),.5f,EventType.Repaint);
                if(i!=_viewIndex)continue;
                EditorGUI.BeginChangeCheck();var changed=Handles.PositionHandle(point,root.rotation);
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_configuration,"Move inspection viewpoint");view.position=root.InverseTransformPoint(changed);
                    EditorUtility.SetDirty(_configuration);Repaint();
                }
            }
        }
        void DrawRuntime()
        {
            var installer=Object.FindFirstObjectByType<VisitorInstaller>();var runtime=installer?installer.Journey:null;
            if(runtime==null){EditorGUILayout.LabelField("等待正式入口装配");return;}
            EditorGUILayout.LabelField("运行阶段",runtime.LoadingStage);
            EditorGUILayout.LabelField("当前房间",runtime.Session.CurrentRoomId);
            EditorGUILayout.LabelField("检查点",runtime.Visit?.ScriptTaskId??"等待");
            EditorGUILayout.LabelField("输入就绪",runtime.InputAllowed.ToString());
            if(!InspectionEditorPreview.Enabled)return;
            _scroll=EditorGUILayout.BeginScrollView(_scroll);
            var panel=runtime.Visit?.Panel;
            if(panel)foreach(var button in panel.GetComponentsInChildren<Button>().ToArray())
            {
                if(!button || !button.IsActive() || !button.IsInteractable())continue;
                var label=button.GetComponentInChildren<TMPro.TMP_Text>();
                using(new EditorGUI.DisabledScope(!runtime.InputAllowed))
                    if(GUILayout.Button(label?label.text:button.name)){button.onClick.Invoke();break;}
            }
            EditorGUILayout.EndScrollView();
        }
        void Release()
        {
            _camera=null;
            _room?.Dispose();_room=null;
            if(_rig)Object.DestroyImmediate(_rig);_rig=null;
            if(_sceneView)_sceneView.ShowRoom(default);
            if(_previewScene.IsValid())EditorSceneManager.ClosePreviewScene(_previewScene);
            _previewScene=default;
            // Destroy references first; the editor releases unused assets before rebuilding.
            if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorUtility.UnloadUnusedAssetsImmediate();
        }

        // Captures the same camera used by the authoring view, without changing source poses.
        public static void CaptureRoomPreviews()
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-inspectionPreviewOutput");
            if(index<0 || index+1>=args.Length)throw new ArgumentException("Preview output folder required.");
            var folder=System.IO.Path.GetFullPath(args[index+1]);System.IO.Directory.CreateDirectory(folder);
            var workspace=CreateInstance<InspectionWorkspace>();
            try
            {
                workspace.RefreshRoom();
                foreach(var id in workspace._ids)
                {
                    workspace._selected=Array.IndexOf(workspace._ids,id);workspace._viewIndex=0;
                    workspace.RefreshRoom();
                    foreach(bool standing in new[]{false,true})
                    {
                        workspace._standing=standing;workspace.FocusView();
                        ProductionPlayProbe.Capture(workspace._camera,System.IO.Path.Combine(folder,id+(standing?"-standing":"-seated")+".png"));
                    }
                }
                Debug.Log("[InspectionPreview] Captured every configured room seated and standing from the current workspace configuration.");
            }
            finally{DestroyImmediate(workspace);}
        }
    }
}
