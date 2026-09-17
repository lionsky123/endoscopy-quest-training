using System;
using BotanicalGardenQR.VisitorAtlasHub.Backend;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.VisitorAtlasHub
{
    public sealed class VisitorAtlasHubInitializationTests
    {
        [TestCase("../map.glb")]
        [TestCase("%2e%2e/map.glb")]
        [TestCase("%2e%2e%5cmap.glb")]
        [TestCase("%43%3a%5cmap.glb")]
        [TestCase("folder//map.glb")]
        [TestCase("folder/map.glb?cache=1")]
        [TestCase("folder/map.glb%3fcache=1")]
        [TestCase("folder/map.gltf")]
        public void StreamingAssetsPathRejectsTraversalAndNonGlbInputs(string path)
            => Assert.Throws<InvalidOperationException>(() =>
                StreamingAssetsUriResolver.ValidateRelativePath(path));

        [Test]
        public void StreamingAssetsResolverSupportsEditorRemoteAndQuestSchemes()
        {
            const string relative = "VisitorAtlasHub/basement map.glb";

            var local = StreamingAssetsUriResolver.Resolve("D:/Garden/StreamingAssets", relative);
            var file = StreamingAssetsUriResolver.Resolve("file:///D:/Garden/StreamingAssets", relative);
            var https = StreamingAssetsUriResolver.Resolve("https://example.test/assets", relative);
            var jar = StreamingAssetsUriResolver.Resolve(
                "jar:file:///data/app/com.example/base.apk!/assets",
                relative);

            Assert.That(local.IsFile, Is.True);
            Assert.That(file.IsFile, Is.True);
            Assert.That(file.AbsoluteUri, Does.EndWith("VisitorAtlasHub/basement%20map.glb"));
            Assert.That(https.AbsoluteUri,
                Is.EqualTo("https://example.test/assets/VisitorAtlasHub/basement%20map.glb"));
            Assert.That(jar.Scheme, Is.EqualTo("jar"));
            Assert.That(jar.AbsoluteUri,
                Is.EqualTo("jar:file:///data/app/com.example/base.apk!/assets/VisitorAtlasHub/basement%20map.glb"));
        }
    }
}
