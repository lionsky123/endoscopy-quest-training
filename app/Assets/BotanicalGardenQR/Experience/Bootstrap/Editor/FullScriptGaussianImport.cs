using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace BotanicalGardenQR.Bootstrap.Editor
{
    // Isolated asset conversion only. No player build, scene installation or pipeline settings writes.
    public static class FullScriptGaussianImport
    {
        public const string Output="Assets/EndoscopyTheme/ImportedModels/LobbyGaussian";
        public static void Preview()
        {
            int exit=1;GameObject root=null,cameraObject=null;
            RenderPipelineAsset pipeline=null;ScriptableObject rendererData=null,feature=null;
            var original=GraphicsSettings.defaultRenderPipeline;
            var originalQuality=QualitySettings.renderPipeline;
            try
            {
                var args=Environment.GetCommandLineArgs();
                var folder=Path.GetFullPath(args[Array.IndexOf(args,"-bgqrCaptureOutput")+1]);
                Directory.CreateDirectory(folder);
                pipeline=UnityEngine.Object.Instantiate(originalQuality?originalQuality:original);
                pipeline.GetType().GetProperty("msaaSampleCount").SetValue(pipeline,1);
                var serialized=new SerializedObject(pipeline);
                var renderers=serialized.FindProperty("m_RendererDataList");
                rendererData=UnityEngine.Object.Instantiate((ScriptableObject)renderers.GetArrayElementAtIndex(0).objectReferenceValue);
                var featureType=Type.GetType("GaussianSplatting.Runtime.GaussianSplatURPFeature, GaussianSplatting",true);
                feature=ScriptableObject.CreateInstance(featureType);
                ((IList)rendererData.GetType().GetProperty("rendererFeatures").GetValue(rendererData)).Add(feature);
                featureType.GetMethod("Create").Invoke(feature,null);
                renderers.arraySize=1;renderers.GetArrayElementAtIndex(0).objectReferenceValue=rendererData;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
                root=new GameObject("LobbyGaussianSample");root.SetActive(false);
                root.transform.rotation=Quaternion.Euler(-90,0,0);
                var type=Type.GetType("GaussianSplatting.Runtime.GaussianSplatRenderer, GaussianSplatting",true);
                var splat=root.AddComponent(type);
                type.GetField("m_Asset").SetValue(splat,AssetDatabase.LoadMainAssetAtPath(Output+"/b660f052d2670589c7476bc95309ed56.asset"));
                type.GetField("m_SHOrder").SetValue(splat,0);
                const string package="Packages/org.nesnausk.gaussian-splatting/Shaders/";
                string[] fields={"m_ShaderSplats","m_ShaderComposite","m_ShaderDebugPoints","m_ShaderDebugBoxes"};
                string[] paths={"RenderGaussianSplats","GaussianComposite","GaussianDebugRenderPoints","GaussianDebugRenderBoxes"};
                for(int i=0;i<fields.Length;i++)type.GetField(fields[i]).SetValue(splat,AssetDatabase.LoadAssetAtPath<Shader>(package+paths[i]+".shader"));
                type.GetField("m_CSSplatUtilities").SetValue(splat,AssetDatabase.LoadAssetAtPath<ComputeShader>(package+"SplatUtilities.compute"));
                root.SetActive(true);
                cameraObject=new GameObject("GaussianPreviewCamera",typeof(Camera));
                var camera=cameraObject.GetComponent<Camera>();camera.allowMSAA=false;camera.allowHDR=true;
                camera.fieldOfView=85;camera.nearClipPlane=.03f;camera.farClipPlane=100;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.1f,.1f);
                for(int view=0;view<9;view++)
                {
                    var position=view>=5?new Vector3(0,-.95f,0):view==4?Vector3.right:Vector3.zero;
                    camera.transform.SetPositionAndRotation(position,Quaternion.Euler(0,(view>=5?view-5:view%4)*90,0));
                    Save(camera,Path.Combine(folder,$"lobby-{view:00}.png"));
                }
                File.WriteAllText(Path.Combine(folder,"preview-notes.txt"),
                    "Imported 793729 SH0 splats; editor Vulkan / Android target / URP render graph; temporary cloned pipeline with MSAA=1; source pipeline untouched.\nSource rotation X=-90 is provisional; views 0-3 at origin, view 4 translated one source unit, views 5-8 lowered .95 source units. Source units are not calibrated metres. Not Quest/XR, scale, route, collision or performance acceptance.\n");
                exit=0;
            }
            catch(Exception e){Debug.LogException(e);}
            finally
            {
                if(root)UnityEngine.Object.DestroyImmediate(root);if(cameraObject)UnityEngine.Object.DestroyImmediate(cameraObject);
                GraphicsSettings.defaultRenderPipeline=original;QualitySettings.renderPipeline=originalQuality;
                if(pipeline)UnityEngine.Object.DestroyImmediate(pipeline);if(feature)UnityEngine.Object.DestroyImmediate(feature);if(rendererData)UnityEngine.Object.DestroyImmediate(rendererData);
            }
            EditorApplication.Exit(exit);
        }
        static void Save(Camera camera,string file)
        {
            var target=new RenderTexture(1280,960,24);target.Create();var old=RenderTexture.active;Texture2D picture=null;
            try
            {
                camera.targetTexture=target;picture=new Texture2D(1280,960,TextureFormat.RGB24,false);
                for(int pass=0;pass<3;pass++){camera.Render();RenderTexture.active=target;picture.ReadPixels(new Rect(0,0,1280,960),0,0);picture.Apply();}
                File.WriteAllBytes(file,picture.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=old;if(picture)UnityEngine.Object.DestroyImmediate(picture);target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
        public static void Prepare()
        {
            int exit=1;ScriptableObject creator=null;
            try
            {
                var args=Environment.GetCommandLineArgs();
                var source=Path.GetFullPath(args[Array.IndexOf(args,"-bgqrLobbySource")+1]);
                var report=Path.GetFullPath(args[Array.IndexOf(args,"-bgqrCaptureOutput")+1]);
                var before=Hash(source);
                if(before!="61ec49842db08f6c06c18213bb6a6811ddf64eb2aef2b37d99859abf39c47913")
                    throw new InvalidDataException("Lobby input differs from the preserved user sample.");
                var type=Type.GetType("GaussianSplatting.Editor.GaussianSplatAssetCreator, GaussianSplattingEditor",true);
                creator=ScriptableObject.CreateInstance(type);
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                type.GetField("m_InputFile",flags).SetValue(creator,source);
                type.GetField("m_OutputFolder",flags).SetValue(creator,Output);
                type.GetField("m_ImportCameras",flags).SetValue(creator,false);
                var quality=type.GetField("m_Quality",flags);
                quality.SetValue(creator,Enum.Parse(quality.FieldType,"Medium"));
                type.GetMethod("ApplyQualityLevel",flags).Invoke(creator,null);
                type.GetMethod("CreateAsset",flags).Invoke(creator,null);
                var error=type.GetField("m_ErrorMessage",flags).GetValue(creator) as string;
                if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
                var assetPath=Output+"/"+Path.GetFileNameWithoutExtension(source)+".asset";
                var asset=AssetDatabase.LoadMainAssetAtPath(assetPath);
                if(!asset)throw new InvalidOperationException("Gaussian asset missing after conversion.");
                var count=(int)asset.GetType().GetProperty("splatCount").GetValue(asset);
                if(count!=793729)throw new InvalidDataException("Unexpected splat count.");
                if(Hash(source)!=before)throw new InvalidDataException("Source changed during conversion.");
                Directory.CreateDirectory(report);
                File.WriteAllText(Path.Combine(report,"import-report.json"),JsonUtility.ToJson(new Report
                { sourceSha256=before,asset=assetPath,splats=count },true));
                AssetDatabase.SaveAssets();exit=0;
            }
            catch(Exception e){Debug.LogException(e);}
            finally{EditorUtility.ClearProgressBar();if(creator)UnityEngine.Object.DestroyImmediate(creator);}
            EditorApplication.Exit(exit);
        }
        static string Hash(string path)
        {using(var stream=File.OpenRead(path))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        [Serializable] sealed class Report
        {
            public string sourceSha256,asset;
            public int splats;
            public string dependencyCommit="2c6fed37da67a217367261fcfcd3316d34c73e76";
            public string status="Imported only; no scene binding or Quest acceptance";
            public string encoding="Medium; SH0 source, absent higher coefficients remain zero";
        }
    }
}
