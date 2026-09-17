using System;
using System.Collections.Generic;
using System.Threading;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Activation.Runtime;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.SpatialHost.Contracts;
using NUnit.Framework;
using UnityEditor;

namespace BotanicalGardenQR.Tests.EditMode.Activation
{
    public sealed class ActivationCoordinatorRearmTests
    {
        const string RoutesPath =
            "Assets/BotanicalGardenQR/Content/Authoring/ContentEntryCatalog.asset";

        [Test]
        public void ModalGate_RequiresFreshConfirmationAfterItReopens()
        {
            var fixture = new Fixture();
            try
            {
                var now = DateTimeOffset.UtcNow;
                var observationId = Guid.NewGuid();
                fixture.Coordinator.SetNewRecognitionActivationAllowed(false);
                fixture.Source.Emit(Observation(
                    observationId, 1, now, "zone:entrance_001"));

                Assert.That(fixture.Flow.CommitCount, Is.Zero);
                Assert.That(fixture.Scan.States[^1].Progress, Is.Zero);
                Assert.That(fixture.Scan.States[^1].IsConfirmation, Is.False);

                fixture.Coordinator.SetNewRecognitionActivationAllowed(true);
                Assert.That(fixture.Source.RearmCount, Is.EqualTo(1));
                fixture.Source.Emit(Observation(
                    observationId, 2, now.AddMilliseconds(20), "zone:entrance_001", 0f, false));
                Assert.That(fixture.Flow.CommitCount, Is.Zero,
                    "Reopening the gate must not commit the confirmation observed behind the modal.");

                fixture.Source.Emit(Observation(
                    observationId, 3, now.AddMilliseconds(1020), "zone:entrance_001"));
                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ExplicitClose_RearmsSameVisibleSource_WithoutLost()
        {
            var fixture = new Fixture();
            try
            {
                var now = DateTimeOffset.UtcNow;
                var observationId = Guid.NewGuid();
                fixture.Source.Emit(Observation(observationId, 1, now, "zone:entrance_001"));

                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(1));
                Assert.That(fixture.Flow.CloseCurrent(), Is.True);
                Assert.That(fixture.Source.RearmCount, Is.EqualTo(1));
                Assert.That(fixture.Source.LastRearm.ObservationId, Is.EqualTo(observationId));

                fixture.Source.Emit(Observation(
                    observationId, 2, now.AddMilliseconds(400), "zone:entrance_001", 0f, false));
                fixture.Source.Emit(Observation(
                    observationId, 3, now.AddMilliseconds(1400), "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(1700));

                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(2));
                Assert.That(fixture.Host.PrepareCount, Is.EqualTo(2));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void FallbackObservationInsideDebounce_PublishesZeroProgressAfterCloseRearm()
        {
            var fixture = new Fixture();
            try
            {
                var now = DateTimeOffset.UtcNow;
                fixture.Source.Emit(Observation(
                    Guid.NewGuid(), 1, now, "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(300));
                Assert.That(fixture.Flow.CloseCurrent(), Is.True);

                var stateCount = fixture.Scan.States.Count;
                fixture.Source.Emit(Observation(
                    Guid.NewGuid(),
                    1,
                    now.AddMilliseconds(320),
                    "zone:entrance_001",
                    0f,
                    false));

                Assert.That(fixture.Scan.States.Count, Is.EqualTo(stateCount + 1));
                Assert.That(fixture.Scan.States[^1].Progress, Is.Zero);
                Assert.That(fixture.Scan.States[^1].IsConfirmation, Is.False);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SourceLossClose_DoesNotRequestRearm()
        {
            var fixture = new Fixture();
            try
            {
                var now = DateTimeOffset.UtcNow;
                var observationId = Guid.NewGuid();
                fixture.Source.Emit(Observation(
                    observationId, 1, now, "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(300));

                fixture.Host.CloseOnLost = true;
                fixture.Source.Emit(LostObservation(
                    observationId, 2, now.AddMilliseconds(400), "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(400));

                Assert.That(fixture.Flow.CloseCount, Is.EqualTo(1));
                Assert.That(fixture.Source.RearmCount, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void OldDuplicateLost_DoesNotAffectNewCommittedActivation()
        {
            var fixture = new Fixture();
            try
            {
                var now = DateTimeOffset.UtcNow;
                var oldId = Guid.NewGuid();
                fixture.Source.Emit(Observation(
                    oldId, 1, now, "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(300));
                Assert.That(fixture.Flow.CloseCurrent(), Is.True);

                fixture.Source.Emit(Observation(
                    Guid.NewGuid(),
                    1,
                    now.AddMilliseconds(400),
                    "zone:entrance_001"));
                fixture.Source.Emit(LostObservation(
                    oldId,
                    2,
                    now.AddMilliseconds(410),
                    "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(700));

                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(2));
                Assert.That(fixture.Host.PrepareCount, Is.EqualTo(2));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ConfirmedCandidate_CommitsBeforeSubsequentLost()
        {
            var fixture = new Fixture();
            try
            {
                var now = DateTimeOffset.UtcNow;
                var candidateId = Guid.NewGuid();
                fixture.Source.Emit(Observation(
                    candidateId,
                    1,
                    now,
                    "zone:entrance_001"));
                fixture.Source.Emit(LostObservation(
                    candidateId,
                    2,
                    now.AddMilliseconds(20),
                    "zone:entrance_001"));
                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(1));
                Assert.That(fixture.Host.PrepareCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void DifferentConfirmedSource_ReplacesImmediately()
        {
            var fixture = new Fixture();
            try
            {
                var now = DateTimeOffset.UtcNow;
                fixture.Source.Emit(Observation(Guid.NewGuid(), 1, now, "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(300));

                fixture.Source.Emit(Observation(
                    Guid.NewGuid(), 1, now.AddMilliseconds(400), "plant:orchid_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(700));

                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(2));
                Assert.That(fixture.Flow.CloseCount, Is.EqualTo(1));
                Assert.That(fixture.Host.PrepareCount, Is.EqualTo(2));
                Assert.That(fixture.Source.RearmCount, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void HostPrepareFailure_DoesNotPrepareFlowOrRetainActivation()
        {
            var fixture = new Fixture();
            try
            {
                var now = DateTimeOffset.UtcNow;
                fixture.Host.RejectNextPrepare = true;

                fixture.Source.Emit(Observation(Guid.NewGuid(), 1, now, "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(300));

                Assert.That(fixture.Host.PrepareCount, Is.EqualTo(1));
                Assert.That(fixture.Flow.CommitCount, Is.Zero);
                Assert.That(fixture.Flow.CurrentSession.IsValid, Is.False);

                fixture.Source.Emit(Observation(
                    Guid.NewGuid(), 1, now.AddMilliseconds(700), "plant:orchid_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(1000));

                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(1));
                Assert.That(fixture.Flow.LastSceneId, Is.EqualTo(new SceneId("giant_saguaro")));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ReplacementCloseFailure_DoesNotCommitCandidateOrDropPreviousFlowSession()
        {
            var fixture = new Fixture();
            try
            {
                var now = DateTimeOffset.UtcNow;
                fixture.Source.Emit(Observation(Guid.NewGuid(), 1, now, "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(300));
                var previousSession = fixture.Flow.CurrentSession;

                fixture.Flow.RejectNextClose = true;
                fixture.Source.Emit(Observation(
                    Guid.NewGuid(), 1, now.AddMilliseconds(400), "plant:orchid_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(700));

                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(1));
                Assert.That(fixture.Host.CommitCount, Is.EqualTo(1));
                Assert.That(fixture.Flow.CurrentSession, Is.EqualTo(previousSession));
                Assert.That(fixture.Flow.LastSceneId, Is.EqualTo(new SceneId("welwitschia")));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ReplacementHostCommitFailure_RestoresPreviousSessionAndAllowsNextOpen()
        {
            var fixture = new Fixture();
            try
            {
                var now = DateTimeOffset.UtcNow;
                fixture.Source.Emit(Observation(Guid.NewGuid(), 1, now, "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(300));
                var previousSession = fixture.Flow.CurrentSession;

                fixture.Host.ThrowOnNextCommit = true;
                fixture.Source.Emit(Observation(
                    Guid.NewGuid(), 1, now.AddMilliseconds(400), "plant:orchid_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(700));

                Assert.That(fixture.Flow.CurrentSession, Is.EqualTo(previousSession));
                Assert.That(fixture.Host.CurrentSession, Is.EqualTo(previousSession));
                Assert.That(fixture.Host.CloseCount, Is.Zero);
                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(2));
                Assert.That(fixture.Flow.LastSceneId, Is.EqualTo(new SceneId("welwitschia")));

                fixture.Source.Emit(Observation(
                    Guid.NewGuid(), 1, now.AddMilliseconds(1100), "plant:bamboo_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(1400));

                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(3));
                Assert.That(fixture.Flow.LastSceneId, Is.EqualTo(new SceneId("baobab")));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ReplacementFlowCommitFailure_RollsBackCandidateHostAndRestoresPreviousSession()
        {
            var fixture = new Fixture();
            try
            {
                var now = DateTimeOffset.UtcNow;
                fixture.Source.Emit(Observation(Guid.NewGuid(), 1, now, "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(300));
                var previousSession = fixture.Flow.CurrentSession;

                fixture.Flow.ThrowOnNextCommit = true;
                fixture.Source.Emit(Observation(
                    Guid.NewGuid(), 1, now.AddMilliseconds(400), "plant:orchid_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(700));

                Assert.That(fixture.Flow.CurrentSession, Is.EqualTo(previousSession));
                Assert.That(fixture.Flow.LastSceneId, Is.EqualTo(new SceneId("welwitschia")));
                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(2));
                Assert.That(fixture.Host.CommitCount, Is.EqualTo(3));
                Assert.That(fixture.Host.CloseCount, Is.EqualTo(1));
                Assert.That(fixture.Host.LastClosedSession, Is.Not.EqualTo(previousSession));
                Assert.That(fixture.Host.CurrentSession, Is.EqualTo(previousSession));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void SameActiveSource_DoesNotPrepareOrResetExperience()
        {
            var fixture = new Fixture();
            try
            {
                var now = DateTimeOffset.UtcNow;
                fixture.Source.Emit(Observation(Guid.NewGuid(), 1, now, "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(300));

                fixture.Source.Emit(Observation(
                    Guid.NewGuid(), 1, now.AddMilliseconds(400), "zone:entrance_001"));
                fixture.Coordinator.Tick(now.AddMilliseconds(700));

                Assert.That(fixture.Host.PrepareCount, Is.EqualTo(1));
                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(1));
                Assert.That(fixture.Host.TrackingUpdateCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ConfirmedQr_PublishesCommittedContentFactWithoutRawPayload()
        {
            var fixture = new Fixture();
            try
            {
                var sink = new ContentSink();
                using var subscription = fixture.Coordinator.ObserveContent(sink);
                fixture.Source.Emit(Observation(Guid.NewGuid(), 1, DateTimeOffset.UtcNow, "zone:entrance_001"));

                Assert.That(sink.Facts, Has.Count.EqualTo(1));
                Assert.That(sink.Facts[0].SceneId, Is.EqualTo(new SceneId("welwitschia")));
                Assert.That(sink.Facts[0].EntryRouteId, Is.EqualTo("entry_welwitschia"));
                Assert.That(sink.Facts[0].EntryKind, Is.EqualTo(new SourceKind("qr")));
                Assert.That(sink.Facts[0].IsRecall, Is.False);
                Assert.That(sink.Facts[0].ContentSession.IsValid, Is.True);
                Assert.That(sink.Facts[0].JourneySession.IsValid, Is.True);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ExplicitClose_PublishesPreviousContentForDecisionSurface()
        {
            var fixture = new Fixture();
            try
            {
                var sink = new RecallSink();
                using var subscription = fixture.Coordinator.Observe(sink);
                fixture.Source.Emit(Observation(Guid.NewGuid(), 1, DateTimeOffset.UtcNow, "zone:entrance_001"));

                Assert.That(fixture.Flow.CloseCurrent(), Is.True);
                var state = sink.States[^1];
                Assert.That(state.IsClosed, Is.True);
                Assert.That(state.HasPreviousContent, Is.True);
                Assert.That(state.CanRecall, Is.True);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void RecallObserverThatThrowsInitially_IsIsolatedBeforeActivationPublishes()
        {
            var fixture = new Fixture();
            try
            {
                Assert.That(
                    () => fixture.Coordinator.Observe(new AlwaysThrowingRecallSink()),
                    Throws.Nothing);

                var now = DateTimeOffset.UtcNow;
                fixture.Source.Emit(Observation(Guid.NewGuid(), 1, now, "zone:entrance_001"));
                Assert.That(
                    () => fixture.Coordinator.Tick(now.AddMilliseconds(300)),
                    Throws.Nothing);
                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ThrowingRecallObserver_DoesNotBlockHealthyObserverOrActivation()
        {
            var fixture = new Fixture();
            try
            {
                using var throwing = fixture.Coordinator.Observe(new ThrowAfterInitialRecallSink());
                var healthySink = new RecallSink();
                using var healthy = fixture.Coordinator.Observe(healthySink);
                var now = DateTimeOffset.UtcNow;
                fixture.Source.Emit(Observation(Guid.NewGuid(), 1, now, "zone:entrance_001"));

                Assert.That(
                    () => fixture.Coordinator.Tick(now.AddMilliseconds(300)),
                    Throws.Nothing);
                Assert.That(fixture.Flow.CommitCount, Is.EqualTo(1));
                Assert.That(healthySink.States.Count, Is.GreaterThan(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ScanObserverThatThrowsInitially_IsIsolatedBeforeScanPublishes()
        {
            var fixture = new Fixture();
            try
            {
                Assert.That(
                    () => fixture.Coordinator.ObserveScan(new AlwaysThrowingScanSink()),
                    Throws.Nothing);
                var now = DateTimeOffset.UtcNow;

                Assert.That(
                    () => fixture.Source.Emit(Observation(
                        Guid.NewGuid(),
                        1,
                        now,
                        "zone:entrance_001",
                        0.5f,
                        false)),
                    Throws.Nothing);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void ThrowingScanObserver_DoesNotBlockHealthyObserver()
        {
            var fixture = new Fixture();
            try
            {
                using var throwing = fixture.Coordinator.ObserveScan(new ThrowAfterInitialScanSink());
                var healthySink = new ScanSink();
                using var healthy = fixture.Coordinator.ObserveScan(healthySink);
                var now = DateTimeOffset.UtcNow;

                Assert.That(
                    () => fixture.Source.Emit(Observation(
                        Guid.NewGuid(),
                        1,
                        now,
                        "zone:entrance_001",
                        0.5f,
                        false)),
                    Throws.Nothing);
                Assert.That(healthySink.States.Count, Is.GreaterThan(1));
                Assert.That(healthySink.States[^1].Progress, Is.EqualTo(0.5f));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        static RecognitionObservation Observation(
            Guid id,
            long revision,
            DateTimeOffset observedAt,
            string payload,
            float progress = 1f,
            bool confirmed = true)
            => new RecognitionObservation(
                id,
                revision,
                observedAt,
                new SourceKind("qr"),
                payload,
                TrackingState.Tracked,
                confirmationProgress: progress,
                isConfirmation: confirmed);

        static RecognitionObservation LostObservation(
            Guid id,
            long revision,
            DateTimeOffset observedAt,
            string payload)
            => new RecognitionObservation(
                id,
                revision,
                observedAt,
                new SourceKind("qr"),
                payload,
                TrackingState.Lost,
                confirmationProgress: 0f,
                isConfirmation: false);

        [Test]
        public void FieldbookConfirmationUsesTheSameTransactionAndCanRetryAfterPrepareFailure()
        {
            using var fixture = new Fixture(true);
            fixture.Coordinator.SetNewRecognitionActivationAllowed(false);
            Assert.That(fixture.Coordinator.TryOpenConfirmedEntry("discovery:giant_saguaro"), Is.False);
            Assert.That(fixture.Host.PrepareCount, Is.Zero);
            fixture.Coordinator.SetNewRecognitionActivationAllowed(true);
            fixture.Host.RejectNextPrepare = true;
            Assert.That(fixture.Coordinator.TryOpenConfirmedEntry("discovery:giant_saguaro"), Is.False);
            Assert.That(fixture.Flow.CommitCount, Is.Zero);
            Assert.That(fixture.Coordinator.TryOpenConfirmedEntry("discovery:giant_saguaro"), Is.True);
            Assert.That(fixture.Flow.LastSceneId, Is.EqualTo(new SceneId("giant_saguaro")));
            Assert.That(fixture.Coordinator.TryOpenConfirmedEntry("discovery:giant_saguaro"), Is.False);
            Assert.That(fixture.Flow.CommitCount, Is.EqualTo(1));
            Assert.That(fixture.Flow.CloseCurrent(), Is.True);
            Assert.That(fixture.Flow.CommitCount, Is.EqualTo(1), "Close cannot automatically reopen the opportunity.");
            Assert.That(fixture.Coordinator.TryOpenConfirmedEntry("discovery:giant_saguaro"), Is.True);
            Assert.That(fixture.Flow.CommitCount, Is.EqualTo(2));
            Assert.That(fixture.Source.RearmCount, Is.Zero, "A fieldbook confirmation must not impersonate a QR.");
        }

        [Test]
        public void EachPublishedMapPointOpensItsOwnContentThroughTheActivationTransaction()
        {
            using var fixture = new Fixture(true);
            var entries = AssetDatabase.LoadAssetAtPath<ContentEntryCatalog>(RoutesPath);
            var scenes = new[] { "giant_saguaro", "baobab", "bottle_tree", "ceiba", "macrozamia", "welwitschia" };
            for (var i = 0; i < scenes.Length; i++)
            {
                Assert.That(entries.TryResolveMapPoint($"P{i + 1:00}", out var entry), Is.True);
                Assert.That(fixture.Coordinator.TryOpenConfirmedEntry(entry.EntryValue), Is.True);
                Assert.That(fixture.Flow.LastSceneId, Is.EqualTo(new SceneId(scenes[i])));
                Assert.That(fixture.Flow.CloseCurrent(), Is.True);
            }
            Assert.That(fixture.Flow.CommitCount, Is.EqualTo(6));
            Assert.That(fixture.Source.RearmCount, Is.Zero);
        }

        [Test]
        public void MapBindingsAreExplicitReorderableAndFailClosedWhenMissingOrAmbiguous()
        {
            var entries = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ContentEntryCatalog>(RoutesPath));
            try
            {
                var data = new SerializedObject(entries);
                var routes = data.FindProperty("_routes");
                routes.MoveArrayElement(routes.arraySize - 1, 0);
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(entries.TryResolveMapPoint("P01", out var first), Is.True);
                Assert.That(first.TargetSceneId, Is.EqualTo(new SceneId("giant_saguaro")));
                Assert.That(entries.TryResolveMapPoint("missing", out _), Is.False);
                // Move P06's content to P01: ambiguity must not pick whichever route happens to be first.
                routes.GetArrayElementAtIndex(0).FindPropertyRelative("_mapPointId").stringValue = "P01";
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(entries.TryResolveMapPoint("P01", out var ambiguous), Is.False);
                Assert.That(ambiguous, Is.Null);
                Assert.That(entries.TryResolveMapPoint("P06", out _), Is.False);
                for (var i = 1; i < routes.arraySize; i++)
                {
                    var route = routes.GetArrayElementAtIndex(i);
                    if (route.FindPropertyRelative("_mapPointId").stringValue == "P01")
                        route.FindPropertyRelative("_mapPointId").stringValue = "P06";
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(entries.TryResolveMapPoint("P01", out var swapped), Is.True);
                Assert.That(swapped.TargetSceneId, Is.EqualTo(new SceneId("welwitschia")));
                Assert.That(entries.TryResolveMapPoint("P06", out swapped), Is.True);
                Assert.That(swapped.TargetSceneId, Is.EqualTo(new SceneId("giant_saguaro")));
            }
            finally { UnityEngine.Object.DestroyImmediate(entries); }
        }

        sealed class Fixture : IDisposable
        {
            readonly ContentEntryCatalog _routes;
            public Fixture(bool fieldbook = false)
            {
                var routes = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ContentEntryCatalog>(RoutesPath));
                _routes = routes;
                var data = new SerializedObject(routes);
                var entries = data.FindProperty("_routes");
                for (var i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("_enabled").boolValue =
                        entry.FindPropertyRelative("_entryKind").stringValue == (fieldbook ? "fieldbook" : "qr");
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(routes, Is.Not.Null, $"Missing test configuration at {RoutesPath}.");
                Source = new RearmSource();
                Host = new FakeHost();
                Flow = new FakeFlow();
                Coordinator = new ActivationCoordinator(
                    new RouteResolver(routes),
                    Host,
                    Flow,
                    new RecognitionSourceRegistry(new[] { Source }),
                    TimeSpan.FromMilliseconds(350),
                    Thread.CurrentThread.ManagedThreadId,
                    JourneySessionId.CreateNew());
                Coordinator.Start();
                Scan = new ScanSink();
                Coordinator.ObserveScan(Scan);
            }

            public RearmSource Source { get; }
            public FakeHost Host { get; }
            public FakeFlow Flow { get; }
            public ActivationCoordinator Coordinator { get; }
            public ScanSink Scan { get; }

            public void Dispose() { Coordinator.Dispose(); UnityEngine.Object.DestroyImmediate(_routes); }
        }

        sealed class RearmSource : IRecognitionSource, IRecognitionRoundRearm
        {
            IRecognitionObservationSink _sink;

            public SourceKind Kind => new SourceKind("qr");
            public int RearmCount { get; private set; }
            public RecognitionObservation LastRearm { get; private set; }

            public IDisposable Start(IRecognitionObservationSink sink)
            {
                _sink = sink;
                return new ActionDisposable(() => _sink = null);
            }

            public bool TryRearm(RecognitionObservation observation)
            {
                RearmCount++;
                LastRearm = observation;
                return true;
            }

            public void Emit(RecognitionObservation observation)
                => _sink.Publish(observation);
        }

        sealed class FakeHost : ISpatialDisplayHost
        {
            public int PrepareCount { get; private set; }
            public int CommitCount { get; private set; }
            public int CloseCount { get; private set; }
            public int TrackingUpdateCount { get; private set; }
            public bool CloseOnLost { get; set; }
            public bool RejectNextPrepare { get; set; }
            public bool ThrowOnNextCommit { get; set; }
            public SessionToken LastClosedSession { get; private set; }
            public SessionToken CurrentSession { get; private set; }
            bool _lost;

            public HostPrepareResult Prepare(
                SessionToken candidate,
                DisplayProfile profile,
                SpatialEvidence? spatialEvidence)
            {
                PrepareCount++;
                if (!RejectNextPrepare)
                    return HostPrepareResult.Success(new HostLease(this, candidate));

                RejectNextPrepare = false;
                return HostPrepareResult.Reject(HostFailure.EnvironmentUnavailable);
            }

            public HostResult Close(SessionToken session)
            {
                if (!session.IsValid || session != CurrentSession)
                    return HostResult.Reject(HostFailure.StaleSession);
                CloseCount++;
                LastClosedSession = session;
                CurrentSession = default;
                return HostResult.Success;
            }

            public HostResult UpdateSourceTracking(
                SessionToken session,
                TrackingState trackingState,
                DateTimeOffset observedAt)
            {
                TrackingUpdateCount++;
                _lost = trackingState == TrackingState.Lost;
                return HostResult.Success;
            }

            public HostSourcePolicyResult Tick(SessionToken session, DateTimeOffset now)
            {
                if (!CloseOnLost || !_lost)
                    return HostSourcePolicyResult.NoChange;

                _lost = false;
                return HostSourcePolicyResult.Changed(HostSourcePolicyAction.CloseRequested);
            }

            public void Commit(SessionToken session)
            {
                if (ThrowOnNextCommit)
                {
                    ThrowOnNextCommit = false;
                    throw new InvalidOperationException("Injected host commit failure.");
                }

                CommitCount++;
                CurrentSession = session;
            }
        }

        sealed class ScanSink : IScanFeedbackSink
        {
            public List<ScanFeedbackState> States { get; } = new List<ScanFeedbackState>();
            public void OnScanFeedbackChanged(ScanFeedbackState state) => States.Add(state);
        }

        sealed class ContentSink : IContentLifecycleSink
        {
            public List<ContentOpenedFact> Facts { get; } = new List<ContentOpenedFact>();
            public void OnContentOpened(ContentOpenedFact fact) => Facts.Add(fact);
            public void OnContentClosed(ContentClosedFact fact) { }
        }

        sealed class RecallSink : IRecallStateSink
        {
            public List<RecallState> States { get; } = new List<RecallState>();
            public void OnStateChanged(RecallState state) => States.Add(state);
        }

        sealed class AlwaysThrowingRecallSink : IRecallStateSink
        {
            public void OnStateChanged(RecallState state)
                => throw new InvalidOperationException("Injected initial recall observer failure.");
        }

        sealed class ThrowAfterInitialRecallSink : IRecallStateSink
        {
            int _publishCount;

            public void OnStateChanged(RecallState state)
            {
                if (++_publishCount > 1)
                    throw new InvalidOperationException("Injected recall observer failure.");
            }
        }

        sealed class AlwaysThrowingScanSink : IScanFeedbackSink
        {
            public void OnScanFeedbackChanged(ScanFeedbackState state)
                => throw new InvalidOperationException("Injected initial scan observer failure.");
        }

        sealed class ThrowAfterInitialScanSink : IScanFeedbackSink
        {
            int _publishCount;

            public void OnScanFeedbackChanged(ScanFeedbackState state)
            {
                if (++_publishCount > 1)
                    throw new InvalidOperationException("Injected scan observer failure.");
            }
        }

        sealed class HostLease : IPreparedHostLease
        {
            readonly FakeHost _owner;

            public HostLease(FakeHost owner, SessionToken session)
            {
                _owner = owner;
                Session = session;
            }

            public SessionToken Session { get; }
            public void Commit() => _owner.Commit(Session);
            public void Dispose() { }
        }

        sealed class FakeFlow : IExperienceFlow
        {
            IFlowStateSink _sink;
            SessionToken _session;
            long _version;

            public int CommitCount { get; private set; }
            public int CloseCount { get; private set; }
            public int CloseAttemptCount { get; private set; }
            public bool RejectNextClose { get; set; }
            public bool ThrowOnNextCommit { get; set; }
            public SessionToken CurrentSession => _session;
            public SceneId LastSceneId { get; private set; }

            public FlowPrepareResult Prepare(SessionToken candidate, SceneId sceneId)
                => FlowPrepareResult.Success(new FlowLease(this, candidate, sceneId));

            public FlowResult EnterFeature(SessionToken session, FeaturePageId page)
                => FlowResult.Reject(FlowFailure.PageUnavailable);

            public FlowResult BackToMain(SessionToken session)
                => FlowResult.Reject(FlowFailure.InvalidTransition);

            public FlowResult Close(SessionToken session)
            {
                if (!_session.IsValid || session != _session)
                    return FlowResult.Reject(FlowFailure.StaleSession);
                CloseAttemptCount++;
                if (RejectNextClose)
                {
                    RejectNextClose = false;
                    return FlowResult.Reject(FlowFailure.LifecycleFailed);
                }

                CloseCount++;
                _session = default;
                Publish(FlowPage.Closed, default);
                return FlowResult.Success;
            }

            public bool CloseCurrent()
                => _session.IsValid && Close(_session).Succeeded;

            public IDisposable Observe(IFlowStateSink sink)
            {
                _sink = sink;
                Publish(FlowPage.Closed, default);
                return new ActionDisposable(() => _sink = null);
            }

            void Commit(SessionToken session, SceneId sceneId)
            {
                if (ThrowOnNextCommit)
                {
                    ThrowOnNextCommit = false;
                    throw new InvalidOperationException("Injected flow commit failure.");
                }

                _session = session;
                LastSceneId = sceneId;
                CommitCount++;
                Publish(FlowPage.Main, sceneId);
            }

            void Publish(FlowPage page, SceneId sceneId)
                => _sink?.OnStateChanged(new ExperienceFlowState(
                    _session,
                    _version++,
                    sceneId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    page,
                    Array.Empty<FeaturePageId>()));

            sealed class FlowLease : IPreparedFlowLease
            {
                readonly FakeFlow _owner;
                readonly SceneId _sceneId;

                public FlowLease(FakeFlow owner, SessionToken session, SceneId sceneId)
                {
                    _owner = owner;
                    Session = session;
                    _sceneId = sceneId;
                }

                public SessionToken Session { get; }
                public void Commit() => _owner.Commit(Session, _sceneId);
                public void Dispose() { }
            }
        }

        sealed class ActionDisposable : IDisposable
        {
            Action _dispose;
            public ActionDisposable(Action dispose) => _dispose = dispose;
            public void Dispose()
            {
                var dispose = _dispose;
                _dispose = null;
                dispose?.Invoke();
            }
        }
    }
}
