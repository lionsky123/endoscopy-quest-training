using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Enters real Play mode: does not construct the runtime or fake XR samples.
    [InitializeOnLoad]
    public static class ProductionPlayProbe
    {
        const string Key = "Endoscopy.ProductionPlayProbe.";
        static double _entered;
        static ProductionPlayProbe()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-endoscopyProbeOutput");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Probe output required.");
            var path = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            SessionState.SetString(Key + "Output", path);
            SessionState.SetBool(Key + "Empty", args.Contains("-endoscopyProbeEmpty"));
            SessionState.SetBool(Key + "Running", true);
            SessionState.SetBool(Key + "Passed", false);
            File.WriteAllText(path, "Preparing real Play mode\n");
            if (SessionState.GetBool(Key + "Empty", false))
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else
                EditorSceneManager.OpenScene("Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity");
            EditorApplication.EnterPlaymode();
        }

        static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key + "Running", false)) return;
            Append(state.ToString());
            if (state == PlayModeStateChange.EnteredPlayMode) _entered = EditorApplication.timeSinceStartup;
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                var passed = SessionState.GetBool(Key + "Passed", false);
                SessionState.SetBool(Key + "Running", false);
                Append(passed ? "PASS" : "FAIL");
                EditorApplication.Exit(passed ? 0 : 2);
            }
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key + "Running", false) || !EditorApplication.isPlaying || _entered <= 0 ||
                EditorApplication.timeSinceStartup - _entered < 12) return;
            _entered = 0;
            var empty = SessionState.GetBool(Key + "Empty", false);
            var objects = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var lobby = GameObject.Find("LobbyPanorama(Clone)") != null && !objects.Any(o => o && o.GetType().Name == "GaussianSplatRenderer");
            var curtain = GameObject.Find("FullScriptTransitionCurtain");
            var installer=objects.OfType<VisitorInstaller>().SingleOrDefault();
            var ready=installer?.Journey?.InputAllowed==true && installer.Journey.Visit.Panel;
            var legacy=objects.Any(o=>o && (o.GetType().Name=="VisitorProloguePresenter" || o.GetType().Name=="VisitorHandReadinessAdapter"));
            var passed = empty || (lobby && !curtain && ready && !legacy);
            Append($"empty={empty}; activePanorama={lobby}; curtainVisible={curtain != null}; briefReady={ready}; legacyOpening={legacy}; graphics={SystemInfo.graphicsDeviceType}");
            if(passed && !empty)
            {
                var camera=installer.CreateValidatedBindings().Platform.Viewer.GetComponent<Camera>();
                var output=Path.ChangeExtension(SessionState.GetString(Key+"Output",""),"png");
                Capture(camera,output);
            }
            SessionState.SetBool(Key + "Passed", passed);
            EditorApplication.ExitPlaymode();
        }

        static void Append(string text)
        {
            File.AppendAllText(SessionState.GetString(Key + "Output", ""), DateTime.UtcNow.ToString("O") + " " + text + "\n");
        }
        internal static void Capture(Camera camera,string path)
        {
            Canvas.ForceUpdateCanvases();
            var target=new RenderTexture(1440,1080,24);
            var previous=RenderTexture.active;var oldTarget=camera.targetTexture;
            var image=new Texture2D(1440,1080,TextureFormat.RGB24,false);
            try
            {
                target.Create();camera.targetTexture=target;
                camera.Render();camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,1440,1080),0,0);image.Apply();
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=oldTarget;RenderTexture.active=previous;
                target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
