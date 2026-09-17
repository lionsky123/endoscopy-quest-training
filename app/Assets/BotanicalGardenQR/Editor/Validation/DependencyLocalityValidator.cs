using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BotanicalGardenQR.Configuration.Runtime;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Editor.Validation
{
    internal static class DependencyLocalityValidator
    {
        static readonly string[] Lines = { "Activation", "FrontendShell", "SpatialHost", "Video", "Panorama", "ImageRing", "Collection", "KnowledgeMiniGame", "Model", "Narration", "Fairy", "Effect", "VisitorPrologue", "VisitorAtlasHub", "PhysicalAugmentation", "MapNavigation" };
        static readonly string[] RetiredMarkers = { "Content/XREAL", "BotanicalExperienceCatalog", "VisitorMediaEntryMap", "EntryMediaRuntimeCoordinator", "BotanicalExperienceDirector", "BotanicalVisitorMediaLifecycleRelay" };
        static readonly Regex ConcreteImplementationImportPattern = new Regex(
            @"^\s*using\s+BotanicalGardenQR\.([A-Za-z0-9_]+)\.(Runtime|Backend|Frontend)\s*;",
            RegexOptions.Multiline | RegexOptions.Compiled);

        public static void Validate(ValidationScope scope, ICollection<CommercialValidationIssue> issues)
        {
            ValidateAssemblyGraph(scope, issues);
            ValidateConcreteImplementationImports(scope, issues);
            ValidateBootstrapSeam(scope, issues);
            ValidateForbiddenCoupling(scope, issues);
            ValidateRuntimeOptions(issues);
            if (scope == ValidationScope.All)
            {
                ValidateReverseDependencies(issues);
                ValidateReadmes(issues);
            }
        }

        static void ValidateForbiddenCoupling(ValidationScope scope, ICollection<CommercialValidationIssue> issues)
        {
            var roots = new List<(string Path, string Line, string[] Forbidden)>();
            if (scope == ValidationScope.All)
            {
                roots.Add(("Assets/BotanicalGardenQR/Experience/Flow", "Experience", new[] { "QRCode", "OVRSpatialAnchor", "AnchorUuid", "RecognitionObservation", "SourceKind", "SpatialEvidence", "LastResolvedActivation" }));
                roots.Add(("Assets/BotanicalGardenQR/Experience/Application", "Experience", new[] { "UnityEngine", "UnityEditor", "Oculus.", "Meta.XR", "QRCode", "SceneId", "AnchorUuid" }));
            }
            if (CommercialArchitectureValidator.Includes(scope, ValidationScope.FrontendShell))
                roots.Add(("Assets/BotanicalGardenQR/Modules/FrontendShell", "FrontendShell", new[] { "QRCode", "OVRSpatialAnchor", "AnchorUuid", "RecognitionObservation", "SourceKind", "SpatialEvidence", "LastResolvedActivation" }));
            if (CommercialArchitectureValidator.Includes(scope, ValidationScope.Activation))
                roots.Add(("Assets/BotanicalGardenQR/Modules/Activation", "Activation", new[] { "AnchorUIManager", "BotanicalOfficialSpatialAnchorAdminBridge", "SpatialAnchorAdminMenu" }));
            foreach (var line in Lines.Where(x => x != "Activation" && x != "FrontendShell" && x != "PhysicalAugmentation" && CommercialArchitectureValidator.Includes(scope, ParseScope(x))))
            {
                roots.Add(($"Assets/BotanicalGardenQR/Modules/{line}/Backend", line, new[] { "UnityEngine.UI", "RectTransform", "PointerEventData", "QRCode", "OVRSpatialAnchor", "AnchorUuid" }));
                roots.Add(($"Assets/BotanicalGardenQR/Modules/{line}/Frontend", line, new[] { "QRCode", "OVRSpatialAnchor", "AnchorUuid", "RecognitionObservation", "SourceKind", "SpatialEvidence" }));
            }
            if (CommercialArchitectureValidator.Includes(scope, ValidationScope.PhysicalAugmentation))
            {
                roots.Add(("Assets/BotanicalGardenQR/Modules/PhysicalAugmentation/Contracts", "PhysicalAugmentation", new[] { "OVRSpatialAnchor", "Meta.XR", "Oculus.", "AnchorUuid" }));
                roots.Add(("Assets/BotanicalGardenQR/Modules/PhysicalAugmentation/Frontend", "PhysicalAugmentation", new[] { "OVRSpatialAnchor", "Meta.XR", "Oculus.", "AnchorUuid", "QRCode", "SceneId", "RecognitionObservation" }));
                roots.Add(("Assets/BotanicalGardenQR/Modules/PhysicalAugmentation/Installation", "PhysicalAugmentation", new[] { "OVRSpatialAnchor", "Meta.XR", "Oculus.", "UnityEngine.UI" }));
            }
            if(CommercialArchitectureValidator.Includes(scope,ValidationScope.MapNavigation))
            {
                roots.Add(("Assets/BotanicalGardenQR/Modules/MapNavigation/Runtime","MapNavigation",new[]{"UnityEngine", "QRCode", "OVRSpatialAnchor", "AnchorUuid", "SceneId"}));
                roots.Add(("Assets/BotanicalGardenQR/Modules/MapNavigation/Contracts","MapNavigation",new[]{"UnityEngine", "QRCode", "OVRSpatialAnchor", "AnchorUuid", "SceneId"}));
            }
            foreach (var root in roots.Where(x => AssetDatabase.IsValidFolder(x.Path)))
                foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { root.Path }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var text = File.ReadAllText(Path.GetFullPath(path));
                    foreach (var marker in root.Forbidden.Where(text.Contains))
                        CommercialArchitectureValidator.Add(issues, "COM-COUPLING-001", path, root.Line, $"Forbidden cross-line dependency marker '{marker}' is present.");
                }
        }

        static void ValidateAssemblyGraph(ValidationScope scope, ICollection<CommercialValidationIssue> issues)
        {
            var definitions = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset").Select(AssetDatabase.GUIDToAssetPath)
                .Where(x => x.StartsWith("Assets/BotanicalGardenQR/", StringComparison.Ordinal)).Select(ReadAsmdef).Where(x => x != null).ToArray();
            foreach (var definition in definitions)
            {
                if (scope == ValidationScope.All && definition.Name == "BotanicalGardenQR.Experience.Application")
                    foreach (var reference in definition.References ?? Array.Empty<string>())
                        if (!reference.StartsWith("BotanicalGardenQR.", StringComparison.Ordinal) ||
                            !reference.EndsWith(".Contracts", StringComparison.Ordinal))
                            CommercialArchitectureValidator.Add(issues, "COM-DEP-002", definition.Path, "Experience",
                                $"Application coordination must consume Contracts, not '{reference}'.");
                var owner = Lines.FirstOrDefault(line => definition.Name.StartsWith("BotanicalGardenQR." + line + ".", StringComparison.Ordinal));
                if (owner == null || (!CommercialArchitectureValidator.Includes(scope, ParseScope(owner)) && scope != ValidationScope.All)) continue;
                foreach (var reference in definition.References ?? Array.Empty<string>())
                {
                    var targetLine = Lines.FirstOrDefault(line => reference.StartsWith("BotanicalGardenQR." + line + ".", StringComparison.Ordinal));
                    if (targetLine != null && !string.Equals(owner, targetLine, StringComparison.Ordinal) && IsConcreteFeatureAssembly(reference))
                        CommercialArchitectureValidator.Add(issues, "COM-DEP-001", definition.Path, owner, $"Feature line references another line's concrete assembly '{reference}'.");
                    if (definition.Name.EndsWith(".Frontend", StringComparison.Ordinal) && (reference.EndsWith(".Backend", StringComparison.Ordinal) || reference.Contains("Activation.Runtime")))
                        CommercialArchitectureValidator.Add(issues, "COM-DEP-002", definition.Path, owner, $"Frontend assembly depends on backend/runtime implementation '{reference}'.");
                }
            }
            DetectCycles(definitions, issues);
        }

        static void ValidateConcreteImplementationImports(ValidationScope scope, ICollection<CommercialValidationIssue> issues)
        {
            foreach (var owner in Lines)
            {
                if (!CommercialArchitectureValidator.Includes(scope, ParseScope(owner)) && scope != ValidationScope.All)
                    continue;

                var root = $"Assets/BotanicalGardenQR/Modules/{owner}";
                if (!AssetDatabase.IsValidFolder(root))
                    continue;

                foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { root }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var normalized = path.Replace('\\', '/');
                    if (normalized.Contains("/Tests/"))
                        continue;

                    var text = File.ReadAllText(Path.GetFullPath(path));
                    foreach (Match match in ConcreteImplementationImportPattern.Matches(text))
                    {
                        var target = match.Groups[1].Value;
                        var layer = match.Groups[2].Value;
                        if (!Lines.Contains(target, StringComparer.Ordinal) || string.Equals(owner, target, StringComparison.Ordinal))
                            continue;

                        CommercialArchitectureValidator.Add(
                            issues,
                            "COM-DEP-004",
                            path,
                            owner,
                            $"Feature source imports another line's concrete {layer} namespace 'BotanicalGardenQR.{target}.{layer}'. Use the target line's Contracts assembly or move composition to Bootstrap.");
                    }
                }
            }
        }

        static bool IsConcreteFeatureAssembly(string reference) =>
            reference.EndsWith(".Backend", StringComparison.Ordinal) ||
            reference.EndsWith(".Frontend", StringComparison.Ordinal) ||
            reference.EndsWith(".Runtime", StringComparison.Ordinal);

        static void DetectCycles(AsmdefInfo[] definitions, ICollection<CommercialValidationIssue> issues)
        {
            var map = definitions.ToDictionary(x => x.Name, StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in definitions) Visit(definition, map, visiting, visited, issues);
        }

        static void Visit(AsmdefInfo node, IReadOnlyDictionary<string, AsmdefInfo> map, ISet<string> visiting, ISet<string> visited, ICollection<CommercialValidationIssue> issues)
        {
            if (visited.Contains(node.Name)) return;
            if (!visiting.Add(node.Name)) { CommercialArchitectureValidator.Add(issues, "COM-DEP-003", node.Path, "Assembly", $"Assembly dependency cycle includes '{node.Name}'."); return; }
            foreach (var reference in node.References ?? Array.Empty<string>()) if (map.TryGetValue(reference, out var target)) Visit(target, map, visiting, visited, issues);
            visiting.Remove(node.Name); visited.Add(node.Name);
        }

        static void ValidateBootstrapSeam(ValidationScope scope, ICollection<CommercialValidationIssue> issues)
        {
            if (scope != ValidationScope.All) return;
            const string root = "Assets/BotanicalGardenQR/Experience/Bootstrap";
            var forbidden = new[] { "ImplementationSelector", "ModelLoader", "ModelDriver", "VideoPlayer", "PanoramaRenderer", "FairyOrbitDriver", "EffectImplementationSelector" };
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var text = File.ReadAllText(Path.GetFullPath(path));
                foreach (var marker in forbidden.Where(text.Contains))
                    CommercialArchitectureValidator.Add(issues, "COM-BOOT-001", path, "Bootstrap", $"Bootstrap crosses the factory seam and references concrete implementation '{marker}'.");
            }
            var asm = ReadAsmdef(root + "/BotanicalGardenQR.Bootstrap.asmdef");
            if (asm == null) return;
            var allowedConcrete = new HashSet<string>(Lines.Select(x => $"BotanicalGardenQR.{x}.Backend").Concat(new[]
            {
                "BotanicalGardenQR.Activation.Runtime",
                "BotanicalGardenQR.Collection.Frontend",
                "BotanicalGardenQR.Collection.Runtime",
                "BotanicalGardenQR.FrontendShell.Runtime",
                "BotanicalGardenQR.JourneyNavigation.Runtime",
                "BotanicalGardenQR.SpatialHost.Runtime",
                "BotanicalGardenQR.VisitorPrologue.Runtime",
                "BotanicalGardenQR.VisitorCoach.Runtime",
                "BotanicalGardenQR.MapNavigation.Runtime"
            }), StringComparer.Ordinal);
            foreach (var reference in asm.References.Where(x => (x.EndsWith(".Backend", StringComparison.Ordinal) || x.EndsWith(".Runtime", StringComparison.Ordinal)) && !allowedConcrete.Contains(x) && x != "BotanicalGardenQR.Configuration.Runtime"))
                CommercialArchitectureValidator.Add(issues, "COM-BOOT-002", asm.Path, "Bootstrap", $"Bootstrap references an unapproved concrete assembly '{reference}'.");
        }

        static void ValidateRuntimeOptions(ICollection<CommercialValidationIssue> issues)
        {
            foreach (var path in AssetDatabase.FindAssets("t:RuntimeEnvironmentOptions").Select(AssetDatabase.GUIDToAssetPath))
            {
                var options = AssetDatabase.LoadAssetAtPath<RuntimeEnvironmentOptions>(path);
                var serialized = new SerializedObject(options);
                var iterator = serialized.GetIterator();
                if (!iterator.NextVisible(true)) continue;
                do if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue is GameObject)
                    CommercialArchitectureValidator.Add(issues, "COM-CONFIG-005", path, "Configuration", $"RuntimeEnvironmentOptions contains scene/prefab object reference '{iterator.propertyPath}'.");
                while (iterator.NextVisible(true));
            }
        }

        static void ValidateReverseDependencies(ICollection<CommercialValidationIssue> issues)
        {
            var reverse = BuildProductionReverseIndex();
            foreach (var pair in reverse)
                if (RetiredMarkers.Any(x => pair.Key.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
                    foreach (var owner in pair.Value)
                        CommercialArchitectureValidator.Add(issues, "COM-LEGACY-001", owner, "Locality", $"Production asset references retired dependency '{pair.Key}'.");
        }

        public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> BuildProductionReverseIndex()
        {
            var mutable = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var roots = AssetDatabase.GetAllAssetPaths().Where(IsProductionReferenceRoot).OrderBy(x => x, StringComparer.Ordinal);
            foreach (var owner in roots)
                foreach (var dependency in AssetDatabase.GetDependencies(owner, true).Where(x => !string.Equals(x, owner, StringComparison.Ordinal)))
                {
                    if (!mutable.TryGetValue(dependency, out var owners)) mutable[dependency] = owners = new HashSet<string>(StringComparer.Ordinal);
                    owners.Add(owner);
                }
            return mutable.ToDictionary(x => x.Key, x => (IReadOnlyCollection<string>)x.Value.OrderBy(y => y, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        }

        static bool IsProductionReferenceRoot(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("/Editor/") || path.Contains("/Tests/")) return false;
            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".unity" || extension == ".prefab" || extension == ".asset" || extension == ".controller" || extension == ".mat";
        }

        static void ValidateReadmes(ICollection<CommercialValidationIssue> issues)
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            var files = Directory.EnumerateFiles(root, "README*", SearchOption.TopDirectoryOnly);
            foreach (var directoryName in new[] { "Assets", "docs", "tools" })
            {
                var directory = Path.Combine(root, directoryName);
                if (Directory.Exists(directory)) files = files.Concat(Directory.EnumerateFiles(directory, "README*", SearchOption.AllDirectories));
            }
            foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var relative = file.Substring(root.Length + 1).Replace('\\', '/');
                CommercialArchitectureValidator.Add(issues, "COM-DOC-001", relative, "Documentation", "Project-owned README files are forbidden; use a purpose-named document.");
            }
        }

        static ValidationScope ParseScope(string value) => (ValidationScope)Enum.Parse(typeof(ValidationScope), value);
        static AsmdefInfo ReadAsmdef(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(Path.GetFullPath(path))) return null;
            var data = JsonUtility.FromJson<AsmdefJson>(File.ReadAllText(Path.GetFullPath(path)));
            return data == null || string.IsNullOrWhiteSpace(data.name) ? null : new AsmdefInfo(path, data.name, data.references ?? Array.Empty<string>());
        }

        [Serializable] sealed class AsmdefJson { public string name; public string[] references; }
        sealed class AsmdefInfo
        {
            public AsmdefInfo(string path, string name, string[] references) { Path = path; Name = name; References = references; }
            public string Path { get; } public string Name { get; } public string[] References { get; }
        }
    }
}
