using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class FullScriptImportedModelTests
    {
        const string Root = "Assets/EndoscopyTheme/ImportedModels/";
        [TestCase("OfficeRoom", "办公室.FBX", 6100, false)]
        [TestCase("ClinicalRoom", "诊疗室模型.FBX", 348, false)]
        [TestCase("Computer", "computer.fbx", 24, true)]
        [TestCase("Desk", "table.fbx", 4, true)]
        [TestCase("Gastroscope", "Gastroscope/Gastroscope.fbx", 2, true)]
        public void PreparedModelLoadsWithFiniteGroundedBoundsAndPreservedMeshes(string name, string source, int meshes, bool prop)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prepared/" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            try
            {
                Assert.That(instance.GetComponentsInChildren<MeshFilter>(true).Count(f => f.sharedMesh), Is.EqualTo(meshes));
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    Assert.That(renderer.sharedMaterials.All(m => m && m.shader), Is.True, renderer.name);
                }
                Assert.That(float.IsNaN(bounds.size.sqrMagnitude) || float.IsInfinity(bounds.size.sqrMagnitude), Is.False);
                Assert.That(bounds.size.sqrMagnitude, Is.GreaterThan(.01f));
                Assert.That(bounds.min.y, Is.EqualTo(0).Within(.001f));
                Assert.That(bounds.center.x, Is.EqualTo(0).Within(.001f));
                Assert.That(bounds.center.z, Is.EqualTo(0).Within(.001f));
                Assert.That(instance.GetComponentsInChildren<Camera>(true), Is.Empty);
                Assert.That(instance.GetComponentsInChildren<Light>(true), Is.Empty);
                Assert.That(instance.GetComponentsInChildren<AudioSource>(true), Is.Empty);
                Assert.That(instance.GetComponent<BoxCollider>() != null, Is.EqualTo(prop));
                var importer = (ModelImporter)AssetImporter.GetAtPath(Root + "Source/" + source);
                Assert.That(importer.importAnimation || importer.importCameras || importer.importLights || importer.addCollider, Is.False);
                // Display normalization is not a claim about source medical dimensions.
                if (name == "Desk") Assert.That(bounds.size.x, Is.EqualTo(1.6f).Within(.01f));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test] public void ThreeDoorFrameLeafPairsRemainAvailableWithoutFakeHingeBinding()
        {
            foreach (var id in new[] { "341335", "342041", "349330" })
                foreach (var part in new[] { "Frame", "Leaf" })
                {
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "Source/OfficeInteractions/" + id + "_" + part + ".asset");
                    Assert.That(mesh, Is.Not.Null);
                    Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                }
        }

        [Test] public void NewHeavyAssetsAreNotEagerlyReferencedByVisitorOrPreloadedAssets()
        {
            var entry = "Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity";
            Assert.That(AssetDatabase.GetDependencies(entry, true).Any(p => p.StartsWith(Root, StringComparison.Ordinal)), Is.False);
            foreach (var asset in PlayerSettings.GetPreloadedAssets().Where(a => a))
                Assert.That(AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(asset), true)
                    .Any(p => p.StartsWith(Root, StringComparison.Ordinal)), Is.False);
            Assert.That(AssetDatabase.FindAssets("", new[] { Root.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath)
                .Any(p => p.Contains("/Resources/")), Is.False);
        }
    }
}
