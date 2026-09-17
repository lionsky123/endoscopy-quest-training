using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Activation.Adapters
{
    /// <summary>
    /// The only production owner of the Meta Spatial Data permission request.
    /// OVRManager, QR tracking and physical-augmentation code only consume the
    /// resulting permission state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MetaSpatialDataPermissionGate : MonoBehaviour, ISpatialDataPermissionGate
    {
        SpatialDataPermissionGateStateMachine _gate;
        bool _disposed;

        SpatialDataPermissionGateStateMachine Gate
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MetaSpatialDataPermissionGate));
                if (_gate != null) return _gate;
                _gate = new SpatialDataPermissionGateStateMachine(
                    new UnitySpatialDataPermissionPlatform());
                _gate.Refresh();
                return _gate;
            }
        }

        public SpatialDataPermissionState CurrentState => Gate.CurrentState;
        public SpatialDataPermissionCommandResult Request() => Gate.Request();
        public SpatialDataPermissionCommandResult Refresh() => Gate.Refresh();
        public IDisposable Observe(ISpatialDataPermissionStateSink sink) => Gate.Observe(sink);

        void Awake() => _ = Gate;

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && !_disposed) Gate.Refresh();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _gate?.Dispose();
            _gate = null;
        }

        void OnDestroy() => Dispose();
    }

    internal interface ISpatialDataPermissionPlatform
    {
        bool IsSupported { get; }
        bool HasPermission { get; }
        void Request(
            Action granted,
            Action denied,
            Action permanentlyDenied,
            Action dismissed);
    }

    internal sealed class SpatialDataPermissionGateStateMachine : ISpatialDataPermissionGate
    {
        readonly ISpatialDataPermissionPlatform _platform;
        readonly Dictionary<long, ISpatialDataPermissionStateSink> _sinks =
            new Dictionary<long, ISpatialDataPermissionStateSink>();
        long _nextSinkId;
        long _requestGeneration;
        bool _disposed;

        public SpatialDataPermissionGateStateMachine(ISpatialDataPermissionPlatform platform)
            => _platform = platform ?? throw new ArgumentNullException(nameof(platform));

        public SpatialDataPermissionState CurrentState { get; private set; } =
            SpatialDataPermissionState.Unknown;

        public SpatialDataPermissionCommandResult Request()
        {
            if (_disposed)
                return SpatialDataPermissionCommandResult.Reject(
                    SpatialDataPermissionCommandFailure.Disposed);
            if (!_platform.IsSupported)
            {
                SetState(SpatialDataPermissionState.Unsupported);
                return SpatialDataPermissionCommandResult.Reject(
                    SpatialDataPermissionCommandFailure.Unsupported);
            }
            if (_platform.HasPermission)
            {
                SetState(SpatialDataPermissionState.Granted);
                return SpatialDataPermissionCommandResult.Success;
            }
            if (CurrentState == SpatialDataPermissionState.Requesting)
                return SpatialDataPermissionCommandResult.Reject(
                    SpatialDataPermissionCommandFailure.RequestInProgress);

            var generation = ++_requestGeneration;
            SetState(SpatialDataPermissionState.Requesting);
            try
            {
                _platform.Request(
                    () => CompleteRequest(generation, SpatialDataPermissionState.Granted),
                    () => CompleteRequest(generation, SpatialDataPermissionState.Denied),
                    () => CompleteRequest(generation, SpatialDataPermissionState.PermanentlyDenied),
                    () => CompleteRequest(generation, SpatialDataPermissionState.Denied));
                return SpatialDataPermissionCommandResult.Success;
            }
            catch (Exception)
            {
                CompleteRequest(generation, SpatialDataPermissionState.Denied);
                return SpatialDataPermissionCommandResult.Reject(
                    SpatialDataPermissionCommandFailure.PlatformFailure);
            }
        }

        public SpatialDataPermissionCommandResult Refresh()
        {
            if (_disposed)
                return SpatialDataPermissionCommandResult.Reject(
                    SpatialDataPermissionCommandFailure.Disposed);
            if (!_platform.IsSupported)
            {
                SetState(SpatialDataPermissionState.Unsupported);
                return SpatialDataPermissionCommandResult.Success;
            }
            if (_platform.HasPermission)
            {
                ++_requestGeneration;
                SetState(SpatialDataPermissionState.Granted);
                return SpatialDataPermissionCommandResult.Success;
            }

            if (CurrentState == SpatialDataPermissionState.Granted ||
                CurrentState == SpatialDataPermissionState.Requesting)
            {
                ++_requestGeneration;
                SetState(SpatialDataPermissionState.Denied);
            }
            return SpatialDataPermissionCommandResult.Success;
        }

        public IDisposable Observe(ISpatialDataPermissionStateSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (_disposed) throw new ObjectDisposedException(nameof(SpatialDataPermissionGateStateMachine));
            var id = ++_nextSinkId;
            _sinks.Add(id, sink);
            TryNotify(sink, CurrentState);
            return new Subscription(this, id);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ++_requestGeneration;
            _sinks.Clear();
        }

        void CompleteRequest(long generation, SpatialDataPermissionState state)
        {
            if (_disposed || generation != _requestGeneration ||
                CurrentState != SpatialDataPermissionState.Requesting)
                return;
            SetState(state);
        }

        void SetState(SpatialDataPermissionState state)
        {
            if (CurrentState == state) return;
            CurrentState = state;
            var sinks = new List<ISpatialDataPermissionStateSink>(_sinks.Values);
            for (var index = 0; index < sinks.Count; index++)
                TryNotify(sinks[index], state);
        }

        static void TryNotify(
            ISpatialDataPermissionStateSink sink,
            SpatialDataPermissionState state)
        {
            try { sink.OnSpatialDataPermissionStateChanged(state); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        void Remove(long id) => _sinks.Remove(id);

        sealed class Subscription : IDisposable
        {
            SpatialDataPermissionGateStateMachine _owner;
            readonly long _id;

            public Subscription(SpatialDataPermissionGateStateMachine owner, long id)
            {
                _owner = owner;
                _id = id;
            }

            public void Dispose()
            {
                var owner = _owner;
                _owner = null;
                owner?.Remove(_id);
            }
        }
    }

    internal sealed class UnitySpatialDataPermissionPlatform : ISpatialDataPermissionPlatform
    {
        public bool IsSupported
        {
            get
            {
#if UNITY_EDITOR || UNITY_ANDROID
                return true;
#else
                return false;
#endif
            }
        }

        public bool HasPermission
        {
            get
            {
#if UNITY_EDITOR
                // Link/XR Sim permissions are configured outside the Android app.
                return true;
#elif UNITY_ANDROID
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                    OVRPermissionsRequester.ScenePermission);
#else
                return false;
#endif
            }
        }

        public void Request(
            Action granted,
            Action denied,
            Action permanentlyDenied,
            Action dismissed)
        {
#if UNITY_EDITOR
            granted?.Invoke();
#elif UNITY_ANDROID
            var completed = false;
            void Complete(Action callback)
            {
                if (completed) return;
                completed = true;
                callback?.Invoke();
            }

            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => Complete(granted);
            callbacks.PermissionDenied += permission => Complete(
                UnityEngine.Android.Permission.ShouldShowRequestPermissionRationale(permission)
                    ? denied
                    : permanentlyDenied);
            callbacks.PermissionRequestDismissed += _ => Complete(dismissed);
            UnityEngine.Android.Permission.RequestUserPermission(
                OVRPermissionsRequester.ScenePermission,
                callbacks);
#else
            throw new PlatformNotSupportedException(
                "Meta Spatial Data permission is available only on Android/Quest.");
#endif
        }
    }
}
