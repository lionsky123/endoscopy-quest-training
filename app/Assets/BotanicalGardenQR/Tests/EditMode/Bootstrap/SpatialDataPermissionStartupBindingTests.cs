using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.FrontendShell.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BotanicalGardenQR.Tests.EditMode.Bootstrap
{
    public sealed class SpatialDataPermissionStartupBindingTests
    {
        [Test]
        public void BeginAndExplorationReady_StartRecognitionOnlyAfterGrant()
        {
            var gate = new FakeGate();
            var surface = new FakeSurface();
            var starts = 0;
            using var binding = new SpatialDataPermissionStartupBinding(
                gate,
                surface,
                () => starts++);

            binding.BeginPermissionRequest();
            binding.RequestRecognitionStart();
            Assert.That(gate.RequestCalls, Is.EqualTo(1));
            Assert.That(starts, Is.Zero);

            gate.Publish(SpatialDataPermissionState.Granted);
            gate.Publish(SpatialDataPermissionState.Granted);

            Assert.That(starts, Is.EqualTo(1));
            Assert.That(surface.State, Is.EqualTo(SpatialDataPermissionState.Granted));
        }

        [Test]
        public void RetryIntent_RequestsAfterDenialAndRefreshesAfterPermanentDenial()
        {
            var gate = new FakeGate();
            var surface = new FakeSurface();
            using var binding = new SpatialDataPermissionStartupBinding(gate, surface, () => { });

            gate.Publish(SpatialDataPermissionState.Denied);
            surface.Retry();
            Assert.That(gate.RequestCalls, Is.EqualTo(1));

            gate.Publish(SpatialDataPermissionState.PermanentlyDenied);
            surface.Retry();
            Assert.That(gate.RefreshCalls, Is.EqualTo(1));
            Assert.That(gate.RequestCalls, Is.EqualTo(1));
        }

        [Test]
        public void RecognitionStartFailure_RemainsRetryableAndLatchesOnlyAfterSuccess()
        {
            var gate = new FakeGate();
            var surface = new FakeSurface();
            var attempts = 0;
            using var binding = new SpatialDataPermissionStartupBinding(
                gate,
                surface,
                () =>
                {
                    attempts++;
                    if (attempts == 1) throw new InvalidOperationException("transient startup failure");
                });

            gate.Publish(SpatialDataPermissionState.Granted);
            LogAssert.Expect(
                LogType.Error,
                "[VisitorStartup] Recognition start failed; one bounded retry is scheduled: InvalidOperationException.");
            binding.RequestRecognitionStart();
            Assert.That(attempts, Is.EqualTo(1));

            binding.RequestRecognitionStart();
            binding.RequestRecognitionStart();

            Assert.That(attempts, Is.EqualTo(2),
                "The first failure must not latch startup, while the first success must latch it exactly once.");
        }

        [Test]
        public void RecognitionStartFailure_RetriesOnceAfterABoundedDelayWithoutAnotherStateEvent()
        {
            var gate = new FakeGate();
            var surface = new FakeSurface();
            var attempts = 0;
            using var binding = new SpatialDataPermissionStartupBinding(
                gate,
                surface,
                () =>
                {
                    attempts++;
                    if (attempts == 1) throw new InvalidOperationException("transient startup failure");
                });

            gate.Publish(SpatialDataPermissionState.Granted);
            LogAssert.Expect(
                LogType.Error,
                "[VisitorStartup] Recognition start failed; one bounded retry is scheduled: InvalidOperationException.");
            binding.RequestRecognitionStart();

            binding.Tick(0.49f);
            Assert.That(attempts, Is.EqualTo(1));
            binding.Tick(0.02f);
            binding.Tick(10f);

            Assert.That(attempts, Is.EqualTo(2),
                "A transient startup failure gets one delayed retry, never an unbounded polling loop.");
        }

        [Test]
        public void Dispose_DetachesStateAndRetryChannels()
        {
            var gate = new FakeGate();
            var surface = new FakeSurface();
            var binding = new SpatialDataPermissionStartupBinding(gate, surface, () => { });

            binding.Dispose();
            gate.Publish(SpatialDataPermissionState.Denied);
            surface.Retry();

            Assert.That(gate.ActiveObservers, Is.Zero);
            Assert.That(gate.RequestCalls, Is.Zero);
            Assert.That(surface.State, Is.EqualTo(SpatialDataPermissionState.Unknown));
        }

        sealed class FakeGate : ISpatialDataPermissionGate
        {
            ISpatialDataPermissionStateSink _sink;

            public SpatialDataPermissionState CurrentState { get; private set; } =
                SpatialDataPermissionState.Unknown;
            public int RequestCalls { get; private set; }
            public int RefreshCalls { get; private set; }
            public int ActiveObservers => _sink == null ? 0 : 1;

            public SpatialDataPermissionCommandResult Request()
            {
                RequestCalls++;
                return SpatialDataPermissionCommandResult.Success;
            }

            public SpatialDataPermissionCommandResult Refresh()
            {
                RefreshCalls++;
                return SpatialDataPermissionCommandResult.Success;
            }

            public IDisposable Observe(ISpatialDataPermissionStateSink sink)
            {
                _sink = sink ?? throw new ArgumentNullException(nameof(sink));
                sink.OnSpatialDataPermissionStateChanged(CurrentState);
                return new CallbackDisposable(() => _sink = null);
            }

            public void Publish(SpatialDataPermissionState state)
            {
                CurrentState = state;
                _sink?.OnSpatialDataPermissionStateChanged(state);
            }

            public void Dispose() => _sink = null;
        }

        sealed class FakeSurface : ISpatialDataPermissionRecoverySurface
        {
            public event Action RetrySpatialPermissionRequested;
            public SpatialDataPermissionState State { get; private set; } =
                SpatialDataPermissionState.Unknown;

            public void OnSpatialDataPermissionStateChanged(SpatialDataPermissionState state)
                => State = state;

            public void Retry() => RetrySpatialPermissionRequested?.Invoke();
        }

        sealed class CallbackDisposable : IDisposable
        {
            Action _dispose;
            public CallbackDisposable(Action dispose) => _dispose = dispose;
            public void Dispose()
            {
                var dispose = _dispose;
                _dispose = null;
                dispose?.Invoke();
            }
        }
    }
}
