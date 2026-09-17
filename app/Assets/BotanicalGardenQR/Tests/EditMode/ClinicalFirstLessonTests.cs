using System;
using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.ImageRing.Contracts;
using BotanicalGardenQR.KnowledgeMiniGame.Contracts;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.Panorama.Backend;
using BotanicalGardenQR.Panorama.Frontend;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalFirstLessonTests
    {
        static readonly SceneId First = new SceneId("giant_saguaro");
        static PublishedSceneResolver Resolver() => new PublishedSceneResolver(AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>(
            "Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset"));
        static TMP_FontAsset Font() => AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(
            "Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset").SharedFont;

        [Test] public void PanoramaProjectionCoversFullLatitudeWithoutResizingTheRoom()
        {
            var root = new GameObject("Projection runtime");
            var viewer = new GameObject("Viewer", typeof(Camera));
            var camera = viewer.GetComponent<Camera>();
            camera.fieldOfView = 83;
            var controller = PanoramaModuleFactory.Create(root.transform, viewer.transform);
            ((IPanoramaDefinitionSource)Resolver()).TryGet(First, out var definition);
            try
            {
                controller.Open(SessionToken.CreateNew(), definition,
                    new PanoramaSurfaceLease(root.transform, Vector2.one, true, () => { }));
                var mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                var vertices = mesh.vertices;
                var uv = mesh.uv;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var latitude = Mathf.Asin(vertices[i].normalized.y) * Mathf.Rad2Deg;
                    Assert.That(latitude, Is.EqualTo((uv[i].y - .5f) * 180).Within(.01f),
                        "Full 2:1 panorama must map the full 180 degrees without trimming or squeezing poles.");
                }
                Assert.That(camera.fieldOfView, Is.EqualTo(83), "Panorama must not override the viewing camera's lens.");
                var material = root.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Assert.That(material.mainTexture, Is.SameAs(definition.Source.Texture));
                Assert.That(material.mainTextureScale, Is.EqualTo(Vector2.one));
                Assert.That(material.mainTextureOffset, Is.EqualTo(Vector2.zero));
                Assert.That(material.shader, Is.SameAs(Resources.Load<Shader>("PanoramaEquirectangular")));
            }
            finally
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>()) UnityEngine.Object.DestroyImmediate(filter.sharedMesh);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>()) UnityEngine.Object.DestroyImmediate(renderer.sharedMaterial);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test] public void ClinicalPanelRemainsVisibleUnderTheAuthoredSharedCanvasGroup()
        {
            var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab"));
            var shell = instance.GetComponentInChildren<GlobalFrontendShell>(true);
            var flow = new ShellFlow();
            var resolver = Resolver();
            resolver.TryGetPresentation(First, out var presentation);
            resolver.TryGet(First, out var descriptor);
            var state = new ExperienceFlowState(SessionToken.CreateNew(), 1, First, descriptor.Title,
                descriptor.Subtitle, descriptor.Summary, FlowPage.Main, descriptor.AvailablePages);
            try
            {
                shell.enabled = false; // Synchronous edit-mode transition; no XR or play-mode startup.
                shell.Configure(flow, presentation, string.Empty);
                shell.PresentClinicalLesson(state, resolver, new Registry());
                var panel = shell.FrontendRoot.parent.Find("ClinicalLessonPanel");
                for (var parent = panel.parent; parent != null; parent = parent.parent)
                {
                    parent.gameObject.SetActive(true);
                    if (parent == instance.transform) break;
                }
                flow.Publish(state);
                flow.Publish(state);
                Assert.That(shell.FrontendRoot.gameObject.activeSelf, Is.False, "Legacy main panel must be hidden.");
                Assert.That(panel.gameObject.activeInHierarchy, Is.True);
                foreach (var group in panel.GetComponentsInParent<CanvasGroup>(true))
                    Assert.That(group.alpha, Is.GreaterThan(0), "A shared CanvasGroup must not make the new sibling invisible.");
                shell.SetApplicationSurfaceSuppressed(true);
                Assert.That(panel.gameObject.activeSelf, Is.False);
                shell.SetApplicationSurfaceSuppressed(false);
                Assert.That(panel.gameObject.activeInHierarchy, Is.True);
                int completions = 0;
                shell.ClinicalCompletionRequested += () => completions++;
                Assert.That(shell.CompleteClinicalLesson(SessionToken.CreateNew()).Succeeded, Is.False);
                Assert.That(completions, Is.Zero);
                Assert.That(shell.CompleteClinicalLesson(state.Session).Succeeded, Is.True);
                Assert.That(flow.CloseCalls, Is.EqualTo(1), "The completion seam closes content before requesting the existing quiz.");
                Assert.That(completions, Is.EqualTo(1));
                var second = new SceneId("baobab");
                resolver.TryGet(second, out var other);
                flow.Publish(new ExperienceFlowState(SessionToken.CreateNew(), 1, second, other.Title,
                    other.Subtitle, other.Summary, FlowPage.Main, other.AvailablePages));
                Assert.That(panel.gameObject.activeSelf, Is.False);
                Assert.That(shell.FrontendRoot.gameObject.activeSelf, Is.True, "Other lessons retain their original presentation.");
            }
            finally { shell.Dispose(); UnityEngine.Object.DestroyImmediate(instance); }
        }

        sealed class ShellFlow : IExperienceFlow
        {
            IFlowStateSink _sink;
            public int CloseCalls;
            public FlowPrepareResult Prepare(SessionToken session, SceneId scene) => throw new NotSupportedException();
            public FlowResult EnterFeature(SessionToken session, FeaturePageId feature) => FlowResult.Success;
            public FlowResult BackToMain(SessionToken session) => FlowResult.Success;
            public FlowResult Close(SessionToken session) { CloseCalls++; return FlowResult.Success; }
            public IDisposable Observe(IFlowStateSink sink) { _sink = sink; return new EmptyLease(); }
            public void Publish(ExperienceFlowState state) => _sink.OnStateChanged(state);
            sealed class EmptyLease : IDisposable { public void Dispose() { } }
        }

        [Test] public void FirstLessonUsesNewFullResolutionPanoramaAndThreeChecks()
        {
            var resolver = Resolver();
            Assert.That(resolver.IsPanoramaReadyForTeaching(First), Is.True);
            Assert.That(((IPanoramaDefinitionSource)resolver).TryGet(First, out var panorama), Is.True);
            Assert.That(panorama.ClinicalLearning, Is.True);
            Assert.That(panorama.EnvironmentMoments, Is.Empty);
            Assert.That(panorama.TeachingComparisons.Count, Is.EqualTo(3));
            foreach (var image in panorama.TeachingComparisons)
                Assert.That(AssetDatabase.GetAssetPath(image), Does.EndWith("-comparison.png"));
            Assert.That(AssetDatabase.GetAssetPath(panorama.Source.Texture), Does.EndWith("giant_saguaro/Endoscopy/cleaning-room-360-v2.png"));
            Assert.That(panorama.Source.Texture.width, Is.EqualTo(7680));
            Assert.That(panorama.Source.Texture.height, Is.EqualTo(3840));
            Assert.That(((IKnowledgeMiniGameDefinitionSource)resolver).TryGet(First, out var quiz), Is.True);
            Assert.That(quiz.QuestionCount, Is.EqualTo(3));
            Assert.That(((IPanoramaDefinitionSource)resolver).TryGet(new SceneId("baobab"), out var second), Is.True);
            Assert.That(second.ClinicalLearning, Is.False, "Other lessons retain their previous UI.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(panorama.Source.Texture));
            Assert.That(importer.GetPlatformTextureSettings("Android").maxTextureSize, Is.EqualTo(8192));
        }

        [Test] public void FirstLessonEntryHasOneActionAndRejectsPointerSubmission()
        {
            var root = new GameObject("Panel test");
            var events = new GameObject("Events", typeof(EventSystem));
            int starts = 0;
            var resolver = Resolver();
            resolver.TryGetLearningImages(First, out var images);
            resolver.TryGet(First, out var descriptor);
            try
            {
                using var panel = new ClinicalLessonPanel(root.transform, Font(), new Registry(), () => starts++);
                panel.Present(new ExperienceFlowState(SessionToken.CreateNew(), 1, First, descriptor.Title,
                    descriptor.Subtitle, descriptor.Summary, FlowPage.Main, descriptor.AvailablePages), images, true);
                panel.SetVisible(true);
                var buttons = root.GetComponentsInChildren<Button>(true);
                Assert.That(buttons.Length, Is.EqualTo(1));
                var start = (NearOnlyButton)buttons.Single();
                Assert.That(start.name, Is.EqualTo("EnterPanorama"));
                start.OnPointerClick(new PointerEventData(events.GetComponent<EventSystem>()));
                start.OnSubmit(new BaseEventData(events.GetComponent<EventSystem>()));
                Assert.That(starts, Is.Zero);
                start.onClick.Invoke();
                Assert.That(starts, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<RawImage>(true), Is.Empty, "Entry is not a media-selection menu.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(events); }
        }

        [Test] public void FirstPanoramaLoadsOneCardInOrderAndCompletesWithoutAMenu()
        {
            var root = new GameObject("Panorama test");
            var viewer = new GameObject("Viewer");
            var events = new GameObject("Events", typeof(EventSystem));
            var frontend = root.AddComponent<PanoramaFrontend>();
            ((IPanoramaDefinitionSource)Resolver()).TryGet(First, out var panorama);
            int completed = 0, exits = 0;
            try
            {
                frontend.Bind(SessionToken.CreateNew(), new Registry(), viewer.transform, root.transform, Font(),
                    () => exits++, panorama.EnvironmentMoments, false, null, true, panorama.Source.Texture,
                    panorama.TeachingComparisons, () => completed++);
                frontend.SetVisible(true);
                var next = (NearOnlyButton)root.GetComponentsInChildren<Button>(true).Single();
                Assert.That(next.name, Is.EqualTo("ContinueSequence"));
                var image = root.GetComponentInChildren<RawImage>(true);
                Assert.That(image.gameObject.activeInHierarchy, Is.False, "Observe the room before showing teaching media.");
                next.OnPointerClick(new PointerEventData(events.GetComponent<EventSystem>()));
                next.OnSubmit(new BaseEventData(events.GetComponent<EventSystem>()));
                Assert.That(image.gameObject.activeInHierarchy, Is.False);
                for (int step = 1; step <= 6; step++)
                {
                    next.onClick.Invoke();
                    Assert.That(image.gameObject.activeInHierarchy, Is.True);
                    Assert.That(root.GetComponentsInChildren<RawImage>(), Has.Length.EqualTo(1));
                    Assert.That(root.GetComponentsInChildren<Button>(), Has.Length.EqualTo(1));
                    bool comparison = step % 2 == 0;
                    Assert.That(image.texture, Is.SameAs(comparison ? panorama.TeachingComparisons[(step - 1) / 2] : panorama.Source.Texture));
                    Assert.That(image.uvRect == new Rect(0, 0, 1, 1), Is.EqualTo(comparison));
                    Assert.That(completed, Is.Zero, "Reading an image does not auto-complete the lesson.");
                }
                Assert.That(next.GetComponentInChildren<TMP_Text>().text, Does.Contain("开始答题"));
                next.onClick.Invoke();
                next.onClick.Invoke();
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(exits, Is.Zero, "Last card completes directly, rather than returning to a selection menu.");
                Assert.That(viewer.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(viewer.transform.rotation, Is.EqualTo(Quaternion.identity));
                frontend.SetVisible(false);
                frontend.SetVisible(true);
                Assert.That(image.gameObject.activeInHierarchy, Is.False);
                next.onClick.Invoke();
                Assert.That(image.texture, Is.SameAs(panorama.Source.Texture), "Re-entering starts at the door detail.");
                frontend.Unbind();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(viewer); UnityEngine.Object.DestroyImmediate(events); }
        }
        sealed class Registry : IFrontendGazeSurfaceRegistry
        {
            public IDisposable SuspendPanelInput() => new Lease();
            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform root, int priority, string label)
            {
                Assert.That(root.GetComponent<Canvas>(), Is.Not.Null);
                Assert.That(root.GetComponent<GraphicRaycaster>(), Is.Not.Null);
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
