using System;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Panorama.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Panorama.Backend
{
    internal sealed class PanoramaController : IPanoramaController, IDisposable
    {
        readonly Transform _runtimeRoot;
        readonly Transform _viewer;
        readonly Action<DiagnosticEvent> _diagnostics;
        readonly StateChannel<PanoramaState> _states;
        GameObject _instance;
        PanoramaRenderer _renderer;
        PanoramaSurfaceLease _surface;
        SessionToken _session;
        long _version;
        int _generation;
        float _initialYaw;
        float _yaw;
        PanoramaSource _source;

        public PanoramaController(Transform runtimeRoot, Transform viewer, Action<DiagnosticEvent> diagnostics = null)
        {
            _runtimeRoot = runtimeRoot;
            _viewer = viewer;
            _diagnostics = diagnostics;
            _states = StateChannel<PanoramaState>.ForCurrentThread(
                new PanoramaState(default, 0, PanoramaPhase.Closed, 0f),
                state => state.Version);
        }

        public PanoramaResult Open(SessionToken session, PanoramaDefinition definition, PanoramaSurfaceLease surface)
        {
            if (!session.IsValid)
                return PanoramaResult.Failure(PanoramaFailureCode.StaleSession, "panorama.session.invalid");
            if (_runtimeRoot == null || _viewer == null)
                return PanoramaResult.Failure(PanoramaFailureCode.SurfaceUnavailable, "panorama.runtime_root.destroyed");
            if (definition == null || definition.Source == null || !IsFinite(definition.InitialYawDegrees))
                return PanoramaResult.Failure(PanoramaFailureCode.InvalidDefinition, "panorama.definition.invalid");
            if (surface == null || surface.ContentRoot == null)
                return PanoramaResult.Failure(PanoramaFailureCode.SurfaceUnavailable, "panorama.surface.unavailable");
            if (_renderer != null)
                return PanoramaResult.Failure(PanoramaFailureCode.RenderFailed, "panorama.already_open");

            _session = session;
            _surface = surface;
            _initialYaw = NormalizeYaw(definition.InitialYawDegrees);
            _yaw = _initialYaw;
            _source = definition.Source;
            var generation = ++_generation;

            try
            {
                _instance = new GameObject("PanoramaRuntime");
                _instance.transform.SetParent(_runtimeRoot, false);
                _instance.transform.localScale = Vector3.one;
                _instance.transform.position = _viewer.position;
                _renderer = _instance.AddComponent<PanoramaRenderer>();
                Publish(PanoramaPhase.Loading);
                _renderer.Begin(definition.Source, _yaw, _viewer, result => OnRenderCompleted(session, generation, result));
                return PanoramaResult.Success();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Diagnose("PANORAMA_RENDER_FAILED", $"Renderer setup failed: {exception.Message}", "open");
                ReleaseRuntime();
                Publish(PanoramaPhase.Failed, new UserFault("360 全景无法显示"));
                return PanoramaResult.Failure(PanoramaFailureCode.RenderFailed, "panorama.renderer.create_failed");
            }
        }

        public PanoramaResult Dispatch(SessionToken session, PanoramaIntent intent)
        {
            if (_renderer == null)
                return PanoramaResult.Failure(PanoramaFailureCode.NotOpen, "panorama.not_open");
            if (!session.Equals(_session))
                return PanoramaResult.Failure(PanoramaFailureCode.StaleSession, "panorama.stale_session");

            switch (intent.Kind)
            {
                case PanoramaIntentKind.RotateByDegrees:
                    if (!IsFinite(intent.Degrees))
                        return PanoramaResult.Failure(PanoramaFailureCode.InvalidIntent, "panorama.rotation.invalid");
                    _yaw = NormalizeYaw(_yaw + intent.Degrees);
                    _renderer.SetYaw(_yaw);
                    Publish(_renderer.IsReady ? PanoramaPhase.Active : PanoramaPhase.Loading);
                    return PanoramaResult.Success();
                case PanoramaIntentKind.ResetView:
                    _yaw = _initialYaw;
                    _renderer.SetYaw(_yaw);
                    Publish(_renderer.IsReady ? PanoramaPhase.Active : PanoramaPhase.Loading);
                    return PanoramaResult.Success();
                default:
                    return PanoramaResult.Failure(PanoramaFailureCode.InvalidIntent, "panorama.intent.unknown");
            }
        }

        public PanoramaResult Close(SessionToken session)
        {
            if (_renderer == null && _surface == null) return PanoramaResult.Success();
            if (!session.Equals(_session))
                return PanoramaResult.Failure(PanoramaFailureCode.StaleSession, "panorama.stale_session");

            ++_generation;
            Diagnose("PANORAMA_CLOSED", "Panorama closed.", "close");
            ReleaseRuntime();
            Publish(PanoramaPhase.Closed);
            return PanoramaResult.Success();
        }

        public IDisposable Observe(IPanoramaStateSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            return _states.Observe(sink.Publish);
        }

        public void Dispose()
        {
            ++_generation;
            ReleaseRuntime();
            _states.Dispose();
            _session = default;
        }

        void OnRenderCompleted(SessionToken session, int generation, PanoramaRenderResult result)
        {
            if (generation != _generation || !session.Equals(_session) || _renderer == null) return;
            if (result.Succeeded)
            {
                Publish(PanoramaPhase.Active);
                return;
            }

            var fault = new UserFault(result.Failure == PanoramaRenderFailure.UnsupportedResource
                ? "360 全景资源格式不受支持"
                : "360 全景加载失败");
            Diagnose("PANORAMA_RENDER_FAILED", $"Renderer completed with {result.Failure}.", "render");
            Publish(PanoramaPhase.Failed, fault);
        }

        void ReleaseRuntime()
        {
            if (_renderer != null)
            {
                _renderer.Release();
                _renderer = null;
            }
            if (_instance != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_instance);
                else UnityEngine.Object.DestroyImmediate(_instance);
                _instance = null;
            }
            _surface?.Dispose();
            _surface = null;
            _source = null;
        }

        void Diagnose(string code, string message, string stage)
        {
            if (_diagnostics == null) return;
            var source = _source;
            var sourceKey = source == null ? string.Empty : source.Kind.ToString();
            var assetPath = source?.StreamingAssetsPath ?? source?.Texture?.name ?? string.Empty;
            _diagnostics.Invoke(new DiagnosticEvent(
                code,
                message,
                "Panorama",
                stage,
                DateTimeOffset.UtcNow,
                sessionToken: _session,
                sourceKey: sourceKey,
                assetPath: assetPath));
        }

        void Publish(PanoramaPhase phase, UserFault fault = default)
        {
            var state = new PanoramaState(_session, ++_version, phase, _yaw, fault);
            _states.Publish(state);
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static float NormalizeYaw(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }
    }
}
