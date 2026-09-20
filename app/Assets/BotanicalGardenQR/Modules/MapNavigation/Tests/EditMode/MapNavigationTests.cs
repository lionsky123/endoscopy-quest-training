using System;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.MapNavigation.Runtime;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.FrontendShell.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.MapNavigation.Tests
{
    public sealed class MapNavigationTests
    {
        static MapDefinition Definition() => new MapDefinition
        {
            mapId = "test",
            scale = 1,
            points = new[]
            {
                new MapPoint
                {
                    id = "first",

                    position = new MapPosition(2, 0, 2)
                },
                new MapPoint
                {
                    id = "second",

                    position = new MapPosition(4, 0, 2)
                }
            },
            routes = new[]
            {
                new MapRoute
                {
                    from = "Start",
                    to = "first",
                    samples = new[]
                    {
                        new MapPosition(0, 0, 0),
                        new MapPosition(0, 0, 2),
                        new MapPosition(2, 0, 2)
                    }
                },
                new MapRoute
                {
                    from = "first",
                    to = "second",
                    samples = new[]
                    {
                        new MapPosition(2, 0, 2),
                        new MapPosition(4, 0, 2)
                    }
                }
            }
        };
        static MapNavigationController Ready(Motion sink = null)
        {
            var n = new MapNavigationController(Definition(), sink ?? new Motion());
            for (int i = 0; i < 12; i++)
                n.TryInitialize(new MapPosition(0, 1.6f, 0), 0, 0, .02f);
            return n;
        }

        [Test]
        public void VirtualRoomFrameIsReadyImmediatelyAndDoesNotFollowHeadTurns()
        {
            var frame = new MapFrame(new MapPosition(6, 0, -4), 90, 1);
            using var n = new MapNavigationController(Definition(), new Motion(), frame);
            Assert.That(n.HasFrame, Is.True);
            Assert.That(n.TryInitialize(new MapPosition(8, 1.7f, 3), -125, .4f, .02f), Is.True);
            Assert.That(n.Frame.Origin.x, Is.EqualTo(6));
            Assert.That(n.Frame.YawDegrees, Is.EqualTo(90));
            Assert.That(n.Begin("first"), Is.True);
            var expected = frame.Transform(Definition().points[0].position);
            Assert.That(MapPosition.Distance(n.CurrentWorldPath[n.CurrentWorldPath.Count-1], expected), Is.LessThan(.001f));
        }

        [Test]
        public void NearbyVisitorMayWalkBesideTheFairyInsteadOfOnItsRoute()
        {
            using var n = Ready();
            n.Begin("first");
            n.Tick(default, true, false, .02f);
            var route = new MapRouteGeometry(n.CurrentWorldPath);
            for (int i = 0; i < 500; i++)
            {
                var p = route.Sample(n.State.Progress);
                n.Tick(new MapPosition(p.x + 2, 1.6f, p.z), true, false, .02f);
            }
            Assert.That(n.State.Phase, Is.EqualTo(MapNavigationPhase.Arrived));
        }

        [Test]
        public void CompletionUnlocksButtonButOnlyExplicitAdvanceCreatesOneNextLeg()
        {
            using var n = Ready();
            using var app = new VisitorGuidanceCoordinator(n, () => { });
            app.Begin(); app.Refresh(0, default, true, false);
            var route = new MapRouteGeometry(n.CurrentWorldPath);
            for (int i = 0; i < 500; i++) n.Tick(route.Sample(n.State.Progress), true, false, .02f);
            for (var stable = 0; stable < 4; stable++) app.TrackVisitorArrival(route.Sample(route.Length), true, .1f);
            app.TargetContentOpened();
            var first = n.State.RequestId;
            app.Refresh(1, new VisitorGuidanceEnvironment(false, false, true, false, false, false), true, false);
            app.HandleIntent(VisitorDialogueIntentKind.Advance);
            Assert.That(n.State.RequestId, Is.EqualTo(first));
            app.Refresh(1, default, true, false);
            Assert.That(app.Surface.Kind, Is.EqualTo(VisitorGuidanceSurfaceKind.Departure));
            Assert.That(n.State.RequestId, Is.EqualTo(first));
            app.HandleIntent(VisitorDialogueIntentKind.Advance);
            var second = n.State.RequestId;
            Assert.That(n.PointForTarget, Is.EqualTo("second"));
            Assert.That(n.State.Phase, Is.EqualTo(MapNavigationPhase.WaitingForVisitor));
            app.HandleIntent(VisitorDialogueIntentKind.Advance);
            app.HandleIntent(VisitorDialogueIntentKind.Dismiss);
            app.Refresh(2, default, true, false);
            Assert.That(n.State.RequestId, Is.EqualTo(second));
        }

        [Test]
        public void FrameCommitsOnceAndAppliesScaleAndYawOnce()
        {
            var d = Definition();
            d.scale = 2;
            using var n = new MapNavigationController(d, new Motion());
            for (int i = 0; i < 12; i++)
                n.TryInitialize(new MapPosition(3, 1.6f, 5), 90, .4f, .02f);
            Assert.That(n.HasFrame, Is.True);
            var p = n.Frame.Transform(new MapPosition(1, 0, 0));
            Assert.That(p.x, Is.EqualTo(3).Within(.001));
            Assert.That(p.y, Is.EqualTo(.4).Within(.001));
            Assert.That(p.z, Is.EqualTo(3).Within(.001));
            n.TryInitialize(new MapPosition(10, 2, 10), 0, 9, 1);
            Assert.That(n.Frame.Origin.x, Is.EqualTo(3));
        }

        [Test]
        public void RejectedMotionDoesNotAdvanceOrClaimArrival()
        {
            using var n = Ready(new Motion { Accept = false });
            n.Begin("first");
            for (int i = 0; i < 100; i++)
                n.Tick(new MapPosition(), true, false, .02f);
            Assert.That(n.State.Progress, Is.Zero);
            Assert.That(n.State.Phase, Is.EqualTo(MapNavigationPhase.Paused));
        }

        [Test]
        public void StationaryVisitorCannotBeLeftAtStart()
        {
            var motion = new Motion();
            using var n = Ready(motion);
            n.Begin("first");
            for (int i = 0; i < 400; i++)
                n.Tick(new MapPosition(), true, false, .02f);
            Assert.That(MapPosition.Distance(default, motion.Last), Is.LessThanOrEqualTo(2.515f));
            Assert.That(n.State.Phase, Is.Not.EqualTo(MapNavigationPhase.Arrived));
        }

        [Test]
        public void RouteFollowsCornersAndFairyCompletesTheLeg()
        {
            var motion = new Motion();
            using var n = Ready(motion);
            n.Begin("first");
            var geometry = new MapRouteGeometry(n.CurrentWorldPath);
            for (int i = 0; i < 500; i++)
            {
                var viewer = geometry.Sample(Math.Max(0, n.State.Progress - .3f));
                n.Tick(viewer, true, false, .02f);
                var p = motion.Last;
                Assert.That(p.x < .001f || Math.Abs(p.z - 2) < .001f, Is.True, "Motion cut across the authored corner.");
            }

            Assert.That(n.State.Phase, Is.EqualTo(MapNavigationPhase.Arrived));
        }

        [Test]
        public void TrackingLossAndModalPauseFreezeProgress()
        {
            using var n = Ready();
            n.Begin("first");
            n.Tick(new MapPosition(), true, false, .1f);
            var progress = n.State.Progress;
            n.Tick(new MapPosition(2, 0, 2), false, false, 10);
            n.Tick(new MapPosition(), true, true, 10);
            Assert.That(n.State.Progress, Is.EqualTo(progress));
            Assert.That(n.State.Phase, Is.EqualTo(MapNavigationPhase.Paused));
        }

        [Test]
        public void FarAheadPositionDoesNotSkipAPathSection()
        {
            using var n = Ready();
            n.Begin("first");
            n.Tick(new MapPosition(2, 0, 2), true, false, .1f);
            Assert.That(n.State.Progress, Is.Zero);
            Assert.That(n.State.Phase, Is.EqualTo(MapNavigationPhase.WaitingForVisitor));
        }

        [Test]
        public void NewTaskHasNewIdentityAndCancelCannotFinish()
        {
            using var n = Ready();
            n.Begin("first");
            var first = n.State.RequestId;
            n.Begin("second");
            Assert.That(n.State.RequestId, Is.GreaterThan(first));
            n.Cancel();
            n.Tick(new MapPosition(4, 0, 2), true, false, 1);
            Assert.That(n.State.Phase, Is.EqualTo(MapNavigationPhase.Cancelled));
        }

        [Test]
        public void BrokenEndpointAndDuplicateMappingAreRejected()
        {
            var d = Definition();
            d.routes[0].samples[0] = new MapPosition(1, 0, 0);
            Assert.Throws<ArgumentException>(() => MapDefinitionValidation.Validate(d));
            d = Definition();
            d.points[1].id = "first";
            Assert.Throws<ArgumentException>(() => MapDefinitionValidation.Validate(d));
        }

        [Test]
        public void FirstScanWaitsForArrivalAndExplicitExitOpensItExactlyOnce()
        {
            using var n = Ready();
            var starts = 0;
            using var app = new VisitorGuidanceCoordinator(n, () => starts++);
            app.Begin();
            app.Refresh(0, default, true, false);
            Assert.That(app.BlocksFirstScan, Is.True);
            Assert.That(starts, Is.Zero);
            app.End();
            app.End();
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(n.State.Phase, Is.EqualTo(MapNavigationPhase.Cancelled));
        }

        [Test]
        public void DepartureDoesNotRequireAnInitialApproachButStillWaitsWhenTooFar()
        {
            var motion = new Motion();
            using var n = Ready(motion);
            n.Begin("first");
            n.Tick(new MapPosition(1.25f, 0, 0), true, false, .1f);
            Assert.That(n.State.Progress, Is.GreaterThan(0));
            n.Tick(new MapPosition(.5f, 0, 0), true, false, .1f);
            Assert.That(n.State.Progress, Is.GreaterThan(0));
            n.Tick(new MapPosition(1.4f, 0, 0), true, false, .1f);
            Assert.That(n.State.Phase, Is.EqualTo(MapNavigationPhase.Moving));
            n.Tick(new MapPosition(3, 0, 0), true, false, .1f);
            Assert.That(n.State.Phase, Is.EqualTo(MapNavigationPhase.WaitingForVisitor));
            var progress = n.State.Progress;
            n.Tick(new MapPosition(1.5f, 0, 0), true, false, .1f);
            Assert.That(n.State.Progress, Is.GreaterThan(progress));
        }

        [Test]
        public void ReacquiredTrackingRejectsAChangedCoordinateFrame()
        {
            using var n = Ready();
            n.Begin("first");
            n.Tick(default, true, false, .1f);
            var progress = n.State.Progress;
            n.Tick(default, false, false, .1f);
            n.Tick(new MapPosition(8, 0, 8), true, false, .1f);
            Assert.That(n.State.Phase, Is.EqualTo(MapNavigationPhase.Unavailable));
            Assert.That(n.State.Progress, Is.EqualTo(progress));
        }

        [Test]
        public void PauseBeforeMotionAcquisitionNeverAppliesARequestedPosition()
        {
            var motion = new Motion();
            using var n = Ready(motion);
            n.Begin("first");
            var applies = motion.Applies;
            n.Tick(default, false, false, .1f);
            n.Tick(default, true, true, .1f);
            Assert.That(motion.Applies, Is.EqualTo(applies));
        }

        [Test]
        public void ApplicationPausesForEachBlockingSurfaceAndDoesNotDepartForPendingReward()
        {
            using var n = Ready();
            using var app = new VisitorGuidanceCoordinator(n, () =>
            {
            });
            app.Begin();
            app.Refresh(0, new VisitorGuidanceEnvironment(false, false, false, true, false, false), true, false);
            Assert.That(app.IsPaused, Is.True);
            Assert.That(app.ShowRoute, Is.False);
            Assert.That(app.BlocksFirstScan, Is.True);
            app.Refresh(0, default(VisitorGuidanceEnvironment), true, false);
            Assert.That(app.IsPaused, Is.False);
            Assert.That(app.ShowRoute, Is.True);
        }



        [Test]
        public void ProjectionAtACrossingStaysInsideTheCurrentProgressWindow()
        {
            var geometry = new MapRouteGeometry(new[] { new MapPosition(-2, 0, -2), new MapPosition(2, 0, 2), new MapPosition(-2, 0, 2), new MapPosition(2, 0, -2) });
            var progress = geometry.Project(default, 1.8f, 3f, out var distance);
            Assert.That(progress, Is.InRange(1.8f, 3f));
            Assert.That(distance, Is.LessThan(.001f));
        }

        [Test]
        public void CallerCannotRewriteTheActiveMapDefinitionOrPath()
        {
            var definition = Definition();
            using var navigation = new MapNavigationController(definition, new Motion());
            definition.points[0].id = "mutated";
            definition.routes[0].samples[1] = new MapPosition(99, 0, 99);
            for (int i = 0; i < 12; i++)
                navigation.TryInitialize(default, 0, 0, .02f);
            Assert.That(navigation.Begin("first"), Is.True);
            Assert.That(navigation.CurrentWorldPath[1].z, Is.EqualTo(2));
            Assert.That(navigation.CurrentWorldPath, Is.Not.InstanceOf<MapPosition[]>());
        }

        [Test]
        public void NearbyFairyJoinsForwardWithoutReturningToTheStart()
        {
            var motion = new Motion { Last = new MapPosition(.2f, 0, 1) };
            using var navigation = Ready(motion);
            navigation.Begin("first");
            navigation.Tick(new MapPosition(0, 0, 1), true, false, .1f);
            Assert.That(navigation.State.Progress, Is.InRange(1f, 1.1f));
            Assert.That(motion.Last.z, Is.GreaterThanOrEqualTo(1f));
            Assert.That(motion.Last.x, Is.Zero);
        }

        [Test]
        public void ReentryWindowStopsAtAnAuthoredCorner()
        {
            var geometry = new MapRouteGeometry(Definition().routes[0].samples);
            Assert.That(geometry.ForwardEntryLimit(1f, 3f), Is.EqualTo(2f));
            var progress = geometry.Project(new MapPosition(1, 0, 2), 1f,
                geometry.ForwardEntryLimit(1f, 3f), out _);
            Assert.That(progress, Is.EqualTo(2f));
        }

        [Test]
        public void VisitorArrivalLatchesWithoutFairyAndRequiresExplicitConfirmation()
        {
            using var navigation = Ready(new Motion { Accept = false });
            var opens = 0;
            using var app = new VisitorGuidanceCoordinator(navigation, () => { }, _ => { opens++; return true; });
            app.Begin(); app.Refresh(0, default, true, false);
            app.TrackVisitorArrival(new MapPosition(2, 0, 2), false, 1);
            Assert.That(app.HasDiscovery, Is.False);
            for (var i = 0; i < 4; i++) app.TrackVisitorArrival(new MapPosition(2, 0, 2), true, .1f);
            Assert.That(app.HasDiscovery, Is.True);
            Assert.That(opens, Is.Zero);
            app.TrackVisitorArrival(default, true, .1f);
            Assert.That(app.HasDiscovery, Is.True, "Arrival opportunity survives leaving its edge.");
            app.HandleIntent(VisitorDialogueIntentKind.Dismiss);
            Assert.That(app.Surface.Kind, Is.EqualTo(VisitorGuidanceSurfaceKind.Discovery), "A stale dismiss action must not hide the station entry.");
            app.HandleIntent(VisitorDialogueIntentKind.Advance);
            Assert.That(opens, Is.EqualTo(1));
            app.TargetContentOpened();
            app.HandleIntent(VisitorDialogueIntentKind.Advance);
            Assert.That(opens, Is.EqualTo(1));
        }

        [Test]
        public void FailedDiscoveryCanRetryAndBlockingReadingDoesNotOpenIt()
        {
            using var navigation = Ready();
            var opens = 0;
            using var app = new VisitorGuidanceCoordinator(navigation, () => { }, _ => { opens++; return false; });
            app.Begin(); app.Refresh(0, default, true, false);
            for (var i = 0; i < 4; i++) app.TrackVisitorArrival(new MapPosition(2, 0, 2), true, .1f);
            app.Refresh(0, new VisitorGuidanceEnvironment(true, false, false, false, false, false), true, false);
            app.HandleIntent(VisitorDialogueIntentKind.Advance);
            Assert.That(opens, Is.Zero);
            app.Refresh(0, default, true, false);
            app.HandleIntent(VisitorDialogueIntentKind.Advance);
            Assert.That(app.Surface.Kind, Is.EqualTo(VisitorGuidanceSurfaceKind.DiscoveryFailed));
            app.HandleIntent(VisitorDialogueIntentKind.Dismiss);
            Assert.That(app.Surface.Kind, Is.EqualTo(VisitorGuidanceSurfaceKind.DiscoveryFailed));
            app.HandleIntent(VisitorDialogueIntentKind.Advance);
            Assert.That(opens, Is.EqualTo(2));
        }

        [Test]
        public void RecoveryPreservesTaskAndFrameAndRejectsFarAwayCandidates()
        {
            using var navigation = Ready(); navigation.Begin("first");
            navigation.Tick(default, true, false, .1f);
            var state = navigation.State; var frame = navigation.Frame;
            Assert.That(navigation.TryGetRecoveryPosition(new MapPosition(20, 0, 20), out _), Is.False);
            Assert.That(navigation.TryGetRecoveryPosition(new MapPosition(.2f, 0, .6f), out var target), Is.True);
            Assert.That(target.x, Is.Zero);
            Assert.That(target.z, Is.GreaterThanOrEqualTo(state.Progress));
            navigation.ReacquireMotion();
            Assert.That(navigation.State.RequestId, Is.EqualTo(state.RequestId));
            Assert.That(navigation.State.Progress, Is.EqualTo(state.Progress));
            Assert.That(navigation.Frame.Origin, Is.EqualTo(frame.Origin));
        }

        [Test]
        public void ProjectionCostIsMeasuredOnThePublishedSamplingScale()
        {
            var points = new MapPosition[65];
            for (var i = 0; i < points.Length; i++) points[i] = new MapPosition(0, 0, i * .05f);
            var geometry = new MapRouteGeometry(points);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var result = 0f;
            for (var i = 0; i < 10000; i++) result = geometry.Project(new MapPosition(.2f, 0, 1), .2f, 1.5f, out _);
            watch.Stop();
            Assert.That(result, Is.EqualTo(1f).Within(.0001f));
            TestContext.WriteLine($"Projection: 10000 calls, 65 samples, {watch.Elapsed.TotalMilliseconds:F3} ms total on this Editor host; not Quest frame timing.");
        }

        sealed class Motion : IMapMotionSink
        {
            public int Applies;
            public bool Accept = true;
            public MapPosition Last;
            public bool TryGetPosition(out MapPosition p)
            {
                p = Last;
                return true;
            }

            public bool Apply(long id, MapPosition p, MapPosition f, bool moving)
            {
                Applies++;
                if (Accept)
                    Last = p;
                return Accept;
            }

            public void Hold(long id)
            {
            }

            public void Release(long id)
            {
            }
        }
    }
}
