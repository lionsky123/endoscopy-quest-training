using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.FrontendShell.Runtime;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Editor.Validation
{
    // Keeps the authored guide theme in sync with the shared room palette.
    public static class RoomUiPaletteMigration
    {
        const string ThemePath = "Assets/BotanicalGardenQR/Content/Authoring/VisitorCoachTheme.asset";

        public static void Apply()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(ThemePath);
            if (!theme) throw new System.InvalidOperationException("Visitor coach theme asset is missing.");

            var serialized = new SerializedObject(theme);
            serialized.FindProperty("_panelColor").colorValue = ClinicalPanelStyle.Surface;
            serialized.FindProperty("_textColor").colorValue = ClinicalPanelStyle.TextPrimary;
            serialized.FindProperty("_detailTextColor").colorValue = ClinicalPanelStyle.Muted;
            serialized.FindProperty("_accentColor").colorValue = ClinicalPanelStyle.Accent;
            serialized.FindProperty("_speakerColor").colorValue = ClinicalPanelStyle.Accent;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[Room UI] Visitor coach palette synchronized.");
        }
    }
}
