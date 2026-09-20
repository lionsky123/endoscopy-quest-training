using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.Experience.Flow;
using BotanicalGardenQR.Fairy.Backend;
using BotanicalGardenQR.Fairy.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.VisitorCoach.Frontend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Fairy
{
    public sealed class FairyApplicationLifecycleTests
    {
        static readonly SceneId FirstScene = new SceneId("first_scene");
        static readonly SceneId SecondScene = new SceneId("second_scene");
        AudioClip _speechClip;
        AudioClip _coachAttentionClip;
        AudioClip _artifactWaitClip;
        AudioClip _artifactReturnClip;
        AudioClip _celebrateClip;
        AudioClip _idleLocomotionClip;
        GameObject _companionEffectPrefab;

        [TestCase(OVRPassthroughLayer.ColorMapEditorType.None)]
        [TestCase(OVRPassthroughLayer.ColorMapEditorType.ColorAdjustment)]
        [TestCase(OVRPassthroughLayer.ColorMapEditorType.Grayscale)]
        [TestCase(OVRPassthroughLayer.ColorMapEditorType.GrayscaleToColor)]
        public void ArrivalWorldGradeChangesRealityAndRestoresCapturedStyle(OVRPassthroughLayer.ColorMapEditorType mode)
        {
            var platform = new GameObject("OfflineGradeTest");
            platform.SetActive(false);
            try
            {
                var layer = platform.AddComponent<OVRPassthroughLayer>();
                var light = platform.AddComponent<Light>();
                light.intensity = 1.7f;
                if (mode == OVRPassthroughLayer.ColorMapEditorType.Grayscale || mode == OVRPassthroughLayer.ColorMapEditorType.GrayscaleToColor)
                    layer.SetColorMapControls(.12f, .08f, .15f, new Gradient(), mode);
                else
                {
                    layer.SetBrightnessContrastSaturation(.08f,.12f,.17f);
                    if (mode == OVRPassthroughLayer.ColorMapEditorType.None) layer.DisableColorMap();
                }
                var gradient = layer.colorMapEditorGradient;
                var visual = new FairyArrivalVisualState(layer, light);
                visual.Capture(); visual.SetWorldShift(1); visual.SetPressure(.4f);
                Assert.That(layer.colorMapEditorBrightness, Is.LessThan(-.15f), "The new scene state must change actual Passthrough, not only virtual light.");
                Assert.That(light.intensity, Is.LessThan(1.2f));
                visual.Restore();
                Assert.That(layer.colorMapEditorType, Is.EqualTo(mode));
                Assert.That(layer.colorMapEditorBrightness, Is.EqualTo(.08f).Within(.0001f));
                Assert.That(layer.colorMapEditorContrast, Is.EqualTo(.12f).Within(.0001f));
                Assert.That(layer.colorMapEditorGradient, Is.SameAs(gradient));
                Assert.That(light.intensity, Is.EqualTo(1.7f).Within(.0001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(platform); }
        }

        [Test]
        public void ExplicitRecallIsBoundedInTimeAndDoesNotAcceptMotionMidTransition()
        {
            var actor = new GameObject("RecallActor");
            var viewer = new GameObject("RecallViewer");
            var floor = new GameObject("RecallFloor");
            try
            {
                var driver = actor.AddComponent<FairyOrbitDriver>();
                driver.Initialize(viewer.transform, floor.transform, FairyBehavior.Guide);
                var start = actor.transform.position;
                Assert.That(driver.ApplyMotion(5, start, Vector3.forward, false, 1.5f, .7f), Is.True);
                var target = new Vector3(.3f, 0, .8f);
                Assert.That(driver.TryRecall(new Vector3(float.NaN, 0, 0)), Is.False);
                Assert.That(driver.TryRecall(target), Is.True);
                Assert.That(driver.TryRecall(Vector3.zero), Is.False);
                Assert.That(driver.ApplyMotion(5, start, Vector3.forward, false, 1.5f, .7f), Is.False);
                driver.Tick(.1f);
                Assert.That(actor.transform.position, Is.EqualTo(start));
                for (var i = 0; i < 3; i++) driver.Tick(.1f);
                Assert.That(actor.transform.position, Is.EqualTo(target));
                Assert.That(actor.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(driver.ApplyMotion(5, target, Vector3.forward, false, 1.5f, .7f), Is.True,
                    "The same navigation request can resume after recall.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(viewer);
                UnityEngine.Object.DestroyImmediate(floor);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _speechClip = AudioClip.Create("FairySpeechTestClip", 64, 1, 44100, false);
            _coachAttentionClip = AudioClip.Create("CoachAttentionTestClip", 64, 1, 44100, false);
            _artifactWaitClip = AudioClip.Create("ArtifactWaitTestClip", 64, 1, 44100, false);
            _artifactReturnClip = AudioClip.Create("ArtifactReturnTestClip", 64, 1, 44100, false);
            _celebrateClip = AudioClip.Create("CelebrateTestClip", 64, 1, 44100, false);
            _idleLocomotionClip = AudioClip.Create("IdleLocomotionTestClip", 64, 1, 44100, false);
            _companionEffectPrefab = new GameObject("FairyCompanionEffectTestPrefab");
            _companionEffectPrefab.AddComponent<ParticleSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_companionEffectPrefab);
            UnityEngine.Object.DestroyImmediate(_celebrateClip);
            UnityEngine.Object.DestroyImmediate(_artifactReturnClip);
            UnityEngine.Object.DestroyImmediate(_artifactWaitClip);
            UnityEngine.Object.DestroyImmediate(_coachAttentionClip);
            UnityEngine.Object.DestroyImmediate(_idleLocomotionClip);
            UnityEngine.Object.DestroyImmediate(_speechClip);
        }

        [TestCase(false, 0f)]
        [TestCase(true, 0f)]
        [TestCase(true, 90f)]
        [TestCase(true, 180f)]
        public void AuthoredFirstLegMovesActualFairyToFirstPoint(bool beginAfterDialogue, float openingYaw)
        {
            var runtimeRoot = new GameObject("FairyRuntimeTestRoot");
            var viewer = new GameObject("FairyViewerTestRoot");
            var groundReference = new GameObject("FairyGroundReferenceTestRoot");
            viewer.transform.position = new Vector3(0f, 1.65f, 0f);
            viewer.transform.rotation = Quaternion.Euler(0, openingYaw, 0);
            groundReference.transform.position = new Vector3(0f, 0.18f, 0f);
            var prefab = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/BotanicalGardenQR/Content/Shared/Fairy/Models/Oppy/OppyFairyGuide.prefab"));
            var arrivalPrefab = CreateArrivalPrefab();
            var maskPrefab = CreateMaskPrefab();
            var platform = new GameObject("FairyArrivalPlatformTestRoot");
            platform.SetActive(false);
            var passthroughLayer = platform.AddComponent<OVRPassthroughLayer>();
            var environmentLight = platform.AddComponent<Light>();
            passthroughLayer.textureOpacity = 0.62f;
            passthroughLayer.edgeRenderingEnabled = false;
            passthroughLayer.edgeColor = Color.magenta;
            environmentLight.intensity = 2.4f;
            var originalPremultipliedAlpha = OVRManager.eyeFovPremultipliedAlphaModeEnabled;
            var controller = FairyModuleFactory.Create(
                runtimeRoot.transform,
                viewer.transform,
                groundReference.transform,
                passthroughLayer,
                environmentLight,
                null);
            var binding = FairyModuleFactory.BindAsCompanion(
                controller,
                new FairyDefinition(
                    prefab,
                    FairyBehavior.Guide,
                    1f,
                    arrivalPrefab,
                    maskPrefab,
                    CompanionFeedback(),
                    0f,
                    0f),
                initiallyVisible: false);
            GameObject dialogueStage = null;
            VisitorDialogueFairyBinding dialogueBinding = null;
            try
            {
                Assert.That(binding.Show().Succeeded, Is.True);
                var instance = runtimeRoot.GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "FairyRuntimeInstance");
                var definition = VisitorMapConfiguration.Resolve(AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json"));
                var start = definition.start;
                var origin = groundReference.transform.position - Quaternion.Euler(0, 90, 0) * new Vector3(start.x, start.y, start.z);
                var roomFrame = new BotanicalGardenQR.MapNavigation.Contracts.MapFrame(
                    new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(origin.x, origin.y, origin.z), 90, definition.scale);
                using var navigation = new BotanicalGardenQR.MapNavigation.Runtime.MapNavigationController(
                    definition, new FairyMapMotionSink((IFairyMotion)binding, definition), roomFrame);
                for (int i = 0; i < 12; i++)
                    navigation.TryInitialize(new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(0, 1.65f, 0), 0, .18f, .02f);
                var driver = instance.GetComponent<FairyOrbitDriver>();
                if (beginAfterDialogue)
                {
                    viewer.transform.position = new Vector3(0, 1.85f, 0);
                    viewer.AddComponent<Camera>().enabled = false;
                    var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(
                        "Assets/BotanicalGardenQR/Content/Authoring/VisitorCoachTheme.asset");
                    var defaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(
                        "Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset");
                    dialogueStage = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
                    var presenter = dialogueStage.GetComponentInChildren<VisitorCoachPresenter>(true);
                    presenter.Configure(viewer.transform, theme, defaults.SharedFont, new MotionGazeRegistry());
                    dialogueBinding = new VisitorDialogueFairyBinding(presenter, binding);
                    var context = new VisitorDialogueContextId("departure-regression");
                    presenter.Present(new VisitorDialogueSurfaceState(1, context, VisitorDialogueOwner.ToolPreparation,
                        VisitorDialogueSurfaceMode.Dialogue, "准备出发", theme.FairySpeakerName,
                        theme.ToolPreparationReadyCopy, 0, 1, string.Empty));
                    var normalScale = instance.localScale;
                    for (int i = 0; i < 200; i++)
                    {
                        driver.Tick(.02f);
                        Assert.That(instance.position.y, Is.EqualTo(groundReference.transform.position.y).Within(.001f));
                        Assert.That(instance.localScale, Is.EqualTo(normalScale));
                    }
                    presenter.Hide(context);
                }
                Assert.That(navigation.Begin(definition.points[0].id), Is.True);
                var animator = instance.GetComponentInChildren<Animator>();
                var bones = instance.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .SelectMany(renderer => renderer.bones).Where(bone => bone != null).Distinct().ToArray();
                Assert.That(bones, Is.Not.Empty, "Use the published character skeleton, not an empty animation test object.");
                var bonePose = bones.Select(bone => bone.localRotation).ToArray();
                var animatedWhileMoving = false;
                var route = new BotanicalGardenQR.MapNavigation.Runtime.MapRouteGeometry(navigation.CurrentWorldPath);
                var initial = instance.position;
                for (int frame = 0; frame < (beginAfterDialogue ? 0 : 30); frame++)
                {
                    navigation.Tick(new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(0, 1.65f, 0), true, false, .02f);
                    driver.Tick(.02f);
                }
                Assert.That(navigation.State.Progress, Is.LessThanOrEqualTo(definition.departureRadius + definition.speed * .6f),
                    "Initial reacquisition stays inside the forward entry window and elapsed travel budget.");
                for (int frame = 0; frame < 1500 && navigation.State.Phase != BotanicalGardenQR.MapNavigation.Contracts.MapNavigationPhase.Arrived; frame++)
                {
                    var before = instance.position;
                    viewer.transform.position = before + new Vector3(.5f, 1.65f, 0);
                    navigation.Tick(new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(viewer.transform.position.x, 1.65f, viewer.transform.position.z), true, false, .02f);
                    animator.Update(.02f);
                    driver.Tick(.02f);
                    if (navigation.State.Progress > .5f && animator.GetBool("Running"))
                        animatedWhileMoving |= bones.Where((bone, index) => Quaternion.Angle(bone.localRotation, bonePose[index]) > 1f).Any();
                    Assert.That(Vector3.Distance(before, instance.position), Is.LessThanOrEqualTo(definition.speed * .02f + .001f));
                    var delta = Vector3.ProjectOnPlane(instance.position - before, Vector3.up);
                    if (delta.sqrMagnitude > .000001f)
                        Assert.That(Vector3.Dot(instance.forward, delta.normalized), Is.GreaterThan(.98f), "Heading must agree with displacement.");
                }
                Assert.That(navigation.State.Phase, Is.EqualTo(BotanicalGardenQR.MapNavigation.Contracts.MapNavigationPhase.Arrived),
                    $"Actual Fairy stuck at {instance.position}, progress {navigation.State.Progress}/{route.Length}");
                Assert.That(animatedWhileMoving, Is.True, "Walking must animate the real skeleton while the route moves.");
                var terminal = instance.position;
                binding.PresentCue(FairyCompanionCue.DialogueFocus(terminal + Vector3.right * 2.2f));
                for (int i = 0; i < 240; i++) driver.Tick(.02f);
                Assert.That(Vector3.Distance(instance.position, terminal), Is.GreaterThan(2f));
                var arrivalEvents = 0;
                navigation.Changed += () => arrivalEvents++;
                var terminalProgress = navigation.State.Progress;
                navigation.Tick(default, true, true, .02f);
                binding.PresentCue(FairyCompanionCue.Idle);
                var readingPosition = instance.position;
                driver.Tick(.02f);
                Assert.That(instance.position, Is.EqualTo(readingPosition), "Cue expiry after this frame's Hold must not unlock restoration.");
                for (int i = 0; i < 30; i++)
                {
                    navigation.Tick(default, true, true, .02f);
                    driver.Tick(.02f);
                    Assert.That(instance.position, Is.EqualTo(readingPosition), "Arrival must still respect remaining reading blockers.");
                }
                for (int i = 0; i < 30; i++)
                {
                    navigation.Tick(default, false, false, .02f);
                    driver.Tick(.02f);
                    Assert.That(instance.position, Is.EqualTo(readingPosition), "Arrival must still respect tracking loss.");
                }
                viewer.transform.rotation = Quaternion.Euler(0, 90, 0);
                for (int i = 0; i < 200; i++)
                {
                    var before = instance.position;
                    navigation.Tick(default, true, false, .02f);
                    driver.Tick(.02f);
                    Assert.That(Vector3.Distance(before, instance.position), Is.LessThanOrEqualTo(definition.speed * .02f + .001f));
                }
                Assert.That(Vector3.Distance(instance.position, terminal), Is.LessThan(.002f), "Cue exit must restore the terminal, not head-front follow.");
                Assert.That(navigation.State.Phase, Is.EqualTo(BotanicalGardenQR.MapNavigation.Contracts.MapNavigationPhase.Arrived));
                Assert.That(navigation.State.Progress, Is.EqualTo(terminalProgress));
                Assert.That(arrivalEvents, Is.Zero, "Terminal pauses and restoration must not replay arrival or mutate route progress.");
                for (int i = 0; i < 20; i++) driver.Tick(.02f);
                Assert.That(Vector3.Distance(instance.position, terminal), Is.LessThan(.002f));

                // A new leg must not use the temporary reading position as its origin.
                binding.PresentCue(FairyCompanionCue.DialogueFocus(terminal + Vector3.right * 2.2f));
                for (int i = 0; i < 160; i++) driver.Tick(.02f);
                var oldRequest = navigation.State.RequestId;
                navigation.Begin(definition.points[1].id);
                ((IFairyMotion)binding).ReleaseMotion(oldRequest);
                binding.PresentCue(FairyCompanionCue.Idle);
                // Include the 2.2 m cue return plus turning at the published walking speed.
                var cueReturnFrames = Mathf.CeilToInt((2.2f / definition.speed + 2f) / .02f);
                for (int i = 0; i < cueReturnFrames; i++)
                {
                    viewer.transform.position = instance.position + new Vector3(.5f, 1.65f, 0);
                    navigation.Tick(new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(viewer.transform.position.x, 1.65f, viewer.transform.position.z), true, false, .02f);
                    if (i == 0)
                    {
                        ((IFairyMotion)binding).HoldMotion(oldRequest);
                        ((IFairyMotion)binding).ReleaseMotion(oldRequest);
                    }
                    driver.Tick(.02f);
                }
                Assert.That(navigation.State.Progress, Is.GreaterThan(0), "Next request must resume after returning to the previous geometric point.");
                ((IFairyMotion)binding).ReleaseMotion(oldRequest);
                var savedProgress = navigation.State.Progress;
                var pausedAt = instance.position;
                binding.PresentCue(FairyCompanionCue.DialogueFocus(pausedAt + Vector3.right * 2.2f));
                for (int i = 0; i < 160; i++)
                {
                    navigation.Tick(new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(viewer.transform.position.x, 1.65f, viewer.transform.position.z), true, true, .02f);
                    driver.Tick(.02f);
                }
                Assert.That(navigation.State.Progress, Is.EqualTo(savedProgress));
                binding.PresentCue(FairyCompanionCue.Idle);
                for (int i = 0; i < 30; i++)
                {
                    navigation.Tick(new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(viewer.transform.position.x, 1.65f, viewer.transform.position.z), true, true, .02f);
                    var before = instance.position;
                    driver.Tick(.02f);
                    Assert.That(instance.position, Is.EqualTo(before), "An unrelated remaining reading blocker must keep restoration paused.");
                }
                for (int i = 0; i < cueReturnFrames; i++)
                {
                    viewer.transform.position = instance.position + new Vector3(.5f, 1.65f, 0);
                    navigation.Tick(new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(viewer.transform.position.x, 1.65f, viewer.transform.position.z), true, false, .02f);
                    driver.Tick(.02f);
                }
                Assert.That(navigation.State.Progress, Is.GreaterThan(savedProgress));

                // Run the remaining published legs through the production adapter and driver.
                for (var leg = 1; leg < definition.points.Length; leg++)
                {
                    if (leg > 1) Assert.That(navigation.Begin(definition.points[leg].id), Is.True);
                    for (var frame = 0; frame < 3000 && navigation.State.Phase != BotanicalGardenQR.MapNavigation.Contracts.MapNavigationPhase.Arrived; frame++)
                    {
                        var before = instance.position;
                        viewer.transform.position = before + new Vector3(.5f, 1.65f, 0);
                        navigation.Tick(new BotanicalGardenQR.MapNavigation.Contracts.MapPosition(
                            viewer.transform.position.x, viewer.transform.position.y, viewer.transform.position.z), true, false, .02f);
                        driver.Tick(.02f);
                        Assert.That(Vector3.Distance(before, instance.position), Is.LessThanOrEqualTo(definition.speed * .02f + .001f));
                    }
                    Assert.That(navigation.State.Phase, Is.EqualTo(BotanicalGardenQR.MapNavigation.Contracts.MapNavigationPhase.Arrived), definition.points[leg].id);
                }

            }
            finally
            {
                dialogueBinding?.Dispose();
                if (dialogueStage != null) UnityEngine.Object.DestroyImmediate(dialogueStage);
                binding.Dispose();
                foreach (var effect in FindArrivalEffectInstances())
                    UnityEngine.Object.DestroyImmediate(effect);
                UnityEngine.Object.DestroyImmediate(maskPrefab);
                UnityEngine.Object.DestroyImmediate(arrivalPrefab);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(platform);
                UnityEngine.Object.DestroyImmediate(groundReference);
                UnityEngine.Object.DestroyImmediate(viewer);
                UnityEngine.Object.DestroyImmediate(runtimeRoot);
            }
        }

        [Test]
        public void InvitationOriginIsForwardedOnceWithoutRestartingAVisibleGuide()
        {
            var configuration = AssetDatabase.LoadAssetAtPath<FairyApplicationConfiguration>(
                "Assets/BotanicalGardenQR/Content/Authoring/FairyApplicationConfiguration.asset");
            Assert.That(configuration.TryGet(out var definition), Is.True);
            var controller = new RecordingFairyController();
            using var binding = FairyModuleFactory.BindAsCompanion(controller, definition, initiallyVisible: false);
            var origin = new Vector3(.25f, 1.15f, -.4f);
            Assert.That(binding.Show(origin).Succeeded, Is.True);
            Assert.That(controller.LastArrivalOrigin, Is.EqualTo(origin));
            var dispatches = controller.Intents.Count;
            binding.Show(Vector3.one);
            Assert.That(controller.Intents.Count, Is.EqualTo(dispatches));
            Assert.That(controller.LastArrivalOrigin, Is.EqualTo(origin));
        }

        [Test]
        public void BrokenArrivalDoesNotPublishActiveOrMarkTheFirstEncounterComplete()
        {
            var root = new GameObject("ArrivalFailureRoot");
            var viewer = new GameObject("ArrivalFailureViewer");
            var prefab = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/BotanicalGardenQR/Content/Shared/Fairy/Models/Oppy/OppyFairyGuide.prefab"));
            var effect = CreateArrivalPrefab();
            var brokenMask = new GameObject("BrokenArrivalMask");
            var platform = new GameObject("ArrivalFailurePlatform");
            platform.transform.SetParent(root.transform, false);
            platform.SetActive(false);
            var controller = FairyModuleFactory.Create(root.transform, viewer.transform, root.transform,
                platform.AddComponent<OVRPassthroughLayer>(), platform.AddComponent<Light>(), null);
            var binding = FairyModuleFactory.BindAsCompanion(controller,
                new FairyDefinition(prefab, FairyBehavior.Guide, 1f, effect, brokenMask, CompanionFeedback(), 0f, 0f), initiallyVisible: false);
            var phases = new List<FairyPhase>();
            using var observation = binding.Observe(new ArrivalStateSink(state => phases.Add(state.Phase)));
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception,
                    new System.Text.RegularExpressions.Regex("InvalidOperationException:.*arrival mask"));
                Assert.That(binding.Show().Succeeded, Is.False);
                Assert.That(phases.Last(), Is.EqualTo(FairyPhase.Failed));
                Assert.That(phases, Has.No.Member(FairyPhase.Active));
                Assert.That(root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "FairyRuntimeInstance").gameObject.activeSelf, Is.False);
            }
            finally
            {
                binding.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(viewer);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(effect);
                UnityEngine.Object.DestroyImmediate(brokenMask);
            }
        }

        [Test]
        public void AuthoredWalkMeasurementAndReactionInterruption()
        {
            const string root = "Assets/BotanicalGardenQR/Content/Shared/Fairy/Models/Oppy/";
            var clip = AssetDatabase.LoadAllAssetsAtPath(root + "Animations/walk_loop.fbx")
                .OfType<AnimationClip>().Single(item => item.name == "walk_loop");
            var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(root + "OppyFairyGuide.prefab"));
            try
            {
                var animator = instance.GetComponentInChildren<Animator>();
                TestContext.WriteLine($"Walk clip duration={clip.length:F4}s, imported speed={clip.averageSpeed}, visual scale={animator.transform.lossyScale}");
                var feet = animator.GetComponentsInChildren<Transform>()
                    .Where(bone => bone.name.IndexOf("foot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   bone.name.IndexOf("ankle", StringComparison.OrdinalIgnoreCase) >= 0).Take(6).ToArray();
                if (feet.Length == 0)
                    TestContext.WriteLine("Skeleton joints: " + string.Join(",", animator.GetComponentsInChildren<Transform>().Select(bone => bone.name)));
                foreach (var foot in feet)
                {
                    var min = float.PositiveInfinity;
                    var max = float.NegativeInfinity;
                    for (int sample = 0; sample <= 40; sample++)
                    {
                        clip.SampleAnimation(animator.gameObject, clip.length * sample / 40f);
                        var z = instance.transform.InverseTransformPoint(foot.position).z;
                        min = Mathf.Min(min, z); max = Mathf.Max(max, z);
                    }
                    TestContext.WriteLine($"Stance measurement {foot.name}: fore/aft range={max-min:F4}m, half-cycle speed={(max-min)*2f/clip.length:F4}m/s");
                }
                var animationDriver = instance.AddComponent<FairyAnimationDriver>();
                animationDriver.Initialize();
                animationDriver.SetTravelSpeed(.5084f);
                Assert.That(animator.GetFloat("WalkRate"), Is.EqualTo(2f).Within(.03f));
                instance.transform.localScale *= 2f;
                animationDriver.SetTravelSpeed(.5084f);
                Assert.That(animator.GetFloat("WalkRate"), Is.EqualTo(1f).Within(.03f), "A larger body covers more ground per step.");
                instance.transform.localScale *= .5f;
                animator.Rebind();
                animator.Update(0f);
                animator.SetTrigger("Wave");
                animator.Update(.2f);
                animator.SetBool("Running", true);
                for (int frame = 0; frame < 15; frame++) animator.Update(.02f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk Start") ||
                            animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), Is.True,
                    "Starting travel must interrupt a full-body standing reaction.");
                animator.SetTrigger("Like");
                for (int frame = 0; frame < 60; frame++) animator.Update(.02f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), Is.True,
                    "A reaction cannot take over the legs during travel.");
                animator.SetBool("Running", false);
                for (int frame = 0; frame < 60; frame++) animator.Update(.02f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        sealed class MotionGazeRegistry : IFrontendGazeSurfaceRegistry
        {
            public IDisposable SuspendPanelInput() => new Registration();
            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform root, int priority, string label) => new Registration();
            sealed class Registration : IFrontendGazeSurfaceRegistration
            {
                public bool IsFocused => false;
                public void Invalidate() { }
                public void Dispose() { }
            }
        }

        sealed class ArrivalStateSink : IFairyStateSink
        {
            readonly Action<FairyState> _publish;
            public ArrivalStateSink(Action<FairyState> publish) => _publish = publish;
            public void Publish(FairyState state) => _publish(state);
        }

        [Test]
        public void Companion_RemainsOneInstanceAcrossContentSwitches()
        {
            var prefab = new GameObject("GlobalFairyTestPrefab");
            var arrivalPrefab = CreateArrivalPrefab();
            var maskPrefab = CreateMaskPrefab();
            var controller = new RecordingFairyController();
            var binding = FairyModuleFactory.BindAsCompanion(
                controller,
                new FairyDefinition(
                    prefab,
                    FairyBehavior.Guide,
                    1f,
                    arrivalPrefab,
                    maskPrefab,
                    CompanionFeedback(),
                    2f,
                    0.3f));
            try
            {
                var flow = ExperienceFlow.CreateOnCurrentThread(
                    new DescriptorSource(),
                    new FeaturePageRegistry(Array.Empty<IFeaturePageLifecycle>()));

                Commit(flow, FirstScene);
                Commit(flow, SecondScene);

                Assert.That(controller.OpenCalls, Is.EqualTo(1));
                Assert.That(controller.CloseCalls, Is.Zero);
                Assert.That(controller.OpenedSession.IsValid, Is.True);
            }
            finally
            {
                binding.Dispose();
                binding.Dispose();
                UnityEngine.Object.DestroyImmediate(maskPrefab);
                UnityEngine.Object.DestroyImmediate(arrivalPrefab);
                UnityEngine.Object.DestroyImmediate(prefab);
            }

            Assert.That(controller.CloseCalls, Is.EqualTo(1));
        }

        [Test]
        public void PublishedSceneResolver_CannotSupplyFairyByPackageOrder()
            => Assert.That(
                typeof(IFairyDefinitionSource).IsAssignableFrom(typeof(PublishedSceneResolver)),
                Is.False);

        [Test]
        public void Companion_ForwardsResolvedWorldCueWithoutChangingLifecycle()
        {
            var prefab = new GameObject("GlobalFairyCueTestPrefab");
            var arrivalPrefab = CreateArrivalPrefab();
            var maskPrefab = CreateMaskPrefab();
            var controller = new RecordingFairyController();
            var binding = FairyModuleFactory.BindAsCompanion(
                controller,
                new FairyDefinition(
                    prefab,
                    FairyBehavior.Guide,
                    1f,
                    arrivalPrefab,
                    maskPrefab,
                    CompanionFeedback(),
                    2f,
                    0.3f));
            try
            {
                var cue = new FairyCompanionCue(
                    FairyCompanionCueKind.Celebrate,
                    new Vector3(1f, 2f, 3f),
                    0.75f);

                var result = binding.PresentCue(cue);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(controller.OpenCalls, Is.EqualTo(1));
                Assert.That(controller.CloseCalls, Is.Zero);
                Assert.That(controller.CueCalls, Is.EqualTo(1));
                Assert.That(controller.LastCue.Kind, Is.EqualTo(FairyCompanionCueKind.Celebrate));
                Assert.That(controller.LastCue.WorldPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(controller.LastCue.DurationSeconds, Is.EqualTo(0.75f));
            }
            finally
            {
                binding.Dispose();
                UnityEngine.Object.DestroyImmediate(maskPrefab);
                UnityEngine.Object.DestroyImmediate(arrivalPrefab);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void CompanionCue_RejectsInvalidDuration()
            => Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FairyCompanionCue(
                    FairyCompanionCueKind.ArtifactReturn,
                    Vector3.one,
                    -0.1f));

        [Test]
        public void CompanionCue_RejectsInvalidPosition()
            => Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FairyCompanionCue(
                    FairyCompanionCueKind.DialogueFocus,
                    new Vector3(float.NaN, 0f, 0f)));

        [Test]
        public void PanoramaVisibility_HidesFairyAndRestoresDeferredShow()
        {
            var prefab = new GameObject("PanoramaFairyTestPrefab");
            var arrivalPrefab = CreateArrivalPrefab();
            var maskPrefab = CreateMaskPrefab();
            var controller = new RecordingFairyController();
            var fairy = FairyModuleFactory.BindAsCompanion(
                controller,
                new FairyDefinition(
                    prefab,
                    FairyBehavior.Guide,
                    1f,
                    arrivalPrefab,
                    maskPrefab,
                    CompanionFeedback(),
                    2f,
                    0.3f));
            try
            {
                var flow = ExperienceFlow.CreateOnCurrentThread(
                    new PanoramaDescriptorSource(),
                    new FeaturePageRegistry(new[] { new SuccessfulPanoramaLifecycle() }));
                using var visibility = new PanoramaFairyVisibilityBinding(flow, fairy);
                var session = Commit(flow, FirstScene);

                Assert.That(flow.EnterFeature(session, FeaturePageId.Panorama).Succeeded, Is.True);
                Assert.That(controller.Intents, Is.EqualTo(new[] { FairyIntent.Hide }));

                Assert.That(fairy.Show().Succeeded, Is.True);
                Assert.That(controller.Intents, Is.EqualTo(new[] { FairyIntent.Hide }));

                Assert.That(flow.BackToMain(session).Succeeded, Is.True);
                Assert.That(controller.Intents, Is.EqualTo(new[] { FairyIntent.Hide, FairyIntent.Show }));
            }
            finally
            {
                fairy.Dispose();
                UnityEngine.Object.DestroyImmediate(maskPrefab);
                UnityEngine.Object.DestroyImmediate(arrivalPrefab);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void PanoramaVisibility_DoesNotRestoreFairyAfterLatestHideIntent()
        {
            var prefab = new GameObject("PanoramaHiddenFairyTestPrefab");
            var arrivalPrefab = CreateArrivalPrefab();
            var maskPrefab = CreateMaskPrefab();
            var controller = new RecordingFairyController();
            var fairy = FairyModuleFactory.BindAsCompanion(
                controller,
                new FairyDefinition(
                    prefab,
                    FairyBehavior.Guide,
                    1f,
                    arrivalPrefab,
                    maskPrefab,
                    CompanionFeedback(),
                    2f,
                    0.3f));
            try
            {
                var flow = ExperienceFlow.CreateOnCurrentThread(
                    new PanoramaDescriptorSource(),
                    new FeaturePageRegistry(new[] { new SuccessfulPanoramaLifecycle() }));
                using var visibility = new PanoramaFairyVisibilityBinding(flow, fairy);
                var session = Commit(flow, FirstScene);

                Assert.That(flow.EnterFeature(session, FeaturePageId.Panorama).Succeeded, Is.True);
                Assert.That(fairy.Hide().Succeeded, Is.True);
                Assert.That(flow.BackToMain(session).Succeeded, Is.True);

                Assert.That(controller.Intents, Is.EqualTo(new[] { FairyIntent.Hide }));
            }
            finally
            {
                fairy.Dispose();
                UnityEngine.Object.DestroyImmediate(maskPrefab);
                UnityEngine.Object.DestroyImmediate(arrivalPrefab);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Companion_FirstRevealPlaysOneArrivalEffectAndLaterShowsDoNotReplay()
        {
            var runtimeRoot = new GameObject("FairyRuntimeTestRoot");
            var viewer = new GameObject("FairyViewerTestRoot");
            var groundReference = new GameObject("FairyGroundReferenceTestRoot");
            viewer.transform.position = new Vector3(0f, 1.65f, 0f);
            groundReference.transform.position = new Vector3(0f, 0.18f, 0f);
            var prefab = CreateFairyPrefab("GlobalFairyArrivalLifecycleTestPrefab");
            var arrivalPrefab = CreateArrivalPrefab();
            var maskPrefab = CreateMaskPrefab();
            var platform = new GameObject("FairyArrivalPlatformTestRoot");
            platform.SetActive(false);
            var passthroughLayer = platform.AddComponent<OVRPassthroughLayer>();
            var environmentLight = platform.AddComponent<Light>();
            passthroughLayer.textureOpacity = 0.62f;
            passthroughLayer.edgeRenderingEnabled = false;
            passthroughLayer.edgeColor = Color.magenta;
            environmentLight.intensity = 2.4f;
            var originalPremultipliedAlpha = OVRManager.eyeFovPremultipliedAlphaModeEnabled;
            var controller = FairyModuleFactory.Create(
                runtimeRoot.transform,
                viewer.transform,
                groundReference.transform,
                passthroughLayer,
                environmentLight,
                null);
            var binding = FairyModuleFactory.BindAsCompanion(
                controller,
                new FairyDefinition(
                    prefab,
                    FairyBehavior.Guide,
                    1f,
                    arrivalPrefab,
                    maskPrefab,
                    CompanionFeedback(),
                    0f,
                    0f),
                initiallyVisible: false);
            try
            {
                var firstShow = binding.Show();
                var fairyInstance = runtimeRoot
                    .GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "FairyRuntimeInstance");
                var firstEffects = FindArrivalEffectInstances();

                Assert.That(firstShow.Succeeded, Is.True);
                Assert.That(fairyInstance.gameObject.activeSelf, Is.True);
                Assert.That(fairyInstance.position.y, Is.EqualTo(groundReference.transform.position.y));
                var motion = (IFairyMotion)binding;
                Assert.That(motion.TryGetMotionPosition(out var initialPosition), Is.True);
                Assert.That(motion.ApplyMotion(1, initialPosition, Vector3.forward, false, 1.5f, .7f), Is.True);
                var nextPosition = initialPosition + Vector3.forward * .01f;
                Assert.That(motion.ApplyMotion(1, nextPosition, Vector3.forward, true, 1.5f, .7f), Is.False,
                    "The viewer-facing character must turn before accepting forward travel.");
                for (var frame = 0; frame < 45; frame++)
                {
                    var beforeTurn = fairyInstance.rotation;
                    fairyInstance.GetComponent<FairyOrbitDriver>().Tick(.02f);
                    Assert.That(fairyInstance.position, Is.EqualTo(initialPosition));
                    Assert.That(Quaternion.Angle(beforeTurn, fairyInstance.rotation), Is.LessThanOrEqualTo(4.81f));
                }
                Assert.That(motion.ApplyMotion(1, nextPosition, Vector3.forward, true, 1.5f, .7f), Is.True);
                Assert.That(fairyInstance.position, Is.EqualTo(nextPosition));
                motion.HoldMotion(1);
                Assert.That(fairyInstance.position, Is.EqualTo(nextPosition));
                motion.ReleaseMotion(1);
                Assert.That(motion.ApplyMotion(1, nextPosition, Vector3.forward, true, 1.5f, .7f), Is.False);
                Assert.That(motion.ApplyMotion(2, nextPosition, Vector3.forward, false, 1.5f, .7f), Is.True);
                motion.ReleaseMotion(1);
                Assert.That(motion.ApplyMotion(2, nextPosition + Vector3.forward * .01f, Vector3.forward, true, 1.5f, .7f), Is.True);
                Assert.That(motion.ApplyMotion(2, initialPosition, Vector3.forward, false, 1.5f, .7f), Is.False,
                    "Even a short stationary reacquisition must move continuously rather than snap.");
                for (var frame = 0; frame < 45; frame++)
                {
                    var before = fairyInstance.position;
                    fairyInstance.GetComponent<FairyOrbitDriver>().Tick(.02f);
                    Assert.That(Vector3.Distance(before, fairyInstance.position), Is.LessThanOrEqualTo(.0141f));
                }
                Assert.That(motion.ApplyMotion(2, initialPosition, Vector3.forward, false, 1.5f, .7f), Is.True);
                motion.ReleaseMotion(2);
                var arrivalAudio = fairyInstance.GetComponent<AudioSource>();
                Assert.That(arrivalAudio, Is.Not.Null);
                Assert.That(arrivalAudio.spatialBlend, Is.EqualTo(1f).Within(0.001f));
                Assert.That(arrivalAudio.spatialize, Is.True,
                    "Fairy arrival audio must use the project's Meta XR Audio spatializer.");
                Assert.That(arrivalAudio.dopplerLevel, Is.Zero,
                    "The orbiting guide must not pitch-shift its short visitor feedback cues.");
                Assert.That(firstEffects.Length, Is.Zero, "Arrival effects finish before Active is published.");
                Assert.That(passthroughLayer.textureOpacity, Is.EqualTo(0.62f));
                Assert.That(passthroughLayer.edgeRenderingEnabled, Is.False);
                Assert.That(passthroughLayer.edgeColor, Is.EqualTo(Color.magenta));
                Assert.That(environmentLight.intensity, Is.EqualTo(2.4f));
                Assert.That(
                    OVRManager.eyeFovPremultipliedAlphaModeEnabled,
                    Is.EqualTo(originalPremultipliedAlpha));
                var runtimeController = (FairyController)controller;
                Assert.That(binding.Speak(new FairySpeech(_speechClip)).FailureCode,
                    Is.EqualTo(_speechClip.length > 0 ? FairyFailureCode.AudioUnavailable : FairyFailureCode.InvalidSpeech),
                    "The muted editor may create a zero-length clip; both cases must reject playback.");
                Assert.That(runtimeController.SpeechPlayCount, Is.Zero,
                    "EditMode must not count a request as actual audio playback.");
                Assert.That(runtimeController.LastSpeechClip, Is.Null);
                Assert.That(fairyInstance.GetComponents<AudioSource>().Length, Is.EqualTo(1),
                    "Fairy teaching speech must reuse its one spatial AudioSource.");

                Assert.That(binding.Hide().Succeeded, Is.True);
                Assert.That(binding.Show().Succeeded, Is.True);
                Assert.That(FindArrivalEffectInstances().Length, Is.Zero);
            }
            finally
            {
                binding.Dispose();
                foreach (var effect in FindArrivalEffectInstances())
                    UnityEngine.Object.DestroyImmediate(effect);
                UnityEngine.Object.DestroyImmediate(maskPrefab);
                UnityEngine.Object.DestroyImmediate(arrivalPrefab);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(platform);
                UnityEngine.Object.DestroyImmediate(groundReference);
                UnityEngine.Object.DestroyImmediate(viewer);
                UnityEngine.Object.DestroyImmediate(runtimeRoot);
            }
        }

        [Test]
        public void CompanionSemanticFeedback_ReusesOneEffectAndDoesNotReplayTheSameCue()
        {
            var runtimeRoot = new GameObject("FairyFeedbackRuntimeTestRoot");
            var viewer = new GameObject("FairyFeedbackViewerTestRoot");
            var groundReference = new GameObject("FairyFeedbackGroundReferenceTestRoot");
            var prefab = CreateFairyPrefab("GlobalFairyFeedbackLifecycleTestPrefab");
            var arrivalPrefab = CreateArrivalPrefab();
            var maskPrefab = CreateMaskPrefab();
            var platform = new GameObject("FairyFeedbackPlatformTestRoot");
            platform.SetActive(false);
            var passthroughLayer = platform.AddComponent<OVRPassthroughLayer>();
            var environmentLight = platform.AddComponent<Light>();
            var controller = FairyModuleFactory.Create(
                runtimeRoot.transform,
                viewer.transform,
                groundReference.transform,
                passthroughLayer,
                environmentLight,
                null);
            var runtimeController = (FairyController)controller;
            var binding = FairyModuleFactory.BindAsCompanion(
                controller,
                new FairyDefinition(
                    prefab,
                    FairyBehavior.Guide,
                    1f,
                    arrivalPrefab,
                    maskPrefab,
                    CompanionFeedback(),
                    0f,
                    0f));
            try
            {
                var effectInstance = runtimeController.CompanionCueEffectInstance;
                var fairyInstance = runtimeRoot
                    .GetComponentsInChildren<Transform>(true)
                    .Single(item => item.name == "FairyRuntimeInstance");

                Assert.That(effectInstance, Is.Not.Null);
                Assert.That(effectInstance.name, Is.EqualTo("FairyCompanionCueEffect"));
                Assert.That(effectInstance.transform.parent, Is.SameAs(fairyInstance));
                Assert.That(
                    effectInstance.transform.localPosition,
                    Is.EqualTo(_companionEffectPrefab.transform.localPosition +
                               new Vector3(0f, 0.4f, 0f)));
                Assert.That(effectInstance.transform.localRotation,
                    Is.EqualTo(_companionEffectPrefab.transform.localRotation));
                Assert.That(fairyInstance.GetComponents<AudioSource>().Length, Is.EqualTo(1));
                Assert.That(
                    runtimeRoot.GetComponentsInChildren<Transform>(true)
                        .Count(item => item.name == "FairyCompanionCueEffect"),
                    Is.EqualTo(1));

                var attention = FairyCompanionCue.CoachAttention(0.55f);
                Assert.That(binding.PresentCue(attention).Succeeded, Is.True);
                Assert.That(runtimeController.CompanionFeedbackPlayCount, Is.EqualTo(1));
                Assert.That(runtimeController.LastCompanionFeedbackClip,
                    Is.SameAs(_coachAttentionClip));

                Assert.That(binding.PresentCue(attention).Succeeded, Is.True);
                Assert.That(runtimeController.CompanionFeedbackPlayCount, Is.EqualTo(1),
                    "The one-shot QR coach reaction must not stack audio or particles.");
                Assert.That(runtimeController.CompanionCueEffectInstance,
                    Is.SameAs(effectInstance));

                var wait = new FairyCompanionCue(
                    FairyCompanionCueKind.ArtifactWait,
                    new Vector3(1f, 0.2f, 0f),
                    0.35f);
                Assert.That(binding.PresentCue(wait).Succeeded, Is.True);
                Assert.That(runtimeController.CompanionFeedbackPlayCount, Is.EqualTo(2));
                Assert.That(runtimeController.LastCompanionFeedbackClip,
                    Is.SameAs(_artifactWaitClip));
                Assert.That(binding.PresentCue(wait).Succeeded, Is.True);
                Assert.That(runtimeController.CompanionFeedbackPlayCount, Is.EqualTo(2),
                    "The artifact-wait cue must not replay while the Fairy remains at the artifact.");

                var artifactReturn = new FairyCompanionCue(
                    FairyCompanionCueKind.ArtifactReturn,
                    new Vector3(1f, 0.2f, 0f),
                    0.65f);
                Assert.That(binding.PresentCue(artifactReturn).Succeeded, Is.True);
                Assert.That(runtimeController.CompanionFeedbackPlayCount, Is.EqualTo(3));
                Assert.That(runtimeController.LastCompanionFeedbackClip,
                    Is.SameAs(_artifactReturnClip));

                Assert.That(binding.PresentCue(FairyCompanionCue.Idle).Succeeded, Is.True);
                var celebration = new FairyCompanionCue(
                    FairyCompanionCueKind.Celebrate,
                    Vector3.zero,
                    1.2f);
                Assert.That(binding.PresentCue(celebration).Succeeded, Is.True);
                Assert.That(runtimeController.CompanionFeedbackPlayCount, Is.EqualTo(4));
                Assert.That(runtimeController.LastCompanionFeedbackClip,
                    Is.SameAs(_celebrateClip));
                Assert.That(runtimeController.CompanionCueEffectInstance,
                    Is.SameAs(effectInstance));
                Assert.That(
                    runtimeRoot.GetComponentsInChildren<Transform>(true)
                        .Count(item => item.name == "FairyCompanionCueEffect"),
                    Is.EqualTo(1));

                Assert.That(binding.Hide().Succeeded, Is.True);
                Assert.That(binding.PresentCue(attention).Succeeded, Is.True);
                Assert.That(runtimeController.CompanionFeedbackPlayCount, Is.EqualTo(4),
                    "A cue received while hidden must remain deferred instead of playing invisibly.");
                Assert.That(binding.Show().Succeeded, Is.True);
                Assert.That(binding.PresentCue(attention).Succeeded, Is.True);
                Assert.That(runtimeController.CompanionFeedbackPlayCount, Is.EqualTo(5),
                    "The same deferred cue must play once after the Fairy becomes visible.");
                Assert.That(binding.PresentCue(attention).Succeeded, Is.True);
                Assert.That(runtimeController.CompanionFeedbackPlayCount, Is.EqualTo(5),
                    "The visible cue must still retain normal one-shot deduplication.");
            }
            finally
            {
                binding.Dispose();
                UnityEngine.Object.DestroyImmediate(maskPrefab);
                UnityEngine.Object.DestroyImmediate(arrivalPrefab);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(platform);
                UnityEngine.Object.DestroyImmediate(groundReference);
                UnityEngine.Object.DestroyImmediate(viewer);
                UnityEngine.Object.DestroyImmediate(runtimeRoot);
            }
        }

        [Test]
        public void ProductionAnimationCannotExposeTheGuideDuringTheEstablishingWorldShot()
        {
            var root = new GameObject("ProductionArrivalVisibilityTest");
            try
            {
                var viewer = new GameObject("Viewer"); viewer.transform.SetParent(root.transform);
                viewer.transform.position = new Vector3(0, 1.65f, 0);
                var floor = new GameObject("Floor"); floor.transform.SetParent(root.transform);
                var platform = new GameObject("OfflinePlatform"); platform.transform.SetParent(root.transform);
                platform.SetActive(false);
                var config = AssetDatabase.LoadAssetAtPath<FairyApplicationConfiguration>(
                    "Assets/BotanicalGardenQR/Content/Authoring/FairyApplicationConfiguration.asset");
                Assert.That(config.TryGet(out var definition), Is.True);
                var controller = FairyModuleFactory.Create(root.transform, viewer.transform, floor.transform,
                    platform.AddComponent<OVRPassthroughLayer>(), platform.AddComponent<Light>(), arrivalHandPosition: point => point + Vector3.forward * .02f);
                using var binding = FairyModuleFactory.BindAsCompanion(controller, definition, initiallyVisible: false);
                Assert.That(binding.Show().Succeeded, Is.True);
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(skins, Is.Not.Empty);
                var window = root.GetComponentInChildren<FairyArrivalOtherWorldWindow>();
                var lateUpdate = typeof(FairyArrivalOtherWorldWindow).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                for (var frame = 0; frame < 80; frame++)
                {
                    ((FairyController)controller).AdvanceFirstArrival(.05f);
                    foreach (var animator in root.GetComponentsInChildren<Animator>()) animator.Update(.05f);
                    lateUpdate.Invoke(window, null);
                    Assert.That(skins.All(skin => !skin.enabled), Is.True,
                        $"At frame {frame}, the establishing shot must remain empty even when the production Animator updates. Visible: " +
                        string.Join(", ", skins.Where(skin => skin.enabled).Select(skin => skin.name)));
                }
                for (var step = 0; step < 100; step++) ((FairyController)controller).AdvanceFirstArrival(.05f);
                Assert.That(skins.Any(skin => skin.enabled), Is.True, "The authored discovery beat reveals the same guide.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Companion_FirstArrivalOpensOtherWorldBeforeOppyAndHideCleansIt(bool completeArrival)
        {
            var runtimeRoot = new GameObject("FairyApertureRuntimeTestRoot");
            var viewer = new GameObject("FairyApertureViewerTestRoot");
            var groundReference = new GameObject("FairyApertureGroundTestRoot");
            viewer.transform.position = new Vector3(0f, 1.65f, 0f);
            groundReference.transform.position = new Vector3(0f, 0.18f, 0f);
            var prefab = CreateFairyPrefab("FairyApertureTestPrefab");
            var arrivalPrefab = CreateArrivalPrefab();
            var maskPrefab = CreateMaskPrefab();
            var platform = new GameObject("FairyAperturePlatformTestRoot");
            platform.SetActive(false);
            var controller = FairyModuleFactory.Create(
                runtimeRoot.transform,
                viewer.transform,
                groundReference.transform,
                platform.AddComponent<OVRPassthroughLayer>(),
                platform.AddComponent<Light>(),
                null, point => point + Vector3.forward * .02f);
            var binding = FairyModuleFactory.BindAsCompanion(
                controller,
                new FairyDefinition(
                    prefab,
                    FairyBehavior.Guide,
                    1f,
                    arrivalPrefab,
                    maskPrefab,
                    CompanionFeedback(),
                    2f,
                    0.3f),
                initiallyVisible: false);
            var arrivalStates = new List<FairyPhase>();
            using var arrivalLease = controller.Observe(new ArrivalStateSink(state =>
            {
                arrivalStates.Add(state.Phase);
                if (state.Phase == FairyPhase.Active)
                    Assert.That(FindArrivalEffectInstances(), Is.Empty,
                        "Ready must not be published before the local effect is cleaned up.");
            }));
            var originalAlpha = OVRManager.eyeFovPremultipliedAlphaModeEnabled;
            try
            {
                Assert.That(binding.Show().Succeeded, Is.True);
                Assert.That(arrivalStates.Last(), Is.EqualTo(FairyPhase.Arriving));
                var passageNodes = runtimeRoot.GetComponentsInChildren<Transform>(true);
                Assert.That(passageNodes.Count(item => item.name == "GroundContract"), Is.Zero,
                    "Spatial opening has no ground magic seal.");
                Assert.That(passageNodes.Any(item => item.name == "TravellingBookContract" || item.name == "RiftContractCrown"),
                    Is.False, "Transfer and portal frame must not duplicate the ground seal artwork.");
                Assert.That(FindArrivalEffectInstances(), Is.Empty,
                    "The main arrival burst belongs to materialization, not the opening cue.");
                var mask = UnityEngine.Object.FindObjectsByType<GameObject>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Single(item => item.name == "FairyArrivalDiscoveryMask");
                var aperture = mask.GetComponentInChildren<FairyArrivalFlashlightAperture>(true);
                Assert.That(aperture, Is.Not.Null);
                Assert.That(mask.GetComponent<FairyArrivalOtherWorldWindow>(), Is.Not.Null);
                Assert.That(mask.transform.Find("OppyOtherWorldEnvironment"), Is.Not.Null);
                Assert.That(mask.transform.Find("OtherWorldPortalStencil"), Is.Not.Null);
                Assert.That(
                    mask.transform.parent.GetComponentsInChildren<Renderer>(true)
                        .Where(item => !item.transform.IsChildOf(mask.transform)),
                    Has.All.Matches<Renderer>(item => !item.enabled),
                    "Oppy must not render before the automatic other-world window finishes opening.");
                var lightVolumes = aperture.transform.Find("parent/LightVolumes");
                Assert.That(lightVolumes, Is.Not.Null);
                Assert.That(aperture.GetComponentsInChildren<Renderer>(true).All(item => !item.enabled), Is.True,
                    "The spatial boundary must not render the former flashlight beams.");
                var fixedSize = aperture.Plane.lossyScale;
                var fixedRotation = aperture.Plane.rotation;
                var destination = aperture.Plane.position;
                viewer.transform.position += Vector3.right * .5f;
                aperture.SetInvitationWindow(viewer.transform.position, destination, 1);
                aperture.SetOpeningProgress(1);
                Assert.That(aperture.Plane.lossyScale, Is.EqualTo(fixedSize));
                Assert.That(aperture.Plane.rotation, Is.EqualTo(fixedRotation), "The door is world-fixed, not a billboard.");

                var window = mask.GetComponent<FairyArrivalOtherWorldWindow>();
                var ambience = mask.GetComponent<AudioSource>();
                Assert.That(ambience, Is.Not.Null);
                var setStrength = typeof(FairyArrivalOtherWorldWindow).GetMethod(
                    "SetStrength", BindingFlags.Instance | BindingFlags.NonPublic);
                var setAnticipation = typeof(FairyArrivalOtherWorldWindow).GetMethod(
                    "SetAnticipation", BindingFlags.Instance | BindingFlags.NonPublic);
                setStrength.Invoke(window, new object[] { 0f });
                setAnticipation.Invoke(window, new object[] { 1f });
                Assert.That(ambience.volume, Is.Zero, "Anticipation cannot make a closed window audible.");
                setStrength.Invoke(window, new object[] { .5f });
                var firstOrder = ambience.volume;
                setAnticipation.Invoke(window, new object[] { 1f });
                Assert.That(ambience.volume, Is.EqualTo(firstOrder).Within(.0001f),
                    "Window visibility and musical anticipation must compose independently of call order.");
                var passthrough = platform.GetComponent<OVRPassthroughLayer>();
                var light = platform.GetComponent<Light>();
                var opacity = passthrough.textureOpacity;
                var edges = passthrough.edgeRenderingEnabled;
                var intensity = light.intensity;
                if (completeArrival)
                {
                    for (var frame = 0; frame < 700; frame++)
                    {
                        ((FairyController)controller).AdvanceFirstArrival(.05f);
                        if (frame == 100)
                        {
                            Assert.That(arrivalStates.Last(), Is.EqualTo(FairyPhase.Arriving),
                                "The other world must have an establishing beat before the character crosses over.");

                        }
                        Assert.That(passthrough.textureOpacity, Is.EqualTo(opacity));

                    }
                    Assert.That(arrivalStates.Last(), Is.EqualTo(FairyPhase.Active));
                    Assert.That(arrivalStates.Count(phase => phase == FairyPhase.Active), Is.EqualTo(1));
                }
                if (!completeArrival)
                {
                    Assert.That(binding.Show().Succeeded, Is.True, "Repeated Show must join the current arrival.");
                    ((FairyController)controller).AdvanceFirstArrival(5f);
                    Assert.That(light.intensity, Is.LessThan(intensity), "Exercise cancellation while temporary lighting is active.");
                }
                Assert.That(binding.Hide().Succeeded, Is.True);
                Assert.That(passthrough.edgeRenderingEnabled, Is.EqualTo(edges));
                Assert.That(light.intensity, Is.EqualTo(intensity));
                Assert.That(FindArrivalEffectInstances(), Is.Empty);
                Assert.That(OVRManager.eyeFovPremultipliedAlphaModeEnabled, Is.EqualTo(originalAlpha));
                Assert.That(
                    UnityEngine.Object.FindObjectsByType<GameObject>(
                            FindObjectsInactive.Include,
                            FindObjectsSortMode.None)
                        .Any(item => item.name == "FairyArrivalDiscoveryMask"),
                    Is.False);
            }
            finally
            {
                binding.Dispose();
                UnityEngine.Object.DestroyImmediate(maskPrefab);
                UnityEngine.Object.DestroyImmediate(arrivalPrefab);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(platform);
                UnityEngine.Object.DestroyImmediate(groundReference);
                UnityEngine.Object.DestroyImmediate(viewer);
                UnityEngine.Object.DestroyImmediate(runtimeRoot);
            }
        }

        [Test]
        public void AuthoredArrival_FramesWholeGuideAndKeepsWindowUntilCrossing()
        {
            var root = new GameObject("AuthoredArrivalFramingTest");
            var cameraObject = new GameObject("ArrivalViewer", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 1.65f, 0);
            camera.fieldOfView = 64f;
            var floor = new GameObject("ArrivalFloor");
            var platform = new GameObject("ArrivalPlatform");
            platform.SetActive(false);
            var light = root.AddComponent<Light>();
            var config = AssetDatabase.LoadAssetAtPath<FairyApplicationConfiguration>(
                "Assets/BotanicalGardenQR/Content/Authoring/FairyApplicationConfiguration.asset");
            Assert.That(config.TryGet(out var definition), Is.True);
            var controller = FairyModuleFactory.Create(root.transform, camera.transform, floor.transform,
                platform.AddComponent<OVRPassthroughLayer>(), light, arrivalHandPosition: point => point + Vector3.forward * .02f);
            using var binding = FairyModuleFactory.BindAsCompanion(controller, definition, initiallyVisible: false);
            try
            {
                Assert.That(binding.Show(new Vector3(0, 1.35f, .55f)).Succeeded, Is.True);
                var runtime = (FairyController)controller;
                var instance = (GameObject)typeof(FairyController).GetField("_instance",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                var body = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .OrderByDescending(item => item.bounds.size.sqrMagnitude).First();
                var fixedDoor = Vector3.zero;
                var firstOpening = 0f;
                var secondOpening = 0f;
                var previousWalkPosition = Vector3.zero;
                var walkedFrames = 0;
                for (var frame = 0; frame < 330; frame++)
                {
                    runtime.AdvanceFirstArrival(.1f);
                    foreach (var animator in instance.GetComponentsInChildren<Animator>()) animator.Update(.1f);
                    var mask = instance.GetComponentInChildren<FairyArrivalOtherWorldWindow>();
                    if (mask == null) continue;
                    // Match Unity's frame order: imported clips can animate renderer visibility;
                    // the window enforces its visibility contract in LateUpdate, after the Animator.
                    typeof(FairyArrivalOtherWorldWindow).GetMethod("LateUpdate",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mask, null);
                    var slice = mask.transform.Find("OppyDiscoveryFlashlightAperture/parent/LightVolumes/LightVolumeD");
                    if (frame == 0) fixedDoor = slice.position;
                    if (frame > 0)
                        Assert.That(Vector3.Distance(slice.position, fixedDoor), Is.LessThan(.001f),
                            "The garden door must remain fixed while the character advances.");
                    var currentAperture = mask.GetComponentInChildren<FairyArrivalFlashlightAperture>();
                    if (frame == 11) firstOpening = currentAperture.Opening;
                    if (frame == 18) Assert.That(currentAperture.Opening, Is.LessThan(firstOpening * .5f), "The first attempt must visibly recoil.");
                    if (frame == 26)
                    {
                        secondOpening = currentAperture.Opening;
                        Assert.That(secondOpening, Is.GreaterThan(firstOpening), "The next attempt is larger.");
                    }
                    if (frame == 32) Assert.That(currentAperture.Opening, Is.LessThan(secondOpening * .5f), "The second attempt must breathe back before release.");
                    if (frame >= 85 && frame <= 140) Assert.That(currentAperture.Opening, Is.GreaterThan(.99f), "Breathing ends before the body crosses.");
                    var moved = previousWalkPosition != Vector3.zero ? Vector3.Distance(instance.transform.position, previousWalkPosition) : 0;
                    if (moved > .001f && body.sharedMaterial.GetFloat("_ArrivalCrossing") > .5f)
                    {
                        if (walkedFrames > 0) Assert.That(Vector3.Distance(instance.transform.position, previousWalkPosition), Is.EqualTo(.03f).Within(.001f), "Discovery beats must not accelerate or reverse walking.");
                        foreach (var animator in instance.GetComponentsInChildren<Animator>())
                            Assert.That(animator.GetFloat("WalkRate"), Is.InRange(.9f, 1.5f), "Keep the authored walk at a natural cadence.");
                        walkedFrames++;
                    }
                    previousWalkPosition = instance.transform.position;
                    if (frame == 125)
                        Assert.That(body.sharedMaterial.GetFloat("_ArrivalCrossing"), Is.EqualTo(1),
                            "Crossing retains per-body-fragment clipping instead of restoring materials at the first step.");
                    if (frame == 195)
                        Assert.That(body.sharedMaterial.GetFloat("_ArrivalCrossing"), Is.Zero,
                            "The cleared body must have its original materials before the door closes.");
                    if (frame == 34)
                        Assert.That(body.enabled, Is.False, "The small travelling invitation must not expose a clipped head.");
                    if (frame == 90)
                    {
                        Assert.That(body.enabled, Is.True, "Reveal the whole guide after the doorway opens.");
                        var corners = new Vector3[8];
                        var bounds = body.bounds;
                        for (var corner = 0; corner < 8; corner++)
                            corners[corner] = bounds.center + Vector3.Scale(bounds.extents,
                                new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                        var portalMesh = slice.GetComponent<MeshFilter>().sharedMesh.bounds;
                        var portalBottom = camera.WorldToViewportPoint(slice.TransformPoint(portalMesh.center - Vector3.up * portalMesh.extents.y)).y;
                        var portalTop = camera.WorldToViewportPoint(slice.TransformPoint(portalMesh.center + Vector3.up * portalMesh.extents.y)).y;
                        var bodyBottom = corners.Min(point => camera.WorldToViewportPoint(point).y);
                        var bodyTop = corners.Max(point => camera.WorldToViewportPoint(point).y);
                        Assert.That(bodyBottom, Is.GreaterThanOrEqualTo(portalBottom),
                            $"At {(frame + 1) * .1f:F1}s the window clips the authored guide's body below the head.");
                        Assert.That(bodyTop, Is.LessThanOrEqualTo(portalTop), "The window must also contain the guide's ears.");
                    }
                    if (frame == 134)
                    {
                        var aperture = mask.GetComponentInChildren<FairyArrivalFlashlightAperture>();
                        Assert.That(aperture.CurrentStrength, Is.GreaterThan(.95f),
                            "The window must stay open while the guide is still crossing, rather than close before the walk.");
                    }
                }
            }
            finally
            {
                binding.Dispose();
                UnityEngine.Object.DestroyImmediate(platform);
                UnityEngine.Object.DestroyImmediate(floor);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(CollectionCompanionCueKind.Idle, FairyCompanionCueKind.Idle)]
        [TestCase(CollectionCompanionCueKind.WaitAtArtifact, FairyCompanionCueKind.ArtifactWait)]
        [TestCase(CollectionCompanionCueKind.ReturnWithArtifact, FairyCompanionCueKind.ArtifactReturn)]
        [TestCase(CollectionCompanionCueKind.Celebrate, FairyCompanionCueKind.Celebrate)]
        public void CollectionCue_MapsToMatchingFairySemantic(
            CollectionCompanionCueKind collectionKind,
            FairyCompanionCueKind expectedFairyKind)
        {
            var position = new Vector3(2f, 1f, -3f);
            var mapped = VisitorCollectionFairyBinding.MapCue(
                new CollectionCompanionCue(collectionKind, position, 0.6f));

            Assert.That(mapped.Kind, Is.EqualTo(expectedFairyKind));
            Assert.That(
                mapped.WorldPosition,
                Is.EqualTo(expectedFairyKind == FairyCompanionCueKind.Idle ? Vector3.zero : position));
            Assert.That(
                mapped.DurationSeconds,
                Is.EqualTo(expectedFairyKind == FairyCompanionCueKind.Idle ? 0f : 0.6f));
        }

        static SessionToken Commit(ExperienceFlow flow, SceneId sceneId)
        {
            var session = SessionToken.CreateNew();
            var prepared = flow.Prepare(session, sceneId);
            Assert.That(prepared.Succeeded, Is.True);
            using (prepared.Lease)
                prepared.Lease.Commit();
            return session;
        }

        FairyCompanionFeedbackDefinition CompanionFeedback()
            => new FairyCompanionFeedbackDefinition(
                _companionEffectPrefab,
                new Vector3(0f, 0.4f, 0f),
                1f,
                _coachAttentionClip,
                0.55f,
                0.55f,
                6,
                _artifactWaitClip,
                0.3f,
                0.35f,
                4,
                _artifactReturnClip,
                0.6f,
                0.65f,
                8,
                _celebrateClip,
                0.72f,
                1.2f,
                12,
                new[] { _idleLocomotionClip },
                0.16f,
                4f,
                9f,
                0.25f,
                2);

        static GameObject CreateArrivalPrefab()
        {
            var prefab = new GameObject("FairyArrivalTestPrefab");
            prefab.AddComponent<ParticleSystem>();
            return prefab;
        }

        static GameObject CreateMaskPrefab()
        {
            var prefab = new GameObject("FairyArrivalMaskTestPrefab");
            var window = prefab.AddComponent<FairyArrivalOtherWorldWindow>();
            var published = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/FairyArrivalDiscoveryMask.prefab");
            EditorUtility.CopySerialized(published.GetComponent<FairyArrivalResonance>(), prefab.AddComponent<FairyArrivalResonance>());
            EditorUtility.CopySerialized(published.GetComponent<FairyArrivalSoundEvents>(), prefab.AddComponent<FairyArrivalSoundEvents>());
            var settings = new SerializedObject(window);
            var ambience = prefab.AddComponent<AudioSource>();
            ambience.playOnAwake = false;
            settings.FindProperty("_ambience").objectReferenceValue = ambience;
            settings.FindProperty("_ritualSigilMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/BotanicalContract.mat");
            settings.ApplyModifiedPropertiesWithoutUndo();
            var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "FairyPassthroughShell";
            shell.transform.SetParent(prefab.transform, false);
            UnityEngine.Object.DestroyImmediate(shell.GetComponent<Collider>());

            var aperture = new GameObject("OppyDiscoveryFlashlightAperture");
            aperture.transform.SetParent(prefab.transform, false);
            aperture.AddComponent<FairyArrivalFlashlightAperture>();
            var parent = new GameObject("parent").transform;
            parent.SetParent(aperture.transform, false);
            var lightVolumes = new GameObject("LightVolumes").transform;
            lightVolumes.SetParent(parent, false);
            CreateApertureRenderer(
                lightVolumes,
                "LightVolumeA",
                "Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/VRFlashlight.mat");
            CreateApertureRenderer(
                parent,
                "LightconeMesh",
                "Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/FlashlightAperture/LightconeGlow.mat");

            CreateApertureRenderer(
                prefab.transform,
                "OppyOtherWorldEnvironment",
                "Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/OtherWorld/BackgroundEnvironment/materials/innerGroundMat.mat");
            CreateApertureRenderer(
                prefab.transform,
                "OtherWorldPortalStencil",
                "Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/OtherWorldPortalStencil.mat");

            prefab.name = "FairyArrivalMaskTestPrefab";
            return prefab;
        }

        static void CreateApertureRenderer(Transform parent, string name, string materialPath)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Quad);
            child.name = name;
            child.transform.SetParent(parent, false);
            UnityEngine.Object.DestroyImmediate(child.GetComponent<Collider>());
            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
        }

        static GameObject CreateFairyPrefab(string name)
        {
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = name;
            prefab.transform.localScale = Vector3.one * 0.2f;
            UnityEngine.Object.DestroyImmediate(prefab.GetComponent<Collider>());
            return prefab;
        }

        static GameObject[] FindArrivalEffectInstances()
            => UnityEngine.Object.FindObjectsByType<GameObject>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(item => item.name == "FairyArrivalWorldShockwave")
                .ToArray();

        sealed class DescriptorSource : ISceneFlowDescriptorSource
        {
            public bool TryGet(SceneId sceneId, out SceneFlowDescriptor descriptor)
            {
                if (sceneId != FirstScene && sceneId != SecondScene)
                {
                    descriptor = null;
                    return false;
                }

                descriptor = new SceneFlowDescriptor(
                    sceneId,
                    sceneId.Value,
                    string.Empty,
                    string.Empty,
                    Array.Empty<FeaturePageId>());
                return true;
            }
        }

        sealed class PanoramaDescriptorSource : ISceneFlowDescriptorSource
        {
            public bool TryGet(SceneId sceneId, out SceneFlowDescriptor descriptor)
            {
                if (sceneId != FirstScene)
                {
                    descriptor = null;
                    return false;
                }

                descriptor = new SceneFlowDescriptor(
                    sceneId,
                    sceneId.Value,
                    string.Empty,
                    string.Empty,
                    new[] { FeaturePageId.Panorama });
                return true;
            }
        }

        sealed class SuccessfulPanoramaLifecycle : IFeaturePageLifecycle
        {
            public FeaturePageId PageId => FeaturePageId.Panorama;
            public FlowResult Prepare(SessionToken session, SceneId sceneId) => FlowResult.Success;
            public FlowResult Activate(SessionToken session) => FlowResult.Success;
            public FlowResult Deactivate(SessionToken session) => FlowResult.Success;
            public FlowResult Release(SessionToken session) => FlowResult.Success;
        }

        sealed class RecordingFairyController : IFairyController
        {
            public Vector3? LastArrivalOrigin { get; private set; }
            public int OpenCalls { get; private set; }
            public int CloseCalls { get; private set; }
            public int CueCalls { get; private set; }
            public SessionToken OpenedSession { get; private set; }
            public FairyCompanionCue LastCue { get; private set; }
            public List<FairyIntent> Intents { get; } = new List<FairyIntent>();

            public FairyResult Open(SessionToken session, FairyDefinition definition)
            {
                OpenCalls++;
                OpenedSession = session;
                return FairyResult.Success();
            }

            public FairyResult Dispatch(SessionToken session, FairyIntent intent, UnityEngine.Vector3? arrivalOrigin = null)
            {
                Intents.Add(intent);
                LastArrivalOrigin = arrivalOrigin;
                return FairyResult.Success();
            }

            public FairyResult Speak(SessionToken session, FairySpeech speech, Action<FairyResult> completion = null)
            {
                completion?.Invoke(FairyResult.Success());
                return FairyResult.Success();
            }
            public FairyResult CancelSpeech(SessionToken session, Guid requestId) => FairyResult.Success();

            public FairyResult PresentCompanionCue(SessionToken session, FairyCompanionCue cue)
            {
                CueCalls++;
                LastCue = cue;
                return FairyResult.Success();
            }

            public FairyResult SetAmbientAudioSuppressed(SessionToken session, bool suppressed)
                => FairyResult.Success();

            public FairyResult Close(SessionToken session)
            {
                CloseCalls++;
                return FairyResult.Success();
            }

            public IDisposable Observe(IFairyStateSink sink) => EmptyDisposable.Instance;
        }

        sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();
            public void Dispose() { }
        }
    }
}
