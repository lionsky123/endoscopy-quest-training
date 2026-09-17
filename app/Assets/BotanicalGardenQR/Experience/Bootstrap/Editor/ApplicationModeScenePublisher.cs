using System;
using System.Collections.Generic;
using System.Linq;
using BotanicalGardenQR.Activation.Adapters;
using BotanicalGardenQR.ApplicationMode.Adapters;
using BotanicalGardenQR.ApplicationMode.Contracts;
using BotanicalGardenQR.ApplicationMode.Editor;
using BotanicalGardenQR.ApplicationMode.Frontend;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.PhysicalAugmentation.Backend;
using Meta.XR.EnvironmentDepth;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    public static class ApplicationModeScenePublisher
    {
        const string VisitorScenePath =
            "Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity";
        const string AdminScenePath =
            "Assets/BotanicalGardenQR/Scenes/Admin/SpatialAnchorAdmin.unity";
        const string VisitorRuntimePrefabPath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab";
        const string PhysicalCatalogPath =
            "Assets/BotanicalGardenQR/Content/Published/PhysicalAugmentationCatalog.asset";
        const string HandsFirstTrackerRelativePath =
            "Interactors/ActiveStatesTrackers/Hand and No Controller";

        [MenuItem("Tools/Botanical Garden/Application Mode/Publish Gateway To Build Scenes")]
        public static void Publish()
        {
            RequireSafeScenePublishingContext();
            RemoveRetiredVisitorRuntimeComponents();
            ConfigurePhysicalAugmentationVisitorPrefab();
            ApplicationModeGatewayPrefabPublisher.Publish();
            var gatewayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ApplicationModeGatewayPrefabPublisher.PrefabPath);
            if (gatewayPrefab == null)
                throw new InvalidOperationException("ApplicationMode gateway Prefab was not published.");

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                PublishVisitor(gatewayPrefab);
                PublishAdmin(gatewayPrefab);
                AssetDatabase.SaveAssets();
                Debug.Log("[ApplicationMode] Gateway published to Visitor and Admin build scenes.");
            }
            finally
            {
                if (setup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        static void RemoveRetiredVisitorRuntimeComponents()
        {
            var root = PrefabUtility.LoadPrefabContents(VisitorRuntimePrefabPath);
            try
            {
                var objects = root.GetComponentsInChildren<Transform>(true)
                    .Select(transform => transform.gameObject)
                    .ToArray();
                var missingCount = objects.Sum(
                    GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
                if (missingCount > 1)
                    throw new InvalidOperationException(
                        $"Visitor runtime contains {missingCount} missing scripts; automatic cleanup is limited to the single retired spatial-navigation locator.");
                if (missingCount == 0) return;

                foreach (var gameObject in objects)
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, VisitorRuntimePrefabPath);
                Debug.Log("[ApplicationMode] Removed the retired Visitor spatial-navigation locator from the runtime Prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ConfigurePhysicalAugmentationVisitorPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(VisitorRuntimePrefabPath);
            try
            {
                var installer = root.GetComponentsInChildren<VisitorInstaller>(true).SingleOrDefault();
                if (installer == null)
                    throw new InvalidOperationException("VisitorRuntime prefab requires exactly one VisitorInstaller.");
                var catalog = AssetDatabase.LoadAssetAtPath<PhysicalAugmentationCatalogAsset>(PhysicalCatalogPath);
                if (catalog == null)
                    throw new InvalidOperationException($"Missing PhysicalAugmentation catalog at '{PhysicalCatalogPath}'.");

                var runtimeRoot = EnsureChild(root.transform, "PhysicalAugmentationRuntime");
                var host = RequireAtMostOneOrAdd<PhysicalAugmentationRuntimeHost>(root, runtimeRoot.gameObject);
                var locatorRoot = EnsureChild(runtimeRoot, "AnchorLocator");
                var locator = RequireAtMostOneOrAdd<MetaPhysicalAnchorLocator>(root, locatorRoot.gameObject);
                var performanceRoot = EnsureChild(runtimeRoot, "PerformanceInstances");
                var depthRoot = EnsureChild(runtimeRoot, "EnvironmentDepth");
                var depth = RequireAtMostOneOrAdd<EnvironmentDepthManager>(root, depthRoot.gameObject);
                depth.enabled = true;
                depth.OcclusionShadersMode = OcclusionShadersMode.SoftOcclusion;
                var permissionRoot = EnsureChild(root.transform, "SpatialDataPermission");
                var permissionGate = RequireAtMostOneOrAdd<MetaSpatialDataPermissionGate>(
                    root,
                    permissionRoot.gameObject);

                ConfigureModelFeaturePageAction(root, installer);

                SetObjectReference(host, "_locator", locator);
                SetObjectReference(host, "_performanceRoot", performanceRoot);
                SetObjectReference(host, "_environmentDepthManager", depth);
                SetObjectReference(installer, "_physicalAugmentationCatalog", catalog);
                SetObjectReference(installer, "_physicalAugmentationRuntimeHost", host);
                SetObjectReference(installer, "_spatialDataPermissionGate", permissionGate);

                AssertNoVisitorAnchorVisuals(locatorRoot);
                EditorUtility.SetDirty(host);
                EditorUtility.SetDirty(locator);
                EditorUtility.SetDirty(depth);
                EditorUtility.SetDirty(permissionGate);
                EditorUtility.SetDirty(installer);
                PrefabUtility.SaveAsPrefabAsset(root, VisitorRuntimePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Transform EnsureChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing;
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static void ConfigureModelFeaturePageAction(GameObject root, VisitorInstaller installer)
        {
            var installerSerialized = new SerializedObject(installer);
            var shell = installerSerialized.FindProperty("_frontendShell")?.objectReferenceValue as GlobalFrontendShell;
            if (shell == null)
                throw new InvalidOperationException("VisitorRuntime requires a GlobalFrontendShell binding.");

            var featurePages = installerSerialized.FindProperty("_featurePages");
            var modelFrontend = featurePages?.FindPropertyRelative("_model")?.objectReferenceValue;
            if (modelFrontend == null)
                throw new InvalidOperationException("VisitorRuntime requires its authored Model frontend binding.");
            var modelSerialized = new SerializedObject(modelFrontend);
            var controlsRoot = modelSerialized.FindProperty("_controlsRoot")?.objectReferenceValue as GameObject;
            var autoMotion = modelSerialized.FindProperty("_autoMotionButton")?.objectReferenceValue as Button;
            var reset = modelSerialized.FindProperty("_resetButton")?.objectReferenceValue as Button;
            var animation = modelSerialized.FindProperty("_animationButton")?.objectReferenceValue as Button;
            if (controlsRoot == null || autoMotion == null || reset == null || animation == null)
                throw new InvalidOperationException(
                    "The Model page requires its authored control root and three existing controls.");

            var shellSerialized = new SerializedObject(shell);
            var slots = shellSerialized.FindProperty("_slots");
            var modelControlRoot = slots?.FindPropertyRelative("_modelControlRoot")?.objectReferenceValue as RectTransform;
            var panoramaExitSlot = slots?.FindPropertyRelative("_panoramaExitSlot")?.objectReferenceValue as RectTransform;
            var targetsProperty = slots?.FindPropertyRelative("_flowTargets");
            if (modelControlRoot == null || controlsRoot.transform != modelControlRoot ||
                panoramaExitSlot == null || targetsProperty == null)
                throw new InvalidOperationException(
                    "GlobalFrontendShell requires its authored Model control root, immersive exit template, and flow target list.");

            var targets = new List<ShellFlowActionTarget>();
            for (var index = 0; index < targetsProperty.arraySize; index++)
            {
                var target = targetsProperty.GetArrayElementAtIndex(index).objectReferenceValue as ShellFlowActionTarget;
                if (target != null && !targets.Contains(target)) targets.Add(target);
            }

            var authoredActions = root.GetComponentsInChildren<ShellFlowActionTarget>(true);
            var actionCandidates = authoredActions
                .Where(target => target.Action == ShellFlowAction.FeaturePageAction)
                .ToArray();
            var exitCandidates = authoredActions
                .Where(target => target.Action == ShellFlowAction.FeaturePageActionExitFocus)
                .ToArray();
            if (actionCandidates.Length > 1 || exitCandidates.Length > 1)
                throw new InvalidOperationException(
                    "VisitorRuntime must contain at most one feature-page action and one focus exit before publishing.");

            var action = actionCandidates.SingleOrDefault();
            var actionObject = action != null
                ? action.gameObject
                : UnityEngine.Object.Instantiate(
                    animation.gameObject,
                    modelControlRoot,
                    false);
            if (actionObject.transform.parent != modelControlRoot)
                actionObject.transform.SetParent(modelControlRoot, false);
            actionObject.name = "PhysicalAugmentationFeatureAction";
            var copiedIndicator = actionObject.transform.Find("ActiveIndicator");
            if (copiedIndicator != null)
                UnityEngine.Object.DestroyImmediate(copiedIndicator.gameObject);
            var copiedActiveState = actionObject.transform.Find("Active");
            if (copiedActiveState != null)
                UnityEngine.Object.DestroyImmediate(copiedActiveState.gameObject);
            var button = actionObject.GetComponent<Button>();
            var label = actionObject.transform.Find("Label")?.GetComponent<Text>();
            if (button == null || label == null)
                throw new InvalidOperationException(
                    "The authored Model animation control must provide one Button and direct Label text.");
            if (action == null) action = actionObject.AddComponent<ShellFlowActionTarget>();
            label.text = "现实演示";
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 13;
            label.resizeTextMaxSize = 16;

            var buttons = new[] { autoMotion, reset, animation, button };
            var positions = new[] { -258f, -86f, 86f, 258f };
            for (var index = 0; index < buttons.Length; index++)
            {
                var rect = buttons[index].transform as RectTransform;
                if (rect == null)
                    throw new InvalidOperationException("Every Model control requires a RectTransform.");
                rect.anchoredPosition = new Vector2(positions[index], 0f);
                rect.sizeDelta = new Vector2(160f, rect.sizeDelta.y);
                EditorUtility.SetDirty(buttons[index]);
            }

            var actionSerialized = new SerializedObject(action);
            actionSerialized.FindProperty("_action").intValue = (int)ShellFlowAction.FeaturePageAction;
            actionSerialized.FindProperty("_feature").intValue = (int)FeaturePageId.Model;
            actionSerialized.FindProperty("_button").objectReferenceValue = button;
            actionSerialized.FindProperty("_label").objectReferenceValue = null;
            actionSerialized.FindProperty("_legacyLabel").objectReferenceValue = label;
            actionSerialized.ApplyModifiedPropertiesWithoutUndo();

            var exitAction = exitCandidates.SingleOrDefault();
            var exitObject = exitAction != null
                ? exitAction.gameObject
                : UnityEngine.Object.Instantiate(
                    panoramaExitSlot.gameObject,
                    panoramaExitSlot.parent,
                    false);
            if (exitObject.transform.parent != panoramaExitSlot.parent)
                exitObject.transform.SetParent(panoramaExitSlot.parent, false);
            exitObject.name = "PhysicalAugmentationFeatureFocusExit";
            exitObject.SetActive(false);
            var exitRect = exitObject.transform as RectTransform;
            var exitButton = exitObject.GetComponent<Button>();
            var exitLabel = exitObject.GetComponentsInChildren<Text>(true).SingleOrDefault();
            if (exitAction == null) exitAction = exitObject.GetComponent<ShellFlowActionTarget>();
            if (exitRect == null || exitButton == null || exitLabel == null || exitAction == null)
                throw new InvalidOperationException(
                    "The authored immersive exit template must provide a RectTransform, Button, label, and semantic action target.");
            exitRect.anchoredPosition = new Vector2(582f, 328f);
            exitLabel.text = "返回面板";
            exitLabel.resizeTextForBestFit = true;
            exitLabel.resizeTextMinSize = 13;
            exitLabel.resizeTextMaxSize = 18;

            var exitSerialized = new SerializedObject(exitAction);
            exitSerialized.FindProperty("_action").intValue =
                (int)ShellFlowAction.FeaturePageActionExitFocus;
            exitSerialized.FindProperty("_feature").intValue = (int)FeaturePageId.Model;
            exitSerialized.FindProperty("_button").objectReferenceValue = exitButton;
            exitSerialized.FindProperty("_label").objectReferenceValue = null;
            exitSerialized.FindProperty("_legacyLabel").objectReferenceValue = exitLabel;
            exitSerialized.ApplyModifiedPropertiesWithoutUndo();
            slots.FindPropertyRelative("_featureFocusExitSlot").objectReferenceValue = exitRect;

            targets.RemoveAll(target => target == null ||
                                        target.Action == ShellFlowAction.FeaturePageAction ||
                                        target.Action == ShellFlowAction.FeaturePageActionExitFocus);
            targets.Add(action);
            targets.Add(exitAction);
            targetsProperty.arraySize = targets.Count;
            for (var index = 0; index < targets.Count; index++)
                targetsProperty.GetArrayElementAtIndex(index).objectReferenceValue = targets[index];
            shellSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(action);
            EditorUtility.SetDirty(exitAction);
            EditorUtility.SetDirty(shell);
        }

        static T RequireAtMostOneOrAdd<T>(GameObject root, GameObject owner) where T : Component
        {
            var existing = root.GetComponentsInChildren<T>(true);
            if (existing.Length > 1)
                throw new InvalidOperationException(
                    $"VisitorRuntime prefab contains {existing.Length} {typeof(T).Name} components; expected at most one.");
            return existing.Length == 1 ? existing[0] : owner.AddComponent<T>();
        }

        static void AssertNoVisitorAnchorVisuals(Transform locatorRoot)
        {
            if (locatorRoot.GetComponentsInChildren<Renderer>(true).Length > 0 ||
                locatorRoot.GetComponentsInChildren<Canvas>(true).Length > 0 ||
                locatorRoot.GetComponentsInChildren<Collider>(true).Length > 0 ||
                locatorRoot.GetComponentsInChildren<Collider2D>(true).Length > 0)
                throw new InvalidOperationException(
                    "Visitor Physical Anchor locator hierarchy must contain no Renderer, Canvas, or Collider.");
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

        static void PublishVisitor(GameObject gatewayPrefab)
        {
            var scene = EditorSceneManager.OpenScene(VisitorScenePath, OpenSceneMode.Single);
            var installer = RequireExactlyOne<VisitorInstaller>(scene, "VisitorInstaller");
            var ovrManager = RequireExactlyOne<OVRManager>(scene, "OVRManager");
            var managerSerialized = new SerializedObject(ovrManager);
            managerSerialized.FindProperty("requestScenePermissionOnStartup").boolValue = false;
            managerSerialized.FindProperty("requestPassthroughCameraAccessPermissionOnStartup").boolValue = false;
            managerSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ovrManager);
            var installerSerialized = new SerializedObject(installer);
            var viewer = installerSerialized.FindProperty("_viewer").objectReferenceValue as Transform;
            var shell = installerSerialized.FindProperty("_frontendShell").objectReferenceValue as GlobalFrontendShell;
            var interactionRigRoot = installerSerialized.FindProperty("_interactionRigRoot")
                .objectReferenceValue as Transform;
            if (viewer == null || shell == null)
                throw new InvalidOperationException(
                    "Visitor ApplicationMode integration requires viewer and frontend shell bindings.");
            if (interactionRigRoot == null)
                throw new InvalidOperationException(
                    "Visitor ApplicationMode integration requires the authored interaction rig root.");

            ConfigureHandsFirstInteraction(interactionRigRoot, installer.gameObject);

            var gateway = RequireGateway(scene, gatewayPrefab);
            ConfigureGateway(
                scene,
                gateway,
                ApplicationModeRole.Visitor,
                viewer,
                shell.GetComponent<CanvasGroup>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void RequireSafeScenePublishingContext()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException(
                    "ApplicationMode publishing is unavailable while entering or running Play Mode.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException(
                    "Exit Prefab Mode before publishing ApplicationMode scenes.");

            var dirtyScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => scene.IsValid() && scene.isLoaded && scene.isDirty)
                .Select(scene => string.IsNullOrWhiteSpace(scene.path) ? scene.name : scene.path)
                .ToArray();
            if (dirtyScenes.Length > 0)
                throw new InvalidOperationException(
                    "Save or discard the currently modified scenes before publishing; no scene was changed. Dirty scenes: " +
                    string.Join(", ", dirtyScenes));
        }

        static void ConfigureHandsFirstInteraction(Transform interactionRigRoot, GameObject owner)
        {
            var left = RequireHandsFirstTrackerBinding(interactionRigRoot, "Left");
            var right = RequireHandsFirstTrackerBinding(interactionRigRoot, "Right");
            if (left.Tracker == right.Tracker || left.HandAvailability == right.HandAvailability)
                throw new InvalidOperationException(
                    "Visitor hands-first interaction requires distinct left and right tracker/HandRef bindings.");

            var existing = owner.GetComponents<HandsFirstInteractionRigBinding>();
            if (existing.Length > 1)
                throw new InvalidOperationException(
                    $"VisitorInstaller contains {existing.Length} HandsFirstInteractionRigBinding components; expected at most one.");
            var binding = existing.Length == 1
                ? existing[0]
                : owner.AddComponent<HandsFirstInteractionRigBinding>();
            binding.InjectBindings(left, right);
            EditorUtility.SetDirty(binding);
        }

        static HandsFirstInteractionRigBinding.TrackerBinding RequireHandsFirstTrackerBinding(
            Transform interactionRigRoot,
            string sideToken)
        {
            var candidates = interactionRigRoot.GetComponentsInChildren<HandRef>(true)
                .Where(hand => hand != null &&
                               HierarchyContainsToken(hand.transform, interactionRigRoot, sideToken))
                .Select(hand => new
                {
                    Hand = hand,
                    Tracker = hand.transform.Find(HandsFirstTrackerRelativePath)
                        ?.GetComponent<ActiveStateTracker>()
                })
                .Where(candidate => candidate.Tracker != null)
                .ToArray();
            if (candidates.Length != 1)
                throw new InvalidOperationException(
                    $"Visitor interaction rig requires exactly one {sideToken} HandRef with tracker path " +
                    $"'{HandsFirstTrackerRelativePath}'; found {candidates.Length}.");
            return new HandsFirstInteractionRigBinding.TrackerBinding(
                candidates[0].Tracker,
                candidates[0].Hand);
        }

        static bool HierarchyContainsToken(Transform current, Transform inclusiveRoot, string token)
        {
            while (current != null)
            {
                if (current.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (current == inclusiveRoot) return false;
                current = current.parent;
            }
            return false;
        }

        static void PublishAdmin(GameObject gatewayPrefab)
        {
            var scene = EditorSceneManager.OpenScene(AdminScenePath, OpenSceneMode.Single);
            var cameras = SceneComponents<Camera>(scene).ToArray();
            var viewer = cameras.FirstOrDefault(camera => camera.CompareTag("MainCamera"))?.transform ??
                         cameras.FirstOrDefault(camera =>
                             string.Equals(camera.name, "CenterEyeAnchor", StringComparison.Ordinal))?.transform;
            if (viewer == null)
                throw new InvalidOperationException(
                    "Admin ApplicationMode integration requires the official CenterEyeAnchor/MainCamera.");

            var gateway = RequireGateway(scene, gatewayPrefab);
            ConfigureGateway(
                scene,
                gateway,
                ApplicationModeRole.Administrator,
                viewer,
                null);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static GameObject RequireGateway(Scene scene, GameObject prefab)
        {
            var gateways = SceneComponents<ApplicationModeControllerHost>(scene).ToArray();
            if (gateways.Length > 1)
                throw new InvalidOperationException(
                    $"Scene '{scene.path}' contains {gateways.Length} ApplicationMode gateways.");
            if (gateways.Length == 1)
                return gateways[0].gameObject;

            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException(
                    $"Could not instantiate ApplicationMode gateway in '{scene.path}'.");
            instance.name = "ApplicationModeGateway";
            return instance;
        }

        static void ConfigureGateway(
            Scene scene,
            GameObject gateway,
            ApplicationModeRole role,
            Transform viewer,
            CanvasGroup backgroundGroup)
        {
            var host = gateway.GetComponent<ApplicationModeControllerHost>();
            var presenter = gateway.GetComponent<ApplicationModePromptPresenter>();
            if (host == null || presenter == null)
                throw new InvalidOperationException("ApplicationMode gateway components are incomplete.");

            var hostSerialized = new SerializedObject(host);
            hostSerialized.FindProperty("_sceneRole").enumValueIndex = (int)role;
            hostSerialized.ApplyModifiedPropertiesWithoutUndo();

            var presenterSerialized = new SerializedObject(presenter);
            presenterSerialized.FindProperty("_viewOrigin").objectReferenceValue = viewer;
            presenterSerialized.FindProperty("_backgroundCanvasGroup").objectReferenceValue =
                backgroundGroup;
            var blocked = presenterSerialized.FindProperty("_blockedBackgroundSelectables");
            var selectables = SceneComponents<Selectable>(scene)
                .Where(selectable => selectable != null &&
                                     !selectable.transform.IsChildOf(gateway.transform))
                .Distinct()
                .ToArray();
            blocked.arraySize = selectables.Length;
            for (var index = 0; index < selectables.Length; index++)
                blocked.GetArrayElementAtIndex(index).objectReferenceValue = selectables[index];
            presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(host);
            EditorUtility.SetDirty(presenter);
            EditorUtility.SetDirty(gateway);
        }

        static T RequireExactlyOne<T>(Scene scene, string label) where T : Component
        {
            var components = SceneComponents<T>(scene).ToArray();
            if (components.Length != 1)
                throw new InvalidOperationException(
                    $"Scene '{scene.path}' requires exactly one {label}; found {components.Length}.");
            return components[0];
        }

        static IEnumerable<T> SceneComponents<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true));
    }
}
