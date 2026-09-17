using System;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using Meta.XR.EnvironmentDepth;
using UnityEngine;

namespace BotanicalGardenQR.PhysicalAugmentation.Backend
{
    [DisallowMultipleComponent]
    public sealed class PhysicalAugmentationRuntimeHost : MonoBehaviour
    {
        [SerializeField] MetaPhysicalAnchorLocator _locator;
        [SerializeField] Transform _performanceRoot;
        [SerializeField] EnvironmentDepthManager _environmentDepthManager;

        PhysicalAugmentationController _controller;
        DelegateSuppressionSource _suppression;
        bool _lastSuppressed;
        bool _lastDepthAvailable;

        public IPhysicalAugmentationController Configure(
            IPhysicalAugmentationDefinitionSource definitions,
            Func<bool> isSuppressed)
        {
            if (_controller != null)
                throw new InvalidOperationException("Physical Augmentation Runtime is already configured.");
            if (_locator == null) throw new InvalidOperationException("Physical Augmentation Runtime requires one Meta locator.");
            if (_performanceRoot == null) throw new InvalidOperationException("Physical Augmentation Runtime requires a performance root.");
            _suppression = new DelegateSuppressionSource(isSuppressed);
            var capability = new EnvironmentDepthCapabilityGate(_environmentDepthManager);
            _controller = new PhysicalAugmentationController(
                definitions,
                _locator.Locator,
                _suppression,
                capability,
                new PrefabPhysicalAugmentationPerformanceFactory(_performanceRoot));
            _lastSuppressed = _suppression.Current;
            _lastDepthAvailable = capability.DepthAvailable;
            Debug.Log(
                $"[PhysicalAugmentation] runtime configured depthSupported={EnvironmentDepthManager.IsSupported} " +
                $"managerPresent={_environmentDepthManager != null} " +
                $"managerEnabled={_environmentDepthManager != null && _environmentDepthManager.enabled} " +
                $"depthAvailable={_lastDepthAvailable} suppressed={_lastSuppressed}.");
            return _controller;
        }

        void Update()
        {
            if (_controller == null || _suppression == null) return;
            var suppressed = _suppression.Current;
            var depthSupported = EnvironmentDepthManager.IsSupported;
            var depthAvailable = _environmentDepthManager != null &&
                                 _environmentDepthManager.enabled &&
                                 depthSupported &&
                                 _environmentDepthManager.IsDepthAvailable;
            if (suppressed == _lastSuppressed && depthAvailable == _lastDepthAvailable) return;
            _lastSuppressed = suppressed;
            _lastDepthAvailable = depthAvailable;
            Debug.Log(
                $"[PhysicalAugmentation] external availability changed depthSupported={depthSupported} " +
                $"managerPresent={_environmentDepthManager != null} " +
                $"managerEnabled={_environmentDepthManager != null && _environmentDepthManager.enabled} " +
                $"depthAvailable={depthAvailable} suppressed={suppressed}.");
            _controller.RefreshExternalAvailability();
        }

        void OnDestroy()
        {
            _controller?.Stop();
            _controller = null;
            _suppression = null;
        }
    }

    internal sealed class DelegateSuppressionSource : IPhysicalAugmentationSuppressionSource
    {
        readonly Func<bool> _source;
        public DelegateSuppressionSource(Func<bool> source)
            => _source = source ?? throw new ArgumentNullException(nameof(source));
        public bool Current
        {
            get
            {
                try { return _source(); } catch (Exception) { return true; }
            }
        }
        public bool IsSuppressed(PhysicalAugmentationPointId pointId, out string diagnosticTag)
        {
            var suppressed = Current;
            diagnosticTag = suppressed ? "physical_augmentation.application_suppressed" : string.Empty;
            return suppressed;
        }
    }

    internal sealed class EnvironmentDepthCapabilityGate : IPhysicalAugmentationCapabilityGate
    {
        readonly EnvironmentDepthManager _manager;
        public EnvironmentDepthCapabilityGate(EnvironmentDepthManager manager) => _manager = manager;
        public bool DepthAvailable => _manager != null && _manager.enabled &&
                                      EnvironmentDepthManager.IsSupported && _manager.IsDepthAvailable;

        public bool IsAvailable(PhysicalAugmentationDefinition definition, out string diagnosticTag)
        {
            if (definition == null || definition.DepthProxyPrefab == null)
            {
                diagnosticTag = "physical_augmentation.depth_proxy_missing";
                return false;
            }
            if (definition.OcclusionPolicy == PhysicalAugmentationOcclusionPolicy.AllowProxyFallback)
            {
                diagnosticTag = string.Empty;
                return true;
            }
            if (!DepthAvailable)
            {
                diagnosticTag = "physical_augmentation.environment_depth_unavailable";
                return false;
            }
            diagnosticTag = string.Empty;
            return true;
        }
    }
}
