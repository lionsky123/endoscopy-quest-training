using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.VisitorCoach.Contracts;
using BotanicalGardenQR.VisitorCoach.Frontend;
using NUnit.Framework;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.VisitorCoach.Tests.EditMode
{
    public sealed class VisitorCoachThemeAndPresenterTests
    {
        const string ThemePath =
            "Assets/BotanicalGardenQR/Content/Authoring/VisitorCoachTheme.asset";
        const string UiDefaultsPath =
            "Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset";

        static readonly string[] ProductionCueKeys =
        {
            VisitorCoachCueKeys.QrConfirm,
            VisitorCoachCueKeys.ArtifactGrab,
            VisitorCoachCueKeys.ArtifactPlace,
            VisitorCoachCueKeys.ArtifactPlaceRetry,
            VisitorCoachCueKeys.PalmRecall,
            VisitorCoachCueKeys.PanoramaEntry
        };

        [Test]
        public void GuidanceShowsWorldFixedFailureEscapeAndExplicitNextLegAction()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(ThemePath);
            var defaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(UiDefaultsPath);
            var viewer = new GameObject("TravelViewer"); viewer.AddComponent<Camera>();
            viewer.transform.SetPositionAndRotation(new Vector3(2, 1.65f, 3), Quaternion.Euler(15, 70, 8));
            var instance = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
            var presenter = instance.GetComponentInChildren<VisitorCoachPresenter>(true);
            try
            {
                presenter.Configure(viewer.transform, theme, defaults.SharedFont, new DialogueGazeRegistry());
                VisitorDialogueIntentKind? selected = null;
                presenter.IntentRequested += intent => selected = intent.Kind;
                presenter.PresentGuidance(new VisitorGuidanceSurface(VisitorGuidanceSurfaceKind.Unavailable, ""));
                var expectedPose = WorldSurfacePlacement.CreateViewerReadingPose(viewer.transform,
                    theme.ViewerDistance, theme.DialogueVerticalOffset, theme.DialogueReadingTiltDegrees);
                Assert.That(Vector3.Distance(presenter.transform.position, expectedPose.position), Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(presenter.transform.rotation, expectedPose.rotation), Is.LessThan(.01f));
                Assert.That(Vector3.Distance(presenter.transform.position, viewer.transform.position), Is.LessThan(.7f));
                Assert.That(viewer.transform.position.y - presenter.transform.position.y, Is.InRange(.12f, .22f));
                Assert.That(presenter.SubmitInput(VisitorDialogueIntentKind.Advance, VisitorDialogueInputMode.HeadGaze, float.MaxValue), Is.False);
                Assert.That(presenter.BodyFits, Is.True);
                Assert.That(presenter.CurrentState.AllowRestart, Is.False);
                Assert.That(presenter.CurrentState.AllowDefer, Is.False);
                Assert.That(presenter.ConfirmForTest(Time.unscaledTime + 1f), Is.True);
                Assert.That(selected, Is.EqualTo(VisitorDialogueIntentKind.Dismiss));
                var fixedPosition = presenter.transform.position;
                viewer.transform.position += Vector3.right; presenter.Tick(.02f);
                Assert.That(presenter.transform.position, Is.EqualTo(fixedPosition));
                presenter.PresentGuidance(new VisitorGuidanceSurface(VisitorGuidanceSurfaceKind.Hidden, ""));
                Assert.That(presenter.CurrentState, Is.Null);
                Assert.That(presenter.ReplayForTest(Time.unscaledTime + 2f), Is.False);
                presenter.PresentGuidance(new VisitorGuidanceSurface(VisitorGuidanceSurfaceKind.Departure, "P02"));
                Assert.That(presenter.BodyFits, Is.True);
                Assert.That(presenter.CurrentState.Body, Does.Contain("P02"));
                Assert.That(presenter.CurrentState.PrimaryActionLabel, Is.EqualTo("前往下一站"));
                Assert.That(presenter.ConfirmForTest(Time.unscaledTime + 3f), Is.True);
                Assert.That(selected, Is.EqualTo(VisitorDialogueIntentKind.Advance));
                presenter.PresentGuidance(new VisitorGuidanceSurface(VisitorGuidanceSurfaceKind.Discovery, "P02"));
                Assert.That(presenter.BodyFits, Is.True);
                Assert.That(presenter.CurrentState.AllowRestart, Is.False);
                Assert.That(presenter.CurrentState.AllowDefer, Is.False);
                Assert.That(presenter.CurrentState.SecondaryActionLabel, Is.Empty);
                Assert.That(presenter.ReplayForTest(Time.unscaledTime + 4f), Is.False);
                Assert.That(selected, Is.EqualTo(VisitorDialogueIntentKind.Advance), "Hidden secondary actions must not dismiss discovery.");
                presenter.PresentGuidance(new VisitorGuidanceSurface(VisitorGuidanceSurfaceKind.DiscoveryFailed, "P02"));
                Assert.That(presenter.CurrentState.PrimaryActionLabel, Is.EqualTo("重试"));
                Assert.That(presenter.CurrentState.AllowRestart, Is.False);
                Assert.That(presenter.CurrentState.AllowDefer, Is.False);
                Assert.That(presenter.ConfirmForTest(Time.unscaledTime + 5f), Is.True);
                Assert.That(selected, Is.EqualTo(VisitorDialogueIntentKind.Advance));
            }
            finally { presenter.Dispose(); UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(viewer); }
        }

        [Test]
        public void ThemeOwnsCompleteTextPagesAndOnlyOptionalEmptyAudioSlots()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(ThemePath);
            Assert.That(theme, Is.Not.Null, ThemePath);
            Assert.That(theme.IsValid(out var error), Is.True, error);
            Assert.That(theme.PresentationPrefab, Is.Not.Null);
            Assert.That(theme.Timing.DirectSeconds, Is.EqualTo(4f));
            Assert.That(theme.Timing.DemonstrationSeconds, Is.EqualTo(8f));
            Assert.That(theme.Timing.RecoverySeconds, Is.EqualTo(12f));
            Assert.That(theme.FairySpeakerName, Is.EqualTo("小精灵"));

            foreach (var cueKey in ProductionCueKeys)
            {
                var pageCount = theme.GetDialoguePageCount(cueKey);
                Assert.That(pageCount, Is.InRange(1, 3), cueKey);
                for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    Assert.That(theme.TryResolveDialoguePage(cueKey, pageIndex, out var page), Is.True,
                        $"{cueKey} page {pageIndex + 1}");
                    Assert.That(page, Is.Not.Empty);
                    Assert.That(page, Does.Not.Contain("…"),
                        "Authored pages must fit instead of relying on an ellipsis.");
                    Assert.That(theme.TryGetOptionalDialogueAudio(cueKey, pageIndex, out _), Is.False,
                        "This phase reserves page-audio slots but does not publish tutorial clips.");
                }
            }

            Assert.That(theme.TryResolveGlobalCopy(
                VisitorCoachCueKeys.QrConfirm,
                VisitorCoachHintLevel.Recovery,
                out var qrRecovery), Is.True);
            Assert.That(qrRecovery, Does.Contain("路线到站"));
            Assert.That(qrRecovery, Does.Contain("轻触"));
            Assert.That(theme.TryResolveGlobalCopy(
                VisitorCoachCueKeys.ArtifactGrab,
                VisitorCoachHintLevel.Initial,
                out _), Is.False,
                "Feature-local action hints remain on their owning surface.");
        }

        [Test]
        public void DialogueStageUsesReadableModalQuestGeometry()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(ThemePath);
            Assert.That(theme, Is.Not.Null, ThemePath);

            var worldWidth = theme.PanelPixels.x * theme.CanvasScale;
            var worldHeight = theme.PanelPixels.y * theme.CanvasScale;
            Assert.That(theme.PanelColor.a, Is.GreaterThanOrEqualTo(0.98f));
            Assert.That(worldWidth, Is.InRange(0.5f, 0.65f));
            Assert.That(worldHeight, Is.InRange(0.2f, 0.28f));
            Assert.That(theme.ViewerDistance, Is.InRange(.45f, .55f));
            Assert.That(theme.DialogueVerticalOffset, Is.InRange(-0.22f, -.12f));
            Assert.That(theme.FairyDialogueHorizontalOffset.x, Is.InRange(-0.42f, -0.28f));
            Assert.That(theme.FairyDialogueHorizontalOffset.y, Is.GreaterThanOrEqualTo(0f));
            Assert.That(theme.DialogueFontSize, Is.GreaterThanOrEqualTo(28f));
            Assert.That(theme.ConfirmDebounceSeconds, Is.InRange(0.1f, 0.3f));
            Assert.That(theme.SpeakerColor, Is.Not.EqualTo(theme.TextColor));
            Assert.That(theme.PanelColor.r, Is.GreaterThan(.85f), "Dialogue surfaces use a light, low-glare base.");
            Assert.That(theme.TextColor.r, Is.LessThan(.2f), "Dialogue body copy remains dark and readable.");
            Assert.That(Contrast(theme.AccentColor, theme.PanelColor), Is.GreaterThanOrEqualTo(4.5f),
                "The teal accent remains readable on the light dialogue surface.");
        }

        static float Contrast(Color foreground, Color background)
        {
            var a = Luminance(foreground);
            var b = Luminance(background);
            return (Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f);
        }

        static float Luminance(Color color)
        {
            static float Linear(float value) => value <= .04045f ? value / 12.92f : Mathf.Pow((value + .055f) / 1.055f, 2.4f);
            return .2126f * Linear(color.r) + .7152f * Linear(color.g) + .0722f * Linear(color.b);
        }

        [Test]
        public void EveryProductionDialoguePageFitsTheAuthoredBodyRegionWithoutEllipsis()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(ThemePath);
            var prologueTheme = AssetDatabase.LoadAssetAtPath<VisitorPrologueThemeAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/VisitorPrologueTheme.asset");
            var uiDefaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(UiDefaultsPath);
            var viewer = new GameObject("DialogueFitViewer");
            var instance = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
            var presenter = instance.GetComponentInChildren<VisitorCoachPresenter>(true);
            try
            {
                viewer.AddComponent<Camera>();
                presenter.Configure(viewer.transform, theme, uiDefaults.SharedFont, new DialogueGazeRegistry());
                presenter.SetInputMode(VisitorDialogueInputMode.HeadGaze);
                var revision = 1L;
                foreach (var cueKey in ProductionCueKeys)
                {
                    var count = theme.GetDialoguePageCount(cueKey);
                    for (var pageIndex = 0; pageIndex < count; pageIndex++)
                    {
                        Assert.That(theme.TryResolveDialoguePage(cueKey, pageIndex, out var body), Is.True);
                        Assert.DoesNotThrow(() => presenter.Present(new VisitorDialogueSurfaceState(
                            revision++,
                            new VisitorDialogueContextId($"fit:{cueKey}:{pageIndex}"),
                            VisitorDialogueOwner.Coach,
                            VisitorDialogueSurfaceMode.Dialogue,
                            "探索教学",
                            theme.FairySpeakerName,
                            body,
                            pageIndex,
                            count)));
                        Assert.That(presenter.BodyFits, Is.True, $"{cueKey} page {pageIndex + 1}");
                    }
                }

                AssertPagesFit(
                    presenter,
                    prologueTheme.Copy.EncounterPageCount,
                    prologueTheme.Copy.TryGetEncounterPage,
                    "prologue:awakening",
                    ref revision);
                foreach (var response in new[] { prologueTheme.Copy.CuriousResponse, prologueTheme.Copy.CompanionResponse })
                {
                    presenter.Present(new VisitorDialogueSurfaceState(revision++, new VisitorDialogueContextId("reply-fit"),
                        VisitorDialogueOwner.Prologue, VisitorDialogueSurfaceMode.Dialogue, "见闻之约", theme.FairySpeakerName,
                        response, 2, 4));
                    Assert.That(presenter.BodyFits, Is.True);
                }
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void DialogueLayoutFitsCopyAndOnlyShowsPageCountForMultiPageText()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(ThemePath);
            var uiDefaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(UiDefaultsPath);
            var viewer = new GameObject("DialogueLayoutViewer", typeof(Camera));
            var noImageViewer = new GameObject("DialogueNoImageViewer", typeof(Camera));
            var instance = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
            var noImageTheme = UnityEngine.Object.Instantiate(theme);
            GameObject noImageInstance = null;
            var presenter = instance.GetComponentInChildren<VisitorCoachPresenter>(true);
            VisitorCoachPresenter noImagePresenter = null;
            try
            {
                presenter.Configure(viewer.transform, theme, uiDefaults.SharedFont, new DialogueGazeRegistry());
                presenter.SetInputMode(VisitorDialogueInputMode.HandPoke);
                var stage = instance.transform.Find("DialogueStage");
                var body = stage.Find("DialogueBody").GetComponent<RectTransform>();
                var stageRect = stage.GetComponent<RectTransform>();
                var pageCounter = stage.Find("PageCounter").gameObject;
                var continueButton = (RectTransform)stage.Find("Continue");
                var continueHitVolume = continueButton.GetComponent<BoxCollider>();
                var continueSurface = continueButton.GetComponent<BoundsClipper>();
                var context = new VisitorDialogueContextId("dialogue:compact-layout");

                presenter.Present(new VisitorDialogueSurfaceState(
                    1, context, VisitorDialogueOwner.Guidance, VisitorDialogueSurfaceMode.Dialogue,
                    "欢迎", "安小卫", "开始学习。", 0, 1,
                    primaryActionLabel: "开始学习", allowDefer: false, allowRestart: false));

                var continueLabel = continueButton.GetComponentInChildren<TMP_Text>();
                Assert.That(continueLabel.color, Is.EqualTo(Color.white),
                    "The main action uses white text on the dark accent surface.");
                var continueGraphic = continueButton.GetComponentInChildren<DialogueContractGraphic>();
                Assert.That(continueGraphic, Is.Not.Null);
                var populateMesh = typeof(DialogueContractGraphic).GetMethod("OnPopulateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null);
                Assert.That(populateMesh, Is.Not.Null);
                var vertices = new List<UIVertex>();
                using (var vertexHelper = new VertexHelper())
                {
                    populateMesh.Invoke(continueGraphic, new object[] { vertexHelper });
                    vertexHelper.GetUIVertexStream(vertices);
                }
                Assert.That(vertices.Count, Is.GreaterThanOrEqualTo(4));
                Assert.That(vertices.Take(4).All(vertex => ((Color)vertex.color).grayscale < .35f), Is.True,
                    "The main action fill remains a dark, readable surface.");

                var shortBodyHeight = body.rect.height;
                var shortStageHeight = stageRect.rect.height;
                Assert.That(shortBodyHeight, Is.LessThan(100f), "Short copy should not reserve the old fixed paragraph block.");
                Assert.That(pageCounter.activeSelf, Is.False, "A single page should not look pageable.");
                Assert.That(presenter.DisplayedPage, Is.Empty);
                Assert.That(presenter.BodyFits, Is.True);
                Assert.That(continueHitVolume.size.x, Is.EqualTo(continueButton.rect.width).Within(.1f));
                Assert.That(continueHitVolume.size.y, Is.EqualTo(continueButton.rect.height).Within(.1f));
                Assert.That(continueSurface.Size.x, Is.EqualTo(continueButton.rect.width).Within(.1f));
                Assert.That(continueSurface.Size.y, Is.EqualTo(continueButton.rect.height).Within(.1f));

                const string longChineseCopy = "先查看本室项目，再按提示观察和操作。\n不确定时可以暂留，之后再回办公室修改。";
                presenter.Present(new VisitorDialogueSurfaceState(
                    2, context, VisitorDialogueOwner.Guidance, VisitorDialogueSurfaceMode.Dialogue,
                    "储存库", "安小卫", longChineseCopy, 0, 2,
                    primaryActionLabel: "继续", allowDefer: false, allowRestart: false));

                Assert.That(body.rect.height, Is.GreaterThan(shortBodyHeight), "Longer Chinese copy should gain readable height instead of shrinking the font.");
                Assert.That(pageCounter.activeSelf, Is.True);
                Assert.That(presenter.DisplayedPage, Is.EqualTo("1/2"));
                Assert.That(presenter.BodyFits, Is.True);
                var bodyBottom = body.anchoredPosition.y - body.rect.height * .5f;
                var buttonTop = continueButton.anchoredPosition.y + continueButton.rect.height * .5f;
                Assert.That(bodyBottom - buttonTop, Is.GreaterThanOrEqualTo(12f), "Primary action should stay below the full body.");

                var themeCopy = new SerializedObject(noImageTheme);
                themeCopy.FindProperty("_welcomePortrait").objectReferenceValue = null;
                themeCopy.FindProperty("_listeningPortrait").objectReferenceValue = null;
                themeCopy.FindProperty("_wonderPortrait").objectReferenceValue = null;
                themeCopy.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(noImageTheme.IsValid(out var themeError), Is.True, themeError);

                noImageInstance = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
                noImagePresenter = noImageInstance.GetComponentInChildren<VisitorCoachPresenter>(true);
                noImagePresenter.Configure(noImageViewer.transform, noImageTheme, uiDefaults.SharedFont, new DialogueGazeRegistry());
                noImagePresenter.SetInputMode(VisitorDialogueInputMode.HandPoke);
                var noImageStage = noImageInstance.transform.Find("DialogueStage");
                var noImageBody = noImageStage.Find("DialogueBody").GetComponent<RectTransform>();
                var noImageButton = (RectTransform)noImageStage.Find("Continue");
                var noImagePortrait = noImageInstance.transform.Find("SpeakerPortrait");
                noImagePresenter.Present(new VisitorDialogueSurfaceState(
                    1, new VisitorDialogueContextId("dialogue:no-portrait-layout"), VisitorDialogueOwner.Guidance,
                    VisitorDialogueSurfaceMode.Dialogue, "欢迎", "安小卫", "开始学习。", 0, 1,
                    primaryActionLabel: "开始学习", allowDefer: false, allowRestart: false));

                Assert.That(noImagePortrait.gameObject.activeSelf, Is.False, "Missing portrait assets should not leave a blank portrait box.");
                Assert.That(noImageBody.rect.width, Is.EqualTo(760f).Within(.1f));
                Assert.That(noImageBody.anchoredPosition.x, Is.EqualTo(0f).Within(.1f), "Copy should use the full centered surface when the portrait is absent.");
                Assert.That(noImageButton.rect.width, Is.EqualTo(noImageBody.rect.width).Within(.1f));
                Assert.That(noImageStage.GetComponent<RectTransform>().rect.height, Is.LessThan(shortStageHeight),
                    "The no-portrait layout should collapse the empty portrait column and use a shorter surface.");
                Assert.That(noImagePresenter.BodyFits, Is.True);
            }
            finally
            {
                presenter.Dispose();
                if (noImagePresenter != null) noImagePresenter.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                if (noImageInstance != null) UnityEngine.Object.DestroyImmediate(noImageInstance);
                UnityEngine.Object.DestroyImmediate(noImageTheme);
                UnityEngine.Object.DestroyImmediate(viewer);
                UnityEngine.Object.DestroyImmediate(noImageViewer);
            }
        }

        [Test]
        public void WelcomePokeFeedbackSeparatesApproachPressTriggerAndReleaseWithoutMovingHitArea()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(ThemePath);
            var defaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(UiDefaultsPath);
            var viewer = new GameObject("WelcomeButtonStateViewer", typeof(Camera));
            var instance = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
            var presenter = instance.GetComponentInChildren<VisitorCoachPresenter>(true);
            try
            {
                presenter.Configure(viewer.transform, theme, defaults.SharedFont, new DialogueGazeRegistry());
                presenter.SetInputMode(VisitorDialogueInputMode.HandPoke);
                presenter.Present(new VisitorDialogueSurfaceState(1,
                    new VisitorDialogueContextId("welcome:button-states"), VisitorDialogueOwner.Guidance,
                    VisitorDialogueSurfaceMode.Dialogue, "欢迎", "安小卫", "欢迎来到内镜中心监督检查。", 0, 1,
                    primaryActionLabel: "开始学习", allowDefer: false, allowRestart: false));

                var target = instance.transform.Find("DialogueStage/Continue")
                    .GetComponent<VisitorDialoguePointableTarget>();
                var rect = (RectTransform)target.transform;
                var startingPosition = rect.position;
                var startingRect = rect.rect;
                var selected = 0;
                target.Selected += () => selected++;
                Assert.That(ButtonVisualStateName(target), Is.EqualTo("Resting"));

                target.PresentTrackedFingerHover(true);
                Assert.That(ButtonVisualStateName(target), Is.EqualTo("Approaching"));
                target.BindTrackedHands(new Oculus.Interaction.Input.IHand[1]);
                var front = rect.position - rect.forward * .03f;
                var press = rect.position - rect.forward * .003f;
                var through = rect.position + rect.forward * .005f;
                target.SampleTrackedFinger(0, front);
                Assert.That(ButtonVisualStateName(target), Is.EqualTo("Approaching"));
                target.SampleTrackedFinger(0, press);
                Assert.That(ButtonVisualStateName(target), Is.EqualTo("Pressed"));
                target.SampleTrackedFinger(0, through);
                Assert.That(ButtonVisualStateName(target), Is.EqualTo("Triggered"));
                target.SampleTrackedFinger(0, through);
                Assert.That(selected, Is.EqualTo(1), "One poke must commit only once while the finger remains through the surface.");

                target.SampleTrackedFinger(0, front);
                Assert.That(ButtonVisualStateName(target), Is.EqualTo("Approaching"));
                target.SampleTrackedFinger(0, press);
                target.SampleTrackedFinger(0, through);
                Assert.That(selected, Is.EqualTo(2), "Withdrawing to the front must rearm a new poke.");
                target.SampleTrackedFinger(0, rect.position + rect.forward * .11f);
                Assert.That(ButtonVisualStateName(target), Is.EqualTo("Resting"));
                Assert.That(rect.position, Is.EqualTo(startingPosition), "Visual press feedback must not move the interaction target.");
                Assert.That(rect.rect, Is.EqualTo(startingRect), "Visual press feedback must not resize the interaction target.");
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void DialoguePanelTiltsForReadingAndKeepsItsWorldPoseAfterOpening()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(ThemePath);
            var defaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(UiDefaultsPath);
            var viewer = new GameObject("TiltedDialogueViewer", typeof(Camera));
            var instance = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
            var presenter = instance.GetComponentInChildren<VisitorCoachPresenter>(true);
            try
            {
                presenter.Configure(viewer.transform, theme, defaults.SharedFont, new DialogueGazeRegistry());
                presenter.SetInputMode(VisitorDialogueInputMode.HandPoke);
                var context = new VisitorDialogueContextId("dialogue:reading-pose");
                presenter.Present(new VisitorDialogueSurfaceState(1, context, VisitorDialogueOwner.Guidance,
                    VisitorDialogueSurfaceMode.Dialogue, "欢迎", "安小卫", "先看这里。", 0, 1,
                    primaryActionLabel: "继续", allowDefer: false, allowRestart: false));

                var firstPose = presenter.CurrentComposition.StagePose;
                var frontNormal = firstPose.rotation * Vector3.back;
                Assert.That(frontNormal.y, Is.GreaterThan(.05f), "The dialogue should angle its reading face slightly upward.");
                var eyeFacing = Quaternion.LookRotation(firstPose.position - viewer.transform.position, Vector3.up);
                var expectedReadingRotation = eyeFacing * Quaternion.Euler(8f, 0f, 0f);
                Assert.That(Quaternion.Angle(firstPose.rotation, expectedReadingRotation), Is.LessThan(.01f),
                    "The upward reading tilt should come from the dialogue presentation profile.");

                viewer.transform.position += Vector3.right;
                viewer.transform.rotation = Quaternion.Euler(20f, 90f, 0f);
                presenter.Present(new VisitorDialogueSurfaceState(2, context, VisitorDialogueOwner.Guidance,
                    VisitorDialogueSurfaceMode.Dialogue, "欢迎", "安小卫", "继续阅读。", 0, 1,
                    primaryActionLabel: "继续", allowDefer: false, allowRestart: false));
                Assert.That(presenter.CurrentComposition.StagePose.position, Is.EqualTo(firstPose.position),
                    "Changing the viewer pose must not drag an already-open panel.");
                Assert.That(presenter.CurrentComposition.StagePose.rotation, Is.EqualTo(firstPose.rotation));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        static string ButtonVisualStateName(VisitorDialoguePointableTarget target)
        {
            var property = typeof(VisitorDialoguePointableTarget).GetProperty("VisualState",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, "The touch target should expose its current visual interaction state.");
            return property.GetValue(target)?.ToString();
        }

        [Test]
        public void WelcomeButtonUsesVisibleFeedbackAndAcceptsTrackedIndexFingerCrossing()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(ThemePath);
            var defaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(UiDefaultsPath);
            var viewer = new GameObject("WelcomeFingerViewer", typeof(Camera));
            var instance = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
            var presenter = instance.GetComponentInChildren<VisitorCoachPresenter>(true);
            try
            {
                presenter.Configure(viewer.transform, theme, defaults.SharedFont, new DialogueGazeRegistry());
                presenter.SetInputMode(VisitorDialogueInputMode.HandPoke);
                presenter.Present(new VisitorDialogueSurfaceState(1,
                    new VisitorDialogueContextId("welcome:finger"), VisitorDialogueOwner.Guidance,
                    VisitorDialogueSurfaceMode.Dialogue, "欢迎", "安小卫", "欢迎来到内镜中心监督检查。", 0, 1,
                    primaryActionLabel: "开始学习", allowDefer: false, allowRestart: false));

                var target = instance.transform.Find("DialogueStage/Continue")
                    .GetComponent<VisitorDialoguePointableTarget>();
                var button = target.GetComponent<UnityEngine.UI.Button>();
                var feedback = new SerializedObject(target).FindProperty("_feedbackGraphic").objectReferenceValue;
                Assert.That(feedback, Is.SameAs(button.targetGraphic));
                Assert.That(((UnityEngine.UI.Graphic)feedback).isActiveAndEnabled, Is.True);
                var visibleGraphic = (DialogueContractGraphic)feedback;
                target.PresentTrackedFingerHover(true);
                Assert.That(visibleGraphic.CurrentProgress, Is.GreaterThan(0f));
                target.PresentTrackedFingerHover(false);
                Assert.That(visibleGraphic.CurrentProgress, Is.Zero);

                target.BindTrackedHands(new Oculus.Interaction.Input.IHand[1]);
                var selected = 0;
                target.Selected += () => selected++;
                var front = target.transform.position - target.transform.forward * .03f;
                var through = target.transform.position + target.transform.forward * .005f;
                target.SampleTrackedFinger(0, front);
                target.SampleTrackedFinger(0, through);
                target.SampleTrackedFinger(0, through);
                Assert.That(selected, Is.EqualTo(1));
                target.SampleTrackedFinger(0, front);
                target.SampleTrackedFinger(0, through);
                Assert.That(selected, Is.EqualTo(2), "A new finger withdrawal and poke must rearm the button.");
                target.SampleTrackedFinger(0, front);
                typeof(VisitorDialoguePointableTarget)
                    .GetMethod("OnApplicationPause", System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic)
                    .Invoke(target, new object[] { true });
                target.SampleTrackedFinger(0, through);
                Assert.That(selected, Is.EqualTo(2), "A paused hand must approach from the front again.");
                target.SampleTrackedFinger(0, front);
                target.SampleTrackedFinger(0, through);
                Assert.That(selected, Is.EqualTo(3));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void PresenterShowsCompleteBodyAndOneConfirmationAdvancesThenSupportsReplay()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(ThemePath);
            var uiDefaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(UiDefaultsPath);
            var viewer = new GameObject("CoachPresenterViewer");
            GameObject instance = null;
            VisitorCoachPresenter presenter = null;
            try
            {
                viewer.AddComponent<Camera>();
                instance = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
                presenter = instance.GetComponentInChildren<VisitorCoachPresenter>(true);
                Assert.That(presenter, Is.Not.Null);
                presenter.Configure(viewer.transform, theme, uiDefaults.SharedFont, new DialogueGazeRegistry());
                presenter.SetInputMode(VisitorDialogueInputMode.HeadGaze);

                var context = new VisitorDialogueContextId("coach:test:palm");
                var intents = new System.Collections.Generic.List<VisitorDialogueIntent>();
                presenter.IntentRequested += intents.Add;
                Assert.That(theme.TryResolveDialoguePage(
                    VisitorCoachCueKeys.PalmRecall, 0, out var firstPage), Is.True);
                var pageCount = theme.GetDialoguePageCount(VisitorCoachCueKeys.PalmRecall);
                presenter.Present(new VisitorDialogueSurfaceState(
                    1,
                    context,
                    VisitorDialogueOwner.Coach,
                    VisitorDialogueSurfaceMode.Dialogue,
                    "图谱魔法",
                    theme.FairySpeakerName,
                    firstPage,
                    0,
                    pageCount,
                    "掌心朝上，保持稳定。"));

                Assert.That(presenter.IsDialogueVisible, Is.True);
                Assert.That(presenter.DisplayedBody, Is.EqualTo(firstPage));
                Assert.That(presenter.DisplayedPage, Is.EqualTo($"1/{pageCount}"));
                Assert.That(presenter.BodyFits, Is.True);
                Assert.That(presenter.VisibleCharacterCount, Is.GreaterThanOrEqualTo(presenter.BodyCharacterCount));

                presenter.Present(new VisitorDialogueSurfaceState(
                    2,
                    context,
                    VisitorDialogueOwner.Coach,
                    VisitorDialogueSurfaceMode.Dialogue,
                    "图谱魔法",
                    theme.FairySpeakerName,
                    firstPage,
                    0,
                    pageCount,
                    "升级后的动作提示"));
                Assert.That(presenter.VisibleCharacterCount, Is.GreaterThanOrEqualTo(presenter.BodyCharacterCount),
                    "A same-page Coach update preserves the complete body.");

                Assert.That(presenter.ConfirmForTest(float.MaxValue), Is.True);
                Assert.That(intents.Count, Is.EqualTo(1));
                Assert.That(intents[0].Kind, Is.EqualTo(VisitorDialogueIntentKind.Advance));

                presenter.Present(new VisitorDialogueSurfaceState(
                    3,
                    context,
                    VisitorDialogueOwner.Coach,
                    VisitorDialogueSurfaceMode.Replay,
                    "图谱魔法",
                    theme.FairySpeakerName,
                    string.Empty,
                    pageCount - 1,
                    pageCount,
                    "掌心朝上，保持稳定。"));
                Assert.That(presenter.IsDialogueVisible, Is.False);
                Assert.That(presenter.IsReplayVisible, Is.True);
                Assert.That(presenter.ReplayForTest(float.MaxValue), Is.True);
                Assert.That(intents.Count, Is.EqualTo(2));
                Assert.That(intents[1].Kind, Is.EqualTo(VisitorDialogueIntentKind.Replay));

                var targets = instance.GetComponentsInChildren<VisitorDialoguePointableTarget>(true);
                Assert.That(targets.Length, Is.EqualTo(3));
                foreach (var target in targets)
                {
                    Assert.That(target.GetComponents<Collider>(), Is.Not.Empty);
                    var serializedTarget = new SerializedObject(target);
                    Assert.That(
                        serializedTarget.FindProperty("_feedbackGraphic")?.objectReferenceValue,
                        Is.Not.Null,
                        target.gameObject.name);
                }
                foreach (var text in instance.GetComponentsInChildren<TMP_Text>(true))
                    Assert.That(text.overflowMode, Is.EqualTo(TextOverflowModes.Overflow), text.gameObject.name);
            }
            finally
            {
                presenter?.Dispose();
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        delegate bool PageResolver(int pageIndex, out string text);

        [Test]
        public void LogicalPageReplacementInvalidatesOneRegisteredStageAndDisposeReleasesIt()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(ThemePath);
            var defaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(UiDefaultsPath);
            var viewer = new GameObject("PageBoundaryViewer", typeof(Camera));
            var instance = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
            var presenter = instance.GetComponentInChildren<VisitorCoachPresenter>(true);
            var registry = new DialogueGazeRegistry();
            try
            {
                presenter.Configure(viewer.transform, theme, defaults.SharedFont, registry);
                presenter.SetInputMode(VisitorDialogueInputMode.HeadGaze);
                var context = new VisitorDialogueContextId("page:boundary");
                presenter.Present(new VisitorDialogueSurfaceState(1, context, VisitorDialogueOwner.Coach,
                    VisitorDialogueSurfaceMode.Dialogue, "说明", "向导", "第一页", 0, 2));
                var invalidations = registry.Invalidations;
                presenter.Present(new VisitorDialogueSurfaceState(2, context, VisitorDialogueOwner.Coach,
                    VisitorDialogueSurfaceMode.Dialogue, "说明", "向导", "第一页", 0, 2, "动作提示更新"));
                Assert.That(registry.Invalidations, Is.EqualTo(invalidations), "A copy-only action hint update preserves current reading input.");
                presenter.Present(new VisitorDialogueSurfaceState(3, context, VisitorDialogueOwner.Coach,
                    VisitorDialogueSurfaceMode.Dialogue, "说明", "向导", "第二页", 1, 2));
                Assert.That(registry.Invalidations, Is.GreaterThan(invalidations));
                Assert.That(registry.Registrations, Is.EqualTo(1));
                presenter.Dispose();
                presenter.Dispose();
                Assert.That(registry.Releases, Is.EqualTo(1));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [TestCase(VisitorDialogueInputMode.HeadGaze, VisitorDialogueInputMode.HandPoke)]
        [TestCase(VisitorDialogueInputMode.HandPoke, VisitorDialogueInputMode.HeadGaze)]
        public void DialogueRejectsTheOldInputChannelAfterPhaseSwitch(VisitorDialogueInputMode active, VisitorDialogueInputMode old)
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(ThemePath);
            var defaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(UiDefaultsPath);
            var viewer = new GameObject("InputModeViewer", typeof(Camera));
            var instance = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
            var presenter = instance.GetComponentInChildren<VisitorCoachPresenter>(true);
            try
            {
                presenter.Configure(viewer.transform, theme, defaults.SharedFont, new DialogueGazeRegistry());
                presenter.SetInputMode(old);
                presenter.Present(new VisitorDialogueSurfaceState(1, new VisitorDialogueContextId("mode:test"),
                    VisitorDialogueOwner.Prologue, VisitorDialogueSurfaceMode.Dialogue, "说明", "向导", "完整正文", 0, 2));
                presenter.SetInputMode(active);
                var intents = 0;
                presenter.IntentRequested += _ => intents++;
                Assert.That(presenter.SubmitInput(VisitorDialogueIntentKind.Advance, old, float.MaxValue), Is.False);
                Assert.That(intents, Is.Zero);
                Assert.That(presenter.SubmitInput(VisitorDialogueIntentKind.Advance, active, float.MaxValue), Is.True);
                Assert.That(intents, Is.EqualTo(1));
                foreach (var target in instance.GetComponentsInChildren<VisitorDialoguePointableTarget>(true))
                    foreach (var collider in target.GetComponents<Collider>())
                        Assert.That(collider.enabled && target.gameObject.activeInHierarchy,
                            Is.EqualTo(active == VisitorDialogueInputMode.HandPoke && target.gameObject.activeInHierarchy));
            }
            finally
            {
                presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        static void AssertPagesFit(
            VisitorCoachPresenter presenter,
            int pageCount,
            PageResolver resolve,
            string contextPrefix,
            ref long revision)
        {
            for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                Assert.That(resolve(pageIndex, out var body), Is.True);
                presenter.Present(new VisitorDialogueSurfaceState(
                    revision++,
                    new VisitorDialogueContextId($"{contextPrefix}:{pageIndex}"),
                    VisitorDialogueOwner.Prologue,
                    VisitorDialogueSurfaceMode.Dialogue,
                    "序章",
                    "小精灵",
                    body,
                    pageIndex,
                    pageCount));
                Assert.That(presenter.BodyFits, Is.True, $"{contextPrefix} page {pageIndex + 1}");
            }
        }

        sealed class DialogueGazeRegistry : BotanicalGardenQR.FrontendShell.Contracts.IFrontendGazeSurfaceRegistry
        {
            public int Registrations;
            public int Invalidations;
            public int Releases;
            public System.IDisposable SuspendPanelInput() => new Suspension();
            sealed class Suspension : System.IDisposable { public void Dispose() { } }
            public BotanicalGardenQR.FrontendShell.Contracts.IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform root, int priority, string label)
            { Registrations++; return new Registration(this); }
            sealed class Registration : BotanicalGardenQR.FrontendShell.Contracts.IFrontendGazeSurfaceRegistration
            {
                readonly DialogueGazeRegistry _owner;
                bool _disposed;
                public Registration(DialogueGazeRegistry owner) => _owner = owner;
                public bool IsFocused => false;
                public void Invalidate() { if (!_disposed) _owner.Invalidations++; }
                public void Dispose() { if (_disposed) return; _disposed = true; _owner.Releases++; }
            }
        }
    }
}
