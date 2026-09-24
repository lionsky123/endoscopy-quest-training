using System;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.VisitorCoach.Frontend;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class EquipmentTeachingTests
    {
        FullScriptJourneyRuntime _runtime;
        VisitorRuntimeBindings _bindings;

        [SetUp] public void Setup()
        {
            var scene=EditorSceneManager.OpenScene("Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity",OpenSceneMode.Single);
            var installer=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<VisitorInstaller>(true)).Single();
            var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(installer.gameObject);
            if(prefab)PrefabUtility.UnpackPrefabInstance(prefab,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            _bindings=installer.CreateValidatedBindings();
            var rig=_bindings.Platform.XrRigRoot.GetComponentInChildren<OVRCameraRig>(true);
            rig.EnsureGameObjectIntegrity();rig.centerEyeAnchor.localPosition=new Vector3(0,1.15f,0);
            _runtime=new FullScriptJourneyRuntime(_bindings,null,new Timing(),new Release());
            _runtime.StartExperience();
            (typeof(OVRCameraRig).GetField("UpdatedAnchors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(rig) as Action<OVRCameraRig>)?.Invoke(rig);
            Frames();
        }

        [TearDown] public void Cleanup()
        {
            _runtime?.Dispose();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }

        [Test] public void VisibleHotspotsTrackThreeIndependentViewsAndReleaseOnExit()
        {
            TouchGuide();
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(1))
            {
                Assert.That(_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door),Is.True);
                Touch("Travel_"+room);
                if(room==FullScriptRoomCatalog.Washing)break;
            }
            Touch("NextTask");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("RE-02"));
            Touch("InspectEquipmentImage");
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("EquipmentTeachingPanel"));
            var teaching=Resources.Load<Texture2D>("ClinicalCourse/FullScriptVisuals/equipment-hotspot-teaching-v1");
            var source=Resources.Load<Texture2D>("ClinicalCourse/FullScriptVisuals/script-equipment");
            Assert.That(teaching,Is.Not.Null);Assert.That(source,Is.Not.Null);
            Assert.That(teaching.width*source.height,Is.EqualTo(source.width*teaching.height),"Both image variants must keep the same hotspot geometry.");
            Assert.That(_runtime.Visit.Panel.transform.Find("EquipmentReference").GetComponent<RawImage>().texture,Is.SameAs(teaching));
            var baseline=_runtime.Session.LearningActionCount("RE-02",ClinicalLearningAction.Observed);
            foreach(var key in new[]{"air-gun","leak-tester","water-gun"})
            {
                var picture=_runtime.Visit.Panel.transform.Find("EquipmentReference");
                Assert.That(picture.Find("EquipmentHotspot_"+key),Is.Not.Null);
                Touch("EquipmentHotspot_"+key);
                Assert.That(_runtime.ScriptActions.Contains("RE-02:equipment:"+key),Is.True);
            }
            Assert.That(_runtime.Session.LearningActionCount("RE-02",ClinicalLearningAction.Observed),Is.EqualTo(baseline+3));
            Touch("EquipmentHotspot_air-gun");
            Assert.That(_runtime.Session.LearningActionCount("RE-02",ClinicalLearningAction.Observed),Is.EqualTo(baseline+3));
            Assert.That(_runtime.Visit.Panel.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("EquipmentHotspot_",StringComparison.Ordinal)),Is.EqualTo(3));
            Touch("CompareEquipmentSource");
            Assert.That(_runtime.Visit.Panel.transform.Find("EquipmentReference").GetComponent<RawImage>().texture,Is.SameAs(source));
            _runtime.Session.TryGetTask("RE-02",out var task);
            Assert.That(task.Status,Is.Not.EqualTo(ClinicalJourneyTaskStatus.Completed));
            var panel=_runtime.Visit.Panel;
            Touch("ReturnFromEquipment");
            Assert.That(panel==null,Is.True);
            Touch("InspectEquipmentImage");
            Assert.That(_runtime.Visit.Panel.GetComponentsInChildren<Button>().Single(b=>b.name=="EquipmentChoice_air-gun")
                .GetComponentInChildren<TMPro.TMP_Text>().text,Does.Contain("已查阅"));
            panel=_runtime.Visit.Panel;
            _runtime.Dispose();_runtime=null;
            Assert.That(panel==null,Is.True);
        }

        void Touch(string name)
        {
            var panel=_runtime.Visit.Panel;
            var button=panel.GetComponentsInChildren<Button>().Single(b=>b.name==name);
            using(var hand=new ClinicalHandFixture(panel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(button);
            Frames();
            var guide=FindGuide();
            if(guide && guide.CurrentState?.Owner==VisitorDialogueOwner.Guidance)TouchGuide();
            if(_runtime.Visit.Panel?.name=="RoomThemeSelector")Touch("Theme_0");
            if(_runtime.Visit.Panel?.name=="RoomThemeTasks")
                Touch(_runtime.Visit.Panel.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("ThemeTask_",StringComparison.Ordinal)).name);
            if(_runtime.Visit.Panel?.name=="WaitingObservationSelector")Touch("ThemeBack");
        }
        VisitorCoachPresenter FindGuide()
            =>UnityEngine.Object.FindObjectsByType<VisitorCoachPresenter>(FindObjectsInactive.Include,FindObjectsSortMode.None).SingleOrDefault();
        void TouchGuide()
        {
            var guide=FindGuide();
            if(guide?.CurrentState?.Owner!=VisitorDialogueOwner.Guidance)return;
            var button=guide.GetComponentsInChildren<Button>(true).Single(candidate=>
                candidate.GetComponentInChildren<TMPro.TMP_Text>(true)?.text==guide.CurrentState.PrimaryActionLabel);
            using(var hand=new ClinicalHandFixture(guide.gameObject,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(button);
            Frames();
        }
        void Frames(){for(int i=0;i<40;i++)_runtime.Tick(.05f);}
        sealed class Timing:ITrackingOriginTiming
        {public bool TryGetSampleTime(out double t){t=9;return true;}public bool TryGetChangeTime(OVRManager.TrackingOrigin o,out double t){t=10;return true;}}
        sealed class Release:IClinicalRoomAssetRelease
        {public void Begin(){}public bool IsComplete=>true;}
    }
}
