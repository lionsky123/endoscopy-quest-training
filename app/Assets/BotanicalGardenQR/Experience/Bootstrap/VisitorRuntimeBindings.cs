using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Activation.Runtime;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Fairy.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.KnowledgeMiniGame.Frontend;
using BotanicalGardenQR.PhysicalAugmentation.Backend;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BotanicalGardenQR.Bootstrap
{
    internal sealed class VisitorRuntimeBindings
    {
        public VisitorRuntimeBindings(
            ConfigurationBindings configuration,
            PlatformBindings platform,
            PresentationBindings presentation,
            RuntimeRootBindings runtimeRoots)
        {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Platform = platform ?? throw new ArgumentNullException(nameof(platform));
            Presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            RuntimeRoots = runtimeRoots ?? throw new ArgumentNullException(nameof(runtimeRoots));
        }

        public ConfigurationBindings Configuration { get; }
        public PlatformBindings Platform { get; }
        public PresentationBindings Presentation { get; }
        public RuntimeRootBindings RuntimeRoots { get; }

        internal sealed class ConfigurationBindings
        {
            public ConfigurationBindings(
                ContentSceneLibrary library,
                ContentEntryCatalog routes,
                RuntimeEnvironmentOptions options,
                FairyApplicationConfiguration fairy,
                GlobalUiDefaults uiDefaults,
                CollectionCatalogAsset collectionCatalog,
                VisitorPrologueThemeAsset prologueTheme,
                VisitorCoachThemeAsset visitorCoachTheme,
                PhysicalAugmentationCatalogAsset physicalAugmentationCatalog,
                TextAsset visitorMapDefinition = null, Material guidanceRouteMaterial = null)
            {
                RuntimeBindingValidator.Required(library, "_sceneLibrary");
                RuntimeBindingValidator.Required(routes, "_contentEntries");
                RuntimeBindingValidator.Required(options, "_runtimeOptions");
                RuntimeBindingValidator.Required(fairy, "_fairyConfiguration");
                RuntimeBindingValidator.Required(uiDefaults, "_globalUiDefaults");
                RuntimeBindingValidator.Required(uiDefaults.SharedFont, "_globalUiDefaults._sharedFont");
                RuntimeBindingValidator.Required(collectionCatalog, "_collectionCatalog");
                RuntimeBindingValidator.Required(prologueTheme, "_prologueTheme");
                RuntimeBindingValidator.Required(visitorCoachTheme, "_visitorCoachTheme");
                RuntimeBindingValidator.Required(physicalAugmentationCatalog, "_physicalAugmentationCatalog");
                if (!prologueTheme.IsValid(out var error)) throw new InvalidOperationException(error);
                if (!visitorCoachTheme.IsValid(out error)) throw new InvalidOperationException(error);
                MapDefinition = VisitorMapConfiguration.Resolve(visitorMapDefinition);
                GuidanceRouteMaterial = guidanceRouteMaterial;
                Library = library;
                Routes = routes;
                Options = options;
                FairyDefinitions = fairy;
                UiDefaults = uiDefaults;
                CollectionCatalog = collectionCatalog;
                PrologueTheme = prologueTheme;
                VisitorCoachTheme = visitorCoachTheme;
                PhysicalAugmentationDefinitions = physicalAugmentationCatalog;
            }

            public BotanicalGardenQR.MapNavigation.Contracts.MapDefinition MapDefinition { get; }
            public Material GuidanceRouteMaterial { get; }
            public ContentSceneLibrary Library { get; }
            public ContentEntryCatalog Routes { get; }
            public RuntimeEnvironmentOptions Options { get; }
            public IFairyDefinitionSource FairyDefinitions { get; }
            public GlobalUiDefaults UiDefaults { get; }
            public CollectionCatalogAsset CollectionCatalog { get; }
            public VisitorPrologueThemeAsset PrologueTheme { get; }
            public VisitorCoachThemeAsset VisitorCoachTheme { get; }
            public IPhysicalAugmentationDefinitionSource PhysicalAugmentationDefinitions { get; }
        }

        internal sealed class PlatformBindings
        {
            public PlatformBindings(
                GameObject xrRigRoot,
                Transform interactionRigRoot,
                GameObject mrukRoot,
                GameObject eventSystemRoot,
                Transform viewer,
                OVRPassthroughLayer fairyArrivalPassthroughLayer,
                Light fairyArrivalEnvironmentLight,
                MonoBehaviour spatialDataPermissionGate,
                MonoBehaviour[] recognitionSourceAdapters,
                RuntimeEnvironmentOptions options)
            {
                RuntimeBindingValidator.Required(xrRigRoot, "_xrRigRoot");
                RuntimeBindingValidator.Required(interactionRigRoot, "_interactionRigRoot");
                RuntimeBindingValidator.Required(mrukRoot, "_mrukRoot");
                RuntimeBindingValidator.Required(eventSystemRoot, "_eventSystemRoot");
                RuntimeBindingValidator.Required(viewer, "_viewer");
                RuntimeBindingValidator.Required(fairyArrivalPassthroughLayer, "_fairyArrivalPassthroughLayer");
                RuntimeBindingValidator.Required(fairyArrivalEnvironmentLight, "_fairyArrivalEnvironmentLight");
                RuntimeBindingValidator.Required(spatialDataPermissionGate, "_spatialDataPermissionGate");
                if (!(spatialDataPermissionGate is ISpatialDataPermissionGate permissionGate))
                    throw new InvalidOperationException(
                        "VisitorInstaller '_spatialDataPermissionGate' must implement ISpatialDataPermissionGate.");
                var eventSystem = eventSystemRoot.GetComponent<EventSystem>();
                if (eventSystem == null)
                    throw new InvalidOperationException("VisitorInstaller event-system root must own an EventSystem component.");
                XrRigRoot = xrRigRoot;
                InteractionRigRoot = interactionRigRoot;
                MrukRoot = mrukRoot;
                EventSystemRoot = eventSystemRoot;
                Viewer = viewer;
                FairyArrivalPassthroughLayer = fairyArrivalPassthroughLayer;
                FairyArrivalEnvironmentLight = fairyArrivalEnvironmentLight;
                SpatialDataPermissionGate = permissionGate;
                EventSystem = eventSystem;
                RecognitionSources = RuntimeBindingValidator.RecognitionSources(
                    recognitionSourceAdapters,
                    options.RecognitionAdapters, options.FieldbookEnabled);
            }

            public GameObject XrRigRoot { get; }
            public Transform InteractionRigRoot { get; }
            public GameObject MrukRoot { get; }
            public GameObject EventSystemRoot { get; }
            public Transform Viewer { get; }
            public Transform FairyGroundReference => XrRigRoot.transform;
            public OVRPassthroughLayer FairyArrivalPassthroughLayer { get; }
            public Light FairyArrivalEnvironmentLight { get; }
            public ISpatialDataPermissionGate SpatialDataPermissionGate { get; }
            public EventSystem EventSystem { get; }
            public IReadOnlyList<IRecognitionSource> RecognitionSources { get; }
        }

        internal sealed class PresentationBindings
        {
            public PresentationBindings(
                GlobalFrontendShell shell,
                KnowledgeMiniGameFrontend observationCompletionFrontend,
                HeadGazeDwellController headGaze,
                StartupRecallPresenter startupRecall,
                GazeReticlePresenter gazeReticle,
                Transform displayRoot,
                GameObject atlasHubPresentationPrefab,
                FeaturePageBindings featurePages)
            {
                RuntimeBindingValidator.Required(shell, "_frontendShell");
                RuntimeBindingValidator.Required(observationCompletionFrontend, "_observationCompletionFrontend");
                RuntimeBindingValidator.Required(headGaze, "_headGazeInteraction");
                RuntimeBindingValidator.Required(startupRecall, "_startupRecallPresentation");
                RuntimeBindingValidator.Required(gazeReticle, "_gazeReticlePresentation");
                RuntimeBindingValidator.Required(displayRoot, "_spatialDisplayRoot");
                RuntimeBindingValidator.Required(atlasHubPresentationPrefab, "_atlasHubPresentationPrefab");
                FeaturePages = featurePages ?? throw new InvalidOperationException("VisitorInstaller requires '_featurePages'.");
                FeaturePages.Validate();
                Shell = shell;
                ObservationCompletionFrontend = observationCompletionFrontend;
                HeadGaze = headGaze;
                StartupRecall = startupRecall;
                GazeReticle = gazeReticle;
                DisplayRoot = displayRoot;
                AtlasHubPresentationPrefab = atlasHubPresentationPrefab;
            }

            public GlobalFrontendShell Shell { get; }
            public KnowledgeMiniGameFrontend ObservationCompletionFrontend { get; }
            public HeadGazeDwellController HeadGaze { get; }
            public StartupRecallPresenter StartupRecall { get; }
            public GazeReticlePresenter GazeReticle { get; }
            public Transform DisplayRoot { get; }
            public GameObject AtlasHubPresentationPrefab { get; }
            public FeaturePageBindings FeaturePages { get; }
        }

        internal sealed class RuntimeRootBindings
        {
            public RuntimeRootBindings(
                Transform video,
                Transform panorama,
                Transform model,
                Transform narration,
                Transform fairy,
                Transform effect,
                PhysicalAugmentationRuntimeHost physicalAugmentationRuntimeHost,
                ActivationCoordinatorDriver activationDriver)
            {
                RuntimeBindingValidator.Required(video, "_videoRuntimeRoot");
                RuntimeBindingValidator.Required(panorama, "_panoramaRuntimeRoot");
                RuntimeBindingValidator.Required(model, "_modelRuntimeRoot");
                RuntimeBindingValidator.Required(narration, "_narrationRuntimeRoot");
                RuntimeBindingValidator.Required(fairy, "_fairyRuntimeRoot");
                RuntimeBindingValidator.Required(effect, "_effectRuntimeRoot");
                RuntimeBindingValidator.Required(physicalAugmentationRuntimeHost, "_physicalAugmentationRuntimeHost");
                RuntimeBindingValidator.Required(activationDriver, "_activationDriver");
                Video = video;
                Panorama = panorama;
                Model = model;
                Narration = narration;
                Fairy = fairy;
                Effect = effect;
                PhysicalAugmentationRuntimeHost = physicalAugmentationRuntimeHost;
                ActivationDriver = activationDriver;
            }

            public Transform Video { get; }
            public Transform Panorama { get; }
            public Transform Model { get; }
            public Transform Narration { get; }
            public Transform Fairy { get; }
            public Transform Effect { get; }
            public PhysicalAugmentationRuntimeHost PhysicalAugmentationRuntimeHost { get; }
            public ActivationCoordinatorDriver ActivationDriver { get; }
        }
    }
}
