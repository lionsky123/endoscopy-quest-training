using BotanicalGardenQR.Collection.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BotanicalGardenQR.Tests.EditMode.Bootstrap
{
    public sealed class VisitorRuntimeCompositionLifecycleTests
    {
        const string ScenePath = "Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity";
        Scene _scene;
        VisitorInstaller _installer;
        VisitorRuntimeComposition _composition;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            _installer = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<VisitorInstaller>(true))
                .SingleOrDefault();
            Assert.That(_installer, Is.Not.Null);
            var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(_installer.gameObject);
            if (prefabRoot != null)
                PrefabUtility.UnpackPrefabInstance(
                    prefabRoot,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
        }

        [TearDown]
        public void TearDown()
        {
            _composition?.Dispose();
            if (_scene.IsValid()) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Scene_UsesStableFloorBasedStageOriginWithoutRecentering()
        {
            var manager = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<OVRManager>(true))
                .SingleOrDefault();
            Assert.That(manager, Is.Not.Null, "Visitor requires exactly one OVRManager.");

            var serialized = new SerializedObject(manager);
            Assert.That(
                serialized.FindProperty("_trackingOriginType").intValue,
                Is.EqualTo((int)OVRManager.TrackingOrigin.Stage),
                "Meta XR Stage remains floor-based while avoiding FloorLevel's recenter-space refresh loop.");
            Assert.That(
                serialized.FindProperty("AllowRecenter").boolValue,
                Is.False,
                "A world-fixed visitor installation must not accept runtime recenter requests.");
            Assert.That(
                serialized.FindProperty("requestScenePermissionOnStartup").boolValue,
                Is.False,
                "OVRManager must not compete with the explicit application permission gate.");
            Assert.That(
                serialized.FindProperty("requestPassthroughCameraAccessPermissionOnStartup").boolValue,
                Is.False,
                "Physical Augmentation does not request raw passthrough camera access.");

            var gates = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .Where(component => component is ISpatialDataPermissionGate)
                .ToArray();
            Assert.That(gates, Has.Length.EqualTo(1));
            Assert.That(
                new SerializedObject(_installer).FindProperty("_spatialDataPermissionGate")
                    .objectReferenceValue,
                Is.SameAs(gates[0]));
        }

        [Test]
        public void Scene_ComposesOneHiddenPhysicalAnchorLocatorAndOneDepthManager()
        {
            var components = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .Where(component => component != null)
                .ToArray();
            var locator = components.SingleOrDefault(component => string.Equals(
                component.GetType().FullName,
                "BotanicalGardenQR.PhysicalAugmentation.Backend.MetaPhysicalAnchorLocator",
                StringComparison.Ordinal));

            Assert.That(locator, Is.Not.Null, "Visitor requires exactly one physical-anchor locator.");
            Assert.That(locator.GetComponentsInChildren<Renderer>(true), Is.Empty);
            Assert.That(locator.GetComponentsInChildren<Canvas>(true), Is.Empty);
            Assert.That(locator.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(locator.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            var depthManagers = components.Where(component => string.Equals(
                    component.GetType().FullName,
                    "Meta.XR.EnvironmentDepth.EnvironmentDepthManager",
                    StringComparison.Ordinal)).ToArray();
            Assert.That(depthManagers, Has.Length.EqualTo(1));
            Assert.That(((Behaviour)depthManagers[0]).enabled, Is.True);
            Assert.That(
                depthManagers[0].GetType().GetProperty("OcclusionShadersMode")
                    ?.GetValue(depthManagers[0])?.ToString(),
                Is.EqualTo("SoftOcclusion"));
            Assert.That(components.Any(component => string.Equals(
                    component.GetType().Name,
                    "SpatialAnchorLoader",
                    StringComparison.Ordinal)),
                Is.False, "Visitor must not compose the administrator's Meta anchor loader.");

            var installer = new SerializedObject(_installer);
            Assert.That(installer.FindProperty("_physicalAugmentationRuntimeHost")
                .objectReferenceValue, Is.Not.Null);
            Assert.That(
                components.Any(component => string.Equals(
                    component.GetType().FullName,
                    "BotanicalGardenQR.PhysicalAugmentation.Frontend.PhysicalAugmentationPresenter",
                    StringComparison.Ordinal)),
                Is.False,
                "Visitor must not retain the retired spatial cue/dwell presenter.");
            Assert.That(
                _scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ShellFlowActionTarget>(true))
                    .Count(target => target.Action == ShellFlowAction.FeaturePageAction),
                Is.EqualTo(1));
            Assert.That(
                _scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ShellFlowActionTarget>(true))
                    .Count(target => target.Action == ShellFlowAction.FeaturePageActionExitFocus),
                Is.EqualTo(1));
        }

        [Test]
        public void Create_RejectsMissingRequiredFairyAuthorityBeforeAllocatingRuntime()
        {
            var footprint = RuntimeFootprint();
            SetField("_fairyConfiguration", null);

            Assert.That(
                () => _installer.CreateValidatedBindings(),
                Throws.InvalidOperationException.With.Message.Contains("_fairyConfiguration"));
            Assert.That(RuntimeFootprint(), Is.EqualTo(footprint));
        }

        [Test]
        public void Create_WithoutSpatialNavigationDependencies_RemainsUsable()
        {
            var diagnostics = new List<DiagnosticEvent>();

            _composition = Create(diagnostics.Add);

            Assert.That(_composition, Is.Not.Null);
            Assert.That(diagnostics.Any(item => item.OwningLine == "GuidedNavigation"), Is.False);
        }

        [TestCase("_featurePages")]
        [TestCase("_startupRecallPresentation")]
        public void Installer_WhenARequiredPresentationBindingIsMissing_RejectsBeforeRuntimeAllocation(string binding)
        {
            var footprint = RuntimeFootprint();
            SetField(binding, null);

            Assert.That(
                () => _installer.CreateValidatedBindings(),
                Throws.InvalidOperationException.With.Message.Contains(binding));
            Assert.That(RuntimeFootprint(), Is.EqualTo(footprint));
        }

        [Test]
        public void Create_WhenValidatedRuntimeRootIsLostMidGraph_RollsBackEarlierRuntime()
        {
            var bindings = _installer.CreateValidatedBindings();
            var videoRoot = bindings.RuntimeRoots.Video;
            var videoComponentCount = videoRoot.GetComponents<Component>().Length;
            UnityEngine.Object.DestroyImmediate(bindings.RuntimeRoots.Model.gameObject);

            Assert.That(
                () => VisitorRuntimeComposition.Create(bindings, null),
                Throws.ArgumentNullException);
            Assert.That(videoRoot.GetComponents<Component>().Length, Is.EqualTo(videoComponentCount));
        }

        [Test]
        public void OwnershipScope_AdoptsChildrenAndReleasesOnceInReverseOrderDespiteFailure()
        {
            var calls = new List<string>();
            var owner = new BootstrapOwnershipScope();
            var child = new BootstrapOwnershipScope();
            owner.Register(() => calls.Add("owner.first"));
            child.Register(() => calls.Add("child.first"));
            child.Register(() => { calls.Add("child.second"); throw new InvalidOperationException("owned release failure"); });
            owner.Adopt(child);
            owner.Register(() => calls.Add("owner.last"));

            LogAssert.Expect(LogType.Exception, new Regex("owned release failure"));
            Assert.That(() => owner.Dispose(), Throws.Nothing);
            Assert.That(() => owner.Dispose(), Throws.Nothing);
            Assert.That(() => child.Dispose(), Throws.Nothing);

            Assert.That(
                calls,
                Is.EqualTo(new[] { "owner.last", "child.second", "child.first", "owner.first" }));
        }

        VisitorRuntimeComposition Create(Action<DiagnosticEvent> diagnostics)
            => VisitorRuntimeComposition.Create(
                _installer.CreateValidatedBindings(),
                diagnostics);

        string RuntimeFootprint()
        {
            var roots = new[] { "_videoRuntimeRoot", "_panoramaRuntimeRoot", "_modelRuntimeRoot", "_narrationRuntimeRoot", "_fairyRuntimeRoot", "_effectRuntimeRoot" }
                .Select(Field<Transform>)
                .Concat(new[] { Field<Component>("_physicalAugmentationRuntimeHost").transform });
            return string.Join("|", roots.Select(root => $"{root.childCount}:{root.GetComponents<Component>().Length}")) +
                   $"|{Field<Transform>("_spatialDisplayRoot").parent.childCount}";
        }

        T Field<T>(string name)
        {
            var field = InstallerField(name);
            return (T)field.GetValue(_installer);
        }

        void SetField(string name, object value) => InstallerField(name).SetValue(_installer, value);

        static FieldInfo InstallerField(string name)
        {
            var field = typeof(VisitorInstaller).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return field;
        }

    }
}
