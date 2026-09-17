using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.KnowledgeMiniGame.Backend;
using BotanicalGardenQR.KnowledgeMiniGame.Contracts;
using BotanicalGardenQR.KnowledgeMiniGame.Frontend;
using BotanicalGardenQR.VisitorCoach.Frontend;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.MapNavigation.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    public static class VisitorGamePresentationCapture
    {
        const string PrefabRoot = "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs";
        const string VisitorRuntimePath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab";
        const string FairyArrivalEffectPath =
            "Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/OppyArrivalEffect.prefab";
        const string FairyPrefabPath =
            "Assets/BotanicalGardenQR/Content/Shared/Fairy/Models/Oppy/OppyFairyGuide.prefab";
        const string FairyArrivalMaskPath =
            "Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/FairyArrivalDiscoveryMask.prefab";
        const string FairyCompanionEffectPath =
            "Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/OppyParticles/OppySparkles.prefab";
        const string CollectionCatalogPath =
            "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset";
        const string VisitorCoachPrefabPath =
            "Assets/BotanicalGardenQR/Modules/VisitorCoach/Frontend/Prefabs/VisitorCoachPresentation.prefab";
        const string VisitorCoachThemePath =
            "Assets/BotanicalGardenQR/Content/Authoring/VisitorCoachTheme.asset";
        const string GlobalUiDefaultsPath =
            "Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset";

        public static void Run()
        {
            RunCaptures(includeInterfaceSurfaces: true, includeFairyEffects: true);
        }

        public static void RunInterfaceOnly()
        {
            RunCaptures(includeInterfaceSurfaces: true, includeFairyEffects: false);
        }

        public static void RunFairyOnly()
        {
            RunCaptures(includeInterfaceSurfaces: false, includeFairyEffects: true);
        }

        static void PrepareFoldout(GameObject root, float progress)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BotanicalGardenQR.Configuration.Runtime.CollectionCatalogAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            var record = catalog.Artifacts.Single(x => x.Matches(new SceneId("ceiba")));
            var foldout = root.GetComponent<BotanicalGardenQR.Collection.Frontend.FieldbookFoldout>();
            foldout.Configure(record.FoldoutImage, record.FoldoutDetailImage);
            foldout.Preview(progress);
        }

        static void RunCaptures(bool includeInterfaceSurfaces, bool includeFairyEffects)
        {
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new InvalidOperationException(
                        "Presentation capture requires a graphics device; do not launch Unity with -nographics.");

                var arguments = Environment.GetCommandLineArgs();
                var outputArgument = Array.IndexOf(arguments, "-bgqrCaptureOutput");
                var output = Path.GetFullPath(outputArgument >= 0 && outputArgument + 1 < arguments.Length
                    ? arguments[outputArgument + 1]
                    : Path.Combine(Path.GetTempPath(), "BGQR-Presentation-" + Guid.NewGuid().ToString("N")));
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if ((output + Path.DirectorySeparatorChar).StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Presentation evidence must remain outside the project.");
                Directory.CreateDirectory(output);
                if (includeInterfaceSurfaces)
                {
                CaptureInvitation(output, "01-fieldbook-invitation.png", 0f);
                CaptureInvitation(output, "01a-book-gathering.png", 0f, .2f);
                CaptureInvitation(output, "01b-book-forming.png", 0f, .7f);
                CaptureInvitation(output, "01c-book-ready.png", 0f, 1.4f);
                CaptureInvitation(output, "02-fieldbook-opening.png", .45f);
                CaptureFairyDialogue(
                    output,
                    "02c-fairy-dialogue.png",
                    VisitorDialogueSurfaceMode.Dialogue,
                    0);
                CaptureFairyDialogue(
                    output,
                    "02d-fairy-dialogue-final.png",
                    VisitorDialogueSurfaceMode.Dialogue,
                    -1);
                CaptureFairyDialogue(
                    output,
                    "02e-fairy-dialogue-replay.png",
                    VisitorDialogueSurfaceMode.Replay,
                    -1);
                CaptureKnowledgeChallenges(output);
                CaptureFairyDialogue(output, "02f-tool-introduction.png", VisitorDialogueSurfaceMode.Dialogue, 0, true);
                CaptureFairyDialogue(output, "02g-tool-try-palm.png", VisitorDialogueSurfaceMode.Replay, 0, true);
                CaptureFairyDialogue(output, "02p-encounter-replies.png", VisitorDialogueSurfaceMode.Dialogue, 0, encounterPage: 1);
                CaptureFairyDialogue(output, "02q-curious-response.png", VisitorDialogueSurfaceMode.Dialogue, 0, encounterPage: 2);
                CaptureFairyDialogue(output, "02r-companion-response.png", VisitorDialogueSurfaceMode.Dialogue, 0, encounterPage: 2, companionReply: true);
                CaptureFairyDialogue(output, "02k-ready-to-explore.png", VisitorDialogueSurfaceMode.Dialogue, 0, true, readyToExplore: true);
                CaptureFairyDialogue(output, "02s-fieldbook-discovery.png", VisitorDialogueSurfaceMode.Dialogue, 0, guidance: VisitorGuidanceSurfaceKind.Discovery);
                CaptureFairyDialogue(output, "02n-guidance-unavailable.png", VisitorDialogueSurfaceMode.Dialogue, 0, guidance: VisitorGuidanceSurfaceKind.Unavailable);
                Capture(VisitorCoachPrefabPath, Path.Combine(output, "02o-guidance-route.png"), (root, camera) =>
                {
                    root.SetActive(false);
                    var definition = VisitorMapConfiguration.Resolve(AssetDatabase.LoadAssetAtPath<TextAsset>(
                        "Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json"));
                    var frame = new MapFrame(default, 0, definition.scale);
                    var path = Array.ConvertAll(definition.routes[0].samples, frame.Transform);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/BotanicalGardenQR/Content/Shared/MapNavigation/GuidanceRoute.mat");
                    var routeRoot = new GameObject("GuidanceRouteCapture");
                    camera.transform.position = new Vector3(-2, 5, -4);
                    camera.transform.LookAt(new Vector3(2, 0, 2));
                    var atlas = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/VisitorAtlasHubPresentation.prefab")
                        .GetComponent<BotanicalGardenQR.VisitorAtlasHub.Frontend.VisitorAtlasHubPresentation>();
                    MapRoutePresentationFactory.Create(routeRoot.transform, material, atlas.MapLabelFont, camera.transform)
                        .Present(path, true, true, "巨人柱");
                }, Vector3.zero, 55f);
                Capture(
                    PrefabRoot + "/CollectionWorldPresentation.prefab",
                    Path.Combine(output, "04-collection-reward.png"),
                    root =>
                    {
                        SetActive(root, "WorldRoot", true);
                        SetActive(root, "ArtifactOffer", false);
                        SetActive(root, "RewardMode", true);
                        SetActive(root, "PageSeal", false);
                        SetActive(root, "BrowseMode", false);
                    },
                    new Vector3(0f, 0.02f, -1.18f),
                    43f);
                Capture(
                    PrefabRoot + "/CollectionWorldPresentation.prefab",
                    Path.Combine(output, "05-collection-compendium.png"),
                    root =>
                    {
                        SetActive(root, "WorldRoot", true);
                        SetActive(root, "ArtifactOffer", false);
                        SetActive(root, "RewardMode", false);
                        SetActive(root, "PageSeal", false);
                        SetActive(root, "BrowseMode", true);
                        SetActive(root, "Compendium", true);
                        SetActive(root, "Detail", false);
                    },
                    new Vector3(0f, 0.03f, -1.38f),
                    43f);
                Capture(
                    PrefabRoot + "/VisitorAtlasHubPresentation.prefab",
                    Path.Combine(output, "05b-atlas-physical-menu.png"),
                    root =>
                    {
                        SetActive(root, "VisualRoot", true);
                        SetActive(root, "EntryChoices", true);
                        SetActive(root, "MapSurface", false);
                        var close = root.GetComponentsInChildren<Transform>(true)
                            .Single(value => string.Equals(value.name, "CloseRoot", StringComparison.Ordinal));
                        close.localPosition = new Vector3(0f, -0.28f, 0.02f);
                        close.localRotation = Quaternion.identity;
                    },
                    new Vector3(0f, 0f, -1.25f),
                    40f);
                Capture(
                    "Assets/BotanicalGardenQR/Content/Shared/Fieldbook/FieldbookPage.prefab",
                    Path.Combine(output, "06-fieldbook-page.png"),
                    root => PrepareFoldout(root, 0f),
                    new Vector3(0f, 0f, -0.56f),
                    38f);
                Capture("Assets/BotanicalGardenQR/Content/Shared/Fieldbook/FieldbookPage.prefab",
                    Path.Combine(output, "06d-fieldbook-page-oblique.png"),
                    root => { PrepareFoldout(root, 1f); root.transform.rotation = Quaternion.Euler(-12, 38, -6); },
                    new Vector3(0, 0, -.56f), 38f);
                foreach (var progress in new[] { .35f, .7f, 1f })
                    Capture("Assets/BotanicalGardenQR/Content/Shared/Fieldbook/FieldbookPage.prefab",
                        Path.Combine(output, "06-fieldbook-open-" + Mathf.RoundToInt(progress * 100) + ".png"),
                        root => PrepareFoldout(root, progress), new Vector3(.1f, 0, -.65f), 38f);
                CaptureArtifactFeedback(output, "06b-artifact-ready.png", false);
                CaptureArtifactFeedback(output, "06c-artifact-placement-valid.png", true);
                Capture(
                    VisitorRuntimePath,
                    Path.Combine(output, "07-close-decision.png"),
                    (root, camera) =>
                    {
                        var closeDecision = root.GetComponentsInChildren<Transform>(true)
                            .Single(value => string.Equals(value.name, "CloseDecisionPopup", StringComparison.Ordinal));
                        foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                            if (canvas.transform != closeDecision && !closeDecision.IsChildOf(canvas.transform))
                                canvas.gameObject.SetActive(false);

                        var controller = root.GetComponentInChildren<StartupRecallPresenter>(true);
                        if (controller == null)
                            throw new InvalidOperationException("VisitorRuntime is missing StartupRecallPresenter.");
                        controller.Prime(camera.transform, "请扫描二维码");
                        controller.OnStateChanged(new RecallState(
                            version: 1,
                            isClosed: true,
                            canRecall: true,
                            hasPreviousContent: true));
                    },
                    new Vector3(0f, 0f, -1.1f),
                    40f);
                Capture(
                    VisitorRuntimePath,
                    Path.Combine(output, "07b-model-page-reality-action.png"),
                    (root, _) => ConfigureModelPagePreview(root),
                    new Vector3(0f, 0f, -1.24f),
                    42f);
                Capture(
                    VisitorRuntimePath,
                    Path.Combine(output, "07c-reality-focus-return.png"),
                    (root, _) => ConfigureRealityFocusPreview(root),
                    new Vector3(0f, 0f, -1.24f),
                    42f);
                }
                if (includeFairyEffects)
                {
                    CaptureFairyArrival(output, "08-fairy-arrival-0080ms.png", 0.08f);
                    CaptureFairyArrival(output, "09-fairy-arrival-0220ms.png", 0.22f);
                    CaptureFairyArrival(output, "10-fairy-arrival-0400ms.png", 0.4f);
                    CaptureFairyArrival(output, "10b-fairy-arrival-0900ms.png", .9f);
                    CaptureFairyArrival(output, "10c-fairy-arrival-1500ms.png", 1.5f);
                    Capture(FairyPrefabPath, Path.Combine(output, "arrival-sequence-final.png"), (instance, camera) =>
                    {
                        var resourceTool = Type.GetType("BotanicalGardenQR.Editor.Build.OppyFairyAssetBuilder, Assembly-CSharp-Editor", true);
                        resourceTool.GetMethod("CaptureArrivalSequence", BindingFlags.Public | BindingFlags.Static)
                            .Invoke(null, new object[] { instance, camera, output });
                    }, new Vector3(0, .7f, -2.1f), 64f);
                    CaptureFairyDiscoveryAperture(output);
                    CaptureFairyCompanionFeedback(output);
                    CaptureGroundMotionSequence(output);
                }
                Debug.Log("VISITOR_GAME_PRESENTATION_CAPTURE_OK " + output);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        static void CaptureGroundMotionSequence(string output)
        {
            Capture(FairyPrefabPath, Path.Combine(output, "ground-motion-final.png"), (instance, camera) =>
            {
                var resourceTool = Type.GetType("BotanicalGardenQR.Editor.Build.OppyFairyAssetBuilder, Assembly-CSharp-Editor", true);
                resourceTool.GetMethod("CaptureGroundMotion", BindingFlags.Public | BindingFlags.Static)
                    .Invoke(null, new object[] { instance, camera, output });
            }, new Vector3(0, 1.65f, -1.2f), 62f);
        }

        static void CaptureKnowledgeChallenges(string outputDirectory)
        {
            var library = AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>(
                "Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset");
            if (library == null)
                throw new InvalidOperationException("Knowledge preview requires the published content library.");
            var source = (IKnowledgeMiniGameDefinitionSource)new PublishedSceneResolver(library);
            var captured = false;
            foreach (var package in library.Packages)
            {
                if (!source.TryGet(package.SceneId, out var definition) ||
                    definition.Kind != KnowledgeMiniGameKind.SingleChoice)
                    continue;
                foreach (var phase in new[]
                         { KnowledgeMiniGamePhase.Ready, KnowledgeMiniGamePhase.Incorrect, KnowledgeMiniGamePhase.Completed })
                {
                    var outputPath = Path.Combine(outputDirectory, $"03-{package.SceneId}-{phase}.png");
                    Capture(
                        PrefabRoot + "/ObservationCompletionSurface.prefab",
                        outputPath,
                        (root, camera) =>
                        {
                            var frontend = root.GetComponent<KnowledgeMiniGameFrontend>();
                            if (frontend == null)
                                throw new InvalidOperationException("Knowledge preview requires the production presenter.");
                            using var controller = KnowledgeMiniGameModuleFactory.Create();
                            var session = SessionToken.CreateNew();
                            frontend.Configure(camera.transform, new CaptureGazeSurfaceRegistry());
                            frontend.Bind(session, controller);
                            controller.Open(session, definition);
                            frontend.SetVisible(true);
                            if (phase == KnowledgeMiniGamePhase.Incorrect)
                            {
                                var question = definition.GetQuestion(0);
                                controller.Submit(session, question.Options.First(
                                    option => !question.IsCorrect(option.AnswerId)).AnswerId);
                            }
                            else if (phase == KnowledgeMiniGamePhase.Completed)
                            {
                                foreach (var question in definition.Questions)
                                    controller.Submit(session, question.CorrectAnswerId);
                            }
                        },
                        new Vector3(0f, 0f, -1.34f),
                        60f);
                    if (!captured && phase == KnowledgeMiniGamePhase.Ready)
                        File.Copy(outputPath, Path.Combine(outputDirectory, "03-observation-completion.png"), true);
                }
                captured = true;
            }
            if (!captured) throw new InvalidOperationException("No published knowledge questions were captured.");
        }

        sealed class CaptureGazeSurfaceRegistry : IFrontendGazeSurfaceRegistry
        {
            public System.IDisposable SuspendPanelInput() => new Suspension();
            sealed class Suspension : System.IDisposable { public void Dispose() { } }
            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform surfaceRoot, int priority, string label)
                => new CaptureGazeRegistration();
        }

        sealed class CaptureGazeRegistration : IFrontendGazeSurfaceRegistration
        {
            public void Invalidate() { }

            public bool IsFocused => false;
            public void Dispose() { }
        }

        static void CaptureFairyArrival(string outputDirectory, string fileName, float simulationTime)
        {
            Capture(
                FairyArrivalEffectPath,
                Path.Combine(outputDirectory, fileName),
                (root, _) =>
                {
                    var particles = root.GetComponent<ParticleSystem>();
                    if (particles == null)
                        throw new InvalidOperationException("Fairy arrival preview requires a root ParticleSystem.");
                    particles.Simulate(simulationTime, true, true, false);
                    var guide = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FairyPrefabPath), root.transform) as GameObject;
                    if (guide == null) throw new InvalidOperationException("Arrival preview guide is missing.");
                    guide.transform.localRotation = Quaternion.Euler(0, 180, 0);
                },
                new Vector3(0f, 0.18f, -1.4f),
                42f);
        }

        static void ConfigureModelPagePreview(GameObject root)
        {
            var shell = root.GetComponentInChildren<GlobalFrontendShell>(true);
            if (shell == null)
                throw new InvalidOperationException("VisitorRuntime is missing GlobalFrontendShell.");
            var shellCanvas = shell.GetComponent<Canvas>();
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas != shellCanvas) canvas.gameObject.SetActive(false);

            var slots = new SerializedObject(shell).FindProperty("_slots");
            SetSlot(slots, "_shellRoot", true);
            SetSlot(slots, "_headerSlot", false);
            SetSlot(slots, "_actionSlot", false);
            SetSlot(slots, "_statusSlot", true);
            SetSlot(slots, "_closeSlot", false);
            SetSlot(slots, "_overlaySlot", false);
            SetSlot(slots, "_narrationDockSlot", false);
            SetSlot(slots, "_panoramaExitSlot", false);
            SetSlot(slots, "_featureFocusExitSlot", false);
            SetSlot(slots, "_videoStageRoot", false);
            SetSlot(slots, "_videoControlRoot", false);
            SetSlot(slots, "_modelStageRoot", true);
            SetSlot(slots, "_modelControlRoot", true);
            var modelControlRoot = slots.FindPropertyRelative("_modelControlRoot")
                ?.objectReferenceValue as Transform;
            if (modelControlRoot?.parent != null)
                modelControlRoot.parent.gameObject.SetActive(true);
            SetText(slots, "_status", "位置已准备好，可以开始现实演示。");

            foreach (var target in shell.GetComponentsInChildren<ShellFlowActionTarget>(true))
            {
                var visible = target.Action == ShellFlowAction.BackToMain ||
                              target.Action == ShellFlowAction.FeaturePageAction;
                target.gameObject.SetActive(visible);
                if (target.Action == ShellFlowAction.FeaturePageAction)
                    target.ApplyFeaturePageAction(true, "现实演示");
            }
        }

        static void ConfigureRealityFocusPreview(GameObject root)
        {
            var shell = root.GetComponentInChildren<GlobalFrontendShell>(true);
            if (shell == null)
                throw new InvalidOperationException("VisitorRuntime is missing GlobalFrontendShell.");
            var shellCanvas = shell.GetComponent<Canvas>();
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas != shellCanvas) canvas.gameObject.SetActive(false);

            var slots = new SerializedObject(shell).FindProperty("_slots");
            SetSlot(slots, "_shellRoot", false);
            SetSlot(slots, "_panoramaExitSlot", false);
            SetSlot(slots, "_featureFocusExitSlot", true);
            foreach (var target in shell.GetComponentsInChildren<ShellFlowActionTarget>(true))
            {
                var visible = target.Action == ShellFlowAction.FeaturePageActionExitFocus;
                target.gameObject.SetActive(visible);
                if (visible) target.ApplyFeaturePageAction(true, "返回面板");
            }
        }

        static void SetSlot(SerializedProperty slots, string name, bool active)
        {
            var transform = slots.FindPropertyRelative(name)?.objectReferenceValue as Transform;
            if (transform != null) transform.gameObject.SetActive(active);
        }

        static void SetText(SerializedProperty slots, string name, string value)
        {
            var text = slots.FindPropertyRelative(name)?.objectReferenceValue as TMPro.TMP_Text;
            if (text != null) text.text = value;
        }

        static void CaptureFairyDiscoveryAperture(string outputDirectory)
        {
            Capture(
                FairyPrefabPath,
                Path.Combine(outputDirectory, "11-fairy-discovery-aperture-alpha.png"),
                (root, camera) =>
                {
                    camera.farClipPlane = 1000f;
                    root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    var maskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FairyArrivalMaskPath);
                    if (maskPrefab == null)
                        throw new InvalidOperationException("Fairy arrival discovery mask is missing.");
                    var mask = PrefabUtility.InstantiatePrefab(maskPrefab, root.transform) as GameObject;
                    if (mask == null)
                        throw new InvalidOperationException("Could not instantiate the Fairy arrival discovery mask.");
                    var aperture = mask.GetComponentsInChildren<MonoBehaviour>(true)
                        .Single(value => value.GetType().Name == "FairyArrivalFlashlightAperture");
                    var initialize = aperture.GetType().GetMethod(
                        "Initialize",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (initialize == null)
                        throw new InvalidOperationException("Fairy flashlight aperture has no Initialize method.");
                    initialize.Invoke(aperture, new object[] { camera.transform, root.transform });
                    aperture.GetType().GetMethod("SetOpeningProgress", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(aperture, new object[] { 1f });

                    var setFlickerTime = aperture.GetType().GetMethod(
                        "SetFlickerTime",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var otherWorld = mask.GetComponentsInChildren<MonoBehaviour>(true)
                        .Single(value => value.GetType().Name == "FairyArrivalOtherWorldWindow");
                    var initializeOtherWorld = otherWorld.GetType().GetMethod(
                        "Initialize",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var showCharacter = otherWorld.GetType().GetMethod(
                        "ShowCharacterInWindow",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var setStrength = otherWorld.GetType().GetMethod(
                        "SetStrength",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (initializeOtherWorld == null || showCharacter == null ||
                        setFlickerTime == null || setStrength == null)
                        throw new InvalidOperationException("Fairy other-world preview methods are incomplete.");
                    initializeOtherWorld.Invoke(otherWorld, new object[] { root.transform, aperture });
                    var strength = (float)setFlickerTime.Invoke(aperture, new object[] { 1f });
                    setStrength.Invoke(otherWorld, new object[] { strength });
                    showCharacter.Invoke(otherWorld, Array.Empty<object>());
                    foreach (var particles in mask.GetComponentsInChildren<ParticleSystem>())
                        particles.Simulate(1.1f, false, true, false);
                },
                new Vector3(0f, 0.32f, -1.4f),
                42f,
                visualizeAlpha: true);
        }

        static void CaptureArtifactFeedback(
            string outputDirectory,
            string fileName,
            bool placementAccepted)
        {
            Capture(
                "Assets/BotanicalGardenQR/Content/Shared/Fieldbook/FieldbookPage.prefab",
                Path.Combine(outputDirectory, fileName),
                root =>
                {
                    var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(CollectionCatalogPath);
                    if (catalog == null)
                        throw new InvalidOperationException(
                            "Artifact feedback preview requires the production Collection catalog.");
                    if (!catalog.TryValidatePresentation(out var error))
                        throw new InvalidOperationException(
                            "Artifact feedback preview requires a valid Collection catalog: " + error);
                    var relay = root.GetComponent<CollectionArtifactGrabRelay>();
                    if (relay == null)
                        throw new InvalidOperationException(
                            "Artifact feedback preview requires CollectionArtifactGrabRelay.");
                    relay.Configure(
                        "artifact_capture",
                        new CollectionInstanceToken("artifact-capture-instance"),
                        catalog.PresentationTheme.ArtifactMotion,
                        new Color(1f, 0.61f, 0.24f, 1f),
                        (_, __) => { },
                        () => { }, new CaptureGazeSurfaceRegistry());
                    relay.SetInteractionEnabled(true);
                    if (placementAccepted) relay.PresentPlacementResult(true);
                },
                new Vector3(0f, 0f, -0.56f),
                38f);
        }

        static void CaptureInvitation(string outputDirectory, string fileName, float openingSeconds, float appearSeconds = 1.5f)
        {
            Capture(PrefabRoot + "/VisitorProloguePresentation.prefab", Path.Combine(outputDirectory, fileName),
                (root, camera) =>
                {
                    var theme = AssetDatabase.LoadAssetAtPath<VisitorPrologueThemeAsset>(
                        "Assets/BotanicalGardenQR/Content/Authoring/VisitorPrologueTheme.asset");
                    SetActive(root, "PresentationRoot", true);
                    SetActive(root, "StartPanel", false);
                    SetActive(root, "FieldbookInvitationRoot", true);
                    SetActive(root, "Button_GuideGazeFallback", false);
                    var ritual = root.GetComponentInChildren<VisitorPrologue.Frontend.FieldbookInvitationRitual>(true);
                    ritual.Configure(theme);
                    ritual.PresentAvailable();
                    typeof(VisitorPrologue.Frontend.FieldbookInvitationRitual).GetMethod("Tick",
                        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ritual, new object[] { appearSeconds });
                    RequiredNamedText(root, "GuideHint").text = openingSeconds > 0 ? theme.Copy.ArrivalTitle : theme.Copy.InvitationTitle;
                    RequiredNamedText(root, "GuideSubHint").text = openingSeconds > 0 ? theme.Copy.ArrivalDetail : theme.Copy.InvitationDetail;
                    if (openingSeconds > 0)
                    {
                        ritual.PresentOpening();
                        typeof(VisitorPrologue.Frontend.FieldbookInvitationRitual).GetMethod("Tick",
                            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ritual, new object[] { openingSeconds });
                    }
                }, new Vector3(0f, 0f, -.85f), 48f);
        }

        static void CaptureFairyDialogue(
            string outputDirectory,
            string fileName,
            VisitorDialogueSurfaceMode mode,
            int requestedPageIndex,
            bool toolPreparation = false,
            bool readyToExplore = false,
            VisitorGuidanceSurfaceKind? guidance = null, int encounterPage = -1, bool companionReply = false)
        {
            Capture(
                VisitorCoachPrefabPath,
                Path.Combine(outputDirectory, fileName),
                (root, camera) =>
                {
                    var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(VisitorCoachThemePath);
                    var uiDefaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(GlobalUiDefaultsPath);
                    var presenter = root.GetComponent<VisitorCoachPresenter>();
                    if (theme == null || uiDefaults == null || presenter == null)
                        throw new InvalidOperationException("Fairy dialogue preview dependencies are incomplete.");

                    var cueKey = toolPreparation ? VisitorCoach.Contracts.VisitorCoachCueKeys.PalmRecall :
                        VisitorCoach.Contracts.VisitorCoachCueKeys.QrConfirm;
                    var pageCount = readyToExplore ? 1 : theme.GetDialoguePageCount(cueKey);
                    var pageIndex = requestedPageIndex < 0 ? pageCount - 1 : requestedPageIndex;
                    var body = string.Empty;
                    if (mode == VisitorDialogueSurfaceMode.Dialogue &&
                        !readyToExplore &&
                        !theme.TryResolveDialoguePage(cueKey, pageIndex, out body))
                        throw new InvalidOperationException(
                            $"Fairy dialogue preview has no QR page {pageIndex + 1}.");
                    if (toolPreparation) { pageCount = 1; pageIndex = 0; body = theme.ToolPreparationIntroduction; }
                    if (readyToExplore) body = theme.ToolPreparationSuccessCopy + "\n" + theme.ToolPreparationReadyCopy;

                    presenter.Configure(camera.transform, theme, uiDefaults.SharedFont, new CaptureGazeSurfaceRegistry());
                    presenter.SetInputMode(VisitorDialogueInputMode.HandPoke);
                    theme.TryResolveCopy(cueKey, VisitorCoach.Contracts.VisitorCoachHintLevel.Initial, out var practiceHint);
                    presenter.Present(new VisitorDialogueSurfaceState(
                        1,
                        new VisitorDialogueContextId($"capture:first-qr:{mode}"),
                        toolPreparation ? VisitorDialogueOwner.ToolPreparation : VisitorDialogueOwner.Coach,
                        mode,
                        readyToExplore ? "我们的第一站" : toolPreparation ? "同行 · 见闻册与卷轴" : "第一站 · 开启学习",
                        theme.FairySpeakerName,
                        body,
                        pageIndex,
                        pageCount,
                        toolPreparation ? theme.ToolPreparationPracticeCopy : "跟随小精灵到站，食指触碰“开启发现”。", toolPreparation && mode == VisitorDialogueSurfaceMode.Replay,
                        readyToExplore ? theme.ToolPreparationStartLabel :
                            toolPreparation ? (mode == VisitorDialogueSurfaceMode.Replay ? "再告诉我一次" : "让我试试") : null,
                        allowRestart: !toolPreparation));
                    if (encounterPage >= 0)
                    {
                        var copy = AssetDatabase.LoadAssetAtPath<VisitorPrologueThemeAsset>("Assets/BotanicalGardenQR/Content/Authoring/VisitorPrologueTheme.asset").Copy;
                        copy.TryGetEncounterPage(encounterPage, out var encounterBody);
                        if (encounterPage == 2) encounterBody = companionReply ? copy.CompanionResponse : copy.CuriousResponse;
                        presenter.Present(new VisitorDialogueSurfaceState(2, new VisitorDialogueContextId("capture:encounter"),
                            VisitorDialogueOwner.Prologue, VisitorDialogueSurfaceMode.Dialogue, "相遇 · 见闻之约", copy.FairySpeakerName,
                            encounterBody, encounterPage, copy.EncounterPageCount,
                            primaryActionLabel: encounterPage == 1 ? copy.CuriousReplyLabel : "继续  ›",
                            secondaryActionLabel: encounterPage == 1 ? copy.CompanionReplyLabel : null,
                            primaryIntent: encounterPage == 1 ? VisitorDialogueIntentKind.ChoosePrimary : VisitorDialogueIntentKind.Advance,
                            secondaryIntent: encounterPage == 1 ? VisitorDialogueIntentKind.ChooseSecondary : VisitorDialogueIntentKind.Replay,
                            expression: companionReply ? VisitorDialogueExpression.Welcome : VisitorDialogueExpression.Wonder));
                    }

                    if (guidance.HasValue) presenter.PresentGuidance(new VisitorGuidanceSurface(guidance.Value, "P02"));
                    presenter.Tick(10f);

                    if (mode == VisitorDialogueSurfaceMode.Dialogue)
                    {
                        var fairyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FairyPrefabPath);
                        var fairy = fairyPrefab != null
                            ? PrefabUtility.InstantiatePrefab(fairyPrefab) as GameObject
                            : null;
                        if (fairy == null)
                            throw new InvalidOperationException("Fairy dialogue preview requires the production Fairy prefab.");
                        var forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
                        var right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
                        var target = camera.transform.position + forward * theme.ViewerDistance +
                                     right * theme.FairyDialogueHorizontalOffset.x +
                                     forward * theme.FairyDialogueHorizontalOffset.y;
                        target.y = 0f;
                        var look = Vector3.ProjectOnPlane(camera.transform.position - target, Vector3.up).normalized;
                        fairy.transform.SetPositionAndRotation(target, Quaternion.LookRotation(look, Vector3.up));
                    }
                },
                new Vector3(0f, 1.65f, -1.2f),
                52f);
        }

        static void CaptureFairyCompanionFeedback(string outputDirectory)
        {
            Capture(
                FairyPrefabPath,
                Path.Combine(outputDirectory, "12-fairy-companion-celebrate.png"),
                (root, _) =>
                {
                    root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    var effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FairyCompanionEffectPath);
                    if (effectPrefab == null)
                        throw new InvalidOperationException("Fairy companion feedback prefab is missing.");
                    var effect = PrefabUtility.InstantiatePrefab(effectPrefab, root.transform) as GameObject;
                    if (effect == null)
                        throw new InvalidOperationException("Could not instantiate Fairy companion feedback.");
                    effect.transform.localPosition = effectPrefab.transform.localPosition +
                                                     new Vector3(0f, 0.4f, 0f);
                    effect.transform.localRotation = effectPrefab.transform.localRotation;
                    effect.transform.localScale = effectPrefab.transform.localScale;
                    var particles = effect.GetComponent<ParticleSystem>();
                    if (particles == null)
                        throw new InvalidOperationException(
                            "Fairy companion feedback preview requires a root ParticleSystem.");
                    particles.Simulate(0.9f, true, true, false);
                    particles.Emit(12);
                    particles.Simulate(0.15f, true, false, false);
                },
                new Vector3(0f, 0.32f, -1.4f),
                42f);
        }

        static void Capture(
            string prefabPath,
            string outputPath,
            Action<GameObject> configure,
            Vector3 cameraPosition,
            float fieldOfView)
            => Capture(
                prefabPath,
                outputPath,
                (root, _) => configure(root),
                cameraPosition,
                fieldOfView);

        static void Capture(
            string prefabPath,
            string outputPath,
            Action<GameObject, Camera> configure,
            Vector3 cameraPosition,
            float fieldOfView,
            bool visualizeAlpha = false)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidOperationException("Preview prefab is missing: " + prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            var cameraObject = new GameObject("PreviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = cameraPosition;
            camera.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.032f, 0.035f, visualizeAlpha ? 0f : 1f);
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20f;
            camera.allowHDR = true;
            foreach (var canvas in instance.GetComponentsInChildren<Canvas>(true)) canvas.worldCamera = camera;
            configure(instance, camera);

            var keyObject = new GameObject("KeyLight");
            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.85f, 1f, 0.93f);
            key.intensity = 1.35f;
            key.transform.rotation = Quaternion.Euler(32f, -28f, 0f);
            var rimObject = new GameObject("RimLight");
            var rim = rimObject.AddComponent<Light>();
            rim.type = LightType.Point;
            rim.color = new Color(0.2f, 0.85f, 1f);
            rim.intensity = 2.1f;
            rim.range = 4f;
            rim.transform.position = new Vector3(-0.65f, 0.55f, -0.4f);

            // Inspect fieldbook materials under neutral light: the usual cyan rim
            // otherwise overwhelms the small page at this close camera distance.
            if (Path.GetFileName(outputPath).Contains("fieldbook"))
            {
                key.color = Color.white;
                key.intensity = 1f;
                rim.color = Color.white;
                rim.intensity = .12f;
            }

            Canvas.ForceUpdateCanvases();
            var texture = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                name = "VisitorGamePresentationPreview"
            };
            texture.Create();
            var previous = RenderTexture.active;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, false);
            image.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
            image.Apply(false, false);
            if (visualizeAlpha) VisualizeEyeBufferAlpha(image);
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(image);
            texture.Release();
            Object.DestroyImmediate(texture);
        }

        static void VisualizeEyeBufferAlpha(Texture2D texture)
        {
            // Approximate composition over a neutral passthrough frame so the alpha-only
            // flashlight slices remain visible in a desktop capture.
            var pixels = texture.GetPixels32();
            for (var index = 0; index < pixels.Length; index++)
            {
                var source = pixels[index];
                var alpha = source.a / 255f;
                source.r = (byte)Mathf.RoundToInt(Mathf.Lerp(58f, source.r, alpha));
                source.g = (byte)Mathf.RoundToInt(Mathf.Lerp(62f, source.g, alpha));
                source.b = (byte)Mathf.RoundToInt(Mathf.Lerp(58f, source.b, alpha));
                source.a = 255;
                pixels[index] = source;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        static void SetActive(GameObject root, string name, bool active)
        {
            var target = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => string.Equals(value.name, name, StringComparison.Ordinal));
            if (target == null) throw new InvalidOperationException($"Preview target '{name}' is missing from {root.name}.");
            target.gameObject.SetActive(active);
            var group = target.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = active ? 1f : 0f;
                group.interactable = active;
                group.blocksRaycasts = active;
            }
        }

        static T RequiredNamedComponent<T>(GameObject root, string name) where T : Component
        {
            var component = root.GetComponentsInChildren<T>(true)
                .SingleOrDefault(value => string.Equals(value.name, name, StringComparison.Ordinal));
            if (component == null)
                throw new InvalidOperationException(
                    $"Preview component '{name}' ({typeof(T).Name}) is missing from {root.name}.");
            return component;
        }

        static TMPro.TMP_Text RequiredNamedText(GameObject root, string name)
            => RequiredNamedComponent<TMPro.TMP_Text>(root, name);
    }
}
