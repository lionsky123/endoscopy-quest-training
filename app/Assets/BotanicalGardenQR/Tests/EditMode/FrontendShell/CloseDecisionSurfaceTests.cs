using System;
using System.Linq;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.FrontendShell.Contracts;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.FrontendShell
{
    public sealed class CloseDecisionSurfaceTests
    {
        const string VisitorRuntimePath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab";
        const string GlobalUiDefaultsPath =
            "Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset";

        [Test]
        public void ProductionStartupHintRetainsAFirstQrFallbackCopy()
        {
            var defaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(GlobalUiDefaultsPath);
            Assert.That(defaults, Is.Not.Null, GlobalUiDefaultsPath);
            Assert.That(defaults.StartupHint,
                Is.EqualTo("跟随路线到站，把圆点停在“开启发现”上。"));
        }

        [Test]
        public void ProductionFrontendShellPassesItsSemanticConfigurationContract()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorRuntimePath);
            Assert.That(prefab, Is.Not.Null, VisitorRuntimePath);

            var shell = prefab.GetComponentInChildren<GlobalFrontendShell>(true);
            Assert.That(shell, Is.Not.Null);
            Assert.That(() => shell.ValidateConfiguration(), Throws.Nothing);
        }

        [Test]
        public void ProductionShellUsesAuthoredFixedSurfaceLayersWithoutRuntimeDuplicates()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorRuntimePath);
            Assert.That(prefab, Is.Not.Null, VisitorRuntimePath);

            var authoredGlassSurfaces = new[]
            {
                "KnowledgePanel",
                "SpatialDock",
                "ObservationMediaSlot",
                "StatusPill",
                "ModeCrumb",
                "VideoStage",
                "VideoControlSlot",
                "ModelInfoSlot",
                "ModelControlSlot"
            };
            var authoredTransforms = prefab.GetComponentsInChildren<Transform>(true);
            foreach (var surfaceName in authoredGlassSurfaces)
            {
                var surfaces = authoredTransforms.Where(value => value.name == surfaceName).ToArray();
                Assert.That(surfaces, Is.Not.Empty, $"Missing authored {surfaceName} surface.");
                foreach (var surface in surfaces)
                {
                    Assert.That(surface.Find("DepthShadow"), Is.Not.Null, $"{surface.name} requires its authored shadow.");
                    Assert.That(surface.Find("GlassHighlight"), Is.Not.Null, $"{surface.name} requires its authored highlight.");
                }
            }

            var modelStage = authoredTransforms
                .Single(value => value.name == "ModelStage");
            Assert.That(modelStage.Find("StageBottomGlow"), Is.Not.Null);
            Assert.That(modelStage.Find("StageTopHairline"), Is.Not.Null);
            var buttonSurfaces = authoredTransforms.Where(value => value.name == "ButtonSurface").ToArray();
            Assert.That(buttonSurfaces, Is.Not.Empty, "Production buttons require authored visual surfaces.");
            foreach (var surface in buttonSurfaces)
            {
                Assert.That(surface.parent.Find("ButtonOutline"), Is.Not.Null);
                Assert.That(surface.parent.Find("FocusAura"), Is.Not.Null);
            }

            var instance = UnityEngine.Object.Instantiate(prefab);
            var shell = instance.GetComponentInChildren<GlobalFrontendShell>(true);
            try
            {
                var text = new TextStyleSpec(28f, Color.white, Vector2.zero, 900f);
                shell.Configure(
                    new SilentFlow(),
                    new PresentationSpec(
                        text,
                        text,
                        new LayoutSpec(new Vector2(1080f, 608f), Vector2.zero, 12f),
                        new AnimationSpec(0.15f)),
                    string.Empty);

                var generatedLayers = instance.GetComponentsInChildren<Transform>(true)
                    .Where(value => value.name == "RuntimeTopRim" ||
                                    value.name.StartsWith("RuntimeDepthLift_", StringComparison.Ordinal))
                    .Select(value => value.name)
                    .ToArray();
                Assert.That(
                    generatedLayers,
                    Is.Empty,
                    "Fixed shadow/rim hierarchy belongs to the production Prefab, not runtime setup.");
            }
            finally
            {
                shell?.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ProductionCloseDecisionKeepsThreeRecoveryActionsAndReleasesCompletedStatus()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorRuntimePath);
            Assert.That(prefab, Is.Not.Null, VisitorRuntimePath);

            var instance = UnityEngine.Object.Instantiate(prefab);
            var viewer = new GameObject("CloseDecisionTestViewer");
            var camera = viewer.AddComponent<Camera>();
            camera.enabled = false;
            var eventSystemObject = new GameObject("CloseDecisionTestEventSystem");
            var eventSystem = eventSystemObject.AddComponent<EventSystem>();
            var controller = instance.GetComponentInChildren<StartupRecallPresenter>(true);
            var shell = instance.GetComponentInChildren<GlobalFrontendShell>(true);
            var reticle = instance.GetComponentInChildren<GazeReticlePresenter>(true);
            var input = instance.GetComponentInChildren<HeadGazeDwellController>(true);

            try
            {
                Assert.That(controller, Is.Not.Null);
                Assert.That(shell, Is.Not.Null);
                Assert.That(reticle, Is.Not.Null);

                var requests = 0;
                var skipRequests = 0;
                var visibilityEvents = 0;
                var lastVisibility = false;
                controller.ObservationCompletionRequested += () => requests++;
                controller.SkipQuizAndCollectRequested += () => skipRequests++;
                controller.CloseDecisionVisibilityChanged += visible =>
                {
                    visibilityEvents++;
                    lastVisibility = visible;
                };
                controller.SetClosedContentContext("当前为自由浏览；主线仍为：测试点位。");
                controller.SetVisitorProgressSummary(new VisitorProgressSummary(0, 2, 0, 6));
                input.Configure(camera, eventSystem, reticle);
                controller.Configure(
                    viewer.transform,
                    new ClosedRecallController(),
                    input,
                    "请扫描二维码");

                var serialized = new SerializedObject(controller);
                var popup = RequiredObject<GameObject>(serialized, "_startupRoot");
                var promptText = RequiredObject<TMP_Text>(serialized, "_startupText");
                var background = RequiredObject<RectTransform>(serialized, "_startupBackground")
                    .GetComponent<Image>();
                var recall = RequiredObject<Button>(serialized, "_recallButton");
                var knowledge = RequiredObject<Button>(serialized, "_nextStationButton");
                var skip = RequiredObject<Button>(serialized, "_skipPointButton");
                var actions = new[] { recall, knowledge, skip };
                Assert.That(
                    popup.GetComponentsInChildren<Button>(true),
                    Has.Length.EqualTo(actions.Length),
                    "CloseDecision must contain only recall, quiz and skip-and-collect, with no action that hides all recovery choices.");

                Canvas.ForceUpdateCanvases();
                Assert.That(popup.activeInHierarchy, Is.True);
                Assert.That(controller.IsCloseDecisionVisible, Is.True);
                Assert.That(promptText.text, Does.Contain("自由浏览"));
                Assert.That(promptText.text, Does.Contain("测试点位"));
                Assert.That(promptText.text, Does.Contain("已观察 0/2"));
                Assert.That(promptText.text, Does.Contain("自由发现 0/6"));
                Assert.That(visibilityEvents, Is.EqualTo(1));
                Assert.That(lastVisibility, Is.True);
                Assert.That(background, Is.Not.Null);
                Assert.That(background.color.r, Is.EqualTo(0.025f).Within(0.001f));
                Assert.That(background.color.g, Is.EqualTo(0.075f).Within(0.001f));
                Assert.That(background.color.b, Is.EqualTo(0.055f).Within(0.001f));
                Assert.That(background.color.a, Is.EqualTo(0.86f).Within(0.001f),
                    "CloseDecision must preserve the pre-regression authored panel appearance.");
                var activeButtons = popup.GetComponentsInChildren<Button>(false);
                Assert.That(activeButtons, Has.Length.EqualTo(actions.Length),
                    "Only the three local CloseDecision actions may become active.");
                foreach (var action in actions)
                {
                    Assert.That(
                        action != null && action.gameObject.activeInHierarchy && action.interactable,
                        Is.True,
                        $"{action?.name ?? "Missing action"} must be visible and gaze-eligible.");
                    var surface = action.targetGraphic as Image;
                    Assert.That(surface, Is.Not.Null, $"{action.name} requires a visible target surface.");
                    Assert.That(action.transform.Find("FocusAura"), Is.Not.Null,
                        $"{action.name} requires authored gaze focus feedback.");
                    Assert.That(action.transform.Find("GazeProgress")?.Find("Fill"), Is.Not.Null,
                        $"{action.name} requires authored dwell progress feedback.");
                    var maximum = Mathf.Max(surface.color.r, Mathf.Max(surface.color.g, surface.color.b));
                    var minimum = Mathf.Min(surface.color.r, Mathf.Min(surface.color.g, surface.color.b));
                    Assert.That(maximum - minimum, Is.LessThanOrEqualTo(0.025f),
                        $"{action.name} must use the same neutral action core as the current visitor UI.");
                    var label = action.GetComponentInChildren<TMP_Text>(true);
                    Assert.That(label, Is.Not.Null);
                    label.ForceMeshUpdate(true, true);
                    Assert.That(label.rectTransform.rect.height,
                        Is.GreaterThanOrEqualTo(label.preferredHeight - 1f),
                        $"{action.name} must grow its internal label box for wrapped or two-line Chinese copy.");
                    var actionLocalRect = (RectTransform)action.transform;
                    var labelRect = RectInLocalSpace(label.rectTransform, actionLocalRect);
                    Assert.That(actionLocalRect.rect.Contains(labelRect.min), Is.True,
                        $"{action.name} label begins outside its hit target.");
                    Assert.That(actionLocalRect.rect.Contains(labelRect.max), Is.True,
                        $"{action.name} label ends outside its hit target.");
                    if ((label.text ?? string.Empty).IndexOf('\n') >= 0)
                    {
                        Assert.That(actionLocalRect.rect.height, Is.GreaterThanOrEqualTo(60f));
                        Assert.That(label.lineSpacing, Is.GreaterThanOrEqualTo(4f),
                            $"{action.name} two-line copy needs explicit breathing room between baselines.");
                    }
                }

                var popupRect = (RectTransform)popup.transform;
                var promptRect = RectInLocalSpace(promptText.rectTransform, popupRect);
                Assert.That(
                    promptText.rectTransform.rect.height,
                    Is.GreaterThanOrEqualTo(promptText.preferredHeight - 1f),
                    "CloseDecision must grow its text box instead of drawing overflow across the actions.");
                for (var first = 0; first < actions.Length; first++)
                {
                    var actionRect = RectInLocalSpace((RectTransform)actions[first].transform, popupRect);
                    Assert.That(promptRect.Overlaps(actionRect), Is.False,
                        $"Prompt text overlaps {actions[first].name}: {promptRect} / {actionRect}");
                    Assert.That(AxisAlignedGap(promptRect, actionRect), Is.GreaterThanOrEqualTo(8f),
                        $"Prompt text is too close to {actions[first].name} for legible Chinese wrapping.");
                }
                for (var first = 0; first < actions.Length; first++)
                for (var second = first + 1; second < actions.Length; second++)
                {
                    var firstRect = RectInLocalSpace((RectTransform)actions[first].transform, popupRect);
                    var secondRect = RectInLocalSpace((RectTransform)actions[second].transform, popupRect);
                    Assert.That(firstRect.Overlaps(secondRect), Is.False,
                        $"{actions[first].name} overlaps {actions[second].name}: {firstRect} / {secondRect}");
                    Assert.That(AxisAlignedGap(firstRect, secondRect), Is.GreaterThanOrEqualTo(8f),
                        $"{actions[first].name} is too close to {actions[second].name} for reliable gaze selection.");
                }

                Assert.That(
                    Mathf.Abs(((RectTransform)knowledge.transform).anchoredPosition.y),
                    Is.LessThanOrEqualTo(12f),
                    "The knowledge action should remain near the viewer's central gaze, not behind another control.");
                var skipLabel = skip.GetComponentInChildren<TMP_Text>(true);
                Assert.That(skipLabel, Is.Not.Null);
                Assert.That(skipLabel.text, Does.Contain("跳过问答并收藏"));
                Assert.That(skipLabel.text, Does.Contain("直接收下本次发现"),
                    "The quiz-skip action must explain direct collection without promising off-route story progression.");
                knowledge.onClick.Invoke();
                Assert.That(requests, Is.EqualTo(1),
                    "The production knowledge action must emit the explicit quiz request.");
                skip.onClick.Invoke();
                Assert.That(skipRequests, Is.EqualTo(1),
                    "The production quiz-skip action must emit direct-collection intent only.");

                controller.SetVisitorProgressSummary(new VisitorProgressSummary(2, 2, 6, 6));
                controller.ShowJourneyStatus("测试点位");
                Canvas.ForceUpdateCanvases();
                Assert.That(promptText.text, Does.Contain("全部完成"));
                Assert.That(promptText.text, Does.Contain("图鉴"));
                Assert.That(recall.gameObject.activeSelf, Is.False);
                Assert.That(knowledge.gameObject.activeSelf, Is.False);
                Assert.That(skip.gameObject.activeSelf, Is.False);
                Assert.That(popup.GetComponentsInChildren<Button>(false), Is.Empty);
                Assert.That(controller.IsCloseDecisionVisible, Is.False,
                    "A completed journey status must release the modal gate so exploration and the atlas remain available.");
                controller.ClearJourneyPrompt();
                Assert.That(controller.IsCloseDecisionVisible, Is.True);

                controller.SetApplicationSurfaceSuppressed(true);
                Assert.That(controller.IsCloseDecisionVisible, Is.False,
                    "A higher-priority modal must hide the close decision from the Coach arbiter.");
                Assert.That(visibilityEvents, Is.EqualTo(4));
                Assert.That(lastVisibility, Is.False);

                controller.SetApplicationSurfaceSuppressed(false);
                Assert.That(controller.IsCloseDecisionVisible, Is.True,
                    "The close decision must report visibility again after modal suppression ends.");
                Assert.That(visibilityEvents, Is.EqualTo(5));
                Assert.That(lastVisibility, Is.True);
            }
            finally
            {
                controller?.Unconfigure();
                input?.Unconfigure();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
                UnityEngine.Object.DestroyImmediate(eventSystemObject);
            }
        }

        [Test]
        public void ProductionVisitorHasNoPersistentCollectionQuickAccessSurface()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorRuntimePath);
            Assert.That(prefab, Is.Not.Null, VisitorRuntimePath);

            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            Assert.That(
                Array.Exists(transforms, item => item != null && item.name == "CollectionQuickAccess"),
                Is.False,
                "Collection browse access must stay hidden until the deliberate palm gesture is performed.");
        }
        static Rect RectInLocalSpace(RectTransform target, RectTransform root)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            var first = root.InverseTransformPoint(corners[0]);
            var minX = first.x;
            var minY = first.y;
            var maxX = first.x;
            var maxY = first.y;
            for (var index = 1; index < corners.Length; index++)
            {
                var point = root.InverseTransformPoint(corners[index]);
                minX = Mathf.Min(minX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxX = Mathf.Max(maxX, point.x);
                maxY = Mathf.Max(maxY, point.y);
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        static float AxisAlignedGap(Rect first, Rect second)
        {
            var horizontal = Mathf.Max(0f, Mathf.Max(first.xMin - second.xMax, second.xMin - first.xMax));
            var vertical = Mathf.Max(0f, Mathf.Max(first.yMin - second.yMax, second.yMin - first.yMax));
            return Mathf.Max(horizontal, vertical);
        }

        static T RequiredObject<T>(SerializedObject serialized, string propertyName)
            where T : UnityEngine.Object
        {
            var property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            var value = property.objectReferenceValue as T;
            Assert.That(value, Is.Not.Null, propertyName);
            return value;
        }

        sealed class ClosedRecallController : IRecallController
        {
            public RecallResult Recall() => RecallResult.Success;

            public IDisposable Observe(IRecallStateSink sink)
            {
                sink.OnStateChanged(new RecallState(
                    version: 1,
                    isClosed: true,
                    canRecall: true,
                    hasPreviousContent: true));
                return EmptyDisposable.Instance;
            }
        }

        sealed class SilentFlow : IExperienceFlow
        {
            public FlowPrepareResult Prepare(SessionToken candidate, SceneId sceneId)
                => FlowPrepareResult.Failure(new UserFault("Unused test flow."));
            public FlowResult EnterFeature(SessionToken session, FeaturePageId page)
                => FlowResult.Reject(FlowFailure.InvalidTransition);
            public FlowResult BackToMain(SessionToken session)
                => FlowResult.Reject(FlowFailure.InvalidTransition);
            public FlowResult Close(SessionToken session)
                => FlowResult.Reject(FlowFailure.InvalidTransition);
            public IDisposable Observe(IFlowStateSink sink) => EmptyDisposable.Instance;
        }

        sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();
            public void Dispose() { }
        }
    }
}
