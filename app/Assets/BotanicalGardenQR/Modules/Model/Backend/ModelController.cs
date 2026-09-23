using System;
using System.Threading;
using System.Threading.Tasks;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Model.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Model.Backend
{
    internal sealed class ModelController : MonoBehaviour, IModelController
    {
        ModelImplementationSelector _selector;
        StateChannel<ModelState> _states;

        CancellationTokenSource _loadCancellation;
        Task _activeLoadTask;
        ModelLoadResult _loaded;
        IModelDriver _driver;
        ModelSurfaceLease _surface;
        ModelPresentationSpec _presentation;
        GameObject _runtimeRootObject;
        Transform _motionRoot;
        Action<DiagnosticEvent> _diagnostics;
        SessionToken _session;
        uint _generation;
        long _version;
        bool _autoMotionActive;
        ModelState _state;

        public void Initialize(ModelImplementationSelector selector, Action<DiagnosticEvent> diagnostics = null)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _diagnostics = diagnostics;
            _state = new ModelState(default, 0, ModelPhase.Closed, false, false, false, false, false);
            _states = StateChannel<ModelState>.ForCurrentThread(_state, state => state.Version);
        }

        public ModelResult Open(SessionToken session, ModelDefinition definition, ModelSurfaceLease surface)
        {
            if (_selector == null)
                return ModelResult.Failure(ModelFailureCode.InvalidDefinition, "model.controller.uninitialized");
            if (!session.IsValid || definition == null)
                return ModelResult.Failure(ModelFailureCode.InvalidDefinition, "model.definition.invalid");
            if (surface == null || surface.ContentRoot == null)
                return ModelResult.Failure(ModelFailureCode.SurfaceUnavailable, "model.surface.unavailable");
            if (_surface != null)
                return ModelResult.Failure(ModelFailureCode.InvalidIntent, "model.already_open");

            var loader = _selector.SelectLoader(definition.Source);
            if (loader == null)
                return ModelResult.Failure(ModelFailureCode.UnsupportedSource, "model.source.unsupported");

            _session = session;
            _surface = surface;
            _presentation = definition.Presentation;
            _driver = _selector.SelectDriver(definition.Animation);
            _loadCancellation = new CancellationTokenSource();
            _autoMotionActive = false;
            var generation = ++_generation;

            _runtimeRootObject = new GameObject("ModelRuntimeRoot");
            _runtimeRootObject.transform.SetParent(surface.ContentRoot, false);
            _runtimeRootObject.SetActive(false);
            _motionRoot = new GameObject("MotionRoot").transform;
            _motionRoot.SetParent(_runtimeRootObject.transform, false);
            Publish(ModelPhase.Loading);
            _activeLoadTask = CompleteOpenAsync(loader, definition.Source, _motionRoot, session, generation, _loadCancellation.Token);
            return ModelResult.Success();
        }

        public ModelResult Dispatch(SessionToken session, ModelIntent intent)
        {
            if (_surface == null)
                return ModelResult.Failure(ModelFailureCode.NotOpen, "model.not_open");
            if (session != _session)
                return ModelResult.Failure(ModelFailureCode.StaleSession, "model.stale_session");
            if (_loaded == null || !_loaded.Succeeded || _driver == null)
                return ModelResult.Failure(ModelFailureCode.InvalidIntent, "model.not_ready");

            switch (intent.Kind)
            {
                case ModelIntentKind.ToggleAutoMotion:
                    if (!_presentation.AllowRotation)
                        return ModelResult.Failure(ModelFailureCode.InvalidIntent, "model.motion.unavailable");
                    _autoMotionActive = !_autoMotionActive;
                    Publish(ModelPhase.Ready);
                    return ModelResult.Success();

                case ModelIntentKind.ResetView:
                    _autoMotionActive = false;
                    ResetMotionRoot();
                    ApplyPresentation(_loaded.Instance);
                    Publish(ModelPhase.Ready);
                    return ModelResult.Success();

                case ModelIntentKind.PlayAnimation:
                    var played = _driver.PlayAnimation();
                    if (!played.Succeeded) return played;
                    Publish(ModelPhase.Ready);
                    return played;

                default:
                    return ModelResult.Failure(ModelFailureCode.InvalidIntent, "model.intent.unknown");
            }
        }

        public ModelResult Close(SessionToken session)
        {
            if (_surface == null)
                return ModelResult.Success();
            if (session != _session)
                return ModelResult.Failure(ModelFailureCode.StaleSession, "model.stale_session");

            ++_generation;
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
            _loadCancellation = null;
            _activeLoadTask = null;
            StopDriver("close");
            DisposeQuietly(_driver, "MODEL_RELEASE_FAILED", "close");
            _driver = null;
            DisposeQuietly(_loaded, "MODEL_RELEASE_FAILED", "close");
            _loaded = null;
            if (_runtimeRootObject != null)
                Destroy(_runtimeRootObject);
            _runtimeRootObject = null;
            _motionRoot = null;
            DisposeQuietly(_surface, "MODEL_RELEASE_FAILED", "close");
            _surface = null;
            _presentation = null;
            _autoMotionActive = false;
            Publish(ModelPhase.Closed);
            return ModelResult.Success();
        }

        public IDisposable Observe(IModelStateSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (_states == null) throw new InvalidOperationException("Model controller is not initialized.");
            return _states.Observe(sink.Publish);
        }

        void Update()
        {
            if (_loaded == null || !_loaded.Succeeded || _driver == null || _motionRoot == null)
                return;

            if (_autoMotionActive)
            {
                var angle = Time.unscaledTime * _presentation.RotationDegreesPerSecond;
                _motionRoot.localRotation = Quaternion.Euler(0f, angle, 0f);
                var offset = _presentation.BobAmplitude <= 0f
                    ? 0f
                    : Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * _presentation.BobFrequency) * _presentation.BobAmplitude;
                _motionRoot.localPosition = new Vector3(0f, offset, 0f);
            }
            else
            {
                ResetMotionRoot();
            }

            if (_driver.Tick(Time.unscaledDeltaTime))
                Publish(ModelPhase.Ready);
        }

        void OnDestroy()
        {
            ++_generation;
            var loadCancellation = _loadCancellation;
            _loadCancellation = null;
            _activeLoadTask = null;
            loadCancellation?.Cancel();
            loadCancellation?.Dispose();
            StopDriver("destroy");
            var driver = _driver;
            _driver = null;
            DisposeQuietly(driver, "MODEL_RELEASE_FAILED", "destroy");
            var loaded = _loaded;
            _loaded = null;
            DisposeQuietly(loaded, "MODEL_RELEASE_FAILED", "destroy");
            var runtimeRoot = _runtimeRootObject;
            _runtimeRootObject = null;
            _motionRoot = null;
            DestroyOwnedObject(runtimeRoot);
            var surface = _surface;
            _surface = null;
            DisposeQuietly(surface, "MODEL_RELEASE_FAILED", "destroy");
            _presentation = null;
            _autoMotionActive = false;
            var states = _states;
            _states = null;
            states?.Dispose();
        }

        static void DestroyOwnedObject(GameObject ownedObject)
        {
            if (ownedObject == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(ownedObject);
                return;
            }
#endif
            Destroy(ownedObject);
        }

        async Task CompleteOpenAsync(
            IModelLoader loader,
            ModelSource source,
            Transform parent,
            SessionToken session,
            uint generation,
            CancellationToken token)
        {
            ModelLoadResult result;
            try
            {
                result = await loader.LoadAsync(source, parent, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (generation == _generation && session == _session && _surface != null)
                {
                    _runtimeRootObject?.SetActive(false);
                    Diagnose("MODEL_LOAD_FAILED", $"Model loader threw {exception.GetType().Name}: {exception.Message}", "load");
                    Publish(ModelPhase.Failed, new UserFault("模型加载失败"));
                }
                return;
            }

            if (generation != _generation || session != _session || token.IsCancellationRequested || _surface == null)
            {
                DisposeQuietly(result, "MODEL_RELEASE_FAILED", "stale_result");
                return;
            }
            if (result == null || !result.Succeeded)
            {
                DisposeQuietly(result, "MODEL_RELEASE_FAILED", "load");
                _runtimeRootObject?.SetActive(false);
                Diagnose("MODEL_LOAD_FAILED", "Model loader returned an unsuccessful result.", "load");
                Publish(ModelPhase.Failed, new UserFault("模型加载失败"));
                return;
            }

            try
            {
                _loaded = result;
                DisableImportedComponents(result.Instance);
                ApplyPresentation(result.Instance);
                var attach = _driver.Attach(result.Instance);
                if (!attach.Succeeded)
                {
                    _loaded = null;
                    DisposeQuietly(result, "MODEL_RELEASE_FAILED", "attach");
                    DisposeQuietly(_driver, "MODEL_RELEASE_FAILED", "attach");
                    _driver = null;
                    _runtimeRootObject?.SetActive(false);
                    Diagnose("MODEL_OPEN_FAILED", "Model animation driver could not attach to the loaded instance.", "attach");
                    Publish(ModelPhase.Failed, new UserFault("模型动画不可用"));
                    return;
                }

                _runtimeRootObject?.SetActive(true);
                Publish(ModelPhase.Ready);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (ReferenceEquals(_loaded, result)) _loaded = null;
                DisposeQuietly(result, "MODEL_RELEASE_FAILED", "open_cleanup");
                DisposeQuietly(_driver, "MODEL_RELEASE_FAILED", "open_cleanup");
                _driver = null;
                _runtimeRootObject?.SetActive(false);
                Diagnose("MODEL_OPEN_FAILED", $"Model post-load setup failed: {exception.Message}", "open");
                Publish(ModelPhase.Failed, new UserFault("模型加载失败"));
            }
        }

        void ApplyPresentation(GameObject instance)
        {
            var transform = instance.transform;
            transform.localPosition = _presentation.LocalPosition;
            transform.localRotation = Quaternion.Euler(_presentation.LocalEulerAngles);
            transform.localScale = _presentation.LocalScale;

            if (_presentation.MaterialOverride == null)
                return;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                    materials[index] = _presentation.MaterialOverride;
                renderer.sharedMaterials = materials;
            }
        }

        void ResetMotionRoot()
        {
            if (_motionRoot == null) return;
            _motionRoot.localPosition = Vector3.zero;
            _motionRoot.localRotation = Quaternion.identity;
        }

        static void DisableImportedComponents(GameObject instance)
        {
            foreach (var camera in instance.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (var listener in instance.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (var light in instance.GetComponentsInChildren<Light>(true)) light.enabled = false;
        }

        void StopDriver(string stage)
        {
            try
            {
                _driver?.StopAnimation();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Diagnose("MODEL_RELEASE_FAILED", $"Model animation stop failed: {exception.Message}", stage);
            }
        }

        void DisposeQuietly(IDisposable disposable, string code, string stage)
        {
            if (disposable == null) return;
            try
            {
                disposable.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Diagnose(code, $"Model resource release failed: {exception.Message}", stage);
            }
        }

        void Diagnose(string code, string message, string stage)
            => _diagnostics?.Invoke(new DiagnosticEvent(
                code,
                message,
                "Model",
                stage,
                DateTimeOffset.UtcNow,
                sessionToken: _session,
                sourceKey: "model"));

        void Publish(ModelPhase phase, UserFault fault = default)
        {
            _state = new ModelState(
                _session,
                ++_version,
                phase,
                phase == ModelPhase.Ready && _presentation != null && _presentation.AllowRotation,
                phase == ModelPhase.Ready && _autoMotionActive,
                phase == ModelPhase.Ready,
                phase == ModelPhase.Ready && _driver != null && _driver.CanPlayAnimation,
                phase == ModelPhase.Ready && _driver != null && _driver.IsAnimationPlaying,
                fault);
            _states.Publish(_state);
        }
    }
}
