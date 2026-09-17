using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using BotanicalGardenQR.PhysicalAugmentation.Installation;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Backend
{
    /// <summary>
    /// Visitor-only owner of Meta anchor leases. Created objects contain no visual or interaction components.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MetaPhysicalAnchorLocator : MonoBehaviour, IPhysicalAugmentationLocator
    {
        PhysicalAnchorLocalizationCoordinator _coordinator;
        event Action<IReadOnlyList<PhysicalAugmentationPointState>> _stateChanged;

        event Action<IReadOnlyList<PhysicalAugmentationPointState>> IPhysicalAugmentationLocator.StateChanged
        {
            add => _stateChanged += value;
            remove => _stateChanged -= value;
        }

        void Awake() => EnsureCoordinator();

        void Update() => _coordinator?.Tick(Time.unscaledTime);

        void OnDestroy()
        {
            _coordinator?.Dispose();
            _coordinator = null;
            _stateChanged = null;
        }

        bool IPhysicalAugmentationLocator.TryStart(
            IReadOnlyList<PhysicalAugmentationDefinition> definitions,
            long generation,
            out string diagnosticTag)
        {
            EnsureCoordinator();
            return _coordinator.TryStart(definitions, generation, out diagnosticTag);
        }

        void IPhysicalAugmentationLocator.Stop(long generation)
            => _coordinator?.Stop(generation);

        internal IPhysicalAugmentationLocator Locator
        {
            get
            {
                EnsureCoordinator();
                return this;
            }
        }

        void EnsureCoordinator()
        {
            if (_coordinator != null) return;
            var store = PhysicalAnchorBindingStore.OpenDefault();
            _coordinator = new PhysicalAnchorLocalizationCoordinator(
                store,
                new MetaPhysicalAnchorPlatform(transform));
            _coordinator.StateChanged += states => _stateChanged?.Invoke(states);
        }
    }

    internal sealed class MetaPhysicalAnchorPlatform : IPhysicalAnchorPlatform
    {
        // Meta's default timeout is zero, which means "wait forever". A bounded
        // attempt keeps the visitor action recoverable when an installed anchor is
        // temporarily out of view or the room has not been scanned yet.
        const double AnchorLocalizationTimeoutSeconds = 15.0;
        readonly Transform _owner;

        public MetaPhysicalAnchorPlatform(Transform owner)
        {
            _owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
        }

        public async Task<PhysicalAnchorPlatformLoadResult> LoadBatchAsync(IReadOnlyList<Guid> uuids)
        {
            if (uuids == null || uuids.Count == 0 || uuids.Count > 50)
            {
                Debug.LogWarning(
                    $"[PhysicalAugmentation] Meta load rejected requestCount={uuids?.Count ?? 0}.");
                return new PhysicalAnchorPlatformLoadResult(
                    false,
                    Array.Empty<IPhysicalAnchorPlatformLease>(),
                    "physical_locator.batch_invalid");
            }
            var request = new Guid[uuids.Count];
            for (var index = 0; index < request.Length; index++) request[index] = uuids[index];
            try
            {
                var unbound = new List<OVRSpatialAnchor.UnboundAnchor>(request.Length);
                var load = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(request, unbound);
                if (!load.Success)
                {
                    Debug.LogWarning(
                        $"[PhysicalAugmentation] Meta load failed requestCount={request.Length} status={load.Status}.");
                    return new PhysicalAnchorPlatformLoadResult(
                        false,
                        Array.Empty<IPhysicalAnchorPlatformLease>(),
                        "physical_locator.meta_load_failed");
                }

                var leases = new List<IPhysicalAnchorPlatformLease>(load.Value.Count);
                var localizedCount = 0;
                var skippedLocalizationCount = 0;
                var skippedPoseCount = 0;
                var exceptionCount = 0;
                for (var index = 0; index < load.Value.Count; index++)
                {
                    var anchor = load.Value[index];
                    GameObject root = null;
                    try
                    {
                        if (!anchor.Localized &&
                            !await anchor.LocalizeAsync(AnchorLocalizationTimeoutSeconds))
                        {
                            skippedLocalizationCount++;
                            continue;
                        }
                        localizedCount++;
                        if (!anchor.TryGetPose(out var pose))
                        {
                            skippedPoseCount++;
                            continue;
                        }
                        root = new GameObject("PhysicalAnchorVisitorLease");
                        root.transform.SetParent(_owner, true);
                        root.transform.SetPositionAndRotation(pose.position, pose.rotation);
                        var spatialAnchor = root.AddComponent<OVRSpatialAnchor>();
                        anchor.BindTo(spatialAnchor);
                        DisableVisualAndInteractionComponents(root);
                        leases.Add(new MetaPhysicalAnchorPlatformLease(anchor.Uuid, spatialAnchor));
                    }
                    catch (Exception exception)
                    {
                        // Partial localization failure is represented by the UUID being absent from the result.
                        exceptionCount++;
                        Debug.LogWarning(
                            $"[PhysicalAugmentation] Meta anchor materialization failed exception={exception.GetType().Name}.");
                        if (root != null)
                        {
                            if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                            else UnityEngine.Object.DestroyImmediate(root);
                        }
                    }
                }
                Debug.Log(
                    $"[PhysicalAugmentation] Meta load completed requestCount={request.Length} " +
                    $"unbound={load.Value.Count} localized={localizedCount} leases={leases.Count} " +
                    $"skippedLocalization={skippedLocalizationCount} skippedPose={skippedPoseCount} " +
                    $"exceptions={exceptionCount}.");
                return new PhysicalAnchorPlatformLoadResult(true, leases);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[PhysicalAugmentation] Meta load exception requestCount={request.Length} " +
                    $"exception={exception.GetType().Name}.");
                return new PhysicalAnchorPlatformLoadResult(
                    false,
                    Array.Empty<IPhysicalAnchorPlatformLease>(),
                    "physical_locator.meta_exception");
            }
        }

        static void DisableVisualAndInteractionComponents(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++) renderers[index].enabled = false;
            var canvases = root.GetComponentsInChildren<Canvas>(true);
            for (var index = 0; index < canvases.Length; index++) canvases[index].enabled = false;
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var index = 0; index < colliders.Length; index++) colliders[index].enabled = false;
            var colliders2D = root.GetComponentsInChildren<Collider2D>(true);
            for (var index = 0; index < colliders2D.Length; index++) colliders2D[index].enabled = false;
        }
    }

    internal sealed class MetaPhysicalAnchorPlatformLease : IPhysicalAnchorPlatformLease
    {
        OVRSpatialAnchor _anchor;

        public MetaPhysicalAnchorPlatformLease(Guid uuid, OVRSpatialAnchor anchor)
        {
            Uuid = uuid;
            _anchor = anchor != null ? anchor : throw new ArgumentNullException(nameof(anchor));
        }

        public Guid Uuid { get; }
        public bool IsTracked => _anchor != null && _anchor.IsTracked;

        public bool TryGetPose(out Pose pose)
        {
            pose = default;
            if (!IsTracked) return false;
            pose = new Pose(_anchor.transform.position, _anchor.transform.rotation);
            return true;
        }

        public void Dispose()
        {
            var anchor = _anchor;
            _anchor = null;
            if (anchor == null) return;
            var root = anchor.gameObject;
            if (Application.isPlaying) UnityEngine.Object.Destroy(root);
            else UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
