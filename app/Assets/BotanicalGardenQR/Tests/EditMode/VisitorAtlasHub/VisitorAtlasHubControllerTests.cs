using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.MapNavigation.Runtime;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.VisitorAtlasHub.Backend;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BotanicalGardenQR.Tests.EditMode.VisitorAtlasHub
{
    public sealed class VisitorAtlasHubControllerTests
    {
        [Test]
        public async Task ConfigureStartsHiddenLoadBeforePoseAndEveryRecallReusesTheSamePoseAndLease()
        {
            var viewer = new GameObject("AtlasViewer");
            var rig = new GameObject("AtlasRig");
            var mapParent = new GameObject("AtlasMapParent");
            var presentation = new RecordingPresentation(mapParent.transform);
            var loader = new DeferredMapLoader();
            var controller = new VisitorAtlasHubController(presentation, loader, null, ReadyMap());
            try
            {
                viewer.transform.SetPositionAndRotation(
                    new Vector3(2f, 1.65f, -1f),
                    Quaternion.Euler(0f, 25f, 0f));
                controller.Configure(viewer.transform, rig.transform);

                Assert.That(loader.LoadCalls, Is.EqualTo(1),
                    "Map preparation must start during Configure, before the first pose Tick.");
                Assert.That(presentation.CommitPoseCalls, Is.Zero);
                Assert.That(presentation.IsVisible, Is.False);

                controller.SetInteractionGate(true);
                controller.Tick(0.02f);
                controller.Tick(0.02f);
                Assert.That(presentation.CommitPoseCalls, Is.EqualTo(1));
                var committedPose = presentation.CommittedPose;
                Assert.That(controller.Phase, Is.EqualTo(VisitorAtlasHubPhase.InitializingHidden));

                viewer.transform.SetPositionAndRotation(
                    new Vector3(8f, 1.65f, 6f),
                    Quaternion.Euler(0f, 160f, 0f));
                controller.Tick(0.5f);
                Assert.That(presentation.CommitPoseCalls, Is.EqualTo(1),
                    "Movement after the initial commit must not update the Hub pose.");

                loader.Complete();
                await WaitUntil(() => controller.Phase == VisitorAtlasHubPhase.ReadyHidden);
                presentation.PalmSample = Candidate();
                controller.Tick(0.225f);
                controller.Tick(0.225f);
                Assert.That(controller.Phase, Is.EqualTo(VisitorAtlasHubPhase.Visible));
                Assert.That(presentation.IsVisible, Is.True);
                Assert.That(presentation.EntryChoicePoseRefreshCalls, Is.EqualTo(1),
                    "First reveal must place the book/scroll entry at the current viewer.");
                Assert.That(presentation.EntryLayerVisible, Is.True);
                Assert.That(presentation.BookInteractionEnabled, Is.True);
                Assert.That(presentation.MapInteractionEnabled, Is.True);

                var bookEntrySelections = 0;
                var collectionOpenRequests = 0;
                controller.BookSelected += () => bookEntrySelections++;
                controller.OpenCollectionRequested += _ => collectionOpenRequests++;
                presentation.PublishMapSelected();
                Assert.That(bookEntrySelections, Is.Zero,
                    "The scroll entry must not publish the book entry intent.");
                Assert.That(collectionOpenRequests, Is.Zero,
                    "The scroll entry must not invoke the Collection path.");
                Assert.That(presentation.BookInteractionEnabled, Is.False);
                Assert.That(presentation.MapInteractionEnabled, Is.False);
                Assert.That(controller.MapRequested, Is.True);
                Assert.That(controller.IsMapVisible, Is.True);
                Assert.That(presentation.IsVisible, Is.False);
                presentation.PublishMapSelected();
                Assert.That(controller.MapRequested, Is.True, "A stale touch after hiding cannot toggle again.");
                controller.SetMapSuppressed(true);
                Assert.That(controller.IsMapVisible, Is.False);
                Assert.That(controller.MapRequested, Is.True);
                controller.Hide();
                controller.SetMapSuppressed(false);
                Assert.That(controller.IsMapVisible, Is.True);
                Assert.That(controller.TryBeginBookOpening(out _), Is.False,
                    "Collection cannot be entered while the map surface is active.");

                presentation.PalmSample = Released();
                controller.Tick(.18f);
                presentation.PalmSample = Candidate();
                controller.Tick(.45f);
                Assert.That(presentation.EntryChoicePoseRefreshCalls, Is.EqualTo(2),
                    "Returning from the fixed map must refresh only the movable entry choices.");
                Assert.That(presentation.BookInteractionEnabled, Is.True);
                Assert.That(presentation.MapInteractionEnabled, Is.True);
                presentation.PublishBookSelected();
                Assert.That(bookEntrySelections, Is.EqualTo(1));
                Assert.That(collectionOpenRequests, Is.Zero,
                    "The entry layer only publishes selection; the independent Bootstrap seam owns opening Collection.");

                Assert.That(controller.TryBeginBookOpening(out var bookGeneration), Is.True);
                presentation.PublishBookOpeningCompleted(bookGeneration);
                Assert.That(collectionOpenRequests, Is.EqualTo(1));
                controller.ConfirmCollectionOpened(bookGeneration);
                Assert.That(controller.Phase, Is.EqualTo(VisitorAtlasHubPhase.Visible));
                Assert.That(presentation.EntryLayerVisible, Is.False,
                    "A real Collection Browse confirmation must suppress the physical entry layer.");
                Assert.That(presentation.BookInteractionEnabled, Is.False);
                Assert.That(presentation.MapInteractionEnabled, Is.False);
                Assert.That(presentation.HideInteractionEnabled, Is.False);

                controller.NotifyCollectionClosed();
                Assert.That(presentation.EntryLayerVisible, Is.True);
                Assert.That(presentation.EntryChoicePoseRefreshCalls, Is.EqualTo(3),
                    "Closing Collection must place the movable entry layer once at the current viewer.");
                Assert.That(presentation.BookInteractionEnabled, Is.True);
                Assert.That(presentation.MapInteractionEnabled, Is.True);

                controller.SetInteractionGate(false);
                Assert.That(presentation.IsVisible, Is.True,
                    "Foreground suppression pauses interaction without hiding the fixed Hub.");
                Assert.That(presentation.BookInteractionEnabled, Is.False);
                controller.SetInteractionGate(true);
                controller.Tick(1f);
                Assert.That(presentation.RevealCalls, Is.EqualTo(2),
                    "A held palm on gate restoration must be primed instead of replaying reveal.");

                controller.Hide();
                Assert.That(controller.Phase, Is.EqualTo(VisitorAtlasHubPhase.ReadyHidden));
                Assert.That(loader.Lease.DisposeCalls, Is.Zero,
                    "Hide retains the already loaded GLB lease.");
                presentation.PalmSample = Released();
                controller.Tick(0.18f);
                presentation.PalmSample = Candidate();
                controller.Tick(0.45f);

                Assert.That(controller.Phase, Is.EqualTo(VisitorAtlasHubPhase.Visible));
                Assert.That(loader.LoadCalls, Is.EqualTo(1));
                Assert.That(presentation.CommitPoseCalls, Is.EqualTo(1));
                Assert.That(presentation.EntryChoicePoseRefreshCalls, Is.EqualTo(4),
                    "Every later summon refreshes the entry pose without recommitting the map pose.");
                Assert.That(presentation.CommittedPose.position, Is.EqualTo(committedPose.position));
                Assert.That(presentation.CommittedPose.rotation, Is.EqualTo(committedPose.rotation));
                presentation.PublishMapSelected();
                Assert.That(controller.MapRequested, Is.False);
                controller.Hide();
            }
            finally
            {
                controller.Dispose();
                Assert.That(loader.Lease.DisposeCalls, Is.EqualTo(loader.HasCompleted ? 1 : 0));
                UnityEngine.Object.DestroyImmediate(mapParent);
                UnityEngine.Object.DestroyImmediate(rig);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public async Task MapPreparationRetriesOnceAndPublishesAStageDiagnostic()
        {
            var viewer = new GameObject("RetryViewer");
            var rig = new GameObject("RetryRig");
            var mapParent = new GameObject("RetryMapParent");
            var presentation = new RecordingPresentation(mapParent.transform);
            var loader = new RetryOnceMapLoader();
            var diagnostics = new List<DiagnosticEvent>();
            var controller = new VisitorAtlasHubController(presentation, loader, diagnostics.Add, ReadyMap());
            try
            {
                controller.Configure(viewer.transform, rig.transform);
                controller.Tick(0.02f);
                controller.Tick(0.02f);

                await WaitUntil(() => controller.Phase == VisitorAtlasHubPhase.ReadyHidden);
                Assert.That(loader.LoadCalls, Is.EqualTo(2));
                Assert.That(diagnostics.Exists(value => value.Code == "ATLAS_MAP_RETRY"), Is.True);
                Assert.That(presentation.CommitPoseCalls, Is.EqualTo(1));
            }
            finally
            {
                controller.Dispose();
                Assert.That(loader.Lease.DisposeCalls, Is.EqualTo(1));
                UnityEngine.Object.DestroyImmediate(mapParent);
                UnityEngine.Object.DestroyImmediate(rig);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public async Task CanceledBookGenerationCannotOpenCollectionAfterItsAnimationCallback()
        {
            var viewer = new GameObject("BookViewer");
            var rig = new GameObject("BookRig");
            var mapParent = new GameObject("BookMapParent");
            var presentation = new RecordingPresentation(mapParent.transform);
            var loader = new ImmediateMapLoader();
            var controller = new VisitorAtlasHubController(presentation, loader, null, ReadyMap());
            try
            {
                controller.Configure(viewer.transform, rig.transform);
                controller.SetInteractionGate(true);
                controller.Tick(0.02f);
                controller.Tick(0.02f);
                await WaitUntil(() => controller.Phase == VisitorAtlasHubPhase.ReadyHidden);
                presentation.PalmSample = Candidate();
                controller.Tick(0.225f);
                controller.Tick(0.225f);
                Assert.That(controller.IsVisible, Is.True);

                var openRequests = 0;
                controller.OpenCollectionRequested += _ => openRequests++;
                Assert.That(controller.TryBeginBookOpening(out var generation), Is.True);
                controller.SetInteractionGate(false);
                presentation.PublishBookOpeningCompleted(generation);

                Assert.That(openRequests, Is.Zero);
                Assert.That(controller.BookState, Is.EqualTo(VisitorAtlasHubBookState.ClosedInteractive));
                Assert.That(presentation.ResetBookCalls, Is.GreaterThanOrEqualTo(1));
                Assert.That(presentation.EntryLayerVisible, Is.True,
                    "A canceled generation must not suppress the physical menu.");
            }
            finally
            {
                controller.Dispose();
                UnityEngine.Object.DestroyImmediate(mapParent);
                UnityEngine.Object.DestroyImmediate(rig);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void ConfigurationFailureDisablesOnlyTheHubAndPublishesOneDiagnostic()
        {
            var viewer = new GameObject("FailedAtlasViewer");
            var rig = new GameObject("FailedAtlasRig");
            var presentation = new ThrowingPresentation();
            var diagnostics = new List<DiagnosticEvent>();
            LogAssert.Expect(
                LogType.Warning,
                new Regex("\\[VisitorAtlasHub\\] Disabled for this visitor session"));
            try
            {
                using var controller = VisitorAtlasHubModuleFactory.Create(
                    presentation,
                    viewer.transform,
                    rig.transform,
                    ReadyMap(),
                    diagnostics.Add);

                Assert.That(controller.Phase, Is.EqualTo(VisitorAtlasHubPhase.Failed));
                Assert.That(controller.IsVisible, Is.False);
                Assert.That(presentation.DisposeCalls, Is.EqualTo(1));
                Assert.That(diagnostics, Has.Count.EqualTo(1));
                Assert.That(diagnostics[0].Code, Is.EqualTo("ATLAS_CONFIGURATION_FAILED"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rig);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        static VisitorAtlasHubPalmSample Candidate() =>
            new VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage.ReadyToSummon, true);

        static VisitorAtlasHubPalmSample Released() =>
            new VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage.TurnPalmUp, true);

        static async Task WaitUntil(Func<bool> predicate)
        {
            for (var attempt = 0; attempt < 20 && !predicate(); attempt++)
                await Task.Yield();
            Assert.That(predicate(), Is.True, "The asynchronous Hub transition did not complete in time.");
        }

        static IMapNavigation ReadyMap()
        {
            var d=new MapDefinition{mapId="test",points=new[]{new MapPoint{id="p",position=new MapPosition(1,0,0)}},routes=new[]{new MapRoute{from="Start",to="p",samples=new[]{new MapPosition(),new MapPosition(1,0,0)}}}};
            var map=new MapNavigationController(d,new NoMotion());
            for(int i=0;i<12;i++)map.TryInitialize(new MapPosition(2,1.6f,-1),25,0,.02f);
            return map;
        }
        sealed class NoMotion:IMapMotionSink
        {public bool TryGetPosition(out MapPosition p){p=default;return false;}public bool Apply(long id,MapPosition p,MapPosition f,bool moving)=>false;public void Hold(long id){}public void Release(long id){} }

        sealed class RecordingPresentation : IVisitorAtlasHubPresentation
        {
            public RecordingPresentation(Transform mapContentRoot)
            {
                MapContentRoot = mapContentRoot;
                Configuration = new VisitorAtlasHubConfiguration(
                    0.15f, 1f, 0f,
                    0.45f, 0.20f, 0.18f, 0.2f, 0.7f, 0.5f);
                PalmSample = Released();
            }

            public Transform MapContentRoot { get; }
            public string StreamingAssetsPath => "VisitorAtlasHub/test.glb";
            public VisitorAtlasHubConfiguration Configuration { get; }
            public Pose CommittedPose { get; private set; }
            public VisitorAtlasHubPalmSample PalmSample { get; set; }
            public int CommitPoseCalls { get; private set; }
            public int EntryChoicePoseRefreshCalls { get; private set; }
            public int RevealCalls { get; private set; }
            public int ResetBookCalls { get; private set; }
            public bool IsVisible { get; private set; }
            public bool BookInteractionEnabled { get; private set; }
            public bool MapInteractionEnabled { get; private set; }
            public bool HideInteractionEnabled { get; private set; }
            public bool EntryLayerVisible { get; private set; } = true;
            public event Action BookSelected;
            public event Action MapSelected;
            public event Action HideRequested { add { } remove { } }
            public event Action<int> BookOpeningCompleted;

            public void Configure(Transform viewer, Transform interactionRigRoot) { }
            public VisitorAtlasHubPalmSample SamplePalmCandidate() => PalmSample;
            public void CommitSessionRoot(Pose sessionRootPose)
            {
                CommitPoseCalls++;
                CommittedPose = sessionRootPose;
            }
            public void RefreshEntryChoicePose() => EntryChoicePoseRefreshCalls++;
            public void SetVisible(bool visible) => IsVisible = visible;
            public void SetEntryLayerVisible(bool visible) => EntryLayerVisible = visible;
            public void SetMapDisplay(bool requested, bool visible) { }
            public void SetInteractionEnabled(
                bool bookEnabled,
                bool mapEnabled,
                bool hideEnabled)
            {
                BookInteractionEnabled = bookEnabled;
                MapInteractionEnabled = mapEnabled;
                HideInteractionEnabled = hideEnabled;
            }
            public void PlayReveal() => RevealCalls++;
            public void BeginBookOpening(int generation) { }
            public void SetBookOpenLocked() { }
            public void ResetBook() => ResetBookCalls++;
            public void RejectBookSelection() { }
            public void Dispose() { }
            public void PublishBookSelected() => BookSelected?.Invoke();
            public void PublishBookOpeningCompleted(int generation) => BookOpeningCompleted?.Invoke(generation);
            public void PublishMapSelected() => MapSelected?.Invoke();
        }

        sealed class RecordingMapLease : IVisitorAtlasHubMapLease
        {
            public GameObject Root { get; } = new GameObject("RecordingAtlasMap");
            public int DisposeCalls { get; private set; }
            public void Dispose()
            {
                DisposeCalls++;
                if (Root != null) UnityEngine.Object.DestroyImmediate(Root);
            }
        }

        sealed class ThrowingPresentation : IVisitorAtlasHubPresentation
        {
            public int DisposeCalls { get; private set; }
            public Transform MapContentRoot => null;
            public string StreamingAssetsPath => "VisitorAtlasHub/test.glb";
            public VisitorAtlasHubConfiguration Configuration => default;
            public event Action BookSelected { add { } remove { } }
            public event Action MapSelected { add { } remove { } }
            public event Action HideRequested { add { } remove { } }
            public event Action<int> BookOpeningCompleted { add { } remove { } }
            public void Configure(Transform viewer, Transform interactionRigRoot) =>
                throw new InvalidOperationException("authored binding missing");
            public VisitorAtlasHubPalmSample SamplePalmCandidate() => default;
            public void CommitSessionRoot(Pose sessionRootPose) { }
            public void RefreshEntryChoicePose() { }
            public void SetVisible(bool visible) { }
            public void SetEntryLayerVisible(bool visible) { }
            public void SetMapDisplay(bool requested, bool visible) { }
            public void SetInteractionEnabled(
                bool bookEnabled,
                bool mapEnabled,
                bool hideEnabled) { }
            public void PlayReveal() { }
            public void BeginBookOpening(int generation) { }
            public void SetBookOpenLocked() { }
            public void ResetBook() { }
            public void RejectBookSelection() { }
            public void Dispose() => DisposeCalls++;
        }

        sealed class DeferredMapLoader : IVisitorAtlasHubMapLoader
        {
            readonly TaskCompletionSource<IVisitorAtlasHubMapLease> _completion =
                new TaskCompletionSource<IVisitorAtlasHubMapLease>();
            public RecordingMapLease Lease { get; } = new RecordingMapLease();
            public int LoadCalls { get; private set; }
            public bool HasCompleted { get; private set; }
            public Task<IVisitorAtlasHubMapLease> LoadAsync(
                Uri source,
                Transform hiddenParent,
                CancellationToken cancellationToken)
            {
                LoadCalls++;
                return _completion.Task;
            }
            public void Complete()
            {
                HasCompleted = true;
                _completion.TrySetResult(Lease);
            }
        }

        sealed class ImmediateMapLoader : IVisitorAtlasHubMapLoader
        {
            public RecordingMapLease Lease { get; } = new RecordingMapLease();
            public Task<IVisitorAtlasHubMapLease> LoadAsync(
                Uri source,
                Transform hiddenParent,
                CancellationToken cancellationToken)
                => Task.FromResult<IVisitorAtlasHubMapLease>(Lease);
        }

        sealed class RetryOnceMapLoader : IVisitorAtlasHubMapLoader
        {
            public RecordingMapLease Lease { get; } = new RecordingMapLease();
            public int LoadCalls { get; private set; }
            public Task<IVisitorAtlasHubMapLease> LoadAsync(
                Uri source,
                Transform hiddenParent,
                CancellationToken cancellationToken)
            {
                LoadCalls++;
                return LoadCalls == 1
                    ? Task.FromException<IVisitorAtlasHubMapLease>(new InvalidOperationException("transient"))
                    : Task.FromResult<IVisitorAtlasHubMapLease>(Lease);
            }
        }
    }
}
