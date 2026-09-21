using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
                var bindings=installer.CreateValidatedBindings();
                var rig=bindings.Platform.XrRigRoot.GetComponentInChildren<OVRCameraRig>(true);
                rig.EnsureGameObjectIntegrity();rig.centerEyeAnchor.localPosition=new Vector3(0,1.65f,0);
                var camera=bindings.Platform.Viewer.GetComponent<Camera>();camera.fieldOfView=75;
                foreach(var other in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))other.enabled=false;
                using(var runtime=new FullScriptJourneyRuntime(bindings,null,new Timing(),new PreviewRelease()))
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
                        for(int field=0;field<6;field++)
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
        static void Tick(FullScriptJourneyRuntime runtime){for(int i=0;i<35;i++)runtime.Tick(.05f);}
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
