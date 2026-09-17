using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Narration.Backend;
using BotanicalGardenQR.Narration.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class NarrationControllerLifecycleTests
    {
        readonly List<UnityEngine.Object> _ownedObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _ownedObjects.Count - 1; index >= 0; index--)
            {
                if (_ownedObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(_ownedObjects[index]);
            }
            _ownedObjects.Clear();
        }

        [Test]
        public void ValidationFailureLeavesControllerRetryableAndCloseReleasesEachSurfaceOnce()
        {
            var controller = CreateController();
            var sink = new RecordingSink();
            var invalidReleases = 0;
            var firstReleases = 0;
            var secondReleases = 0;

            using (controller.Observe(sink))
            {
                var invalidSurface = CreateSurface("InvalidNarrationSurface", () => invalidReleases++);
                var invalid = controller.Open(default, CreateDefinition("InvalidSessionClip"), invalidSurface);

                Assert.That(invalid.Succeeded, Is.False);
                Assert.That(invalid.FailureCode, Is.EqualTo(NarrationFailureCode.StaleSession));
                Assert.That(invalidSurface.IsDisposed, Is.False,
                    "A rejected Open must not consume a lease it never accepted.");
                invalidSurface.Dispose();
                Assert.That(invalidReleases, Is.EqualTo(1));

                var firstSession = SessionToken.CreateNew();
                var firstSurface = CreateSurface("FirstNarrationSurface", () => firstReleases++);
                Assert.That(controller.Open(firstSession, CreateDefinition("FirstNarrationClip"), firstSurface).Succeeded,
                    Is.True);
                Assert.That(sink.Latest.Phase, Is.EqualTo(NarrationPhase.Ready));

                Assert.That(controller.Dispatch(firstSession,
                    new NarrationIntent(NarrationIntentKind.ToggleMute)).Succeeded, Is.True);
                Assert.That(sink.Latest.IsMuted, Is.True);

                var staleSession = SessionToken.CreateNew();
                Assert.That(controller.Dispatch(staleSession,
                    new NarrationIntent(NarrationIntentKind.Replay)).FailureCode,
                    Is.EqualTo(NarrationFailureCode.StaleSession));
                Assert.That(controller.Close(staleSession).FailureCode,
                    Is.EqualTo(NarrationFailureCode.StaleSession));
                Assert.That(firstReleases, Is.Zero);

                ExpectEditModeDestroyError();
                Assert.That(controller.Close(firstSession).Succeeded, Is.True);
                Assert.That(firstReleases, Is.EqualTo(1));
                Assert.That(firstSurface.IsDisposed, Is.True);
                Assert.That(sink.Latest.Phase, Is.EqualTo(NarrationPhase.Closed));
                Assert.That(controller.Close(firstSession).Succeeded, Is.True);
                Assert.That(firstReleases, Is.EqualTo(1));

                var secondSession = SessionToken.CreateNew();
                var secondSurface = CreateSurface("SecondNarrationSurface", () => secondReleases++);
                Assert.That(controller.Open(secondSession, CreateDefinition("SecondNarrationClip"), secondSurface).Succeeded,
                    Is.True, "A completed Close must leave the controller reusable.");
                Assert.That(sink.Latest.Session, Is.EqualTo(secondSession));
                Assert.That(sink.Latest.Phase, Is.EqualTo(NarrationPhase.Ready));

                ExpectEditModeDestroyError();
                Assert.That(controller.Close(secondSession).Succeeded, Is.True);
                Assert.That(secondReleases, Is.EqualTo(1));
                Assert.That(secondSurface.IsDisposed, Is.True);
            }
        }

        [Test]
        public void DestroyWhileOpenReleasesAcceptedSurfaceExactlyOnce()
        {
            var controller = CreateController();
            var releases = 0;
            var session = SessionToken.CreateNew();
            var surface = CreateSurface("DestroyedNarrationSurface", () => releases++);

            Assert.That(controller.Open(session, CreateDefinition("DestroyedNarrationClip"), surface).Succeeded, Is.True);
            ExpectEditModeDestroyError();
            InvokeOnDestroy(controller);
            InvokeOnDestroy(controller);

            Assert.That(releases, Is.EqualTo(1));
            Assert.That(surface.IsDisposed, Is.True);
            surface.Dispose();
            Assert.That(releases, Is.EqualTo(1));
        }

        [Test]
        public void PageEnterPolicyStartsPlaybackWithoutVisitorCommand()
        {
            var controller = CreateController();
            var sink = new RecordingSink();
            var session = SessionToken.CreateNew();
            var surface = CreateSurface("PageEnterNarrationSurface", () => { });

            using (controller.Observe(sink))
            {
                Assert.That(
                    controller.Open(
                        session,
                        CreateDefinition("PageEnterNarrationClip", NarrationStartPolicy.OnPageEnter),
                        surface).Succeeded,
                    Is.True);
                Assert.That(sink.Latest.Phase, Is.EqualTo(NarrationPhase.Playing));

                Assert.That(
                    controller.Dispatch(session, new NarrationIntent(NarrationIntentKind.TogglePlayback)).Succeeded,
                    Is.True);
                Assert.That(sink.Latest.Phase, Is.EqualTo(NarrationPhase.Paused));

                ExpectEditModeDestroyError();
                Assert.That(controller.Close(session).Succeeded, Is.True);
            }
        }

        INarrationController CreateController()
            => NarrationModuleFactory.Create(OwnGameObject("NarrationControllerOwner").transform);

        NarrationDefinition CreateDefinition(
            string clipName,
            NarrationStartPolicy startPolicy = NarrationStartPolicy.OnVisitorCommand)
        {
            var clip = AudioClip.Create(clipName, 4410, 1, 44100, false);
            _ownedObjects.Add(clip);
            return new NarrationDefinition(clip, startPolicy);
        }

        NarrationSurfaceLease CreateSurface(string name, Action release)
            => new NarrationSurfaceLease(OwnGameObject(name).transform, Vector2.one, true, release);

        GameObject OwnGameObject(string name)
        {
            var gameObject = new GameObject(name);
            _ownedObjects.Add(gameObject);
            return gameObject;
        }

        static void ExpectEditModeDestroyError()
            => LogAssert.Expect(
                LogType.Error,
                new Regex("Destroy may not be called from edit mode! Use DestroyImmediate instead\\."));

        static void InvokeOnDestroy(INarrationController controller)
        {
            var method = controller.GetType().GetMethod(
                "OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, null);
        }

        sealed class RecordingSink : INarrationStateSink
        {
            public List<NarrationState> States { get; } = new List<NarrationState>();
            public NarrationState Latest => States[States.Count - 1];
            public void Publish(NarrationState state) => States.Add(state);
        }
    }
}
