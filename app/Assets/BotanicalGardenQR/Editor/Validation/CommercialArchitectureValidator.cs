using System;
using System.Collections.Generic;
using System.Linq;

namespace BotanicalGardenQR.Editor.Validation
{
    public enum ValidationScope
    {
        All, Activation, FrontendShell, SpatialHost, Video, Panorama, ImageRing, Collection, KnowledgeMiniGame, Model, Narration, Fairy, Effect, VisitorPrologue, VisitorAtlasHub, PhysicalAugmentation, MapNavigation
    }

    public sealed class CommercialValidationIssue
    {
        public CommercialValidationIssue(string code, string assetPath, string line, string message)
        {
            Code = code;
            AssetPath = string.IsNullOrWhiteSpace(assetPath) ? "<project>" : assetPath;
            Line = string.IsNullOrWhiteSpace(line) ? "All" : line;
            Message = message;
        }

        public string Code { get; }
        public string AssetPath { get; }
        public string Line { get; }
        public string Message { get; }
        public override string ToString() => $"[{Code}] [{Line}] {AssetPath}: {Message}";
    }

    public sealed class CommercialValidationReport
    {
        readonly CommercialValidationIssue[] _issues;
        public CommercialValidationReport(IEnumerable<CommercialValidationIssue> issues) => _issues = issues.OrderBy(x => x.Code, StringComparer.Ordinal).ThenBy(x => x.AssetPath, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<CommercialValidationIssue> Issues => _issues;
        public bool IsValid => _issues.Length == 0;
        public string Format() => IsValid ? "Commercial architecture validation passed." : string.Join(Environment.NewLine, _issues.Select(x => x.ToString()));
    }

    public static class CommercialArchitectureValidator
    {
        public static CommercialValidationReport Validate(ValidationScope scope)
        {
            var issues = new List<CommercialValidationIssue>();
            ContentSceneValidator.Validate(scope, issues);
            ContentEntryValidator.Validate(scope, issues);
            VisitorSceneValidator.Validate(scope, issues);
            DependencyLocalityValidator.Validate(scope, issues);
            AssetStructureValidator.Validate(scope, issues);
            PhysicalAugmentationArchitectureValidator.Validate(scope, issues);
            return new CommercialValidationReport(issues);
        }

        internal static bool Includes(ValidationScope requested, ValidationScope line) => requested == ValidationScope.All || requested == line;
        internal static void Add(ICollection<CommercialValidationIssue> issues, string code, string path, string line, string message)
            => issues.Add(new CommercialValidationIssue(code, path, line, message));
    }
}
