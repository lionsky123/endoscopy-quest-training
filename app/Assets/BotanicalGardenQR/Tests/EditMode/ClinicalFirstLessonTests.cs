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
                shell.ClinicalCompletionRequested += _ => completions++;
                Assert.That(shell.CompleteClinicalLesson(SessionToken.CreateNew()).Succeeded, Is.False);
                Assert.That(completions, Is.Zero);
                flow.RejectClose = true;
                Assert.That(shell.CompleteClinicalLesson(state.Session).Succeeded, Is.False);
                Assert.That(completions, Is.Zero, "A failed close must not publish completion.");
                flow.RejectClose = false;
                Assert.That(shell.CompleteClinicalLesson(state.Session).Succeeded, Is.True);
                Assert.That(flow.CloseCalls, Is.EqualTo(2), "A successful close precedes completion; the failed attempt can be retried.");
                Assert.That(completions, Is.EqualTo(1));
                Assert.That(shell.CompleteClinicalLesson(state.Session).Succeeded, Is.False);
                Assert.That(completions, Is.EqualTo(1), "The same session may complete only once.");
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
            public bool RejectClose;
            public FlowPrepareResult Prepare(SessionToken session, SceneId scene) => throw new NotSupportedException();
            public FlowResult EnterFeature(SessionToken session, FeaturePageId feature) => FlowResult.Success;
            public FlowResult BackToMain(SessionToken session) => FlowResult.Success;
            public FlowResult Close(SessionToken session)
            {
                CloseCalls++;
                if (RejectClose) return FlowResult.Reject(FlowFailure.StaleSession);
                Publish(new ExperienceFlowState(default, 2, default, "", "", "", FlowPage.Closed, Array.Empty<FeaturePageId>()));
                return FlowResult.Success;
            }
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

        // Current no-quiz interaction coverage lives in ClinicalObservationInteractionTests.

        [Test]
        public void EvidenceSessionDoesNotAcceptPartialEvidenceOrHintsAsCompletion()
        {
            var definition = JsonUtility.FromJson<ClinicalEvidenceLesson>(Resources.Load<TextAsset>("ClinicalEvidence/lesson").text);
            var session = new ClinicalEvidenceSession(definition);
            Assert.That(session.Continue(), Is.False);
            for (int topic = 0; topic < 3; topic++)
            {
                session.Locate(); session.Ask();
                for (int n = 0; n < 10; n++) { session.Hint(); session.Submit(); }
                Assert.That(session.CompletedCount, Is.EqualTo(topic));
                if (topic > 0)
                {
                    int firstRequired = Enumerable.Range(0, session.Topic.regions.Length).First(i => (session.Topic.requiredMask & (1 << i)) != 0);
                    session.SelectRegion(firstRequired); session.SelectVerdict(session.Topic.conforms);
                    Assert.That(session.Submit(), Is.False, "Correct verdict with only part of the evidence is insufficient.");
                    Assert.That(session.CompletedCount, Is.EqualTo(topic));
                }
                for (int i = 0; i < session.Topic.regions.Length; i++)
                    if ((session.Topic.requiredMask & (1 << i)) != 0 && (session.SelectedMask & (1 << i)) == 0) session.SelectRegion(i);
                session.SelectVerdict(session.Topic.conforms);
                Assert.That(session.Submit(), Is.True);
                Assert.That(session.Submit(), Is.False);
                Assert.That(session.Continue(), Is.EqualTo(topic == 2));
            }
            Assert.That(session.CompletedCount, Is.EqualTo(3));
            Assert.That(session.Continue(), Is.False);
            session.Reset(); Assert.That(session.CompletedCount, Is.Zero);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void MixedSkippedAndCorrectTopicsFinishWithoutAwardingSkippedAnswers(int skipMask)
        {
            var session = new ClinicalEvidenceSession(JsonUtility.FromJson<ClinicalEvidenceLesson>(Resources.Load<TextAsset>("ClinicalEvidence/lesson").text));
            int correct = 0, skipped = 0;
            Assert.That(session.Skip(), Is.False, "Observation and method stages are not questions.");
            for (int topic = 0; topic < 3; topic++)
            {
                session.Locate(); session.Ask();
                if ((skipMask & (1 << topic)) != 0)
                {
                    session.SelectRegion(0); session.SelectVerdict(!session.Topic.conforms); session.Submit();
                    Assert.That(session.Skip(), Is.True, "Skipping remains available after a wrong attempt.");
                    Assert.That(session.Skip(), Is.False, "Each topic can only be skipped once.");
                    skipped++;
                }
                else
                {
                    for (int i = 0; i < session.Topic.regions.Length; i++)
                        if ((session.Topic.requiredMask & (1 << i)) != 0) session.SelectRegion(i);
                    session.SelectVerdict(session.Topic.conforms); Assert.That(session.Submit(), Is.True); correct++;
                    Assert.That(session.Skip(), Is.False, "A correct topic cannot subsequently become skipped.");
                }
                Assert.That(session.Continue(), Is.EqualTo(topic == 2));
                Assert.That(session.CompletedCount, Is.EqualTo(correct));
                Assert.That(session.SkippedCount, Is.EqualTo(skipped));
            }
            Assert.That(session.Phase, Is.EqualTo(ClinicalEvidencePhase.Finished));
            Assert.That(session.SkippedTopicMask, Is.EqualTo(skipMask));
            session.Reset();
            Assert.That(session.ResolvedCount, Is.Zero); Assert.That(session.SkippedTopicMask, Is.Zero);
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
