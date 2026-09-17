using System;
using BotanicalGardenQR.PhysicalAugmentation.Contracts;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.PhysicalAugmentation
{
    public sealed class PhysicalAugmentationContractTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" point")]
        [TestCase("point ")]
        public void PointId_RejectsMissingOrNonCanonicalValues(string value)
            => Assert.Throws<ArgumentException>(() => new PhysicalAugmentationPointId(value));

        [Test]
        public void DefinitionCatalog_RejectsDuplicatePointIds()
        {
            var assets = CreatePrefabSet();
            try
            {
                var definition = CreateDefinition("cactus_flower", assets);
                Assert.Throws<ArgumentException>(() =>
                    new PhysicalAugmentationDefinitionCatalog(new[] { definition, definition }));
            }
            finally
            {
                DestroyPrefabSet(assets);
            }
        }

        [Test]
        public void DefinitionCatalog_RejectsDuplicateInstallationAnchorNumbers()
        {
            var assets = CreatePrefabSet();
            try
            {
                var first = CreateDefinition("cactus_flower", assets);
                var second = CreateDefinition("bat_roost", assets);
                Assert.Throws<ArgumentException>(() =>
                    new PhysicalAugmentationDefinitionCatalog(new[] { first, second }));
            }
            finally
            {
                DestroyPrefabSet(assets);
            }
        }

        [Test]
        public void PointState_RequiresStablePoseBeforeActivation()
        {
            Assert.Throws<ArgumentException>(() => new PhysicalAugmentationPointState(
                new PhysicalAugmentationPointId("cactus_flower"),
                PhysicalAugmentationLocalizationPhase.Stabilizing,
                1,
                true,
                Pose.identity,
                1f,
                PhysicalAugmentationPointActivityPhase.Available));
        }

        [Test]
        public void DefinitionUsesIdentityRealityTransformByDefaultAndAcceptsCustomValues()
        {
            var assets = CreatePrefabSet();
            try
            {
                var defaults = CreateDefinition("default_transform", assets);
                var custom = new PhysicalAugmentationDefinition(
                    new PhysicalAugmentationPointId("custom_transform"),
                    "Custom",
                    2,
                    assets[0], assets[1],
                    0.6f, 0.015f, 2f, 0.35f, 0.03f, 5f,
                    0.5f, 2f,
                    PhysicalAugmentationOcclusionPolicy.RequireEnvironmentDepthAndProxy,
                    new Vector3(10f, 20f, 30f),
                    1.25f);

                Assert.That(Quaternion.Angle(defaults.ModelRotationInAnchorSpace, Quaternion.identity), Is.LessThan(0.01f));
                Assert.That(defaults.ModelUniformScale, Is.EqualTo(1f));
                Assert.That(
                    Quaternion.Angle(custom.ModelRotationInAnchorSpace, Quaternion.Euler(10f, 20f, 30f)),
                    Is.LessThan(0.01f));
                Assert.That(custom.ModelUniformScale, Is.EqualTo(1.25f));
            }
            finally
            {
                DestroyPrefabSet(assets);
            }
        }

        [Test]
        public void DefinitionRejectsInvalidRealityScale()
        {
            var assets = CreatePrefabSet();
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new PhysicalAugmentationDefinition(
                    new PhysicalAugmentationPointId("invalid_scale"),
                    "Invalid",
                    2,
                    assets[0], assets[1],
                    0.6f, 0.015f, 2f, 0.35f, 0.03f, 5f,
                    0.5f, 2f,
                    PhysicalAugmentationOcclusionPolicy.RequireEnvironmentDepthAndProxy,
                    Vector3.zero,
                    0f));
            }
            finally
            {
                DestroyPrefabSet(assets);
            }
        }

        [Test]
        public void DefinitionDoesNotExposeRetiredSpatialCueOrCalibrationPreviewPolicy()
        {
            var retiredProperties = new[]
            {
                "CalibrationPreviewPrefab",
                "VisitorCuePrefab",
                "ActivationDistanceMeters",
                "FocusConeDegrees",
                "DwellSeconds"
            };
            foreach (var propertyName in retiredProperties)
                Assert.That(typeof(PhysicalAugmentationDefinition).GetProperty(propertyName),
                    Is.Null, propertyName);
        }

        [Test]
        public void PublicContracts_DoNotExposeUuidOrOvrTypes()
        {
            var assembly = typeof(IPhysicalAugmentationController).Assembly;
            foreach (var type in assembly.GetExportedTypes())
            {
                Assert.That(type.FullName, Does.Not.Contain("Guid"));
                Assert.That(type.FullName, Does.Not.Contain("Uuid"));
                Assert.That(type.FullName, Does.Not.Contain("OVR"));
                foreach (var member in type.GetMembers())
                {
                    Assert.That(member.ToString(), Does.Not.Contain("System.Guid"), $"{type.FullName}.{member.Name}");
                    Assert.That(member.ToString(), Does.Not.Contain("OVR"), $"{type.FullName}.{member.Name}");
                }
            }
        }

        static GameObject[] CreatePrefabSet()
            => new[]
            {
                new GameObject("Performance"),
                new GameObject("Proxy")
            };

        static PhysicalAugmentationDefinition CreateDefinition(string pointId, GameObject[] assets)
            => new PhysicalAugmentationDefinition(
                new PhysicalAugmentationPointId(pointId),
                "仙人掌花",
                1,
                assets[0],
                assets[1],
                0.6f,
                0.015f,
                2f,
                0.35f,
                0.03f,
                5f,
                0.5f,
                2f,
                PhysicalAugmentationOcclusionPolicy.RequireEnvironmentDepthAndProxy);

        static void DestroyPrefabSet(GameObject[] assets)
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
