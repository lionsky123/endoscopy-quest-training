using System;
using System.IO;
using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.Panorama.Backend;
using BotanicalGardenQR.Panorama.Frontend;
using BotanicalGardenQR.FrontendShell.Runtime;
using Oculus.Interaction;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Offline layout evidence only. Does not enter Play Mode or invoke any build API.
    public static class ClinicalEvidencePreview
    {
        public static void Capture()
        {
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Rendering requires graphics.");
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "-bgqrCaptureOutput");
                if (index < 0 || index + 1 >= args.Length) throw new InvalidOperationException("An output directory is required.");
                var output = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(output);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Evidence preview");
                var viewer = new GameObject("Evidence camera", typeof(Camera));
                var camera = viewer.GetComponent<Camera>();
                camera.nearClipPlane = .01f; camera.farClipPlane = 30; camera.fieldOfView = 85; camera.aspect = 4f / 3f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .05f, .065f);
                var resolver = new PublishedSceneResolver(AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>("Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset"));
                ((IPanoramaDefinitionSource)resolver).TryGet(new SceneId("giant_saguaro"), out var panorama);
                var font = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>("Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset").SharedFont;
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray;
                var light = new GameObject("Model light", typeof(Light));
                light.GetComponent<Light>().type = LightType.Directional; light.GetComponent<Light>().intensity = .8f;
                light.transform.rotation = Quaternion.Euler(35, -20, 0);
                var finished = 0;
                var session = SessionToken.CreateNew();
                var background = PanoramaModuleFactory.Create(root.transform, viewer.transform);
                background.Open(session, panorama, new PanoramaSurfaceLease(root.transform, Vector2.one, true, () => { }));
                var frontend = root.AddComponent<PanoramaFrontend>();
                frontend.Bind(SessionToken.CreateNew(), new Registry(camera), viewer.transform, root.transform, font,
                    () => { }, panorama.EnvironmentMoments, false, null, true, panorama.Source.Texture,
                    panorama.TeachingComparisons, () => finished++, panorama.InitialYawDegrees);
                frontend.SetVisible(true);
                File.WriteAllLines(Path.Combine(output, "observation-materials.txt"),
                    root.GetComponentInChildren<ClinicalObservationToken>().GetComponentsInChildren<Renderer>()
                        .SelectMany(r => r.sharedMaterials.Select(m => m.name + ": " + m.shader.name)));
                void AdvanceTime(float seconds)
                {
                    var world = typeof(PanoramaFrontend).GetField("_spatialWorld", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(frontend);
                    var controls = world.GetType().GetField("_clinicalControls", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(world);
                    controls.GetType().GetMethod("Tick").Invoke(controls, new object[] { seconds });
                }
                var lesson = JsonUtility.FromJson<ClinicalEvidenceLesson>(Resources.Load<TextAsset>("ClinicalEvidence/lesson").text);
                var token = root.GetComponentInChildren<ClinicalObservationToken>();
                void AdvanceTutorial()
                {
                    var presenter = root.GetComponentInChildren<BotanicalGardenQR.VisitorCoach.Frontend.VisitorCoachPresenter>();
                    var accepted = (bool)presenter.GetType().GetMethod("SubmitInput", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(presenter, new object[] { BotanicalGardenQR.FrontendShell.Contracts.VisitorDialogueIntentKind.Advance,
                            BotanicalGardenQR.VisitorCoach.Frontend.VisitorDialogueInputMode.HandPoke, float.MaxValue });
                    if (!accepted) throw new InvalidOperationException("Tutorial hand action was not ready.");
                }
                AdvanceTime(.4f);
                Render(camera, Path.Combine(output, "0a-operation-tutorial.png"));
                AdvanceTutorial();
                AdvanceTime(.02f); BeginHold(token); AimLens(token, viewer.transform, Vector3.forward);
                for (int frame = 0; frame < 30; frame++) AdvanceTime(.02f);
                Render(camera, Path.Combine(output, "0b-practice-progress.png"));
                for (int frame = 0; frame < 40; frame++) AdvanceTime(.02f);
                Render(camera, Path.Combine(output, "0c-ready-for-observation.png"));
                AdvanceTutorial();
                for (int topic = 0; topic < 3; topic++)
                {
                    AdvanceTime(.4f);
                    Render(camera, Path.Combine(output, $"{topic + 1}-turn-guide.png"));
                    AdvanceTime(30);
                    var forward = ClinicalEvidenceSession.PanoramaDirection(lesson.topics[topic].panoramaUv, panorama.InitialYawDegrees);
                    viewer.transform.rotation = Quaternion.LookRotation(forward);
                    AdvanceTime(.1f);
                    Render(camera, Path.Combine(output, $"{topic + 1}-object-entry.png"));
                    if (!token.IsHeld) BeginHold(token);
                    AimLens(token, viewer.transform, forward);
                    AdvanceTime(.02f);
                    if (topic == 0)
                    {
                        VerifyLensSampling(camera, token, output);
                        var material = token.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Single(m => m.name == "InspectionMagnifier_LivePanorama");
                        material.SetFloat("_Magnification", 1); Render(camera, Path.Combine(output, "1-lens-1x.png"));
                        material.SetFloat("_Magnification", ClinicalObservationToken.Magnification); Render(camera, Path.Combine(output, "1-lens-4x.png"));
                    }
                    for (int frame = 0; frame < 25; frame++) AdvanceTime(.02f);
                    Render(camera, Path.Combine(output, $"{topic + 1}-observing.png"));
                    for (int frame = 0; frame < 45; frame++) AdvanceTime(.02f);
                    Render(camera, Path.Combine(output, $"{topic + 1}-observed-ready.png"));
                    if (topic == 0)
                    {
                        token.GetComponent<Grabbable>().ProcessPointerEvent(new PointerEvent(731, PointerEventType.Unselect,
                            new Pose(token.transform.position, token.transform.rotation)));
                        token.Tick(.02f); AdvanceTime(.02f);
                        Render(camera, Path.Combine(output, "1-paused-view.png"));
                    }
                    root.GetComponentsInChildren<Button>().Single(b => b.name == "ContinueObservation").onClick.Invoke();
                    AdvanceTime(.02f);
                    Render(camera, Path.Combine(output, $"{topic + 1}-fairy-explanation.png"));
                    root.GetComponentsInChildren<Button>().Single(b => b.name == "LookBackAtScene").onClick.Invoke();
                    AdvanceTime(30);
                    Render(camera, Path.Combine(output, $"{topic + 1}-clean-observation.png"));
                    root.GetComponentsInChildren<Button>().Single(b => b.name == "ObservationHelp").onClick.Invoke();
                    AdvanceTime(.4f);
                    Render(camera, Path.Combine(output, $"{topic + 1}-operation-help.png"));
                    AdvanceTime(30);
                    root.GetComponentsInChildren<Button>().Single(b => b.name == "ContinuePictureTeaching").onClick.Invoke();
                }
                AdvanceTime(.02f);
                if (finished != 1) throw new InvalidOperationException("Observation did not complete exactly once.");
                if (token.IsHeld || token.GetComponent<Grabbable>().SelectingPointsCount != 0)
                    throw new InvalidOperationException("Finish must dismiss the held tool without asking for a manual release.");
                frontend.Unbind(); background.Close(session);
                using (var entry = new ClinicalLessonPanel(root.transform, font, new Registry(camera), () => { }))
                {
                    entry.Present(null, null, true); entry.SetVisible(true);
                    Render(camera, Path.Combine(output, "0-room-entry.png"));
                }
                Object.DestroyImmediate(root); Object.DestroyImmediate(viewer);
                VisitorGamePresentationCapture.CaptureInvitation(output, "0d-glowing-handprint.png", 0);
                Debug.Log("Clinical evidence previews saved to " + output);
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        static void BeginHold(ClinicalObservationToken token)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var grab = token.GetComponent<Grabbable>();
            if (!(bool)typeof(Grabbable).GetField("_started", flags).GetValue(grab))
                { typeof(Grabbable).GetMethod("Awake", flags).Invoke(grab, null); typeof(Grabbable).GetMethod("Start", flags).Invoke(grab, null); }
            var locker = token.GetComponent<RigidbodyKinematicLocker>();
            if (!locker)
            {
                locker = token.gameObject.AddComponent<RigidbodyKinematicLocker>();
                typeof(RigidbodyKinematicLocker).GetMethod("Awake", flags).Invoke(locker, null);
            }
            token.Tick(1);
            var pose = new Pose(token.transform.position, token.transform.rotation);
            grab.ProcessPointerEvent(new PointerEvent(731, PointerEventType.Hover, pose));
            grab.ProcessPointerEvent(new PointerEvent(731, PointerEventType.Select, pose));
        }
        static void AimLens(ClinicalObservationToken token, Transform viewer, Vector3 ray)
        {
            var centre = token.transform.InverseTransformPoint(token.LensCenter);
            var normal = token.transform.InverseTransformDirection(token.LensNormal);
            if (Vector3.Dot(normal, Vector3.forward) < 0) normal = -normal;
            var rotation = Quaternion.LookRotation(ray) * Quaternion.FromToRotation(normal, Vector3.forward);
            var position = viewer.position + ray * .32f - rotation * centre;
            token.GetComponent<Grabbable>().ProcessPointerEvent(new PointerEvent(731, PointerEventType.Move, new Pose(position, rotation)));
        }
        static void VerifyLensSampling(Camera camera, ClinicalObservationToken token, string output)
        {
            var material = token.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Single(m => m.name == "InspectionMagnifier_LivePanorama");
            var original = material.GetTexture("_MainTex");
            var ramp = new Texture2D(512, 256, TextureFormat.RGBAFloat, false, true) { wrapMode = TextureWrapMode.Repeat };
            var colors = new Color[512 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 512; x++) colors[y * 512 + x] = new Color(x / 511f, y / 255f, .2f, 1);
            ramp.SetPixels(colors); ramp.Apply();
            var start = camera.transform.position;
            var heldPose = new Pose(token.transform.position, token.transform.rotation);
            var report = new System.Collections.Generic.List<string>();
            try
            {
                material.SetTexture("_MainTex", ramp);
                float Span(float magnification)
                {
                    material.SetFloat("_Magnification", magnification);
                    var pixels = ReadOptics(camera);
                    try
                    {
                        var centre = camera.WorldToViewportPoint(token.LensCenter);
                        int x = Mathf.RoundToInt(centre.x * pixels.width), y = Mathf.RoundToInt(centre.y * pixels.height);
                        return Mathf.Abs(pixels.GetPixel(x + 20, y).r - pixels.GetPixel(x - 20, y).r);
                    }
                    finally { Object.DestroyImmediate(pixels); }
                }
                var ratio = Span(1) / Span(ClinicalObservationToken.Magnification);
                report.Add("Measured optical magnification: " + ratio);
                if (float.IsNaN(ratio) || ratio < 3.6f || ratio > 4.4f) throw new InvalidOperationException("Actual glass mesh does not render 4x magnification: " + ratio);
                // Two physical eye offsets and a lean: compare GPU samples to the
                // authored panorama's fixed sphere, not merely a shader uniform.
                foreach (var probe in new[] { new Vector2(-.032f, 0), new Vector2(.032f, 0), new Vector2(.12f, 0), new Vector2(0, 8) })
                {
                    camera.transform.position = start + camera.transform.right * probe.x;
                    // Deliberately do not tick the lesson after this late hand move:
                    // the shader must use the mesh's current transform on its own.
                    if (probe.y != 0) AimLens(token, camera.transform, Quaternion.Euler(0, probe.y, 0) * camera.transform.forward);
                    var pixels = ReadOptics(camera);
                    try
                    {
                        var screen = camera.WorldToViewportPoint(token.LensCenter);
                        var actual = pixels.GetPixel(Mathf.RoundToInt(screen.x * pixels.width), Mathf.RoundToInt(screen.y * pixels.height));
                        var origin = (Vector3)material.GetVector("_PanoramaOrigin");
                        var ray = (token.LensCenter - camera.transform.position).normalized;
                        var eye = camera.transform.position - origin;
                        var b = Vector3.Dot(eye, ray); var radius = material.GetFloat("_PanoramaRadius");
                        var hit = eye + ray * (-b + Mathf.Sqrt(b * b - eye.sqrMagnitude + radius * radius));
                        var direction = Quaternion.Euler(0, -material.GetFloat("_YawRadians") * Mathf.Rad2Deg, 0) * hit.normalized;
                        var expected = new Vector2(Mathf.Atan2(direction.x, direction.z) / (2 * Mathf.PI) + .5f, Mathf.Asin(direction.y) / Mathf.PI + .5f);
                        var error = Vector2.Distance(new Vector2(actual.r, actual.g), expected);
                        report.Add($"Eye/lean offset {probe.x}, hand rotation {probe.y}: sampled ({actual.r}, {actual.g}); expected {expected}; UV error {error}");
                        if (error > .015f) throw new InvalidOperationException("Lens panorama alignment failed: " + error);
                    }
                    finally { Object.DestroyImmediate(pixels); }
                }
                if (ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("Magnifier shader compilation failed.");
                File.WriteAllLines(Path.Combine(output, "lens-optics-check.txt"), report);
            }
            finally
            {
                camera.transform.position = start;
                token.GetComponent<Grabbable>().ProcessPointerEvent(new PointerEvent(731, PointerEventType.Move, heldPose));
                material.SetTexture("_MainTex", original); material.SetFloat("_Magnification", ClinicalObservationToken.Magnification);
                Object.DestroyImmediate(ramp);
            }
        }
        static Texture2D ReadOptics(Camera camera)
        {
            var target = new RenderTexture(640, 480, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.Create(); var previous = RenderTexture.active; var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                var image = new Texture2D(640, 480, TextureFormat.RGBAFloat, false, true);
                image.ReadPixels(new Rect(0, 0, 640, 480), 0, 0); image.Apply(); return image;
            }
            finally { camera.targetTexture = previousTarget; RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); }
        }
        static void Render(Camera camera, string path)
        {
            Canvas.ForceUpdateCanvases();
            var target = new RenderTexture(1600, 1200, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create(); var previous = RenderTexture.active;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            camera.Render(); image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = previous;
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
