using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Model.Backend;
using BotanicalGardenQR.Model.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ModelControllerLifecycleTests
    {
        readonly List<GameObject> _ownedObjects = new List<GameObject>();

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

        [UnityTest]
        public IEnumerator CloseDuringLoadRejectsLateResultAndDisposesItExactlyOnce()
        {
            var loader = new ControlledLoader();
            var driver = new RecordingDriver();
            var controller = CreateController(new[] { loader }, new[] { driver });
            var sink = new RecordingSink();
            var session = SessionToken.CreateNew();
            var releases = 0;
            var surface = CreateSurface("CloseSurface", () => releases++);

            using (controller.Observe(sink))
            {
                Assert.That(controller.Open(session, CreateDefinition(), surface).Succeeded, Is.True);
                Assert.That(sink.Latest.Phase, Is.EqualTo(ModelPhase.Loading));
                ExpectEditModeDestroyErrors(1);
                Assert.That(controller.Close(session).Succeeded, Is.True);
                var closedVersion = sink.Latest.Version;
                Assert.That(loader.Token.IsCancellationRequested, Is.True);
                Assert.That(releases, Is.EqualTo(1));

                var resource = new RecordingDisposable();
                var lateInstance = Own("LateModel");
                ExpectEditModeDestroyErrors(1);
                loader.Succeed(lateInstance, resource);
                yield return null;

                Assert.That(resource.DisposeCount, Is.EqualTo(1));
                Assert.That(sink.Latest.Phase, Is.EqualTo(ModelPhase.Closed));
                Assert.That(sink.States.Any(state => state.Version > closedVersion &&
                    (state.Phase == ModelPhase.Ready || state.Phase == ModelPhase.Failed)), Is.False);

                Assert.That(controller.Close(session).Succeeded, Is.True);
                Assert.That(resource.DisposeCount, Is.EqualTo(1));
                Assert.That(releases, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator RapidReopenKeepsNewSessionAuthoritativeAndReleasesBothSessionsOnce()
        {
            var firstLoader = new ControlledLoader();
            var secondLoader = new ControlledLoader();
            var firstDriver = new RecordingDriver();
            var secondDriver = new RecordingDriver();
            var controller = CreateController(
                new[] { firstLoader, secondLoader },
                new[] { firstDriver, secondDriver });
            var sink = new RecordingSink();
            var firstSession = SessionToken.CreateNew();
            var secondSession = SessionToken.CreateNew();
            var firstSurfaceReleases = 0;
            var secondSurfaceReleases = 0;

            using (controller.Observe(sink))
            {
                Assert.That(controller.Open(firstSession, CreateDefinition(),
                    CreateSurface("FirstSurface", () => firstSurfaceReleases++)).Succeeded, Is.True);
                ExpectEditModeDestroyErrors(1);
                Assert.That(controller.Close(firstSession).Succeeded, Is.True);
                Assert.That(controller.Open(secondSession, CreateDefinition(),
                    CreateSurface("SecondSurface", () => secondSurfaceReleases++)).Succeeded, Is.True);

                var oldResource = new RecordingDisposable();
                ExpectEditModeDestroyErrors(1);
                firstLoader.Succeed(Own("OldModel"), oldResource);
                yield return null;

                Assert.That(oldResource.DisposeCount, Is.EqualTo(1));
                Assert.That(sink.Latest.Session, Is.EqualTo(secondSession));
                Assert.That(sink.Latest.Phase, Is.EqualTo(ModelPhase.Loading));

                var currentResource = new RecordingDisposable();
                secondLoader.Succeed(Own("CurrentModel"), currentResource);
                yield return null;

                Assert.That(sink.Latest.Session, Is.EqualTo(secondSession));
                Assert.That(sink.Latest.Phase, Is.EqualTo(ModelPhase.Ready));
                Assert.That(currentResource.DisposeCount, Is.Zero);
                Assert.That(firstDriver.DisposeCount, Is.EqualTo(1));

                ExpectEditModeDestroyErrors(2);
                Assert.That(controller.Close(secondSession).Succeeded, Is.True);
                yield return null;
                Assert.That(currentResource.DisposeCount, Is.EqualTo(1));
                Assert.That(secondDriver.DisposeCount, Is.EqualTo(1));
                Assert.That(firstSurfaceReleases, Is.EqualTo(1));
                Assert.That(secondSurfaceReleases, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator DestroyDuringLoadCancelsAndRemovesOwnedRootBeforeRejectingLateResult()
        {
            var loader = new ControlledLoader();
            var driver = new RecordingDriver();
            var controller = CreateController(new[] { loader }, new[] { driver });
            var session = SessionToken.CreateNew();
            var releases = 0;
            var surfaceRoot = Own("DestroyLoadingSurface");
            var surface = new ModelSurfaceLease(surfaceRoot.transform, Vector3.one, false, () => releases++);

            Assert.That(controller.Open(session, CreateDefinition(), surface).Succeeded, Is.True);
            Assert.That(surfaceRoot.transform.Find("ModelRuntimeRoot"), Is.Not.Null);
            InvokeOnDestroy(controller);
            InvokeOnDestroy(controller);
            Assert.That(loader.Token.IsCancellationRequested, Is.True);
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(driver.DisposeCount, Is.EqualTo(1));

            var resource = new RecordingDisposable();
            ExpectEditModeDestroyErrors(1);
            loader.Succeed(Own("DestroyedOwnerLateModel"), resource);
            yield return null;

            Assert.That(resource.DisposeCount, Is.EqualTo(1));
            Assert.That(surfaceRoot.transform.Find("ModelRuntimeRoot"), Is.Null,
                "Model owns this runtime root, so destroying the Controller must not leave it under the leased surface.");
        }

        [UnityTest]
        public IEnumerator LoaderExceptionBecomesFailedStateWithoutEscapingAsyncContinuation()
        {
            var loader = new ControlledLoader();
            var driver = new RecordingDriver();
            var diagnostics = new List<DiagnosticEvent>();
            var controller = CreateController(new[] { loader }, new[] { driver }, diagnostics.Add);
            var sink = new RecordingSink();
            var session = SessionToken.CreateNew();
            var releases = 0;

            using (controller.Observe(sink))
            {
                Assert.That(controller.Open(session, CreateDefinition(),
                    CreateSurface("ThrowingLoaderSurface", () => releases++)).Succeeded, Is.True);
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: loader failure"));
                loader.Fail(new InvalidOperationException("loader failure"));
                yield return null;

                Assert.That(sink.Latest.Session, Is.EqualTo(session));
                Assert.That(sink.Latest.Phase, Is.EqualTo(ModelPhase.Failed));
                Assert.That(diagnostics.Select(item => item.Code), Does.Contain("MODEL_LOAD_FAILED"));
                ExpectEditModeDestroyErrors(1);
                Assert.That(controller.Close(session).Succeeded, Is.True);
                Assert.That(driver.DisposeCount, Is.EqualTo(1));
                Assert.That(releases, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator PostLoadExceptionDisposesResultAndDriverExactlyOnce()
        {
            var loader = new ControlledLoader();
            var driver = new RecordingDriver { AttachException = new InvalidOperationException("attach failure") };
            var controller = CreateController(new[] { loader }, new[] { driver });
            var sink = new RecordingSink();
            var session = SessionToken.CreateNew();
            var releases = 0;
            var resource = new RecordingDisposable();

            using (controller.Observe(sink))
            {
                Assert.That(controller.Open(session, CreateDefinition(),
                    CreateSurface("AttachFailureSurface", () => releases++)).Succeeded, Is.True);
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: attach failure"));
                ExpectEditModeDestroyErrors(1);
                loader.Succeed(Own("AttachFailureModel"), resource);
                yield return null;

                Assert.That(sink.Latest.Phase, Is.EqualTo(ModelPhase.Failed));
                Assert.That(resource.DisposeCount, Is.EqualTo(1));
                Assert.That(driver.DisposeCount, Is.EqualTo(1));

                ExpectEditModeDestroyErrors(1);
                Assert.That(controller.Close(session).Succeeded, Is.True);
                UnityEngine.Object.DestroyImmediate(controller.gameObject);
                yield return null;
                Assert.That(resource.DisposeCount, Is.EqualTo(1));
                Assert.That(driver.DisposeCount, Is.EqualTo(1));
                Assert.That(releases, Is.EqualTo(1));
            }
        }

        [UnityTest]
        public IEnumerator DestroyAfterReadyReleasesOwnedObjectsAndRuntimeRootExactlyOnce()
        {
            var loader = new ControlledLoader();
            var driver = new RecordingDriver();
            var controller = CreateController(new[] { loader }, new[] { driver });
            var session = SessionToken.CreateNew();
            var releases = 0;
            var resource = new RecordingDisposable();
            var surfaceRoot = Own("DestroyReadySurface");

            Assert.That(controller.Open(session, CreateDefinition(),
                new ModelSurfaceLease(surfaceRoot.transform, Vector3.one, false, () => releases++)).Succeeded, Is.True);
            loader.Succeed(Own("ReadyModel"), resource);
            yield return null;
            Assert.That(surfaceRoot.transform.Find("ModelRuntimeRoot"), Is.Not.Null);

            ExpectEditModeDestroyErrors(1);
            InvokeOnDestroy(controller);
            InvokeOnDestroy(controller);
            yield return null;

            Assert.That(resource.DisposeCount, Is.EqualTo(1));
            Assert.That(driver.DisposeCount, Is.EqualTo(1));
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(surfaceRoot.transform.Find("ModelRuntimeRoot"), Is.Null,
                "Destroy must mirror Close for the runtime root owned by ModelController.");
        }

        ModelController CreateController(
            IEnumerable<IModelLoader> loaders,
            IEnumerable<IModelDriver> drivers,
            Action<DiagnosticEvent> diagnostics = null)
        {
            var owner = Own("ModelControllerOwner");
            var controller = owner.AddComponent<ModelController>();
            controller.Initialize(new TestSelector(loaders, drivers), diagnostics);
            return controller;
        }

        static void ExpectEditModeDestroyErrors(int count)
        {
            for (var index = 0; index < count; index++)
            {
                LogAssert.Expect(
                    LogType.Error,
                    new Regex("Destroy may not be called from edit mode! Use DestroyImmediate instead\\."));
            }
        }

        static void InvokeOnDestroy(ModelController controller)
        {
            var method = typeof(ModelController).GetMethod(
                "OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, null);
        }

        ModelDefinition CreateDefinition()
            => new ModelDefinition(ModelSource.FromPrefab(Own("ModelSourcePrefab")));

        ModelSurfaceLease CreateSurface(string name, Action release)
            => new ModelSurfaceLease(Own(name).transform, Vector3.one, false, release);

        GameObject Own(string name)
        {
            var gameObject = new GameObject(name);
            _ownedObjects.Add(gameObject);
            return gameObject;
        }

        sealed class TestSelector : ModelImplementationSelector
        {
            readonly Queue<IModelLoader> _loaders;
            readonly Queue<IModelDriver> _drivers;

            public TestSelector(IEnumerable<IModelLoader> loaders, IEnumerable<IModelDriver> drivers)
            {
                _loaders = new Queue<IModelLoader>(loaders);
                _drivers = new Queue<IModelDriver>(drivers);
            }

            public override IModelLoader SelectLoader(ModelSource source) => _loaders.Dequeue();
            public override IModelDriver SelectDriver(ModelAnimationSpec animation) => _drivers.Dequeue();
        }

        sealed class ControlledLoader : IModelLoader
        {
            readonly TaskCompletionSource<ModelLoadResult> _completion =
                new TaskCompletionSource<ModelLoadResult>();

            public CancellationToken Token { get; private set; }

            public Task<ModelLoadResult> LoadAsync(
                ModelSource source,
                Transform parent,
                CancellationToken cancellationToken)
            {
                Token = cancellationToken;
                return _completion.Task;
            }

            public void Succeed(GameObject instance, IDisposable resource)
                => _completion.SetResult(ModelLoadResult.Success(instance, resource));

            public void Fail(Exception exception) => _completion.SetException(exception);
        }

        sealed class RecordingDriver : IModelDriver
        {
            public Exception AttachException { get; set; }
            public int DisposeCount { get; private set; }
            public int StopCount { get; private set; }
            public bool CanPlayAnimation => false;
            public bool IsAnimationPlaying => false;

            public ModelResult Attach(GameObject instance)
            {
                if (AttachException != null) throw AttachException;
                return ModelResult.Success();
            }

            public ModelResult PlayAnimation()
                => ModelResult.Failure(ModelFailureCode.UnsupportedAnimation, "test.animation.unavailable");

            public bool Tick(float deltaTime) => false;
            public void StopAnimation() => StopCount++;
            public void Dispose() => DisposeCount++;
        }

        sealed class RecordingDisposable : IDisposable
        {
            public int DisposeCount { get; private set; }
            public void Dispose() => DisposeCount++;
        }

        sealed class RecordingSink : IModelStateSink
        {
            public List<ModelState> States { get; } = new List<ModelState>();
            public ModelState Latest => States[States.Count - 1];
            public void Publish(ModelState state) => States.Add(state);
        }
    }
}
