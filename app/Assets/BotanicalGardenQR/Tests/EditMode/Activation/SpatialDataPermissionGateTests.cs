using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Adapters;
using BotanicalGardenQR.Activation.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode.Activation
{
    public sealed class SpatialDataPermissionGateTests
    {
        [Test]
        public void FirstRequest_PublishesRequestingThenGranted()
        {
            var platform = new FakePlatform();
            using var gate = new SpatialDataPermissionGateStateMachine(platform);
            var sink = new RecordingSink();
            using var observation = gate.Observe(sink);

            Assert.That(gate.Refresh().Succeeded, Is.True);
            Assert.That(gate.Request().Succeeded, Is.True);
            platform.CompleteGranted();

            Assert.That(
                sink.States,
                Is.EqualTo(new[]
                {
                    SpatialDataPermissionState.Unknown,
                    SpatialDataPermissionState.Requesting,
                    SpatialDataPermissionState.Granted
                }));
            Assert.That(gate.CurrentState, Is.EqualTo(SpatialDataPermissionState.Granted));
            Assert.That(platform.RequestCount, Is.EqualTo(1));
        }

        [Test]
        public void DenialCanRetryAndPermanentDenialRecoversAfterSettingsRefresh()
        {
            var platform = new FakePlatform();
            using var gate = new SpatialDataPermissionGateStateMachine(platform);

            gate.Request();
            platform.CompleteDenied();
            Assert.That(gate.CurrentState, Is.EqualTo(SpatialDataPermissionState.Denied));

            gate.Request();
            platform.CompletePermanentlyDenied();
            Assert.That(
                gate.CurrentState,
                Is.EqualTo(SpatialDataPermissionState.PermanentlyDenied));

            platform.HasPermission = true;
            Assert.That(gate.Refresh().Succeeded, Is.True);
            Assert.That(gate.CurrentState, Is.EqualTo(SpatialDataPermissionState.Granted));
            Assert.That(platform.RequestCount, Is.EqualTo(2));
        }

        [Test]
        public void FocusStyleRefresh_InvalidatesLatePermissionCallback()
        {
            var platform = new FakePlatform();
            using var gate = new SpatialDataPermissionGateStateMachine(platform);

            gate.Request();
            platform.HasPermission = true;
            gate.Refresh();
            platform.CompleteDenied();

            Assert.That(gate.CurrentState, Is.EqualTo(SpatialDataPermissionState.Granted));
        }

        [Test]
        public void FocusStyleRefresh_RecoversARequestWhosePlatformCallbackWasLost()
        {
            var platform = new FakePlatform();
            using var gate = new SpatialDataPermissionGateStateMachine(platform);

            gate.Request();
            gate.Refresh();

            Assert.That(gate.CurrentState, Is.EqualTo(SpatialDataPermissionState.Denied));
            Assert.That(gate.Request().Succeeded, Is.True);
            Assert.That(platform.RequestCount, Is.EqualTo(2));
        }

        [Test]
        public void UnsupportedPlatform_FailsClosedWithoutRequesting()
        {
            var platform = new FakePlatform { IsSupported = false };
            using var gate = new SpatialDataPermissionGateStateMachine(platform);

            var refreshed = gate.Refresh();
            var requested = gate.Request();

            Assert.That(refreshed.Succeeded, Is.True);
            Assert.That(requested.Succeeded, Is.False);
            Assert.That(
                requested.Failure,
                Is.EqualTo(SpatialDataPermissionCommandFailure.Unsupported));
            Assert.That(gate.CurrentState, Is.EqualTo(SpatialDataPermissionState.Unsupported));
            Assert.That(platform.RequestCount, Is.Zero);
        }

        [Test]
        public void RepeatedRequestWhileDialogIsOpen_DoesNotStartASecondSystemRequest()
        {
            var platform = new FakePlatform();
            using var gate = new SpatialDataPermissionGateStateMachine(platform);

            var first = gate.Request();
            var repeated = gate.Request();

            Assert.That(first.Succeeded, Is.True);
            Assert.That(repeated.Succeeded, Is.False);
            Assert.That(
                repeated.Failure,
                Is.EqualTo(SpatialDataPermissionCommandFailure.RequestInProgress));
            Assert.That(platform.RequestCount, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_InvalidatesLatePlatformCallbackAndDetachesObservers()
        {
            var platform = new FakePlatform();
            var gate = new SpatialDataPermissionGateStateMachine(platform);
            var sink = new RecordingSink();
            gate.Observe(sink);
            gate.Request();

            gate.Dispose();
            Assert.That(() => platform.CompleteGranted(), Throws.Nothing);

            Assert.That(
                sink.States,
                Is.EqualTo(new[]
                {
                    SpatialDataPermissionState.Unknown,
                    SpatialDataPermissionState.Requesting
                }));
            Assert.That(gate.Request().Failure, Is.EqualTo(SpatialDataPermissionCommandFailure.Disposed));
        }

        sealed class FakePlatform : ISpatialDataPermissionPlatform
        {
            Action _granted;
            Action _denied;
            Action _permanentlyDenied;
            Action _dismissed;

            public bool IsSupported { get; set; } = true;
            public bool HasPermission { get; set; }
            public int RequestCount { get; private set; }

            public void Request(
                Action granted,
                Action denied,
                Action permanentlyDenied,
                Action dismissed)
            {
                RequestCount++;
                _granted = granted;
                _denied = denied;
                _permanentlyDenied = permanentlyDenied;
                _dismissed = dismissed;
            }

            public void CompleteGranted() => Complete(ref _granted);
            public void CompleteDenied() => Complete(ref _denied);
            public void CompletePermanentlyDenied() => Complete(ref _permanentlyDenied);

            static void Complete(ref Action callback)
            {
                var action = callback;
                callback = null;
                action?.Invoke();
            }
        }

        sealed class RecordingSink : ISpatialDataPermissionStateSink
        {
            public List<SpatialDataPermissionState> States { get; } =
                new List<SpatialDataPermissionState>();

            public void OnSpatialDataPermissionStateChanged(SpatialDataPermissionState state)
                => States.Add(state);
        }
    }
}
