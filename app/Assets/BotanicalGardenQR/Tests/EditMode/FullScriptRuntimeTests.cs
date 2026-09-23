using System;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.MapNavigation.Contracts;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class FullScriptRuntimeTests
    {
        FullScriptJourneyRuntime _runtime;
        VisitorRuntimeBindings _bindings;
        OVRCameraRig _rig;
        readonly Timing _timing = new Timing();
        readonly ControlledRelease _release = new ControlledRelease();
        [SetUp] public void Setup()
        {
            _timing.Tracked=true;
            var scene=EditorSceneManager.OpenScene("Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity",OpenSceneMode.Single);
            var installer=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<VisitorInstaller>(true)).Single();
            var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(installer.gameObject);
            if(prefab) PrefabUtility.UnpackPrefabInstance(prefab,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            installer.ConfigureArchivedBindingsForEditor();
            _bindings=installer.CreateValidatedBindings();
            _rig=_bindings.Platform.XrRigRoot.GetComponentInChildren<OVRCameraRig>(true);
            _rig.EnsureGameObjectIntegrity();
            _rig.centerEyeAnchor.localPosition=new Vector3(0,1.65f,0);
            _release.Complete=true;_release.Begun=0;
            _runtime=new FullScriptJourneyRuntime(_bindings,null,_timing,_release,stationary:false);
            _runtime.StartExperience();Samples();Frames(15);
        }
        [TearDown] public void Cleanup()
        {
            _runtime?.Dispose();_runtime=null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
        [Test] public void StartupLoadsActualLobbyAndPreservesWorldOnHeadMovement()
        {
            Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo("R00_LOBBY"));
            Assert.That(_runtime.Prologue.CurrentState.Phase,Is.EqualTo(VisitorProloguePhase.Invitation));
            Assert.That(_runtime.Visit.Room.Root.GetComponentsInChildren<MeshRenderer>().Length,Is.GreaterThan(6));
            Assert.That(GameObject.Find("VirtualWashingRoom"),Is.Null);
            var world=_runtime.Visit.Room.Root.transform;
            var position=world.position;var rotation=world.rotation;
            _rig.centerEyeAnchor.localPosition+=Vector3.right*.3f;
            _rig.centerEyeAnchor.localRotation=Quaternion.Euler(0,60,0);Samples();Frames(10);
            Assert.That(world.position,Is.EqualTo(position));Assert.That(world.rotation,Is.EqualTo(rotation));
        }
        [Test] public void ImportedGaussianSampleIsNotPreloadedByDevelopmentLobby()
        {
            var type=Type.GetType("GaussianSplatting.Runtime.GaussianSplatAsset, GaussianSplatting",true);
            Assert.That(Resources.FindObjectsOfTypeAll(type).Length,Is.Zero,"Non-runtime lobby data must not be loaded by startup.");
            const string path="Assets/EndoscopyTheme/ImportedModels/LobbyGaussian/b660f052d2670589c7476bc95309ed56.asset";
            Assert.That(path,Does.Not.Contain("/Resources/"));
            var asset=AssetDatabase.LoadMainAssetAtPath(path);
            Assert.That(asset,Is.Not.Null);
            Assert.That(type.GetProperty("splatCount").GetValue(asset),Is.EqualTo(793729));
            Resources.UnloadAsset(asset);
        }
        [Test] public void DoorRequiresArrivalAndSdkPokeThenLoadsOfficeWithoutMovingHeadOrHands()
        {
            _bindings.Platform.Viewer.position+=Vector3.forward*2;
            Assert.That(_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door),Is.False);
            Assert.That(_runtime.RequestRoom("R01_OFFICE"),Is.False);
            MoveToDoor();
            Assert.That(_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door),Is.True);
            var panel=_runtime.Visit.Panel;
            var button=panel.GetComponentsInChildren<Button>().Single(b=>b.name=="Travel_R01_OFFICE");
            using(var hand=new ClinicalHandFixture(panel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze)) hand.Touch(button);
            Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo("R00_LOBBY"),"Poke queues; it cannot synchronously move rooms.");
            var head=_rig.centerEyeAnchor.position;var left=_rig.leftHandAnchor.position;var right=_rig.rightHandAnchor.position;
            Frames(35);
            Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo("R01_OFFICE"));
            Assert.That(_runtime.Visit.Room.Root.name,Is.EqualTo("R01_OFFICE"));
            Assert.That(_runtime.Visit.Map.roomResource,Is.EqualTo("FullScriptRooms/Office"));
            Assert.That(_runtime.Visit.Room.Root.GetComponentsInChildren<MeshRenderer>().Length,Is.InRange(1,100));
            Assert.That(_runtime.Visit.OfficeTerminal.transform.localPosition,Is.EqualTo(_runtime.Visit.Room.TerminalPosition.Value));
            Assert.That(GameObject.Find("R00_LOBBY"),Is.Null);
            Assert.That(Vector3.Distance(head,_rig.centerEyeAnchor.position),Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(left,_rig.leftHandAnchor.position),Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(right,_rig.rightHandAnchor.position),Is.LessThan(.0001f));
        }
        [Test] public void FullRouteLoadsEveryRoomAndRetainsSixWashingStations()
        {
            FinishPrologue();
            foreach(var id in _runtime.Definition.mainlineRoomIds.Skip(1))
            {
                MoveToDoor();Assert.That(_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door),Is.True);
                Assert.That(_runtime.RequestRoom(id),Is.True,id);Frames(35);
                Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo(id));
                Assert.That(_runtime.Prologue.CurrentState.IsExplorationReady,Is.True,"Room changes must not replay the opening.");
                if(id==FullScriptRoomCatalog.Washing)
                {
                    Assert.That(_runtime.Visit.Map.points.Length,Is.EqualTo(7));
                    Assert.That(_runtime.Visit.Map.points.Take(6).Select(p=>p.id),
                        Is.EqualTo(_bindings.Configuration.MapDefinition.points.Select(p=>p.id)));
                    Assert.That(_runtime.Visit.Room.Root.name,Is.EqualTo("VirtualWashingRoom"));
                    Assert.That(_runtime.Visit.Map.routes.Last().samples.Last().x,Is.EqualTo(_runtime.Visit.Map.start.x));
                }
            }
            Assert.That(_runtime.Session.HasVisitedAllMainlineRooms,Is.True);
            Assert.That(_runtime.Session.TryFinishGuidedAtSummary(),Is.True);
            MoveToDoor();Assert.That(_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door),Is.True);
            Assert.That(_runtime.RequestRoom("R02_STORAGE"),Is.True,"Submitted rooms remain available for read-only review.");Frames(35);
            Assert.That(_runtime.Session.TrySkipGuidedTask("ST-01"),Is.False);
        }
        [Test] public void RecordReviewIsHiddenUntilUnifiedSubmissionAndCyclesThroughOfficeFieldsAndStorageWeeks()
        {
            _runtime.SelectMode(ClinicalJourneyMode.IndependentCheck);
            foreach(var id in _runtime.Definition.mainlineRoomIds.Skip(1))
            {
                MoveToDoor();_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);
                Assert.That(_runtime.RequestRoom(id),Is.True);Frames(35);
            }
            Assert.That(_runtime.Visit.TryOpen(FullScriptRoomCatalog.Overview),Is.True);
            Assert.That(_runtime.Visit.Panel.GetComponentsInChildren<Button>().Any(b=>b.name=="RecordReview"),Is.False);
            var submit=_runtime.Visit.Panel.GetComponentsInChildren<Button>().Single(b=>b.name=="SubmitJourney");
            using(var hand=new ClinicalHandFixture(_runtime.Visit.Panel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze)) hand.Touch(submit);
            Assert.That(_runtime.Session.IsSubmitted,Is.True);
            var stablePanel=_runtime.Visit.Panel;
            var pose=stablePanel.transform.position;
            int reviewCount=ClinicalRecordReview.AfterSubmission(_runtime.Session).Length;
            for(int i=0;i<reviewCount+1;i++)
            {
                var review=_runtime.Visit.Panel.GetComponentsInChildren<Button>().Single(b=>b.name=="RecordReview");
                using(var hand=new ClinicalHandFixture(_runtime.Visit.Panel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze)) hand.Touch(review);
                var body=_runtime.Visit.Panel.GetComponentsInChildren<TMPro.TMP_Text>().Single(t=>t.name=="RoomBrief");
                Assert.That(body.text,Does.Contain(((i%reviewCount)+1)+"/"+reviewCount));
                Assert.That(body.text,Does.Contain("未答"));
                body.ForceMeshUpdate();Assert.That(body.isTextOverflowing,Is.False,"Review page "+(i%reviewCount+1));
                Assert.That(_runtime.Visit.Panel,Is.SameAs(stablePanel));
                Assert.That(stablePanel.transform.position,Is.EqualTo(pose));
            }
            foreach(var task in _runtime.Definition.rooms.SelectMany(r=>r.taskIds))
            {
                var next=stablePanel.GetComponentsInChildren<Button>().Single(b=>b.name=="ResultDetails");
                using(var hand=new ClinicalHandFixture(stablePanel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze)) hand.Touch(next);
                var body=stablePanel.GetComponentsInChildren<TMPro.TMP_Text>().Single(t=>t.name=="RoomBrief");
                Assert.That(body.text,Does.Contain(task));
                body.ForceMeshUpdate();Assert.That(body.isTextOverflowing,Is.False,task);
            }
            for(int station=1;station<=6;station++)
            {
                var next=stablePanel.GetComponentsInChildren<Button>().Single(b=>b.name=="ResultDetails");
                using(var hand=new ClinicalHandFixture(stablePanel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze)) hand.Touch(next);
                var body=stablePanel.GetComponentsInChildren<TMPro.TMP_Text>().Single(t=>t.name=="RoomBrief");
                Assert.That(body.text,Does.Contain($"旧洗消 P{station:00}"));
                body.ForceMeshUpdate();Assert.That(body.isTextOverflowing,Is.False);
            }
            Assert.That(_runtime.Session.TryLeaveIndependentUnanswered("OF-01"),Is.False);
        }
        [Test] public void LaterWashingLessonDataSurvivesVisitDisposalButNotNewRun()
        {
            var lessons=BotanicalGardenQR.FrontendShell.Runtime.ClinicalCourseCatalog.Load().lessons;
            var sessions=lessons.Select(_runtime.ResumeWashingLesson).ToArray();
            _runtime.ObservationProgress.Begin();_runtime.ObservationProgress.Record(0,true);
            foreach(var session in sessions){session.Continue();session.Skip();}
            MoveToDoor();_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);
            Assert.That(_runtime.RequestRoom("R01_OFFICE"),Is.True);Frames(35);
            Assert.That(_runtime.ObservationProgress.SkippedCount,Is.EqualTo(1));
            for(int i=0;i<lessons.Length;i++)
            {
                Assert.That(_runtime.ResumeWashingLesson(lessons[i]),Is.SameAs(sessions[i]));
                Assert.That(sessions[i].SkippedCount,Is.EqualTo(1));
                Assert.That(sessions[i].CorrectCount,Is.Zero);
            }
            _runtime.Dispose();
            _runtime=new FullScriptJourneyRuntime(_bindings,null,_timing,_release,stationary:false);
            Assert.That(_runtime.ObservationProgress.Started,Is.False);
            Assert.That(_runtime.ObservationProgress.NextTopic,Is.Zero);
            foreach(var lesson in lessons)
            {
                var fresh=_runtime.ResumeWashingLesson(lesson);
                Assert.That(fresh.SkippedCount,Is.Zero);
                Assert.That(fresh.Phase,Is.EqualTo(BotanicalGardenQR.FrontendShell.Runtime.ClinicalCoursePhase.Introduction));
            }
        }
        [Test] public void TrackingLossDuringTransitionHoldsCommitUntilReliableSamplesReturn()
        {
            MoveToDoor();_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);
            Assert.That(_runtime.RequestRoom("R01_OFFICE"),Is.True);
            _timing.Tracked=false;Samples();Frames(25);
            Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo("R00_LOBBY"));
            Assert.That(_runtime.InputAllowed,Is.False);
            _timing.Tracked=true;Samples();Frames(20);
            Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo("R01_OFFICE"));
        }
        [Test] public void IndependentModeDoesNotLoadWashingTeachingAnswers()
        {
            _runtime.SelectMode(ClinicalJourneyMode.IndependentCheck);
            foreach(var id in _runtime.Definition.mainlineRoomIds.Skip(1).TakeWhile(id=>id!="R01_OFFICE" || !_runtime.Session.HasVisited("R01_OFFICE")))
            {
                MoveToDoor();_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);
                Assert.That(_runtime.RequestRoom(id),Is.True);Frames(35);
            }
            Assert.That(_runtime.Visit.Map.points.Select(p=>p.id),Is.EqualTo(new[]{FullScriptRoomCatalog.Overview,FullScriptRoomCatalog.Door}));
        }
        [Test] public void OfficeComputerOpensThroughHandPokeAndKeepsReadingSeparateFromCompletion()
        {
            MoveToDoor();_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);_runtime.RequestRoom("R01_OFFICE");Frames(35);
            var point=_runtime.Visit.Room.Frame.Transform(_runtime.Visit.Map.points[0].position);
            _bindings.Platform.Viewer.position=new Vector3(point.x,1.65f,point.z);
            Assert.That(_runtime.Visit.TryOpen(FullScriptRoomCatalog.Overview),Is.True);
            Assert.That(_runtime.Visit.Panel,Is.Null,"The enlarged record must wait for a poke on the computer.");
            var terminal=_runtime.Visit.OfficeTerminal;
            using(var hand=new ClinicalHandFixture(terminal,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))
                hand.Touch(terminal.GetComponentsInChildren<Button>().Single());
            var panel=_runtime.Visit.Panel;
            Assert.That(panel,Is.Not.Null);
            var position=panel.transform.position;var rotation=panel.transform.rotation;
            using(var hand=new ClinicalHandFixture(panel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))
                for(var i=0;i<ClinicalTrainingRecords.FieldCount;i++)hand.Touch(panel.transform.Find("RecordField"+i).GetComponent<Button>());
            Assert.That(_runtime.OfficeFieldsViewed.Count,Is.EqualTo(ClinicalTrainingRecords.FieldCount));
            Canvas.ForceUpdateCanvases();
            foreach(var text in panel.GetComponentsInChildren<TMPro.TMP_Text>())
            {text.ForceMeshUpdate();Assert.That(text.isTextOverflowing,Is.False,text.transform.parent.name+"/"+text.name+": "+text.text);}
            _runtime.Session.TryGetTask("OF-01",out var task);
            Assert.That(task.Status,Is.Not.EqualTo(ClinicalJourneyTaskStatus.Completed));
            Assert.That(panel.transform.position,Is.EqualTo(position));Assert.That(panel.transform.rotation,Is.EqualTo(rotation));
            MoveToDoor();_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);_runtime.RequestRoom("R02_STORAGE");Frames(35);
            MoveToDoor();_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);_runtime.RequestRoom("R01_OFFICE");Frames(35);
            Assert.That(_runtime.OfficeFieldsViewed.Count,Is.EqualTo(ClinicalTrainingRecords.FieldCount),"Revisit keeps reading marks within the same process.");
        }
        [Test] public void NewRuntimeAlwaysStartsFreshWithoutResume()
        {
            _runtime.Session.TrySkipGuidedTask("N00");_runtime.OfficeFieldsViewed.Add("date");
            _runtime.OfficeFieldsViewed.Add("patient");_runtime.OfficeFieldsViewed.Add("scope");
            _runtime.OfficeFieldsViewed.Add("start");
            _runtime.Dispose();
            _runtime=new FullScriptJourneyRuntime(_bindings,null,_timing,_release,stationary:false);
            _runtime.StartExperience();Samples();Frames(15);
            Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo("R00_LOBBY"));
            Assert.That(_runtime.OfficeFieldsViewed,Is.Empty);
            Assert.That(_runtime.Session.VisitedRooms.Count,Is.EqualTo(1));
            _runtime.Session.TryGetTask("N00",out var task);
            Assert.That(task.Status,Is.EqualTo(ClinicalJourneyTaskStatus.Unstarted));
        }
        [Test] public void FailedRoomResourceRestoresPreviousRoomBeforeHandRecovery()
        {
            foreach(var id in _runtime.Definition.mainlineRoomIds.Skip(1).TakeWhile(id=>id!=FullScriptRoomCatalog.Washing))
            {MoveToDoor();_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);Assert.That(_runtime.RequestRoom(id),Is.True);Frames(35);}
            var map=_bindings.Configuration.MapDefinition;var digest=map.modelDigest;
            try
            {
                map.modelDigest="invalid-for-resource-failure-test";
                MoveToDoor();_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);
                Assert.That(_runtime.RequestRoom(FullScriptRoomCatalog.Washing),Is.True);Frames(40);
                Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo("R04_RESP"));
                Assert.That(_runtime.Visit.RoomId,Is.EqualTo("R04_RESP"));
                Assert.That(_runtime.InputAllowed,Is.False);
                Assert.That(_runtime.Session.HasVisited(FullScriptRoomCatalog.Washing),Is.False);
                var retry=GameObject.Find("RetryRoom").GetComponent<Button>();
                using(var hand=new ClinicalHandFixture(retry.gameObject,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(retry);
                Frames(15);Assert.That(_runtime.InputAllowed,Is.True);
            }
            finally{map.modelDigest=digest;}
            MoveToDoor();_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);
            Assert.That(_runtime.RequestRoom(FullScriptRoomCatalog.Washing),Is.True);Frames(35);
            Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo(FullScriptRoomCatalog.Washing));
        }
        [Test] public void NextRoomCannotLoadUntilPreviousAssetsHaveFinishedReleasing()
        {
            var original=_runtime.Visit.Room.Root;
            _release.Complete=false;
            MoveToDoor();_runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);
            Assert.That(_runtime.RequestRoom("R01_OFFICE"),Is.True);
            Frames(40);
            Assert.That(original==null,Is.True);
            Assert.That(_runtime.Visit,Is.Null);
            Assert.That(_release.Begun,Is.EqualTo(1));
            Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo("R00_LOBBY"));
            Assert.That(_runtime.Session.HasVisited("R01_OFFICE"),Is.False);
            _release.Complete=true;Frames(20);
            Assert.That(_runtime.Visit.RoomId,Is.EqualTo("R01_OFFICE"));
            Assert.That(_runtime.InputAllowed,Is.True);
        }
        sealed class ControlledRelease:IClinicalRoomAssetRelease
        { public bool Complete=true; public int Begun; public void Begin(){Begun++;} public bool IsComplete=>Complete; }
        void FinishPrologue()
        {
            var p=_runtime.Prologue;p.ReportHandAvailability(true);
            var epoch=p.CurrentState.Epoch;
            Assert.That(p.RequestInvitation(epoch,VisitorPrologueInputModality.PalmHold).Succeeded,Is.True);
            p.ReportArrivalReady(epoch);p.ReportBookOpened(epoch);p.BeginDialogue(epoch,4);
            AssertOpeningFits();
            p.AdvanceDialogue(epoch,p.CurrentState.Version);
            AssertOpeningFits();
            p.ChooseReply(epoch,p.CurrentState.Version,VisitorEncounterReply.PromiseToExplore);
            AssertOpeningFits();
            p.AdvanceDialogue(epoch,p.CurrentState.Version);AssertOpeningFits();p.AdvanceDialogue(epoch,p.CurrentState.Version);
            Assert.That(p.CurrentState.IsExplorationReady,Is.True);
        }
        void AssertOpeningFits()
        {
            var presenter=UnityEngine.Object.FindObjectsByType<BotanicalGardenQR.VisitorCoach.Frontend.VisitorCoachPresenter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
            Assert.That(presenter.BodyFits,Is.True,presenter.DisplayedBody);
        }
        void MoveToDoor()
        {
            var p=_runtime.Visit.Room.Frame.Transform(_runtime.Visit.Map.start);
            _bindings.Platform.Viewer.position=new Vector3(p.x,1.65f,p.z);
        }
        void Frames(int count){for(var i=0;i<count;i++)_runtime.Tick(.05f);}
        void Samples()
        {
            var field=typeof(OVRCameraRig).GetField("UpdatedAnchors",BindingFlags.Instance|BindingFlags.NonPublic);
            (field.GetValue(_rig) as Action<OVRCameraRig>)?.Invoke(_rig);
        }
        sealed class Timing:ITrackingOriginTiming
        {
            public bool Tracked=true;
            public bool TryGetSampleTime(out double time){time=9;return Tracked;}
            public bool TryGetChangeTime(OVRManager.TrackingOrigin origin,out double time){time=10;return true;}
        }
    }
}
