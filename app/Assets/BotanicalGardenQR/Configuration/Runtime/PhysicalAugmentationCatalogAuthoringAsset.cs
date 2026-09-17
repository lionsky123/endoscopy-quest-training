using System;
using System.Collections.Generic;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Runtime
{
    [CreateAssetMenu(menuName = "Botanical Garden QR/Physical Augmentation Catalog Authoring", fileName = "PhysicalAugmentationCatalog")]
    public sealed class PhysicalAugmentationCatalogAuthoringAsset : ScriptableObject,
        IPhysicalAugmentationDefinitionSource
    {
        [SerializeField] PhysicalAugmentationPointRecord[] _points =
            Array.Empty<PhysicalAugmentationPointRecord>();
        PhysicalAugmentationDefinitionCatalog _runtimeCatalog;

        public IReadOnlyList<PhysicalAugmentationPointRecord> Points =>
            _points ?? Array.Empty<PhysicalAugmentationPointRecord>();

        public IReadOnlyList<PhysicalAugmentationDefinition> Definitions
        {
            get
            {
                EnsureRuntimeCatalog();
                return _runtimeCatalog.Definitions;
            }
        }

        public bool TryGet(
            PhysicalAugmentationPointId pointId,
            out PhysicalAugmentationDefinition definition)
        {
            EnsureRuntimeCatalog();
            return _runtimeCatalog.TryGet(pointId, out definition);
        }

        public bool TryBuild(out PhysicalAugmentationDefinitionCatalog catalog, out string error)
            => PhysicalAugmentationCatalogCompiler.TryBuild(Points, out catalog, out error);

#if UNITY_EDITOR
        public PhysicalAugmentationPointRecord[] CopyPoints()
            => _points == null
                ? Array.Empty<PhysicalAugmentationPointRecord>()
                : (PhysicalAugmentationPointRecord[])_points.Clone();
#endif

        void OnEnable() => _runtimeCatalog = null;

#if UNITY_EDITOR
        void OnValidate() => _runtimeCatalog = null;
#endif

        void EnsureRuntimeCatalog()
        {
            if (_runtimeCatalog != null) return;
            if (!TryBuild(out _runtimeCatalog, out var error))
                throw new InvalidOperationException(error);
        }
    }
}
