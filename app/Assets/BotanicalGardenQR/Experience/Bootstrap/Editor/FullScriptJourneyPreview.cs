using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.VisitorCoach.Frontend;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Actual production room host, editor-only screenshots. No BuildPipeline or player launch.
    public static class FullScriptJourneyPreview
    {
        public static void Capture()
        {
            int code=1;
            try
            {
                var args=Environment.GetCommandLineArgs();
                var output=Path.GetFullPath(args[Array.IndexOf(args,"-bgqrCaptureOutput")+1]);
                Directory.CreateDirectory(output);
                var scene=EditorSceneManager.OpenScene("Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity",OpenSceneMode.Single);
                var installer=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<VisitorInstaller>(true)).Single();
                var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(installer.gameObject);
                if(prefab)PrefabUtility.UnpackPrefabInstance(prefab,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                installer.ConfigureArchivedBindingsForEditor();
                var bindings=installer.CreateValidatedBindings();
                var rig=bindings.Platform.XrRigRoot.GetComponentInChildren<OVRCameraRig>(true);
                rig.EnsureGameObjectIntegrity();rig.centerEyeAnchor.localPosition=new Vector3(0,1.65f,0);
                var camera=bindings.Platform.Viewer.GetComponent<Camera>();camera.fieldOfView=75;
                foreach(var other in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))other.enabled=false;
                using(var runtime=new FullScriptJourneyRuntime(bindings,null,new Timing(),new PreviewRelease(),stationary:false))
                {
                    runtime.StartExperience();
                    var changed=typeof(OVRCameraRig).GetField("UpdatedAnchors",BindingFlags.Instance|BindingFlags.NonPublic);
                    (changed.GetValue(rig) as Action<OVRCameraRig>)?.Invoke(rig);
                    Tick(runtime);
                    Save(camera,output,"01-lobby-opening");
                    var p=runtime.Prologue;p.ReportHandAvailability(true);var epoch=p.CurrentState.Epoch;
                    p.RequestInvitation(epoch,VisitorPrologueInputModality.PalmHold);
                    p.ReportArrivalReady(epoch);p.ReportBookOpened(epoch);p.BeginDialogue(epoch,4);
                    p.AdvanceDialogue(epoch,p.CurrentState.Version);p.ChooseReply(epoch,p.CurrentState.Version,VisitorEncounterReply.PromiseToExplore);
                    p.AdvanceDialogue(epoch,p.CurrentState.Version);p.AdvanceDialogue(epoch,p.CurrentState.Version);
                    runtime.Visit.TryOpen(FullScriptRoomCatalog.Overview);Tick(runtime);
                    FacePanel(camera,runtime.Visit.Panel);
                    Save(camera,output,"02-lobby-modes");
                    runtime.Visit.Panel.transform.Find(args.Contains("-bgqrIndependent")?"ModeIndependent":"ModeGuided").GetComponent<Button>().onClick.Invoke();
                    var route=runtime.Definition.mainlineRoomIds.Skip(1).ToArray();
                    int index=3;
                    foreach(var id in route)
                    {
                        var entry=runtime.Visit.Room.Frame.Transform(runtime.Visit.Map.start);
                        camera.transform.position=new Vector3(entry.x,1.65f,entry.z);
                        if(!runtime.Visit.TryOpen(FullScriptRoomCatalog.Door))throw new InvalidOperationException("Door unavailable");
                        FacePanel(camera,runtime.Visit.Panel);Save(camera,output,$"{index++:00}-door");
                        if(!runtime.RequestRoom(id))throw new InvalidOperationException("Transition rejected: "+id);
                        Tick(runtime);
                        if(runtime.Visit.RoomId!=id)throw new InvalidOperationException("Wrong loaded room");
                        var first=runtime.Visit.Room.Frame.Transform(runtime.Visit.Map.routes[0].samples[1]);
                        var spawn=runtime.Visit.Room.Frame.Transform(runtime.Visit.Map.start);
                        camera.transform.rotation=Quaternion.LookRotation(new Vector3(first.x-spawn.x,0,first.z-spawn.z));
                        Save(camera,output,$"{index++:00}-"+id);
                        if(id=="R01_OFFICE")
                        {
                            var point=runtime.Visit.Room.Frame.Transform(runtime.Visit.Map.points[0].position);
                            camera.transform.position=new Vector3(point.x,1.65f,point.z);
                            runtime.Visit.TryOpen(FullScriptRoomCatalog.Overview);Tick(runtime);
                            if(runtime.Visit.OfficeTerminal.activeSelf)
                            {
                                var standing=camera.transform.position;
                                FacePanel(camera,runtime.Visit.OfficeTerminal);Save(camera,output,$"{index++:00}-office-terminal");
                                camera.transform.position=standing;
                                runtime.Visit.OfficeTerminal.GetComponentInChildren<Button>().onClick.Invoke();
                            }
                            FacePanel(camera,runtime.Visit.Panel);Save(camera,output,$"{index++:00}-office-records-or-summary");
                        }
                    }
                    if(args.Contains("-bgqrIndependent"))
                    {
                        var panel=runtime.Visit.Panel;
                        panel.transform.Find("SubmitJourney").GetComponent<Button>().onClick.Invoke();
                        FacePanel(camera,panel);Save(camera,output,"20-submitted-summary");
                        for(int field=0;field<ClinicalTrainingRecords.FieldCount;field++)
                        {
                            panel.transform.Find("RecordReview").GetComponent<Button>().onClick.Invoke();
                            Save(camera,output,$"{21+field:00}-field-review");
                        }
                        for(int task=0;task<runtime.Definition.rooms.Sum(r=>r.taskIds.Length);task++)
                        {
                            panel.transform.Find("ResultDetails").GetComponent<Button>().onClick.Invoke();
                            Save(camera,output,$"{27+task:00}-task-record");
                        }
                    }
                    File.WriteAllText(Path.Combine(output,"capture-notes.txt"),
                        "Android target; editor-only production FullScriptJourneyRuntime.\nOpening completion and UI callbacks are driven by this preview harness; screenshots are not headset or hand-interaction validation.\nOffice: published supplied model with standing bay. Other non-washing rooms: development geometry until separately published. Office records: fictional training data, not original evidence or clinical standard.\n");
                }
                code=0;
            }
            catch(Exception error){Debug.LogException(error);}
            EditorApplication.Exit(code);
        }
        public static void CaptureStationary()
        {
            int code=1;
            try
            {
                var args=Environment.GetCommandLineArgs();var output=Path.GetFullPath(args[Array.IndexOf(args,"-bgqrCaptureOutput")+1]);Directory.CreateDirectory(output);
                var scene=EditorSceneManager.OpenScene("Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity",OpenSceneMode.Single);
                var installer=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<VisitorInstaller>(true)).Single();
                var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(installer.gameObject);
                if(prefab)PrefabUtility.UnpackPrefabInstance(prefab,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var bindings=installer.CreateValidatedBindings();var rig=bindings.Platform.XrRigRoot.GetComponentInChildren<OVRCameraRig>(true);
                rig.EnsureGameObjectIntegrity();rig.centerEyeAnchor.localPosition=new Vector3(0,1.2f,0);
                var camera=bindings.Platform.Viewer.GetComponent<Camera>();camera.fieldOfView=78;
                foreach(var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))c.enabled=false;
                using(var runtime=new FullScriptJourneyRuntime(bindings,null,new Timing(),new PreviewRelease()))
                {
                    runtime.StartExperience();
                    (typeof(OVRCameraRig).GetField("UpdatedAnchors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(rig) as Action<OVRCameraRig>)?.Invoke(rig);
                    Tick(runtime);Save(camera,output,"01-entry-guide");
                    ContinueEntryGuide(runtime);Tick(runtime);Save(camera,output,"02-lobby-choices");
                    int index=3;
                    foreach(var id in runtime.Definition.mainlineRoomIds.Skip(1))
                    {
                        runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);runtime.RequestRoom(id);Tick(runtime);
                        if(runtime.Visit?.RoomId!=id)throw new InvalidOperationException("Wrong room: "+id);
                        Save(camera,output,$"{index++:00}-{id}-entry-guide");
                        ContinueEntryGuide(runtime);Tick(runtime);
                        Save(camera,output,$"{index++:00}-{id}-current-task");
                        if(id=="R02_STORAGE")
                        {
                            runtime.Visit.Panel.transform.Find("StorageSample_ST-01").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"storage-cabinet-task");
                            runtime.Visit.Panel.transform.Find("ExpandReference").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"storage-teaching-expanded");
                            runtime.Visit.Panel.transform.Find("ExpandReference").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            runtime.Visit.Panel.transform.Find("InspectStorageCabinet").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"storage-cabinet-overview");
                            runtime.Visit.Panel.transform.Find("SwitchStorageDistance").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"storage-cabinet-closed");
                            foreach(var leaf in runtime.Visit.Room.Root.GetComponentsInChildren<Transform>().Where(t=>t.name=="LeftDoor"||t.name=="RightDoor"))
                                leaf.localRotation=Quaternion.Euler(0,leaf.name=="LeftDoor"?85:-85,0);
                            Tick(runtime);Save(camera,output,"storage-cabinet-open");
                            runtime.Visit.Panel.transform.Find("ReturnToInspection").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            runtime.Visit.Panel.transform.Find("NextTask").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            runtime.Visit.Panel.transform.Find("OpenStorageRecords").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"storage-cleaning-register");
                            runtime.Visit.Panel.transform.Find("StorageWeek2").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"storage-cleaning-register-selected");
                            runtime.Visit.Panel.transform.Find("ReturnToInspection").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            runtime.Visit.Panel.transform.Find("InspectStorageRegister").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"storage-cabinet-side-register");
                            var mounted=runtime.Visit.Room.Root.GetComponentsInChildren<Transform>().Single(t=>t.name=="CabinetSideRegister");
                            mounted.Find("OpenMountedRegister").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"storage-cabinet-register-expanded");
                            runtime.Visit.Panel.transform.Find("ReturnToInspection").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            runtime.Visit.Panel.transform.Find("NextTask").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            runtime.Visit.Panel.transform.Find("InspectObject").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"gastroscope-object-inspection");
                            runtime.Visit.Panel.transform.Find("ReturnToInspection").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                        }
                        if(id=="R03_WAITING")
                        {
                            runtime.Visit.Panel.transform.Find("InspectEnvironment").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"waiting-observation");
                            runtime.Visit.Panel.transform.Find("SwitchObservationSide").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"waiting-corridor-observation");
                            runtime.Visit.Panel.transform.Find("ReturnToInspection").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                        }
                        if(id=="R01_OFFICE" && !runtime.Session.HasVisitedAllMainlineRooms)
                        {
                            runtime.Visit.Panel.transform.Find("NextTask").GetComponent<Button>().onClick.Invoke();
                            Tick(runtime);
                            runtime.Visit.Panel.transform.Find("OpenCurrentDocument").GetComponent<Button>().onClick.Invoke();
                            Tick(runtime);
                            Save(camera,output,"office-seated-records");
                            runtime.Visit.Panel.transform.Find("OpenDocuments").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            runtime.Visit.Panel.transform.Find("Doc1").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"office-leak-register-paper");
                            runtime.Visit.Panel.transform.Find("LeakUseLink1").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                            Save(camera,output,"office-leak-assessment-guided");
                            runtime.Visit.Panel.transform.Find("ReturnToTerminal").GetComponent<Button>().onClick.Invoke();
                            Tick(runtime);
                        }
                        if(id==FullScriptRoomCatalog.Washing)
                            for(int n=0;n<5;n++)
                            {
                                runtime.Visit.Panel.transform.Find("NextTask").GetComponent<Button>().onClick.Invoke();
                                Tick(runtime);
                                Save(camera,output,"washing-"+runtime.Visit.ScriptTaskId);
                                if(runtime.Visit.ScriptTaskId=="RE-05")
                                {
                                    runtime.Visit.Panel.transform.Find("NextDetail").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                                    runtime.Visit.Panel.transform.Find("OpenLinkedLeakRecords").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                                    Save(camera,output,"washing-linked-leak-ledger");
                                    runtime.Visit.Panel.transform.Find("BackToRows").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                                    Save(camera,output,"washing-linked-use-records");
                                    runtime.Visit.Panel.transform.Find("ReturnToTerminal").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                                }
                                if(runtime.Visit.ScriptTaskId=="RE-04")
                                {
                                    runtime.Visit.Panel.transform.Find("InspectObject").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                                    Save(camera,output,"disinfectant-object-inspection");
                                    runtime.Visit.Panel.transform.Find("ReturnToInspection").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                                }
                                if(runtime.Visit.ScriptTaskId=="RE-02")
                                {
                                    for(int detail=0;detail<2;detail++)
                                    {runtime.Visit.Panel.transform.Find("NextDetail").GetComponent<Button>().onClick.Invoke();Tick(runtime);}
                                    Save(camera,output,"washing-brush-overview");
                                    runtime.Visit.Panel.transform.Find("ExpandReference").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                                    runtime.Visit.Panel.transform.Find("BrushFocus").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                                    Save(camera,output,"washing-brush-bristles");
                                    runtime.Visit.Panel.transform.Find("BrushFocus").GetComponent<Button>().onClick.Invoke();Tick(runtime);
                                    Save(camera,output,"washing-brush-wire");
                                }
                            }
                    }
                    File.WriteAllText(Path.Combine(output,"stationary-capture.txt"),"Production runtime / Android target / Vulkan editor / fixed 1.2m viewer; callbacks driven by preview, not headset proof. No APK. Source scale remains provisional.\n");
                }
                code=0;
            }
            catch(Exception e){Debug.LogException(e);}
            EditorApplication.Exit(code);
        }
        static void Tick(FullScriptJourneyRuntime runtime){for(int i=0;i<35;i++)runtime.Tick(.05f);}
        static void ContinueEntryGuide(FullScriptJourneyRuntime runtime)
        {
            var presenter=Object.FindObjectsByType<VisitorCoachPresenter>(FindObjectsInactive.Include,FindObjectsSortMode.None).SingleOrDefault();
            var state=presenter?.CurrentState;
            if(state?.Owner!=VisitorDialogueOwner.Guidance)return;
            presenter.Hide(state.Context);
            runtime.Visit.BeginFromEntryGuide();
        }
        static void FacePanel(Camera camera,GameObject panel)
        {
            if(!panel)throw new InvalidOperationException("Expected panel missing");
            camera.transform.position=panel.transform.position-panel.transform.forward*.65f;
            camera.transform.rotation=panel.transform.rotation;
        }
        static void Save(Camera camera,string output,string name)
        {
            Canvas.ForceUpdateCanvases();
            var target=new RenderTexture(1440,1080,24);target.Create();
            var old=RenderTexture.active;Texture2D image=null;
            try
            {
                camera.targetTexture=target;
                image=new Texture2D(1440,1080,TextureFormat.RGBA32,false);
                for(int pass=0;pass<2;pass++)
                {
                    camera.Render();RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,1440,1080),0,0);image.Apply();
                }
                File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=old;
                if(image)Object.DestroyImmediate(image);target.Release();Object.DestroyImmediate(target);
            }
        }
        // Editor screenshot stepping is synchronous; real runtime uses UnloadUnusedAssets.
        sealed class PreviewRelease:IClinicalRoomAssetRelease
        {public void Begin(){} public bool IsComplete=>true;}
        sealed class Timing:ITrackingOriginTiming
        {
            public bool TryGetSampleTime(out double time){time=9;return true;}
            public bool TryGetChangeTime(OVRManager.TrackingOrigin origin,out double time){time=10;return true;}
        }
    }
}
