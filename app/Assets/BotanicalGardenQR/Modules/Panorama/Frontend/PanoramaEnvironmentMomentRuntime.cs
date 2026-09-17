using System;
using System.Collections.Generic;
using BotanicalGardenQR.Panorama.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Panorama.Frontend
{
    internal interface IPanoramaEnvironmentMomentRuntime : IDisposable
    {
        void Activate();
        void Deactivate();
    }

    internal interface IPanoramaEnvironmentMomentRuntimeFactory
    {
        IPanoramaEnvironmentMomentRuntime Create(
            PanoramaEnvironmentMomentDefinition definition,
            Transform parent);
    }

    internal sealed class PanoramaEnvironmentMomentController : IDisposable
    {
        const int MaximumMomentCount = 4;

        readonly PanoramaEnvironmentMomentDefinition[] _definitions;
        readonly IPanoramaEnvironmentMomentRuntime[] _runtimes;
        readonly IPanoramaEnvironmentMomentRuntimeFactory _factory;
        readonly Transform _parent;

        int _activeIndex = -1;
        bool _disposed;

        public PanoramaEnvironmentMomentController(
            IReadOnlyList<PanoramaEnvironmentMomentDefinition> definitions,
            Transform parent,
            IPanoramaEnvironmentMomentRuntimeFactory factory = null)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            if (definitions.Count > MaximumMomentCount)
                throw new ArgumentOutOfRangeException(nameof(definitions), "Panorama supports at most four environment moments.");
            _parent = parent != null ? parent : throw new ArgumentNullException(nameof(parent));
            _factory = factory ?? PrefabPanoramaEnvironmentMomentRuntimeFactory.Instance;
            _definitions = new PanoramaEnvironmentMomentDefinition[definitions.Count];
            _runtimes = new IPanoramaEnvironmentMomentRuntime[definitions.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index] ??
                                 throw new ArgumentException("Environment moments cannot contain null entries.", nameof(definitions));
                if (!ids.Add(definition.MomentId))
                    throw new ArgumentException($"Environment moment ID '{definition.MomentId}' is duplicated.", nameof(definitions));
                _definitions[index] = definition;
            }
        }

        public bool HasMoments => _definitions.Length > 0;
        public PanoramaEnvironmentMomentDefinition ActiveDefinition
            => _activeIndex >= 0 ? _definitions[_activeIndex] : null;

        public PanoramaEnvironmentMomentDefinition Cycle()
        {
            RequireAvailable();
            if (_definitions.Length == 0) return null;
            var next = _activeIndex + 1;
            if (next >= _definitions.Length) next = -1;
            return Apply(next);
        }

        public void Reset()
        {
            if (_disposed || _activeIndex < 0) return;
            Apply(-1);
        }

        PanoramaEnvironmentMomentDefinition Apply(int nextIndex)
        {
            if (_activeIndex >= 0)
            {
                try
                {
                    _runtimes[_activeIndex]?.Deactivate();
                }
                finally
                {
                    _activeIndex = -1;
                }
            }

            if (nextIndex < 0) return null;
            var runtime = _runtimes[nextIndex];
            if (runtime == null)
            {
                runtime = _factory.Create(_definitions[nextIndex], _parent) ??
                          throw new InvalidOperationException("Environment moment factory returned no runtime.");
                _runtimes[nextIndex] = runtime;
            }

            try
            {
                runtime.Activate();
                _activeIndex = nextIndex;
                return _definitions[nextIndex];
            }
            catch
            {
                runtime.Dispose();
                _runtimes[nextIndex] = null;
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            try { Reset(); }
            catch (Exception exception) { Debug.LogException(exception); }
            _activeIndex = -1;
            _disposed = true;
            for (var index = 0; index < _runtimes.Length; index++)
            {
                try { _runtimes[index]?.Dispose(); }
                catch (Exception exception) { Debug.LogException(exception); }
                _runtimes[index] = null;
            }
        }

        void RequireAvailable()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PanoramaEnvironmentMomentController));
        }
    }

    internal sealed class PrefabPanoramaEnvironmentMomentRuntimeFactory : IPanoramaEnvironmentMomentRuntimeFactory
    {
        public static readonly PrefabPanoramaEnvironmentMomentRuntimeFactory Instance =
            new PrefabPanoramaEnvironmentMomentRuntimeFactory();

        PrefabPanoramaEnvironmentMomentRuntimeFactory() { }

        public IPanoramaEnvironmentMomentRuntime Create(
            PanoramaEnvironmentMomentDefinition definition,
            Transform parent)
            => new PrefabPanoramaEnvironmentMomentRuntime(definition, parent);
    }

    internal sealed class PrefabPanoramaEnvironmentMomentRuntime : IPanoramaEnvironmentMomentRuntime
    {
        GameObject _instance;
        readonly IPanoramaEnvironmentMomentEffect[] _effects;
        bool _active;

        public PrefabPanoramaEnvironmentMomentRuntime(
            PanoramaEnvironmentMomentDefinition definition,
            Transform parent)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _instance = UnityEngine.Object.Instantiate(definition.Prefab, parent, false);
            _instance.name = "EnvironmentMoment_" + definition.MomentId;
            _instance.transform.localScale *= definition.Scale;

            var behaviours = _instance.GetComponentsInChildren<MonoBehaviour>(true);
            var effects = new List<IPanoramaEnvironmentMomentEffect>(behaviours.Length);
            for (var index = 0; index < behaviours.Length; index++)
                if (behaviours[index] is IPanoramaEnvironmentMomentEffect effect)
                    effects.Add(effect);
            _effects = effects.ToArray();
            _instance.SetActive(false);
        }

        public void Activate()
        {
            RequireAvailable();
            if (_active) return;
            _instance.SetActive(true);
            _active = true;
            try
            {
                for (var index = 0; index < _effects.Length; index++)
                    _effects[index].Activate();
            }
            catch
            {
                Deactivate();
                throw;
            }
        }

        public void Deactivate()
        {
            if (_instance == null) return;
            if (!_active)
            {
                _instance.SetActive(false);
                return;
            }
            for (var index = 0; index < _effects.Length; index++)
            {
                try { _effects[index].Deactivate(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            _active = false;
            _instance.SetActive(false);
        }

        public void Dispose()
        {
            if (_instance == null) return;
            Deactivate();
            Destroy(_instance);
            _instance = null;
        }

        void RequireAvailable()
        {
            if (_instance == null) throw new ObjectDisposedException(nameof(PrefabPanoramaEnvironmentMomentRuntime));
        }

        static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
