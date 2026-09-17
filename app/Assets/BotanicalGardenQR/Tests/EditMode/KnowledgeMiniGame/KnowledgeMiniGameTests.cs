using System;
using System.Linq;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Collection.Runtime;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.KnowledgeMiniGame.Backend;
using BotanicalGardenQR.KnowledgeMiniGame.Contracts;
using BotanicalGardenQR.KnowledgeMiniGame.Frontend;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.KnowledgeMiniGame
{
    public sealed class KnowledgeMiniGameTests
    {
        [Test]
        public void WrongAnswerCanRetryThenCorrectAnswerAdvancesUntilFinalQuestionCompletes()
        {
            using var controller = KnowledgeMiniGameModuleFactory.Create();
            var sink = new RecordingSink();
            using var subscription = controller.Observe(sink);
            var session = SessionToken.CreateNew();

            Assert.That(controller.Open(session, CreateDefinition()).Succeeded, Is.True);
            Assert.That(sink.State.Phase, Is.EqualTo(KnowledgeMiniGamePhase.Ready));

            Assert.That(controller.Submit(session, "woodpecker").Succeeded, Is.True);
            Assert.That(sink.State.Phase, Is.EqualTo(KnowledgeMiniGamePhase.Incorrect));
            Assert.That(sink.State.Attempts, Is.EqualTo(1));
            Assert.That(sink.State.CanSubmit, Is.True);
            Assert.That(sink.State.Feedback, Is.EqualTo("Look at the night clue."));

            Assert.That(controller.Submit(session, "bat").Succeeded, Is.True);
            Assert.That(sink.State.Phase, Is.EqualTo(KnowledgeMiniGamePhase.Ready));
            Assert.That(sink.State.Attempts, Is.EqualTo(2));
            Assert.That(sink.State.QuestionIndex, Is.EqualTo(1));
            Assert.That(sink.State.IsCompleted, Is.False);
            Assert.That(sink.State.CanSubmit, Is.True);
            Assert.That(sink.State.Feedback, Is.EqualTo("Bats visit the flowers at night."));

            Assert.That(controller.Submit(session, "water").Succeeded, Is.True);
            Assert.That(sink.State.Phase, Is.EqualTo(KnowledgeMiniGamePhase.Completed));
            Assert.That(sink.State.Attempts, Is.EqualTo(3));
            Assert.That(sink.State.IsCompleted, Is.True);
            Assert.That(sink.State.CanSubmit, Is.False);
            Assert.That(sink.State.Feedback, Is.EqualTo("Vascular bundles support the cactus and move water."));
            Assert.That(
                controller.Submit(session, "water").FailureCode,
                Is.EqualTo(KnowledgeMiniGameFailureCode.AlreadyCompleted));
        }

        [Test]
        public void StaleSessionCannotChangeCurrentAnswerState()
        {
            using var controller = KnowledgeMiniGameModuleFactory.Create();
            var sink = new RecordingSink();
            using var subscription = controller.Observe(sink);
            var active = SessionToken.CreateNew();

            controller.Open(active, CreateDefinition());
            var versionBefore = sink.State.Version;
            var result = controller.Submit(SessionToken.CreateNew(), "bat");

            Assert.That(result.FailureCode, Is.EqualTo(KnowledgeMiniGameFailureCode.StaleSession));
            Assert.That(sink.State.Version, Is.EqualTo(versionBefore));
            Assert.That(sink.State.Phase, Is.EqualTo(KnowledgeMiniGamePhase.Ready));
        }

        [Test]
        public void PublishedLibraryConfiguresEveryPlantWithOneCurrentProductQuestion()
        {
            var library = AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>(
                "Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset");
            Assert.That(library, Is.Not.Null);
            var source = (IKnowledgeMiniGameDefinitionSource)new PublishedSceneResolver(library);

            var sceneIds = new[]
            {
                "giant_saguaro",
                "baobab",
                "bottle_tree",
                "ceiba",
                "macrozamia",
                "welwitschia"
            };
            foreach (var sceneId in sceneIds)
            {
                Assert.That(
                    source.TryGet(new SceneId(sceneId), out var plantDefinition),
                    Is.True,
                    $"{sceneId} should publish a knowledge quiz.");
                Assert.That(plantDefinition.Kind, Is.EqualTo(KnowledgeMiniGameKind.SingleChoice), sceneId);
                Assert.That(plantDefinition.QuestionCount, Is.EqualTo(1), sceneId);
            }

            Assert.That(source.TryGet(new SceneId("giant_saguaro"), out var definition), Is.True);
            Assert.That(definition.QuestionCount, Is.EqualTo(1));
            Assert.That(definition.GetQuestion(0).Question, Does.Contain("生长环境"));
            Assert.That(definition.GetQuestion(0).Options[0].Text, Is.EqualTo("北美荒漠"));
        }

        [Test]
        public void EveryPlantQuizHasUniqueConfiguredArtifactDrop()
        {
            const string catalogPath = "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(catalogPath);
            Assert.That(catalog, Is.Not.Null);

            var sceneIds = new[]
            {
                "giant_saguaro",
                "baobab",
                "bottle_tree",
                "ceiba",
                "macrozamia",
                "welwitschia"
            };
            var artifactIds = sceneIds.Select(sceneId =>
            {
                Assert.That(
                    catalog.TryGetArtifactId(new SceneId(sceneId), out var artifactId),
                    Is.True,
                    $"{sceneId} should map to one artifact.");
                Assert.That(catalog.TryGetArtifactPresentation(artifactId, out var presentation), Is.True, sceneId);
                Assert.That(presentation.PresentationPrefab, Is.Not.Null, sceneId);
                Assert.That(presentation.FoldoutImage, Is.Not.Null, sceneId);
                Assert.That(presentation.FoldoutDetailImage, Is.Not.Null, sceneId);
                Assert.That(AssetDatabase.GetAssetPath(presentation.FoldoutImage), Does.StartWith("Assets/BotanicalGardenQR/Content/Scenes/" + sceneId + "/Images/"));
                Assert.That(AssetDatabase.GetAssetPath(presentation.FoldoutDetailImage), Does.StartWith("Assets/BotanicalGardenQR/Content/Scenes/" + sceneId + "/Images/"));
                Assert.That(presentation.DropDuration, Is.GreaterThan(0f), sceneId);
                return artifactId;
            }).ToArray();

            Assert.That(artifactIds.Distinct().Count(), Is.EqualTo(sceneIds.Length));
            Assert.That(catalog.TryBuild(out var definition, out var error), Is.True, error);
            Assert.That(definition.TotalCount, Is.EqualTo(sceneIds.Length));
            Assert.That(catalog.TryValidatePresentation(out error), Is.True, error);
        }

        [Test]
        public void CollectionWorldHasOneAuthoredBrowseSlotPerCatalogArtifact()
        {
            const string catalogPath = "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset";
            const string prefabPath =
                "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/CollectionWorldPresentation.prefab";
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(catalogPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);

            var frontend = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(component => component.GetType().Name == "CollectionWorldFrontend");
            var entryViews = new SerializedObject(frontend).FindProperty("_entryViews");
            Assert.That(entryViews, Is.Not.Null);
            Assert.That(entryViews.arraySize, Is.EqualTo(catalog.Artifacts.Count));

            var roots = Enumerable.Range(0, entryViews.arraySize)
                .Select(index => entryViews.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("_root").objectReferenceValue)
                .ToArray();
            var buttons = Enumerable.Range(0, entryViews.arraySize)
                .Select(index => entryViews.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("_button").objectReferenceValue)
                .ToArray();
            Assert.That(roots, Has.All.Not.Null);
            Assert.That(buttons, Has.All.Not.Null);
            Assert.That(roots.Distinct().Count(), Is.EqualTo(catalog.Artifacts.Count));
            Assert.That(buttons.Distinct().Count(), Is.EqualTo(catalog.Artifacts.Count));
        }

        [Test]
        public void PostCloseQuizCanReturnToChoicesAndFinalExplanationContinuesExactlyOnce()
        {
            const string prefabPath =
                "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/ObservationCompletionSurface.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);

            var instance = UnityEngine.Object.Instantiate(prefab);
            var viewer = new GameObject("KnowledgeMiniGameTestViewer");
            viewer.AddComponent<Camera>();
            var frontend = instance.GetComponent<KnowledgeMiniGameFrontend>();
            var content = new TestContentLifecycleSource();
            var requests = new TestCompletionRequestSource();
            var sceneId = new SceneId("giant_saguaro");
            var journeySession = JourneySessionId.CreateNew();
            var contentSession = SessionToken.CreateNew();
            var artifact = new CollectionArtifactDefinition(
                "artifact:giant_saguaro",
                "巨人柱果实",
                "观察完成后掉落的收藏物",
                "植物观察",
                0,
                "slot:giant_saguaro");
            ObservationCompletedFact completed = null;
            var completionCount = 0;

            try
            {
                frontend.Configure(viewer.transform, new TestGazeSurfaceRegistry());
                using var controller = KnowledgeMiniGameModuleFactory.Create();
                using var collection = CollectionProgressModuleFactory.Create();
                collection.BeginSession(journeySession, new CollectionCatalog(new[] { artifact }));
                using var binding = new KnowledgeMiniGameCompletionBinding(
                    content,
                    requests,
                    new TestDefinitionSource(sceneId, CreateDefinition()),
                    controller,
                    frontend,
                    fact =>
                    {
                        completed = fact;
                        completionCount++;
                        Assert.That(frontend.IsVisible, Is.False,
                            "The quiz must release its modal before a reward is offered.");
                        collection.OfferArtifact(fact, artifact.ArtifactId);
                    });

                content.Open(new ContentOpenedFact(
                    contentSession,
                    journeySession,
                    sceneId,
                    new SourceKind("qr"),
                    "entry_giant"));
                content.Close(new ContentClosedFact(contentSession, journeySession, sceneId, true));
                Assert.That(completed, Is.Null);
                Assert.That(
                    collection.CurrentState.Artifacts.Single().State,
                    Is.EqualTo(CollectionArtifactStatus.Unseen),
                    "Closing ordinary content must not offer the artifact before the quiz completes.");

                requests.Raise();
                Assert.That(frontend.IsVisible, Is.True);
                var activeAnswers = instance.GetComponentsInChildren<Button>(true)
                    .Where(button => button.name.StartsWith("Answer_") && button.gameObject.activeSelf)
                    .OrderBy(button => button.name)
                    .ToArray();
                Assert.That(activeAnswers, Has.Length.EqualTo(3));
                Assert.That(
                    ((RectTransform)activeAnswers[2].transform).anchoredPosition,
                    Is.EqualTo(new Vector2(0f, -85f)),
                    "A three-option quiz should center its final gaze card.");

                instance.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "Button_Return")
                    .onClick.Invoke();
                Assert.That(frontend.IsVisible, Is.False);
                Assert.That(completed, Is.Null);
                Assert.That(collection.CurrentState.Artifacts.Single().State, Is.EqualTo(CollectionArtifactStatus.Unseen));

                requests.Raise();
                Assert.That(frontend.IsVisible, Is.True);
                Assert.That(controller.Submit(contentSession, "bat").Succeeded, Is.True);
                Assert.That(completed, Is.Null);
                Assert.That(controller.Submit(contentSession, "water").Succeeded, Is.True);
                Assert.That(completed, Is.Null, "The final explanation must remain readable before handoff.");
                Assert.That(frontend.IsVisible, Is.True);
                Assert.That(instance.GetComponentsInChildren<Button>()
                    .Where(button => button.gameObject.activeInHierarchy)
                    .Select(button => button.name), Is.EquivalentTo(new[] { "Button_Return" }));
                Assert.That(instance.GetComponentsInChildren<TMP_Text>()
                    .Single(text => text.name == "Feedback").text,
                    Is.EqualTo("Vascular bundles support the cactus and move water."));
                Assert.That(instance.GetComponentsInChildren<Image>()
                    .Single(image => image.name == "TopAccent").fillAmount, Is.EqualTo(1f));

                requests.Raise();
                content.Close(new ContentClosedFact(SessionToken.CreateNew(), journeySession, sceneId, true));
                content.Close(new ContentClosedFact(contentSession, journeySession, sceneId, true));
                Assert.That(frontend.IsVisible, Is.True, "Old or duplicate close facts must not discard the explanation.");
                var continueButton = instance.GetComponentsInChildren<Button>()
                    .Single(button => button.name == "Button_Return");
                continueButton.onClick.Invoke();
                continueButton.onClick.Invoke();
                requests.Raise();
                Assert.That(completionCount, Is.EqualTo(1));
                Assert.That(completed, Is.Not.Null);
                Assert.That(completed.SceneId, Is.EqualTo(sceneId));
                Assert.That(frontend.IsVisible, Is.False);
                Assert.That(collection.CurrentState.Artifacts.Single().State, Is.EqualTo(CollectionArtifactStatus.Available));
                Assert.That(
                    collection.CurrentState.PendingPresentation.Kind,
                    Is.EqualTo(CollectionPresentationKind.ArtifactOffered));
            }
            finally
            {
                if (frontend != null) frontend.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void HiddenInputsAndStaleSnapshotsCannotSubmitOrDismissTheCurrentQuiz()
        {
            using var view = new QuizView();
            using var controller = KnowledgeMiniGameModuleFactory.Create();
            var sink = new RecordingSink();
            using var states = controller.Observe(sink);
            var session = SessionToken.CreateNew();
            view.Frontend.Bind(session, controller);
            controller.Open(session, CreateDefinition());
            var exits = 0;
            view.Frontend.ExitRequested += _ => exits++;
            view.Button("Answer_0").onClick.Invoke();
            view.Button("Button_Return").onClick.Invoke();
            Assert.That(sink.State.Attempts, Is.Zero);
            Assert.That(exits, Is.Zero);

            view.Frontend.SetVisible(true);
            view.Frontend.Publish(new KnowledgeMiniGameState(
                SessionToken.CreateNew(), long.MaxValue, KnowledgeMiniGamePhase.Closed));
            Assert.That(view.Frontend.IsVisible, Is.True);
            view.Button("Answer_0").onClick.Invoke();
            Assert.That(sink.State.QuestionIndex, Is.EqualTo(1));
            view.Frontend.Publish(new KnowledgeMiniGameState(
                session, 0, KnowledgeMiniGamePhase.Ready, CreateDefinition()));
            Assert.That(view.Text("Question").text, Is.EqualTo("What moves through the cactus?"));
            view.Frontend.SetVisible(false);
            view.Button("Answer_0").onClick.Invoke();
            Assert.That(sink.State.IsCompleted, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SingleQuestionAndConfirmationWaitForAcknowledgementAndCancelPendingWorkOnNewContent(bool confirmation)
        {
            using var view = new QuizView();
            using var controller = KnowledgeMiniGameModuleFactory.Create();
            var content = new TestContentLifecycleSource();
            var requests = new TestCompletionRequestSource();
            var scene = new SceneId("test_observation");
            var session = SessionToken.CreateNew();
            var journey = JourneySessionId.CreateNew();
            var definition = confirmation
                ? KnowledgeMiniGameDefinition.CreateConfirmation("完成观察？", "记住刚才看到的线索。")
                : new KnowledgeMiniGameDefinition(new[] { CreateDefinition().GetQuestion(0) });
            var count = 0;
            using var binding = new KnowledgeMiniGameCompletionBinding(
                content, requests, new TestDefinitionSource(scene, definition), controller, view.Frontend, _ => count++);
            content.Open(new ContentOpenedFact(session, journey, scene, new SourceKind("qr"), "test-entry"));
            content.Close(new ContentClosedFact(session, journey, scene, true));
            requests.Raise();
            controller.Submit(session, confirmation ? "confirm" : "bat");
            Assert.That(view.Frontend.IsVisible, Is.True);
            Assert.That(count, Is.Zero);
            Assert.That(view.Button("Button_Confirm").gameObject.activeSelf, Is.False);
            var continueButton = view.Button("Button_Return");
            content.Open(new ContentOpenedFact(SessionToken.CreateNew(), journey, scene, new SourceKind("qr"), "test-entry"));
            continueButton.onClick.Invoke();
            Assert.That(view.Frontend.IsVisible, Is.False);
            Assert.That(count, Is.Zero, "A queued completion input cannot award an interrupted old observation.");
        }

        [Test]
        public void GazeFeedbackChangesColourWithoutMovingTheHitTarget()
        {
            using var view = new QuizView();
            var button = view.Button("Answer_0");
            var rect = (RectTransform)button.transform;
            var before = new Vector3[4];
            var after = new Vector3[4];
            rect.GetWorldCorners(before);
            var visual = button.GetComponent<IFrontendGazeProgressPresenter>();
            var surface = button.GetComponent<Image>();
            var baseColour = surface.color;
            foreach (var progress in new[] { 0.25f, 0.7f, 1f })
            {
                visual.PresentGazeProgress(progress);
                rect.GetWorldCorners(after);
                Assert.That(after, Is.EqualTo(before));
                Assert.That(surface.color, Is.Not.EqualTo(baseColour));
            }
            visual.PresentGazeProgress(0f);
            Assert.That(surface.color, Is.EqualTo(baseColour));
        }

        [Test]
        public void EveryPublishedQuestionRetryAndExplanationFitsTheAuthoredReadingSurface()
        {
            using var view = new QuizView();
            var library = AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>(
                "Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset");
            var source = (IKnowledgeMiniGameDefinitionSource)new PublishedSceneResolver(library);
            foreach (var package in library.Packages)
            {
                if (!source.TryGet(package.SceneId, out var definition)) continue;
                using var controller = KnowledgeMiniGameModuleFactory.Create();
                var session = SessionToken.CreateNew();
                view.Frontend.Bind(session, controller);
                controller.Open(session, definition);
                view.Frontend.SetVisible(true);
                view.AssertTextFits(package.SceneId + " ready");
                for (var index = 0; index < definition.QuestionCount; index++)
                {
                    var question = definition.GetQuestion(index);
                    controller.Submit(session, question.Options.First(option => !question.IsCorrect(option.AnswerId)).AnswerId);
                    view.AssertTextFits(package.SceneId + " retry");
                    controller.Submit(session, question.CorrectAnswerId);
                    view.AssertTextFits(package.SceneId + " success");
                }
                view.Frontend.Unbind();
            }
        }

        [Test]
        public void ObservationCompletionSurfaceUsesGazeOnlySeparatedControls()
        {
            const string prefabPath =
                "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/ObservationCompletionSurface.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);

            var controls = prefab.GetComponentsInChildren<Button>(true)
                .Where(button => button != null &&
                    (button.name.StartsWith("Answer_") ||
                     button.name == "Button_Confirm" ||
                     button.name == "Button_Return" ||
                     button.name == "Button_Defer"))
                .ToArray();

            Assert.That(controls, Has.Length.EqualTo(7));
            foreach (var control in controls)
            {
                Assert.That(control.targetGraphic, Is.Not.Null, control.name);
                Assert.That(control.targetGraphic.raycastTarget, Is.False, control.name);
                Assert.That(control.navigation.mode, Is.EqualTo(Navigation.Mode.None), control.name);
                Assert.That(
                    control.GetComponent<IFrontendGazeProgressPresenter>(),
                    Is.Not.Null,
                    $"{control.name} is missing authored dwell feedback.");
            }

            var singleChoiceControls = controls
                .Where(control => control.name.StartsWith("Answer_") || control.name == "Button_Return")
                .ToArray();
            AssertControlsDoNotOverlap(singleChoiceControls);
            AssertControlsDoNotOverlap(controls
                .Where(control => control.name == "Button_Confirm" || control.name == "Button_Return")
                .ToArray());

            var answerRects = controls
                .Where(control => control.name.StartsWith("Answer_"))
                .Select(control => control.transform as RectTransform)
                .ToArray();
            Assert.That(answerRects, Has.Length.EqualTo(4));
            Assert.That(answerRects.All(rect => rect != null && rect.sizeDelta.y >= 100f), Is.True);
            Assert.That(answerRects.Count(rect => rect.anchoredPosition.x < 0f), Is.EqualTo(2));
            Assert.That(answerRects.Count(rect => rect.anchoredPosition.x > 0f), Is.EqualTo(2));
        }

        static void AssertControlsDoNotOverlap(Button[] controls)
        {
            for (var left = 0; left < controls.Length; left++)
            {
                for (var right = left + 1; right < controls.Length; right++)
                {
                    var leftRect = controls[left].transform as RectTransform;
                    var rightRect = controls[right].transform as RectTransform;
                    Assert.That(leftRect, Is.Not.Null, controls[left].name);
                    Assert.That(rightRect, Is.Not.Null, controls[right].name);
                    Assert.That(
                        RectanglesOverlap(leftRect, rightRect),
                        Is.False,
                        $"{controls[left].name} overlaps {controls[right].name}.");
                }
            }
        }

        [Test]
        public void ObservationCompletionFrontendReenforcesGazeOnlyInput()
        {
            const string prefabPath =
                "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/ObservationCompletionSurface.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);

            var instance = UnityEngine.Object.Instantiate(prefab);
            var viewer = new GameObject("KnowledgeMiniGameGazeOnlyViewer");
            viewer.AddComponent<Camera>();
            var frontend = instance.GetComponent<KnowledgeMiniGameFrontend>();

            try
            {
                var controls = instance.GetComponentsInChildren<Button>(true)
                    .Where(button => button != null &&
                        (button.name.StartsWith("Answer_") ||
                         button.name == "Button_Confirm" ||
                         button.name == "Button_Return" ||
                         button.name == "Button_Defer"))
                    .ToArray();
                Assert.That(controls, Has.Length.EqualTo(7));
                foreach (var control in controls)
                {
                    control.targetGraphic.raycastTarget = true;
                    var navigation = control.navigation;
                    navigation.mode = Navigation.Mode.Automatic;
                    control.navigation = navigation;
                }

                frontend.Configure(viewer.transform, new TestGazeSurfaceRegistry());

                foreach (var control in controls)
                {
                    Assert.That(control.targetGraphic.raycastTarget, Is.False, control.name);
                    Assert.That(control.navigation.mode, Is.EqualTo(Navigation.Mode.None), control.name);
                }
            }
            finally
            {
                if (frontend != null) frontend.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void ProductionVisitorRuntimeUsesRedesignedGazeCardSurface()
        {
            const string runtimePath =
                "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab";
            var runtime = AssetDatabase.LoadAssetAtPath<GameObject>(runtimePath);
            Assert.That(runtime, Is.Not.Null);

            var frontend = runtime.GetComponentInChildren<KnowledgeMiniGameFrontend>(true);
            Assert.That(frontend, Is.Not.Null, "Production VisitorRuntime is not wired to the quiz surface.");
            var canvas = frontend.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(((RectTransform)canvas.transform).sizeDelta, Is.EqualTo(new Vector2(1000f, 720f)));

            var answers = frontend.GetComponentsInChildren<Button>(true)
                .Where(button => button.name.StartsWith("Answer_"))
                .ToArray();
            Assert.That(answers, Has.Length.EqualTo(4));
            Assert.That(answers.All(answer =>
            {
                var rect = (RectTransform)answer.transform;
                return rect.sizeDelta == new Vector2(410f, 116f) &&
                       answer.GetComponent<IFrontendGazeProgressPresenter>() != null;
            }), Is.True);
        }

        static bool RectanglesOverlap(RectTransform left, RectTransform right)
        {
            var offset = left.anchoredPosition - right.anchoredPosition;
            var horizontalLimit = (left.sizeDelta.x + right.sizeDelta.x) * 0.5f;
            var verticalLimit = (left.sizeDelta.y + right.sizeDelta.y) * 0.5f;
            return Mathf.Abs(offset.x) < horizontalLimit && Mathf.Abs(offset.y) < verticalLimit;
        }

        static KnowledgeMiniGameDefinition CreateDefinition()
            => new KnowledgeMiniGameDefinition(
                new[]
                {
                    new KnowledgeMiniGameQuestionDefinition(
                        "Who visits the flower at night?",
                        new[]
                        {
                            new KnowledgeMiniGameOptionDefinition("bat", "Bat"),
                            new KnowledgeMiniGameOptionDefinition("woodpecker", "Woodpecker"),
                            new KnowledgeMiniGameOptionDefinition("lizard", "Lizard")
                        },
                        "bat",
                        "Bats visit the flowers at night.",
                        "Look at the night clue."),
                    new KnowledgeMiniGameQuestionDefinition(
                        "What moves through the cactus?",
                        new[]
                        {
                            new KnowledgeMiniGameOptionDefinition("water", "Water"),
                            new KnowledgeMiniGameOptionDefinition("sand", "Sand")
                        },
                        "water",
                        "Vascular bundles support the cactus and move water.",
                        "Look at the cactus interior clue.")
                });

        sealed class RecordingSink : IKnowledgeMiniGameStateSink
        {
            public KnowledgeMiniGameState State { get; private set; }
            public void Publish(KnowledgeMiniGameState state) => State = state;
        }

        sealed class QuizView : IDisposable
        {
            readonly GameObject _instance;
            readonly GameObject _viewer;
            public KnowledgeMiniGameFrontend Frontend { get; }

            public QuizView()
            {
                _instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/ObservationCompletionSurface.prefab"));
                _viewer = new GameObject("QuizReviewViewer", typeof(Camera));
                Frontend = _instance.GetComponent<KnowledgeMiniGameFrontend>();
                Frontend.Configure(_viewer.transform, new TestGazeSurfaceRegistry());
            }

            public Button Button(string name) => _instance.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
            public TMP_Text Text(string name) => _instance.GetComponentsInChildren<TMP_Text>(true).Single(item => item.name == name);

            public void AssertTextFits(string state)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var text in _instance.GetComponentsInChildren<TMP_Text>())
                {
                    var size = text.rectTransform.rect.size;
                    var preferred = text.GetPreferredValues(text.text, size.x, 0f);
                    Assert.That(text.enableAutoSizing, Is.False, state + " / " + text.name);
                    Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Overflow), state + " / " + text.name);
                    text.ForceMeshUpdate();
                    Assert.That(preferred.y, Is.LessThanOrEqualTo(size.y + 0.5f), state + " / " + text.name + " / " + text.text);
                    Assert.That(text.textBounds.size.x, Is.LessThanOrEqualTo(size.x + 0.5f), state + " / " + text.name + " / " + text.text);
                }
            }

            public void Dispose()
            {
                Frontend.Dispose();
                UnityEngine.Object.DestroyImmediate(_instance);
                UnityEngine.Object.DestroyImmediate(_viewer);
            }
        }

        sealed class TestDefinitionSource : IKnowledgeMiniGameDefinitionSource
        {
            readonly SceneId _sceneId;
            readonly KnowledgeMiniGameDefinition _definition;

            public TestDefinitionSource(SceneId sceneId, KnowledgeMiniGameDefinition definition)
            {
                _sceneId = sceneId;
                _definition = definition;
            }

            public bool TryGet(SceneId sceneId, out KnowledgeMiniGameDefinition definition)
            {
                definition = sceneId == _sceneId ? _definition : null;
                return definition != null;
            }
        }

        sealed class TestCompletionRequestSource : IObservationCompletionRequestSource
        {
            public event Action ObservationCompletionRequested;
            public void Raise() => ObservationCompletionRequested?.Invoke();
        }

        sealed class TestContentLifecycleSource : IContentLifecycleSource
        {
            IContentLifecycleSink _sink;

            public IDisposable Observe(IContentLifecycleSink sink)
            {
                _sink = sink;
                return new TestSubscription(() => _sink = null);
            }

            public void Open(ContentOpenedFact fact) => _sink?.OnContentOpened(fact);
            public void Close(ContentClosedFact fact) => _sink?.OnContentClosed(fact);
        }

        sealed class TestGazeSurfaceRegistry : IFrontendGazeSurfaceRegistry
        {
            public System.IDisposable SuspendPanelInput() => new Suspension();
            sealed class Suspension : System.IDisposable { public void Dispose() { } }
            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(
                Transform surfaceRoot,
                int priority,
                string label)
                => new TestGazeRegistration();
        }

        sealed class TestGazeRegistration : IFrontendGazeSurfaceRegistration
        {
            public void Invalidate() { }

            public bool IsFocused => false;
            public void Dispose() { }
        }

        sealed class TestSubscription : IDisposable
        {
            Action _dispose;

            public TestSubscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                var dispose = _dispose;
                _dispose = null;
                dispose?.Invoke();
            }
        }
    }
}
