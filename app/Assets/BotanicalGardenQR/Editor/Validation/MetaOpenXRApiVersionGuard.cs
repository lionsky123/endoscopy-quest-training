using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace BotanicalGardenQR.Editor.Validation
{
    internal static class MetaOpenXRApiVersionGuard
    {
        const string RecommendedApiVersion = "1.1.53";
        const string Meta205AuthoredApiVersion = "1.1.45";
        const string SettingsPath = "Assets/XR/Settings/OpenXR Package Settings.asset";
        const string PackageManifestPath = "Packages/manifest.json";

        static readonly BuildTargetGroup[] TargetGroups =
        {
            BuildTargetGroup.Android,
            BuildTargetGroup.Standalone
        };

        public static void Validate(ICollection<CommercialValidationIssue> issues)
        {
            if (issues == null) throw new System.ArgumentNullException(nameof(issues));

            var usesKnownMeta205PackageGraph = UsesKnownMeta205PackageGraph();
            foreach (var targetGroup in TargetGroups)
            {
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
                if (!settings) continue;

                foreach (var feature in settings.GetFeatures())
                {
                    if (!feature || !IsMetaFeature(feature)) continue;

                    var serialized = new SerializedObject(feature);
                    var targetApi = serialized.FindProperty("targetOpenXRApiVersion");
                    if (targetApi == null || string.IsNullOrWhiteSpace(targetApi.stringValue)) continue;
                    if (!IsUnsupportedTarget(targetApi.stringValue, usesKnownMeta205PackageGraph)) continue;

                    CommercialArchitectureValidator.Add(
                        issues,
                        "COM-XR-004",
                        SettingsPath,
                        "Bootstrap",
                        $"Meta OpenXR feature '{feature.name}' targets API {targetApi.stringValue}; expected at least {RecommendedApiVersion}, " +
                        $"except for Meta XR 205's upstream-authored {Meta205AuthoredApiVersion} value.");
                }
            }
        }

        static bool IsMetaFeature(OpenXRFeature feature)
        {
            var serialized = new SerializedObject(feature);
            var company = serialized.FindProperty("company")?.stringValue;
            var featureId = serialized.FindProperty("featureIdInternal")?.stringValue;
            return company == "Meta" || (featureId != null && featureId.StartsWith("com.meta.openxr"));
        }

        internal static bool IsLowerThanRecommended(string version)
            => TryParseVersion(version, out var currentMajor, out var currentMinor, out var currentPatch)
               && TryParseVersion(RecommendedApiVersion, out var recommendedMajor, out var recommendedMinor, out var recommendedPatch)
               && (currentMajor, currentMinor, currentPatch)
                    .CompareTo((recommendedMajor, recommendedMinor, recommendedPatch)) < 0;

        internal static bool IsUnsupportedTarget(string version, bool usesKnownMeta205PackageGraph)
        {
            if (!TryParseVersion(version, out _, out _, out _)) return true;
            return IsLowerThanRecommended(version) &&
                   !(usesKnownMeta205PackageGraph && version == Meta205AuthoredApiVersion);
        }

        static bool UsesKnownMeta205PackageGraph()
        {
            if (!File.Exists(PackageManifestPath)) return false;
            var manifest = File.ReadAllText(PackageManifestPath);
            return Regex.IsMatch(manifest,
                       "\\\"com\\.meta\\.xr\\.sdk\\.core\\\"\\s*:\\s*\\\"205\\.0\\.0\\\"") &&
                   Regex.IsMatch(manifest,
                       "\\\"com\\.unity\\.xr\\.openxr\\\"\\s*:\\s*\\\"1\\.16\\.1\\\"");
        }

        static bool TryParseVersion(string version, out int major, out int minor, out int patch)
        {
            major = 0;
            minor = 0;
            patch = 0;
            if (string.IsNullOrWhiteSpace(version)) return false;

            var parts = version.Split('.');
            return parts.Length >= 3
                   && int.TryParse(parts[0], out major)
                   && int.TryParse(parts[1], out minor)
                   && int.TryParse(parts[2], out patch);
        }
    }
}
