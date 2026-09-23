using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Explicit asset migration; never builds or deploys a player.
    public static class LobbyPanoramaPublish
    {
        public static void Run()
        {
            int code=1;GameObject root=null;
            try
            {
                const string image="Assets/EndoscopyTheme/Panoramas/Lobby360.png";
                const string output="Assets/EndoscopyTheme/Resources/FullScriptRooms/Stationary";
                AssetDatabase.Refresh();
                var importer=(TextureImporter)AssetImporter.GetAtPath(image);
                importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
                importer.mipmapEnabled=true;importer.isReadable=false;importer.maxTextureSize=4096;
                importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=TextureWrapMode.Clamp;
                importer.filterMode=FilterMode.Trilinear;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {name="Android",overridden=true,maxTextureSize=4096,format=TextureImporterFormat.ASTC_6x6});
                importer.SaveAndReimport();
                var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/BotanicalGardenQR/Modules/Panorama/Backend/Resources/PanoramaEquirectangular.shader");
                if(!shader)throw new InvalidOperationException("Panorama shader missing.");
                string materialPath="Assets/EndoscopyTheme/Panoramas/Lobby360.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
                material.shader=shader;material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(image);EditorUtility.SetDirty(material);
                root=GameObject.CreatePrimitive(PrimitiveType.Sphere);root.name="LobbyPanorama";
                UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());
                root.transform.localScale=Vector3.one*80;
                var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                PrefabUtility.SaveAsPrefabAsset(root,output+"/LobbyPanorama.prefab");
                const string archive="Assets/EndoscopyTheme/ArchivedLobbyGaussian";
                Directory.CreateDirectory(archive);AssetDatabase.Refresh();
                string old=output+"/LobbyGaussian.prefab";
                if(AssetDatabase.LoadAssetAtPath<GameObject>(old))
                {string error=AssetDatabase.MoveAsset(old,archive+"/LobbyGaussian.prefab");if(error!="")throw new IOException(error);}
                var data=AssetDatabase.LoadMainAssetAtPath("Assets/Universal Render Pipeline Asset_Renderer.asset");
                var serialized=new SerializedObject(data);var features=serialized.FindProperty("m_RendererFeatures");
                for(int i=features.arraySize-1;i>=0;i--)
                {
                    var feature=features.GetArrayElementAtIndex(i).objectReferenceValue;
                    if(feature && feature.GetType().Name=="GaussianSplatURPFeature")
                    {features.GetArrayElementAtIndex(i).objectReferenceValue=null;features.DeleteArrayElementAtIndex(i);serialized.ApplyModifiedPropertiesWithoutUndo();UnityEngine.Object.DestroyImmediate(feature,true);}
                }
                EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();AssetDatabase.Refresh();code=0;
            }
            catch(Exception e){Debug.LogException(e);}
            finally{if(root)UnityEngine.Object.DestroyImmediate(root);}
            EditorApplication.Exit(code);
        }
    }
}
