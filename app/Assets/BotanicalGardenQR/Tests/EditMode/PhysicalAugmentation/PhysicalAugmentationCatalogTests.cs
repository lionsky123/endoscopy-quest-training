using System.Reflection;
using BotanicalGardenQR.Configuration.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.PhysicalAugmentation
{
    public sealed class PhysicalAugmentationCatalogTests
    {
        PhysicalAugmentationCatalogAuthoringAsset _catalog;

        [SetUp]
        public void SetUp() => _catalog = ScriptableObject.CreateInstance<PhysicalAugmentationCatalogAuthoringAsset>();

        [TearDown]
        public void TearDown()
        {
            if (_catalog != null) Object.DestroyImmediate(_catalog);
        }

        [Test]
        public void EmptyCatalog_IsAValidDisabledCapability()
        {
            Assert.That(_catalog.TryBuild(out var catalog, out var error), Is.True, error);
            Assert.That(catalog.Definitions, Is.Empty);
        }

        [Test]
        public void DuplicatePointIds_AreRejectedBeforeAssetLoading()
        {
            SetRecords(CreateDisabledRecord("cactus_flower"), CreateDisabledRecord("cactus_flower"));

            Assert.That(_catalog.TryBuild(out _, out var error), Is.False);
            Assert.That(error, Does.Contain("duplicated"));
        }

        [Test]
        public void EnabledPointWithoutClosedPrefabSet_IsRejected()
        {
            var record = CreateDisabledRecord("cactus_flower");
            SetPrivate(record, "_enabled", true);
            SetRecords(record);

            Assert.That(_catalog.TryBuild(out _, out var error), Is.False);
            Assert.That(error, Does.Contain("prefab root"));
        }

        [Test]
        public void DuplicateEnabledInstallationAnchorNumbers_AreRejectedBeforeAssetLoading()
        {
            var first = CreateDisabledRecord("cactus_flower");
            var second = CreateDisabledRecord("bat_roost");
            SetPrivate(first, "_enabled", true);
            SetPrivate(second, "_enabled", true);
            SetPrivate(first, "_installationAnchorNumber", 3);
            SetPrivate(second, "_installationAnchorNumber", 3);
            SetRecords(first, second);

            Assert.That(_catalog.TryBuild(out _, out var error), Is.False);
            Assert.That(error, Does.Contain("installation anchor number '3' is duplicated"));
        }

        [Test]
        public void EnabledPointRequiresPositiveInstallationAnchorNumber()
        {
            var record = CreateDisabledRecord("cactus_flower");
            SetPrivate(record, "_enabled", true);
            SetPrivate(record, "_installationAnchorNumber", 0);
            SetRecords(record);

            Assert.That(_catalog.TryBuild(out _, out var error), Is.False);
            Assert.That(error, Does.Contain("positive installation anchor number"));
        }

        static PhysicalAugmentationPointRecord CreateDisabledRecord(string pointId)
        {
            var record = new PhysicalAugmentationPointRecord();
            SetPrivate(record, "_enabled", false);
            SetPrivate(record, "_pointId", pointId);
            return record;
        }

        void SetRecords(params PhysicalAugmentationPointRecord[] records)
        {
            SetPrivate(_catalog, "_points", records);
        }

        static void SetPrivate(object target, string fieldName, object value)
            => target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
    }
}
