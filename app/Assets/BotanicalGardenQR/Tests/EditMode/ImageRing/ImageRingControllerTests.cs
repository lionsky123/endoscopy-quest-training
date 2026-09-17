using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Contracts.Flow;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.ImageRing.Backend;
using BotanicalGardenQR.ImageRing.Contracts;
using BotanicalGardenQR.ImageRing.Frontend;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.Panorama.Frontend;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.ImageRing
{
    public sealed class ImageRingControllerTests
    {
        const string SharedFontAssetPath =
            "Assets/BotanicalGardenQR/Resources/BotanicalGardenQR/Fonts/SourceHanSansSC-Regular SDF.asset";

        readonly List<Texture2D> _textures = new List<Texture2D>();
        readonly List<AudioClip> _audioClips = new List<AudioClip>();

        [TearDown]
        public void TearDown()
        {
            foreach (var texture in _textures)
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
            _textures.Clear();
            foreach (var clip in _audioClips)
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
            _audioClips.Clear();
        }

        [Test]
        public void DefinitionRejectsFewerThanThreeItems()
        {
            var items = new[] { Item(0), Item(1) };
            Assert.Throws<ArgumentOutOfRangeException>(() => new ImageRingDefinition(items));
        }

        [Test]
        public void DefinitionRejectsDuplicateTextures()
        {
            var texture = Texture();
            var items = new[]
            {
                new ImageRingItemDefinition(texture, Audio(), "A", "A description"),
                new ImageRingItemDefinition(texture, Audio(), "B", "B description"),
                Item(2)
            };

            Assert.Throws<ArgumentException>(() => new ImageRingDefinition(items));
        }

        [Test]
        public void DefinitionRejectsDuplicateAudioClips()
        {
            var audio = Audio();
            var items = new[]
            {
                new ImageRingItemDefinition(Texture(), audio, "A", "A description"),
                new ImageRingItemDefinition(Texture(), audio, "B", "B description"),
                Item(2)
            };

            Assert.Throws<ArgumentException>(() => new ImageRingDefinition(items));
        }

        [Test]
        public void OpenAndClosePublishOneSessionLifecycle()
        {
            var runtime = new FakeRuntime();
            var controller = ImageRingModuleFactory.Create(runtime);
            var sink = new RecordingSink();
            var subscription = controller.Observe(sink);
            var session = SessionToken.CreateNew();

            Assert.That(controller.Open(session, Definition()).Succeeded, Is.True);
            Assert.That(runtime.IsOpen, Is.True);
            Assert.That(
                sink.Phases,
                Is.EqualTo(new[]
                {
                    ImageRingPhase.Closed,
                    ImageRingPhase.Opening,
                    ImageRingPhase.Visible
                }));

            Assert.That(controller.Close(session).Succeeded, Is.True);
            Assert.That(runtime.IsOpen, Is.False);
            Assert.That(sink.Phases[sink.Phases.Count - 1], Is.EqualTo(ImageRingPhase.Closed));

            subscription.Dispose();
            controller.Dispose();
        }

        [Test]
        public void CallbackFromClosedGenerationCannotCloseNewSession()
        {
            var runtime = new FakeRuntime();
            var controller = ImageRingModuleFactory.Create(runtime);
            var first = SessionToken.CreateNew();
            var second = SessionToken.CreateNew();

            Assert.That(controller.Open(first, Definition()).Succeeded, Is.True);
            var staleCallback = runtime.RequestClose;
            Assert.That(controller.Close(first).Succeeded, Is.True);
            Assert.That(controller.Open(second, Definition()).Succeeded, Is.True);

            staleCallback();

            Assert.That(runtime.IsOpen, Is.True);
            Assert.That(runtime.CloseCount, Is.EqualTo(1));
            Assert.That(controller.Close(second).Succeeded, Is.True);
            controller.Dispose();
        }

        [Test]
        public void CurrentRuntimeCloseRequestClosesController()
        {
            var runtime = new FakeRuntime();
            var controller = ImageRingModuleFactory.Create(runtime);
            var sink = new RecordingSink();
            var subscription = controller.Observe(sink);
            var session = SessionToken.CreateNew();
            Assert.That(controller.Open(session, Definition()).Succeeded, Is.True);

            runtime.RequestClose();

            Assert.That(runtime.IsOpen, Is.False);
            Assert.That(sink.Phases[sink.Phases.Count - 1], Is.EqualTo(ImageRingPhase.Closed));
            subscription.Dispose();
            controller.Dispose();
        }

        [Test]
        public void RuntimeOpenFailureRollsBackAndPublishesFailure()
        {
            var runtime = new FakeRuntime { ThrowOnOpen = true };
            var controller = ImageRingModuleFactory.Create(runtime);
            var sink = new RecordingSink();
            var subscription = controller.Observe(sink);

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Test failure"));
            var result = controller.Open(SessionToken.CreateNew(), Definition());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo(ImageRingFailureCode.RuntimeFailed));
            Assert.That(runtime.CloseCount, Is.EqualTo(1));
            Assert.That(sink.Phases[sink.Phases.Count - 1], Is.EqualTo(ImageRingPhase.Failed));
            subscription.Dispose();
            controller.Dispose();
        }

        [Test]
        public void RuntimeOpenFailureAllowsLaterSessionRetry()
        {
            var runtime = new FakeRuntime { ThrowOnOpen = true };
            var controller = ImageRingModuleFactory.Create(runtime);

            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Test failure"));
            Assert.That(controller.Open(SessionToken.CreateNew(), Definition()).Succeeded, Is.False);

            runtime.ThrowOnOpen = false;
            var retrySession = SessionToken.CreateNew();
            Assert.That(controller.Open(retrySession, Definition()).Succeeded, Is.True);
            Assert.That(controller.Close(retrySession).Succeeded, Is.True);
            controller.Dispose();
        }

        [Test]
        public void PanoramaAuxiliaryBindingIgnoresStaleCloseAndReleasesOnce()
        {
            var controller = new ControllableImageRingController();
            var adapter = new PanoramaImageRingBinding(
                new ImageRingDefinitionSource(Definition()),
                controller);
            var binding = new PanoramaAuxiliaryExperienceBinding(
                adapter.HasDefinition,
                adapter.Open,
                adapter.Close);
            var sceneId = new SceneId("test_scene");
            var first = SessionToken.CreateNew();
            var second = SessionToken.CreateNew();
            var firstClosed = 0;
            var secondClosed = 0;

            Assert.That(binding.TryOpen(first, sceneId, () => firstClosed++), Is.True);
            controller.Publish(second, ImageRingPhase.Closed);
            Assert.That(firstClosed, Is.Zero);

            controller.Publish(first, ImageRingPhase.Closed);
            controller.Publish(first, ImageRingPhase.Closed);
            Assert.That(firstClosed, Is.EqualTo(1));

            Assert.That(binding.TryOpen(second, sceneId, () => secondClosed++), Is.True);
            binding.Close(first);
            Assert.That(controller.CloseCount, Is.Zero);

            controller.Publish(second, ImageRingPhase.Closed);
            Assert.That(secondClosed, Is.EqualTo(1));

            Assert.That(binding.TryOpen(second, sceneId, () => secondClosed++), Is.True);
            binding.Close(second);
            binding.Close(second);
            Assert.That(controller.CloseCount, Is.EqualTo(1));
            Assert.That(secondClosed, Is.EqualTo(1));

            adapter.Dispose();
            adapter.Dispose();
            Assert.That(controller.SubscriptionDisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void FrontendCloseStopsAudioAndReleasesWorldIdempotently()
        {
            var runtimeParent = new GameObject("ImageRingRuntimeParent");
            var viewer = new GameObject("ImageRingViewer");
            IImageRingRuntime runtime = null;
            try
            {
                runtime = ImageRingFrontend.Create(
                    runtimeParent.transform,
                    viewer.transform,
                    new NoopGazeSurfaceRegistry(),
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SharedFontAssetPath));
                var frontend = (ImageRingFrontend)runtime;
                var session = SessionToken.CreateNew();
                var definition = Definition();

                runtime.Open(session, definition, _ => { }, () => { });
                runtime.PlayAudio(definition.Items[0].Audio);
                Assert.That(frontend.transform.Find("ImageRingSpatialWorld"), Is.Not.Null);
                Assert.That(frontend.GetComponent<AudioSource>().clip, Is.SameAs(definition.Items[0].Audio));

                runtime.Close();
                runtime.Close();
                Assert.That(frontend.transform.Find("ImageRingSpatialWorld"), Is.Null);
                Assert.That(frontend.GetComponent<AudioSource>().clip, Is.Null);

                runtime.Dispose();
                runtime.Dispose();
                runtime = null;
            }
            finally
            {
                runtime?.Dispose();
                UnityEngine.Object.DestroyImmediate(runtimeParent);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void PanoramaAuxiliaryOpenHidesControlsOnlyAfterSuccessAndRestoresOnClose()
        {
            var frontendObject = new GameObject("PanoramaFrontend");
            var runtimeRoot = new GameObject("PanoramaRuntimeRoot");
            var viewer = new GameObject("PanoramaViewer");
            var frontend = frontendObject.AddComponent<PanoramaFrontend>();
            var shell = new RecordingFrontendShell();
            var controller = new SuccessfulPanoramaController();
            var session = SessionToken.CreateNew();
            var sceneId = new SceneId("test_scene");
            var auxiliaryOpenSucceeds = false;
            var auxiliaryOpenCount = 0;
            Action auxiliaryClosed = null;
            PanoramaPageLifecycleBinding lifecycle = null;

            try
            {
                lifecycle = new PanoramaPageLifecycleBinding(
                    shell,
                    new PanoramaDefinitionSource(
                        new PanoramaDefinition(PanoramaSource.FromTexture(Texture()))),
                    controller,
                    frontend,
                    runtimeRoot.transform,
                    viewer.transform,
                    new NoopGazeSurfaceRegistry(),
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SharedFontAssetPath),
                    new PanoramaAuxiliaryExperienceBinding(
                        _ => true,
                        (_, __, closed) =>
                        {
                            auxiliaryOpenCount++;
                            if (!auxiliaryOpenSucceeds) return false;
                            auxiliaryClosed = closed;
                            return true;
                        },
                        _ => { }));

                Assert.That(lifecycle.Prepare(session, sceneId).Succeeded, Is.True);
                Assert.That(lifecycle.Activate(session).Succeeded, Is.True);
                var spatialRoot = runtimeRoot.transform.Find("PanoramaSpatialRoot");
                var imageBubble = spatialRoot.Find("PanoramaImageRingBubble");
                var imageButton = imageBubble.GetComponentInChildren<Button>(true);
                Assert.That(spatialRoot.gameObject.activeSelf, Is.True);

                imageButton.onClick.Invoke();
                Assert.That(auxiliaryOpenCount, Is.EqualTo(1));
                Assert.That(shell.StatusCount, Is.EqualTo(1));
                Assert.That(spatialRoot.gameObject.activeSelf, Is.True);

                auxiliaryOpenSucceeds = true;
                imageButton.onClick.Invoke();
                Assert.That(auxiliaryOpenCount, Is.EqualTo(2));
                Assert.That(spatialRoot.gameObject.activeSelf, Is.False);

                auxiliaryClosed();
                Assert.That(spatialRoot.gameObject.activeSelf, Is.True);
                Assert.That(lifecycle.Release(session).Succeeded, Is.True);
                lifecycle = null;
            }
            finally
            {
                if (lifecycle != null) lifecycle.Release(session);
                UnityEngine.Object.DestroyImmediate(frontendObject);
                UnityEngine.Object.DestroyImmediate(runtimeRoot);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        [Test]
        public void PlaybackIntentKeepsOneActiveClipAndIgnoresOldItemStop()
        {
            var runtime = new FakeRuntime();
            var controller = ImageRingModuleFactory.Create(runtime);
            var definition = Definition();
            var session = SessionToken.CreateNew();
            Assert.That(controller.Open(session, definition).Succeeded, Is.True);

            runtime.DispatchPlayback(new ImageRingItemPlaybackIntent(0, ImageRingItemPlaybackIntentKind.Play));
            Assert.That(runtime.PlayedClips, Is.EqualTo(new[] { definition.Items[0].Audio }));

            runtime.DispatchPlayback(new ImageRingItemPlaybackIntent(1, ImageRingItemPlaybackIntentKind.Play));
            Assert.That(runtime.ImmediateStopCount, Is.EqualTo(1));
            Assert.That(runtime.PlayedClips, Is.EqualTo(new[]
            {
                definition.Items[0].Audio,
                definition.Items[1].Audio
            }));

            runtime.DispatchPlayback(new ImageRingItemPlaybackIntent(0, ImageRingItemPlaybackIntentKind.Stop));
            Assert.That(runtime.FadeStopCount, Is.EqualTo(0));
            runtime.DispatchPlayback(new ImageRingItemPlaybackIntent(1, ImageRingItemPlaybackIntentKind.Stop));
            Assert.That(runtime.FadeStopCount, Is.EqualTo(1));

            controller.Dispose();
        }

        [Test]
        public void PlaybackCallbackFromClosedGenerationCannotAffectNewSession()
        {
            var runtime = new FakeRuntime();
            var controller = ImageRingModuleFactory.Create(runtime);
            var first = SessionToken.CreateNew();
            var second = SessionToken.CreateNew();
            Assert.That(controller.Open(first, Definition()).Succeeded, Is.True);
            var stalePlayback = runtime.DispatchPlayback;
            Assert.That(controller.Close(first).Succeeded, Is.True);
            Assert.That(controller.Open(second, Definition()).Succeeded, Is.True);

            stalePlayback(new ImageRingItemPlaybackIntent(0, ImageRingItemPlaybackIntentKind.Play));

            Assert.That(runtime.PlayedClips, Is.Empty);
            controller.Dispose();
        }

        ImageRingDefinition Definition()
            => new ImageRingDefinition(new[] { Item(0), Item(1), Item(2) });

        ImageRingItemDefinition Item(int index)
            => new ImageRingItemDefinition(Texture(), Audio(), $"Title {index}", $"Description {index}");

        Texture2D Texture()
        {
            var texture = new Texture2D(2, 2);
            _textures.Add(texture);
            return texture;
        }

        AudioClip Audio()
        {
            var clip = AudioClip.Create($"Audio {_audioClips.Count}", 32, 1, 24000, false);
            _audioClips.Add(clip);
            return clip;
        }

        sealed class FakeRuntime : IImageRingRuntime
        {
            public bool ThrowOnOpen { get; set; }
            public bool IsOpen { get; private set; }
            public int CloseCount { get; private set; }
            public Action RequestClose { get; private set; }
            public Action<ImageRingItemPlaybackIntent> DispatchPlayback { get; private set; }
            public List<AudioClip> PlayedClips { get; } = new List<AudioClip>();
            public int ImmediateStopCount { get; private set; }
            public int FadeStopCount { get; private set; }

            public void Open(
                SessionToken session,
                ImageRingDefinition definition,
                Action<ImageRingItemPlaybackIntent> dispatchPlayback,
                Action requestClose)
            {
                RequestClose = requestClose;
                DispatchPlayback = dispatchPlayback;
                if (ThrowOnOpen) throw new InvalidOperationException("Test failure");
                IsOpen = true;
            }

            public void PlayAudio(AudioClip clip) => PlayedClips.Add(clip);

            public void StopAudio(bool immediate)
            {
                if (immediate) ImmediateStopCount++;
                else FadeStopCount++;
            }

            public void Close()
            {
                CloseCount++;
                IsOpen = false;
            }

            public void Dispose()
            {
                IsOpen = false;
                RequestClose = null;
                DispatchPlayback = null;
            }
        }

        sealed class ImageRingDefinitionSource : IImageRingDefinitionSource
        {
            readonly ImageRingDefinition _definition;

            public ImageRingDefinitionSource(ImageRingDefinition definition)
                => _definition = definition;

            public bool TryGet(SceneId sceneId, out ImageRingDefinition definition)
            {
                definition = _definition;
                return sceneId.IsValid;
            }
        }

        sealed class ControllableImageRingController : IImageRingController
        {
            IImageRingStateSink _sink;
            long _version;

            public int CloseCount { get; private set; }
            public int SubscriptionDisposeCount { get; private set; }

            public ImageRingResult Open(SessionToken session, ImageRingDefinition definition)
                => ImageRingResult.Success();

            public ImageRingResult Close(SessionToken session)
            {
                CloseCount++;
                Publish(session, ImageRingPhase.Closed);
                return ImageRingResult.Success();
            }

            public IDisposable Observe(IImageRingStateSink sink)
            {
                _sink = sink;
                return new CallbackDisposable(() => SubscriptionDisposeCount++);
            }

            public void Publish(SessionToken session, ImageRingPhase phase)
                => _sink?.Publish(new ImageRingState(session, ++_version, phase));

            public void Dispose() { }
        }

        sealed class PanoramaDefinitionSource : IPanoramaDefinitionSource
        {
            readonly PanoramaDefinition _definition;

            public PanoramaDefinitionSource(PanoramaDefinition definition)
                => _definition = definition;

            public bool TryGet(SceneId sceneId, out PanoramaDefinition definition)
            {
                definition = _definition;
                return sceneId.IsValid;
            }
        }

        sealed class SuccessfulPanoramaController : IPanoramaController
        {
            public PanoramaResult Open(
                SessionToken session,
                PanoramaDefinition definition,
                PanoramaSurfaceLease surface)
                => PanoramaResult.Success();

            public PanoramaResult Dispatch(SessionToken session, PanoramaIntent intent)
                => PanoramaResult.Success();

            public PanoramaResult Close(SessionToken session)
                => PanoramaResult.Success();

            public IDisposable Observe(IPanoramaStateSink sink)
                => new CallbackDisposable(() => { });
        }

        sealed class RecordingFrontendShell : IGlobalFrontendShell
        {
            public int StatusCount { get; private set; }

            public FlowResult Dispatch(FlowIntent intent) => FlowResult.Success;
            public void ShowStatus(SessionToken session, UserFault fault) => StatusCount++;
            public IDisposable BindFeaturePageAction(
                FeaturePageId feature,
                IFeaturePageActionSource source)
                => new CallbackDisposable(() => { });
            public FrontendSurfaceResult AcquireFeatureSurface(SessionToken session, FeaturePageId feature)
                => FrontendSurfaceResult.Reject(FrontendSurfaceFailure.ShellUnavailable);
            public FrontendNarrationDockLease AcquireNarrationDock(SessionToken session) => null;
            public FlowResult SetImmersiveFeature(SessionToken session, FeaturePageId feature, bool active)
                => FlowResult.Success;
            public void Dispose() { }
        }

        sealed class NoopGazeSurfaceRegistry : IFrontendGazeSurfaceRegistry
        {
            public System.IDisposable SuspendPanelInput() => new Suspension();
            sealed class Suspension : System.IDisposable { public void Dispose() { } }
            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(
                Transform surfaceRoot,
                int priority,
                string label)
                => new NoopGazeSurfaceRegistration();
        }

        sealed class NoopGazeSurfaceRegistration : IFrontendGazeSurfaceRegistration
        {
            public void Invalidate() { }

            public bool IsFocused => false;
            public void Dispose() { }
        }

        sealed class CallbackDisposable : IDisposable
        {
            readonly Action _dispose;
            bool _disposed;

            public CallbackDisposable(Action dispose) => _dispose = dispose;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _dispose();
            }
        }

        sealed class RecordingSink : IImageRingStateSink
        {
            public List<ImageRingPhase> Phases { get; } = new List<ImageRingPhase>();
            public void Publish(ImageRingState state) => Phases.Add(state.Phase);
        }
    }
}
