using System;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.SpatialAnchorAdmin.Authoring;
using BotanicalGardenQR.SpatialAnchorAdmin.Official;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.Admin
{
    public sealed class SpatialAnchorAdminContractTests
    {
        const string AdminScenePath =
            "Assets/BotanicalGardenQR/Scenes/Admin/SpatialAnchorAdmin.unity";
        const string RetiredAuthoringPrefabPath =
            "Assets/BotanicalGardenQR/SpatialAnchorAdmin/Authoring/Prefabs/SpatialInstallationAuthoring.prefab";
        const string AnchorPrefabPath =
            "Assets/BotanicalGardenQR/SpatialAnchorAdmin/Official/SpatialAnchor/Prefabs/DemoAnchorPrefab.prefab";

        [Test]
        public void ControllerLatch_EmitsOnePlacementEdgeAndOneRecallPerReleaseCycle()
        {
            var latch = new AdminControllerInputLatch();

            var first = latch.Update(false, true, true, 1f, 0.5f);
            var held = latch.Update(false, true, true, 1.6f, 0.5f);
            var stillHeld = latch.Update(false, true, true, 2f, 0.5f);
            latch.Update(false, false, false, 2.1f, 0.5f);
            var secondPress = latch.Update(false, true, true, 3f, 0.5f);
            var secondHold = latch.Update(false, true, true, 3.6f, 0.5f);

            Assert.That(first.PlacementPressed, Is.True);
            Assert.That(held.PlacementPressed, Is.False);
            Assert.That(held.RecallRequested, Is.True);
            Assert.That(stillHeld.RecallRequested, Is.False);
            Assert.That(secondPress.PlacementPressed, Is.True);
            Assert.That(secondHold.RecallRequested, Is.True);
        }

        [Test]
        public void OfficialPanelPlacement_CalculatesAnUprightPoseInFrontOfViewer()
        {
            var pose = OfficialAdminPanelPlacement.CalculateTargetPose(
                new Vector3(1f, 2f, 3f),
                Quaternion.Euler(25f, 90f, 15f),
                1.2f,
                0f,
                0.1f,
                true);

            Assert.That(pose.position.y, Is.EqualTo(2.1f).Within(0.001f));
            Assert.That(Vector3.Dot(pose.rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Distance(new Vector3(1f, 2.1f, 3f), pose.position),
                Is.EqualTo(1.2f).Within(0.001f));
        }

        [Test]
        public void CandidatePoseResolverRejectsEnvironmentMissWithoutDistanceFallback()
        {
            var accepted = AdminCandidatePoseResolver.TryResolve(
                new Ray(Vector3.zero, Vector3.forward),
                false,
                Vector3.zero,
                Vector3.zero,
                out _,
                out var diagnosticTag);

            Assert.That(accepted, Is.False);
            Assert.That(diagnosticTag, Is.EqualTo("admin_candidate.environment_no_hit"));
        }

        [Test]
        public void CandidatePoseResolverKeepsWorldUpAndFacesTheEnvironmentSurface()
        {
            var normal = new Vector3(0f, 0f, -1f);
            var accepted = AdminCandidatePoseResolver.TryResolve(
                new Ray(Vector3.zero, Vector3.forward),
                true,
                new Vector3(1f, 2f, 3f),
                normal,
                out var pose,
                out var diagnosticTag);

            Assert.That(accepted, Is.True);
            Assert.That(pose.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(Vector3.Dot(pose.rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
            Assert.That(Vector3.Dot(pose.rotation * Vector3.forward, normal), Is.GreaterThan(0.999f));
            Assert.That(diagnosticTag, Is.EqualTo("admin_candidate.environment_raycast"));
        }

        [Test]
        public void CandidateFineTuneResolverUsesCandidateRightAndWorldUpWithFrameIndependentSpeed()
        {
            var pose = new Pose(Vector3.zero, Quaternion.identity);

            var deadZone = AdminCandidateFineTuneResolver.ResolveWorldDelta(
                pose, new Vector2(0.1f, 0f), 1f, 0.04f, 0.2f);
            var horizontal = AdminCandidateFineTuneResolver.ResolveWorldDelta(
                pose, Vector2.right, 1f, 0.04f, 0.2f);
            var vertical = AdminCandidateFineTuneResolver.ResolveWorldDelta(
                pose, Vector2.up, 1f, 0.04f, 0.2f);
            var halfFrame = AdminCandidateFineTuneResolver.ResolveWorldDelta(
                pose, Vector2.right, 0.5f, 0.04f, 0.2f);

            Assert.That(deadZone, Is.EqualTo(Vector3.zero));
            Assert.That(Vector3.Distance(horizontal, Vector3.right * 0.04f), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(vertical, Vector3.up * 0.04f), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(halfFrame * 2f, horizontal), Is.LessThan(0.0001f));
        }

        [Test]
        public void CandidatePoseReviewClampsPositionAndPreservesInitialRotation()
        {
            var initial = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 45f, 0f));
            var requested = new Pose(initial.position + Vector3.right, Quaternion.Euler(80f, 10f, 20f));

            var resolved = OfficialAnchorCandidatePoseReview.ResolveBoundedPosition(
                initial,
                requested,
                0.2f);

            Assert.That(Vector3.Distance(initial.position, resolved.position), Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(Quaternion.Angle(initial.rotation, resolved.rotation), Is.LessThan(0.001f));
        }

        [Test]
        public void LoadState_ClampsCountsAndPreservesLifecycleOutcome()
        {
            var loading = new OfficialAnchorLoadState(
                OfficialAnchorLoadPhase.Loading,
                "正在加载",
                -4,
                9);
            var completed = loading.WithCounts(3, 8);

            Assert.That(loading.SavedAnchorCount, Is.Zero);
            Assert.That(loading.TrackedAnchorCount, Is.Zero);
            Assert.That(loading.IsLoading, Is.True);
            Assert.That(completed.Phase, Is.EqualTo(OfficialAnchorLoadPhase.Loading));
            Assert.That(completed.Message, Is.EqualTo("正在加载"));
            Assert.That(completed.SavedAnchorCount, Is.EqualTo(3));
            Assert.That(completed.TrackedAnchorCount, Is.EqualTo(3));
        }

        [Test]
        public void ControllerInput_InterfaceContainsNoRouteAuthoringDependency()
        {
            var inputType = typeof(AdminControllerInput);
            var commandType = typeof(IOfficialSpatialAnchorAdminCommands);
            var bridgeType = typeof(BotanicalOfficialSpatialAnchorAdminBridge);
            var fields = inputType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(fields.Any(field => field.Name.IndexOf(
                "authoringController",
                StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(fields.Single(field => field.Name == "_officialAnchorAdminSource").FieldType,
                Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(fields.Any(field => field.Name == "_manualPlacementDistance"), Is.False,
                "Admin placement must not silently fall back to a fixed distance when the environment ray misses.");
            Assert.That(commandType.IsAssignableFrom(bridgeType), Is.True);
        }

        [Test]
        public void RuntimeAndPresentation_CommunicateOnlyThroughAdminContracts()
        {
            var bridgeFieldTypes = typeof(BotanicalOfficialSpatialAnchorAdminBridge)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.FieldType.Name)
                .ToArray();
            var presentationFieldTypes = typeof(OfficialAdminPanelPresentation)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.FieldType.Name)
                .ToArray();

            Assert.That(bridgeFieldTypes, Does.Not.Contain("OfficialAdminPanelPlacement"));
            Assert.That(bridgeFieldTypes, Does.Not.Contain("AnchorUIManager"));
            Assert.That(bridgeFieldTypes, Does.Not.Contain("SpatialAnchorLoader"));
            Assert.That(presentationFieldTypes, Does.Not.Contain("BotanicalOfficialSpatialAnchorAdminBridge"));
            Assert.That(presentationFieldTypes, Does.Not.Contain("AnchorUIManager"));
            Assert.That(presentationFieldTypes, Does.Not.Contain("SpatialAnchorLoader"));
        }

        [Test]
        public void GuidedPlacement_SeparatesCandidateCreationExplicitSaveAndSavedAnchorRetuning()
        {
            var commandType = typeof(IOfficialSpatialAnchorAdminCommands);
            var begin = commandType.GetMethod("BeginGuidedAnchorPlacement");
            var placeCandidate = commandType.GetMethod("PlaceGuidedAnchorCandidate");
            var commitNewCandidate = commandType.GetMethod("CommitNewAnchorCandidate");
            var legacyPlaceAndSave = commandType.GetMethod("PlaceAndSaveGuidedAnchor");
            var beginRetune = commandType.GetMethod("BeginRetuneHoveredAnchor");
            var readIdentity = commandType.GetMethod("TryGetAnchorEditIdentity");
            var rename = commandType.GetMethod("RenameAnchorBeingAdjusted");
            var commitRetune = commandType.GetMethod("CommitAnchorAdjustment");
            var adjust = commandType.GetMethod("AdjustAnchorAdjustmentPose");
            var readAdjustment = commandType.GetMethod("TryGetAnchorAdjustment");
            var adjustmentEvent = commandType.GetEvent("AnchorAdjustmentChanged");
            var legacyConfirm = commandType.GetMethod("ConfirmGuidedAnchorCandidate");
            var legacyReplace = commandType.GetMethod("ReplaceGuidedAnchorCandidate");
            var assign = commandType.GetMethod("AssignPhysicalAugmentationPoint");
            var select = commandType.GetMethod("SelectHoveredAnchor");
            var eraseSelected = commandType.GetMethod("BeginEraseSelectedAnchor");
            var setEraseMode = commandType.GetMethod("SetEraseMode");
            var eraseHovered = commandType.GetMethod("BeginEraseHoveredAnchor");
            var isEraseMode = commandType.GetProperty("IsEraseMode");
            var phaseType = typeof(GuidedAnchorPlacementPhase);

            Assert.That(begin, Is.Not.Null);
            Assert.That(begin.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(string).MakeByRefType() }));
            Assert.That(placeCandidate, Is.Not.Null);
            Assert.That(placeCandidate.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(Pose), typeof(string).MakeByRefType() }));
            Assert.That(commitNewCandidate, Is.Not.Null);
            Assert.That(commitNewCandidate.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(string).MakeByRefType() }));
            Assert.That(legacyPlaceAndSave, Is.Null,
                "X candidate creation must not expose an API whose contract promises persistence.");
            Assert.That(beginRetune, Is.Not.Null);
            Assert.That(readIdentity, Is.Not.Null);
            Assert.That(rename, Is.Not.Null);
            Assert.That(commitRetune, Is.Not.Null);
            Assert.That(adjust, Is.Not.Null);
            Assert.That(readAdjustment, Is.Not.Null);
            Assert.That(adjustmentEvent, Is.Not.Null);
            Assert.That(legacyConfirm, Is.Null);
            Assert.That(legacyReplace, Is.Null);
            Assert.That(assign, Is.Null);
            Assert.That(select, Is.Null);
            Assert.That(eraseSelected, Is.Null);
            Assert.That(setEraseMode, Is.Not.Null);
            Assert.That(eraseHovered, Is.Not.Null);
            Assert.That(isEraseMode, Is.Not.Null);
            Assert.That(Enum.GetNames(phaseType), Does.Contain("ReviewingNewAnchor"));
            Assert.That(Enum.GetNames(phaseType), Does.Contain("AdjustingSavedAnchor"));
            Assert.That(Enum.GetNames(phaseType), Does.Contain("CleaningReplacedAnchor"));
            Assert.That(Enum.GetNames(phaseType), Does.Not.Contain("PendingBinding"));
            Assert.That(Enum.GetNames(phaseType), Does.Contain("PendingConfigurationRecord"),
                "Failure recovery may retry the generated device record without becoming a normal binding step.");
        }

        [Test]
        public void LoadingSavedAnchorsCanReportConfiguredBindingRefreshFailure()
        {
            var commandType = typeof(IOfficialSpatialAnchorAdminCommands);
            var load = commandType.GetMethod("LoadAnchors");

            Assert.That(load, Is.Not.Null);
            Assert.That(load.GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(string).MakeByRefType() }));
        }

        [Test]
        public void LegacyUuidSourceIsMigrationOnlyAndNoLongerAProductionIndex()
        {
            var legacyIndex = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("AnchorUuidStore", false))
                .FirstOrDefault(candidate => candidate != null);
            var migration = FindType("LegacyAnchorUuidMigrationSource");
            var publicMethods = migration.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Select(method => method.Name)
                .ToArray();

            Assert.That(legacyIndex, Is.Null);
            Assert.That(publicMethods, Does.Contain("ReadCandidates"));
            Assert.That(publicMethods, Does.Contain("CompleteMigration"));
            Assert.That(publicMethods, Does.Not.Contain("Add"));
            Assert.That(publicMethods, Does.Not.Contain("Remove"));
            Assert.That(publicMethods, Does.Not.Contain("Clear"));
        }

        [Test]
        public void PublishedAdminScene_ContainsOnlyTheOfficialAnchorWorkspace()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(AdminScenePath, OpenSceneMode.Single);
                var components = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .Where(component => component != null)
                    .ToArray();
                var manager = SingleNamed(components, "AnchorUIManager");
                var bridge = SingleNamed(
                    components,
                    "BotanicalGardenQR.SpatialAnchorAdmin.Official.BotanicalOfficialSpatialAnchorAdminBridge");
                var presentation = SingleNamed(
                    components,
                    "BotanicalGardenQR.SpatialAnchorAdmin.Official.OfficialAdminPanelPresentation");
                var placement = SingleNamed(
                    components,
                    "BotanicalGardenQR.SpatialAnchorAdmin.Official.OfficialAdminPanelPlacement");
                var input = SingleNamed(
                    components,
                    "BotanicalGardenQR.SpatialAnchorAdmin.Authoring.AdminControllerInput");

                var presentationSerialized = new SerializedObject(presentation);
                Assert.That(presentationSerialized.FindProperty("_officialAnchorAdminSource")
                    .objectReferenceValue, Is.SameAs(bridge));
                Assert.That(presentationSerialized.FindProperty("_workflowStatusText")
                    .objectReferenceValue, Is.Not.Null);
                var panelRoot = presentationSerialized.FindProperty("_panelRoot")
                    .objectReferenceValue as RectTransform;
                var standardActions = presentationSerialized.FindProperty("_standardActionsRoot")
                    .objectReferenceValue as GameObject;
                var candidateActions = presentationSerialized.FindProperty("_candidateActionsRoot")
                    .objectReferenceValue as GameObject;
                Assert.That(panelRoot, Is.Not.Null);
                Assert.That(panelRoot.anchorMin, Is.EqualTo(Vector2.one * 0.5f));
                Assert.That(panelRoot.anchorMax, Is.EqualTo(Vector2.one * 0.5f));
                Assert.That(panelRoot.sizeDelta.y, Is.GreaterThanOrEqualTo(420f));
                var officialCanvas = panelRoot.GetComponentInParent<Canvas>(true);
                Assert.That(officialCanvas, Is.Not.Null);
                Assert.That(officialCanvas.transform.childCount, Is.EqualTo(1),
                    "The official Canvas must not retain a second legacy panel beside the main workspace.");
                Assert.That(officialCanvas.transform.GetChild(0), Is.SameAs(panelRoot));
                var publishedCopies = components.OfType<TMP_Text>()
                    .Select(text => text.text ?? string.Empty)
                    .ToArray();
                Assert.That(publishedCopies.Any(copy => copy.Contains("候选不会自动保存")), Is.False);
                Assert.That(publishedCopies.Any(copy => copy.Contains("放置后自动保存")), Is.False);
                Assert.That(publishedCopies.Any(copy => copy.Contains("按 X 后自动保存")), Is.False);
                Assert.That(publishedCopies.Any(copy => copy.Contains("返回管理员菜单")), Is.False);
                Assert.That(standardActions, Is.Not.Null);
                Assert.That(standardActions.activeSelf, Is.True);
                var createButton = presentationSerialized.FindProperty("_createButton")
                    .objectReferenceValue as Button;
                Assert.That(createButton, Is.Not.Null);
                Assert.That(createButton.gameObject.activeSelf, Is.True,
                    "The single Admin workspace must expose anchor placement directly.");
                Assert.That(createButton.GetComponentInChildren<TMP_Text>(true).text,
                    Is.EqualTo("放置锚点"));
                Assert.That(createButton.onClick.GetPersistentEventCount(), Is.Zero,
                    "Runtime presentation owns the one placement action; the imported sample callback must not bypass it.");
                var loadButton = presentationSerialized.FindProperty("_loadButton")
                    .objectReferenceValue as Button;
                Assert.That(loadButton, Is.Not.Null);
                Assert.That(loadButton.transform.parent, Is.SameAs(standardActions.transform));
                Assert.That(loadButton.GetComponentInChildren<TMP_Text>(true).text,
                    Does.StartWith("查看锚点（"));
                Assert.That(loadButton.onClick.GetPersistentEventCount(), Is.Zero,
                    "Runtime presentation must own loading and count refresh through the Admin interface.");
                var eraseButton = presentationSerialized.FindProperty("_eraseAnchorButton")
                    .objectReferenceValue as Button;
                Assert.That(eraseButton, Is.Not.Null);
                Assert.That(eraseButton.gameObject.activeSelf, Is.True);
                Assert.That(eraseButton.transform.parent, Is.SameAs(standardActions.transform));
                Assert.That(eraseButton.GetComponentInChildren<TMP_Text>(true).text,
                    Is.EqualTo("删除锚点"));
                Assert.That(eraseButton.onClick.GetPersistentEventCount(), Is.Zero,
                    "Runtime presentation must own delete-mode entry and emit only the semantic command.");
                Assert.That(standardActions.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(3),
                    "The one main panel should contain only Place, Load, and Delete Anchor actions.");
                Assert.That(candidateActions, Is.Not.Null);
                Assert.That(candidateActions.transform.parent, Is.SameAs(panelRoot));
                Assert.That(candidateActions.activeSelf, Is.False,
                    "Adjustment actions replace the standard actions only while retuning or recovering a save.");
                var saveCandidateButton = presentationSerialized.FindProperty("_saveCandidateButton")
                    .objectReferenceValue as Button;
                var renameAnchorButton = presentationSerialized.FindProperty("_renameAnchorButton")
                    .objectReferenceValue as Button;
                var anchorIdentityText = presentationSerialized.FindProperty("_anchorIdentityText")
                    .objectReferenceValue as TMP_Text;
                var cancelCandidateButton = presentationSerialized.FindProperty("_cancelCandidateButton")
                    .objectReferenceValue as Button;
                Assert.That(anchorIdentityText, Is.Not.Null);
                Assert.That(anchorIdentityText.transform.parent, Is.SameAs(candidateActions.transform));
                AssertCandidateButton(renameAnchorButton, candidateActions.transform, "修改名称");
                AssertCandidateButton(saveCandidateButton, candidateActions.transform, "保存锚点");
                AssertCandidateButton(cancelCandidateButton, candidateActions.transform, "取消");
                Assert.That(candidateActions.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(3));
                Assert.That(candidateActions.transform.Find("ReplaceAnchorCandidate"), Is.Null);
                var instructionCanvas = presentationSerialized.FindProperty("_controllerHintCanvas")
                    .objectReferenceValue as Canvas;
                var instruction = instructionCanvas.GetComponentInChildren<TMP_Text>(true);
                Assert.That(instruction.text, Does.Contain("长按 Y 取消"));
                Assert.That(instruction.text, Does.Not.Contain("返回菜单"));
                var placementCopy = presentation.GetType().GetField(
                    "PlacementCopy",
                    BindingFlags.Static | BindingFlags.NonPublic)?.GetRawConstantValue() as string;
                Assert.That(placementCopy, Does.Contain("待保存候选"));
                Assert.That(placementCopy, Does.Not.Contain("自动保存"));
                Assert.That(placementCopy, Does.Contain("点击已保存锚点"));
                Assert.That(((RectTransform)instructionCanvas.transform).sizeDelta.y,
                    Is.LessThanOrEqualTo(72f),
                    "The two-line placement hint must not retain the old three-line overlay height.");
                var managerSerialized = new SerializedObject(manager);
                var placementPreview = managerSerialized.FindProperty("_placementPreview")
                    .objectReferenceValue as GameObject;
                Assert.That(placementPreview, Is.Not.Null);
                Assert.That(placementPreview.GetComponentInChildren<TMP_Text>(true).text,
                    Does.Contain("待保存候选"));
                var selectModeButton = managerSerialized.FindProperty("_selectModeButton")
                    .objectReferenceValue as GameObject;
                Assert.That(selectModeButton, Is.Not.Null);
                Assert.That(selectModeButton.activeSelf, Is.False,
                    "The imported sample's create/select mode UI must remain hidden.");
                Assert.That(panelRoot.Find("AnchorCandidateActions"), Is.SameAs(candidateActions.transform),
                    "Candidate actions must remain inside the one official Admin panel, not become a second panel.");
                var bridgeSerialized = new SerializedObject(bridge);
                Assert.That(bridgeSerialized.FindProperty("_lifecycleSource")
                    .objectReferenceValue, Is.SameAs(manager),
                    "Runtime Bridge must consume the UI-neutral lifecycle port, not a concrete panel or loader.");
                Assert.That(new SerializedObject(placement).FindProperty("_officialAnchorAdminSource")
                    .objectReferenceValue, Is.SameAs(bridge),
                    "Frontend placement owns semantic workspace visibility requests.");
                Assert.That(bridgeSerialized.FindProperty("_physicalAugmentationCatalog")
                    .objectReferenceValue, Is.Not.Null,
                    "Project configuration, not an Admin binding page, must determine the anchor's content role.");
                Assert.That(components.Any(component => string.Equals(
                        component.GetType().FullName,
                        "BotanicalGardenQR.SpatialAnchorAdmin.Official.OfficialPhysicalAugmentationAdminPresenter",
                        StringComparison.Ordinal)),
                    Is.False);

                var anchorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AnchorPrefabPath);
                var anchorComponent = anchorPrefab.GetComponents<Component>()
                    .Single(component => component.GetType().Name == "Anchor");
                Assert.That(anchorComponent.GetComponent<Collider>(), Is.Not.Null,
                    "The controller ray must be able to hover the Anchor targeted by delete mode.");
                var anchorMenu = new SerializedObject(anchorComponent)
                    .FindProperty("_anchorMenu").objectReferenceValue as GameObject;
                Assert.That(anchorMenu, Is.Not.Null);
                Assert.That(anchorMenu.activeSelf, Is.False,
                    "Anchor visuals may show state, but must not expose a local Save/Hide/Erase menu.");

                var inputSerialized = new SerializedObject(input);
                Assert.That(inputSerialized.FindProperty("_officialAnchorAdminSource")
                    .objectReferenceValue, Is.SameAs(bridge));
                var placementGuide = managerSerialized.FindProperty("_lineRenderer").objectReferenceValue;
                var panelPointer = inputSerialized.FindProperty("_pointerLine").objectReferenceValue;
                Assert.That(panelPointer, Is.Not.Null);
                Assert.That(panelPointer, Is.Not.SameAs(placementGuide));
                Assert.That(inputSerialized.FindProperty("_environmentRaycastManager")
                    .objectReferenceValue, Is.Not.Null);
                Assert.That(inputSerialized.FindProperty("_candidateFineTuneMetersPerSecond")
                    .floatValue, Is.GreaterThanOrEqualTo(0.08f),
                    "Fine tuning must be visibly responsive while retaining the bounded pose review.");
                Assert.That(inputSerialized.FindProperty("_candidateFineTuneDeadZone")
                    .floatValue, Is.InRange(0f, 0.95f));
                Assert.That(components.Count(component => string.Equals(
                        component.GetType().FullName,
                        "Meta.XR.EnvironmentRaycastManager",
                        StringComparison.Ordinal)),
                    Is.EqualTo(1));
                Assert.That(components.Count(component => string.Equals(
                        component.GetType().Name,
                        "SpatialAnchorLoader",
                        StringComparison.Ordinal)),
                    Is.EqualTo(1), "Admin requires exactly one official Meta anchor loader.");
                Assert.That(components.Any(component => string.Equals(
                        component.GetType().FullName,
                        "BotanicalGardenQR.PhysicalAugmentation.Backend.MetaPhysicalAnchorLocator",
                        StringComparison.Ordinal)),
                    Is.False, "Admin must not compose the visitor physical-anchor locator.");
                Assert.That(components.Any(component => string.Equals(
                        component.GetType().FullName,
                        "Meta.XR.EnvironmentDepth.EnvironmentDepthManager",
                        StringComparison.Ordinal)),
                    Is.False, "Admin authoring does not own the visitor depth manager.");

                var retiredTokens = new[]
                {
                    "RouteAuthoring",
                    "RouteCalibration",
                    "EnvironmentInspection",
                    "SpatialInstallationAuthoring",
                    "NavigationNode",
                    "PublishActive",
                    "PhysicalAugmentationAuthoringPanel",
                    "PhysicalAugmentationAdminPreviewRoot",
                    "OpenPhysicalAugmentationAuthoring",
                    "ConfirmPhysicalBinding"
                };
                var names = components.Select(component => component.transform.name).Distinct().ToArray();
                foreach (var token in retiredTokens)
                    Assert.That(names.Any(name => name.IndexOf(
                        token,
                        StringComparison.OrdinalIgnoreCase) >= 0), Is.False, token);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(RetiredAuthoringPrefabPath), Is.Null);

                var projectConfig = AssetDatabase.LoadMainAssetAtPath("Assets/Oculus/OculusProjectConfig.asset");
                Assert.That(projectConfig, Is.Not.Null);
                Assert.That(new SerializedObject(projectConfig)
                    .FindProperty("requiresSystemKeyboard").boolValue, Is.True,
                    "Anchor renaming requires Meta's official system keyboard Overlay capability.");
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        static bool ReadBool(object value, string propertyName)
            => (bool)value.GetType().GetProperty(propertyName).GetValue(value);

        static void AssertCandidateButton(Button button, Transform expectedParent, string expectedCopy)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.transform.parent, Is.SameAs(expectedParent));
            Assert.That(button.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo(expectedCopy));
            Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero,
                "Runtime presentation must own candidate actions without serialized callbacks.");
        }

        static Type FindType(string fullName)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Missing type '{fullName}'.");
            return type;
        }

        static Component SingleNamed(Component[] components, string fullName)
            => components.Single(component => string.Equals(
                component.GetType().FullName,
                fullName,
                StringComparison.Ordinal));
    }
}
