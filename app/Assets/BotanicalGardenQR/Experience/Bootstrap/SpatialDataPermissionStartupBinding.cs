using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap
{
    /// <summary>
    /// Application seam between the platform permission owner, the existing
    /// recovery surface and Recognition's one-way startup gate.
    /// </summary>
    internal sealed class SpatialDataPermissionStartupBinding :
        ISpatialDataPermissionStateSink,
        IDisposable
    {
        const int MaximumRecognitionStartAttempts = 2;
        const float RecognitionRetryDelaySeconds = 0.5f;

        readonly ISpatialDataPermissionGate _gate;
        readonly ISpatialDataPermissionRecoverySurface _surface;
        readonly Action _startRecognition;
        readonly IDisposable _stateSubscription;
        bool _recognitionRequested;
        bool _recognitionStarted;
        bool _recognitionStarting;
        bool _recognitionRetryScheduled;
        int _recognitionStartAttempts;
        float _recognitionRetryDelayRemaining;
        bool _disposed;

        public SpatialDataPermissionStartupBinding(
            ISpatialDataPermissionGate gate,
            ISpatialDataPermissionRecoverySurface surface,
            Action startRecognition)
        {
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _surface = surface ?? throw new ArgumentNullException(nameof(surface));
            _startRecognition = startRecognition ?? throw new ArgumentNullException(nameof(startRecognition));
            _surface.RetrySpatialPermissionRequested += HandleRetryRequested;
            try
            {
                _stateSubscription = _gate.Observe(this) ??
                                     throw new InvalidOperationException(
                                         "Spatial Data permission observation returned no lease.");
            }
            catch
            {
                _surface.RetrySpatialPermissionRequested -= HandleRetryRequested;
                throw;
            }
        }

        public void BeginPermissionRequest()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SpatialDataPermissionStartupBinding));
            var refreshed = _gate.Refresh();
            if (!refreshed.Succeeded &&
                refreshed.Failure != SpatialDataPermissionCommandFailure.Unsupported)
                Debug.LogWarning(
                    $"Spatial Data permission refresh was rejected: {refreshed.Failure}.");
            if (_gate.CurrentState == SpatialDataPermissionState.Unknown ||
                _gate.CurrentState == SpatialDataPermissionState.Denied)
                RequestPermission();
        }

        public void RequestRecognitionStart()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SpatialDataPermissionStartupBinding));
            _recognitionRequested = true;
            TryStartRecognition(_gate.CurrentState);
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (_disposed || !_recognitionRetryScheduled || _recognitionStarted) return;
            if (float.IsNaN(unscaledDeltaSeconds) || float.IsInfinity(unscaledDeltaSeconds) ||
                unscaledDeltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
            if (_gate.CurrentState != SpatialDataPermissionState.Granted) return;

            _recognitionRetryDelayRemaining -= unscaledDeltaSeconds;
            if (_recognitionRetryDelayRemaining > 0f) return;
            _recognitionRetryScheduled = false;
            TryStartRecognition(SpatialDataPermissionState.Granted);
        }

        public void OnSpatialDataPermissionStateChanged(SpatialDataPermissionState state)
        {
            if (_disposed) return;
            _surface.OnSpatialDataPermissionStateChanged(state);
            TryStartRecognition(state);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _recognitionRetryScheduled = false;
            _surface.RetrySpatialPermissionRequested -= HandleRetryRequested;
            _stateSubscription.Dispose();
        }

        void HandleRetryRequested()
        {
            if (_disposed) return;
            if (_gate.CurrentState == SpatialDataPermissionState.PermanentlyDenied)
            {
                var refreshed = _gate.Refresh();
                if (!refreshed.Succeeded)
                    Debug.LogWarning(
                        $"Spatial Data permission settings refresh was rejected: {refreshed.Failure}.");
                return;
            }
            RequestPermission();
        }

        void RequestPermission()
        {
            var requested = _gate.Request();
            if (!requested.Succeeded &&
                requested.Failure != SpatialDataPermissionCommandFailure.RequestInProgress &&
                requested.Failure != SpatialDataPermissionCommandFailure.Unsupported)
                Debug.LogWarning(
                    $"Spatial Data permission request was rejected: {requested.Failure}.");
        }

        void TryStartRecognition(SpatialDataPermissionState state)
        {
            if (!_recognitionRequested || _recognitionStarted || _recognitionStarting ||
                state != SpatialDataPermissionState.Granted ||
                _recognitionStartAttempts >= MaximumRecognitionStartAttempts)
                return;
            _recognitionRetryScheduled = false;
            _recognitionStarting = true;
            _recognitionStartAttempts++;
            try
            {
                _startRecognition();
                _recognitionStarted = true;
            }
            catch (Exception exception)
            {
                if (_recognitionStartAttempts < MaximumRecognitionStartAttempts)
                {
                    _recognitionRetryDelayRemaining = RecognitionRetryDelaySeconds;
                    _recognitionRetryScheduled = true;
                    Debug.LogError(
                        $"[VisitorStartup] Recognition start failed; one bounded retry is scheduled: {exception.GetType().Name}.");
                }
                else
                {
                    Debug.LogError(
                        $"[VisitorStartup] Recognition start failed after the bounded retry: {exception.GetType().Name}.");
                }
            }
            finally
            {
                _recognitionStarting = false;
            }
        }
    }
}
