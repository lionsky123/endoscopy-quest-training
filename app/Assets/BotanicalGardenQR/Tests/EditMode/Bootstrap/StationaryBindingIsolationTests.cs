using System;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Configuration.Runtime;
using NUnit.Framework;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BotanicalGardenQR.Tests.EditMode.Bootstrap
{
    public sealed class StationaryBindingIsolationTests
    {
        const string ScenePath="Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity";
        Scene _scene;
        VisitorInstaller _installer;

        [SetUp]
        public void SetUp()
        {
            _scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var roots=_scene.GetRootGameObjects();
            var installers=roots.SelectMany(root=>root.GetComponentsInChildren<VisitorInstaller>(true)).ToArray();
            Assert.That(installers.Length,Is.EqualTo(1),
                "Formal scene must contain exactly one installer; roots: "+
                string.Join(", ",roots.Select(root=>root.name)));
            _installer=installers.Single();
            var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(_installer.gameObject);
            if(prefab!=null)
                PrefabUtility.UnpackPrefabInstance(prefab,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            var options=(RuntimeEnvironmentOptions)Field("_runtimeOptions").GetValue(_installer);
            Assert.That(options.VirtualRoomEnabled,Is.True,"This check targets the current stationary route.");
        }

        [TearDown]
        public void TearDown()
        {
            if(_scene.IsValid())EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }

        [Test]
        public void StationaryBindingsDoNotRequireArchivedProloguePagesOrRuntimeRoots()
        {
            foreach(var name in new[]
            {
                "_prologueTheme","_frontendShell","_observationCompletionFrontend",
                "_startupRecallPresentation","_atlasHubPresentationPrefab","_featurePages",
                "_videoRuntimeRoot","_panoramaRuntimeRoot","_modelRuntimeRoot",
                "_narrationRuntimeRoot","_effectRuntimeRoot",
                "_physicalAugmentationRuntimeHost","_activationDriver"
            })Field(name).SetValue(_installer,null);

            var bindings=_installer.CreateValidatedBindings();

            Assert.That(bindings.Configuration.PrologueTheme,Is.Null);
            Assert.That(bindings.Presentation.FeaturePages,Is.Null);
            Assert.That(bindings.RuntimeRoots.Video,Is.Null);
            Assert.That(bindings.RuntimeRoots.Fairy,Is.Not.Null);
            Assert.That(bindings.Configuration.MapDefinition,Is.Not.Null,
                "Room point data is still used by the current journey.");
            Assert.That(()=>VisitorRuntimeComposition.Create(bindings,null),
                Throws.InvalidOperationException.With.Message.Contains("Archived composition"),
                "The archived factory must still require an explicit archived opt-in.");
        }

        [Test]
        public void FormalStationarySceneDoesNotInstantiateArchivedUiOrRecognitionModules()
        {
            var archived = new[]
            {
                "GlobalFrontendShell",
                "GazeReticlePresenter",
                "ShellFlowActionTarget",
                "EntryMediaFrontend",
                "ApplicationModeControllerHost",
                "ApplicationModePromptPresenter",
                "ActivationCoordinatorDriver",
                "QrRecognitionSourceAdapter",
                "MetaSpatialDataPermissionGate",
                "PhysicalAugmentationRuntimeHost",
                "MetaPhysicalAnchorLocator",
                "EnvironmentDepthManager",
                "ModelFrontend",
                "KnowledgeMiniGameFrontend",
                "VideoFrontend",
                "PanoramaFrontend",
                "NarrationFrontend"
            };
            var legacyComponents = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null)
                .Where(component => archived.Contains(component.GetType().Name))
                .Select(component => component.GetType().FullName + " on " + HierarchyPath(component.transform) +
                                     " (activeInHierarchy=" + component.gameObject.activeInHierarchy + ")")
                .ToArray();
            Assert.That(legacyComponents, Is.Empty,
                "The formal stationary scene still instantiates archived modules: " +
                string.Join(", ", legacyComponents));

            var bindings = _installer.CreateValidatedBindings();
            Assert.That(bindings.Configuration.PrologueTheme, Is.Null);
            Assert.That(bindings.Presentation.Shell, Is.Null);
            Assert.That(bindings.Presentation.StartupRecall, Is.Not.Null,
                "Keep the startup-only failure surface; it is not bound to the normal journey.");
            Assert.That(bindings.Presentation.GazeReticle, Is.Null);
            Assert.That(bindings.Presentation.FeaturePages, Is.Null);
            Assert.That(bindings.Platform.RecognitionSources, Is.Empty);
            Assert.That(bindings.Platform.SpatialDataPermissionGate, Is.Null);
            Assert.That(bindings.RuntimeRoots.ActivationDriver, Is.Null);
            Assert.That(bindings.RuntimeRoots.Video, Is.Null);
            Assert.That(bindings.RuntimeRoots.Panorama, Is.Null);
            Assert.That(bindings.RuntimeRoots.Model, Is.Null);
            Assert.That(bindings.RuntimeRoots.Narration, Is.Null);
            Assert.That(bindings.RuntimeRoots.Effect, Is.Null);
            Assert.That(bindings.RuntimeRoots.PhysicalAugmentationRuntimeHost, Is.Null);
        }

        [Test]
        public void FormalSceneKeepsBothRealHandPokeInteractorsActive()
        {
            var bindings=_installer.CreateValidatedBindings();
            var interactors=bindings.Platform.InteractionRigRoot.GetComponentsInChildren<PokeInteractor>(true);
            Assert.That(interactors.Length,Is.GreaterThanOrEqualTo(2),
                "Formal interaction rig needs near poke on both hands.");
            Assert.That(interactors.Count(interactor=>interactor.gameObject.activeInHierarchy && interactor.enabled),
                Is.GreaterThanOrEqualTo(2),
                "Formal interaction rig's near poke must remain active; found: "+
                string.Join(", ",interactors.Select(interactor=>
                    interactor.transform.parent?.name+"/"+interactor.name+" active="+
                    interactor.gameObject.activeInHierarchy+" enabled="+interactor.enabled)));
            Assert.That(interactors.Count(interactor=>interactor.gameObject.activeInHierarchy &&
                interactor.enabled && interactor.GetComponent<HandRef>()!=null),Is.GreaterThanOrEqualTo(2),
                "The dialogue must sample the same two tracked hands used by near poke.");
            var trackedSources=interactors.Where(interactor=>interactor.gameObject.activeInHierarchy &&
                    interactor.enabled && interactor.GetComponent<HandRef>()!=null)
                .Select(interactor=>new SerializedObject(interactor.GetComponent<HandRef>())
                    .FindProperty("_hand").objectReferenceValue)
                .ToArray();
            Assert.That(trackedSources.Count(source=>source is IHand),Is.GreaterThanOrEqualTo(2),
                "Both gallery drag inputs need a real serialized tracked-hand source.");
            Assert.That(trackedSources.Where(source=>source is IHand).Distinct().Count(),Is.GreaterThanOrEqualTo(2),
                "Left and right gallery drag inputs must not resolve to the same hand.");
        }

        FieldInfo Field(string name)
            =>typeof(VisitorInstaller).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)
                ??throw new InvalidOperationException("Missing installer field: "+name);

        static string HierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }
    }
}
