using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Configuration.Editor
{
    public sealed class ContentSceneAuthoringWindow : EditorWindow
    {
        Vector2 _scroll;
        string _report = "Select Validate All.";

        [MenuItem("Botanical Garden QR/Content Authoring")]
        static void Open() => GetWindow<ContentSceneAuthoringWindow>("Content Authoring");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Commercial Content Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Content scenes, recognition routes, global UI defaults, and runtime options remain separate authoritative assets. This window validates authoring only; the automated release pipeline owns generated runtime assets.",
                MessageType.Info);

            if (GUILayout.Button("Validate All"))
                _report = ContentSceneConfigurationValidator.ValidateAll().Format();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.SelectableLabel(_report, EditorStyles.textArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }
}
