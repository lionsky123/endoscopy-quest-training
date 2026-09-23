using System;
using System.Linq;
using BotanicalGardenQR.MapNavigation.Runtime;
using BotanicalGardenQR.MapNavigation.Frontend;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Activation.Runtime;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.Collection.Runtime;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Effect.Backend;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Flow;
using BotanicalGardenQR.Fairy.Backend;
using BotanicalGardenQR.Fairy.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.ImageRing.Backend;
using BotanicalGardenQR.ImageRing.Contracts;
using BotanicalGardenQR.ImageRing.Frontend;
using BotanicalGardenQR.KnowledgeMiniGame.Backend;
using BotanicalGardenQR.KnowledgeMiniGame.Contracts;
using BotanicalGardenQR.KnowledgeMiniGame.Frontend;
using BotanicalGardenQR.JourneyNavigation.Contracts;
using BotanicalGardenQR.JourneyNavigation.Runtime;
using BotanicalGardenQR.Model.Backend;
using BotanicalGardenQR.Model.Contracts;
using BotanicalGardenQR.Narration.Backend;
using BotanicalGardenQR.Narration.Contracts;
using BotanicalGardenQR.Narration.Frontend;
using BotanicalGardenQR.Panorama.Backend;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Backend;
using BotanicalGardenQR.SpatialHost.Contracts;
using BotanicalGardenQR.SpatialHost.Runtime;
using BotanicalGardenQR.Video.Backend;
using BotanicalGardenQR.Video.Contracts;
using BotanicalGardenQR.VisitorAtlasHub.Frontend;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using BotanicalGardenQR.VisitorPrologue.Frontend;
using BotanicalGardenQR.VisitorPrologue.Runtime;
using BotanicalGardenQR.VisitorCoach.Contracts;
using BotanicalGardenQR.VisitorCoach.Frontend;
using BotanicalGardenQR.VisitorCoach.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BotanicalGardenQR.Bootstrap
{
    public sealed partial class VisitorRuntimeComposition : IDisposable
    {
        readonly BootstrapOwnershipScope _ownership;
        readonly SpatialDataPermissionStartupBinding _permissionStartupBinding;
        readonly VisitorPrologueStartupBinding _prologueStartupBinding;
        readonly Action<float> _tick;
        readonly Func<bool> _canStart;
        Action _begin;
        bool _startRequested, _startPending;
        bool _disposed;

        VisitorRuntimeComposition(
            BootstrapOwnershipScope ownership,
            SpatialDataPermissionStartupBinding permissionStartupBinding,
            VisitorPrologueStartupBinding prologueStartupBinding,
            Action<float> tick, Func<bool> canStart)
        {
            _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
            _permissionStartupBinding = permissionStartupBinding;
            _prologueStartupBinding = prologueStartupBinding;
            _tick = tick;
            _canStart = canStart;
        }

        internal static VisitorRuntimeComposition Create(
            VisitorRuntimeBindings bindings,
            Action<DiagnosticEvent> diagnostics, FullScriptRoomVisit visit = null)
        {
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));
            // Production is composed independently. Historical C09 factories below
            // must never be initialized merely to leave them hidden or unticked.
            if (visit?.Stationary == true) return CreateStationary(bindings, diagnostics, visit);
            if (bindings.Configuration.Library == null || bindings.Configuration.Routes == null ||
                bindings.Configuration.CollectionCatalog == null || bindings.Configuration.PhysicalAugmentationDefinitions == null)
                throw new InvalidOperationException("Archived composition requires explicit archived configuration; production does not load it.");
            var configuration = bindings.Configuration;
            var platform = bindings.Platform;
            var presentation = bindings.Presentation;
            var runtimeRoots = bindings.RuntimeRoots;
            var library = configuration.Library;
            var routes = configuration.Routes;
            var options = configuration.Options;
            var mapDefinition = visit?.Map ?? configuration.MapDefinition;
            var fairyDefinitions = configuration.FairyDefinitions;
            var uiDefaults = configuration.UiDefaults;
            var prologueTheme = configuration.PrologueTheme;
            var visitorCoachTheme = configuration.VisitorCoachTheme;
            var physicalAugmentationDefinitions = configuration.PhysicalAugmentationDefinitions;
            var collectionCatalog = configuration.CollectionCatalog;
            var recognitionSources = platform.RecognitionSources;
            var spatialDataPermissionGate = platform.SpatialDataPermissionGate;
            var interactionRigRoot = platform.InteractionRigRoot;
            var viewer = platform.Viewer;
            var fairyGroundReference = platform.FairyGroundReference;
            var fairyArrivalPassthroughLayer = platform.FairyArrivalPassthroughLayer;
            var fairyArrivalEnvironmentLight = platform.FairyArrivalEnvironmentLight;
            var eventSystem = platform.EventSystem;
            var displayRoot = presentation.DisplayRoot;
            var atlasHubPresentationPrefab = presentation.AtlasHubPresentationPrefab;
            var shell = presentation.Shell;
            var headGaze = presentation.HeadGaze;
            var startupRecall = presentation.StartupRecall;
            var observationCompletionFrontend = presentation.ObservationCompletionFrontend;
            var gazeReticle = presentation.GazeReticle;
            var featurePages = presentation.FeaturePages;
            var videoRuntimeRoot = runtimeRoots.Video;
            var panoramaRuntimeRoot = runtimeRoots.Panorama;
            var modelRuntimeRoot = runtimeRoots.Model;
            var narrationRuntimeRoot = runtimeRoots.Narration;
            var fairyRuntimeRoot = runtimeRoots.Fairy;
            var effectRuntimeRoot = runtimeRoots.Effect;
            var physicalAugmentationRuntimeHost = runtimeRoots.PhysicalAugmentationRuntimeHost;
            var activationDriver = runtimeRoots.ActivationDriver;
            var ownership = new BootstrapOwnershipScope();
            try
            {
                if(visit?.Stationary==true)
                {
                    prologueTheme=prologueTheme.CreateStationaryVariant();
                    var ownedTheme=prologueTheme;
                    ownership.Register(()=>
                    {
                        if(UnityEngine.Application.isPlaying)UnityEngine.Object.Destroy(ownedTheme);
                        else UnityEngine.Object.DestroyImmediate(ownedTheme);
                    });
                }
                if(visit!=null)
                {
                    featurePages.PanoramaFrontend.ClinicalProgress=visit.ObservationProgress;
                    featurePages.PanoramaFrontend.ClinicalReadOnly=()=>visit.TeachingReadOnly;
                    ownership.Register(()=>
                    {
                        featurePages.PanoramaFrontend.ClinicalProgress=null;
                        featurePages.PanoramaFrontend.ClinicalReadOnly=null;
                    });
                }
                VirtualRoomEnvironment room = visit?.Room;
                if (options.VirtualRoomEnabled && room == null)
                {
                    room = VirtualRoomEnvironment.Create(platform.XrRigRoot, platform.MrukRoot,
                        configuration.MapDefinition);
                    ownership.Register(room.Dispose);
                    if (room.TrackingOrigin != null)
                    {
                        var trackingGuard = new VirtualRoomTrackingGuard(room.TrackingOrigin,
                            viewer, interactionRigRoot, uiDefaults.SharedFont);
                        ownership.Register(trackingGuard.Dispose);
                    }
                }
                if (spatialDataPermissionGate != null) ownership.Register(spatialDataPermissionGate.Dispose);
                var journeySession = JourneySessionId.CreateNew();
                var visitorCoachSession = new VisitorCoachSessionId(journeySession.Value.ToString("N"));
                var visitorCoach = VisitorCoachModuleFactory.Create(visitorCoachTheme.Timing);
                ownership.Register(() =>
                {
                    visitorCoach.EndSession(visitorCoachSession);
                    visitorCoach.Dispose();
                });
                var beginCoach = visitorCoach.BeginSession(visitorCoachSession);
                if (!beginCoach.Succeeded)
                    throw new InvalidOperationException("Visitor Coach session could not begin.");
                if (!collectionCatalog.TryBuild(out var catalog, out var catalogError))
                    throw new InvalidOperationException(catalogError);
                var collectionProgress = CollectionProgressModuleFactory.Create();
                ownership.Register(collectionProgress.Dispose);
                collectionProgress.BeginSession(journeySession, catalog);
                if (!fairyDefinitions.TryGet(out var fairyDefinition) || fairyDefinition == null)
                    throw new InvalidOperationException(
                        "Visitor Runtime requires one valid application-global Fairy definition.");

                var definitions = new PublishedSceneResolver(library);
                var video = VideoModuleFactory.Create(videoRuntimeRoot, options.Video);
                ownership.Register(() => DisposeCreatedRuntime(video));
                var panorama = PanoramaModuleFactory.Create(panoramaRuntimeRoot, viewer, diagnostics);
                ownership.Register(() => DisposeCreatedRuntime(panorama));
                if (room != null) ownership.Register(panorama.Observe(room).Dispose);
                var model = ModelModuleFactory.Create(modelRuntimeRoot, diagnostics);
                ownership.Register(() => DisposeCreatedRuntime(model));
                var narration = NarrationModuleFactory.Create(narrationRuntimeRoot);
                ownership.Register(() => DisposeCreatedRuntime(narration));
                IImageRingController imageRing = null;
                var imageRingRuntime = ImageRingFrontend.Create(
                    displayRoot,
                    viewer,
                    headGaze,
                    uiDefaults.SharedFont);
                ownership.Register(() =>
                {
                    if (imageRing != null) DisposeCreatedRuntime(imageRing);
                    else DisposeCreatedRuntime(imageRingRuntime);
                });
                imageRing = ImageRingModuleFactory.Create(imageRingRuntime, diagnostics);
                var imageRingBinding = new PanoramaImageRingBinding(definitions, imageRing);
                ownership.Register(imageRingBinding.Dispose);

                var pageLifecycles = featurePages.Create(
                    shell,
                    definitions,
                    video,
                    panorama,
                    model,
                    imageRingBinding,
                    panoramaRuntimeRoot,
                    viewer,
                    headGaze,
                    uiDefaults.SharedFont);
                var flow = ExperienceFlow.CreateOnCurrentThread(definitions, new FeaturePageRegistry(pageLifecycles));
                var host = SpatialHostModuleFactory.Create(viewer, displayRoot);
                var focusQueryProvider = new ViewerGazeFocusQueryProvider(
                    () => viewer != null
                        ? new SpatialEvidence(viewer.position, viewer.rotation, true)
                        : (SpatialEvidence?)null,
                    options.ScanFocusPaddingDegrees);
                var activation = ActivationModuleFactory.CreateOnCurrentThread(
                    routes,
                    host,
                    flow,
                    recognitionSources,
                    options,
                    focusQueryProvider,
                    journeySession,
                    diagnostics);
                ownership.Register(activation.Dispose);

                ownership.Register(shell.Dispose);
                if(visit != null)
                {
                    shell.ResumeClinicalCourse = visit.ResumeWashingLesson;
                    shell.ClinicalCourseReadOnly = () => visit.TeachingReadOnly;
                }
                ownership.Register(gazeReticle.Unconfigure);
                ownership.Register(headGaze.Unconfigure);
                ownership.Register(startupRecall.Unconfigure);
                FrontendShellModuleFactory.Configure(
                    shell,
                    headGaze,
                    startupRecall,
                    gazeReticle,
                    viewer,
                    eventSystem,
                    flow,
                    activation,
                    activation,
                    DefaultPresentation(uiDefaults),
                    uiDefaults.StartupHint);
                headGaze.SetHandOnly(true);
                var journey = JourneyNavigationModuleFactory.Create();
                ownership.Register(journey.Dispose);
                var journeyCloseDecisionBinding = new JourneyCloseDecisionBinding(
                    activation,
                    journeySession,
                    journey,
                    startupRecall,
                    startupRecall,
                    collectionProgress,
                    collectionCatalog,
                    collectionsEnabled: false);
                ownership.Register(journeyCloseDecisionBinding.Dispose);
                var visitorProgressSummaryBinding = new VisitorProgressSummaryBinding(
                    journey,
                    collectionProgress,
                    routes.Routes.Where(r => r != null && r.Enabled).Select(r => r.TargetSceneId).Distinct().Count(),
                    startupRecall);
                ownership.Register(visitorProgressSummaryBinding.Dispose);
                var collectionPresentationObject = InstantiateConfiguredPresentation(
                    collectionCatalog.PresentationTheme.WorldPrefab,
                    displayRoot != null ? displayRoot.parent : null,
                    "CollectionWorldPresentation");
                ownership.Register(() => Destroy(collectionPresentationObject));
                var collectionWorld = RequiredComponent<CollectionWorldFrontend>(
                    collectionPresentationObject,
                    "Collection World prefab");
                ownership.Register(collectionWorld.Dispose);
                collectionWorld.Configure(viewer, headGaze, collectionCatalog);
                // Retain the hidden modal surface contract; this course has no collection UI.
                collectionPresentationObject.SetActive(false);
                var prologuePresentationObject = InstantiateConfiguredPresentation(
                    prologueTheme.PresentationPrefab,
                    displayRoot != null ? displayRoot.parent : null,
                    "VisitorProloguePresentation");
                ownership.Register(() => Destroy(prologuePresentationObject));
                var prologuePresenter = RequiredComponent<VisitorProloguePresenter>(
                    prologuePresentationObject,
                    "Visitor Prologue prefab");
                var handReadiness = RequiredComponent<VisitorHandReadinessAdapter>(
                    prologuePresentationObject,
                    "Visitor Prologue prefab");
                var prologue = visit?.Prologue ?? VisitorPrologueModuleFactory.Create();
                if (visit == null) ownership.Register(prologue.Dispose);
                ownership.Register(prologuePresenter.Dispose);
                prologuePresenter.Configure(viewer, headGaze, prologueTheme);
                prologuePresenter.Bind(prologue);
                ownership.Register(handReadiness.Unconfigure);
                handReadiness.Configure(interactionRigRoot, prologue, prologueTheme.GazeFallbackDelaySeconds,
                    viewer, uiDefaults.SharedFont);
                prologuePresenter.BindHands(handReadiness);

                var fairyBinding = TryBindOptional("Fairy", ownership, optionalOwnership =>
                {
                    optionalOwnership.Register(
                        () => DestroyCreatedRuntime(fairyRuntimeRoot, "FairyModuleRuntime"));
                    var fairy = FairyModuleFactory.Create(
                        fairyRuntimeRoot,
                        viewer,
                        fairyGroundReference,
                        fairyArrivalPassthroughLayer,
                        fairyArrivalEnvironmentLight,
                        diagnostics,
                        handReadiness.GetArrivalHandPosition, room?.GuidePath);
                    var binding = FairyModuleFactory.BindAsCompanion(
                        fairy,
                        fairyDefinition,
                        initiallyVisible: false);
                    optionalOwnership.Register(binding.Dispose);
                    return binding;
                });
                if (fairyBinding != null)
                {
                    var panoramaFairyVisibilityBinding = new PanoramaFairyVisibilityBinding(flow, fairyBinding);
                    ownership.Register(panoramaFairyVisibilityBinding.Dispose);
                }

                var knowledgeMiniGame = KnowledgeMiniGameModuleFactory.Create();
                ownership.Register(knowledgeMiniGame.Dispose);
                // The authored frontend survives room visits; only its bindings are visit-owned.
                ownership.Register(visit == null ? observationCompletionFrontend.Dispose : observationCompletionFrontend.Unconfigure);
                observationCompletionFrontend.Configure(viewer, headGaze);
                var knowledgeMiniGameBinding = new KnowledgeMiniGameCompletionBinding(
                    activation,
                    startupRecall,
                    definitions,
                    knowledgeMiniGame,
                    observationCompletionFrontend,
                    journeyCloseDecisionBinding.AcceptObservationCompleted, ownsFrontend: visit == null);
                ownership.Register(knowledgeMiniGameBinding.Dispose);
                shell.ClinicalCompletionRequested += journeyCloseDecisionBinding.AcceptClinicalLessonCompleted;
                ownership.Register(() => shell.ClinicalCompletionRequested -= journeyCloseDecisionBinding.AcceptClinicalLessonCompleted);
                shell.ClinicalReviewClosed += journeyCloseDecisionBinding.AcceptClinicalReviewClosed;
                ownership.Register(()=>shell.ClinicalReviewClosed -= journeyCloseDecisionBinding.AcceptClinicalReviewClosed);
                if(visit!=null)
                {
                    shell.ClinicalReviewClosed += visit.AcceptTeachingReviewClosed;
                    ownership.Register(()=>shell.ClinicalReviewClosed -= visit.AcceptTeachingReviewClosed);
                }
                var narrationDockBinding = new NarrationDockBinding(
                    flow,
                    shell,
                    definitions,
                    narration,
                    featurePages.NarrationFrontend,
                    suppressed =>
                    {
                        if (fairyBinding != null)
                            fairyBinding.SetAmbientAudioSuppressed(suppressed);
                    });
                ownership.Register(narrationDockBinding.Dispose);
                flow.SetMainSurfaceLifecycle(narrationDockBinding);
                var presentationBinding = new ScenePresentationBinding(flow, definitions, shell, headGaze);
                ownership.Register(presentationBinding.Dispose);
                TryBindOptional("Effect", ownership, optionalOwnership =>
                {
                    optionalOwnership.Register(
                        () => DestroyCreatedRuntime(effectRuntimeRoot, "EffectModuleRuntime"));
                    var effect = EffectModuleFactory.Create(effectRuntimeRoot, diagnostics);
                    var binding = EffectModuleFactory.BindToFlow(flow, definitions, effect);
                    optionalOwnership.Register(binding.Dispose);
                    return binding;
                });

                var visitorCoachPresentationObject = InstantiateConfiguredPresentation(
                    visitorCoachTheme.PresentationPrefab,
                    displayRoot != null ? displayRoot.parent : null,
                    "VisitorCoachPresentation");
                ownership.Register(() => Destroy(visitorCoachPresentationObject));
                var visitorCoachPresenter = RequiredComponent<VisitorCoachPresenter>(
                    visitorCoachPresentationObject,
                    "Visitor Coach prefab");
                visitorCoachPresenter.Configure(
                    viewer,
                    visitorCoachTheme,
                    uiDefaults.SharedFont,
                    headGaze);

                var toolPreparation = new VisitorToolPreparation();
                ownership.Register(toolPreparation.Dispose);
                var modalEnvironment = new VisitorModalPresentationBinding(
                    shell,
                    startupRecall,
                    prologuePresenter,
                    visitorCoachPresenter,
                    observationCompletionFrontend,
                    collectionWorld,
                    activation,
                    flow,
                    prologue,
                    toolPreparation);
                ownership.Register(modalEnvironment.Dispose);
                var modalBinding = new VisitorModalCoordinator(modalEnvironment);
                ownership.Register(modalBinding.Dispose);
                // Read the existing route label font without creating palm tools or a map display.
                var mapLabelFont = RequiredComponent<VisitorAtlasHubPresentation>(
                    atlasHubPresentationPrefab, "Route font source").MapLabelFont;
                var mapNavigation = MapNavigationModuleFactory.Create(mapDefinition,
                    new FairyMapMotionSink(fairyBinding, mapDefinition), room?.Frame);
                ownership.Register(mapNavigation.Dispose);
                var mapRoute = configuration.GuidanceRouteMaterial != null
                    ? MapRoutePresentationFactory.Create(displayRoot.parent, configuration.GuidanceRouteMaterial, mapLabelFont, viewer)
                    : null;
                if (mapRoute != null) ownership.Register(mapRoute.Dispose);
                if (!options.VirtualRoomEnabled) TryBindOptional("PhysicalAugmentation", ownership, optionalOwnership =>
                {
                    var binding = new PhysicalAugmentationVisitorBinding(
                        flow,
                        definitions,
                        physicalAugmentationDefinitions,
                        physicalAugmentationRuntimeHost,
                        () => modalBinding.CoachCuesSuppressed,
                        spatialDataPermissionGate);
                    optionalOwnership.Register(binding.Dispose);
                    var actionLease = shell.BindFeaturePageAction(FeaturePageId.Model, binding);
                    optionalOwnership.Register(actionLease.Dispose);
                    return binding;
                });
                var visitorCoachRuntime = new VisitorCoachRuntimeBinding(
                    visitorCoach,
                    visitorCoachSession,
                    visitorCoachPresenter,
                    visitorCoachTheme,
                    () => modalBinding.CoachCuesSuppressed);
                ownership.Register(visitorCoachRuntime.Dispose);
                var visitorCoachPanorama = new VisitorCoachPanoramaBinding(
                    visitorCoach,
                    visitorCoachSession,
                    visitorCoachTheme,
                    featurePages.PanoramaFrontend);
                ownership.Register(visitorCoachPanorama.Dispose);
                var visitorCoachPrologue = new VisitorCoachPrologueBinding(
                    visitorCoach,
                    visitorCoachSession,
                    prologuePresenter);
                ownership.Register(visitorCoachPrologue.Dispose);
                var visitorCoachQr = new VisitorCoachQrBinding(
                    visitorCoach,
                    visitorCoachSession,
                    journeySession,
                    activation,
                    visitorCoachTheme);
                ownership.Register(visitorCoachQr.Dispose);
                var spatialDataPermissionBinding = options.VirtualRoomEnabled ? null : new SpatialDataPermissionStartupBinding(
                    spatialDataPermissionGate,
                    startupRecall,
                    () =>
                    {
                        try
                        {
                            var firstScan = visitorCoachQr.BeginFirstScanOpportunity();
                            if (!firstScan.Succeeded)
                                Debug.LogWarning(
                                    $"首次二维码教学机会未能创建，Recognition 仍将启动：{firstScan.FailureCode}。");
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                            Debug.LogWarning("首次二维码教学初始化异常，Recognition 仍将启动。");
                        }
                        activation.Start();
                    });
                if (spatialDataPermissionBinding != null) ownership.Register(spatialDataPermissionBinding.Dispose);
                if (fairyBinding != null)
                {
                    var visitorDialogueFairy = new VisitorDialogueFairyBinding(
                        visitorCoachPresenter,
                        fairyBinding);
                    ownership.Register(visitorDialogueFairy.Dispose);
                    var visitorCoachFairy = new VisitorCoachFairyBinding(
                        visitorCoach,
                        visitorCoachSession,
                        fairyBinding,
                        visitorCoachTheme.FairyAttentionSeconds);
                    ownership.Register(visitorCoachFairy.Dispose);
                }
                VisitorGuidanceCoordinator guidance = null;
                bool OpenPoint(string pointId)
                {
                    if (visit != null && visit.TryOpen(pointId))
                    {
                        guidance.TargetContentOpened();
                        return true;
                    }
                    return routes.TryResolveMapPoint(pointId, out var entry) && activation.TryOpenConfirmedEntry(entry.EntryValue);
                }
                guidance = new VisitorGuidanceCoordinator(mapNavigation,
                    () => { if (!options.VirtualRoomEnabled && !options.FieldbookEnabled) spatialDataPermissionBinding.RequestRecognitionStart(); },
                    options.VirtualRoomEnabled || options.FieldbookEnabled ? OpenPoint : (Func<string, bool>)null,
                    options.ArrivalRadius, options.ArrivalExitRadius, options.ArrivalStableSeconds);
                ownership.Register(guidance.Dispose);
                var guidanceBinding = new VisitorMapGuidanceBinding(mapNavigation,guidance,viewer,fairyGroundReference,journey,
                    collectionProgress,collectionWorld,visitorCoachPresenter,modalEnvironment,mapRoute,activation,
                    () => visit?.CompletionRevision ?? 0, () => visit?.ContentOpen == true);
                ownership.Register(guidanceBinding.Dispose);
                void BeginGuidedLearning()
                {
                    toolPreparation.CompleteWithoutTools();
                    if(visit?.Stationary == true) visit.BeginStationary();
                    else guidance.Begin();
                }
                var prologueStartupBinding = new VisitorPrologueStartupBinding(
                    prologue,
                    prologuePresenter,
                    visitorCoachPresenter,
                    fairyBinding,
                    prologueTheme,
                    BeginGuidedLearning,
                    gazeReticle.SetPresentationEnabled);
                ownership.Register(prologueStartupBinding.Dispose);
                ownership.Register(() => activationDriver.Unconfigure(activation));
                activationDriver.Configure(activation);

                var composition = new VisitorRuntimeComposition(
                    ownership,
                    spatialDataPermissionBinding,
                    prologueStartupBinding,
                    deltaSeconds =>
                    {
                        var alignmentValid = (room?.TrackingOrigin == null || room.TrackingOrigin.CanInteract) && (visit == null || visit.InputAllowed);
                        // Still tick with tracked=false so navigation sends Hold to
                        // the Fairy instead of leaving its last movement running.
                        if(visit?.Stationary != true) guidanceBinding.Tick(deltaSeconds, alignmentValid);
                        else visit.TickStationary(deltaSeconds);
                        if (!alignmentValid || visit?.ContentOpen == true) return;
                        visitorCoachRuntime.Tick(deltaSeconds);
                    }, () => (room?.TrackingOrigin == null || room.TrackingOrigin.CanInteract) && (visit == null || visit.InputAllowed));
                composition._begin = () =>
                {
                    if (visit != null && prologue.CurrentState.IsExplorationReady)
                    {
                        fairyBinding?.Show(visit.Stationary ? viewer.position+Vector3.ProjectOnPlane(viewer.forward,Vector3.up).normalized*.85f-Vector3.up*.4f : room.GuidePath.StartPosition);
                        BeginGuidedLearning();
                    }
                    else prologueStartupBinding.Begin();
                };
                return composition;
            }
            catch
            {
                ownership.Dispose();
                throw;
            }
        }

        public void StartExperience()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(VisitorRuntimeComposition));
            if (_startRequested) return;
            _startRequested = _startPending = true;
            _permissionStartupBinding?.BeginPermissionRequest();
            TryStartExperience();
        }

        void TryStartExperience()
        {
            if (!_startPending || !_canStart()) return;
            _startPending = false;
            _begin();
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (_disposed) return;
            TryStartExperience();
            _permissionStartupBinding?.Tick(unscaledDeltaSeconds);
            _tick?.Invoke(unscaledDeltaSeconds);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _ownership.Dispose();
        }

        static PresentationSpec DefaultPresentation(GlobalUiDefaults defaults)
            => new PresentationSpec(defaults.TitleStyle, defaults.SubtitleStyle, defaults.Layout, defaults.Animation);

        static T TryBindOptional<T>(
            string moduleName,
            BootstrapOwnershipScope ownership,
            Func<BootstrapOwnershipScope, T> bind)
            where T : class, IDisposable
        {
            var optionalOwnership = new BootstrapOwnershipScope();
            try
            {
                var result = bind(optionalOwnership);
                ownership.Adopt(optionalOwnership);
                return result;
            }
            catch (Exception exception)
            {
                optionalOwnership.Dispose();
                Debug.LogError(
                    $"Optional visitor module '{moduleName}' failed to initialize and was isolated from QR startup.\n{exception}");
                return null;
            }
        }

        static GameObject InstantiateConfiguredPresentation(GameObject prefab, Transform parent, string instanceName)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = instanceName;
            return instance;
        }

        static T RequiredComponent<T>(GameObject instance, string ownerLabel) where T : Component
        {
            var component = instance != null ? instance.GetComponentInChildren<T>(true) : null;
            if (component == null)
                throw new InvalidOperationException($"{ownerLabel} requires one {typeof(T).Name} component.");
            return component;
        }

        static void DisposeCreatedRuntime(object runtime)
        {
            if (runtime is Component component)
            {
                Destroy(component);
                return;
            }
            (runtime as IDisposable)?.Dispose();
        }

        static void DestroyCreatedRuntime(Transform runtimeRoot, string childName)
        {
            if (runtimeRoot == null || string.IsNullOrEmpty(childName)) return;
            var child = runtimeRoot.Find(childName);
            if (child != null) Destroy(child.gameObject);
        }

        static void Destroy(UnityEngine.Object runtimeObject)
        {
            if (runtimeObject == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(runtimeObject);
            else UnityEngine.Object.DestroyImmediate(runtimeObject);
        }
    }
}
