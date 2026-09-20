using System;
using System.IO;
using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Editor-only visual evidence. No Play Mode, player build, deployment or target switch.
    public static class ClinicalCoursePreview
    {
        static GameObject _root, _viewer;
        static ClinicalCoursePanel _panel;
        static Camera _camera;
        static string _output;
        static double _deadline;
        public static void Capture()
        {
            try
            {
                var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, "-bgqrCaptureOutput");
                if (index < 0) throw new InvalidOperationException("Missing capture output.");
                _output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(_output);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _root = new GameObject("Later course preview"); _viewer = new GameObject("Camera", typeof(Camera)); _viewer.tag = "MainCamera";
                _camera = _viewer.GetComponent<Camera>(); _camera.nearClipPlane = .01f; _camera.fieldOfView = 70;
                _camera.clearFlags = CameraClearFlags.SolidColor; _camera.backgroundColor = new Color(.025f, .04f, .06f);
                var light = new GameObject("Teaching light", typeof(Light)); light.transform.rotation = Quaternion.Euler(35, -30, 0);
                light.GetComponent<Light>().type = LightType.Directional; light.GetComponent<Light>().intensity = 1.5f;
                RenderSettings.ambientLight = Color.gray;
                var font = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>("Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset").SharedFont;
                _panel = new ClinicalCoursePanel(_root.transform, font, new Registry(_camera), _ => true);
                foreach (var lesson in ClinicalCourseCatalog.Load().lessons)
                {
                    _panel.Present(SessionToken.CreateNew(), lesson.sceneId); _panel.SetVisible(true);
                    _camera.transform.LookAt(_root.transform.Find("ClinicalCoursePanel").position);
                    Render($"P0{lesson.station}-intro"); Find("ContinueCourse").onClick.Invoke();
                    while (_panel.Session.Phase != ClinicalCoursePhase.Finished)
                    {
                        _panel.Tick(9); var task = _panel.Session.Step;
                        Render($"P0{lesson.station}-{_panel.Session.Index + 1}-{task.mode}");
                        if (task.mode == "evidence") for (int c = 0; c < 3; c++) { Find("EvidenceTab" + c).onClick.Invoke(); Render($"P0{lesson.station}-{_panel.Session.Index + 1}-card{c + 1}"); }
                        if (task.mode == "sequence") foreach (int card in task.sequenceOrder.Reverse()) Find("ProcessCard" + card).onClick.Invoke();
                        else Find("CourseChoice" + ((task.correct + 1) % 3)).onClick.Invoke();
                        Find("SubmitCourse").onClick.Invoke();
                        Render($"P0{lesson.station}-{_panel.Session.Index + 1}-retry");
                        if (task.mode == "sequence")
                        {
                            foreach (int card in _panel.Session.Sequence.ToArray()) Find("ProcessCard" + card).onClick.Invoke();
                            foreach (int card in task.sequenceOrder) Find("ProcessCard" + card).onClick.Invoke();
                        }
                        else Find("CourseChoice" + task.correct).onClick.Invoke();
                        Find("SubmitCourse").onClick.Invoke();
                        Find("ContinueCourse").onClick.Invoke();
                    }
                    Render($"P0{lesson.station}-finished");
                }
                // Verify the actual local clip decoder, not an image substituted for the video.
                _panel.Present(SessionToken.CreateNew(), "bottle_tree"); _panel.SetVisible(true);
                Find("ContinueCourse").onClick.Invoke(); Find("MediaAction").onClick.Invoke();
                _deadline = EditorApplication.timeSinceStartup + 25;
                EditorApplication.update += WaitForVideo;
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        static Button Find(string name) => _root.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
        static void WaitForVideo()
        {
            try
            {
                var player = _root.GetComponentInChildren<VideoPlayer>();
                if (player && player.isPlaying && player.frame >= 8)
                {
                    player.Pause(); Render("P03-decoded-video");
                    File.WriteAllText(Path.Combine(_output, "video-validation.txt"), $"Decoded local clip; frame={player.frame}; size={player.width}x{player.height}; audio={player.audioOutputMode}; target=Android; editor-only.");
                    Finish(0);
                }
                else if (EditorApplication.timeSinceStartup > _deadline) throw new TimeoutException("Local video did not decode in the editor within 25 seconds.");
            }
            catch (Exception error) { Debug.LogException(error); Finish(1); }
        }
        static void Finish(int code)
        {
            EditorApplication.update -= WaitForVideo; _panel?.Dispose(); _panel = null;
            if (_root) Object.DestroyImmediate(_root); if (_viewer) Object.DestroyImmediate(_viewer);
            EditorApplication.Exit(code);
        }
        static void Render(string name)
        {
            Canvas.ForceUpdateCanvases();
            var target = new RenderTexture(1600, 1200, 24) { antiAliasing = 4 }; target.Create();
            var previous = RenderTexture.active; _camera.targetTexture = target; _camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(1600, 1200, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1600, 1200), 0, 0); image.Apply();
            File.WriteAllBytes(Path.Combine(_output, name + ".png"), image.EncodeToPNG());
            _camera.targetTexture = null; RenderTexture.active = previous;
            Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
        }
        sealed class Registry : IFrontendGazeSurfaceRegistry
        {
            readonly Camera _camera;
            public Registry(Camera camera) { _camera = camera; }
            public IDisposable SuspendPanelInput() => new Lease();
            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform root, int priority, string label)
            { root.GetComponent<Canvas>().worldCamera = _camera; return new Lease(); }
            sealed class Lease : IFrontendGazeSurfaceRegistration
            { public bool IsFocused => false; public void Invalidate() { } public void Dispose() { } }
        }
    }
}
