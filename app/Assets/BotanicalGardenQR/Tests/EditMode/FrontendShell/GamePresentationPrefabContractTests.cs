using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Collection.Frontend;
using BotanicalGardenQR.Collection.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.KnowledgeMiniGame.Frontend;
using BotanicalGardenQR.VisitorPrologue.Frontend;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.Grab.GrabSurfaces;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.FrontendShell
{
    public sealed class GamePresentationPrefabContractTests
    {
        [Test]
        public void ProductionCollectionConfigUsesGenericOneHalfAndCompleteMilestones()
        {
            var catalogAsset = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            Assert.That(catalogAsset, Is.Not.Null);
            Assert.That(catalogAsset.TryBuild(out var catalog, out var error), Is.True, error);
            Assert.That(catalog.TotalCount, Is.EqualTo(6));
            Assert.That(catalog.Milestones.Count, Is.EqualTo(3));
            Assert.That(catalog.Milestones[0].Kind, Is.EqualTo(CollectionMilestoneKind.FirstCollection));
            Assert.That(catalog.Milestones[1].Kind, Is.EqualTo(CollectionMilestoneKind.CollectedCountReached));
            Assert.That(catalog.Milestones[1].Threshold, Is.EqualTo(3));
            Assert.That(catalog.Milestones[2].Kind, Is.EqualTo(CollectionMilestoneKind.CollectionCompleted));
        }

        [Test]
        public void CollectionRewardCopyUsesCountsAndCompletionInsteadOfMilestoneIds()
        {
            var first = ProgressState(1, 6);
            var middle = ProgressState(3, 6);
            var complete = ProgressState(6, 6);
            var pending = new CollectionPendingPresentation(
                new CollectionPresentationId(1),
                CollectionPresentationKind.Reward,
                "artifact:test",
                true,
                new[] { "opaque_config_id" });

            Assert.That(CollectionWorldFrontend.RewardTitle(first, pending, true),
                Is.EqualTo("第一张见闻页已入册"));
            Assert.That(CollectionWorldFrontend.RewardTitle(middle, pending, true),
                Is.EqualTo("见闻册又丰富了一些"));
            Assert.That(CollectionWorldFrontend.RewardTitle(complete, pending, true),
                Is.EqualTo("这趟见闻已完整收录"));
            Assert.That(CollectionWorldFrontend.RewardTitle(middle, pending, false),
                Is.EqualTo("见闻页已入册"));
            Assert.That(CollectionWorldFrontend.RewardSummary(complete), Does.Contain("6 张见闻页"));
            Assert.That(CollectionWorldFrontend.RewardSummary(middle), Does.Not.Contain("这一路"));
        }

        const string VisitorRuntimePath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab";
        const string CollectionWorldPath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/CollectionWorldPresentation.prefab";
        const string VisitorProloguePath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/VisitorProloguePresentation.prefab";
        const string CollectionArtifactPath =
            "Assets/BotanicalGardenQR/Content/Shared/Fieldbook/FieldbookPage.prefab";
        const string ObservationCompletionPath =
            "Assets/BotanicalGardenQR/Experience/Bootstrap/Art/GamePresentation/Prefabs/ObservationCompletionSurface.prefab";
        const string VisitorScenePath =
            "Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity";

        [Test]
        public void ProductionVisitorHasOneListenerAndAudiblePresentationCueChains()
        {
            var scene = SceneManager.GetSceneByPath(VisitorScenePath);
            var openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(VisitorScenePath, OpenSceneMode.Additive);

            try
            {
                var listeners = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<AudioListener>(true))
                    .Where(listener => listener != null && listener.enabled && listener.gameObject.activeInHierarchy)
                    .ToArray();
                Assert.That(listeners, Has.Length.EqualTo(1),
                    "VisitorRuntime requires exactly one active AudioListener.");

                AssertAudiblePresentationSource(VisitorProloguePath, "Visitor Prologue");
                AssertAudiblePresentationSource(CollectionWorldPath, "Collection World");

                var prologueTheme = AssetDatabase.LoadAssetAtPath<VisitorPrologueThemeAsset>(
                    "Assets/BotanicalGardenQR/Content/Authoring/VisitorPrologueTheme.asset");
                Assert.That(prologueTheme, Is.Not.Null);
                Assert.That(prologueTheme.IsValid(out var prologueError), Is.True, prologueError);
                var prologueCues = new[]
                {
                    prologueTheme.InvitationAudio
                };
                Assert.That(prologueCues, Has.All.Not.Null);
                foreach (var cue in prologueCues)
                    Assert.That(cue.length, Is.GreaterThan(0.1f),
                        "Every production Prologue cue must contain audible sample data.");

                var collection = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(
                    "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
                Assert.That(collection, Is.Not.Null);
                Assert.That(collection.Artifacts, Is.Not.Empty);
                foreach (var artifact in collection.Artifacts)
                {
                    Assert.That(artifact, Is.Not.Null);
                    Assert.That(artifact.DropAudio, Is.Not.Null, $"{artifact.ArtifactId} has no drop cue.");
                    Assert.That(artifact.CollectAudio, Is.Not.Null, $"{artifact.ArtifactId} has no collect cue.");
                    Assert.That(artifact.ReturnAudio, Is.Not.Null, $"{artifact.ArtifactId} has no return cue.");
                    Assert.That(artifact.DropAudio.length, Is.GreaterThan(0.1f), artifact.ArtifactId);
                    Assert.That(artifact.CollectAudio.length, Is.GreaterThan(0.1f), artifact.ArtifactId);
                    Assert.That(artifact.ReturnAudio.length, Is.GreaterThan(0.1f), artifact.ArtifactId);
                }
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TutorialGazeTargetUsesTheSharedButtonProgressContract()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorProloguePath);
            Assert.That(prefab, Is.Not.Null, VisitorProloguePath);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var button = instance.GetComponentsInChildren<Button>(true)
                    .Single(value => value.name == "Button_GuideGazeFallback");
                var track = button.transform.Find("GazeProgress")?.GetComponent<Image>();
                var fill = button.transform.Find("GazeProgress/Fill")?.GetComponent<Image>();
                var aura = button.transform.Find("FocusAura")?.GetComponent<Image>();
                var attention = button.GetComponent<AttentionGazeTargetVisual>();
                Assert.That(track, Is.Not.Null);
                Assert.That(fill, Is.Not.Null);
                Assert.That(aura, Is.Not.Null);
                Assert.That(attention, Is.Not.Null,
                    "The taught target needs an idle attention beacon before gaze begins.");

                attention.PresentGazeProgress(0f);

                Assert.That(fill.fillAmount, Is.Zero);
                Assert.That(track.color.a, Is.GreaterThan(0.05f));
                Assert.That(aura.color.a, Is.GreaterThan(0.05f),
                    "The target must remain discoverable while no dwell is in progress.");

                EntryUIButtonVisual.ApplyGazeFocus(button, 0.64f);

                Assert.That(fill.fillAmount, Is.EqualTo(0.64f).Within(0.001f));
                Assert.That(fill.color.a, Is.GreaterThan(0.8f));
                Assert.That(track.color.a, Is.GreaterThan(0.1f));
                Assert.That(aura.color.a, Is.GreaterThan(0.1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ProductionVisitorRuntimeContainsNoLegacyBadgeRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorRuntimePath);
            Assert.That(prefab, Is.Not.Null);

            var retired = prefab.GetComponentsInChildren<Transform>(true)
                .Where(value => value != null &&
                                value.name.IndexOf("Badge", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(value => value.name)
                .ToArray();
            Assert.That(retired, Is.Empty, "Legacy Badge presentation must not remain in VisitorRuntime.");
        }

        [Test]
        public void CompendiumAndItsEntryActionBelongOnlyToBrowseMode()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionWorldPath);
            Assert.That(prefab, Is.Not.Null);
            var presenter = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(value => value != null && value.GetType().Name == "CollectionWorldFrontend");
            var serialized = new SerializedObject(presenter);
            var rewardRoot = RequiredObject<GameObject>(serialized, "_rewardRoot");
            var browseRoot = RequiredObject<GameObject>(serialized, "_browseRoot");
            var compendiumRoot = RequiredObject<GameObject>(serialized, "_compendiumRoot");
            var toggle = RequiredObject<Button>(serialized, "_toggleCompendiumButton");

            Assert.That(compendiumRoot.transform.IsChildOf(browseRoot.transform), Is.True);
            Assert.That(compendiumRoot.transform.IsChildOf(rewardRoot.transform), Is.False);
            Assert.That(toggle.transform.IsChildOf(browseRoot.transform), Is.True);
            Assert.That(toggle.transform.IsChildOf(rewardRoot.transform), Is.False);

        }

        [Test]
        public void CollectionArtifactIsPoseFreeAndAcceptsPalmAndPinchGrabs()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionArtifactPath);
            Assert.That(prefab, Is.Not.Null, CollectionArtifactPath);

            var grabbable = prefab.GetComponent<Grabbable>();
            var rigidbody = prefab.GetComponent<Rigidbody>();
            var collider = prefab.GetComponentInChildren<BoxCollider>(true);
            var relay = prefab.GetComponent<CollectionArtifactGrabRelay>();
            var interactables = prefab.GetComponentsInChildren<HandGrabInteractable>(true);
            var relayData = new SerializedObject(relay);

            Assert.That(grabbable, Is.Not.Null);
            Assert.That(rigidbody, Is.Not.Null);
            Assert.That(collider, Is.Not.Null);
            Assert.That(relay, Is.Not.Null);
            Assert.That(collider.isTrigger, Is.False,
                "The hand-sized Artifact needs the same physical collider closure as FirstHand Ball.");
            Assert.That(collider.size, Is.EqualTo(new Vector3(.035f, .11f, .035f)));
            Assert.That(collider.GetComponentInParent<Rigidbody>(true), Is.EqualTo(rigidbody),
                "Relay ownership is an authored hierarchy invariant and must be available during Awake.");
            Assert.That(interactables, Has.Length.EqualTo(1),
                "The pre-optimization chain keeps one HandGrabInteractable on the Artifact root.");
            Assert.That(interactables[0].gameObject, Is.EqualTo(prefab));
            Assert.That(interactables[0].Rigidbody, Is.EqualTo(rigidbody));
            Assert.That(interactables[0].SupportedGrabTypes, Is.EqualTo(GrabTypeFlags.All),
                "The artifact accepts natural Palm and Pinch grabs for either reliable hand.");
            Assert.That(interactables[0].HandGrabPoses, Has.Count.EqualTo(0),
                "Pose-free grabbing scores against the physics colliders instead of an authored hand pose.");
            var visualRoot = prefab.transform.Find("ArtifactVisualRoot");
            Assert.That(visualRoot, Is.Not.Null);
            Assert.That(visualRoot.localScale, Is.EqualTo(Vector3.one),
                "The page visual keeps authored hand-sized dimensions.");
            Assert.That(relayData.FindProperty("_visualRoot").objectReferenceValue, Is.EqualTo(visualRoot),
                "Semantic feedback must scale only the authored visual child, never the grab collider root.");
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .Any(material => material.HasProperty("_BaseColor") ||
                                     material.HasProperty("_Color")),
                Is.True,
                "Artifact feedback needs a visible base-color fallback when a shader emission keyword is disabled.");

            var interactableData = new SerializedObject(interactables[0]);
            Assert.That(interactableData.FindProperty("_scoringModifier")
                .FindPropertyRelative("_positionRotationWeight").floatValue,
                Is.EqualTo(1f).Within(0.0001f));
            var filters = interactableData.FindProperty("_interactorFilters");
            Assert.That(filters.arraySize, Is.EqualTo(1));
            Assert.That(filters.GetArrayElementAtIndex(0).objectReferenceValue, Is.EqualTo(relay),
                "Availability is filtered without disabling and re-registering SDK components.");
        }

        [Test]
        public void CollectionArtifactRelayAcceptsBothAuthoredHandGrabInteractorsWhenArmed()
        {
            var scene = SceneManager.GetSceneByPath(VisitorScenePath);
            var openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(VisitorScenePath, OpenSceneMode.Additive);

            var artifactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionArtifactPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            var artifact = UnityEngine.Object.Instantiate(artifactPrefab);
            try
            {
                Assert.That(catalog, Is.Not.Null);
                var installer = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<VisitorInstaller>(true))
                    .FirstOrDefault(candidate => candidate != null);
                Assert.That(installer, Is.Not.Null);
                var interactionRigRoot = new SerializedObject(installer)
                    .FindProperty("_interactionRigRoot").objectReferenceValue as Transform;
                Assert.That(interactionRigRoot, Is.Not.Null);

                var candidates = interactionRigRoot.GetComponentsInChildren<HandGrabInteractor>(true)
                    .Where(interactor => interactor != null)
                    .ToArray();
                var candidatePaths = candidates.Select(interactor => HierarchyPath(interactor.transform)).ToArray();
                Assert.That(candidatePaths.Any(path =>
                    path.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0), Is.True);
                Assert.That(candidatePaths.Any(path =>
                    path.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0), Is.True);

                var relay = artifact.GetComponent<CollectionArtifactGrabRelay>();
                relay.Configure(
                    "artifact_dual_hand",
                    new CollectionInstanceToken("dual-hand-instance"),
                    catalog.PresentationTheme.ArtifactMotion,
                    Color.green,
                    (_, __) => { },
                    () => { }, new CapturingGazeSurfaceRegistry());
                relay.SetInteractionEnabled(true);

                foreach (var candidate in candidates)
                    Assert.That(relay.Filter(candidate.gameObject), Is.True,
                        $"Armed Artifact rejected the authored HandGrabInteractor at {HierarchyPath(candidate.transform)}.");

                relay.SetInteractionEnabled(false);
                foreach (var candidate in candidates)
                    Assert.That(relay.Filter(candidate.gameObject), Is.False,
                        "An unarmed Artifact must reject every hand candidate.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(artifact);
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void CollectionArtifactRelayKeepsTheGrabActiveUntilOneRelease()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionArtifactPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            Assert.That(prefab, Is.Not.Null, CollectionArtifactPath);
            Assert.That(catalog, Is.Not.Null);
            var instance = UnityEngine.Object.Instantiate(prefab);

            try
            {
                var relay = instance.GetComponent<CollectionArtifactGrabRelay>();
                var grabbable = instance.GetComponent<Grabbable>();
                var collider = instance.GetComponentInChildren<BoxCollider>(true);
                var interactable = instance.GetComponent<HandGrabInteractable>();
                var kinematicLocker = instance.GetComponent<RigidbodyKinematicLocker>();
                Assert.That(relay, Is.Not.Null);
                Assert.That(grabbable, Is.Not.Null);
                Assert.That(interactable, Is.Not.Null);
                Assert.That(kinematicLocker, Is.Not.Null);
                if (grabbable.Points == null) InvokeSdkLifecycle(grabbable, "Awake");
                InvokeSdkLifecycle(kinematicLocker, "Awake");

                var grabStarted = 0;
                var grabReleased = 0;
                relay.Configure(
                    "artifact_test",
                    new BotanicalGardenQR.Collection.Contracts.CollectionInstanceToken("test-instance"),
                    catalog.PresentationTheme.ArtifactMotion,
                    Color.yellow,
                    (_, __) => grabStarted++,
                    () => grabReleased++, new CapturingGazeSurfaceRegistry());
                relay.SetInteractionEnabled(false);

                Assert.That(grabbable.enabled, Is.True,
                    "The SDK pointable must stay registered while the scripted drop is unavailable.");
                Assert.That(interactable.enabled, Is.False,
                    "Configure and interaction gating must be safe even when an EditMode-created instance has not received Awake.");
                Assert.That(collider.enabled, Is.True,
                    "The physical grab volume stays active for the whole lifecycle; only the interactable registration is gated.");
                Assert.That(instance.GetComponent<Rigidbody>().detectCollisions, Is.True);
                Assert.That(relay.Filter(null), Is.False);

                relay.SetInteractionEnabled(true);

                Assert.That(grabbable.enabled, Is.True);
                Assert.That(interactable.enabled, Is.True);
                Assert.That(collider.enabled, Is.True);
                Assert.That(relay.FeedbackState, Is.EqualTo(CollectionArtifactFeedbackState.Ready));
                Assert.That(relay.Filter(null), Is.False,
                    "An armed Artifact still rejects an unresolvable interactor GameObject.");
                var pose = new Pose(instance.transform.position, instance.transform.rotation);
                var grabRootScale = instance.transform.localScale;
                grabbable.ProcessPointerEvent(new PointerEvent(733, PointerEventType.Hover, pose));
                Assert.That(relay.FeedbackState, Is.EqualTo(CollectionArtifactFeedbackState.Hovered));
                grabbable.ProcessPointerEvent(new PointerEvent(733, PointerEventType.Select, pose));

                Assert.That(grabStarted, Is.EqualTo(1));
                Assert.That(grabReleased, Is.Zero,
                    "Select starts a real held interaction; it is not the collection boundary.");
                Assert.That(relay.IsArmed, Is.True);
                Assert.That(grabbable.enabled, Is.True);
                Assert.That(interactable.enabled, Is.True,
                    "The SDK Grabbable must remain selected so the Artifact can follow the hand.");
                Assert.That(collider.enabled, Is.True);
                Assert.That(instance.GetComponent<Rigidbody>().detectCollisions, Is.True);
                Assert.That(relay.FeedbackState, Is.EqualTo(CollectionArtifactFeedbackState.Held));

                relay.PresentPlacementProximity(false);
                Assert.That(relay.FeedbackState, Is.EqualTo(CollectionArtifactFeedbackState.PlacementInvalid));
                relay.PresentPlacementProximity(true);
                Assert.That(relay.FeedbackState, Is.EqualTo(CollectionArtifactFeedbackState.PlacementValid));
                var tintedRenderer = (Renderer)new SerializedObject(relay).FindProperty("_renderers")
                    .GetArrayElementAtIndex(0).objectReferenceValue;
                var tintedMaterialIndex = Array.FindIndex(
                    tintedRenderer.sharedMaterials,
                    material => material != null &&
                                (material.HasProperty("_BaseColor") || material.HasProperty("_Color")));
                var tintedProperty = tintedRenderer.sharedMaterials[tintedMaterialIndex].HasProperty("_BaseColor")
                    ? "_BaseColor"
                    : "_Color";
                var feedbackBlock = new MaterialPropertyBlock();
                tintedRenderer.GetPropertyBlock(feedbackBlock, tintedMaterialIndex);
                var validTint = feedbackBlock.GetColor(tintedProperty);
                Assert.That(validTint.g, Is.GreaterThan(validTint.r),
                    "A valid placement must remain visibly green even when shader emission is disabled.");
                relay.PresentPlacementProximity(null);
                Assert.That(relay.FeedbackState, Is.EqualTo(CollectionArtifactFeedbackState.Held));
                Assert.That(instance.transform.localScale, Is.EqualTo(grabRootScale),
                    "Presentation feedback must never resize the root collider while selected.");

                grabbable.ProcessPointerEvent(new PointerEvent(733, PointerEventType.Unselect, pose));

                Assert.That(grabReleased, Is.EqualTo(1));
                Assert.That(relay.IsArmed, Is.True,
                    "A release remains retryable until the placement owner accepts it.");
                Assert.That(interactable.enabled, Is.True);
                Assert.That(relay.FeedbackState, Is.EqualTo(CollectionArtifactFeedbackState.Ready));

                grabbable.ProcessPointerEvent(new PointerEvent(733, PointerEventType.Select, pose));
                grabbable.ProcessPointerEvent(new PointerEvent(733, PointerEventType.Unselect, pose));

                Assert.That(grabStarted, Is.EqualTo(2));
                Assert.That(grabReleased, Is.EqualTo(2),
                    "A missed placement can begin one new physical grab/release cycle.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CollectionArtifactOfferIsReachableWithoutMovingTheWholeCompendiumCloser()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionWorldPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            Assert.That(prefab, Is.Not.Null, CollectionWorldPath);
            Assert.That(catalog, Is.Not.Null);

            var frontend = prefab.GetComponent<CollectionWorldFrontend>();
            Assert.That(frontend, Is.Not.Null);
            var serialized = new SerializedObject(frontend);
            var spawn = RequiredObject<Transform>(serialized, "_artifactSpawn");
            var catchPoint = RequiredObject<Transform>(serialized, "_artifactCatch");
            var viewerDistance = catalog.PresentationTheme.ViewerDistance;

            Assert.That(viewerDistance, Is.EqualTo(1.24f).Within(0.0001f),
                "The full collection tree/compendium should keep its established viewing distance.");
            Assert.That(spawn.localPosition.z, Is.EqualTo(-0.59f).Within(0.0001f));
            Assert.That(catchPoint.localPosition.z, Is.EqualTo(-0.62f).Within(0.0001f));

            var spawnHorizontalDistance = viewerDistance + spawn.localPosition.z;
            var catchHorizontalDistance = viewerDistance + catchPoint.localPosition.z;
            var verticalOffset = catalog.PresentationTheme.VerticalOffset;
            var spawnReachDistance = new Vector2(
                spawnHorizontalDistance,
                verticalOffset + spawn.localPosition.y).magnitude;
            var catchReachDistance = new Vector2(
                catchHorizontalDistance,
                verticalOffset + catchPoint.localPosition.y).magnitude;

            Assert.That(spawnHorizontalDistance, Is.InRange(0.6f, 0.7f),
                "The falling Artifact must enter the same near-hand reach closure as the reference interaction surfaces.");
            Assert.That(catchHorizontalDistance, Is.InRange(0.6f, 0.7f),
                "The stable Artifact must not remain at the former head-front distance of about one metre.");
            Assert.That(spawnReachDistance, Is.LessThanOrEqualTo(0.72f));
            Assert.That(catchReachDistance, Is.LessThanOrEqualTo(0.72f));
        }

        [Test]
        public void CollectionFoldoutUsesOneTransformerWithTwoAccessibleEdges()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionArtifactPath);
            var foldout = prefab.GetComponent<FieldbookFoldout>();
            Assert.That(foldout, Is.Not.Null);
            var data = new SerializedObject(prefab.GetComponent<Grabbable>());
            Assert.That(data.FindProperty("_oneGrabTransformer").objectReferenceValue, Is.SameAs(foldout));
            Assert.That(prefab.GetComponentsInChildren<BoxCollider>(true).Length, Is.EqualTo(2));
        }

        [Test]
        public void CollectionArtifactReleaseNeverCollectsEvenAtFormerBookSlot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionWorldPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            Assert.That(catalog.TryBuild(out var definition, out var error), Is.True, error);

            var instance = UnityEngine.Object.Instantiate(prefab);
            var viewer = new GameObject("CollectionPlacementViewer");
            viewer.AddComponent<Camera>();
            var frontend = instance.GetComponent<CollectionWorldFrontend>();
            using var progress = CollectionProgressModuleFactory.Create();
            var intents = new CapturingCollectionIntentSink(progress);
            var journey = JourneySessionId.CreateNew();
            var tutorialStages = new List<CollectionArtifactTutorialStage>();
            try
            {
                var serialized = new SerializedObject(frontend);
                var tree = RequiredObject<Transform>(serialized, "_bookResponseRoot");
                var grassStone = RequiredObject<Transform>(serialized, "_recordDisplayRoot");
                var treeBasePosition = tree.localPosition;
                var grassStoneBasePosition = grassStone.localPosition;
                frontend.Configure(viewer.transform, new CapturingGazeSurfaceRegistry(), catalog);
                frontend.ArtifactTutorialStageChanged += tutorialStages.Add;
                frontend.Bind(progress, intents);
                progress.BeginSession(journey, definition);
                var offered = definition.Artifacts[0];
                progress.OfferArtifact(
                    new ObservationCompletedFact(
                        SessionToken.CreateNew(),
                        journey,
                        new SceneId("giant_saguaro"),
                        ObservationCompletionKind.Confirmation,
                        "completion:manual-placement"),
                    offered.ArtifactId);

                var artifactHint = RequiredObject<TMP_Text>(serialized, "_artifactHint");
                Assert.That(artifactHint.text, Does.Contain("页边"));

                Assert.That(Vector3.Distance(tree.localPosition, treeBasePosition),
                    Is.LessThan(0.0001f));
                Assert.That(Vector3.Distance(grassStone.localPosition, grassStoneBasePosition),
                    Is.LessThan(0.0001f),
                    "The tree and its authored grass/stone base must move as one coherent placement scene.");

                var artifactField = typeof(CollectionWorldFrontend).GetField(
                    "_activeArtifactObject",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(artifactField, Is.Not.Null);
                var artifactObject = artifactField.GetValue(frontend) as GameObject;
                Assert.That(artifactObject, Is.Not.Null);
                var relay = artifactObject.GetComponent<CollectionArtifactGrabRelay>();
                var grabbable = artifactObject.GetComponent<Grabbable>();
                var kinematicLocker = artifactObject.GetComponent<RigidbodyKinematicLocker>();
                Assert.That(relay, Is.Not.Null);
                Assert.That(grabbable, Is.Not.Null);
                Assert.That(kinematicLocker, Is.Not.Null);
                if (grabbable.Points == null) InvokeSdkLifecycle(grabbable, "Awake");
                InvokeSdkLifecycle(grabbable, "Start");
                InvokeSdkLifecycle(kinematicLocker, "Awake");
                relay.SetInteractionEnabled(true);

                var catchPoint = RequiredObject<Transform>(serialized, "_artifactCatch");
                var returnSlot = RequiredObject<Transform>(serialized, "_artifactReturnSlot");
                const float radius = .18f; // Former slot radius must no longer grant collection.
                var rotation = artifactObject.transform.rotation;
                var catchPose = new Pose(artifactObject.transform.position, rotation);
                grabbable.ProcessPointerEvent(new PointerEvent(811, PointerEventType.Hover, catchPose));
                grabbable.ProcessPointerEvent(new PointerEvent(811, PointerEventType.Select, catchPose));

                Assert.That(progress.CurrentState.Artifacts[0].State,
                    Is.EqualTo(CollectionArtifactStatus.Available),
                    "Select starts following the hand and must not collect the Artifact.");

                var missedPosition = returnSlot.position + Vector3.right * (radius * 1.25f);
                var missedPose = new Pose(missedPosition, rotation);
                grabbable.ProcessPointerEvent(new PointerEvent(811, PointerEventType.Move, missedPose));
                grabbable.ProcessPointerEvent(new PointerEvent(811, PointerEventType.Unselect, missedPose));

                Assert.That(progress.CurrentState.Artifacts[0].State,
                    Is.EqualTo(CollectionArtifactStatus.Available));
                catchPose = new Pose(artifactObject.transform.position, artifactObject.transform.rotation);
                grabbable.ProcessPointerEvent(new PointerEvent(811, PointerEventType.Select, catchPose));
                var acceptedPose = new Pose(returnSlot.position, artifactObject.transform.rotation);
                grabbable.ProcessPointerEvent(new PointerEvent(811, PointerEventType.Move, acceptedPose));
                grabbable.ProcessPointerEvent(new PointerEvent(811, PointerEventType.Unselect, acceptedPose));

                Assert.That(progress.CurrentState.Artifacts[0].State,
                    Is.EqualTo(CollectionArtifactStatus.Available),
                    "Release cannot collect a foldout, even at the former book slot.");
                Assert.That(tutorialStages, Does.Contain(CollectionArtifactTutorialStage.Available));
                Assert.That(tutorialStages, Does.Contain(CollectionArtifactTutorialStage.Held));
                Assert.That(tutorialStages, Has.No.Member(CollectionArtifactTutorialStage.PlacementMissed));
                Assert.That(tutorialStages, Has.No.Member(CollectionArtifactTutorialStage.Placed));

                var foldout = artifactObject.GetComponent<FieldbookFoldout>();
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var gesture = (FoldoutGesture)typeof(FieldbookFoldout).GetField("_gesture", flags).GetValue(foldout);
                gesture.Begin(0, 1);
                gesture.Move(.08f, .025f, true);
                gesture.End();
                var beforeRecovery = foldout.Progress;
                var tokenBeforeRecovery = progress.CurrentState.Artifacts[0].InstanceToken;
                typeof(CollectionWorldFrontend).GetMethod("StopMotion", flags).Invoke(frontend, null);
                viewer.transform.position = Vector3.right * 3;
                var recover = typeof(CollectionWorldFrontend).GetMethod("TickFoldoutRecovery", flags);
                recover.Invoke(frontend, new object[] { 1f });
                recover.Invoke(frontend, new object[] { 2.1f });
                Assert.That(Vector3.Distance(artifactObject.transform.position, viewer.transform.position), Is.LessThan(1f));
                Assert.That(foldout.Progress, Is.EqualTo(beforeRecovery));
                Assert.That(progress.CurrentState.Artifacts[0].InstanceToken, Is.EqualTo(tokenBeforeRecovery));
                Assert.That(progress.CurrentState.Artifacts[0].State, Is.EqualTo(CollectionArtifactStatus.Available));
                var recoveredPosition = artifactObject.transform.position;
                viewer.transform.position = Vector3.left * 3;
                recover.Invoke(frontend, new object[] { 4f });
                recover.Invoke(frontend, new object[] { 6f });
                Assert.That(artifactObject.transform.position, Is.EqualTo(recoveredPosition),
                    "Recovery must not become continuous head-follow or repeatedly teleport a suspended page.");
            }
            finally
            {
                frontend?.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void CollectionWorldOwnsNoPalmRecallEntrypoint()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionWorldPath);
            Assert.That(prefab, Is.Not.Null, CollectionWorldPath);
            var palmSeed = prefab.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(value => string.Equals(value.name, "PalmSeed", StringComparison.Ordinal));
            Assert.That(palmSeed, Is.Null,
                "Palm recall belongs only to VisitorAtlasHub; Collection must not keep a hidden seed listener.");
        }

        [Test]
        public void ExplicitBrowseOpeningShowsTheCompendiumWithoutASecondHiddenAction()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionWorldPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryBuild(out var definition, out var error), Is.True, error);

            var instance = UnityEngine.Object.Instantiate(prefab);
            var viewer = new GameObject("CollectionBrowseViewer");
            viewer.AddComponent<Camera>();
            var frontend = instance.GetComponent<CollectionWorldFrontend>();
            using var progress = CollectionProgressModuleFactory.Create();
            var session = BotanicalGardenQR.Experience.Contracts.JourneySessionId.CreateNew();
            try
            {
                frontend.Configure(viewer.transform, new CapturingGazeSurfaceRegistry(), catalog);
                frontend.Bind(progress, new NoOpCollectionIntentSink());
                progress.BeginSession(session, definition);

                var artifacts = definition.Artifacts
                    .Select((artifact, index) => new CollectionArtifactState(
                        artifact,
                        index == 0 ? CollectionArtifactStatus.Available : CollectionArtifactStatus.Unseen,
                        default))
                    .ToArray();
                frontend.OnCollectionProgressStateChanged(new CollectionProgressViewState(
                    2,
                    session,
                    artifacts,
                    0,
                    definition.TotalCount,
                    Array.Empty<string>(),
                    null));

                frontend.SetBrowseOpeningAllowed(false);
                Assert.That(frontend.CanOpenBrowseMode, Is.False,
                    "An application-owned foreground surface must block the palm/browse entry.");
                frontend.OpenBrowseMode();
                Assert.That(frontend.SurfaceKind, Is.EqualTo(CollectionPresentationSurfaceKind.Hidden));

                frontend.SetBrowseOpeningAllowed(true);
                Assert.That(frontend.CanOpenBrowseMode, Is.True);
                frontend.OpenBrowseMode();

                var serialized = new SerializedObject(frontend);
                var compendium = RequiredObject<GameObject>(serialized, "_compendiumRoot");
                Assert.That(frontend.SurfaceKind, Is.EqualTo(CollectionPresentationSurfaceKind.Browse));
                Assert.That(compendium.activeSelf, Is.True,
                    "The explicit entry must show the six-item compendium immediately.");
                var entries = serialized.FindProperty("_entryViews");
                var availableState = entries.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("_state").objectReferenceValue as TMP_Text;
                Assert.That(availableState, Is.Not.Null);
                Assert.That(availableState.text, Is.EqualTo("待收取 · 凝视重现"));
            }
            finally
            {
                frontend?.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void CollectionArtifactMotionThemeKeepsFiniteThreeStageFeedback()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            var motion = catalog.PresentationTheme.ArtifactMotion;
            Assert.That(motion, Is.Not.Null);
            Assert.That(motion.IsValid(out var error), Is.True, error);
            Assert.That(motion.AnticipationSeconds, Is.InRange(0.05f, 0.6f));
            Assert.That(motion.ReadySettleSeconds, Is.InRange(0.05f, 0.6f));
            Assert.That(motion.MissReturnSeconds, Is.InRange(0.1f, 0.8f));
            Assert.That(motion.PlacementFeedbackRadiusMultiplier, Is.InRange(1f, 4f));
            Assert.That(CollectionWorldFrontend.Smooth01(-1f), Is.Zero);
            Assert.That(CollectionWorldFrontend.Smooth01(1f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(CollectionArtifactGrabRelay.FeedbackScaleMultiplier(
                    CollectionArtifactFeedbackState.Dormant,
                    motion,
                    0f),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(CollectionArtifactGrabRelay.FeedbackScaleMultiplier(
                    CollectionArtifactFeedbackState.Held,
                    motion,
                    0f),
                Is.EqualTo(motion.HeldScale).Within(0.0001f));
            Assert.That(CollectionArtifactGrabRelay.FeedbackScaleMultiplier(
                    CollectionArtifactFeedbackState.PlacementValid,
                    motion,
                    0f),
                Is.GreaterThan(CollectionArtifactGrabRelay.FeedbackScaleMultiplier(
                    CollectionArtifactFeedbackState.PlacementInvalid,
                    motion,
                    0f)));
        }

        [Test]
        public void CompletingTheCollectionDoesNotOpenBrowseWithoutAnExplicitVisitorIntent()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionWorldPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryBuild(out var definition, out var error), Is.True, error);

            var instance = UnityEngine.Object.Instantiate(prefab);
            var viewer = new GameObject("CollectionCompletionViewer");
            viewer.AddComponent<Camera>();
            var frontend = instance.GetComponent<CollectionWorldFrontend>();
            using var progress = CollectionProgressModuleFactory.Create();
            var pending = new CollectionPendingPresentation(
                new CollectionPresentationId(99),
                CollectionPresentationKind.Reward,
                definition.Artifacts[0].ArtifactId,
                true,
                new[] { "collection_complete" });
            var artifacts = definition.Artifacts
                .Select(artifact => new CollectionArtifactState(
                    artifact,
                    CollectionArtifactStatus.Collected,
                    default))
                .ToArray();
            var session = JourneySessionId.CreateNew();
            var pendingState = new CollectionProgressViewState(
                10,
                session,
                artifacts,
                definition.TotalCount,
                definition.TotalCount,
                new[] { "collection_complete" },
                pending);
            var settledState = new CollectionProgressViewState(
                11,
                session,
                artifacts,
                definition.TotalCount,
                definition.TotalCount,
                new[] { "collection_complete" },
                null);

            try
            {
                frontend.Configure(viewer.transform, new CapturingGazeSurfaceRegistry(), catalog);
                frontend.Bind(progress, new NoOpCollectionIntentSink());
                frontend.OnCollectionProgressStateChanged(pendingState);
                Assert.That(frontend.SurfaceKind, Is.EqualTo(CollectionPresentationSurfaceKind.Reward));

                var stateField = typeof(CollectionWorldFrontend).GetField(
                    "_state",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var finish = typeof(CollectionWorldFrontend).GetMethod(
                    "FinishPendingPresentation",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(stateField, Is.Not.Null);
                Assert.That(finish, Is.Not.Null);
                stateField.SetValue(frontend, settledState);
                finish.Invoke(frontend, new object[] { pending.Id });

                var serialized = new SerializedObject(frontend);
                var compendium = RequiredObject<GameObject>(serialized, "_compendiumRoot");
                Assert.That(frontend.SurfaceKind, Is.Not.EqualTo(CollectionPresentationSurfaceKind.Browse),
                    "Collection completion may close its reward, but Browse requires a separate visitor intent.");
                Assert.That(compendium.activeSelf, Is.False);
            }
            finally
            {
                frontend?.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void CollectedEntryDetailReplacesCompendiumUntilClosed()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionWorldPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryBuild(out var definition, out var error), Is.True, error);

            var instance = UnityEngine.Object.Instantiate(prefab);
            var viewer = new GameObject("CollectionDetailViewer");
            viewer.AddComponent<Camera>();
            var frontend = instance.GetComponent<CollectionWorldFrontend>();
            try
            {
                frontend.Configure(viewer.transform, new CapturingGazeSurfaceRegistry(), catalog);
                var artifacts = definition.Artifacts
                    .Select((artifact, index) => new CollectionArtifactState(
                        artifact,
                        index == 0 ? CollectionArtifactStatus.Collected : CollectionArtifactStatus.Unseen,
                        default))
                    .ToArray();
                frontend.OnCollectionProgressStateChanged(new CollectionProgressViewState(
                    1,
                    BotanicalGardenQR.Experience.Contracts.JourneySessionId.CreateNew(),
                    artifacts,
                    1,
                    definition.TotalCount,
                    Array.Empty<string>(),
                    null));
                frontend.OpenBrowseMode();

                var serialized = new SerializedObject(frontend);
                var compendium = RequiredObject<GameObject>(serialized, "_compendiumRoot");
                var detail = RequiredObject<GameObject>(serialized, "_detailRoot");
                var closeDetail = RequiredObject<Button>(serialized, "_closeDetailButton");
                var entries = serialized.FindProperty("_entryViews");
                var firstEntry = entries.GetArrayElementAtIndex(0);
                var firstEntryButton = firstEntry.FindPropertyRelative("_button").objectReferenceValue as Button;
                Assert.That(firstEntryButton, Is.Not.Null);

                firstEntryButton.onClick.Invoke();

                Assert.That(compendium.activeSelf, Is.False,
                    "Opening a collected seed detail must replace the compendium instead of overlapping it.");
                Assert.That(detail.activeSelf, Is.True);

                closeDetail.onClick.Invoke();

                Assert.That(detail.activeSelf, Is.False);
                Assert.That(compendium.activeSelf, Is.True,
                    "Closing a seed detail must restore the same compendium browse surface.");
            }
            finally
            {
                frontend?.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void DeferredAvailableArtifactDoesNotBlockBrowseAndCanBeReopenedFromItsEntry()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionWorldPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CollectionCatalogAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/CollectionCatalog.asset");
            Assert.That(catalog.TryBuild(out var definition, out var error), Is.True, error);

            var instance = UnityEngine.Object.Instantiate(prefab);
            var viewer = new GameObject("CollectionAvailableBrowseViewer");
            viewer.AddComponent<Camera>();
            var frontend = instance.GetComponent<CollectionWorldFrontend>();
            using var progress = CollectionProgressModuleFactory.Create();
            var intents = new CapturingCollectionIntentSink(progress);
            var journey = JourneySessionId.CreateNew();
            try
            {
                progress.BeginSession(journey, definition);
                var artifact = definition.Artifacts[0];
                progress.OfferArtifact(
                    new ObservationCompletedFact(
                        SessionToken.CreateNew(), journey, new SceneId("giant_saguaro"),
                        ObservationCompletionKind.Confirmation, "completion:test"),
                    artifact.ArtifactId);
                progress.DeferAvailableArtifactPresentation(progress.CurrentState.PendingPresentation.Id);

                frontend.Configure(viewer.transform, new CapturingGazeSurfaceRegistry(), catalog);
                frontend.Bind(progress, intents);
                var serialized = new SerializedObject(frontend);
                var deferButton = RequiredObject<Button>(serialized, "_deferArtifactButton");

                frontend.OpenBrowseMode();

                Assert.That(frontend.SurfaceKind, Is.EqualTo(CollectionPresentationSurfaceKind.Browse));
                Assert.That(intents.PresentedArtifactId, Is.Empty);
                var entries = serialized.FindProperty("_entryViews");
                var firstButton = entries.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("_button").objectReferenceValue as Button;
                Assert.That(firstButton, Is.Not.Null);
                Assert.That(firstButton.interactable, Is.True);

                firstButton.onClick.Invoke();

                Assert.That(intents.PresentedArtifactId, Is.EqualTo(artifact.ArtifactId));
                Assert.That(frontend.SurfaceKind, Is.EqualTo(CollectionPresentationSurfaceKind.ArtifactOffer));

                deferButton.onClick.Invoke();

                Assert.That(frontend.SurfaceKind, Is.EqualTo(CollectionPresentationSurfaceKind.Browse));

                firstButton.onClick.Invoke();
                var available = progress.CurrentState.Artifacts[0];
                progress.CollectArtifact(available.Definition.ArtifactId, available.InstanceToken);

                Assert.That(progress.CurrentState.Artifacts[0].State,
                    Is.EqualTo(CollectionArtifactStatus.Collected));
                var firstEntry = entries.GetArrayElementAtIndex(0);
                var entryState = firstEntry.FindPropertyRelative("_state").objectReferenceValue as TMP_Text;
                var discoveredMark = firstEntry.FindPropertyRelative("_discoveredMark").objectReferenceValue as Image;
                var browseCount = RequiredObject<TMP_Text>(serialized, "_browseCount");
                Assert.That(entryState, Is.Not.Null);
                Assert.That(entryState.text, Is.EqualTo("已收藏"));
                Assert.That(discoveredMark, Is.Not.Null);
                Assert.That(discoveredMark.gameObject.activeSelf, Is.True);
                Assert.That(browseCount.text, Is.EqualTo($"1 / {definition.Artifacts.Count}"));
            }
            finally
            {
                frontend?.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void VisitorGamePresentationContainsNoDevelopmentBuildLabels()
        {
            var paths = new[] { VisitorProloguePath, ObservationCompletionPath, CollectionWorldPath };
            var forbidden = new[] { "办公室测试", "测试版", "调试版", "placeholder", "preview build", "demo build" };
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                var leaked = prefab.GetComponentsInChildren<TMP_Text>(true)
                    .Where(label => label != null && forbidden.Any(marker =>
                        (label.text ?? string.Empty).IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0))
                    .Select(label => $"{label.name}: {label.text}")
                    .ToArray();

                Assert.That(leaked, Is.Empty, $"Development-only visitor text leaked into {path}.");
            }
        }

        [Test]
        public void VisitorInvitationUsesOneFieldbookAndNoRetiredSeedPokeChain()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorProloguePath);
            Assert.That(prefab.GetComponentsInChildren<FieldbookInvitationRitual>(true), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<PokeInteractable>(true), Is.Empty);
            var book = RequiredTransform(prefab.transform, "InvitationBookVisual").GetComponent<MeshFilter>();
            Assert.That(book.sharedMesh, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(book.sharedMesh), Does.EndWith("RpgItemCollection2/book.obj"));
            Assert.That(prefab.GetComponentsInChildren<Transform>(true).Any(t =>
                t.name == "StartAction" || t.name == "AwakeningSeed" || t.name == "SeedTouchSurface"), Is.False);
        }

        [Test]
        public void VisitorScenePreservesPokeInteractorsForRealTools()
        {
            var scene = SceneManager.GetSceneByPath(VisitorScenePath);
            var openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(VisitorScenePath, OpenSceneMode.Additive);

            try
            {
                var activePokeInteractors = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PokeInteractor>(true))
                    .Where(interactor => interactor != null && interactor.enabled &&
                        interactor.gameObject.activeInHierarchy)
                    .ToArray();
                Assert.That(activePokeInteractors, Has.Length.GreaterThanOrEqualTo(2),
                    "Visitor keeps the official Poke interactor pair for actual tools and content.");
                foreach (var interactor in activePokeInteractors)
                {
                    var serialized = new SerializedObject(interactor);
                    Assert.That(RequiredObject<Transform>(serialized, "_pointTransform"), Is.Not.Null);
                }
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void VisitorSceneHandsFirstBindingRemovesControllerGateFromRealHandGrabInteractors()
        {
            var scene = SceneManager.GetSceneByPath(VisitorScenePath);
            var openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(VisitorScenePath, OpenSceneMode.Additive);

            try
            {
                var installer = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<VisitorInstaller>(true))
                    .FirstOrDefault(candidate => candidate != null);
                Assert.That(installer, Is.Not.Null,
                    "The visitor scene must carry its VisitorInstaller composition root.");
                var interactionRigRoot = new SerializedObject(installer)
                    .FindProperty("_interactionRigRoot").objectReferenceValue as Transform;
                Assert.That(interactionRigRoot, Is.Not.Null,
                    "VisitorInstaller must keep the interaction rig root bound for the always-on guard.");

                var handsFirstBindings = installer.GetComponents<HandsFirstInteractionRigBinding>();
                Assert.That(handsFirstBindings, Has.Length.EqualTo(1),
                    "Visitor publishing must author exactly one typed hands-first rig binding.");
                var handsFirst = handsFirstBindings[0];
                Assert.That(handsFirst.Bindings.Count, Is.EqualTo(2),
                    "Visitor hands-first policy requires explicit left and right tracker/HandRef bindings.");
                handsFirst.ApplyHandsFirstPolicy();

                var handGrabInteractors = interactionRigRoot
                    .GetComponentsInChildren<HandGrabInteractor>(true)
                    .Where(interactor => interactor != null &&
                                         handsFirst.Bindings.Any(binding =>
                                             GatesAHandGrabInteractor(
                                                 binding.Tracker,
                                                 new[] { interactor })))
                    .ToArray();
                Assert.That(handGrabInteractors, Has.Length.EqualTo(2),
                    "Visitor requires the real left/right HandGrabInteractors from the official interaction rig.");

                foreach (var authoredBinding in handsFirst.Bindings)
                {
                    Assert.That(authoredBinding, Is.Not.Null);
                    Assert.That(authoredBinding.Tracker, Is.Not.Null);
                    Assert.That(authoredBinding.HandAvailability, Is.Not.Null);
                    Assert.That(authoredBinding.Tracker.transform.IsChildOf(interactionRigRoot), Is.True);
                    Assert.That(authoredBinding.HandAvailability.transform.IsChildOf(interactionRigRoot), Is.True);
                    Assert.That(new SerializedObject(authoredBinding.Tracker)
                            .FindProperty("_activeState").objectReferenceValue,
                        Is.SameAs(authoredBinding.HandAvailability),
                        "The public ApplyHandsFirstPolicy contract must inject the authored HandRef into its tracker.");
                }
                var authoredPaths = handsFirst.Bindings
                    .Select(binding => HierarchyPath(binding.Tracker.transform))
                    .ToArray();
                Assert.That(authoredPaths.Any(path =>
                    path.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0), Is.True);
                Assert.That(authoredPaths.Any(path =>
                    path.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0), Is.True);

                foreach (var interactor in handGrabInteractors)
                {
                    Assert.That(interactor.enabled, Is.True,
                        $"{HierarchyPath(interactor.transform)} must be authored enabled.");
                    Assert.That(interactor.gameObject.activeSelf, Is.True,
                        $"{HierarchyPath(interactor.transform)} must be authored active.");
                }

                var controllerGatedTrackers = interactionRigRoot
                    .GetComponentsInChildren<ActiveStateTracker>(true)
                    .Where(tracker => tracker != null &&
                                      GatesAHandGrabInteractor(tracker, handGrabInteractors))
                    .Where(tracker => DependsOnControllerState(
                        new SerializedObject(tracker).FindProperty("_activeState").objectReferenceValue))
                    .ToArray();
                Assert.That(controllerGatedTrackers, Is.Empty,
                    "Meta XR 205 hand-grab interactors may be gated by hand-tracking state, but no " +
                    "ActiveStateTracker that controls them may depend on controller state.");
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        static bool GatesAHandGrabInteractor(ActiveStateTracker tracker, HandGrabInteractor[] interactors)
        {
            var serialized = new SerializedObject(tracker);
            var includeChildren = serialized.FindProperty("_includeChildrenAsDependents").boolValue;
            if (includeChildren && interactors.Any(interactor =>
                    interactor != null && interactor.transform.IsChildOf(tracker.transform)))
                return true;

            var gameObjects = serialized.FindProperty("_gameObjects");
            for (var i = 0; i < gameObjects.arraySize; i++)
            {
                if (!(gameObjects.GetArrayElementAtIndex(i).objectReferenceValue is GameObject gated))
                    continue;
                foreach (var interactor in interactors)
                    if (interactor != null &&
                        (interactor.transform == gated.transform || interactor.transform.IsChildOf(gated.transform)))
                        return true;
            }

            var monoBehaviours = serialized.FindProperty("_monoBehaviours");
            for (var i = 0; i < monoBehaviours.arraySize; i++)
                if (monoBehaviours.GetArrayElementAtIndex(i).objectReferenceValue is HandGrabInteractor)
                    return true;

            return false;
        }

        static bool DependsOnControllerState(UnityEngine.Object activeState)
        {
            if (activeState is IController) return true;
            if (activeState != null)
            {
                var typeName = activeState.GetType().FullName;
                if (string.Equals(
                        typeName,
                        "Oculus.Interaction.ControllerActiveState",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        typeName,
                        "Oculus.Interaction.Input.ControllerActiveState",
                        StringComparison.Ordinal))
                    return true;
            }
            switch (activeState)
            {
                case ActiveStateGroup group:
                {
                    var states = new SerializedObject(group).FindProperty("_activeStates");
                    for (var i = 0; i < states.arraySize; i++)
                        if (DependsOnControllerState(
                                states.GetArrayElementAtIndex(i).objectReferenceValue))
                            return true;
                    return false;
                }
                case ActiveStateNot not:
                    return DependsOnControllerState(
                        new SerializedObject(not).FindProperty("_activeState").objectReferenceValue);
                default:
                    return false;
            }
        }

        [Test]
        public void VisitorProloguePresenterConfiguresWhileGuideRitualStartsHidden()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorProloguePath);
            var theme = AssetDatabase.LoadAssetAtPath<VisitorPrologueThemeAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/VisitorPrologueTheme.asset");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(theme, Is.Not.Null);

            var instance = UnityEngine.Object.Instantiate(prefab);
            var presenter = instance.GetComponent<VisitorProloguePresenter>();
            var viewer = new GameObject("VisitorPrologueConfigurationViewer");
            viewer.AddComponent<Camera>();

            try
            {
                Assert.DoesNotThrow(() =>
                    presenter.Configure(viewer.transform, new CapturingGazeSurfaceRegistry(), theme));
            }
            finally
            {
                presenter?.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void CollectionWorldExposesPresentationLifecycle()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionWorldPath);
            Assert.That(prefab, Is.Not.Null);
            var presenter = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(value => value != null && value.GetType().Name == "CollectionWorldFrontend");
            Assert.That(presenter.GetType().GetProperty("PresentationPhase"), Is.Not.Null);
        }

        [Test]
        public void ObservationCompletionRegistersItsWorldSpaceCanvasForGaze()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ObservationCompletionPath);
            Assert.That(prefab, Is.Not.Null);

            var instance = UnityEngine.Object.Instantiate(prefab);
            var viewer = new GameObject("ObservationCompletionTestViewer");
            viewer.AddComponent<Camera>();
            var presenter = instance.GetComponent<KnowledgeMiniGameFrontend>();
            var expectedCanvas = instance.GetComponentInChildren<Canvas>(true);
            var registry = new CapturingGazeSurfaceRegistry();

            try
            {
                Assert.That(presenter, Is.Not.Null);
                Assert.That(expectedCanvas, Is.Not.Null);

                presenter.Configure(viewer.transform, registry);

                Assert.That(registry.RegisteredSurface, Is.EqualTo(expectedCanvas.transform));
                Assert.That(registry.Label, Is.EqualTo("ObservationCompletionSurface"));
            }
            finally
            {
                if (presenter != null) presenter.Dispose();
                UnityEngine.Object.DestroyImmediate(instance);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [TestCase(ObservationCompletionPath, "InnerFrame")]
        [TestCase(CollectionWorldPath, "RewardGlass")]
        [TestCase(CollectionWorldPath, "ArtifactGlass")]
        public void DominantGamePanelsUseNeutralTranslucentGlass(string prefabPath, string imageName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            var image = prefab.GetComponentsInChildren<Image>(true)
                .Single(value => value != null && value.name == imageName);
            var color = image.color;

            Assert.That(color.a, Is.InRange(0.28f, 0.65f), $"{prefabPath}: {imageName} has invalid glass opacity.");
            Assert.That(
                color.g,
                Is.LessThanOrEqualTo(Mathf.Max(color.r, color.b) + 0.02f),
                $"{prefabPath}: {imageName} must not use a green-dominant backing colour.");
        }

        [Test]
        public void CollectionCompendiumUsesFocusedLayeredParchmentBookStyle()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectionWorldPath);
            Assert.That(prefab, Is.Not.Null, CollectionWorldPath);

            var browseCanvas = prefab.GetComponentsInChildren<Canvas>(true)
                .Single(value => value.name == "BrowseCanvas");
            var canvasRect = (RectTransform)browseCanvas.transform;
            var leftPage = prefab.GetComponentsInChildren<Image>(true)
                .Single(value => value.name == "LeftParchmentPage");
            var rightPage = prefab.GetComponentsInChildren<Image>(true)
                .Single(value => value.name == "RightParchmentPage");
            var spine = prefab.GetComponentsInChildren<Image>(true)
                .Single(value => value.name == "ParchmentSpine");
            var cover = prefab.GetComponentsInChildren<Image>(true)
                .Single(value => value.name == "BrowseGlass");
            var detail = prefab.GetComponentsInChildren<Image>(true)
                .Single(value => value.name == "DetailSurface");
            var title = prefab.GetComponentsInChildren<TMP_Text>(true)
                .Single(value => value.name == "BrowseTitle");
            var tree = prefab.GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == "FieldbookReception");

            Assert.That(Mathf.Abs(canvasRect.anchoredPosition.x), Is.LessThanOrEqualTo(0.12f),
                "The open compendium should occupy the viewer focus instead of remaining right-offset.");
            Assert.That(tree.localPosition.x, Is.LessThan(canvasRect.localPosition.x - 0.3f),
                "The tree remains a left-side companion without moving its authored return slot beyond release reach.");
            Assert.That(((RectTransform)leftPage.transform).anchoredPosition.x, Is.LessThan(0f));
            Assert.That(((RectTransform)rightPage.transform).anchoredPosition.x, Is.GreaterThan(0f));
            Assert.That(leftPage.color.a, Is.GreaterThanOrEqualTo(0.95f));
            Assert.That(rightPage.color.a, Is.GreaterThanOrEqualTo(0.95f));
            Assert.That(leftPage.color.r, Is.GreaterThan(leftPage.color.b));
            Assert.That(rightPage.color.r, Is.GreaterThan(rightPage.color.b));
            Assert.That(((RectTransform)spine.transform).sizeDelta.x, Is.InRange(16f, 28f));
            Assert.That(cover.color.r, Is.GreaterThan(cover.color.g * 2f),
                "The page spread requires a visibly brown leather cover beneath it.");
            Assert.That(detail.color.r, Is.GreaterThan(detail.color.b));
            Assert.That(detail.color.a, Is.GreaterThanOrEqualTo(0.9f));
            Assert.That(title.text, Is.EqualTo("魔法植物图鉴"));
            Assert.That(title.color.maxColorComponent, Is.LessThan(0.25f),
                "Book-page titles use dark ink rather than luminous glass-panel text.");
            Assert.That(leftPage.raycastTarget, Is.False);
            Assert.That(rightPage.raycastTarget, Is.False);
            Assert.That(spine.raycastTarget, Is.False);
        }

        [TestCase(ObservationCompletionPath)]
        [TestCase(CollectionWorldPath)]
        public void SemanticOutlinedButtonsUseOpaqueNeutralCores(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            var outlinedButtons = prefab.GetComponentsInChildren<Button>(true)
                .Where(button => button != null && button.GetComponent<Outline>() != null)
                .ToArray();

            Assert.That(outlinedButtons, Is.Not.Empty, prefabPath);
            foreach (var button in outlinedButtons)
            {
                var image = button.targetGraphic as Image;
                Assert.That(image, Is.Not.Null, $"{prefabPath}: {button.name} requires an Image target.");
                Assert.That(
                    image.color.a,
                    Is.GreaterThanOrEqualTo(0.99f),
                    $"{prefabPath}: {button.name} leaks its semantic outline through a translucent core.");
                Assert.That(
                    Mathf.Max(image.color.r, image.color.g, image.color.b) -
                    Mathf.Min(image.color.r, image.color.g, image.color.b),
                    Is.LessThanOrEqualTo(0.025f),
                    $"{prefabPath}: {button.name} must keep its button core chromatically neutral.");
            }
        }

        static CollectionProgressViewState ProgressState(int collected, int total)
            => new CollectionProgressViewState(
                1,
                default,
                Array.Empty<CollectionArtifactState>(),
                collected,
                total,
                Array.Empty<string>(),
                null);

        static void AssertAudiblePresentationSource(string prefabPath, string label)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            var sources = prefab.GetComponentsInChildren<AudioSource>(true);
            Assert.That(sources, Has.Length.EqualTo(1),
                $"{label} must keep one semantic cue source rather than parallel audio chains.");
            var source = sources[0];
            Assert.That(source.enabled, Is.True, label);
            Assert.That(source.playOnAwake, Is.False, label);
            Assert.That(source.mute, Is.False, label);
            Assert.That(source.volume, Is.GreaterThanOrEqualTo(0.5f), label);
            Assert.That(source.spatialBlend, Is.GreaterThanOrEqualTo(0.75f),
                $"{label} is a world-space surface and should remain locally audible.");
        }

        static T RequiredObject<T>(SerializedObject serialized, string propertyName)
            where T : UnityEngine.Object
        {
            var property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            var value = property.objectReferenceValue as T;
            Assert.That(value, Is.Not.Null, propertyName);
            return value;
        }

        static void InvokeSdkLifecycle(MonoBehaviour target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName}");
            Assert.DoesNotThrow(() => method.Invoke(target, null),
                $"{target.GetType().Name}.{methodName} must complete with the production prefab bindings.");
        }

        static Transform RequiredTransform(Transform root, string name)
        {
            var matches = root.GetComponentsInChildren<Transform>(true)
                .Where(value => value != null && value.name == name)
                .ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), name);
            return matches[0];
        }

        static string HierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        static float MaximumRendererProjection(IEnumerable<Renderer> renderers, Vector3 direction)
        {
            var maximum = float.NegativeInfinity;
            foreach (var renderer in renderers)
            {
                var bounds = renderer.bounds;
                var center = bounds.center;
                var extents = bounds.extents;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                {
                    var corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    maximum = Mathf.Max(maximum, Vector3.Dot(corner, direction));
                }
            }

            return maximum;
        }

        sealed class CapturingGazeSurfaceRegistry : IFrontendGazeSurfaceRegistry
        {
            public System.IDisposable SuspendPanelInput() => new Suspension();
            sealed class Suspension : System.IDisposable { public void Dispose() { } }
            public Transform RegisteredSurface { get; private set; }
            public string Label { get; private set; }

            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(
                Transform surfaceRoot,
                int priority,
                string label)
            {
                RegisteredSurface = surfaceRoot;
                Label = label;
                return new EmptyRegistration();
            }
        }

        sealed class NoOpCollectionIntentSink : BotanicalGardenQR.Collection.Contracts.ICollectionPresentationIntentSink
        {
            public void RequestCollectArtifact(
                string artifactId,
                BotanicalGardenQR.Collection.Contracts.CollectionInstanceToken instanceToken) { }

            public void RequestDeferAvailableArtifactPresentation(
                BotanicalGardenQR.Collection.Contracts.CollectionPresentationId presentationId) { }

            public void RequestPresentAvailableArtifact(string artifactId) { }

            public void RequestAcknowledgePresentation(
                BotanicalGardenQR.Collection.Contracts.CollectionPresentationId presentationId) { }
        }

        sealed class CapturingCollectionIntentSink : BotanicalGardenQR.Collection.Contracts.ICollectionPresentationIntentSink
        {
            readonly BotanicalGardenQR.Collection.Contracts.ICollectionProgress _progress;

            public CapturingCollectionIntentSink(
                BotanicalGardenQR.Collection.Contracts.ICollectionProgress progress)
            {
                _progress = progress;
            }

            public string PresentedArtifactId { get; private set; } = string.Empty;

            public void RequestCollectArtifact(
                string artifactId,
                BotanicalGardenQR.Collection.Contracts.CollectionInstanceToken instanceToken)
                => _progress.CollectArtifact(artifactId, instanceToken);

            public void RequestDeferAvailableArtifactPresentation(
                BotanicalGardenQR.Collection.Contracts.CollectionPresentationId presentationId)
                => _progress.DeferAvailableArtifactPresentation(presentationId);

            public void RequestPresentAvailableArtifact(string artifactId)
            {
                PresentedArtifactId = artifactId;
                _progress.RequestAvailableArtifactPresentation(artifactId);
            }

            public void RequestAcknowledgePresentation(
                BotanicalGardenQR.Collection.Contracts.CollectionPresentationId presentationId)
                => _progress.AcknowledgePresentation(presentationId);
        }

        sealed class EmptyRegistration : IFrontendGazeSurfaceRegistration
        {
            public void Invalidate() { }

            public bool IsFocused => false;
            public void Dispose() { }
        }
    }

}
