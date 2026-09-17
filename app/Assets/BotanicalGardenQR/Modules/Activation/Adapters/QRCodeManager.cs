// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.MRUtilityKit;
using System;
using System.Collections.Generic;
using UnityEngine;


namespace BotanicalGardenQR.Activation.Adapters
{
    public class QRCodeManager : MonoBehaviour
    {
        const float TrackingCheckIntervalSeconds = 0.5f;

        //
        // Static interface

        public const string ScenePermission = OVRPermissionsRequester.ScenePermission;

        public bool IsSupported => _mrukInstance && _mrukInstance.QRCodeTrackingSupported;

        public bool HasPermissions
#if UNITY_EDITOR
            => true;
#else
            => UnityEngine.Android.Permission.HasUserAuthorizedPermission(ScenePermission);
#endif

        public int ActiveTrackedCount => _activeCount;
        public bool TrackingActive => _mrukInstance &&
                                      _mrukInstance.TrackerConfiguration.QRCodeTrackingEnabled;

        /// <summary>
        /// Narrow instance event for explicitly wired product adapters. New runtime code
        /// should prefer this over the legacy static sample event.
        /// </summary>
        public event Action<MRUKTrackable, string> CodeDetected;

        /// <summary>Narrow instance event paired with <see cref="CodeDetected"/>.</summary>
        public event Action<MRUKTrackable> CodeRemoved;

        public void ReplayActiveCodes(Action<MRUKTrackable, string> receiver)
        {
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));
            foreach (var pair in _activeCodes)
                if (pair.Key != null)
                    receiver(pair.Key, pair.Value);
        }

        public bool TrackingEnabled
        {
            get => _mrukInstance && _mrukInstance.SceneSettings.TrackerConfiguration.QRCodeTrackingEnabled;
            set
            {
                if (!_mrukInstance)
                {
                    return;
                }
                var config = _mrukInstance.SceneSettings.TrackerConfiguration;
                config.QRCodeTrackingEnabled = value;
                _mrukInstance.SceneSettings.TrackerConfiguration = config;
            }
        }

        //
        // Serialized fields

        [SerializeField]
        MRUK _mrukInstance;

        // non-serialized fields

        int _activeCount;
        bool _isListening;
        bool _permissionMissingLogged;
        bool _supportPendingLogged;
        bool _trackingReadyLogged;
        float _nextTrackingCheck;
        readonly Dictionary<MRUKTrackable, string> _activeCodes =
            new Dictionary<MRUKTrackable, string>();


        //
        // MonoBehaviour messages

        void OnEnable()
        {
            if (!_mrukInstance)
            {
                Log($"{nameof(QRCodeManager)} requires an MRUK object in the scene!", LogType.Error);
                return;
            }

            _mrukInstance.SceneSettings.TrackableAdded.AddListener(OnTrackableAdded);
            _mrukInstance.SceneSettings.TrackableRemoved.AddListener(OnTrackableRemoved);
            _isListening = true;
            _nextTrackingCheck = 0f;
        }

        void Start() => EnsureTrackingRequested();

        void Update()
        {
            if (Time.unscaledTime < _nextTrackingCheck) return;
            EnsureTrackingRequested();
        }

        void EnsureTrackingRequested()
        {
            _nextTrackingCheck = Time.unscaledTime + TrackingCheckIntervalSeconds;
            if (!_mrukInstance) return;

            if (!HasPermissions)
            {
                if (!_permissionMissingLogged)
                {
                    _permissionMissingLogged = true;
                    Log(
                        "Spatial Data permission is not granted; the application permission gate keeps QR tracking unavailable.",
                        LogType.Warning);
                }
                if (TrackingEnabled) TrackingEnabled = false;
                _trackingReadyLogged = false;
                return;
            }

            _permissionMissingLogged = false;
            // Desired tracking must be set before querying runtime support.
            // MRUK applies this desired configuration asynchronously.
            if (!TrackingEnabled)
            {
                TrackingEnabled = true;
                Log("QR tracking requested in MRUK tracker configuration.");
            }

            if (!IsSupported)
            {
                if (!_supportPendingLogged)
                {
                    _supportPendingLogged = true;
                    Log("QR tracking support is not ready yet; startup will retry.", LogType.Warning);
                }
                return;
            }

            _supportPendingLogged = false;
            if (TrackingActive)
            {
                if (!_trackingReadyLogged)
                {
                    _trackingReadyLogged = true;
                    Log("QR tracking is active.");
                }
            }
            else
            {
                _trackingReadyLogged = false;
            }
        }

        void OnDisable()
        {
            if (_isListening && _mrukInstance)
            {
                _mrukInstance.SceneSettings.TrackableAdded.RemoveListener(OnTrackableAdded);
                _mrukInstance.SceneSettings.TrackableRemoved.RemoveListener(OnTrackableRemoved);
                _isListening = false;
            }
            _activeCodes.Clear();
            _activeCount = 0;
            _trackingReadyLogged = false;
        }


        //
        // UnityEvent listeners

        public void OnTrackableAdded(MRUKTrackable trackable)
        {
            if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode)
            {
                return;
            }

            var payload = GetPayload(trackable);

            _activeCodes[trackable] = payload;
            _activeCount = _activeCodes.Count;

            Log(
                $"{nameof(OnTrackableAdded)}: QRCode detected " +
                $"(active={_activeCount}, payloadLength={payload.Length}).");
            CodeDetected?.Invoke(trackable, payload);
        }

        public void OnTrackableRemoved(MRUKTrackable trackable)
        {
            if (trackable.TrackableType != OVRAnchor.TrackableType.QRCode)
            {
                return;
            }

            _activeCodes.Remove(trackable);
            _activeCount = _activeCodes.Count;
            Log($"QRCode removed (active={_activeCount}).");
            CodeRemoved?.Invoke(trackable);

        }


        //
        // private impl.

        void Log(object msg, LogType type = LogType.Log)
        {
            Debug.LogFormat(
                logType: type,
                logOptions: LogOption.None,
                context: this,
                format: "{0}: {1}", nameof(QRCodeManager), msg
            );
        }

        static string GetPayload(MRUKTrackable trackable)
        {
            if (trackable.MarkerPayloadString is { } str)
            {
                return str;
            }

            return trackable.MarkerPayloadBytes is { } bytes
                ? Convert.ToBase64String(bytes)
                : string.Empty;
        }

    }
}
