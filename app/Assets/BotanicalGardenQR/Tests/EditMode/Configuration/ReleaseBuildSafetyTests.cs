using System.IO;
using System.Linq;
using BotanicalGardenQR.Editor.Validation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Configuration
{
    public sealed class ReleaseBuildSafetyTests
    {
        [Test]
        public void AndroidIconSlotsUseThePublishedBrandingSources()
        {
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/BotanicalGardenQR/Branding/BotanicalGardenQuestIcon-v1.png");
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/BotanicalGardenQR/Branding/BotanicalGardenQuestIconForeground-v1.png");
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/BotanicalGardenQR/Branding/BotanicalGardenQuestIconBackground-v1.png");
            Assert.That(full, Is.Not.Null);
            Assert.That(foreground, Is.Not.Null);
            Assert.That(background, Is.Not.Null);

            var assigned = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android)
                .SelectMany(kind => PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind))
                .SelectMany(icon => icon.GetTextures())
                .ToArray();

            Assert.That(assigned, Is.Not.Empty);
            Assert.That(assigned, Has.None.Null,
                "Every Android adaptive, round, and legacy icon layer must be assigned.");
            Assert.That(assigned, Does.Contain(full));
            Assert.That(assigned, Does.Contain(foreground));
            Assert.That(assigned, Does.Contain(background));

            foreach (var texture in new[] { full, foreground, background })
            {
                var path = AssetDatabase.GetAssetPath(texture);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(
                    importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.Uncompressed),
                    $"Android launcher icon source '{path}' must remain uncompressed so Unity does not export a visibly degraded icon.");
            }
        }

        [Test]
        public void BuildOutputPolicy_RejectsRepositoryPathsAndAllowsSiblingFolder()
        {
            var parent = Path.Combine(Path.GetTempPath(), "BotanicalGardenQR-ReleaseSafety");
            var project = Path.Combine(parent, "BotanicalGardenQR-Base");

            Assert.That(
                ReleaseBuildSafety.IsExternalBuildOutput(project, Path.Combine(project, "Builds", "app.apk")),
                Is.False);
            Assert.That(
                ReleaseBuildSafety.IsExternalBuildOutput(project, project),
                Is.False);
            Assert.That(
                ReleaseBuildSafety.IsExternalBuildOutput(
                    project,
                    Path.Combine(parent, "BotanicalGardenQR-Builds", "Android", "app.apk")),
                Is.True);
        }

        [Test]
        public void LocalDeploymentTarget_IsAllowedForAnyExplicitBuildAndRun()
        {
            Assert.That(
                ReleaseBuildSafety.IsLocalBuildAndRun(
                    BuildOptions.Development | BuildOptions.AutoRunPlayer),
                Is.True);
            Assert.That(
                ReleaseBuildSafety.IsLocalBuildAndRun(BuildOptions.AutoRunPlayer),
                Is.True);
            Assert.That(
                ReleaseBuildSafety.IsLocalBuildAndRun(BuildOptions.Development),
                Is.False);
            Assert.That(
                ReleaseBuildSafety.IsLocalBuildAndRun(BuildOptions.None),
                Is.False);
        }
    }
}
