using System;
using System.IO;
using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.ImageRing.Contracts;
using BotanicalGardenQR.Panorama.Frontend;
using BotanicalGardenQR.Panorama.Backend;
using BotanicalGardenQR.Panorama.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EndoscopyTheme.Editor
{
    public static class ClinicalFirstLessonPreview
    {
        public static void UpdateAndCapture()
        {
            ClinicalContentImport.UpdateFirstLesson();
            Capture();
        }
        public static void Capture()
        {
            var output = Environment.GetEnvironmentVariable("ENDOSCOPY_FIRST_LESSON_PREVIEW");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Preview output directory is required.");
            Directory.CreateDirectory(output);
            var root = new GameObject("ClinicalPreview");
            var cameraObject = new GameObject("ClinicalPreviewCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -2);
            camera.orthographic = true;
            camera.orthographicSize = .235f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .16f, .2f);
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 10;
            var registry = new PreviewRegistry(camera);
            try
            {
                root.transform.localScale = Vector3.one * .00105f;
                var defaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>("Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset");
                var resolver = new PublishedSceneResolver(AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>("Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset"));
                var scene = new SceneId("giant_saguaro");
                resolver.TryGet(scene, out var descriptor);
                resolver.TryGetLearningImages(scene, out var images);
                using (var panel = new ClinicalLessonPanel(root.transform, defaults.SharedFont, registry, () => { }))
                {
                    panel.Present(new ExperienceFlowState(SessionToken.CreateNew(), 1, scene, descriptor.Title, descriptor.Subtitle,
                        descriptor.Summary, FlowPage.Main, descriptor.AvailablePages), images, true);
                    panel.SetVisible(true);
                    Save(camera, root, Path.Combine(output, "first-lesson-panel.png"), 1920, 1200);
                }
                root.transform.localScale = Vector3.one;
                var viewer = new GameObject("PreviewViewer");
                viewer.transform.SetParent(root.transform, false);
                var panorama = root.AddComponent<PanoramaFrontend>();
                ((BotanicalGardenQR.Panorama.Contracts.IPanoramaDefinitionSource)resolver).TryGet(scene, out var panoramaDefinition);
                panorama.Bind(SessionToken.CreateNew(), registry, viewer.transform, root.transform, defaults.SharedFont,
                    () => { }, Array.Empty<BotanicalGardenQR.Panorama.Contracts.PanoramaEnvironmentMomentDefinition>(), false, null, true,
                    panoramaDefinition.Source.Texture, panoramaDefinition.TeachingComparisons);
                panorama.SetVisible(true);
                camera.transform.position = new Vector3(0, -.23f, -2);
                camera.orthographicSize = .135f;
                Save(camera, root, Path.Combine(output, "first-lesson-panorama-controls.png"), 1800, 850);
                camera.transform.position = new Vector3(0, 0, -2);
                camera.orthographicSize = .33f;
                for (int i = 1; i <= 3; i++)
                {
                    
                    root.GetComponentsInChildren<Button>(true).Single(b => b.name == "ContinueSequence").onClick.Invoke();
                    Save(camera, root, Path.Combine(output, "first-lesson-detail-" + i + ".png"), 1600, 1440);
                    root.GetComponentsInChildren<Button>(true).Single(b => b.name == "ContinueSequence").onClick.Invoke();
                    Save(camera, root, Path.Combine(output, "first-lesson-comparison-" + i + ".png"), 1600, 1440);
                }
                panorama.SetVisible(false);
                panorama.SetVisible(true);
                var room = new GameObject("PreviewRoom");
                room.transform.SetParent(root.transform, false);
                var controller = PanoramaModuleFactory.Create(room.transform, viewer.transform);
                controller.Open(SessionToken.CreateNew(), panoramaDefinition,
                    new PanoramaSurfaceLease(room.transform, Vector2.one, true, () => { }));
                try
                {
                    camera.orthographic = false;
                    camera.fieldOfView = 75;
                    camera.transform.position = Vector3.zero;
                    Save(camera, root, Path.Combine(output, "first-lesson-immersive.png"), 1600, 1200);
                    root.GetComponentsInChildren<Button>(true).Single(b => b.name == "ContinueSequence").onClick.Invoke();
                    Save(camera, root, Path.Combine(output, "first-lesson-guided-card.png"), 1600, 1200);
                }
                finally
                {
                    foreach (var filter in room.GetComponentsInChildren<MeshFilter>()) UnityEngine.Object.DestroyImmediate(filter.sharedMesh);
                    foreach (var renderer in room.GetComponentsInChildren<MeshRenderer>()) UnityEngine.Object.DestroyImmediate(renderer.sharedMaterial);
                    UnityEngine.Object.DestroyImmediate(room);
                }
                panorama.Unbind();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(cameraObject); }
            ClinicalPanoramaProjectionCheck.Capture();
        }
        static void Save(Camera camera, GameObject root, string path, int width, int height)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TMPro.TMP_Text>(true)) text.ForceMeshUpdate(true);
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }
        sealed class PreviewRegistry : IFrontendGazeSurfaceRegistry
        {
            readonly Camera _camera;
            public PreviewRegistry(Camera camera) { _camera = camera; }
            public IDisposable SuspendPanelInput() => new Lease();
            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform root, int priority, string label)
            {
                var canvas = root.GetComponent<Canvas>();
                if (canvas == null || canvas.GetComponent<GraphicRaycaster>() == null) throw new InvalidOperationException("Touch surface lacks canvas/raycaster.");
                canvas.worldCamera = _camera;
                return new Lease();
            }
            sealed class Lease : IFrontendGazeSurfaceRegistration
            {
                public bool IsFocused => false;
                public void Invalidate() { }
                public void Dispose() { }
            }
        }
    }
}
