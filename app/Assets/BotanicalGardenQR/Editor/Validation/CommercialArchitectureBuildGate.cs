using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BotanicalGardenQR.Editor.Validation
{
    public sealed class CommercialArchitectureBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            // A deliberate Build And Run is the local iteration path.
            // The repository validation script and commercial builds still run the full
            // architecture gate in their isolated/release inputs.
            if (ReleaseBuildSafety.IsLocalBuildAndRun(report.summary.options))
                return;

            var validation = CommercialArchitectureValidator.Validate(ValidationScope.All);
            if (!validation.IsValid)
                throw new BuildFailedException("Commercial architecture validation failed. No assets were modified.\n" + validation.Format());
        }
    }
}
