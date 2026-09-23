using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    public static class StationaryEntryPublish
    {
        public const string PrefabPath = "Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab";
        static readonly string[] Fields = { "_sceneLibrary", "_contentEntries", "_collectionCatalog", "_physicalAugmentationCatalog" };
        public static void Publish()
        {
            int code = 1;
            GameObject root = null;
            try
            {
                var args = Environment.GetCommandLineArgs();
                var output = Path.GetFullPath(args[Array.IndexOf(args, "-bgqrEntryOutput") + 1]);
                Directory.CreateDirectory(output);
                var backup = Path.Combine(output, "VisitorRuntime.before-dependency-isolation.prefab");
                if (!File.Exists(backup)) File.Copy(PrefabPath, backup);
                var before = AssetDatabase.GetDependencies(PrefabPath, true);
                root = PrefabUtility.LoadPrefabContents(PrefabPath);
                var installer = root.GetComponentsInChildren<VisitorInstaller>(true).Single();
                var serialized = new SerializedObject(installer);
                var changes = Fields.Select(field => field + "=" + AssetDatabase.GetAssetPath(serialized.FindProperty(field).objectReferenceValue)).ToArray();
                foreach (var field in Fields) serialized.FindProperty(field).objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.UnloadPrefabContents(root); root = null;
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
                var after = AssetDatabase.GetDependencies(PrefabPath, true);
                File.WriteAllLines(Path.Combine(output, "entry-dependencies-before.txt"), before);
                File.WriteAllLines(Path.Combine(output, "entry-dependencies-after.txt"), after);
                File.WriteAllLines(Path.Combine(output, "entry-dependencies-removed.txt"), before.Except(after));
                File.WriteAllLines(Path.Combine(output, "entry-fields.txt"), changes);
                if (after.Any(path => path.Contains("/Content/Scenes/") || path.EndsWith("/ContentSceneLibrary.asset")))
                    throw new InvalidOperationException("An indirect archived course dependency remains; inspect the dependency report.");
                code = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            finally { if (root) PrefabUtility.UnloadPrefabContents(root); }
            EditorApplication.Exit(code);
        }
    }
}
