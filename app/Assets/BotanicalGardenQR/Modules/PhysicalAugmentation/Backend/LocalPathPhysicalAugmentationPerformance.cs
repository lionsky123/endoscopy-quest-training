using System;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Backend
{
    public abstract class PhysicalAugmentationPerformanceBehaviour : MonoBehaviour
    {
        PhysicalAugmentationOwnedMedia _ownedMedia;
        bool _ownedMediaResolved;

        public event Action<long, bool, string> Completed;
        public abstract void ResetPerformance();
        public abstract void Play(long generation);
        public abstract void StopPerformance();

        protected void Complete(long generation, bool succeeded, string diagnosticTag = null)
            => Completed?.Invoke(generation, succeeded, diagnosticTag ?? string.Empty);

        protected void ResetOwnedMedia() => ResolveOwnedMedia()?.ResetOwnedMedia();
        protected void PlayOwnedMedia() => ResolveOwnedMedia()?.PlayOwnedMedia();
        protected void StopOwnedMedia() => ResolveOwnedMedia()?.StopOwnedMedia();

        PhysicalAugmentationOwnedMedia ResolveOwnedMedia()
        {
            if (_ownedMediaResolved) return _ownedMedia;
            _ownedMedia = GetComponent<PhysicalAugmentationOwnedMedia>();
            _ownedMediaResolved = true;
            return _ownedMedia;
        }

        protected virtual void OnDestroy()
        {
            StopOwnedMedia();
            Completed = null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class LocalPathPhysicalAugmentationPerformance : PhysicalAugmentationPerformanceBehaviour
    {
        [SerializeField] Transform _revealRoot;
        [SerializeField] Transform _movingRoot;
        [SerializeField] Transform[] _localWaypoints = Array.Empty<Transform>();
        [SerializeField, Min(1)] int _holdWaypointIndex = 1;
        [SerializeField, Min(0.1f)] float _revealDurationSeconds = 1.2f;
        [SerializeField, Min(0.1f)] float _approachDurationSeconds = 2.2f;
        [SerializeField, Min(0f)] float _holdDurationSeconds = 1.4f;
        [SerializeField, Min(0.1f)] float _departDurationSeconds = 2f;
        Vector3 _revealScale;
        long _generation;
        float _startedAt;
        bool _initialized;
        bool _playing;

        public override void ResetPerformance()
        {
            ResetOwnedMedia();
            EnsureInitialized();
            _playing = false;
            _generation = 0;
            if (_revealRoot != null) _revealRoot.localScale = Vector3.zero;
            if (_movingRoot != null)
            {
                ApplyWaypointPose(0);
                _movingRoot.gameObject.SetActive(false);
            }
        }

        public override void Play(long generation)
        {
            if (generation <= 0) throw new ArgumentOutOfRangeException(nameof(generation));
            ValidateAuthoring();
            ResetPerformance();
            _generation = generation;
            _startedAt = Time.unscaledTime;
            _movingRoot.gameObject.SetActive(true);
            Present(0f);
            PlayOwnedMedia();
            _playing = true;
        }

        public override void StopPerformance()
        {
            StopOwnedMedia();
            if (!_playing) return;
            ResetPerformance();
        }

        void Update()
        {
            if (!_playing) return;
            var elapsed = Mathf.Max(0f, Time.unscaledTime - _startedAt);
            var total = _approachDurationSeconds + _holdDurationSeconds + _departDurationSeconds;
            Present(elapsed);
            if (elapsed < total) return;
            var generation = _generation;
            _playing = false;
            Complete(generation, true);
        }

        void Present(float elapsed)
        {
            var reveal = Smooth01(elapsed / Mathf.Max(0.1f, _revealDurationSeconds));
            _revealRoot.localScale = _revealScale * reveal;

            if (elapsed <= _approachDurationSeconds)
            {
                SamplePath(0, _holdWaypointIndex, elapsed / _approachDurationSeconds);
                return;
            }
            elapsed -= _approachDurationSeconds;
            if (elapsed <= _holdDurationSeconds)
            {
                ApplyWaypointPose(_holdWaypointIndex);
                return;
            }
            elapsed -= _holdDurationSeconds;
            SamplePath(
                _holdWaypointIndex,
                _localWaypoints.Length - 1,
                elapsed / _departDurationSeconds);
        }

        void SamplePath(int first, int last, float progress)
        {
            if (last <= first)
            {
                ApplyWaypointPose(first);
                return;
            }
            var scaled = Mathf.Clamp01(progress) * (last - first);
            var segment = Mathf.Min(Mathf.FloorToInt(scaled), last - first - 1);
            var from = first + segment;
            var localProgress = Smooth01(scaled - segment);
            var left = _localWaypoints[from];
            var right = _localWaypoints[from + 1];
            _movingRoot.localPosition = Vector3.LerpUnclamped(
                left.localPosition,
                right.localPosition,
                localProgress);
            _movingRoot.localRotation = Quaternion.SlerpUnclamped(
                left.localRotation,
                right.localRotation,
                localProgress);
        }

        void ApplyWaypointPose(int index)
        {
            if (_movingRoot == null || _localWaypoints == null ||
                index < 0 || index >= _localWaypoints.Length || _localWaypoints[index] == null)
                return;
            _movingRoot.SetLocalPositionAndRotation(
                _localWaypoints[index].localPosition,
                _localWaypoints[index].localRotation);
        }

        void EnsureInitialized()
        {
            if (_initialized) return;
            if (_revealRoot != null) _revealScale = _revealRoot.localScale;
            _initialized = true;
        }

        void ValidateAuthoring()
        {
            EnsureInitialized();
            if (_revealRoot == null || _movingRoot == null || _localWaypoints == null ||
                _localWaypoints.Length < 3 || _holdWaypointIndex <= 0 ||
                _holdWaypointIndex >= _localWaypoints.Length - 1)
                throw new InvalidOperationException("Local path performance authoring is incomplete.");
            for (var index = 0; index < _localWaypoints.Length; index++)
                if (_localWaypoints[index] == null || _localWaypoints[index].parent != transform)
                    throw new InvalidOperationException("Local path waypoints must be direct children of the performance root.");
            if (_revealRoot.parent != transform || _movingRoot.parent != transform)
                throw new InvalidOperationException("Reveal and moving roots must be direct children of the performance root.");
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }

    internal sealed class PrefabPhysicalAugmentationPerformanceFactory : IPhysicalAugmentationPerformanceFactory
    {
        readonly Transform _root;
        public PrefabPhysicalAugmentationPerformanceFactory(Transform root)
            => _root = root != null ? root : throw new ArgumentNullException(nameof(root));

        public bool TryPrepare(
            PhysicalAugmentationDefinition definition,
            Pose resolvedPose,
            float uniformScale,
            long generation,
            out IPhysicalAugmentationPerformanceLease lease,
            out string diagnosticTag)
        {
            lease = null;
            diagnosticTag = string.Empty;
            if (definition == null || definition.PerformancePrefab == null ||
                definition.DepthProxyPrefab == null || !(uniformScale > 0f))
            {
                diagnosticTag = "physical_augmentation.performance_definition_invalid";
                return false;
            }

            GameObject instanceRoot = null;
            try
            {
                instanceRoot = new GameObject("PhysicalAugmentationPerformanceInstance");
                instanceRoot.transform.SetParent(_root, true);
                instanceRoot.transform.SetPositionAndRotation(resolvedPose.position, resolvedPose.rotation);
                instanceRoot.transform.localScale = Vector3.one * uniformScale;
                instanceRoot.SetActive(false);
                var performance = UnityEngine.Object.Instantiate(
                    definition.PerformancePrefab,
                    instanceRoot.transform,
                    false);
                UnityEngine.Object.Instantiate(
                    definition.DepthProxyPrefab,
                    instanceRoot.transform,
                    false);
                var behaviour = performance.GetComponentInChildren<PhysicalAugmentationPerformanceBehaviour>(true);
                if (behaviour == null)
                    throw new InvalidOperationException("Performance prefab has no PhysicalAugmentationPerformanceBehaviour.");
                lease = new PrefabPhysicalAugmentationPerformanceLease(
                    instanceRoot,
                    behaviour,
                    generation);
                return true;
            }
            catch (Exception)
            {
                if (instanceRoot != null) Destroy(instanceRoot);
                diagnosticTag = "physical_augmentation.performance_prepare_failed";
                return false;
            }
        }

        static void Destroy(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }

    internal sealed class PrefabPhysicalAugmentationPerformanceLease : IPhysicalAugmentationPerformanceLease
    {
        GameObject _root;
        PhysicalAugmentationPerformanceBehaviour _behaviour;
        Action<PhysicalAugmentationPerformanceCompletion> _completed;
        readonly long _generation;
        bool _playing;

        public PrefabPhysicalAugmentationPerformanceLease(
            GameObject root,
            PhysicalAugmentationPerformanceBehaviour behaviour,
            long generation)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour));
            _generation = generation;
            _behaviour.Completed += HandleCompleted;
        }

        public void Play(Action<PhysicalAugmentationPerformanceCompletion> completed)
        {
            if (_root == null || _behaviour == null || _playing)
                throw new InvalidOperationException("Performance lease cannot play.");
            _completed = completed ?? throw new ArgumentNullException(nameof(completed));
            _playing = true;
            _root.SetActive(true);
            _behaviour.ResetPerformance();
            _behaviour.Play(_generation);
        }

        public void Stop()
        {
            _playing = false;
            try { _behaviour?.StopPerformance(); } catch (Exception) { }
            if (_root != null) _root.SetActive(false);
        }

        public void Dispose()
        {
            Stop();
            if (_behaviour != null) _behaviour.Completed -= HandleCompleted;
            _behaviour = null;
            _completed = null;
            var root = _root;
            _root = null;
            if (root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }

        void HandleCompleted(long generation, bool succeeded, string diagnosticTag)
        {
            if (!_playing || generation != _generation) return;
            _playing = false;
            try { _behaviour?.StopPerformance(); } catch (Exception) { }
            var completed = _completed;
            _completed = null;
            completed?.Invoke(new PhysicalAugmentationPerformanceCompletion(succeeded, diagnosticTag));
        }
    }
}
