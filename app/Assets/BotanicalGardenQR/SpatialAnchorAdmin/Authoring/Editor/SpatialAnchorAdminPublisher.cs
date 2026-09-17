using System;
using System.Collections.Generic;
using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.SpatialAnchorAdmin.Official;
using Meta.XR;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BotanicalGardenQR.SpatialAnchorAdmin.Authoring.Editor
{
    public static class SpatialAnchorAdminPublisher
    {
        const string AdminScenePath =
            "Assets/BotanicalGardenQR/Scenes/Admin/SpatialAnchorAdmin.unity";
        const string FontPath =
            "Assets/BotanicalGardenQR/Resources/BotanicalGardenQR/Fonts/SourceHanSansSC-Regular SDF.asset";
        const string PhysicalCatalogPath =
            "Assets/BotanicalGardenQR/Content/Published/PhysicalAugmentationCatalog.asset";
        const float AdminRayDistanceMeters = 8f;
        const float CandidateFineTuneMetersPerSecond = 0.10f;
        const float CandidateFineTuneDeadZone = 0.2f;

        static readonly string[] RetiredRootNames =
        {
            "SpatialInstallationAuthoring",
            "EnvironmentRaycastManager",
            "MRUK"
        };

        [MenuItem("Tools/Botanical Garden/Admin/Publish Spatial Anchor Admin")]
        public static void Publish()
        {
            var scene = EditorSceneManager.OpenScene(AdminScenePath, OpenSceneMode.Single);
            var viewer = SceneComponents<Camera>(scene)
                .FirstOrDefault(camera => camera.CompareTag("MainCamera"))?.transform ??
                         SceneComponents<Camera>(scene)
                             .FirstOrDefault(camera => string.Equals(
                                 camera.name,
                                 "CenterEyeAnchor",
                                 StringComparison.Ordinal))?.transform;
            if (viewer == null)
                throw new InvalidOperationException("Admin scene camera/viewer is missing.");
            var cameraRig = SceneComponents<OVRCameraRig>(scene).SingleOrDefault();
            if (cameraRig == null)
                throw new InvalidOperationException("Admin scene requires exactly one OVRCameraRig.");
            var manager = SceneComponents<AnchorUIManager>(scene).SingleOrDefault();
            var official = SceneComponents<BotanicalOfficialSpatialAnchorAdminBridge>(scene).SingleOrDefault();
            var presentation = SceneComponents<OfficialAdminPanelPresentation>(scene).SingleOrDefault();
            if (manager == null || official == null || presentation == null)
                throw new InvalidOperationException(
                    "Admin scene requires one official manager, bridge, and presentation.");

            RemoveRetiredRoots(scene);
            var panelPlacement = SceneComponents<OfficialAdminPanelPlacement>(scene).SingleOrDefault();
            if (panelPlacement == null)
                throw new InvalidOperationException("Admin scene requires one official panel placement owner.");
            ConfigureOfficialWorkspace(scene, viewer, manager, official, presentation, panelPlacement);
            ConfigureControllerRuntime(scene, cameraRig, manager, official);
            AssertNoRetiredAdminContent(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            PublishApplicationModeGateway();
            Debug.Log("[SpatialAnchorAdmin] Published anchor-only Admin scene.");
        }

        static void PublishApplicationModeGateway()
        {
            const string typeName =
                "BotanicalGardenQR.Bootstrap.Editor.ApplicationModeScenePublisher, " +
                "BotanicalGardenQR.Bootstrap.Editor";
            var publisherType = Type.GetType(typeName, throwOnError: false);
            var publish = publisherType?.GetMethod(
                "Publish",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (publish == null)
                throw new InvalidOperationException(
                    "ApplicationMode scene publisher is unavailable during Admin publication.");
            publish.Invoke(null, null);
        }

        static void ConfigureOfficialWorkspace(
            Scene scene,
            Transform viewer,
            AnchorUIManager manager,
            BotanicalOfficialSpatialAnchorAdminBridge official,
            OfficialAdminPanelPresentation presentation,
            OfficialAdminPanelPlacement panelPlacement)
        {
            var managerSerialized = new SerializedObject(manager);
            var createModeButton = managerSerialized.FindProperty("_createModeButton")
                ?.objectReferenceValue as GameObject;
            var selectModeButton = managerSerialized.FindProperty("_selectModeButton")
                ?.objectReferenceValue as GameObject;
            var canvas = createModeButton != null
                ? createModeButton.GetComponentInParent<Canvas>(true)
                : null;
            if (canvas == null)
                throw new InvalidOperationException("Official anchor manager menu Canvas is missing.");

            var presentationSerialized = new SerializedObject(presentation);
            var panelRoot = presentationSerialized.FindProperty("_panelRoot")
                ?.objectReferenceValue as RectTransform;
            var createButton = presentationSerialized.FindProperty("_createButton")
                ?.objectReferenceValue as Button;
            var loadButton = presentationSerialized.FindProperty("_loadButton")
                ?.objectReferenceValue as Button;
            var instructionCanvas = presentationSerialized.FindProperty("_controllerHintCanvas")
                ?.objectReferenceValue as Canvas;
            if (panelRoot == null || createButton == null || loadButton == null ||
                instructionCanvas == null || instructionCanvas == canvas)
                throw new InvalidOperationException("Official anchor workspace bindings are incomplete.");

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
                throw new InvalidOperationException("Official anchor workspace Canvas requires a RectTransform.");
            canvasRect.sizeDelta = new Vector2(900f, 760f);
            ConfigureCenteredRect(panelRoot, new Vector2(680f, 580f));
            RemoveObsoleteOfficialCanvasChildren(canvas, panelRoot);

            RemoveChildIfPresent(panelRoot, "BotanicalMaintenanceStatus");
            RemoveChildIfPresent(panelRoot, "ResetSpatialData");
            RemoveChildIfPresent(panelRoot, "ReturnToAdminMenu");

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                throw new InvalidOperationException($"Missing Chinese font at '{FontPath}'.");
            var status = EnsureStatus(panelRoot, font);
            var standardActions = EnsureVerticalSection(
                panelRoot,
                "AnchorStandardActions",
                330f,
                14f);
            MoveNamedChildIfPresent(panelRoot, standardActions, createButton.gameObject.name);
            MoveNamedChildIfPresent(panelRoot, standardActions, loadButton.gameObject.name);
            MoveNamedChildIfPresent(panelRoot, standardActions, "EraseSelectedAnchor");
            var legacyEraseButton = standardActions.Find("EraseSelectedAnchor");
            var currentEraseButton = standardActions.Find("EraseAnchor");
            if (legacyEraseButton != null && currentEraseButton != null)
                UnityEngine.Object.DestroyImmediate(legacyEraseButton.gameObject, true);
            else if (legacyEraseButton != null)
                legacyEraseButton.name = "EraseAnchor";
            MoveNamedChildIfPresent(panelRoot, standardActions, "EraseAnchor");
            var eraseAnchorButton = EnsureActionButton(
                standardActions,
                "EraseAnchor",
                "删除锚点",
                font);
            var candidateActions = EnsureVerticalSection(
                panelRoot,
                "AnchorCandidateActions",
                420f,
                14f);
            var anchorIdentity = EnsureAnchorIdentity(candidateActions, font);
            var renameAnchorButton = EnsureActionButton(
                candidateActions,
                "RenameAnchor",
                "修改名称",
                font);
            var saveCandidateButton = EnsureActionButton(
                candidateActions,
                "SaveAnchorCandidate",
                "保存锚点",
                font);
            var cancelCandidateButton = EnsureActionButton(
                candidateActions,
                "CancelAnchorCandidate",
                "取消微调",
                font);
            RemoveChildIfPresent(candidateActions, "ReplaceAnchorCandidate");
            candidateActions.gameObject.SetActive(false);
            RemoveChildIfPresent(standardActions, "ManualPhysicalCoarsePlacement");
            RemoveChildIfPresent(standardActions, "OpenPhysicalAugmentationAuthoring");
            RemoveChildIfPresent(panelRoot.parent, "PhysicalAugmentationAuthoringPanel");
            var previewRoot = scene.GetRootGameObjects()
                .FirstOrDefault(candidate => candidate.name == "PhysicalAugmentationAdminPreviewRoot");
            if (previewRoot != null) UnityEngine.Object.DestroyImmediate(previewRoot, true);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(canvas.gameObject);
            createButton.gameObject.SetActive(true);
            createButton.onClick = new Button.ButtonClickedEvent();
            loadButton.onClick = new Button.ButtonClickedEvent();
            eraseAnchorButton.onClick = new Button.ButtonClickedEvent();
            renameAnchorButton.onClick = new Button.ButtonClickedEvent();
            saveCandidateButton.onClick = new Button.ButtonClickedEvent();
            cancelCandidateButton.onClick = new Button.ButtonClickedEvent();
            var createLabel = createButton.GetComponentInChildren<TMP_Text>(true);
            if (createLabel != null) createLabel.text = "放置锚点";
            if (selectModeButton != null) selectModeButton.SetActive(false);

            SetObjectReference(presentation, "_officialAnchorAdminSource", official);
            SetObjectReference(presentation, "_workflowStatusText", status);
            SetObjectReference(presentation, "_standardActionsRoot", standardActions.gameObject);
            SetObjectReference(presentation, "_eraseAnchorButton", eraseAnchorButton);
            SetObjectReference(presentation, "_candidateActionsRoot", candidateActions.gameObject);
            SetObjectReference(presentation, "_anchorIdentityText", anchorIdentity);
            SetObjectReference(presentation, "_renameAnchorButton", renameAnchorButton);
            SetObjectReference(presentation, "_saveCandidateButton", saveCandidateButton);
            SetObjectReference(presentation, "_cancelCandidateButton", cancelCandidateButton);
            SetObjectReference(official, "_lifecycleSource", manager);
            SetObjectReference(panelPlacement, "_officialAnchorAdminSource", official);
            var catalog = AssetDatabase.LoadAssetAtPath<PhysicalAugmentationCatalogAsset>(PhysicalCatalogPath);
            if (catalog == null)
                throw new InvalidOperationException($"Missing published PhysicalAugmentation catalog at '{PhysicalCatalogPath}'.");
            SetObjectReference(official, "_physicalAugmentationCatalog", catalog);
            createButton.transform.SetSiblingIndex(0);
            loadButton.transform.SetSiblingIndex(1);
            eraseAnchorButton.transform.SetSiblingIndex(2);
            status.rectTransform.SetSiblingIndex(0);
            standardActions.SetSiblingIndex(1);
            candidateActions.SetSiblingIndex(2);
            anchorIdentity.rectTransform.SetSiblingIndex(0);
            renameAnchorButton.transform.SetSiblingIndex(1);
            saveCandidateButton.transform.SetSiblingIndex(2);
            cancelCandidateButton.transform.SetSiblingIndex(3);

            var placementSerialized = new SerializedObject(panelPlacement);
            placementSerialized.FindProperty("_viewer").objectReferenceValue = viewer;
            placementSerialized.FindProperty("_distance").floatValue = 1.15f;
            placementSerialized.FindProperty("_horizontalOffset").floatValue = 0f;
            placementSerialized.FindProperty("_verticalOffset").floatValue = 0.12f;
            placementSerialized.FindProperty("_summonOnEnable").boolValue = true;
            placementSerialized.ApplyModifiedPropertiesWithoutUndo();

            ConfigureControllerCanvas(canvas, viewer.GetComponent<Camera>());
            var placementInstruction = managerSerialized.FindProperty("_placementInstructionCanvas");
            if (placementInstruction == null)
                throw new InvalidOperationException("Official manager placement instruction binding is missing.");
            placementInstruction.objectReferenceValue = instructionCanvas;
            managerSerialized.ApplyModifiedPropertiesWithoutUndo();
            instructionCanvas.enabled = false;
            foreach (var raycaster in instructionCanvas.GetComponents<BaseRaycaster>())
                UnityEngine.Object.DestroyImmediate(raycaster, true);
            foreach (var graphic in instructionCanvas.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            var placementPreview = managerSerialized.FindProperty("_placementPreview")
                ?.objectReferenceValue as GameObject;
            var placementPreviewText = placementPreview != null
                ? placementPreview.GetComponentInChildren<TMP_Text>(true)
                : null;
            if (placementPreviewText == null)
                throw new InvalidOperationException(
                    "Official anchor placement preview is missing its instruction label.");
            placementPreviewText.text =
                "坐标标记 = 锚点位置\n" +
                "按 X 创建待保存候选；点击已保存锚点可微调";
            placementPreviewText.raycastTarget = false;
            EditorUtility.SetDirty(placementPreviewText);
            PrefabUtility.RecordPrefabInstancePropertyModifications(placementPreviewText);

            createModeButton.name = "Create Mode Button";
            if (selectModeButton != null) selectModeButton.name = "Exit Create Mode";
            presentation.Apply();
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(official);
            EditorUtility.SetDirty(presentation);
            EditorUtility.SetDirty(panelPlacement);
            EditorUtility.SetDirty(canvas);
            EditorUtility.SetDirty(instructionCanvas);
        }

        static RectTransform EnsureVerticalSection(
            RectTransform parent,
            string name,
            float height,
            float spacing)
        {
            var existing = parent.Find(name);
            var root = existing != null
                ? existing.gameObject
                : new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup),
                    typeof(LayoutElement));
            if (existing == null) root.transform.SetParent(parent, false);
            root.layer = parent.gameObject.layer;
            var group = root.GetComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            var layout = root.GetComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            return root.GetComponent<RectTransform>();
        }

        static void ConfigureCenteredRect(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = Vector2.one * 0.5f;
            rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.sizeDelta = size;
        }

        static void RemoveObsoleteOfficialCanvasChildren(Canvas canvas, RectTransform panelRoot)
        {
            for (var index = canvas.transform.childCount - 1; index >= 0; index--)
            {
                var child = canvas.transform.GetChild(index);
                if (child == panelRoot) continue;
                UnityEngine.Object.DestroyImmediate(child.gameObject, true);
            }
        }

        static void MoveNamedChildIfPresent(
            RectTransform source,
            RectTransform destination,
            string childName)
        {
            var child = source.Find(childName);
            if (child != null && child != destination)
                child.SetParent(destination, false);
        }

        static void ConfigureControllerRuntime(
            Scene scene,
            OVRCameraRig cameraRig,
            AnchorUIManager manager,
            BotanicalOfficialSpatialAnchorAdminBridge official)
        {
            RemoveComprehensiveInteractionRuntime(scene, cameraRig);
            ConfigureAdminControllerTracking(scene);
            foreach (var hand in SceneComponents<OVRHand>(scene).ToArray())
                UnityEngine.Object.DestroyImmediate(hand, true);

            var leftController = cameraRig.GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(transform => string.Equals(
                    transform.name,
                    AdminControllerControlMap.ControllerAnchorName,
                    StringComparison.Ordinal));
            if (leftController == null)
                throw new InvalidOperationException(
                    $"Official OVRCameraRig is missing '{AdminControllerControlMap.ControllerAnchorName}'.");

            var managerSerialized = new SerializedObject(manager);
            var placementPose = managerSerialized.FindProperty("_anchorPlacementTransform")
                ?.objectReferenceValue as Transform;
            var placementGuide = managerSerialized.FindProperty("_lineRenderer")
                ?.objectReferenceValue as LineRenderer;
            if (placementPose == null || placementGuide == null)
                throw new InvalidOperationException(
                    "Official AnchorUIManager placement transform or guide is missing.");

            var placementPrefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(placementPose.gameObject);
            var placementRoot = placementPrefabRoot != null
                ? placementPrefabRoot.transform
                : placementPose.root;
            if (placementRoot == cameraRig.transform || placementRoot.parent == null)
                throw new InvalidOperationException(
                    "Official anchor placement root could not be isolated for left-controller binding.");
            placementRoot.SetParent(leftController, false);
            placementRoot.localPosition = Vector3.zero;
            placementRoot.localRotation = Quaternion.identity;
            placementRoot.localScale = Vector3.one;
            managerSerialized.FindProperty("_trackedDevice").objectReferenceValue = leftController;
            managerSerialized.ApplyModifiedPropertiesWithoutUndo();

            var eventSystems = SceneComponents<EventSystem>(scene).ToArray();
            if (eventSystems.Length > 1)
                throw new InvalidOperationException($"Admin scene contains {eventSystems.Length} EventSystems.");
            EventSystem eventSystem;
            if (eventSystems.Length == 0)
            {
                var eventSystemObject = new GameObject("AdminControllerEventSystem", typeof(EventSystem));
                SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }
            else
            {
                eventSystem = eventSystems[0];
            }

            eventSystem.gameObject.name = "AdminControllerEventSystem";
            RemoveLegacyInteractionComponents(eventSystem.gameObject);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(eventSystem.gameObject);
            var focusRing = EnsureControllerFocusRing(scene, eventSystem.transform);
            var pointerLine = EnsureControllerPointerLine(scene, eventSystem.transform, placementGuide);

            var inputs = SceneComponents<AdminControllerInput>(scene).ToArray();
            for (var index = 0; index < inputs.Length; index++)
                if (inputs[index].gameObject != eventSystem.gameObject)
                    UnityEngine.Object.DestroyImmediate(inputs[index], true);
            var input = eventSystem.GetComponent<AdminControllerInput>() ??
                        eventSystem.gameObject.AddComponent<AdminControllerInput>();
            var environmentRaycastManagers = SceneComponents<EnvironmentRaycastManager>(scene).ToArray();
            if (environmentRaycastManagers.Length > 1)
                throw new InvalidOperationException(
                    $"Admin scene contains {environmentRaycastManagers.Length} EnvironmentRaycastManager components; expected at most one.");
            EnvironmentRaycastManager environmentRaycastManager;
            if (environmentRaycastManagers.Length == 0)
            {
                var environmentObject = new GameObject(
                    "PhysicalAugmentationEnvironmentRaycast",
                    typeof(EnvironmentRaycastManager));
                SceneManager.MoveGameObjectToScene(environmentObject, scene);
                environmentRaycastManager = environmentObject.GetComponent<EnvironmentRaycastManager>();
            }
            else
            {
                environmentRaycastManager = environmentRaycastManagers[0];
                environmentRaycastManager.gameObject.name = "PhysicalAugmentationEnvironmentRaycast";
            }
            SetObjectReference(input, "_rayOrigin", leftController);
            SetObjectReference(input, "_placementPose", placementPose);
            SetObjectReference(input, "_eventSystem", eventSystem);
            SetObjectReference(input, "_officialAnchorAdminSource", official);
            SetObjectReference(input, "_pointerLine", pointerLine);
            SetObjectReference(input, "_focusRing", focusRing);
            SetObjectReference(input, "_environmentRaycastManager", environmentRaycastManager);
            var inputSerialized = new SerializedObject(input);
            inputSerialized.FindProperty("_maximumRayDistance").floatValue = AdminRayDistanceMeters;
            inputSerialized.FindProperty("_candidateFineTuneMetersPerSecond").floatValue =
                CandidateFineTuneMetersPerSecond;
            inputSerialized.FindProperty("_candidateFineTuneDeadZone").floatValue =
                CandidateFineTuneDeadZone;
            inputSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(input);
            EditorUtility.SetDirty(environmentRaycastManager);
            EditorUtility.SetDirty(manager);
        }

        static void RemoveRetiredRoots(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects().ToArray())
            {
                if (RetiredRootNames.Any(name => root.name.StartsWith(name, StringComparison.Ordinal)))
                    UnityEngine.Object.DestroyImmediate(root, true);
            }
        }

        static void AssertNoRetiredAdminContent(Scene scene)
        {
            var retiredTokens = new[]
            {
                "RouteAuthoring",
                "RouteCalibration",
                "EnvironmentInspection",
                "SpatialInstallationAuthoring",
                "NavigationNode",
                "PublishActive"
            };
            foreach (var transform in SceneComponents<Transform>(scene))
                for (var index = 0; index < retiredTokens.Length; index++)
                    if (transform.name.IndexOf(retiredTokens[index], StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new InvalidOperationException(
                            $"Admin scene still contains retired navigation object '{transform.name}'.");
            foreach (var text in SceneComponents<TMP_Text>(scene))
                if (text.text.IndexOf("放置后自动保存", StringComparison.Ordinal) >= 0 ||
                    text.text.IndexOf("按 X 后自动保存", StringComparison.Ordinal) >= 0 ||
                    text.text.IndexOf("返回管理员菜单", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException(
                        $"Admin scene still contains retired placement copy on '{text.name}'.");
        }

        static TMP_Text EnsureStatus(RectTransform parent, TMP_FontAsset font)
        {
            var existing = parent.Find("AnchorWorkflowStatus");
            var root = existing != null
                ? existing.gameObject
                : new GameObject(
                    "AnchorWorkflowStatus",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI),
                    typeof(LayoutElement));
            if (existing == null) root.transform.SetParent(parent, false);
            root.layer = parent.gameObject.layer;
            var text = root.GetComponent<TextMeshProUGUI>() ??
                       throw new InvalidOperationException(
                           "AnchorWorkflowStatus is missing TextMeshProUGUI.");
            text.font = font;
            text.text = "放置需命中现实表面；按 X 只创建待保存候选。点击已保存锚点可查看名称并微调位置。";
            text.fontSize = 17f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            var layout = root.GetComponent<LayoutElement>() ??
                         throw new InvalidOperationException(
                             "AnchorWorkflowStatus is missing LayoutElement.");
            layout.minHeight = 82f;
            layout.preferredHeight = 82f;
            return text;
        }

        static TMP_Text EnsureAnchorIdentity(RectTransform parent, TMP_FontAsset font)
        {
            var existing = parent.Find("SelectedAnchorIdentity");
            var root = existing != null
                ? existing.gameObject
                : new GameObject(
                    "SelectedAnchorIdentity",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI),
                    typeof(LayoutElement));
            if (existing == null) root.transform.SetParent(parent, false);
            root.layer = parent.gameObject.layer;
            var text = root.GetComponent<TextMeshProUGUI>() ??
                       throw new InvalidOperationException(
                           "SelectedAnchorIdentity is missing TextMeshProUGUI.");
            text.font = font;
            text.text = "当前选择：锚点 01";
            text.fontSize = 20f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            var layout = root.GetComponent<LayoutElement>() ??
                         throw new InvalidOperationException(
                             "SelectedAnchorIdentity is missing LayoutElement.");
            layout.minHeight = 54f;
            layout.preferredHeight = 54f;
            layout.flexibleHeight = 0f;
            return text;
        }

        static Button EnsureActionButton(
            RectTransform parent,
            string name,
            string label,
            TMP_FontAsset font)
        {
            var existing = parent.Find(name);
            var root = existing != null
                ? existing.gameObject
                : new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button),
                    typeof(LayoutElement));
            if (existing == null) root.transform.SetParent(parent, false);
            root.layer = parent.gameObject.layer;

            var image = root.GetComponent<Image>() ?? root.AddComponent<Image>();
            var button = root.GetComponent<Button>() ?? root.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            var labelTransform = root.transform.Find("Label");
            var labelObject = labelTransform != null
                ? labelTransform.gameObject
                : new GameObject(
                    "Label",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
            if (labelTransform == null) labelObject.transform.SetParent(root.transform, false);
            labelObject.layer = root.layer;
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(16f, 8f);
            labelRect.offsetMax = new Vector2(-16f, -8f);
            var text = labelObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = label;
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return button;
        }

        static void ConfigureControllerCanvas(Canvas canvas, Camera worldCamera)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = worldCamera;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 500;
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            foreach (var component in canvas.GetComponents<Component>())
            {
                if (component == null || component is Transform || component is Canvas ||
                    component is CanvasScaler || component is GraphicRaycaster)
                    continue;
                var name = component.GetType().FullName ?? string.Empty;
                if (name == "OVRRaycaster" || name == "Oculus.Interaction.PointableCanvas")
                    UnityEngine.Object.DestroyImmediate(component, true);
            }
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(canvas.gameObject);
        }

        static AdminPointerRingGraphic EnsureControllerFocusRing(Scene scene, Transform parent)
        {
            const string ringName = "AdminControllerFocusRing";
            var named = SceneComponents<Transform>(scene)
                .Where(transform => string.Equals(transform.name, ringName, StringComparison.Ordinal))
                .ToArray();
            var ringTransform = named.OfType<RectTransform>()
                                    .FirstOrDefault(transform => transform.parent == parent) ??
                                named.OfType<RectTransform>().FirstOrDefault();
            if (ringTransform == null)
            {
                foreach (var transform in named)
                    UnityEngine.Object.DestroyImmediate(transform.gameObject, true);
                var ringObject = new GameObject(
                    ringName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(AdminPointerRingGraphic));
                SceneManager.MoveGameObjectToScene(ringObject, scene);
                ringTransform = ringObject.GetComponent<RectTransform>();
            }
            foreach (var transform in named)
                if (transform != null && transform != ringTransform)
                    UnityEngine.Object.DestroyImmediate(transform.gameObject, true);

            ringTransform.SetParent(parent, false);
            ringTransform.localPosition = Vector3.zero;
            ringTransform.localRotation = Quaternion.identity;
            ringTransform.localScale = Vector3.one;
            ringTransform.anchorMin = Vector2.one * 0.5f;
            ringTransform.anchorMax = Vector2.one * 0.5f;
            ringTransform.pivot = Vector2.one * 0.5f;
            ringTransform.sizeDelta = Vector2.one;
            ringTransform.gameObject.layer = parent.gameObject.layer;
            foreach (var line in ringTransform.GetComponents<LineRenderer>())
                UnityEngine.Object.DestroyImmediate(line, true);
            foreach (var renderer in ringTransform.GetComponents<MeshRenderer>())
                UnityEngine.Object.DestroyImmediate(renderer, true);
            foreach (var filter in ringTransform.GetComponents<MeshFilter>())
                UnityEngine.Object.DestroyImmediate(filter, true);
            foreach (var sorting in ringTransform.GetComponents<SortingGroup>())
                UnityEngine.Object.DestroyImmediate(sorting, true);
            foreach (var collider in ringTransform.GetComponents<Collider>())
                UnityEngine.Object.DestroyImmediate(collider, true);

            var ring = ringTransform.GetComponent<AdminPointerRingGraphic>() ??
                       ringTransform.gameObject.AddComponent<AdminPointerRingGraphic>();
            ring.material = Graphic.defaultGraphicMaterial;
            ring.color = new Color(0.72f, 0.78f, 0.82f, 0.9f);
            ring.raycastTarget = false;
            ring.enabled = false;
            EditorUtility.SetDirty(ring);
            return ring;
        }

        static LineRenderer EnsureControllerPointerLine(
            Scene scene,
            Transform parent,
            LineRenderer template)
        {
            const string pointerName = "AdminControllerPointerLine";
            var named = SceneComponents<Transform>(scene)
                .Where(transform => string.Equals(transform.name, pointerName, StringComparison.Ordinal))
                .ToArray();
            var pointerTransform = named.FirstOrDefault(transform => transform.parent == parent) ??
                                   named.FirstOrDefault();
            if (pointerTransform == null)
            {
                var pointerObject = new GameObject(pointerName, typeof(LineRenderer));
                SceneManager.MoveGameObjectToScene(pointerObject, scene);
                pointerTransform = pointerObject.transform;
            }
            foreach (var transform in named)
                if (transform != null && transform != pointerTransform)
                    UnityEngine.Object.DestroyImmediate(transform.gameObject, true);

            pointerTransform.SetParent(parent, false);
            pointerTransform.localPosition = Vector3.zero;
            pointerTransform.localRotation = Quaternion.identity;
            pointerTransform.localScale = Vector3.one;
            pointerTransform.gameObject.layer = parent.gameObject.layer;
            var pointerLine = pointerTransform.GetComponent<LineRenderer>() ??
                              pointerTransform.gameObject.AddComponent<LineRenderer>();
            EditorUtility.CopySerialized(template, pointerLine);
            pointerLine.useWorldSpace = true;
            pointerLine.positionCount = 2;
            pointerLine.SetPosition(0, Vector3.zero);
            pointerLine.SetPosition(1, Vector3.forward);
            pointerLine.enabled = false;
            EditorUtility.SetDirty(pointerLine);
            return pointerLine;
        }

        static void ConfigureAdminControllerTracking(Scene scene)
        {
            var managers = SceneComponents<OVRManager>(scene).ToArray();
            if (managers.Length != 1)
                throw new InvalidOperationException(
                    $"Admin scene requires exactly one OVRManager; found {managers.Length}.");
            var serialized = new SerializedObject(managers[0]);
            serialized.FindProperty("SimultaneousHandsAndControllersEnabled").boolValue = false;
            serialized.FindProperty("launchSimultaneousHandsControllersOnStartup").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(managers[0]);
        }

        static void RemoveComprehensiveInteractionRuntime(Scene scene, OVRCameraRig cameraRig)
        {
            var rigRefs = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .Where(component => component != null && string.Equals(
                    component.GetType().FullName,
                    "Oculus.Interaction.Input.OVRCameraRigRef",
                    StringComparison.Ordinal))
                .ToArray();
            foreach (var rigRef in rigRefs)
            {
                var root = rigRef.transform;
                while (root.parent != null && root.parent != cameraRig.transform) root = root.parent;
                if (root.parent == cameraRig.transform)
                    UnityEngine.Object.DestroyImmediate(root.gameObject, true);
            }
        }

        static void RemoveLegacyInteractionComponents(GameObject target)
        {
            foreach (var component in target.GetComponents<Component>())
            {
                if (component == null || component is Transform || component is EventSystem ||
                    component is AdminControllerInput)
                    continue;
                var name = component.GetType().FullName ?? string.Empty;
                if (name == "Oculus.Interaction.PointableCanvasModule" ||
                    name == "BotanicalGardenQR.SpatialAnchorAdmin.Authoring.AdminPointableSelectionFallback" ||
                    name == "BotanicalGardenQR.SpatialAnchorAdmin.Authoring.AdminInteractionRuntimeGuard" ||
                    name == "BotanicalGardenQR.SpatialAnchorAdmin.Authoring.AdminGazeGestureInput")
                    UnityEngine.Object.DestroyImmediate(component, true);
            }
        }

        static void RemoveChildIfPresent(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject, true);
        }

        static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    $"{target.GetType().Name} is missing serialized property '{propertyName}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static IEnumerable<T> SceneComponents<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }
}
