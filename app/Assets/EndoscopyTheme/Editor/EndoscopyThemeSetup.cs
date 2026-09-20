using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;

namespace EndoscopyTheme.Editor
{
    [InitializeOnLoad]
    public static class EndoscopyThemeSetup
    {
        static EndoscopyThemeSetup(){EditorApplication.delayCall+=Apply;}
        public static void Apply()
        {
            PlayerSettings.companyName="EndoscopyTraining";
            PlayerSettings.productName="内镜中心监督检查";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.endoscopy.inspection");
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if(settings)foreach(var feature in settings.GetFeatures())
                if(feature.GetType().Name=="OpenXRCompositionLayersFeature") {feature.enabled=true;EditorUtility.SetDirty(feature);}
            var audio=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0]);
            audio.FindProperty("m_DisableAudio").boolValue=true;audio.ApplyModifiedPropertiesWithoutUndo();
            // VR is the application environment, including the system loading backdrop.
            var config=AssetDatabase.LoadMainAssetAtPath("Assets/Oculus/OculusProjectConfig.asset");
            if(config)
            {
                var vr=new SerializedObject(config);
                foreach(var property in new[]{"anchorSupport","sceneSupport","_insightPassthroughSupport","_systemLoadingScreenBackground"})
                    vr.FindProperty(property).intValue=0;
                vr.FindProperty("insightPassthroughEnabled").boolValue=false;
                vr.FindProperty("isPassthroughCameraAccessEnabled").boolValue=false;
                vr.ApplyModifiedPropertiesWithoutUndo();
            }
            // Meta creates this editor connection asset on import. This teaching app does
            // not use DevAgent; exclude the generated asset before the template's gate.
            const string generatedAgent="Assets/Resources/DevAgentSettings.asset";
            if(AssetDatabase.LoadMainAssetAtPath(generatedAgent)!=null&&!AssetDatabase.DeleteAsset(generatedAgent))
                throw new InvalidOperationException("Could not exclude generated DevAgent settings from the teaching app.");
            AssetDatabase.SaveAssets();
        }
        public static void PublishTheme()
        {
            Apply();BotanicalGardenQR.Configuration.Editor.ContentScenePublishCli.Run();
            Debug.Log("C09 VR endoscopy theme published. No player build was requested.");
        }
    }
    // Invoked only by an explicit user build. Keeps this replica on the one Chinese app identity.
    public sealed class EndoscopyBuildIdentity:IPreprocessBuildWithReport,IPostprocessBuildWithReport
    {
        public int callbackOrder=>-2100;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.Android)return;
            EndoscopyThemeSetup.Apply();
            if(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android)!="com.endoscopy.inspection")throw new BuildFailedException("Endoscopy package identity mismatch.");
            PlayerSettings.Android.bundleVersionCode=Math.Max(14,PlayerSettings.Android.bundleVersionCode+1);
        }
        public void OnPostprocessBuild(BuildReport report)
        {
            if(report.summary.platform!=BuildTarget.Android||report.summary.result!=BuildResult.Succeeded)return;
            var path=report.summary.outputPath;if(!File.Exists(path))return;
            using var stream=File.OpenRead(path);using var hash=System.Security.Cryptography.SHA256.Create();
            string digest=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");
            File.WriteAllText(path+".receipt.json",JsonUtility.ToJson(new Receipt{packageId="com.endoscopy.inspection",version=PlayerSettings.bundleVersion,revision="C09",code=PlayerSettings.Android.bundleVersionCode,buildId=Guid.NewGuid().ToString("N"),builtUtc=DateTime.UtcNow.ToString("O"),apkPath=path,apkSha256=digest},true));
        }
        [Serializable] sealed class Receipt {public string packageId,version,revision,buildId,builtUtc,apkPath,apkSha256;public int code;}
    }
}
