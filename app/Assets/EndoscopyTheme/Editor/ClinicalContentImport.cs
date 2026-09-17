using System;
using System.IO;
using BotanicalGardenQR.Configuration.Runtime;
using UnityEditor;
using UnityEngine;

namespace EndoscopyTheme.Editor
{
    // Explicit authoring command only. Writes managed references through Unity, never builds a player.
    public static class ClinicalContentImport
    {
        [MenuItem("Endoscopy/Update First Lesson Content")]
        public static void UpdateFirstLesson()
        {
            const string folder = "Assets/BotanicalGardenQR/Content/Scenes/giant_saguaro/Endoscopy";
            const string panoramaPath = folder + "/cleaning-room-360-v2.png";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(panoramaPath);
            if (texture == null) throw new InvalidOperationException("First lesson panorama is missing.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(panoramaPath);
            importer.maxTextureSize = 8192;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 8192;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(panoramaPath);
            if (texture.width != 7680 || texture.height != 3840)
                throw new InvalidOperationException("The supplied panorama must retain its 7680 x 3840 pixels.");
            var asset = AssetDatabase.LoadAssetAtPath<ContentSceneConfig>(
                "Assets/BotanicalGardenQR/Content/Authoring/Scenes/giant_saguaro/ContentSceneConfig.asset");
            var serialized = new SerializedObject(asset);
            serialized.FindProperty("_content._panorama").managedReferenceValue = new PanoramaContentSpec();
            serialized.FindProperty("_content._knowledgeMiniGame").managedReferenceValue =
                JsonUtility.FromJson<KnowledgeMiniGameContentSpec>(File.ReadAllText("Assets/EndoscopyTheme/Editor/first-lesson-quiz.json"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
            serialized.FindProperty("_content._panorama._texture").objectReferenceValue = texture;
            serialized.FindProperty("_content._panorama._readyForTeaching").boolValue = true;
            serialized.FindProperty("_content._summary").stringValue = "观察实体隔断与门、两类内镜工位分设和流程方向。查看教学图谱后，进入 360 全景转身观察。";
            var cards = serialized.FindProperty("_content._imageRing._items");
            var files = new[] { "lesson1-door-comparison.png", "lesson1-equipment-comparison.png", "lesson1-flow-comparison.png" };
            var titles = new[] { "隔断与门 · 正误对照", "槽与机器分设 · 正误对照", "单向衔接 · 正误对照" };
            var descriptions = new[] {
                "左：隔断完整、门关闭。右：门敞开。观察门框与闭合状态。",
                "左：两类槽组与机器分别配置。右：共用一套设备。分设不能只靠标签。",
                "左：按五环节由污到洁。右：向污染端回流。干燥为独立干燥工位。"
            };
            cards.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                string imagePath = folder + "/" + files[i];
                var cardImage = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
                if (cardImage == null) throw new InvalidOperationException("Missing comparison image: " + imagePath);
                var card = cards.GetArrayElementAtIndex(i);
                card.FindPropertyRelative("_image").objectReferenceValue = cardImage;
                card.FindPropertyRelative("_title").stringValue = titles[i];
                card.FindPropertyRelative("_description").stringValue = descriptions[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            var result = BotanicalGardenQR.Configuration.Editor.ContentScenePublisher.PublishContentLibrary();
            if (!result.Succeeded) throw new InvalidOperationException(result.Validation.Format());
            AssetDatabase.SaveAssets();
            Debug.Log("First lesson content reset and new 7680 x 3840 panorama published. No player build.");
        }
        [Serializable] class Manifest { public Stop[] stops; }
        [Serializable] class Stop { public string id; public Card[] cards; public string questionsJson; }
        [Serializable] class Card { public string imageGuid, audioGuid, title, description; }
        public static void Run()
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/EndoscopyTheme/Editor/clinical-content.json"));
            foreach(var stop in manifest.stops)
            {
                string path="Assets/BotanicalGardenQR/Content/Authoring/Scenes/"+stop.id+"/ContentSceneConfig.asset";
                var asset=AssetDatabase.LoadAssetAtPath<ContentSceneConfig>(path);
                var mediaRoot="Assets/BotanicalGardenQR/Content/Scenes/"+stop.id+"/Endoscopy";
                if(!AssetDatabase.IsValidFolder(mediaRoot))AssetDatabase.CreateFolder("Assets/BotanicalGardenQR/Content/Scenes/"+stop.id,"Endoscopy");
                var serialized=new SerializedObject(asset);
                serialized.FindProperty("_content._panorama").managedReferenceValue=new PanoramaContentSpec();
                serialized.FindProperty("_content._imageRing").managedReferenceValue=new ImageRingContentSpec();
                serialized.FindProperty("_content._knowledgeMiniGame").managedReferenceValue=string.IsNullOrEmpty(stop.questionsJson)?null:JsonUtility.FromJson<KnowledgeMiniGameContentSpec>(stop.questionsJson);
                serialized.ApplyModifiedPropertiesWithoutUndo();serialized.Update();
                serialized.FindProperty("_content._panorama._texture").objectReferenceValue=CopyMedia<Texture>("Assets/EndoscopyTheme/Media/washing_panorama.png",mediaRoot);
                var items=serialized.FindProperty("_content._imageRing._items");items.arraySize=stop.cards.Length;
                for(int i=0;i<stop.cards.Length;i++)
                {
                    var item=items.GetArrayElementAtIndex(i);var card=stop.cards[i];
                    item.FindPropertyRelative("_image").objectReferenceValue=CopyMedia<Texture2D>(AssetDatabase.GUIDToAssetPath(card.imageGuid),mediaRoot);
                    item.FindPropertyRelative("_audio").objectReferenceValue=CopyMedia<AudioClip>(AssetDatabase.GUIDToAssetPath(card.audioGuid),mediaRoot);
                    item.FindPropertyRelative("_title").stringValue=card.title;item.FindPropertyRelative("_description").stringValue=card.description;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);
                if(asset.Content.Panorama?.Texture==null||asset.Content.ImageRing.Items.Count!=3)throw new InvalidOperationException("Clinical media import failed: "+stop.id);
            }
            EndoscopyThemeSetup.PublishTheme();
            if (File.Exists("Assets/BotanicalGardenQR/Content/Scenes/giant_saguaro/Endoscopy/cleaning-room-360-v2.png")) UpdateFirstLesson();
        }
        static T CopyMedia<T>(string source,string folder) where T:UnityEngine.Object
        {
            string destination=folder+"/"+Path.GetFileName(source);
            if(!File.Exists(destination)&&!AssetDatabase.CopyAsset(source,destination))throw new IOException("Could not copy clinical media: "+source);
            return AssetDatabase.LoadAssetAtPath<T>(destination);
        }
    }
}
