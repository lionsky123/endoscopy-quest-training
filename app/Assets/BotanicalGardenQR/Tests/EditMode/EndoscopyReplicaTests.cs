using System.Collections;
using System.Linq;
using BotanicalGardenQR.Configuration.Editor;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using NUnit.Framework;
using Oculus.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class EndoscopyReplicaTests
    {
        [Test] public void ReplicatedProjectPassesReadOnlyArchitectureValidation()
        {
            // Read-only configuration inspection; never invokes a build callback or BuildPlayer.
            BotanicalGardenQR.Editor.Validation.ReleaseBuildSafety.ValidateOrThrow();
            var result=BotanicalGardenQR.Editor.Validation.CommercialArchitectureValidator.Validate(BotanicalGardenQR.Editor.Validation.ValidationScope.All);
            Assert.That(result.IsValid,Is.True,result.Format());
        }
        static int presses;
        static void RecordPress(){presses++;}
        [Test] public void SixPublishedStopsContainClinicalMediaAndThirteenQuestions()
        {
            var result=ContentScenePublisher.PublishContentLibrary();Assert.That(result.Succeeded,Is.True,result.Validation.Format());
            var library=AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>(ContentScenePublisher.DefaultLibraryPath);
            var ids=new[]{"giant_saguaro","baobab","bottle_tree","ceiba","macrozamia","welwitschia"};
            var counts=new[]{3,3,0,3,0,4};Assert.That(library.Packages.Count,Is.EqualTo(6));
            for(int i=0;i<6;i++)
            {
                Assert.That(library.TryGet(new SceneId(ids[i]),out var package),Is.True);
                var content=(ContentSpec)typeof(ScenePackage).GetField("_content",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(package);Assert.That(content.Title,Does.Contain(i%2==0?"正确":"错乱"));
                Assert.That(content.Model,Is.Null);Assert.That(content.Video,Is.Null);Assert.That(content.Narration,Is.Null);
                Assert.That(content.KnowledgeMiniGame?.Questions.Count??0,Is.EqualTo(counts[i]));
                Assert.That(content.Panorama,Is.Not.Null,ids[i]+" panorama missing after publication");
                Assert.That(content.ImageRing,Is.Not.Null,ids[i]+" image ring missing after publication");
                var mediaRoot="Assets/BotanicalGardenQR/Content/Scenes/"+ids[i]+"/Endoscopy/";
                Assert.That(AssetDatabase.GetAssetPath(content.Panorama.Texture),Does.StartWith(mediaRoot));
                Assert.That(content.ImageRing.Items.Count,Is.EqualTo(3));
                foreach(var item in content.ImageRing.Items)Assert.That(AssetDatabase.GetAssetPath(item.Image),Does.StartWith(mediaRoot));
            }
        }
        [Test] public void ReplicaKeepsOneChineseApp()
        {
            Assert.That(PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android),Is.EqualTo("com.endoscopy.inspection"));
            Assert.That(PlayerSettings.productName,Is.EqualTo("内镜中心监督检查"));
            Assert.That(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path),Is.EquivalentTo(new[]{"Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity"}),
                "The learner build must launch the guided journey; the authoring scene stays disabled.");
            var composition=AssetDatabase.LoadAllAssetsAtPath("Assets/XR/Settings/OpenXR Package Settings.asset")
                .Single(asset=>asset.name=="OpenXRCompositionLayersFeature Android");
            Assert.That(new SerializedObject(composition).FindProperty("m_enabled").boolValue,Is.True,"Android Composition Layers Support must remain enabled.");
        }
        [Test] public void CurrentGuidedJourneyKeepsPlayerAudioEnabled()
        {
            Assert.That(new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0]).FindProperty("m_DisableAudio").boolValue,Is.False,
                "The current guided journey uses authorized button feedback and background music.");
        }
        [Test] public void ActualMetaSurfaceCatchesFingerSweepAndRejectsPointerSubmit()
        {
            // Isolate SDK surfaces in EditMode: entering PlayMode boots the template's
            // native MRUK runtime, which requires a working desktop XR environment.
            var go=new GameObject("Replica button",typeof(RectTransform),typeof(Image),typeof(NearOnlyButton));
            var finger=new GameObject("Test-only fingertip");var eventObject=new GameObject("Event system",typeof(EventSystem));
            PokeInteractor poke=null;
            PokeInteractable surface=null;
            try
            {
                var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(446,68);rect.localScale=Vector3.one*.00065f;
                var button=go.GetComponent<NearOnlyButton>();button.targetGraphic=go.GetComponent<Image>();presses=0;button.onClick.AddListener(RecordPress);
                ClinicalNearTouch.Bind(go.transform);
                var events=eventObject.GetComponent<EventSystem>();button.OnPointerClick(new PointerEventData(events));button.OnSubmit(new BaseEventData(events));Assert.That(presses,Is.Zero);
                var adapter=go.GetComponent<ClinicalNearTouch>();
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(ClinicalNearTouch).GetField("readyAt",flags).SetValue(adapter,float.NegativeInfinity);
                typeof(ClinicalNearTouch).GetField("lastCommit",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).SetValue(null,float.NegativeInfinity);
                foreach(var component in go.GetComponents<MonoBehaviour>())
                {
                    if(component is ClinicalNearTouch||component is UnityEngine.EventSystems.UIBehaviour)continue;
                    Lifecycle(component,"Awake");Lifecycle(component,"Start");
                }
                surface=go.GetComponent<PokeInteractable>();surface.Enable();
                finger.transform.position=new Vector3(0,0,-.06f);
                poke=finger.AddComponent<PokeInteractor>();Lifecycle(poke,"Awake");poke.InjectAllPokeInteractor(finger.transform,.008f);poke.IsRootDriver=false;
                float time=1;poke.SetTimeProvider(()=>time);Lifecycle(poke,"Start");
                poke.Drive();Assert.That(poke.State,Is.EqualTo(InteractorState.Hover),"Front approach must hover the actual surface.");
                time+=.02f;finger.transform.position=new Vector3(0,0,-.05f);poke.Drive();
                time+=.02f;finger.transform.position=new Vector3(0,0,.05f);poke.Drive();Assert.That(presses,Is.EqualTo(1),"State: "+poke.State+" button active: "+button.IsActive()+" interactable: "+button.IsInteractable());
                for(int i=0;i<8;i++){time+=.02f;poke.Drive();}Assert.That(presses,Is.EqualTo(1));
                poke.Disable();Assert.That(presses,Is.EqualTo(1));
            }
            finally
            {
                if(poke)poke.Disable();
                if(surface)surface.Disable();
                Object.DestroyImmediate(finger);Object.DestroyImmediate(go);Object.DestroyImmediate(eventObject);
            }
        }
        static void Lifecycle(MonoBehaviour target,string name)
        {
            target.GetType().GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(target,null);
        }
    }
}
