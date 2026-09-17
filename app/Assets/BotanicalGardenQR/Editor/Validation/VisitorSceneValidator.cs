using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.ApplicationMode.Adapters;
using BotanicalGardenQR.ApplicationMode.Contracts;
using BotanicalGardenQR.ApplicationMode.Frontend;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.FrontendShell.Runtime;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BotanicalGardenQR.Editor.Validation
{
    internal static class VisitorSceneValidator
    {
        const string HeadGazeInputTypeName = "HeadGazeDwellController";
        const string GazeReticlePresenterTypeName = "GazeReticlePresenter";
        const string LegacyRayTypeName = "OVRRayHelper";
        const string FirstPersonLocomotorTypeName = "Oculus.Interaction.Locomotion.FirstPersonLocomotor";
        const string AdminOfficialBridgeTypeName = "BotanicalGardenQR.SpatialAnchorAdmin.Official.BotanicalOfficialSpatialAnchorAdminBridge";
        const string AdminOfficialPanelPresentationTypeName = "BotanicalGardenQR.SpatialAnchorAdmin.Official.OfficialAdminPanelPresentation";
        const string AdminPanelPlacementTypeName = "BotanicalGardenQR.SpatialAnchorAdmin.Official.OfficialAdminPanelPlacement";
        const string AdminGazeGestureInputTypeName = "BotanicalGardenQR.SpatialAnchorAdmin.Authoring.AdminGazeGestureInput";
        const string AdminControllerInputTypeName = "BotanicalGardenQR.SpatialAnchorAdmin.Authoring.AdminControllerInput";
        const string AdminPointerRingGraphicTypeName = "BotanicalGardenQR.SpatialAnchorAdmin.Authoring.AdminPointerRingGraphic";
        const string AdminPointableSelectionFallbackTypeName = "BotanicalGardenQR.SpatialAnchorAdmin.Authoring.AdminPointableSelectionFallback";
        const string OvrCameraRigTypeName = "OVRCameraRig";
        const string OvrManagerTypeName = "OVRManager";
        const string OvrCameraRigRefTypeName = "Oculus.Interaction.Input.OVRCameraRigRef";
        const string OvrHandTypeName = "OVRHand";
        const string PointableCanvasModuleTypeName = "Oculus.Interaction.PointableCanvasModule";
        const string PointableCanvasTypeName = "Oculus.Interaction.PointableCanvas";
        const string TextMeshProUguiTypeName = "TMPro.TextMeshProUGUI";
        const string PhysicalAnchorLocatorTypeName =
            "BotanicalGardenQR.PhysicalAugmentation.Backend.MetaPhysicalAnchorLocator";
        const string EnvironmentDepthManagerTypeName =
            "Meta.XR.EnvironmentDepth.EnvironmentDepthManager";
        const string EnvironmentRaycastManagerTypeName =
            "Meta.XR.EnvironmentRaycastManager";
        const int AutomaticEyeBufferSharpening = 1 << 18;
        const float MinimumWorldCanvasDimensionMeters = 0.05f;
        const float MaximumWorldCanvasDimensionMeters = 5f;

        static readonly string[] RetiredRootNames = { "BotanicalEntranceRuntime", "BotanicalSpatialAnchorVisitorRuntime", "Botanical Experience Runtime", "VisitorMediaEntryMap", "EntryMediaRuntimeCoordinator" };
        static readonly string[] AdminTypeNames = { "AnchorUIManager", "BotanicalOfficialSpatialAnchorAdminBridge", "SpatialAnchorLoader" };
        public static void Validate(ValidationScope scope, ICollection<CommercialValidationIssue> issues)
        {
            if (scope != ValidationScope.All && scope != ValidationScope.Activation && scope != ValidationScope.FrontendShell && scope != ValidationScope.SpatialHost && scope != ValidationScope.PhysicalAugmentation) return;
            ValidateBoundaryProjectConfig(issues);
            if (scope == ValidationScope.All)
                ValidateSpatialAnchorAdminAssemblyBoundaries(issues);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var enabled = EditorBuildSettings.scenes.Where(x => x.enabled).ToArray();
                if (scope == ValidationScope.All)
                {
                    var adminCount = enabled.Count(x => x.path.IndexOf("SpatialAnchorAdmin", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (enabled.Length != 2 || adminCount != 1)
                        CommercialArchitectureValidator.Add(issues, "COM-SCENE-000", "ProjectSettings/EditorBuildSettings.asset", "Bootstrap", $"Commercial build requires one Visitor Scene and one spatial-anchor Admin Scene; found {enabled.Length} enabled scenes ({adminCount} admin)." );
                }
                foreach (var buildScene in enabled)
                {
                    var actualGuid = AssetDatabase.AssetPathToGUID(buildScene.path);
                    if (string.IsNullOrWhiteSpace(actualGuid) ||
                        !string.Equals(buildScene.guid.ToString(), actualGuid, StringComparison.OrdinalIgnoreCase))
                        CommercialArchitectureValidator.Add(issues, "COM-SCENE-008", "ProjectSettings/EditorBuildSettings.asset", "Bootstrap", $"Build Scene GUID does not match its asset path: {buildScene.path}.");
                    ValidateBuildScene(buildScene.path, scope, issues);
                }
            }
            finally
            {
                if (setup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        static void ValidateBuildScene(string path, ValidationScope scope, ICollection<CommercialValidationIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { CommercialArchitectureValidator.Add(issues, "COM-SCENE-001", path, "Bootstrap", "Enabled Build Scene cannot be loaded."); return; }
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var components = roots.SelectMany(x => x.GetComponentsInChildren<Component>(true)).Where(x => x != null).ToArray();
                foreach (var gameObject in roots.SelectMany(DescendantsAndSelf))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
                        CommercialArchitectureValidator.Add(issues, "COM-SCENE-002", path, "Bootstrap", $"Scene object '{gameObject.name}' contains a missing script.");
                var isAdmin = path.IndexOf("SpatialAnchorAdmin", StringComparison.OrdinalIgnoreCase) >= 0;
                var allInstallers = components.OfType<VisitorInstaller>().ToArray();
                var installers = allInstallers.Where(x => x.enabled && x.gameObject.activeInHierarchy).ToArray();
                ValidateApplicationMode(components, path, isAdmin, issues);
                ValidateBoundaryManagers(components, path, issues);
                if (CommercialArchitectureValidator.Includes(
                        scope,
                        ValidationScope.PhysicalAugmentation))
                    ValidatePhysicalAugmentationTopology(
                        components,
                        path,
                        isAdmin,
                        issues);
                if (isAdmin)
                {
                    if (allInstallers.Length != 0) CommercialArchitectureValidator.Add(issues, "COM-ADMIN-001", path, "Activation", "Spatial-anchor administrator scene must not contain VisitorInstaller.");
                    ValidateAdminInteraction(components, path, issues);
                    return;
                }
                if (allInstallers.Length != 1) CommercialArchitectureValidator.Add(issues, "COM-SCENE-007", path, "Bootstrap", $"Visitor Build Scene contains {allInstallers.Length} VisitorInstaller components, including disabled instances; parallel runtimes are forbidden.");
                if (installers.Length != 1) CommercialArchitectureValidator.Add(issues, "COM-SCENE-003", path, "Bootstrap", $"Visitor Build Scene requires exactly one enabled VisitorInstaller; found {installers.Length}.");
                ValidateSpatialDataPermissionTopology(components, installers, path, issues);
                ValidateUnsupportedVisitorLocomotion(components, path, issues);
                ValidateVisitorRendering(components, path, issues);
                foreach (var installer in installers)
                {
                    ValidateInstaller(installer, path, issues);
                    if (CommercialArchitectureValidator.Includes(scope, ValidationScope.SpatialHost) || CommercialArchitectureValidator.Includes(scope, ValidationScope.FrontendShell))
                        ValidateDisplayHierarchy(installer, path, issues);
                    if (CommercialArchitectureValidator.Includes(scope, ValidationScope.FrontendShell))
                        ValidateFrontendTopology(installer, path, issues);
                }
                if (CommercialArchitectureValidator.Includes(scope, ValidationScope.FrontendShell))
                {
                    ValidateLegacyRay(components, path, issues);
                    ValidateHeadGazeInput(components, installers, path, issues);
                    ValidateGazeReticlePresenter(components, installers, path, issues);
                }
                foreach (var component in components)
                    if (AdminTypeNames.Any(x => string.Equals(component.GetType().Name, x, StringComparison.Ordinal)))
                        CommercialArchitectureValidator.Add(issues, "COM-ADMIN-002", path, "Activation", $"Visitor runtime contains administrator-only type '{component.GetType().Name}'.");
                foreach (var root in roots)
                    if (RetiredRootNames.Any(x => root.name.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
                        CommercialArchitectureValidator.Add(issues, "COM-SCENE-004", path, "Bootstrap", $"Retired or parallel runtime root remains in production Scene: '{root.name}'.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        static void ValidateBoundaryProjectConfig(ICollection<CommercialValidationIssue> issues)
        {
            MetaOpenXRApiVersionGuard.Validate(issues);
            ValidateBoundaryAndroidManifest(issues);

            const string path = "Assets/Oculus/OculusProjectConfig.asset";
            var config = AssetDatabase.LoadMainAssetAtPath(path);
            if (config != null)
            {
                var serialized = new SerializedObject(config);
                var support = serialized.FindProperty("boundaryVisibilitySupport");
                if (support != null && support.intValue != 0)
                    CommercialArchitectureValidator.Add(
                        issues,
                        "COM-XR-001",
                        path,
                        "Bootstrap",
                        "Boundary visibility is device-admin owned; the application must not declare Boundary Visibility support.");
                var systemKeyboard = serialized.FindProperty("requiresSystemKeyboard");
                if (systemKeyboard == null || !systemKeyboard.boolValue)
                    CommercialArchitectureValidator.Add(
                        issues,
                        "COM-XR-005",
                        path,
                        "SpatialAnchorAdmin",
                        "Admin anchor renaming requires the official Meta system keyboard Overlay capability.");
                var targetDevices = serialized.FindProperty("targetDeviceTypes");
                if (targetDevices == null || !targetDevices.isArray || targetDevices.arraySize != 1 ||
                    targetDevices.GetArrayElementAtIndex(0).intValue != 4)
                    CommercialArchitectureValidator.Add(
                        issues,
                        "COM-XR-006",
                        path,
                        "Bootstrap",
                        "The published product baseline is Quest 3 only; Meta targetDeviceTypes must contain exactly Quest3.");
            }

            const string openXrSettingsPath = "Assets/XR/Settings/OpenXR Package Settings.asset";
            foreach (var feature in AssetDatabase.LoadAllAssetsAtPath(openXrSettingsPath)
                         .Where(asset => asset != null &&
                                         string.Equals(
                                             asset.GetType().Name,
                                             "BoundaryVisibilityFeature",
                                             StringComparison.Ordinal)))
            {
                var serialized = new SerializedObject(feature);
                var enabled = serialized.FindProperty("m_enabled")?.boolValue ?? false;
                var suppress = serialized.FindProperty("m_SuppressVisibility")?.boolValue ?? false;
                if (!enabled && !suppress) continue;
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-XR-003",
                    openXrSettingsPath,
                    "Bootstrap",
                    $"OpenXR Boundary Visibility feature '{feature.name}' must remain disabled and must not request suppression.");
            }
        }

        static void ValidateBoundaryAndroidManifest(ICollection<CommercialValidationIssue> issues)
        {
            const string manifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
            const string permission = "com.oculus.permission.BOUNDARY_VISIBILITY";

            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                  throw new DirectoryNotFoundException("Unity project root is unavailable.");
                var absoluteManifestPath = Path.Combine(
                    projectRoot,
                    manifestPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absoluteManifestPath)) return;

                XNamespace android = "http://schemas.android.com/apk/res/android";
                var document = XDocument.Load(absoluteManifestPath, LoadOptions.None);
                var declared = document.Root != null && document.Root
                    .Elements("uses-permission")
                    .Any(element => string.Equals(
                        (string)element.Attribute(android + "name"),
                        permission,
                        StringComparison.Ordinal));
                if (!declared) return;

                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-XR-004",
                    manifestPath,
                    "Bootstrap",
                    "Boundary visibility is device-admin owned; the Android manifest must not request Boundary Visibility permission.");
            }
            catch (Exception exception)
            {
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-XR-004",
                    manifestPath,
                    "Bootstrap",
                    $"Boundary permission validation could not read the Android manifest: {exception.GetType().Name}.");
            }
        }

        static void ValidateBoundaryManagers(
            IEnumerable<Component> components,
            string path,
            ICollection<CommercialValidationIssue> issues)
        {
            foreach (var manager in components.Where(component =>
                         string.Equals(component.GetType().Name, "OVRManager", StringComparison.Ordinal)))
            {
                var serialized = new SerializedObject(manager);
                var suppress = serialized.FindProperty("shouldBoundaryVisibilityBeSuppressed");
                if (suppress == null || !suppress.boolValue) continue;
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-XR-002",
                    path,
                    "Bootstrap",
                    $"OVRManager '{manager.name}' requests boundary suppression, but boundary visibility is device-admin owned.");
            }
        }

        static void ValidatePhysicalAugmentationTopology(
            Component[] components,
            string path,
            bool isAdmin,
            ICollection<CommercialValidationIssue> issues)
        {
            var locators = ComponentsOfType(components, PhysicalAnchorLocatorTypeName);
            var depthManagers = ComponentsOfType(
                components,
                EnvironmentDepthManagerTypeName);
            var anchorLoaders = components.Where(component => string.Equals(
                    component.GetType().Name,
                    "SpatialAnchorLoader",
                    StringComparison.Ordinal))
                .ToArray();
            var environmentRaycasts = ComponentsOfType(
                components,
                EnvironmentRaycastManagerTypeName);

            if (isAdmin)
            {
                if (locators.Length != 0 || depthManagers.Length != 0 ||
                    anchorLoaders.Length != 1 || environmentRaycasts.Length != 1)
                    CommercialArchitectureValidator.Add(
                        issues,
                        "COM-PHYSICAL-SCENE-001",
                        path,
                        "PhysicalAugmentation",
                        "Admin requires one visible authoring anchor loader and environment raycast manager, with no Visitor locator or depth manager." );
                return;
            }

            if (locators.Length != 1 || depthManagers.Length != 1 ||
                anchorLoaders.Length != 0 || environmentRaycasts.Length != 0)
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-PHYSICAL-SCENE-002",
                    path,
                    "PhysicalAugmentation",
                    "Visitor requires one hidden physical-anchor locator and one EnvironmentDepthManager, with no administrator loader or raycast manager." );

            if (locators.Length == 1 &&
                (locators[0].GetComponentsInChildren<Renderer>(true).Length != 0 ||
                 locators[0].GetComponentsInChildren<Canvas>(true).Length != 0 ||
                 locators[0].GetComponentsInChildren<Collider>(true).Length != 0 ||
                 locators[0].GetComponentsInChildren<Collider2D>(true).Length != 0))
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-PHYSICAL-SCENE-003",
                    path,
                    "PhysicalAugmentation",
                    "Visitor anchor locator hierarchy must remain invisible and non-interactive." );

            if (depthManagers.Length == 1)
            {
                var manager = depthManagers[0];
                var enabled = !(manager is Behaviour behaviour) || behaviour.enabled;
                var mode = manager.GetType().GetProperty("OcclusionShadersMode")
                    ?.GetValue(manager)?.ToString();
                if (!enabled || !string.Equals(
                        mode,
                        "SoftOcclusion",
                        StringComparison.Ordinal))
                    CommercialArchitectureValidator.Add(
                        issues,
                        "COM-PHYSICAL-SCENE-004",
                        path,
                        "PhysicalAugmentation",
                        "Visitor EnvironmentDepthManager must be enabled in SoftOcclusion mode." );
            }

            var managers = ComponentsOfType(components, OvrManagerTypeName);
            if (managers.Length != 1) return;
            var serialized = new SerializedObject(managers[0]);
            var requestsScenePermission = serialized
                .FindProperty("requestScenePermissionOnStartup")?.boolValue ?? false;
            var requestsRawCamera = serialized
                .FindProperty("requestPassthroughCameraAccessPermissionOnStartup")
                ?.boolValue ?? false;
            if (requestsScenePermission || requestsRawCamera)
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-PHYSICAL-SCENE-005",
                    path,
                    "PhysicalAugmentation",
                    "Visitor must delegate Spatial Data permission to its explicit Activation gate; OVRManager scene and raw-camera auto requests must remain disabled." );
        }

        static void ValidateSpatialAnchorAdminAssemblyBoundaries(
            ICollection<CommercialValidationIssue> issues)
        {
            const string root = "Assets/BotanicalGardenQR/SpatialAnchorAdmin";
            var expectedAssemblies = new[]
            {
                (Prefix: "/Contracts/", Assembly: "BotanicalGardenQR.SpatialAnchorAdmin.Contracts"),
                (Prefix: "/Runtime/", Assembly: "BotanicalGardenQR.SpatialAnchorAdmin.Runtime"),
                (Prefix: "/Frontend/", Assembly: "BotanicalGardenQR.SpatialAnchorAdmin.Frontend"),
                (Prefix: "/Authoring/Editor/", Assembly: "BotanicalGardenQR.SpatialAnchorAdmin.Editor"),
                (Prefix: "/Official/SpatialAnchor/Persistence/", Assembly: "BotanicalGardenQR.SpatialAnchor.Persistence")
            };
            var mismatches = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                var expected = expectedAssemblies.FirstOrDefault(candidate =>
                    path.IndexOf(candidate.Prefix, StringComparison.Ordinal) >= 0);
                if (string.IsNullOrWhiteSpace(expected.Assembly))
                {
                    mismatches.Add($"{path}=unowned");
                    continue;
                }
                var actual = CompilationPipeline.GetAssemblyNameFromScriptPath(path);
                var normalizedActual = actual != null && actual.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    ? actual.Substring(0, actual.Length - 4)
                    : actual;
                if (!string.Equals(normalizedActual, expected.Assembly, StringComparison.Ordinal))
                    mismatches.Add($"{path}={actual ?? "<none>"}, expected {expected.Assembly}");
                if (!string.Equals(expected.Assembly, "BotanicalGardenQR.SpatialAnchorAdmin.Editor", StringComparison.Ordinal) &&
                    File.ReadAllText(path).IndexOf("using UnityEditor", StringComparison.Ordinal) >= 0)
                    mismatches.Add($"{path}=Player script references UnityEditor");
            }

            var runtimeDefinition = File.ReadAllText(
                $"{root}/Runtime/BotanicalGardenQR.SpatialAnchorAdmin.Runtime.asmdef");
            foreach (var forbidden in new[]
                     {
                         "BotanicalGardenQR.SpatialAnchorAdmin.Frontend",
                         "Oculus.VR",
                         "Unity.TextMeshPro",
                         "Unity.ugui"
                     })
                if (runtimeDefinition.IndexOf(forbidden, StringComparison.Ordinal) >= 0)
                    mismatches.Add($"Runtime asmdef references forbidden assembly {forbidden}");
            var contractsDefinition = File.ReadAllText(
                $"{root}/Contracts/BotanicalGardenQR.SpatialAnchorAdmin.Contracts.asmdef");
            if (contractsDefinition.IndexOf("\"references\": []", StringComparison.Ordinal) < 0)
                mismatches.Add("Contracts asmdef references a concrete BotanicalGardenQR assembly");

            if (mismatches.Count == 0) return;
            CommercialArchitectureValidator.Add(
                issues,
                "COM-ADMIN-009",
                root,
                "SpatialAnchorAdmin",
                "Admin assembly boundary violation(s): " + string.Join("; ", mismatches));
        }

        static void ValidateSpatialDataPermissionTopology(
            Component[] components,
            IEnumerable<VisitorInstaller> installers,
            string path,
            ICollection<CommercialValidationIssue> issues)
        {
            var gates = components
                .Where(component => component is ISpatialDataPermissionGate)
                .ToArray();
            if (gates.Length != 1)
            {
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-ACT-015",
                    path,
                    "Activation",
                    $"Visitor requires exactly one Spatial Data permission gate; found {gates.Length}.");
                return;
            }

            foreach (var installer in installers)
            {
                var configured = new SerializedObject(installer)
                    .FindProperty("_spatialDataPermissionGate")?.objectReferenceValue;
                if (configured == gates[0]) continue;
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-ACT-016",
                    path,
                    "Bootstrap",
                    "VisitorInstaller must reference the scene's one Spatial Data permission gate.");
            }
        }

        static void ValidateUnsupportedVisitorLocomotion(
            IEnumerable<Component> components,
            string path,
            ICollection<CommercialValidationIssue> issues)
        {
            foreach (var locomotor in components.Where(component =>
                         string.Equals(component.GetType().FullName, FirstPersonLocomotorTypeName, StringComparison.Ordinal) &&
                         component.gameObject.activeInHierarchy &&
                         (!(component is Behaviour behaviour) || behaviour.enabled)))
            {
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-XR-005",
                    path,
                    "Bootstrap",
                    $"Fixed-venue Visitor Scene must not run FirstPersonLocomotor on '{HierarchyPath(locomotor.transform)}'; physical walking and the OVR camera rig own visitor movement.");
            }
        }

        static void ValidateVisitorRendering(
            IEnumerable<Component> components,
            string path,
            ICollection<CommercialValidationIssue> issues)
        {
            var managers = components.Where(component =>
                    string.Equals(component.GetType().Name, "OVRManager", StringComparison.Ordinal) &&
                    component.gameObject.activeInHierarchy &&
                    (!(component is Behaviour behaviour) || behaviour.enabled))
                .ToArray();
            if (managers.Length != 1)
            {
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-XR-006",
                    path,
                    "Bootstrap",
                    $"Visitor Scene requires exactly one active OVRManager; found {managers.Length}.");
                return;
            }

            var activeControllerVisuals = components.Where(component =>
                    string.Equals(component.GetType().Name, "OVRControllerHelper", StringComparison.Ordinal) &&
                    component.gameObject.activeInHierarchy &&
                    (!(component is Behaviour behaviour) || behaviour.enabled))
                .ToArray();
            foreach (var controllerVisual in activeControllerVisuals)
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-XR-009",
                    path,
                    "Bootstrap",
                    $"Visitor Scene must not activate the controller-model visual '{HierarchyPath(controllerVisual.transform)}'; hand tracking and controller input remain independent of this visual-only helper.");

            var serialized = new SerializedObject(managers[0]);

            if (serialized.FindProperty("_sharpenType")?.intValue != AutomaticEyeBufferSharpening)
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-XR-007",
                    path,
                    "FrontendShell",
                    "Visitor OVRManager must use the current Meta SDK Automatic eye-buffer sharpening mode for world-space text and media clarity.");

            var dynamicResolutionEnabled = serialized.FindProperty("_enableDynamicResolution")?.boolValue ?? false;
            var quest3Minimum = serialized.FindProperty("quest3MinDynamicResolutionScale")?.floatValue ?? 0f;
            var quest3Maximum = serialized.FindProperty("quest3MaxDynamicResolutionScale")?.floatValue ?? 0f;
            if (!dynamicResolutionEnabled ||
                Mathf.Abs(quest3Minimum - 0.7f) > 0.0001f ||
                Mathf.Abs(quest3Maximum - 1.6f) > 0.0001f)
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-XR-008",
                    path,
                    "Bootstrap",
                    "Visitor OVRManager must preserve Meta's Quest 3 dynamic-resolution baseline (enabled, 0.7–1.6) until device profiling approves a replacement range.");
        }

        static IEnumerable<GameObject> DescendantsAndSelf(GameObject root)
        {
            yield return root;
            foreach (Transform child in root.transform)
                foreach (var descendant in DescendantsAndSelf(child.gameObject)) yield return descendant;
        }

        static void ValidateInstaller(VisitorInstaller installer, string path, ICollection<CommercialValidationIssue> issues)
        {
            var serialized = new SerializedObject(installer);
            var iterator = serialized.GetIterator();
            if (!iterator.NextVisible(true)) return;
            do
            {
                if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                    iterator.objectReferenceValue == null)
                    CommercialArchitectureValidator.Add(issues, "COM-SCENE-005", path, "Bootstrap", $"VisitorInstaller reference '{iterator.propertyPath}' is empty.");
                if (iterator.isArray && iterator.propertyType != SerializedPropertyType.String && iterator.arraySize == 0 && iterator.propertyPath == "_recognitionSourceAdapters")
                    CommercialArchitectureValidator.Add(issues, "COM-SCENE-006", path, "Activation", "VisitorInstaller has no recognition source adapters.");
            } while (iterator.NextVisible(true));
        }

        static void ValidateDisplayHierarchy(VisitorInstaller installer, string path, ICollection<CommercialValidationIssue> issues)
        {
            var serialized = new SerializedObject(installer);
            var displayRoot = serialized.FindProperty("_spatialDisplayRoot")?.objectReferenceValue as Transform;
            var shell = serialized.FindProperty("_frontendShell")?.objectReferenceValue as Component;
            if (displayRoot == null || shell == null) return;

            var shellTransform = shell.transform;
            if (shellTransform != displayRoot && !shellTransform.IsChildOf(displayRoot))
                CommercialArchitectureValidator.Add(issues, "COM-HOST-002", path, "SpatialHost", $"VisitorInstaller SpatialDisplayRoot '{displayRoot.name}' does not own GlobalFrontendShell '{shell.name}'. The host must move the complete frontend hierarchy.");
            if (displayRoot.childCount == 0)
                CommercialArchitectureValidator.Add(issues, "COM-HOST-003", path, "SpatialHost", $"VisitorInstaller SpatialDisplayRoot '{displayRoot.name}' is empty.");

            foreach (var canvas in shell.GetComponentsInChildren<Canvas>(true).Where(x => x.renderMode == RenderMode.WorldSpace))
            {
                var rect = canvas.transform as RectTransform;
                if (rect == null) continue;
                var scale = rect.lossyScale;
                var width = Mathf.Abs(rect.rect.width * scale.x);
                var height = Mathf.Abs(rect.rect.height * scale.y);
                if (!ReasonableWorldCanvasDimension(width) || !ReasonableWorldCanvasDimension(height))
                    CommercialArchitectureValidator.Add(issues, "COM-SHELL-003", path, "FrontendShell", $"World-space Canvas '{canvas.name}' has unreasonable physical size {width:0.###}m x {height:0.###}m. Each dimension must be between {MinimumWorldCanvasDimensionMeters:0.##}m and {MaximumWorldCanvasDimensionMeters:0.##}m.");
            }
        }

        static void ValidateFrontendTopology(VisitorInstaller installer, string path, ICollection<CommercialValidationIssue> issues)
        {
            var installerSerialized = new SerializedObject(installer);
            var shell = installerSerialized.FindProperty("_frontendShell")?.objectReferenceValue as GlobalFrontendShell;
            if (shell == null) return;

            RequireObjectReference(installerSerialized, "_headGazeInteraction", path, "COM-SHELL-047",
                "Visitor head-gaze interaction is missing.", issues);
            RequireObjectReference(installerSerialized, "_startupRecallPresentation", path, "COM-SHELL-054",
                "Visitor Startup/Recall presentation is missing.", issues);
            try
            {
                shell.ValidateConfiguration();
            }
            catch (Exception exception)
            {
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-053", path, "FrontendShell",
                    $"GlobalFrontendShell semantic configuration is invalid: {exception.Message}");
            }
            ValidateFrontendCanvas(shell, shell.FrontendRoot as RectTransform, path, issues);

            var featurePages = installerSerialized.FindProperty("_featurePages");
            ValidateInitialFrontend(featurePages, "_video", "VideoFrontend", new[] { "_pageRoot", "_controlsRoot" }, path, issues);
            ValidateInitialFrontend(featurePages, "_model", "ModelFrontend", new[] { "_pageRoot", "_controlsRoot" }, path, issues);
            ValidateInitialFrontend(featurePages, "_panorama", "PanoramaFrontend", Array.Empty<string>(), path, issues);
            ValidateInitialFrontend(featurePages, "_narration", "NarrationFrontend", new[] { "_dockRoot" }, path, issues);
        }

        static void ValidateFrontendCanvas(
            Component shell,
            RectTransform shellRoot,
            string path,
            ICollection<CommercialValidationIssue> issues)
        {
            var canvas = shell.GetComponent<Canvas>();
            var raycaster = shell.GetComponent<GraphicRaycaster>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace || raycaster == null)
            {
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-027", path, "FrontendShell",
                    "GlobalFrontendShell requires a world-space Canvas and GraphicRaycaster input surface.");
                return;
            }

            var shellBackground = shellRoot != null ? shellRoot.GetComponent<Image>() : null;
            if (shellBackground != null && shellBackground.raycastTarget)
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-029", path, "FrontendShell",
                    "FrontendShell background must not consume input raycasts.");

            foreach (var button in shell.GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<Collider>() != null)
                    CommercialArchitectureValidator.Add(issues, "COM-SHELL-032", path, "FrontendShell",
                        $"Flow button '{button.name}' must use GraphicRaycaster input, not a Collider workaround.");
            }
        }

        static void ValidateInitialFrontend(
            SerializedProperty featurePages,
            string propertyName,
            string expectedTypeName,
            string[] rootProperties,
            string path,
            ICollection<CommercialValidationIssue> issues)
        {
            var property = featurePages?.FindPropertyRelative(propertyName);
            var component = property?.objectReferenceValue as Component;
            if (component == null)
            {
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-017", path, "FrontendShell", $"FeaturePageBindings reference '{propertyName}' is missing.");
                return;
            }
            if (!string.Equals(component.GetType().Name, expectedTypeName, StringComparison.Ordinal))
            {
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-018", path, "FrontendShell", $"FeaturePageBindings '{propertyName}' must reference {expectedTypeName}.");
                return;
            }

            if (rootProperties.Length == 0 && component.gameObject.activeSelf)
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-020", path, "FrontendShell", $"Unbound {expectedTypeName} object '{component.gameObject.name}' must be inactive in the production Prefab.");

            var serialized = new SerializedObject(component);
            foreach (var rootProperty in rootProperties)
            {
                var root = serialized.FindProperty(rootProperty)?.objectReferenceValue as GameObject;
                if (root == null)
                {
                    CommercialArchitectureValidator.Add(issues, "COM-SHELL-019", path, "FrontendShell", $"{expectedTypeName} reference '{rootProperty}' is missing.");
                }
                else if (root.activeSelf)
                {
                    CommercialArchitectureValidator.Add(issues, "COM-SHELL-020", path, "FrontendShell", $"Unbound {expectedTypeName} root '{root.name}' must be inactive in the production Prefab.");
                }
            }

            if (string.Equals(expectedTypeName, "PanoramaFrontend", StringComparison.Ordinal))
            {
                foreach (var child in component.GetComponentsInChildren<Component>(true))
                {
                    if (child == null) continue;
                    var typeName = child.GetType().Name;
                    if (typeName == "Button" || typeName == "Slider" || typeName == "GraphicRaycaster" || typeName == "RawImage")
                        CommercialArchitectureValidator.Add(issues, "COM-SHELL-021", path, "FrontendShell", $"PanoramaFrontend contains an obsolete UI/pointer component '{typeName}'.");
                }
            }

            foreach (var child in component.GetComponentsInChildren<Transform>(true))
                if (child.name.IndexOf("NarrationPage", StringComparison.OrdinalIgnoreCase) >= 0)
                    CommercialArchitectureValidator.Add(issues, "COM-SHELL-022", path, "FrontendShell", $"Retired fourth-page object remains under FrontendShell: '{child.name}'.");
        }

        static void RequireObjectReference(
            SerializedObject serialized,
            string propertyPath,
            string path,
            string code,
            string message,
            ICollection<CommercialValidationIssue> issues)
        {
            if (serialized.FindProperty(propertyPath)?.objectReferenceValue == null)
                CommercialArchitectureValidator.Add(issues, code, path, "FrontendShell", message);
        }

        static void ValidateLegacyRay(IEnumerable<Component> components, string path, ICollection<CommercialValidationIssue> issues)
        {
            foreach (var legacyRay in components.Where(x => string.Equals(x.GetType().Name, LegacyRayTypeName, StringComparison.Ordinal)))
            {
                if (!legacyRay.gameObject.activeInHierarchy) continue;
                var behaviourEnabled = !(legacyRay is Behaviour behaviour) || behaviour.enabled;
                var visibleRenderer = legacyRay.GetComponentsInChildren<Renderer>(true).Any(x => x.enabled && x.gameObject.activeInHierarchy);
                if (behaviourEnabled || visibleRenderer)
                    CommercialArchitectureValidator.Add(issues, "COM-SHELL-004", path, "FrontendShell", $"Legacy OVRRayHelper on '{HierarchyPath(legacyRay.transform)}' is active and can render the obsolete white controller ray.");
            }
        }

        static void ValidateHeadGazeInput(IEnumerable<Component> components, IEnumerable<VisitorInstaller> installers, string path, ICollection<CommercialValidationIssue> issues)
        {
            var inputs = components.Where(x =>
                    string.Equals(x.GetType().Name, HeadGazeInputTypeName, StringComparison.Ordinal) &&
                    x.gameObject.activeInHierarchy &&
                    (!(x is Behaviour behaviour) || behaviour.enabled))
                .ToArray();
            if (inputs.Length != 1)
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-005", path, "FrontendShell", $"Visitor Build Scene requires exactly one enabled {HeadGazeInputTypeName} pointer input; found {inputs.Length}.");
            if (inputs.Length == 1 && installers.Any(x => !HasSerializedReference(x, inputs[0])))
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-006", path, "FrontendShell", $"VisitorInstaller does not explicitly reference the enabled {HeadGazeInputTypeName} pointer input.");
            if (inputs.Length != 1) return;

            var input = inputs[0];
            var presenters = components.Where(x => x.GetType().Name == "StartupRecallPresenter" &&
                x.gameObject.activeInHierarchy && (!(x is Behaviour behaviour) || behaviour.enabled)).ToArray();
            if (presenters.Length != 1 || installers.Any(x => presenters.Length == 1 && !HasSerializedReference(x, presenters[0])))
            {
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-054", path, "FrontendShell",
                    "Visitor requires exactly one enabled StartupRecallPresenter explicitly bound by VisitorInstaller.");
                return;
            }
            var presenter = presenters[0];
            var presentation = new SerializedObject(presenter);
            var recallButton = presentation.FindProperty("_recallButton")?.objectReferenceValue as Button;
            foreach (var field in new[] { "_startupRoot", "_startupBackground", "_startupText", "_recallButton", "_nextStationButton", "_skipPointButton" })
                if (presentation.FindProperty(field)?.objectReferenceValue == null)
                    CommercialArchitectureValidator.Add(issues, "COM-SHELL-007", path, "FrontendShell",
                        "Startup/Recall presentation is missing its serialized binding: " + field);
            if (recallButton != null)
            {
                if (recallButton.GetComponent<Collider>() != null)
                    CommercialArchitectureValidator.Add(issues, "COM-SHELL-034", path, "FrontendShell",
                        "Startup recall button must use GraphicRaycaster input, not a Collider workaround.");
            }
            foreach (var installer in installers)
            {
                var displayRoot = new SerializedObject(installer).FindProperty("_spatialDisplayRoot")?.objectReferenceValue as Transform;
                if (displayRoot != null && (input.transform == displayRoot || input.transform.IsChildOf(displayRoot) ||
                    presenter.transform == displayRoot || presenter.transform.IsChildOf(displayRoot)))
                    CommercialArchitectureValidator.Add(issues, "COM-SHELL-008", path, "FrontendShell", "Gaze input and Startup/Recall presentation must remain outside SpatialDisplayRoot so they stay available while the host is closed.");
            }
        }

        static void ValidateApplicationMode(
            Component[] components,
            string path,
            bool isAdmin,
            ICollection<CommercialValidationIssue> issues)
        {
            var hosts = components.OfType<ApplicationModeControllerHost>().ToArray();
            var inputs = components.OfType<MetaLeftMenuHoldAdapter>().ToArray();
            var presenters = components.OfType<ApplicationModePromptPresenter>().ToArray();
            if (hosts.Length != 1 || inputs.Length != 1 || presenters.Length != 1)
            {
                CommercialArchitectureValidator.Add(issues, "COM-MODE-001", path, "ApplicationMode",
                    $"Build Scene requires one ApplicationMode Host/Input/Presenter; found {hosts.Length}/{inputs.Length}/{presenters.Length}.");
                return;
            }

            var hostSerialized = new SerializedObject(hosts[0]);
            var expectedRole = isAdmin
                ? ApplicationModeRole.Administrator
                : ApplicationModeRole.Visitor;
            var actualRole = (ApplicationModeRole)(
                hostSerialized.FindProperty("_sceneRole")?.enumValueIndex ?? -1);
            var options = hostSerialized.FindProperty("_options")?.objectReferenceValue
                as ApplicationModeOptionsAsset;
            var reason = string.Empty;
            var optionsValid = options != null && options.TryValidate(expectedRole, out reason);
            if (actualRole != expectedRole || !optionsValid)
                CommercialArchitectureValidator.Add(issues, "COM-MODE-002", path, "ApplicationMode",
                    $"ApplicationMode role/options are invalid for {expectedRole}: " +
                    $"{(string.IsNullOrWhiteSpace(reason) ? "role_or_options_missing" : reason.TrimEnd('.'))}.");
            if (options != null &&
                (!options.TryGetBuildIndex(expectedRole, out var buildIndex) ||
                 buildIndex < 0 ||
                 buildIndex >= EditorBuildSettings.scenes.Length ||
                 !EditorBuildSettings.scenes[buildIndex].enabled ||
                 !string.Equals(
                     EditorBuildSettings.scenes[buildIndex].path,
                     path,
                     StringComparison.Ordinal)))
                CommercialArchitectureValidator.Add(issues, "COM-MODE-004", path, "ApplicationMode",
                    $"ApplicationMode {expectedRole} build index must resolve to this enabled Build Scene.");

            try
            {
                presenters[0].ValidateConfiguration(hosts[0]);
            }
            catch (Exception exception)
            {
                CommercialArchitectureValidator.Add(issues, "COM-MODE-003", path, "ApplicationMode",
                    $"ApplicationMode prompt semantic configuration is invalid: {exception.Message}");
            }
        }

        static void ValidateAdminInteraction(
            Component[] components,
            string path,
            ICollection<CommercialValidationIssue> issues)
        {
            var officialBridges = ComponentsOfType(components, AdminOfficialBridgeTypeName);
            var officialPresentations = ComponentsOfType(
                components,
                AdminOfficialPanelPresentationTypeName);
            var placements = ComponentsOfType(components, AdminPanelPlacementTypeName);
            var anchorManagers = ComponentsOfType(components, "AnchorUIManager");
            if (officialBridges.Length != 1 ||
                officialPresentations.Length != 1 ||
                placements.Length != 1 ||
                anchorManagers.Length != 1)
            {
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-ADMIN-003",
                    path,
                    "SpatialAnchorAdmin",
                    $"Admin requires one official anchor manager, bridge, single presentation, and panel placement; found {anchorManagers.Length}/{officialBridges.Length}/{officialPresentations.Length}/{placements.Length}.");
                return;
            }

            var bridge = officialBridges[0];
            var presentation = officialPresentations[0];
            var placement = placements[0];
            var manager = anchorManagers[0];
            var bridgeSerialized = new SerializedObject(bridge);
            var presentationSerialized = new SerializedObject(presentation);
            var placementSerialized = new SerializedObject(placement);
            var anchorManagerUiSerialized = new SerializedObject(manager);
            var panelRoot = presentationSerialized.FindProperty("_panelRoot")
                ?.objectReferenceValue as RectTransform;
            var instructionCanvas = presentationSerialized.FindProperty("_controllerHintCanvas")
                ?.objectReferenceValue as Canvas;
            var officialCanvas = panelRoot != null
                ? panelRoot.GetComponentInParent<Canvas>(true)
                : null;
            var createButton = presentationSerialized.FindProperty("_createButton")
                ?.objectReferenceValue as Button;
            var loadButton = presentationSerialized.FindProperty("_loadButton")
                ?.objectReferenceValue as Button;
            var eraseButton = presentationSerialized.FindProperty("_eraseAnchorButton")
                ?.objectReferenceValue as Button;
            var standardActions = presentationSerialized.FindProperty("_standardActionsRoot")
                ?.objectReferenceValue as GameObject;
            var candidateActions = presentationSerialized.FindProperty("_candidateActionsRoot")
                ?.objectReferenceValue as GameObject;
            var saveCandidateButton = presentationSerialized.FindProperty("_saveCandidateButton")
                ?.objectReferenceValue as Button;
            var renameAnchorButton = presentationSerialized.FindProperty("_renameAnchorButton")
                ?.objectReferenceValue as Button;
            var anchorIdentityText = presentationSerialized.FindProperty("_anchorIdentityText")
                ?.objectReferenceValue;
            var cancelCandidateButton = presentationSerialized.FindProperty("_cancelCandidateButton")
                ?.objectReferenceValue as Button;
            var createLabel = createButton != null
                ? ComponentsOfType(
                        createButton.GetComponentsInChildren<Component>(true),
                        TextMeshProUguiTypeName)
                    .FirstOrDefault()
                : null;
            var selectModeButton = anchorManagerUiSerialized.FindProperty("_selectModeButton")
                ?.objectReferenceValue as GameObject;
            var eraseLabel = eraseButton != null
                ? ComponentsOfType(
                        eraseButton.GetComponentsInChildren<Component>(true),
                        TextMeshProUguiTypeName)
                    .FirstOrDefault()
                : null;
            string ButtonCopy(Button button)
            {
                if (button == null) return null;
                var label = ComponentsOfType(
                        button.GetComponentsInChildren<Component>(true),
                        TextMeshProUguiTypeName)
                    .FirstOrDefault();
                return label?.GetType().GetProperty("text")?.GetValue(label) as string;
            }
            var presentationBindingsValid =
                presentationSerialized.FindProperty("_officialAnchorAdminSource")
                    ?.objectReferenceValue == bridge &&
                createButton != null &&
                createButton.gameObject.activeSelf &&
                string.Equals(
                    createLabel?.GetType().GetProperty("text")?.GetValue(createLabel) as string,
                    "放置锚点",
                    StringComparison.Ordinal) &&
                createButton.onClick.GetPersistentEventCount() == 0 &&
                standardActions != null &&
                loadButton != null &&
                loadButton.gameObject.activeSelf &&
                loadButton.transform.parent == standardActions.transform &&
                loadButton.onClick.GetPersistentEventCount() == 0 &&
                (ButtonCopy(loadButton)?.StartsWith("查看锚点（", StringComparison.Ordinal) ?? false) &&
                selectModeButton != null &&
                !selectModeButton.activeSelf &&
                eraseButton != null &&
                eraseButton.gameObject.activeSelf &&
                eraseButton.onClick.GetPersistentEventCount() == 0 &&
                string.Equals(
                    eraseLabel?.GetType().GetProperty("text")?.GetValue(eraseLabel) as string,
                    "删除锚点",
                    StringComparison.Ordinal) &&
                eraseButton.transform.parent == standardActions.transform &&
                standardActions.GetComponentsInChildren<Button>(true).Length == 3 &&
                candidateActions != null &&
                !candidateActions.activeSelf &&
                candidateActions.transform.parent == panelRoot &&
                saveCandidateButton != null &&
                saveCandidateButton.transform.parent == candidateActions.transform &&
                saveCandidateButton.onClick.GetPersistentEventCount() == 0 &&
                string.Equals(ButtonCopy(saveCandidateButton), "保存锚点", StringComparison.Ordinal) &&
                renameAnchorButton != null &&
                renameAnchorButton.transform.parent == candidateActions.transform &&
                renameAnchorButton.onClick.GetPersistentEventCount() == 0 &&
                string.Equals(ButtonCopy(renameAnchorButton), "修改名称", StringComparison.Ordinal) &&
                anchorIdentityText != null &&
                cancelCandidateButton != null &&
                cancelCandidateButton.transform.parent == candidateActions.transform &&
                cancelCandidateButton.onClick.GetPersistentEventCount() == 0 &&
                string.Equals(ButtonCopy(cancelCandidateButton), "取消", StringComparison.Ordinal) &&
                candidateActions.GetComponentsInChildren<Button>(true).Length == 3 &&
                candidateActions.transform.Find("ReplaceAnchorCandidate") == null &&
                presentationSerialized.FindProperty("_workflowStatusText")
                    ?.objectReferenceValue != null;
            var configurationAssociationValid =
                bridgeSerialized.FindProperty("_physicalAugmentationCatalog")?.objectReferenceValue != null;
            var publishedAdminCopies = ComponentsOfType(components, TextMeshProUguiTypeName)
                .Select(component => component.GetType().GetProperty("text")?.GetValue(component) as string ?? string.Empty)
                .ToArray();
            var singleWorkflowValid = panelRoot != null &&
                                      panelRoot.gameObject.activeSelf &&
                                      officialCanvas != null &&
                                      officialCanvas.transform.childCount == 1 &&
                                      officialCanvas.transform.GetChild(0) == panelRoot &&
                                      panelRoot.Find("AnchorCandidateActions") == candidateActions.transform &&
                                      publishedAdminCopies.All(copy =>
                                          copy.IndexOf("候选不会自动保存", StringComparison.Ordinal) < 0 &&
                                          copy.IndexOf("放置后自动保存", StringComparison.Ordinal) < 0 &&
                                          copy.IndexOf("按 X 后自动保存", StringComparison.Ordinal) < 0 &&
                                          copy.IndexOf("返回管理员菜单", StringComparison.Ordinal) < 0) &&
                                      components.OfType<Transform>().All(transform =>
                                          transform.name != "PhysicalAugmentationAuthoringPanel" &&
                                          transform.name != "PhysicalAugmentationAdminPreviewRoot" &&
                                          transform.name != "OpenPhysicalAugmentationAuthoring" &&
                                          transform.name != "ConfirmPhysicalBinding");
            var instructionLabel = instructionCanvas != null
                ? ComponentsOfType(
                        instructionCanvas.GetComponentsInChildren<Component>(true),
                        TextMeshProUguiTypeName)
                    .FirstOrDefault()
                : null;
            var instructionText = instructionLabel?.GetType().GetProperty("text")
                ?.GetValue(instructionLabel) as string;
            var instructionValid = instructionCanvas != null &&
                                   instructionCanvas != officialCanvas &&
                                   instructionCanvas.transform is RectTransform instructionRect &&
                                   instructionRect.sizeDelta.y <= 72f &&
                                   !string.IsNullOrWhiteSpace(instructionText) &&
                                   instructionText.IndexOf("待保存候选", StringComparison.Ordinal) >= 0 &&
                                   instructionText.IndexOf("点击已保存锚点", StringComparison.Ordinal) >= 0 &&
                                   instructionText.IndexOf("长按 Y 取消", StringComparison.Ordinal) >= 0 &&
                                   instructionText.IndexOf("返回菜单", StringComparison.Ordinal) < 0 &&
                                   instructionCanvas.GetComponents<BaseRaycaster>().Length == 0 &&
                                   instructionCanvas.GetComponentsInChildren<Graphic>(true)
                                       .All(graphic => !graphic.raycastTarget);
            var placementValid =
                bridgeSerialized.FindProperty("_lifecycleSource")?.objectReferenceValue == manager &&
                placementSerialized.FindProperty("_officialAnchorAdminSource")?.objectReferenceValue == bridge &&
                placementSerialized.FindProperty("_viewer")?.objectReferenceValue != null &&
                (placementSerialized.FindProperty("_summonOnEnable")?.boolValue ?? false);
            if (!presentationBindingsValid ||
                !configurationAssociationValid ||
                !singleWorkflowValid ||
                !instructionValid ||
                !placementValid)
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-ADMIN-004",
                    path,
                    "SpatialAnchorAdmin",
                    "Official anchor workspace must expose mutually exclusive Place/View/Delete, explicit Save/Cancel Candidate, and Rename/Resave/Cancel Adjustment states in one panel, keep that panel as the official Canvas's only direct child, contain no Point/calibration/Binding workspace, and publish the unsaved-candidate/long-Y hint.");

            var ovrHands = ComponentsOfType(components, OvrHandTypeName);
            var pointableModules = ComponentsOfType(components, PointableCanvasModuleTypeName);
            var pointableCanvases = ComponentsOfType(components, PointableCanvasTypeName);
            var cameraRigs = ComponentsOfType(components, OvrCameraRigTypeName);
            var ovrManagers = ComponentsOfType(components, OvrManagerTypeName);
            var rigRefs = ComponentsOfType(components, OvrCameraRigRefTypeName);
            var selectionFallbacks = ComponentsOfType(
                components,
                AdminPointableSelectionFallbackTypeName);
            var gazeInputs = ComponentsOfType(components, AdminGazeGestureInputTypeName);
            var controllerInputs = ComponentsOfType(components, AdminControllerInputTypeName);
            var eventSystems = components.OfType<EventSystem>().ToArray();
            var controllerSerialized = controllerInputs.Length == 1
                ? new SerializedObject(controllerInputs[0])
                : null;
            var controllerBound = controllerSerialized != null &&
                                  new[]
                                  {
                                      "_rayOrigin",
                                      "_placementPose",
                                      "_eventSystem",
                                      "_officialAnchorAdminSource",
                                      "_pointerLine",
                                      "_focusRing"
                                  }.All(field =>
                                      controllerSerialized.FindProperty(field)
                                          ?.objectReferenceValue != null);
            var focusRing = controllerSerialized?.FindProperty("_focusRing")
                ?.objectReferenceValue as Graphic;
            var namedFocusRings = components.OfType<Graphic>()
                .Where(graphic => string.Equals(
                    graphic.name,
                    "AdminControllerFocusRing",
                    StringComparison.Ordinal))
                .ToArray();
            var focusRingRect = focusRing?.transform as RectTransform;
            var focusRingUiValid = focusRing != null &&
                                   string.Equals(
                                       focusRing.GetType().FullName,
                                       AdminPointerRingGraphicTypeName,
                                       StringComparison.Ordinal) &&
                                   focusRingRect != null &&
                                   (focusRingRect.sizeDelta - Vector2.one).sqrMagnitude < 0.000001f &&
                                   focusRing.GetComponent<CanvasRenderer>() != null &&
                                   !focusRing.raycastTarget &&
                                   !focusRing.enabled &&
                                   namedFocusRings.Length == 1 &&
                                   namedFocusRings[0] == focusRing &&
                                   focusRing.GetComponent<MeshFilter>() == null &&
                                   focusRing.GetComponent<MeshRenderer>() == null &&
                                   focusRing.GetComponent<SortingGroup>() == null &&
                                   focusRing.GetComponent<LineRenderer>() == null &&
                                   focusRing.GetComponent<Collider>() == null &&
                                   focusRing.GetComponent<GraphicRaycaster>() == null;
            var managerSerialized = ovrManagers.Length == 1
                ? new SerializedObject(ovrManagers[0])
                : null;
            var simultaneousInputDisabled = managerSerialized != null &&
                                            !(managerSerialized.FindProperty(
                                                "SimultaneousHandsAndControllersEnabled")
                                                ?.boolValue ?? true) &&
                                            !(managerSerialized.FindProperty(
                                                "launchSimultaneousHandsControllersOnStartup")
                                                ?.boolValue ?? true);
            var leftController = cameraRigs.Length == 1
                ? cameraRigs[0].GetComponentsInChildren<Transform>(true)
                    .SingleOrDefault(transform => string.Equals(
                        transform.name,
                        "LeftControllerInHandAnchor",
                        StringComparison.Ordinal))
                : null;
            var rayOrigin = controllerSerialized?.FindProperty("_rayOrigin")
                ?.objectReferenceValue as Transform;
            var placementPose = controllerSerialized?.FindProperty("_placementPose")
                ?.objectReferenceValue as Transform;
            var pointerLine = controllerSerialized?.FindProperty("_pointerLine")
                ?.objectReferenceValue as LineRenderer;
            var anchorManagerSerialized = new SerializedObject(manager);
            var placementGuide = anchorManagerSerialized.FindProperty("_lineRenderer")
                ?.objectReferenceValue as LineRenderer;
            var officialControllerBinding =
                leftController != null &&
                rayOrigin == leftController &&
                placementPose != null &&
                placementPose.IsChildOf(leftController) &&
                controllerSerialized?.FindProperty("_eventSystem")?.objectReferenceValue ==
                    eventSystems.SingleOrDefault() &&
                anchorManagerSerialized.FindProperty("_trackedDevice")
                    ?.objectReferenceValue == leftController &&
                anchorManagerSerialized.FindProperty("_anchorPlacementTransform")
                    ?.objectReferenceValue == placementPose &&
                pointerLine != null &&
                placementGuide != null &&
                pointerLine != placementGuide;
            if (cameraRigs.Length != 1 ||
                ovrManagers.Length != 1 ||
                ovrHands.Length != 0 ||
                rigRefs.Length != 0 ||
                pointableModules.Length != 0 ||
                pointableCanvases.Length != 0 ||
                selectionFallbacks.Length != 0 ||
                gazeInputs.Length != 0 ||
                controllerInputs.Length != 1 ||
                eventSystems.Length != 1 ||
                !controllerBound ||
                !focusRingUiValid ||
                !simultaneousInputDisabled ||
                !officialControllerBinding)
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-ADMIN-005",
                    path,
                    "SpatialAnchorAdmin",
                    $"Admin requires one left-controller input bound to the official placement pose, a separate UI pointer and placement guide, one EventSystem, and controller-only OVRManager settings. Found controller={controllerInputs.Length}, eventSystems={eventSystems.Length}, managers={ovrManagers.Length}, hands={ovrHands.Length}, gaze={gazeInputs.Length}, pointableModules={pointableModules.Length}, pointableCanvases={pointableCanvases.Length}, rigRefs={rigRefs.Length}, fallbacks={selectionFallbacks.Length}, focusRingValid={focusRingUiValid}, officialBinding={officialControllerBinding}.");

            var interactiveCanvases = placement.GetComponentsInChildren<Canvas>(true)
                .Where(canvas => canvas.GetComponentInChildren<Button>(true) != null)
                .ToArray();
            var targetsAreQuestSized = HasQuestSizedAuthoringTargets(
                officialCanvas,
                placement,
                out var targetSizingReason);
            if (interactiveCanvases.Length != 1 ||
                interactiveCanvases[0] != officialCanvas ||
                !HasControllerInteraction(officialCanvas) ||
                !targetsAreQuestSized)
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-ADMIN-006",
                    path,
                    "SpatialAnchorAdmin",
                    $"Admin must expose exactly one Quest-sized official-anchor World Space Canvas with GraphicRaycaster and no PointableCanvas surface: {targetSizingReason}");

            var activeLocomotion = components.Where(component =>
                    component is Behaviour behaviour &&
                    behaviour.isActiveAndEnabled &&
                    (component.GetType().FullName ?? string.Empty)
                        .StartsWith(
                            "Oculus.Interaction.Locomotion.",
                            StringComparison.Ordinal))
                .ToArray();
            var activeMovementBranches = components.OfType<Transform>()
                .Where(transform =>
                    transform.gameObject.activeInHierarchy &&
                    IsVirtualMovementBranch(transform.name))
                .ToArray();
            if (activeLocomotion.Length != 0 || activeMovementBranches.Length != 0)
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-ADMIN-007",
                    path,
                    "SpatialAnchorAdmin",
                    $"Admin must not contain locomotion, teleport, or tunneling branches; found {activeLocomotion.Length} active locomotion behaviours and {activeMovementBranches.Length} active movement branches.");

            var retiredTokens = new[]
            {
                "RouteAuthoring",
                "RouteCalibration",
                "EnvironmentInspection",
                "SpatialInstallationAuthoring",
                "NavigationNode",
                "PublishActive"
            };
            var retiredObjects = components.OfType<Transform>()
                .Where(transform => retiredTokens.Any(token =>
                    transform.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(HierarchyPath)
                .ToArray();
            var retiredTypes = components
                .Where(component =>
                {
                    var fullName = component.GetType().FullName ?? string.Empty;
                    return fullName.IndexOf("GuidedNavigation", StringComparison.Ordinal) >= 0 ||
                           fullName.IndexOf("SpatialInstallation", StringComparison.Ordinal) >= 0 ||
                           fullName.IndexOf("VenueNavigation", StringComparison.Ordinal) >= 0 ||
                           fullName.IndexOf("AdminMruk", StringComparison.Ordinal) >= 0;
                })
                .Select(component => component.GetType().FullName)
                .ToArray();
            if (retiredObjects.Length != 0 || retiredTypes.Length != 0)
                CommercialArchitectureValidator.Add(
                    issues,
                    "COM-ADMIN-008",
                    path,
                    "SpatialAnchorAdmin",
                    $"Retired route-authoring content remains in Admin. Objects=[{string.Join(", ", retiredObjects)}], types=[{string.Join(", ", retiredTypes)}].");
        }
        static bool HasQuestSizedAuthoringTargets(Canvas canvas, Component placement, out string reason)
        {
            reason = string.Empty;
            if (canvas == null || placement == null)
            {
                reason = "canvas_or_placement_missing";
                return false;
            }

            var canvasRect = canvas.GetComponent<RectTransform>();
            var worldScaleProperty = new SerializedObject(placement).FindProperty("_worldScale");
            if (canvasRect == null || worldScaleProperty == null)
            {
                reason = "rect_or_scale_missing";
                return false;
            }

            var worldScale = worldScaleProperty.floatValue;
            // The Admin panel relies on nested LayoutGroups. When a scene is opened
            // headlessly, Unity may not have evaluated those groups yet, so force one
            // in-memory layout pass before measuring authored button targets.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
            var buttons = canvas.GetComponentsInChildren<Button>(true);
            if (buttons.Length == 0)
            {
                reason = "buttons_missing";
                return false;
            }

            foreach (var button in buttons)
            {
                var rect = button.GetComponent<RectTransform>();
                var label = ComponentsOfType(
                        button.GetComponentsInChildren<Component>(true),
                        TextMeshProUguiTypeName)
                    .FirstOrDefault();
                if (rect == null || label == null)
                {
                    reason = $"{button.name}_rect_or_label_missing";
                    return false;
                }

                var fontSizeValue = label.GetType().GetProperty("fontSize")?.GetValue(label);
                var fontSize = fontSizeValue is float value ? value : 0f;
                // LayoutElement/VerticalLayoutGroup can leave the serialized RectTransform
                // at zero until Unity performs a layout pass. Validate the size that the
                // authored layout will produce instead of rejecting a valid button as 0 m.
                var layoutHeight = rect.rect.height;
                if (layoutHeight <= Mathf.Epsilon)
                    layoutHeight = LayoutUtility.GetPreferredHeight(rect);
                if (layoutHeight <= Mathf.Epsilon)
                    layoutHeight = LayoutUtility.GetMinHeight(rect);
                var physicalHeight = layoutHeight * Mathf.Abs(canvasRect.localScale.y) * worldScale;
                if (physicalHeight < 0.064f || fontSize < 20f)
                {
                    reason = $"{button.name}_height_{physicalHeight:0.000}m_font_{fontSize:0.#}";
                    return false;
                }

                // Layout groups can drive the button's anchored position and size in a
                // nested parent. Convert its actual world corners into the official
                // Canvas's local space instead of comparing child-local coordinates
                // directly with the Canvas rect.
                var buttonBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    canvas.transform,
                    rect.transform);
                if (buttonBounds.min.x < canvasRect.rect.xMin ||
                    buttonBounds.min.y < canvasRect.rect.yMin ||
                    buttonBounds.max.x > canvasRect.rect.xMax ||
                    buttonBounds.max.y > canvasRect.rect.yMax)
                {
                    reason = $"{button.name}_outside_canvas";
                    return false;
                }
            }

            return true;
        }

        static bool IsVirtualMovementBranch(string name)
            => !string.IsNullOrWhiteSpace(name) &&
               (name.IndexOf("Locomotor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Locomotion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Teleport", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Tunneling", StringComparison.OrdinalIgnoreCase) >= 0);

        static Component[] ComponentsOfType(
            IEnumerable<Component> components,
            string fullTypeName)
            => components.Where(component => component != null &&
                    string.Equals(component.GetType().FullName, fullTypeName, StringComparison.Ordinal))
                .ToArray();

        static bool HasControllerInteraction(Canvas canvas)
        {
            if (canvas == null) return false;
            var components = canvas.GetComponents<Component>();
            return canvas.renderMode == RenderMode.WorldSpace &&
                   canvas.GetComponent<GraphicRaycaster>() != null &&
                   ComponentsOfType(components, PointableCanvasTypeName).Length == 0;
        }

        static void ValidateGazeReticlePresenter(IEnumerable<Component> components, IEnumerable<VisitorInstaller> installers, string path, ICollection<CommercialValidationIssue> issues)
        {
            var presenters = components.Where(x =>
                    string.Equals(x.GetType().Name, GazeReticlePresenterTypeName, StringComparison.Ordinal) &&
                    x.gameObject.activeInHierarchy &&
                    (!(x is Behaviour behaviour) || behaviour.enabled))
                .ToArray();
            if (presenters.Length != 1)
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-035", path, "FrontendShell", $"Visitor Build Scene requires exactly one enabled {GazeReticlePresenterTypeName}; found {presenters.Length}.");
            if (presenters.Length == 1 && installers.Any(x => !HasSerializedReference(x, presenters[0])))
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-036", path, "FrontendShell", $"VisitorInstaller does not explicitly reference the enabled {GazeReticlePresenterTypeName}.");
            if (presenters.Length != 1) return;

            var presenter = presenters[0] as GazeReticlePresenter;
            if (presenter == null) return;
            try
            {
                presenter.ValidateConfiguration();
            }
            catch (Exception exception)
            {
                CommercialArchitectureValidator.Add(issues, "COM-SHELL-039", path, "FrontendShell",
                    $"Gaze reticle semantic configuration is invalid: {exception.Message}");
            }

            foreach (var installer in installers)
            {
                var displayRoot = new SerializedObject(installer).FindProperty("_spatialDisplayRoot")?.objectReferenceValue as Transform;
                if (displayRoot != null && (presenter.transform == displayRoot || presenter.transform.IsChildOf(displayRoot)))
                    CommercialArchitectureValidator.Add(issues, "COM-SHELL-037", path, "FrontendShell", "Gaze reticle presentation must remain outside SpatialDisplayRoot so a panel transition cannot remove startup scan feedback.");
            }
        }

        static bool HasSerializedReference(UnityEngine.Object owner, UnityEngine.Object expected)
        {
            var iterator = new SerializedObject(owner).GetIterator();
            if (!iterator.NextVisible(true)) return false;
            do if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == expected) return true;
            while (iterator.NextVisible(true));
            return false;
        }

        static bool Approximately(Vector3 left, Vector3 right)
            => Mathf.Abs(left.x - right.x) <= 0.0001f &&
               Mathf.Abs(left.y - right.y) <= 0.0001f &&
               Mathf.Abs(left.z - right.z) <= 0.0001f;

        static bool ReasonableWorldCanvasDimension(float value)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value >= MinimumWorldCanvasDimensionMeters && value <= MaximumWorldCanvasDimensionMeters;

        static string HierarchyPath(Transform transform)
        {
            var parts = new Stack<string>();
            for (var current = transform; current != null; current = current.parent) parts.Push(current.name);
            return string.Join("/", parts);
        }
    }
}
