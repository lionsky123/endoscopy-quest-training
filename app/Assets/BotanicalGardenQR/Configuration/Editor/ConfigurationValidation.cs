using System;
using System.Collections.Generic;
using System.Linq;

namespace BotanicalGardenQR.Configuration.Editor
{
    public sealed class ConfigurationIssue
    {
        public ConfigurationIssue(string code, string assetPath, string message)
        {
            Code = string.IsNullOrWhiteSpace(code) ? throw new ArgumentException("An issue code is required.", nameof(code)) : code;
            AssetPath = assetPath ?? string.Empty;
            Message = string.IsNullOrWhiteSpace(message) ? throw new ArgumentException("An issue message is required.", nameof(message)) : message;
        }

        public string Code { get; }
        public string AssetPath { get; }
        public string Message { get; }
        public override string ToString() => $"[{Code}] {AssetPath}: {Message}";
    }

    public sealed class ConfigurationValidationResult
    {
        readonly ConfigurationIssue[] _issues;

        public ConfigurationValidationResult(IEnumerable<ConfigurationIssue> issues)
        {
            _issues = (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray();
        }

        public IReadOnlyList<ConfigurationIssue> Issues => _issues;
        public bool IsValid => _issues.Length == 0;
        public string Format() => IsValid ? "Configuration is valid." : string.Join(Environment.NewLine, _issues.Select(issue => issue.ToString()));
    }
}
