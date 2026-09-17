using System;
using System.Collections.Generic;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.Panorama.Frontend;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Panorama.Tests.EditMode
{
    public sealed class PanoramaEnvironmentMomentTests
    {
        [Test]
        public void Controller_CyclesConfiguredOrderThenOff_WithOnlyOneActiveRuntime()
        {
            var parent = new GameObject("MomentParent");
            var firstPrefab = new GameObject("FirstPrefab");
            var secondPrefab = new GameObject("SecondPrefab");
            var factory = new RecordingFactory();
            try
            {
                var first = Definition("dry-wind", firstPrefab, Color.yellow);
                var second = Definition("night-pollination", secondPrefab, Color.cyan);
                using (var controller = new PanoramaEnvironmentMomentController(
                           new[] { first, second },
                           parent.transform,
                           factory))
                {
                    Assert.That(controller.Cycle(), Is.SameAs(first));
                    Assert.That(factory["dry-wind"].IsActive, Is.True);

                    Assert.That(controller.Cycle(), Is.SameAs(second));
                    Assert.That(factory["dry-wind"].IsActive, Is.False);
                    Assert.That(factory["dry-wind"].DeactivateCount, Is.EqualTo(1));
                    Assert.That(factory["night-pollination"].IsActive, Is.True);

                    Assert.That(controller.Cycle(), Is.Null);
                    Assert.That(factory["night-pollination"].IsActive, Is.False);
                    Assert.That(controller.ActiveDefinition, Is.Null);
                }

                Assert.That(factory["dry-wind"].DisposeCount, Is.EqualTo(1));
                Assert.That(factory["night-pollination"].DisposeCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(firstPrefab);
                UnityEngine.Object.DestroyImmediate(secondPrefab);
            }
        }

        [Test]
        public void Controller_WithNoConfiguredMoments_RemainsOffAndCreatesNothing()
        {
            var parent = new GameObject("MomentParent");
            var factory = new RecordingFactory();
            try
            {
                using (var controller = new PanoramaEnvironmentMomentController(
                           Array.Empty<PanoramaEnvironmentMomentDefinition>(),
                           parent.transform,
                           factory))
                {
                    Assert.That(controller.HasMoments, Is.False);
                    Assert.That(controller.Cycle(), Is.Null);
                    Assert.That(controller.ActiveDefinition, Is.Null);
                }
                Assert.That(factory.CreateCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void Controller_RejectsDuplicateIdsAndMoreThanFourMoments()
        {
            var parent = new GameObject("MomentParent");
            var prefab = new GameObject("MomentPrefab");
            try
            {
                var duplicate = Definition("flowering", prefab, Color.magenta);
                Assert.Throws<ArgumentException>(() => new PanoramaEnvironmentMomentController(
                    new[] { duplicate, duplicate },
                    parent.transform));

                Assert.Throws<ArgumentOutOfRangeException>(() => new PanoramaEnvironmentMomentController(
                    new[]
                    {
                        Definition("moment-1", prefab, Color.white),
                        Definition("moment-2", prefab, Color.white),
                        Definition("moment-3", prefab, Color.white),
                        Definition("moment-4", prefab, Color.white),
                        Definition("moment-5", prefab, Color.white)
                    },
                    parent.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Controller_ResetAndDispose_DeactivateAndReleaseActiveRuntime()
        {
            var parent = new GameObject("MomentParent");
            var prefab = new GameObject("MomentPrefab");
            var factory = new RecordingFactory();
            try
            {
                using (var controller = new PanoramaEnvironmentMomentController(
                           new[] { Definition("dry-wind", prefab, Color.yellow) },
                           parent.transform,
                           factory))
                {
                    controller.Cycle();
                    controller.Reset();
                    Assert.That(factory["dry-wind"].DeactivateCount, Is.EqualTo(1));

                    controller.Cycle();
                    controller.Dispose();
                    Assert.That(factory["dry-wind"].DeactivateCount, Is.EqualTo(2));
                    Assert.That(factory["dry-wind"].DisposeCount, Is.EqualTo(1));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void PrefabRuntime_InvokesOptionalEffectContractAndOwnsInstanceLifetime()
        {
            var parent = new GameObject("MomentParent");
            var prefab = new GameObject("MomentPrefab");
            prefab.AddComponent<CountingEnvironmentMomentEffect>();
            try
            {
                var definition = Definition("flowering", prefab, Color.magenta);
                using (var runtime = PrefabPanoramaEnvironmentMomentRuntimeFactory.Instance.Create(
                           definition,
                           parent.transform))
                {
                    var instance = parent.GetComponentInChildren<CountingEnvironmentMomentEffect>(true);
                    Assert.That(instance, Is.Not.Null);
                    Assert.That(instance.gameObject.activeSelf, Is.False);

                    runtime.Activate();
                    Assert.That(instance.gameObject.activeSelf, Is.True);
                    Assert.That(instance.ActivateCount, Is.EqualTo(1));

                    runtime.Deactivate();
                    Assert.That(instance.gameObject.activeSelf, Is.False);
                    Assert.That(instance.DeactivateCount, Is.EqualTo(1));
                }

                Assert.That(parent.transform.childCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        static PanoramaEnvironmentMomentDefinition Definition(
            string id,
            GameObject prefab,
            Color accent)
            => new PanoramaEnvironmentMomentDefinition(id, prefab, accent, 1f);

        sealed class RecordingFactory : IPanoramaEnvironmentMomentRuntimeFactory
        {
            readonly Dictionary<string, RecordingRuntime> _runtimes =
                new Dictionary<string, RecordingRuntime>(StringComparer.Ordinal);

            public int CreateCount { get; private set; }
            public RecordingRuntime this[string id] => _runtimes[id];

            public IPanoramaEnvironmentMomentRuntime Create(
                PanoramaEnvironmentMomentDefinition definition,
                Transform parent)
            {
                CreateCount++;
                var runtime = new RecordingRuntime();
                _runtimes.Add(definition.MomentId, runtime);
                return runtime;
            }
        }

        sealed class RecordingRuntime : IPanoramaEnvironmentMomentRuntime
        {
            public bool IsActive { get; private set; }
            public int DeactivateCount { get; private set; }
            public int DisposeCount { get; private set; }

            public void Activate() => IsActive = true;

            public void Deactivate()
            {
                IsActive = false;
                DeactivateCount++;
            }

            public void Dispose()
            {
                IsActive = false;
                DisposeCount++;
            }
        }
    }

    public sealed class CountingEnvironmentMomentEffect : MonoBehaviour, IPanoramaEnvironmentMomentEffect
    {
        public int ActivateCount { get; private set; }
        public int DeactivateCount { get; private set; }
        public void Activate() => ActivateCount++;
        public void Deactivate() => DeactivateCount++;
    }
}
