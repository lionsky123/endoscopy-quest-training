using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Collection.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.JourneyNavigation.Contracts;
using BotanicalGardenQR.JourneyNavigation.Runtime;
using NUnit.Framework;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.MapNavigation.Runtime;
using BotanicalGardenQR.KnowledgeMiniGame.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.KnowledgeMiniGame.Frontend;
using BotanicalGardenQR.KnowledgeMiniGame.Backend;

namespace BotanicalGardenQR.Tests.EditMode.JourneyNavigation
{
    public sealed class JourneyCloseDecisionBindingTests
    {
        static readonly SceneId GiantSaguaro = new SceneId("giant_saguaro");
        static readonly SceneId Baobab = new SceneId("baobab");

        [TestCase(false)]
        [TestCase(true)]
        public void ClinicalSixStopCourseAdvancesWithoutAtlasOrCollection(bool skip)
        {
            var entries = AssetDatabase.LoadAssetAtPath<ContentEntryCatalog>("Assets/BotanicalGardenQR/Content/Authoring/ContentEntryCatalog.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>("Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            var map = VisitorMapConfiguration.Resolve(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json"));
            var session = JourneySessionId.CreateNew();
            var source = new TestContentLifecycleSource();
            var intents = new TestJourneyIntentSource();
            using var collection = CollectionProgressModuleFactory.Create();
            Assert.That(catalog.TryBuild(out var definition, out var error), Is.True, error);
            collection.BeginSession(session, definition);
            using var journey = JourneyNavigationModuleFactory.Create();
            using var ui = new ProductionCloseUi();
            using var binding = new JourneyCloseDecisionBinding(source, session, journey, intents,
                ui.Startup, collection, catalog, collectionsEnabled: false);
            ui.BindQuiz(source, binding.AcceptObservationCompleted);
            using var preparation = new VisitorToolPreparation();
            preparation.CompleteWithoutTools();
            Assert.That(preparation.HasExited, Is.True);
            Assert.That(preparation.HasSummoned, Is.False);
            Assert.That(preparation.IsPracticing, Is.False);
            using var navigation = new MapNavigationController(map, new MapMotion());
            using var guidance = new VisitorGuidanceCoordinator(navigation, () => { });
            for (int frame = 0; frame < 12; frame++) navigation.TryInitialize(default, 0, 0, .02f);
            guidance.Begin();
            guidance.Refresh(0, default, navigation.HasFrame, false);
            Assert.That(map.points.Length, Is.EqualTo(6));
            for (int station = 0; station < map.points.Length; station++)
            {
                Assert.That(guidance.ShowRoute, Is.True, "Walking guidance must be visible without summoning a map.");
                Assert.That(guidance.ShowTarget, Is.True);
                var route = new MapRouteGeometry(navigation.CurrentWorldPath);
                for (int frame = 0; frame < 2000 && navigation.State.Phase != MapNavigationPhase.Arrived; frame++)
                    navigation.Tick(route.Sample(navigation.State.Progress), true, false, .05f);
                Assert.That(navigation.State.Phase, Is.EqualTo(MapNavigationPhase.Arrived));
                for (int frame = 0; frame < 4; frame++) guidance.TrackVisitorArrival(route.Sample(route.Length), true, .1f);
                Assert.That(entries.TryResolveMapPoint(map.points[station].id, out var entry), Is.True);
                var content = SessionToken.CreateNew();
                ui.SetClosed(false);
                source.Open(new ContentOpenedFact(content, session, entry.TargetSceneId, RecognitionSourceKinds.Fieldbook, "entry"));
                binding.AcceptClinicalLessonCompleted(content);
                Assert.That(journey.CurrentState.CompletedCount, Is.EqualTo(station), "An open or unrelated lesson cannot complete through the clinical seam.");
                guidance.TargetContentOpened();
                source.Close(new ContentClosedFact(content, session, entry.TargetSceneId, true));
                ui.SetClosed(true);
                Assert.That(journey.CurrentState.CompletedCount, Is.EqualTo(station), "Closing the page alone must not finish learning.");
                if (station == 0)
                {
                    binding.AcceptClinicalLessonCompleted(SessionToken.CreateNew());
                    intents.RequestSkipQuizAndCollect();
                    Assert.That(journey.CurrentState.CompletedCount, Is.Zero, "Stale events and skip cannot bypass picture tasks.");
                    binding.AcceptClinicalLessonCompleted(content);
                    binding.AcceptClinicalLessonCompleted(content);
                    Assert.That(ui.Quiz.IsVisible, Is.False, "Picture completion must not open the old three-question quiz.");
                }
                else
                {
                    var course = new ClinicalCourseSession(ClinicalCourseCatalog.Load().Find(entry.TargetSceneId.Value));
                    course.Continue();
                    while (course.Phase != ClinicalCoursePhase.Finished)
                    {
                        if (skip) course.Skip();
                        else
                        {
                            for (int evidence = 0; evidence < 3; evidence++) course.ReadEvidence(evidence);
                            if (course.Step.mode == "sequence") foreach (int card in course.Step.sequenceOrder) course.ToggleSequence(card);
                            else course.Select(course.Step.correct);
                            Assert.That(course.Submit(), Is.True);
                            course.Continue();
                        }
                    }
                    binding.AcceptClinicalLessonCompleted(content);
                    binding.AcceptClinicalLessonCompleted(content);
                    Assert.That(ui.Quiz.IsVisible, Is.False, "Later multimedia lessons must not append the legacy text quiz.");
                }
                Assert.That(journey.CurrentState.CompletedCount, Is.EqualTo(station + 1));
                Assert.That(collection.CurrentState.CollectedCount, Is.Zero);
                Assert.That(collection.CurrentState.PendingPresentation, Is.Null, "No foldout or collection step may block the course.");
                var request = navigation.State.RequestId;
                guidance.Refresh(journey.CurrentState.CompletionRevision,
                    new VisitorGuidanceEnvironment(false, false, false, false, ui.Quiz.IsVisible, ui.Startup.IsCloseDecisionVisible),
                    navigation.HasFrame, false);
                Assert.That(ui.Startup.IsCloseDecisionVisible, Is.False, "A completed lesson must consume its old close decision.");
                Assert.That(navigation.State.RequestId, Is.EqualTo(request), "Next station requires explicit departure.");
                if (station == map.points.Length - 1)
                {
                    Assert.That(guidance.Phase, Is.EqualTo(VisitorGuidancePhase.Ended));
                    continue;
                }
                Assert.That(guidance.Surface.Kind, Is.EqualTo(VisitorGuidanceSurfaceKind.Departure));
                guidance.HandleIntent(VisitorDialogueIntentKind.Advance);
                Assert.That(navigation.State.RequestId, Is.GreaterThan(request));
                Assert.That(navigation.PointForTarget, Is.EqualTo(map.points[station + 1].id));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ActualContentCompletionAdvancesGeometryWithIndependentCollectionDeduplication(bool fieldbook)
        {
            var entries = AssetDatabase.LoadAssetAtPath<ContentEntryCatalog>("Assets/BotanicalGardenQR/Content/Authoring/ContentEntryCatalog.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>("Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            var scenes = new PublishedSceneResolver(AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>("Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset"));
            var map = VisitorMapConfiguration.Resolve(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json"));
            var session = JourneySessionId.CreateNew();
            var source = new TestContentLifecycleSource();
            using var collection = CollectionProgressModuleFactory.Create();
            Assert.That(catalog.TryBuild(out var collectionDefinition, out var error), Is.True, error);
            collection.BeginSession(session, collectionDefinition);
            using var journey = JourneyNavigationModuleFactory.Create();
            using var binding = new JourneyCloseDecisionBinding(source, session, journey, new TestJourneyIntentSource(),
                new TestNextPointPromptPresenter(), collection, catalog);
            using var navigation = new MapNavigationController(map, new MapMotion());
            ContentEntryRoute confirmedEntry = null;
            using var guidance = new VisitorGuidanceCoordinator(navigation, () => { },
                fieldbook ? point => entries.TryResolveMapPoint(point, out confirmedEntry) : (Func<string, bool>)null);
            for (int i = 0; i < 12; i++) navigation.TryInitialize(default, 0, 0, .02f);
            guidance.Begin(); guidance.Refresh(0, default, true, false);
            var payloads = fieldbook ? new[] { "discovery:giant_saguaro", "discovery:baobab", "discovery:bottle_tree", "discovery:ceiba", "discovery:macrozamia", "discovery:welwitschia" } : new[] { "zone:tropical_house", "plant:bamboo_001" };
            var expectedScenes = fieldbook ? new[] { GiantSaguaro, Baobab, new SceneId("bottle_tree"), new SceneId("ceiba"), new SceneId("macrozamia"), new SceneId("welwitschia") } : new[] { new SceneId("bottle_tree"), Baobab };
            var entryKind = fieldbook ? RecognitionSourceKinds.Fieldbook : RecognitionSourceKinds.Qr;
            for (int station = 0; station < payloads.Length; station++)
            {
                var route = new MapRouteGeometry(navigation.CurrentWorldPath);
                for (int frame = 0; frame < 2000 && navigation.State.Phase != MapNavigationPhase.Arrived; frame++)
                    navigation.Tick(route.Sample(navigation.State.Progress), true, false, .05f);
                Assert.That(navigation.State.Phase, Is.EqualTo(MapNavigationPhase.Arrived));
                var entry = System.Linq.Enumerable.Single(entries.Routes, value => value.EntryKind == entryKind && value.EntryValue == payloads[station]);
                Assert.That(entry.TargetSceneId, Is.EqualTo(expectedScenes[station]));
                // This legacy collection compatibility path consumes completion
                // facts. C09 observations/media no longer require a published quiz.
                var content = SessionToken.CreateNew();
                source.Open(new ContentOpenedFact(content, session, entry.TargetSceneId, entryKind, "verified_entry"));
                for (var stable = 0; stable < 4; stable++) guidance.TrackVisitorArrival(route.Sample(route.Length), true, .1f);
                if (fieldbook)
                {
                    guidance.HandleIntent(VisitorDialogueIntentKind.Advance);
                    Assert.That(confirmedEntry, Is.SameAs(entry), "Arrival must resolve the current point, independently of previous content.");
                }
                guidance.TargetContentOpened();
                source.Close(new ContentClosedFact(content, session, entry.TargetSceneId, true));
                var completed = new ObservationCompletedFact(content, session, entry.TargetSceneId,
                    ObservationCompletionKind.SingleChoice, "observation:" + entry.TargetSceneId.Value, "answer");
                Assert.That(catalog.TryGetArtifactId(completed.SceneId, out var artifact), Is.True);
                collection.OfferArtifact(completed, artifact);
                binding.AcceptObservationCompleted(completed);
                if (fieldbook)
                {
                    var offered = System.Linq.Enumerable.Single(collection.CurrentState.Artifacts, value => value.Definition.ArtifactId == artifact);
                    Assert.That(offered.State, Is.EqualTo(CollectionArtifactStatus.Available), "Every new station must offer its own foldout.");
                    Assert.That(collection.CurrentState.PendingPresentation.Kind, Is.EqualTo(CollectionPresentationKind.ArtifactOffered));
                    collection.CollectArtifact(artifact, offered.InstanceToken);
                    if (collection.CurrentState.PendingPresentation != null) collection.AcknowledgePresentation(collection.CurrentState.PendingPresentation.Id);
                    Assert.That(collection.CurrentState.CollectedCount, Is.EqualTo(station + 1));
                    Assert.That(journey.CurrentState.CompletionRevision, Is.EqualTo(station + 1));
                }
                else
                {
                    Assert.That(collection.CurrentState.PendingPresentation.ArtifactId, Is.EqualTo("artifact:" + expectedScenes[station].Value));
                    collection.DeferAvailableArtifactPresentation(collection.CurrentState.PendingPresentation.Id);
                }
                guidance.Refresh(journey.CurrentState.CompletionRevision, default, true, false);
                var request = navigation.State.RequestId;
                if (station == map.points.Length - 1)
                {
                    Assert.That(guidance.Phase, Is.EqualTo(VisitorGuidancePhase.Ended));
                    guidance.HandleIntent(VisitorDialogueIntentKind.Advance);
                    Assert.That(navigation.State.RequestId, Is.EqualTo(request));
                    continue;
                }
                Assert.That(guidance.Surface.Kind, Is.EqualTo(VisitorGuidanceSurfaceKind.Departure));
                guidance.HandleIntent(VisitorDialogueIntentKind.Advance);
                Assert.That(navigation.State.RequestId, Is.GreaterThan(request));
                Assert.That(navigation.CurrentWorldPath[0], Is.EqualTo(navigation.Frame.Transform(map.points[station].position)));
                Assert.That(navigation.PointForTarget, Is.EqualTo(map.points[station + 1].id));
            }
        }

        sealed class ProductionCloseUi : IDisposable, IRecallController, IFrontendGazeSurfaceRegistry
        {
            readonly GameObject _runtime, _viewer, _quiz;
            readonly IKnowledgeMiniGameController _controller = KnowledgeMiniGameModuleFactory.Create();
            readonly PublishedSceneResolver _definitions;
            KnowledgeMiniGameCompletionBinding _binding;
            IRecallStateSink _sink;
            bool _closed = true;
            long _revision;
            public StartupRecallPresenter Startup { get; }
            public KnowledgeMiniGameFrontend Quiz { get; }
            public RecallState CurrentState { get; private set; }
            public ProductionCloseUi()
            {
                _runtime = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab"));
                _viewer = new GameObject("ClosedDecisionViewer", typeof(Camera));
                Startup = _runtime.GetComponentInChildren<StartupRecallPresenter>(true);
                CurrentState = new RecallState(++_revision, true, true, hasPreviousContent: true);
                Startup.Configure(_viewer.transform, this, this, "完成本站学习");
                _quiz = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/ObservationCompletionSurface.prefab"));
                Quiz = _quiz.GetComponent<KnowledgeMiniGameFrontend>();
                Quiz.Configure(_viewer.transform, this);
                Quiz.SurfaceVisibilityChanged += _ => RefreshModal();
                _definitions = new PublishedSceneResolver(AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>(
                    "Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset"));
            }
            public void BindQuiz(IContentLifecycleSource source, Action<ObservationCompletedFact> completed)
                => _binding = new KnowledgeMiniGameCompletionBinding(source, Startup, _definitions, _controller, Quiz, completed);
            public void SetClosed(bool closed)
            {
                _closed = closed;
                CurrentState = new RecallState(++_revision, closed, closed, hasPreviousContent: true);
                _sink.OnStateChanged(CurrentState);
                RefreshModal();
            }
            void RefreshModal()
            {
                var policy = VisitorModalCoordinator.Resolve(new VisitorModalFacts(_revision, _closed, true,
                    completionVisible: Quiz.IsVisible, closeDecisionVisible: Startup.IsCloseDecisionVisible));
                Startup.SetApplicationSurfaceSuppressed(policy.ShellSuppressed);
            }
            public void CompleteQuiz(SessionToken session, SceneId scene)
            {
                Startup.BeginClosedContentQuiz();
                Assert.That(Quiz.IsVisible, Is.True);
                if (((IKnowledgeMiniGameDefinitionSource)_definitions).TryGet(scene, out var definition))
                    for (int i = 0; i < definition.QuestionCount; i++)
                        Assert.That(_controller.Submit(session, definition.GetQuestion(i).CorrectAnswerId).Succeeded, Is.True);
                else _quiz.GetComponentsInChildren<Button>(true).Single(b => b.name == "Button_Confirm").onClick.Invoke();
                _quiz.GetComponentsInChildren<Button>(true).Single(b => b.name == "Button_Return").onClick.Invoke();
                Assert.That(Quiz.IsVisible, Is.False);
            }
            public RecallResult Recall() => RecallResult.Success;
            public IDisposable Observe(IRecallStateSink sink) { _sink = sink; sink.OnStateChanged(CurrentState); return new DelegateDisposable(() => _sink = null); }
            public IDisposable SuspendPanelInput() => new DelegateDisposable(() => { });
            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform root, int priority, string label) => new Registration();
            sealed class Registration : IFrontendGazeSurfaceRegistration
            {
                public bool IsFocused => false;
                public void Invalidate() { }
                public void Dispose() { }
            }
            public void Dispose()
            {
                _binding?.Dispose(); _controller.Dispose(); Startup.Unconfigure();
                UnityEngine.Object.DestroyImmediate(_quiz); UnityEngine.Object.DestroyImmediate(_runtime); UnityEngine.Object.DestroyImmediate(_viewer);
            }
        }

        sealed class MapMotion : IMapMotionSink
        {
            MapPosition _position;
            public bool TryGetPosition(out MapPosition position) { position = _position; return true; }
            public bool Apply(long id, MapPosition position, MapPosition forward, bool moving) { _position = position; return true; }
            public void Hold(long id) { }
            public void Release(long id) { }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ArbitraryFirstQrCompletesOnceAndDoesNotPreselectNextPlant(bool skip)
        {
            var source = new TestContentLifecycleSource();
            var intents = new TestJourneyIntentSource();
            var prompts = new TestNextPointPromptPresenter();
            var session = JourneySessionId.CreateNew();
            var catalog = new TestCollectionCatalogSource();
            using var collection = CollectionProgressModuleFactory.Create();
            using var journey = JourneyNavigationModuleFactory.Create();
            using var binding = new JourneyCloseDecisionBinding(source, session, journey, intents,
                prompts, collection, catalog);
            collection.BeginSession(session, catalog.Catalog);
            var old = SessionToken.CreateNew();
            var current = SessionToken.CreateNew();
            source.Open(new ContentOpenedFact(old, session, GiantSaguaro, RecognitionSourceKinds.Qr, "entry_giant_saguaro"));
            source.Open(new ContentOpenedFact(current, session, Baobab, RecognitionSourceKinds.Qr, "entry_baobab"));
            source.Close(new ContentClosedFact(old, session, GiantSaguaro, true));
            binding.AcceptObservationCompleted(new ObservationCompletedFact(old, session, GiantSaguaro,
                ObservationCompletionKind.SingleChoice, "question", "answer"));
            Assert.That(journey.CurrentState.CompletionRevision, Is.Zero);
            source.Close(new ContentClosedFact(current, session, Baobab, true));
            Assert.That(journey.CurrentState.CompletionRevision, Is.Zero, "Closing alone is not completion.");
            var completed = new ObservationCompletedFact(current, session, Baobab,
                ObservationCompletionKind.SingleChoice, "question", "answer");
            if (skip) intents.RequestSkipQuizAndCollect();
            else binding.AcceptObservationCompleted(completed);
            binding.AcceptObservationCompleted(completed);
            Assert.That(journey.CurrentState.CompletionRevision, Is.EqualTo(1));
            Assert.That(journey.CurrentState.CompletedCount, Is.EqualTo(1));
            Assert.That(journey.CurrentState.CurrentScene, Is.EqualTo(Baobab));
            Assert.That(journey.CurrentState.ProgressState, Is.EqualTo(JourneyProgressState.AwaitingQr));
            Assert.That(prompts.NextTitles, Is.Empty, "Content completion cannot name a next plant.");
            Assert.That(prompts.ClosedContentContext, Is.Empty);
            if (skip) Assert.That(CollectionState(collection, "artifact:baobab"), Is.EqualTo(CollectionArtifactStatus.Collected));
        }

        [Test]
        public void MissingArtifactCannotGrantOrCompleteSkippedQuiz()
        {
            var source = new TestContentLifecycleSource();
            var intents = new TestJourneyIntentSource();
            var prompts = new TestNextPointPromptPresenter();
            var session = JourneySessionId.CreateNew();
            var catalog = new TestCollectionCatalogSource(mapBaobab: false);
            using var collection = CollectionProgressModuleFactory.Create();
            using var journey = JourneyNavigationModuleFactory.Create();
            using var binding = new JourneyCloseDecisionBinding(source, session, journey, intents,
                prompts, collection, catalog);
            collection.BeginSession(session, catalog.Catalog);
            var current = SessionToken.CreateNew();
            source.Open(new ContentOpenedFact(current, session, Baobab, RecognitionSourceKinds.Qr, "entry_baobab"));
            source.Close(new ContentClosedFact(current, session, Baobab, true));
            intents.RequestSkipQuizAndCollect();
            Assert.That(journey.CurrentState.CompletionRevision, Is.Zero);
            Assert.That(collection.CurrentState.CollectedCount, Is.Zero);
            Assert.That(prompts.Messages, Is.Not.Empty);
        }

        static CollectionArtifactStatus CollectionState(ICollectionProgress collection, string artifactId)
        {
            for (var index = 0; index < collection.CurrentState.Artifacts.Count; index++)
                if (string.Equals(
                        collection.CurrentState.Artifacts[index].Definition.ArtifactId,
                        artifactId,
                        StringComparison.Ordinal))
                    return collection.CurrentState.Artifacts[index].State;
            Assert.Fail($"Artifact '{artifactId}' was not found.");
            return CollectionArtifactStatus.Unseen;
        }

        sealed class TestContentLifecycleSource : IContentLifecycleSource
        {
            readonly List<IContentLifecycleSink> _sinks = new List<IContentLifecycleSink>();

            public IDisposable Observe(IContentLifecycleSink sink)
            {
                _sinks.Add(sink);
                return new DelegateDisposable(() => _sinks.Remove(sink));
            }

            public void Open(ContentOpenedFact fact)
            {
                var snapshot = _sinks.ToArray();
                for (var index = 0; index < snapshot.Length; index++) snapshot[index].OnContentOpened(fact);
            }

            public void Close(ContentClosedFact fact)
            {
                var snapshot = _sinks.ToArray();
                for (var index = 0; index < snapshot.Length; index++) snapshot[index].OnContentClosed(fact);
            }
        }

        sealed class TestJourneyIntentSource : IClosedContentJourneyIntentSource
        {
            public event Action SkipQuizAndCollectRequested;
            public void RequestSkipQuizAndCollect() => SkipQuizAndCollectRequested?.Invoke();
        }

        sealed class TestNextPointPromptPresenter : INextPointPromptPresenter
        {
            public void SetClosedContentRequiresLearning(bool required) { }
            public void CompleteClosedContent() { }
            public readonly List<string> NextTitles = new List<string>();
            public readonly List<string> Messages = new List<string>();
            public readonly List<string> StatusMessages = new List<string>();
            public string ClosedContentContext { get; private set; } = string.Empty;
            public int ClearCount { get; private set; }
            bool _hasPrompt;

            public void ShowJourneyMessage(string message) => Messages.Add(message);
            public void ShowJourneyStatus(string message)
            {
                StatusMessages.Add(message);
                _hasPrompt = true;
            }
            public void SetClosedContentContext(string message)
                => ClosedContentContext = message ?? string.Empty;
            public void ClearJourneyPrompt()
            {
                if (!_hasPrompt) return;
                _hasPrompt = false;
                ClearCount++;
            }
        }

        sealed class TestCollectionCatalogSource : ICollectionCatalogSource
        {
            readonly bool _mapGiantSaguaro;
            readonly bool _mapBaobab;

            public TestCollectionCatalogSource(bool mapGiantSaguaro = true, bool mapBaobab = true)
            {
                _mapGiantSaguaro = mapGiantSaguaro;
                _mapBaobab = mapBaobab;
                Catalog = new CollectionCatalog(new[]
                {
                    new CollectionArtifactDefinition(
                        "artifact:giant_saguaro", "巨人柱果实", "", "植物", 0, "slot:giant_saguaro"),
                    new CollectionArtifactDefinition(
                        "artifact:baobab", "猴面包树果实", "", "植物", 1, "slot:baobab")
                });
            }

            public CollectionCatalog Catalog { get; }

            public bool TryGetCollectionCatalog(out CollectionCatalog catalog)
            {
                catalog = Catalog;
                return true;
            }

            public bool TryGetArtifactId(SceneId sceneId, out string artifactId)
            {
                if (_mapGiantSaguaro && sceneId == GiantSaguaro)
                {
                    artifactId = "artifact:giant_saguaro";
                    return true;
                }
                if (_mapBaobab && sceneId == Baobab)
                {
                    artifactId = "artifact:baobab";
                    return true;
                }
                artifactId = string.Empty;
                return false;
            }
        }

        sealed class DelegateDisposable : IDisposable
        {
            Action _dispose;
            public DelegateDisposable(Action dispose) => _dispose = dispose;
            public void Dispose()
            {
                var dispose = _dispose;
                _dispose = null;
                dispose?.Invoke();
            }
        }
    }
}
