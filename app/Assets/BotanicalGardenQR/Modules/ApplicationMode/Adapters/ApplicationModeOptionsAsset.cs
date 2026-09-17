using BotanicalGardenQR.ApplicationMode.Contracts;
using UnityEngine;

namespace BotanicalGardenQR.ApplicationMode.Adapters
{
    [CreateAssetMenu(
        fileName = "ApplicationModeOptions",
        menuName = "Botanical Garden/Application Mode Options")]
    public sealed class ApplicationModeOptionsAsset : ScriptableObject
    {
        [SerializeField, Min(0.1f)] float _menuHoldSeconds = 1.2f;
        [SerializeField, Min(1f)] float _promptTimeoutSeconds = 30f;
        [SerializeField, Min(0)] int _visitorSceneBuildIndex;
        [SerializeField, Min(0)] int _administratorSceneBuildIndex = 1;

        public float MenuHoldSeconds => _menuHoldSeconds;
        public float PromptTimeoutSeconds => _promptTimeoutSeconds;

        public bool TryGetBuildIndex(ApplicationModeRole role, out int buildIndex)
        {
            buildIndex = role == ApplicationModeRole.Visitor
                ? _visitorSceneBuildIndex
                : _administratorSceneBuildIndex;
            return buildIndex >= 0;
        }

        public bool TryValidate(out string reason) =>
            TryValidate(ApplicationModeRole.Visitor, out reason);

        public bool TryValidate(ApplicationModeRole role, out string reason)
        {
            if (!IsPositiveFinite(_menuHoldSeconds))
            {
                reason = "Menu hold duration must be finite and greater than zero.";
                return false;
            }
            if (!IsPositiveFinite(_promptTimeoutSeconds))
            {
                reason = "Prompt timeout must be finite and greater than zero.";
                return false;
            }
            if (_visitorSceneBuildIndex < 0 || _administratorSceneBuildIndex < 0 ||
                _visitorSceneBuildIndex == _administratorSceneBuildIndex)
            {
                reason = "Visitor and Administrator must use distinct non-negative build indexes.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        static bool IsPositiveFinite(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
