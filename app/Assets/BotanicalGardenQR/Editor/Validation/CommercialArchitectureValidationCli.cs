using System;
using System.Linq;
using UnityEngine;

namespace BotanicalGardenQR.Editor.Validation
{
    public static class CommercialArchitectureValidationCli
    {
        public static void Run()
        {
            var scope = ParseScope(Environment.GetCommandLineArgs());
            var report = CommercialArchitectureValidator.Validate(scope);
            if (!report.IsValid)
                throw new InvalidOperationException($"Commercial architecture validation failed for scope {scope} with {report.Issues.Count} error(s):\n{report.Format()}");

            Debug.Log($"Commercial architecture validation passed for scope {scope}.");
        }

        static ValidationScope ParseScope(string[] arguments)
        {
            var index = Array.FindIndex(arguments, x => string.Equals(x, "-scope", StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index + 1 >= arguments.Length) return ValidationScope.All;
            if (Enum.TryParse(arguments[index + 1], true, out ValidationScope scope) && Enum.GetNames(typeof(ValidationScope)).Contains(scope.ToString())) return scope;
            throw new ArgumentException($"Unknown validation scope '{arguments[index + 1]}'.");
        }
    }
}
