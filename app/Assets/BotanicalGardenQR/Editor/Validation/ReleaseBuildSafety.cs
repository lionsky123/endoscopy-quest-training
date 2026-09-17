using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BotanicalGardenQR.Editor.Validation
{
    /// <summary>
    /// Fail-closed release boundary for editor-generated credentials and device targets.
    /// Build entry points in the authoring project only validate. The isolated validation
    /// build may sanitize the two exact project-owned locations inside its disposable mirror.
    /// </summary>
    public static class ReleaseBuildSafety
    {
        public const string DevAgentSettingsPath = "Assets/Resources/DevAgentSettings.asset";
        public const string QuestBuildProfilePath = "Assets/Settings/Build Profiles/Meta Quest.asset";

        const string DeploymentTargetPropertyName = "m_CurrentDeploymentTargetId";
        const string BuiltInDefaultDeploymentTarget = "__builtin__target_default";

        public static void SanitizeForExplicitReleaseBuild()
        {
            if (CredentialArtifactExists() && !AssetDatabase.DeleteAsset(DevAgentSettingsPath))
                throw new BuildFailedException(
                    $"Release sanitization could not remove '{DevAgentSettingsPath}'.");

            NormalizeDeploymentTarget();
            AssetDatabase.SaveAssets();
            ValidateOrThrow();
        }

        public static void ValidateOrThrow(BuildOptions buildOptions = BuildOptions.None)
        {
            var isLocalBuildAndRun = IsLocalBuildAndRun(buildOptions);
            var credentialArtifactExists = CredentialArtifactExists();
            if (credentialArtifactExists && !isLocalBuildAndRun)
                throw new BuildFailedException(
                    $"Release build rejected because editor-generated credential material exists at '{DevAgentSettingsPath}'. " +
                    "For local testing, use Build And Run; commercial and validation builds must use the repository Android validation script so the asset is excluded from its isolated mirror.");
            if (credentialArtifactExists)
                Debug.LogWarning(
                    $"[BotanicalGardenQR] Local Build And Run includes ignored editor credential material at '{DevAgentSettingsPath}'. " +
                    "Keep this APK private and do not distribute it.");

            var deploymentTarget = FindDeploymentTargetProperty();
            if (!string.Equals(
                    deploymentTarget.stringValue,
                    BuiltInDefaultDeploymentTarget,
                    StringComparison.Ordinal) &&
                !isLocalBuildAndRun)
                throw new BuildFailedException(
                    $"Release build rejected because '{QuestBuildProfilePath}' contains a workstation deployment target. " +
                    "Release and validation APKs must produce an APK only and must not retain a Quest device identifier. " +
                    "A local target is permitted for Build And Run; commercial and validation builds must use the builtin target.");
        }

        public static bool IsLocalBuildAndRun(BuildOptions buildOptions) =>
            (buildOptions & BuildOptions.AutoRunPlayer) != 0;

        public static bool IsExternalBuildOutput(string projectRoot, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(outputPath)) return false;
            var normalizedRoot = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedOutput = Path.GetFullPath(outputPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(normalizedRoot, normalizedOutput, StringComparison.OrdinalIgnoreCase)) return false;
            return !normalizedOutput.StartsWith(
                EnsureTrailingSeparator(normalizedRoot),
                StringComparison.OrdinalIgnoreCase);
        }

        static void NormalizeDeploymentTarget()
        {
            var deploymentTarget = FindDeploymentTargetProperty();
            if (string.Equals(
                    deploymentTarget.stringValue,
                    BuiltInDefaultDeploymentTarget,
                    StringComparison.Ordinal)) return;
            deploymentTarget.stringValue = BuiltInDefaultDeploymentTarget;
            deploymentTarget.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(deploymentTarget.serializedObject.targetObject);
        }

        static bool CredentialArtifactExists()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var assetPath = Path.Combine(
                projectRoot,
                DevAgentSettingsPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(assetPath) || File.Exists(assetPath + ".meta");
        }

        static SerializedProperty FindDeploymentTargetProperty()
        {
            var profile = AssetDatabase.LoadMainAssetAtPath(QuestBuildProfilePath);
            if (profile == null)
                throw new BuildFailedException($"Required Quest build profile is missing at '{QuestBuildProfilePath}'.");

            var serialized = new SerializedObject(profile);
            var iterator = serialized.GetIterator();
            SerializedProperty match = null;
            while (iterator.Next(true))
            {
                if (!string.Equals(iterator.name, DeploymentTargetPropertyName, StringComparison.Ordinal)) continue;
                if (iterator.propertyType != SerializedPropertyType.String)
                    throw new BuildFailedException(
                        $"Quest build profile property '{DeploymentTargetPropertyName}' is not a string.");
                if (match != null)
                    throw new BuildFailedException(
                        $"Quest build profile contains multiple '{DeploymentTargetPropertyName}' properties.");
                match = iterator.Copy();
            }

            return match ?? throw new BuildFailedException(
                $"Quest build profile is missing '{DeploymentTargetPropertyName}'. Unity profile serialization may have changed.");
        }

        static string EnsureTrailingSeparator(string path)
            => path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
    }

    public sealed class ReleaseBuildSafetyGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -2000;

        public void OnPreprocessBuild(BuildReport report) =>
            ReleaseBuildSafety.ValidateOrThrow(report.summary.options);
    }
}
