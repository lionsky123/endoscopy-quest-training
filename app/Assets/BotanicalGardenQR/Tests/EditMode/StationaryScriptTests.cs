using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.VisitorCoach.Frontend;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using BotanicalGardenQR.Video.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Application;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode
{
    // Production covers guided learning only. Removed independent-entry cases are archived in docs/history/tests-before-guided-only-20260923.
    public sealed class StationaryScriptTests
    {
        FullScriptJourneyRuntime _runtime;
        VisitorRuntimeBindings _bindings;
        OVRCameraRig _rig;
        readonly Release _release=new Release();
        Timing _timing;
        bool _keepThemeSelector;
        [SetUp] public void Setup()
        {
            _keepThemeSelector=false;
            var scene=EditorSceneManager.OpenScene("Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity",OpenSceneMode.Single);
            var installer=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<VisitorInstaller>(true)).Single();
            var prefab=PrefabUtility.GetOutermostPrefabInstanceRoot(installer.gameObject);
            if(prefab)PrefabUtility.UnpackPrefabInstance(prefab,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            _bindings=installer.CreateValidatedBindings();
            _rig=_bindings.Platform.XrRigRoot.GetComponentInChildren<OVRCameraRig>(true);_rig.EnsureGameObjectIntegrity();
            _rig.centerEyeAnchor.localPosition=new Vector3(0,1.15f,0);
            _release.Complete=true;
            _timing=new Timing();
            _runtime=new FullScriptJourneyRuntime(_bindings,null,_timing,_release);
            _runtime.StartExperience();
            (typeof(OVRCameraRig).GetField("UpdatedAnchors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_rig) as Action<OVRCameraRig>)?.Invoke(_rig);
            Frames();
        }
        [TearDown] public void Cleanup(){_runtime?.Dispose();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
        [Test] public void SoundSettingsUseNearTouchAndRemainWorldFixed()
        {
            var controls=GameObject.Find("FullScriptSoundControls");
            Assert.That(controls,Is.Not.Null);
            var initialPosition=controls.transform.position;
            var initialRotation=controls.transform.rotation;
            var buttons=controls.GetComponentsInChildren<Button>(true);
            using(var hand=new ClinicalHandFixture(controls,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))
            {
                hand.Touch(buttons.Single(button=>button.name=="OpenSoundSettings"));
                Assert.That(controls.transform.Find("SoundSettingsPanel").gameObject.activeSelf,Is.True);
                var musicBefore=_runtime.Audio.Settings.MusicVolume;
                hand.Touch(buttons.Single(button=>button.name=="MusicUp"));
                Assert.That(_runtime.Audio.Settings.MusicVolume,Is.GreaterThan(musicBefore));
                var effectsBefore=_runtime.Audio.Settings.EffectsVolume;
                hand.Touch(buttons.Single(button=>button.name=="EffectsDown"));
                Assert.That(_runtime.Audio.Settings.EffectsVolume,Is.LessThan(effectsBefore));
                hand.Touch(buttons.Single(button=>button.name=="ToggleSoundMute"));
                Assert.That(_runtime.Audio.Settings.Muted,Is.True);
            }
            _rig.centerEyeAnchor.localPosition+=Vector3.right*.3f;
            _rig.centerEyeAnchor.localRotation=Quaternion.Euler(0,60,0);
            Samples();Frames();
            Assert.That(controls.transform.position,Is.EqualTo(initialPosition));
            Assert.That(controls.transform.rotation,Is.EqualTo(initialRotation));
        }
        [Test] public void GuidedLearningDoesNotTreatReadAcknowledgementAsJudgement()
        {
            FinishOpening();OpenDoor();Touch("Travel_R01_OFFICE");
            Assert.That(_runtime.Session.TryCompleteGuidedTask("OF-01"),Is.False,
                "Reading or acknowledging a method cannot replace criterion-level answers.");
            var session=_runtime.Session;
            var rows=ClinicalTrainingRecords.Query();
            int field=Enumerable.Range(0,ClinicalTrainingRecords.FieldCount).First(i=>rows.Any(row=>string.IsNullOrEmpty(row.Field(i))));
            string criterion=ClinicalTrainingRecords.Criterion(field);
            var missing=rows.First(row=>string.IsNullOrEmpty(row.Field(field)));
            Assert.That(session.TryRecordGuidedFinding("OF-01",criterion,ClinicalJourneyJudgement.NoIssue,new[]{missing.Id}),Is.True);
            var first=session.GetLearningAttempt("OF-01",criterion);
            Assert.That(first.Correct,Is.False);Assert.That(first.IncorrectAttempts,Is.EqualTo(1));Assert.That(first.HintUsed,Is.True);
            Assert.That(session.TryRecordGuidedFinding("OF-01",criterion,ClinicalJourneyJudgement.NoIssue,new[]{missing.Id}),Is.False);
            Assert.That(session.GetLearningAttempt("OF-01",criterion).Attempts,Is.EqualTo(1));
            Assert.That(session.TryRequestLearningHelp("OF-01",criterion,true),Is.True);
            Assert.That(session.TryRecordGuidedFinding("OF-01",criterion,ClinicalJourneyJudgement.IssueFound,new[]{missing.Id}),Is.True);
            var revised=session.GetLearningAttempt("OF-01",criterion);
            Assert.That(revised.Correct,Is.True);Assert.That(revised.Revisions,Is.EqualTo(1));
            Assert.That(revised.IncorrectAttempts,Is.EqualTo(1));Assert.That(revised.ExplanationUsed,Is.True);
            Assert.That(session.TryRecordGuidedFinding("OF-03","missing",ClinicalJourneyJudgement.NoIssue,new[]{missing.Id}),Is.False);
            Assert.That(session.LearningAttempts("OF-03"),Is.Empty);
            Assert.That(session.TryRecordLearningAction("OF-01",ClinicalLearningAction.Observed,"field"),Is.True);
            Assert.That(session.TryRecordLearningAction("OF-01",ClinicalLearningAction.Observed,"field"),Is.False);
            Assert.That(session.TryIntroduceLearningMethod("OF-01","records"),Is.True);
            Assert.That(session.TryIntroduceLearningMethod("OF-01","records"),Is.False);
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(2))
            {OpenDoor();Touch("Travel_"+room);}
            Assert.That(session.TryFinishGuidedAtSummary(),Is.True);
            Assert.That(session.TryRecordGuidedFinding("OF-01",criterion,ClinicalJourneyJudgement.NoIssue,new[]{missing.Id}),Is.False);
            Assert.That(session.TryRequestLearningHelp("OF-01",criterion,false),Is.False);
            Assert.That(session.TryRecordLearningAction("OF-01",ClinicalLearningAction.Observed,"late"),Is.False);
            var fresh=new ClinicalJourneySession(_runtime.Definition,ClinicalJourneyMode.GuidedLearning);
            Assert.That(fresh.LearningAttempts(),Is.Empty);
        }
        [Test] public void RoomThemesAllowDirectChoiceWithoutChangingRoomOrCompletingTasks()
        {
            _keepThemeSelector=true;
            FinishOpening();OpenDoor();Touch("Travel_R01_OFFICE");
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("RoomThemeSelector"));
            var root=_runtime.Visit.Room.Root;
            Assert.That(_runtime.Visit.Panel.transform.Find("ThemeBack"),Is.Null,
                "The initial office menu must not repeat its first choice as a back action.");
            Touch("Theme_1");
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("OfficeRecordsExpanded"),
                "The electronic record category opens the computer content directly.");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("OF-01"));
            Assert.That(_runtime.Visit.Room.Root,Is.SameAs(root));
            Touch("ReturnToTerminal");Touch("Theme_2");
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("OfficeRecordsExpanded"),
                "The paper category opens a readable document rather than another task menu.");
            Assert.That(_runtime.Visit.Panel.transform.Find("DocumentGalleryControls"),Is.Not.Null);
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("OF-05"));
            Touch("BackToRows");
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("RoomThemeSelector"),
                "A paper document returns to its room categories in one near touch.");
            foreach(var room in new[]{"R02_STORAGE","R03_WAITING"}){OpenDoor();Touch("Travel_"+room);}
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("WaitingObservationSelector"));
            Touch("WaitingCorridor");
            Assert.That(_runtime.Visit.Panel.transform.Find("ObservationSide").GetComponent<TMPro.TMP_Text>().text,Does.Contain("诊疗通道侧"));
            OpenDoor();Touch("Travel_R04_GI");Touch("Theme_1");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("CL-02.GI"),"The category opens its first object directly.");
            Touch("NextTask");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("CL-03.GI"));
            OpenDoor();Touch("Travel_R04_RESP");Touch("Theme_1");Touch("NextTask");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("CL-03.RESP"));
            OpenDoor();Touch("Travel_R05_REPROCESSING");Touch("Theme_1");Touch("NextTask");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("RE-04"));
            Assert.That(_runtime.Visit.Panel.transform.Find("InspectObject"),Is.Not.Null);
            foreach(var id in new[]{"OF-02","OF-05","CL-03.GI","CL-03.RESP","RE-04"})
            {_runtime.Session.TryGetTask(id,out var task);Assert.That(task.Status,Is.Not.EqualTo(ClinicalJourneyTaskStatus.Completed));}
        }
        [Test] public void SinkVideoEntryReplacesUnavailableCopyWithoutCompletingWaterChecks()
        {
            FinishOpening();
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(1))
            {
                OpenDoor();Touch("Travel_"+room);
                if(room==FullScriptRoomCatalog.Washing)break;
            }
            Touch("NextTask");Touch("NextTask");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("RE-03"));
            Touch("NextDetail");Touch("NextDetail");
            Assert.That(_runtime.Visit.Panel.transform.Find("WatchSinkVideo"),Is.Not.Null);
            Assert.That(_runtime.Visit.Panel.transform.Find("ScriptBody").GetComponent<TMPro.TMP_Text>().text,Does.Not.Contain("暂不可用"));
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath,"ClinicalCourse/sink-trigger-04m46s-05m06s-with-audio.mp4")),Is.True);
            _runtime.Session.TryGetTask("RE-03",out var task);
            Assert.That(task.Status,Is.Not.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Completed));
            var players=new List<SinkPlayer>();
            _runtime.Visit.SinkVideoFactory=root=>{var player=new SinkPlayer();players.Add(player);return player;};
            Touch("WatchSinkVideo");
            var first=players[0];
            Assert.That(first.Source.StreamingAssetsPath,Is.EqualTo(FullScriptRoomVisit.SinkVideoPath));
            Assert.That(first.Lease.IsDisposed,Is.False);
            first.Publish(VideoPhase.Playing);
            Touch("PauseSinkVideo");
            Assert.That(first.LastIntent,Is.EqualTo(VideoIntentKind.TogglePlayback));
            first.Publish(VideoPhase.Paused);
            Assert.That(_runtime.Visit.Panel.transform.Find("PauseSinkVideo").GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("继续播放"));
            first.Publish(VideoPhase.Failed);
            Touch("ReplaySinkVideo");
            Assert.That(first.Closed,Is.True);Assert.That(first.Lease.IsDisposed,Is.True);
            Assert.That(first.Unsubscribed,Is.True);
            var second=players[1];
            second.Publish(VideoPhase.Completed);
            Assert.That(_runtime.ScriptActions.Contains("RE-03:video-watched"),Is.True);
            _runtime.Session.TryGetTask("RE-03",out task);
            Assert.That(task.Status,Is.Not.EqualTo(ClinicalJourneyTaskStatus.Completed));
            Touch("ReplaySinkVideo");Assert.That(second.LastIntent,Is.EqualTo(VideoIntentKind.Replay));
            var panel=_runtime.Visit.Panel;
            Touch("ReturnFromSinkVideo");
            Assert.That(second.Closed,Is.True);Assert.That(second.Lease.IsDisposed,Is.True);
            Assert.That(panel==null,Is.True);
            Touch("WatchSinkVideo");var third=players[2];
            _runtime.Dispose();
            Assert.That(third.Closed,Is.True);Assert.That(third.Unsubscribed,Is.True);
            Assert.That(third.Lease.IsDisposed,Is.True);
        }
        sealed class SinkPlayer : IVideoController
        {
            IVideoStateSink _sink;SessionToken _session;
            public VideoSource Source;public VideoSurfaceLease Lease;
            public bool Closed,Unsubscribed;public VideoIntentKind LastIntent;
            public VideoResult Open(SessionToken session,VideoDefinition definition,VideoSurfaceLease surface)
            {_session=session;Source=definition.Source;Lease=surface;Publish(VideoPhase.Loading);return VideoResult.Success();}
            public VideoResult Dispatch(SessionToken session,VideoIntent intent)
            {LastIntent=intent.Kind;return VideoResult.Success();}
            public VideoResult Close(SessionToken session)
            {Closed=true;Lease.Dispose();return VideoResult.Success();}
            public IDisposable Observe(IVideoStateSink sink){_sink=sink;return new Subscription(()=>{Unsubscribed=true;_sink=null;});}
            public void Publish(VideoPhase phase)=>_sink?.Publish(new VideoState(_session,1,phase,0,20,false,
                phase==VideoPhase.Playing || phase==VideoPhase.Paused,phase==VideoPhase.Completed || phase==VideoPhase.Playing,true));
            sealed class Subscription : IDisposable
            {readonly Action _dispose;public Subscription(Action dispose){_dispose=dispose;}public void Dispose()=>_dispose();}
        }
        [Test] public void FormalEntryUsesFairyDialogueAndHandPokeToStartTheGuidedMainline()
        {
            var guide=FindGuide();
            Assert.That(guide,Is.Not.Null);
            var state=guide.CurrentState;
            Assert.That(state,Is.Not.Null);
            Assert.That(state.Owner,Is.EqualTo(VisitorDialogueOwner.Guidance));
            Assert.That(state.Speaker,Is.EqualTo("安小卫"));
            Assert.That(state.PrimaryActionLabel,Is.EqualTo("开始学习"));
            Assert.That(state.Body.Length,Is.LessThanOrEqualTo(75));
            Assert.That(guide.GetComponentsInChildren<Button>().Count(button=>button.isActiveAndEnabled&&button.IsInteractable()),Is.EqualTo(1),
                "The welcome dialogue exposes one active action.");
            Assert.That(_runtime.Prologue,Is.Null);
            Assert.That(_runtime.Visit.Panel,Is.Null,"Entry guidance is the only opening surface.");

            TouchGuide(guide);

            Assert.That(guide.CurrentState,Is.Null);
            Assert.That(_runtime.Session.Mode,Is.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyMode.GuidedLearning));
            Assert.That(_runtime.Visit.DoorOpen,Is.True);
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("FullScriptRoomGallery"));
            var buttons=_runtime.Visit.Panel.GetComponentsInChildren<Button>();
            Assert.That(buttons.Any(button=>button.name=="Travel_R01_OFFICE"),Is.True);
            Assert.That(buttons.Any(button=>button.name=="ModeGuided" || button.name=="ModeIndependent"),Is.False);
            Assert.That(_runtime.Visit.Panel.transform.Find("RoomGalleryHeader/RoomGalleryTitle").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("选择房间"));
            Assert.That(_runtime.Visit.Panel.transform.Find("RoomGalleryHeader/RoomGallerySummary").GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("当前房间：医院大厅").And.Contain("推荐下一站：办公室").And.Contain("训练场景示意"));
            Assert.That(_runtime.Visit.Panel.transform.Find("RoomGalleryDragHandle"),Is.Not.Null);
            var officeChoice=buttons.Single(button=>button.name=="Travel_R01_OFFICE");
            Assert.That(((RectTransform)officeChoice.transform).rect.height,Is.GreaterThanOrEqualTo(500));
            Assert.That(officeChoice.GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("办公室"));
        }
        [Test] public void RoomGalleryShowsSevenIllustrativeRoomImagesAndStaysWorldFixed()
        {
            FinishOpening();
            Assert.That(OpenDoor(),Is.True);
            var panel=_runtime.Visit.Panel;
            var pose=new Pose(panel.transform.position,panel.transform.rotation);
            var cards=panel.GetComponentsInChildren<Button>().Where(button=>button.name.StartsWith("Travel_",StringComparison.Ordinal)).ToArray();
            Assert.That(cards,Has.Length.EqualTo(_runtime.Definition.rooms.Length-1));
            Assert.That(panel.GetComponentsInChildren<Button>(),Has.Length.EqualTo(cards.Length+3),
                "The room images, two near-touch browse arrows and one tutorial control are the gallery's buttons.");
            Assert.That(panel.transform.Find("RoomGalleryHandleRail/GalleryPrevious"),Is.Not.Null);
            Assert.That(panel.transform.Find("RoomGalleryHandleRail/GalleryNext"),Is.Not.Null);
            Assert.That(panel.transform.Find("RoomGalleryHandleRail/GalleryPrevious").GetComponentInChildren<TMPro.TMP_Text>().text,
                Is.EqualTo("上一张"));
            Assert.That(panel.transform.Find("RoomGalleryHandleRail/GalleryNext").GetComponentInChildren<TMPro.TMP_Text>().text,
                Is.EqualTo("下一张"));
            var atlas=Resources.Load<Texture2D>("FullScriptRooms/RoomGallery/room-preview-atlas-v1");
            Assert.That(atlas,Is.Not.Null);
            var uvRects=new HashSet<Rect>();
            foreach(var card in cards)
            {
                var towardViewer=_bindings.Platform.Viewer.position-card.transform.position;
                Assert.That(Vector3.Dot(-card.transform.forward,towardViewer.normalized),Is.GreaterThan(.9f),
                    card.name+" canvas content must face the viewer, not its back side.");
                var image=card.transform.Find("RoomPreviewImage").GetComponent<RawImage>();
                Assert.That(image.texture,Is.SameAs(atlas));
                Assert.That(image.uvRect.width,Is.GreaterThan(.29f));
                Assert.That(image.uvRect.height,Is.GreaterThan(.29f));
                uvRects.Add(image.uvRect);
            }
            Assert.That(uvRects,Has.Count.EqualTo(cards.Length),"Each room card must use its own illustration cell.");
            var office=cards.Single(button=>button.name=="Travel_R01_OFFICE");
            Assert.That(office.transform.Find("RoomCardStatus").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("推荐下一站"));
            _rig.centerEyeAnchor.localRotation=Quaternion.Euler(0,35,0);
            Frames();
            Assert.That(panel.transform.position,Is.EqualTo(pose.position));
            Assert.That(panel.transform.rotation,Is.EqualTo(pose.rotation),"Turning the viewer must not rotate the gallery.");
        }
        [Test] public void RoomGalleryMakesTheRecommendedRoomAndPinchHandleEasyToSee()
        {
            FinishOpening();
            Assert.That(OpenDoor(),Is.True);
            var panel=_runtime.Visit.Panel;
            var headerTitle=panel.transform.Find("RoomGalleryHeader/RoomGalleryTitle").GetComponent<TMPro.TMP_Text>();
            Assert.That(headerTitle.fontSize,Is.GreaterThanOrEqualTo(54),"The page heading needs a clear reading hierarchy.");
            var routeSummary=panel.transform.Find("RoomGalleryHeader/RoomGallerySummary").GetComponent<TMPro.TMP_Text>();
            Assert.That(routeSummary.fontSize,Is.GreaterThanOrEqualTo(32));
            Assert.That(routeSummary.text,Does.Contain("\n"),"Current location and recommended destination need separate readable lines.");

            var office=panel.GetComponentsInChildren<Button>().Single(button=>button.name=="Travel_R01_OFFICE");
            var officeTitle=office.transform.Find("Label").GetComponent<TMPro.TMP_Text>();
            Assert.That(officeTitle.color,Is.EqualTo(ClinicalPanelStyle.ShellText),"The recommended room name must contrast with its violet card.");
            var cards=panel.GetComponentsInChildren<Button>().Where(button=>button.name.StartsWith("Travel_",StringComparison.Ordinal)).ToArray();
            Assert.That(cards.Count(card=>card.GetComponent<CanvasGroup>().alpha>.95f),Is.GreaterThanOrEqualTo(3),
                "The selected card and adjacent choices must remain clear rather than fading behind each other.");

            var handle=panel.transform.Find("RoomGalleryDragHandle");
            Assert.That(handle.GetComponent<UnityEngine.UI.Image>().color,Is.EqualTo(ClinicalPanelStyle.SurfaceSubtle),
                "The optional pinch affordance should remain distinct without competing with the selected room.");
            Assert.That(((RectTransform)handle).localPosition.z,
                Is.EqualTo(-.72f/.00072f).Within(.1f),
                "The short handle must sit in front of the room card within natural seated reach.");
            Assert.That(Vector3.Distance(_bindings.Platform.Viewer.position,handle.position),
                Is.LessThan(.7f),"The seated user should not need to stretch toward the room gallery.");
            Assert.That(((RectTransform)handle).rect.width*.00072f,
                Is.LessThanOrEqualTo(FullScriptRoomGalleryDragState.HandleRadius*2f),
                "The whole visible handle must fit inside the gesture's horizontal capture distance.");
            var instruction=handle.Find("RoomGalleryDragInstruction").GetComponent<TMPro.TMP_Text>();
            Assert.That(instruction.color,Is.EqualTo(ClinicalPanelStyle.TextPrimary));
            Assert.That(instruction.text,Does.Contain("滑动"));
        }
        [Test] public void GalleryArrowBrowseMovesTheRoomCardsWithoutLeavingTheGallery()
        {
            FinishOpening();
            Assert.That(OpenDoor(),Is.True);
            var panel=_runtime.Visit.Panel;
            var office=panel.GetComponentsInChildren<Button>().Single(button=>button.name=="Travel_R01_OFFICE");
            var before=office.transform.position;
            Touch("GalleryNext");
            Assert.That(_runtime.Visit.Panel,Is.SameAs(panel));
            Assert.That(office.transform.position,Is.Not.EqualTo(before));
            Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo("R00_LOBBY"));
        }
        [Test] public void RoomGalleryTutorialCanBeSkippedAndReplayedWithoutChangingLearningTasks()
        {
            FinishOpening();
            Assert.That(OpenDoor(),Is.True);
            var taskStates=_runtime.Definition.rooms.SelectMany(room=>room.taskIds).ToDictionary(taskId=>taskId,taskId=>
            {
                Assert.That(_runtime.Session.TryGetTask(taskId,out var snapshot),Is.True);
                return snapshot.Status;
            });
            var roomId=_runtime.Session.CurrentRoomId;
            var mainlineIndex=_runtime.Session.MainlineIndex;
            var action=_runtime.Visit.Panel.GetComponentsInChildren<Button>().Single(button=>button.name=="GalleryTutorialAction");
            var handleInstruction=_runtime.Visit.Panel.transform.Find("WalkingHint").GetComponent<TMPro.TMP_Text>();
            Assert.That(handleInstruction.text,Is.EqualTo("轻触两侧换图，轻触图片进入。"));
            Assert.That(action.GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("拖动演示"));

            using(var hand=new ClinicalHandFixture(_runtime.Visit.Panel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))
                hand.Touch(action);
            _runtime.Tick(.05f);

            Assert.That(_runtime.GalleryTutorial.Step,Is.EqualTo(FullScriptGalleryTutorialStep.MoveToHandle));
            Assert.That(action.GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("退出演示"));
            Assert.That(handleInstruction.text,Is.EqualTo("把手移到下方亮起的短把手处。"));
            Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo(roomId));
            Assert.That(_runtime.Session.MainlineIndex,Is.EqualTo(mainlineIndex));
            foreach(var taskId in taskStates.Keys)
            {
                Assert.That(_runtime.Session.TryGetTask(taskId,out var snapshot),Is.True);
                Assert.That(snapshot.Status,Is.EqualTo(taskStates[taskId]),taskId);
            }

            using(var hand=new ClinicalHandFixture(_runtime.Visit.Panel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))
                hand.Touch(action);
            _runtime.Tick(.05f);

            Assert.That(_runtime.GalleryTutorial.Step,Is.EqualTo(FullScriptGalleryTutorialStep.Skipped));
            Assert.That(action.GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("拖动演示"));
            Assert.That(handleInstruction.text,Is.EqualTo("轻触两侧换图，轻触图片进入。"));
        }
        [Test] public void RoomGalleryTutorialShowsTheCurrentGestureDiagramAndRestoresItOnReplay()
        {
            FinishOpening();
            Assert.That(OpenDoor(),Is.True);
            var panel=_runtime.Visit.Panel;
            var diagram=panel.transform.Find("RoomGalleryDragHandle/TutorialGesture");
            Assert.That(diagram,Is.Not.Null,"The current hand action needs a repeatable visual demonstration.");
            var image=diagram.GetComponent<RawImage>();
            Assert.That(image,Is.Not.Null);
            Assert.That(image.texture,Is.SameAs(Resources.Load<Texture2D>("FullScriptRooms/RoomGallery/gallery-gesture-tutorial-v2")));
            Assert.That(((Texture2D)image.texture).width,Is.EqualTo(1536));
            Assert.That(((Texture2D)image.texture).height,Is.EqualTo(1024));
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(image.texture));
            Assert.That(importer.npotScale.ToString(),Is.EqualTo("None"));
            Assert.That(importer.alphaIsTransparency,Is.True);
            Assert.That(diagram.gameObject.activeSelf,Is.False);

            Touch("GalleryTutorialAction");
            Assert.That(diagram.gameObject.activeSelf,Is.True);
            Assert.That(_runtime.GalleryTutorial.Step,Is.EqualTo(FullScriptGalleryTutorialStep.MoveToHandle));
            Assert.That(image.uvRect,Is.EqualTo(new Rect(0,.5f,1f/3f,.5f)));
            Touch("GalleryTutorialAction");
            Assert.That(diagram.gameObject.activeSelf,Is.False);
        }
        [Test] public void RoomTransitionKeepsItsIllustrationAndNonBlackCurtainThroughRelease()
        {
            FinishOpening();
            Assert.That(OpenDoor(),Is.True);
            _release.Complete=false;
            var gallery=_runtime.Visit.Panel;
            var office=gallery.GetComponentsInChildren<Button>().Single(button=>button.name=="Travel_R01_OFFICE");
            using(var hand=new ClinicalHandFixture(gallery,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(office);
            for(var i=0;i<10;i++)_runtime.Tick(.05f);

            var curtain=(GameObject)typeof(FullScriptJourneyRuntime).GetField("_curtain",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_runtime);
            var shade=(Image)typeof(FullScriptJourneyRuntime).GetField("_shade",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_runtime);
            var preview=curtain.transform.Find("TransitionStatus/TransitionPreviewFrame/TransitionPreview").GetComponent<RawImage>();
            var message=curtain.transform.Find("TransitionStatus/Message").GetComponent<TMPro.TMP_Text>();
            Assert.That(_runtime.LoadingStage,Is.EqualTo("WaitRelease"));
            Assert.That(_runtime.Visit,Is.Null,"The previous room has been released while the light transition remains.");
            Assert.That(curtain.activeSelf,Is.True);
            Assert.That(shade.color,Is.Not.EqualTo(Color.black));
            Assert.That(preview.texture,Is.SameAs(Resources.Load<Texture2D>("FullScriptRooms/RoomGallery/room-preview-atlas-v1")));
            Assert.That(preview.uvRect.x,Is.GreaterThan(.34f),"The destination card must show the office illustration.");
            Assert.That(message.text,Does.Contain("办公室"));

            _release.Complete=true;
            Frames();
            Assert.That(_runtime.Visit.RoomId,Is.EqualTo("R01_OFFICE"));
            Assert.That(curtain.activeSelf,Is.False);
        }
        [Test] public void DefaultLobbyBindsPanoramaWithoutGaussianOrPipelineOverride()
        {
            Assert.That(_runtime.Visit.Map.roomResource,Is.EqualTo(FullScriptRoomCatalog.LobbyPanorama));
            var room=_runtime.Visit.Room.Root;
            Assert.That(room.GetComponentsInChildren<MonoBehaviour>(true).Any(b=>b && b.GetType().Name=="GaussianSplatRenderer"),Is.False);
            var panorama=room.transform.Find("LobbyPanorama(Clone)");Assert.That(panorama,Is.Not.Null);
            var material=panorama.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(material.mainTexture,Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(material.mainTexture),Is.EqualTo("Assets/EndoscopyTheme/Panoramas/Lobby360.png"));
            Assert.That(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name,Is.Not.EqualTo("LobbyGaussianRuntimePipeline"));
            FinishOpening();Assert.That(OpenDoor(),Is.True);
            Touch("Travel_"+_runtime.Definition.mainlineRoomIds[1]);Frames();
            Assert.That(panorama==null,Is.True,"Leaving the lobby must destroy its panorama.");
        }
        [Test] public void RoomEntryGuideUsesLocalCopyAndNearTouchOpensThatRoomsTask()
        {
            FinishOpening();
            Assert.That(OpenDoor(),Is.True);
            var routePanel=_runtime.Visit.Panel;
            var officeChoice=routePanel.GetComponentsInChildren<Button>().Single(button=>button.name=="Travel_R01_OFFICE");
            using(var hand=new ClinicalHandFixture(routePanel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(officeChoice);
            Frames();

            var guide=FindGuide();
            Assert.That(guide.CurrentState.Owner,Is.EqualTo(VisitorDialogueOwner.Guidance));
            Assert.That(guide.CurrentState.Chapter,Is.EqualTo("办公室"));
            Assert.That(guide.CurrentState.PrimaryActionLabel,Is.EqualTo("查看办公室资料"));
            Assert.That(guide.CurrentState.Body,Does.Contain("办公室的模拟资料"));
            Assert.That(_runtime.Visit.Panel,Is.Null,"The room cue appears alone until the learner chooses to continue.");
            TouchGuide(guide);
            Assert.That(guide.CurrentState,Is.Null);
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("RoomThemeSelector"));
            Touch("Theme_0");
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("StationaryScriptPanel"));
            Assert.That(_runtime.Visit.Panel.transform.Find("NextTask"),Is.Not.Null);
            var previousPresenter=guide.gameObject;
            Assert.That(OpenDoor(),Is.True);
            Touch("Travel_R02_STORAGE");
            Assert.That(previousPresenter==null,Is.True,"Leaving the room must release its guide presenter and event binding.");
            Assert.That(_runtime.Visit.RoomId,Is.EqualTo("R02_STORAGE"));
        }
        [Test] public void StorageSampleSelectorOffersThreeTopicsAndKeepsMissingHangingScopeIncomplete()
        {
            FinishOpening();
            foreach(var room in new[]{"R01_OFFICE","R02_STORAGE"})
            {OpenDoor();Touch("Travel_"+room);}
            var root=_runtime.Visit.Room.Root;
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("StorageSampleSelector"));
            Assert.That(_runtime.Visit.Panel.transform.Find("StorageSampleTitle").GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("选择检查内容"));
            Assert.That(_runtime.Visit.Panel.transform.Find("StorageSampleAvailability"),Is.Null,
                "The category screen should not repeat workflow rules beneath the choices.");
            Assert.That(((RectTransform)_runtime.Visit.Panel.transform).rect.height,Is.LessThanOrEqualTo(560),
                "The selector should leave the storage cabinet visible behind it.");
            var choices=_runtime.Visit.Panel.GetComponentsInChildren<Button>().Where(button=>button.name.StartsWith("StorageSample_ST-",StringComparison.Ordinal)).ToArray();
            Assert.That(choices.Select(button=>button.name),Is.EquivalentTo(new[]{"StorageSample_ST-01","StorageSample_ST-02","StorageSample_ST-03"}));
            var cabinetChoice=choices.Single(button=>button.name=="StorageSample_ST-01");
            var registerChoice=choices.Single(button=>button.name=="StorageSample_ST-02");
            var scopeChoice=choices.Single(button=>button.name=="StorageSample_ST-03");
            Assert.That(cabinetChoice.targetGraphic.color,
                Is.EqualTo(ClinicalPanelStyle.Accent),"The cabinet is the recommended starting choice.");
            Assert.That(((RectTransform)cabinetChoice.transform).anchoredPosition.y,
                Is.EqualTo(((RectTransform)registerChoice.transform).anchoredPosition.y),"The direct objects share one row.");
            Assert.That(((RectTransform)cabinetChoice.transform).anchoredPosition.y,
                Is.EqualTo(((RectTransform)scopeChoice.transform).anchoredPosition.y));
            Assert.That(_runtime.Visit.Panel.transform.Find("StorageSampleGastroscopeCopy").GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("悬挂检查暂不可用"));

            Touch("StorageSample_ST-02");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("ST-02"));
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("StorageRegisterObservation"),
                "The register choice goes straight to the paper mounted on the cabinet.");
            Assert.That(_runtime.Visit.Room.Root,Is.SameAs(root));
            Touch("ReturnToInspection");
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("StorageSampleSelector"));
            Touch("StorageSample_ST-03");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("ST-03"));
            Assert.That(_runtime.Visit.Room.Root,Is.SameAs(root));
            _runtime.Session.TryGetTask("ST-03",out var scopeTask);
            Assert.That(scopeTask.Status,Is.Not.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Completed));
            Touch("InspectObject");
            Assert.That(_runtime.Visit.Panel.transform.Find("ObjectObservationStatus").GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("非悬挂状态"));
            Touch("ReturnToInspection");
            Touch("ChooseStorageSample");Touch("StorageSample_ST-01");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("ST-01"));
            Assert.That(_runtime.Visit.Room.Root,Is.SameAs(root));
            foreach(var taskId in new[]{"ST-01","ST-02","ST-03"})
            {
                _runtime.Session.TryGetTask(taskId,out var task);
                Assert.That(task.Status,Is.Not.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Completed),taskId);
            }
        }
        [Test] public void RoomSelectionRejectsRepeatedRequestsUntilTheRoomBarrierCommits()
        {
            FinishOpening();
            Assert.That(OpenDoor(),Is.True);
            var panel=_runtime.Visit.Panel;
            var office=panel.GetComponentsInChildren<Button>().Single(button=>button.name=="Travel_R01_OFFICE");
            using(var hand=new ClinicalHandFixture(panel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(office);
            _runtime.Tick(.05f); // Commit the near-touch request into the loading state machine.

            Assert.That(_runtime.InputAllowed,Is.False);
            Assert.That(_runtime.RequestRoom("R01_OFFICE"),Is.False,"The same room request cannot be submitted a second time.");
            Assert.That(_runtime.RequestInspectionPoint("N00",()=>{}),Is.False,"A checkpoint request cannot overlap room loading.");
            Assert.That(_runtime.Session.RoomVisitCount("R01_OFFICE"),Is.Zero);
            Assert.That(_runtime.Session.MainlineIndex,Is.Zero);

            Frames();

            Assert.That(_runtime.Visit.RoomId,Is.EqualTo("R01_OFFICE"));
            Assert.That(_runtime.Session.RoomVisitCount("R01_OFFICE"),Is.EqualTo(1));
            Assert.That(_runtime.Session.MainlineIndex,Is.EqualTo(1));
        }
        [Test] public void ReturningToLobbyShowsChapterChoicesWithoutRepeatingTheWelcomeAction()
        {
            FinishOpening();
            Touch("Travel_R01_OFFICE");
            Assert.That(_runtime.Visit.RoomId,Is.EqualTo("R01_OFFICE"));
            Assert.That(OpenDoor(),Is.True);
            Touch("Travel_R00_LOBBY");

            Assert.That(_runtime.Visit.RoomId,Is.EqualTo(_runtime.Definition.startRoomId));
            Assert.That(_runtime.Session.MainlineIndex,Is.EqualTo(1));
            Assert.That(_runtime.Visit.DoorOpen,Is.True);
            var buttons=_runtime.Visit.Panel.GetComponentsInChildren<Button>();
            Assert.That(buttons.Any(button=>button.name=="Travel_R01_OFFICE"),Is.True);
            Assert.That(buttons.Any(button=>button.name=="ModeGuided"),Is.False);
            Assert.That(buttons.Any(button=>button.name=="Travel_R00_LOBBY"),Is.False);
            var officeChoice=buttons.Single(button=>button.name=="Travel_R01_OFFICE");
            Assert.That(officeChoice.GetComponentInChildren<TMPro.TMP_Text>().text,
                Is.EqualTo(_runtime.Definition.FindRoom("R01_OFFICE").displayName));
            Assert.That(officeChoice.transform.Find("RoomCardStatus").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo("已到访 · 可回看"));
        }
        [Test] public void ProductionPrefabDoesNotReferenceArchivedCourseAssets()
        {
            Assert.That(_bindings.Configuration.Library,Is.Null);
            Assert.That(_bindings.Configuration.Routes,Is.Null);
            Assert.That(_bindings.Configuration.CollectionCatalog,Is.Null);
            Assert.That(_bindings.Configuration.PhysicalAugmentationDefinitions,Is.Null);
            var paths=AssetDatabase.GetDependencies("Assets/BotanicalGardenQR/Experience/Bootstrap/Prefabs/VisitorRuntime.prefab",true);
            Assert.That(paths.Where(p=>p.Contains("/Content/Scenes/") || p.EndsWith("/ContentSceneLibrary.asset")),Is.Empty);
            Assert.That(()=>VisitorRuntimeComposition.Create(_bindings,null),Throws.InvalidOperationException.With.Message.Contains("Archived composition"));
        }
        [Test] public void OriginalScriptReferencesImportAsRuntimeTextures()
        {
            foreach(var name in new[]{"script-equipment","script-ppe"})
            {
                var image=Resources.Load<Texture2D>("ClinicalCourse/FullScriptVisuals/"+name);
                Assert.That(image,Is.Not.Null,name);
                Assert.That(image.width,Is.GreaterThan(100));
            }
        }
        [Test] public void ProductionStationaryEntryDoesNotCreateArchivedCourseModules()
        {
            AssertNoArchivedModules();
            FinishOpening();
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(1))
            {
                OpenDoor();Touch("Travel_"+room);
                AssertNoArchivedModules();
            }
            for(int page=0;page<_runtime.Definition.rooms.Sum(r=>r.taskIds.Length)+7;page++)
            {
                Touch("ResultDetails");
                var copy=_runtime.Visit.Panel.transform.Find("RoomBrief").GetComponent<TMPro.TMP_Text>().text;
                Assert.That(copy,Does.Not.Contain("旧洗消"));
                Assert.That(copy,Does.Not.Contain("到门口"));
            }
        }
        [Test] public void ProductionEntryDoesNotInstantiateOrCompleteTheArchivedPrologue()
        {
            Assert.That(_runtime.Prologue, Is.Null, "The current script must not create a legacy state machine.");
            var names = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(b => b).Select(b => b.GetType().Name).ToArray();
            Assert.That(names, Does.Not.Contain("VisitorProloguePresenter"));
            Assert.That(names, Does.Not.Contain("VisitorHandReadinessAdapter"));
            var guide=FindGuide();
            Assert.That(guide.CurrentState,Is.Not.Null,"The current guide presents the formal entry dialogue.");
            Assert.That(guide.CurrentState.Owner,Is.EqualTo(VisitorDialogueOwner.Guidance));
            Assert.That(_runtime.Visit.Panel,Is.Null,"A second opening panel must not compete with the guide.");
        }
        [Test] public void ProductionOffersOnlyGuidedLearningWithoutWalking()
        {
            _runtime.SelectMode(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyMode.IndependentCheck);
            Assert.That(_runtime.Session.Mode,Is.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyMode.GuidedLearning));
            FinishOpening();
            Assert.That(_runtime.Visit.DoorOpen,Is.True);
            var buttons=_runtime.Visit.Panel.GetComponentsInChildren<Button>();
            Assert.That(buttons.Any(button=>button.name=="ModeGuided" || button.name=="ModeIndependent"),Is.False);
            Assert.That(_runtime.Visit.Panel.transform.Find("WalkingHint").GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("轻触两侧换图，轻触图片进入"),
                "The first gallery visit should explain the direct arrow and image actions.");
        }
        [TestCase(0)]
        [TestCase(3)]
        [TestCase(4)]
        public void InspectionPointWaitsForTrackingWithoutFalseCompletion(int ticksBeforeLoss)
        {
            int completions=0;
            var head=_rig.centerEyeAnchor.position;
            var left=_rig.leftHandAnchor.position;
            Assert.That(_runtime.RequestInspectionPoint("N00",()=>completions++),Is.True);
            Assert.That(_runtime.RequestInspectionPoint("N00",()=>completions++),Is.False,"A checkpoint cannot start twice while its curtain transition is active.");
            Assert.That(_runtime.RequestRoom("R01_OFFICE"),Is.False,"A room switch cannot overlap a checkpoint transition.");
            for(int i=0;i<ticksBeforeLoss;i++)_runtime.Tick(.1f);
            _timing.Tracked=false;Samples();
            var roomPose=new Pose(_runtime.Visit.Room.Root.transform.position,_runtime.Visit.Room.Root.transform.rotation);
            for(int i=0;i<300;i++)_runtime.Tick(.1f);
            Assert.That(completions,Is.Zero,"Lost tracking must not complete an unaligned inspection point after a timeout.");
            Assert.That(GameObject.Find("FullScriptTransitionCurtain"),Is.Not.Null);
            Assert.That(_runtime.InputAllowed,Is.False);
            Assert.That(_runtime.Visit.Room.Root.transform.position,Is.EqualTo(roomPose.position));
            Assert.That(_runtime.Visit.Room.Root.transform.rotation,Is.EqualTo(roomPose.rotation));
            _timing.Tracked=true;Samples();Frames();
            Assert.That(completions,Is.EqualTo(1));
            Assert.That(_runtime.InputAllowed,Is.True);
            Assert.That(GameObject.Find("FullScriptTransitionCurtain"),Is.Null);
            Assert.That(_rig.centerEyeAnchor.position,Is.EqualTo(head));
            Assert.That(_rig.leftHandAnchor.position,Is.EqualTo(left));
            Frames();Assert.That(completions,Is.EqualTo(1));
        }
        [Test] public void InitialLobbyWaitsForReliableTrackingWithoutTimeoutReveal()
        {
            _runtime.Dispose();_timing.Tracked=false;
            _runtime=new FullScriptJourneyRuntime(_bindings,null,_timing,_release);
            _runtime.StartExperience();Samples();
            for(int i=0;i<300;i++)_runtime.Tick(.1f);
            Assert.That(_runtime.InputAllowed,Is.False);
            Assert.That(GameObject.Find("FullScriptTransitionCurtain"),Is.Not.Null);
            Assert.That(_runtime.Visit.Panel,Is.Null);
            _timing.Tracked=true;Samples();Frames();
            Assert.That(_runtime.InputAllowed,Is.True);
            Assert.That(_runtime.Visit.Panel,Is.Null);
            Assert.That(FindGuide().CurrentState.Owner,Is.EqualTo(VisitorDialogueOwner.Guidance));
        }
        void Samples() => (typeof(OVRCameraRig).GetField("UpdatedAnchors",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_rig) as Action<OVRCameraRig>)?.Invoke(_rig);
        [Test] public void MissingInitialLobbyCanRetryAfterAssetsAreRestored()
        {
            const string original="Assets/EndoscopyTheme/Resources/FullScriptRooms/Stationary/LobbyPanorama.prefab";
            const string unavailable="Assets/EndoscopyTheme/Resources/FullScriptRooms/Stationary/LobbyPanorama.UnavailableForTest.prefab";
            _runtime.Dispose();
            Assert.That(AssetDatabase.MoveAsset(original,unavailable),Is.Empty);
            try
            {
                Assert.DoesNotThrow(()=>_runtime=new FullScriptJourneyRuntime(_bindings,null,_timing,_release),
                    "A missing first room must retain the loading/retry surface instead of aborting startup.");
                _runtime.StartExperience();Samples();Frames();
                Assert.That(_runtime.Visit,Is.Null);
                Assert.That(_runtime.InputAllowed,Is.False);
                Assert.That(GameObject.Find("RetryRoom"),Is.Not.Null);
                Assert.That(GameObject.Find("FullScriptTransitionCurtain"),Is.Not.Null);
                var curtain=GameObject.Find("FullScriptTransitionCurtain");
                var message=curtain.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(text=>text.name=="Message");
                var retryButton=GameObject.Find("RetryRoom").GetComponent<Button>();
                Assert.That(message.color,Is.EqualTo(ClinicalPanelStyle.TextPrimary),"Failure instructions must be readable on the light transition panel.");
                Assert.That(retryButton.targetGraphic.color,Is.EqualTo(ClinicalPanelStyle.Accent),"The retry action must retain a full-contrast primary surface.");
                Assert.That(retryButton.GetComponentInChildren<TMPro.TMP_Text>().color,Is.EqualTo(ClinicalPanelStyle.ShellText),"The primary retry label must contrast with its dark surface.");
                Assert.That(AssetDatabase.MoveAsset(unavailable,original),Is.Empty);
                _release.Complete=false;
                using(var hand=new ClinicalHandFixture(retryButton.gameObject,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(retryButton);
                Frames();Assert.That(_runtime.Visit,Is.Null,"Retry must honor the asset release barrier.");
                _release.Complete=true;Frames();
                Assert.That(_runtime.InputAllowed,Is.True);
                Assert.That(_runtime.Visit.RoomId,Is.EqualTo("R00_LOBBY"));
                Assert.That(_runtime.Visit.Panel,Is.Null);
                Assert.That(FindGuide().CurrentState.Owner,Is.EqualTo(VisitorDialogueOwner.Guidance));
                Assert.That(_runtime.Session.VisitedRooms.Count,Is.EqualTo(1));
            }
            finally
            {
                if(AssetDatabase.LoadAssetAtPath<GameObject>(unavailable))
                    Assert.That(AssetDatabase.MoveAsset(unavailable,original),Is.Empty);
            }
        }
        [Test] public void WorkspacePreviewIsIsolatedAndRefreshReleasesPreviousRoom()
        {
            _runtime.Dispose();
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var originalRoots=scene.GetRootGameObjects();
            var type=Type.GetType("BotanicalGardenQR.Bootstrap.Editor.InspectionWorkspace, BotanicalGardenQR.Bootstrap.Editor",true);
            var window=(EditorWindow)ScriptableObject.CreateInstance(type);
            var refresh=type.GetMethod("RefreshRoom",BindingFlags.Instance|BindingFlags.NonPublic);
            var roomField=type.GetField("_room",BindingFlags.Instance|BindingFlags.NonPublic);
            var sceneCount=EditorSceneManager.previewSceneCount;
            try
            {
                refresh.Invoke(window,null);
                var first=((VirtualRoomEnvironment)roomField.GetValue(window)).Root;
                Assert.That(EditorSceneManager.IsPreviewScene(first.scene),Is.True,
                    "Opening the current workspace must not mix preview geometry with an old open scene.");
                CollectionAssert.AreEquivalent(originalRoots,scene.GetRootGameObjects());
                refresh.Invoke(window,null);
                Assert.That(first==null,Is.True,"Refresh must dispose the previous room.");
                var next=((VirtualRoomEnvironment)roomField.GetValue(window)).Root;
                Assert.That(next,Is.Not.Null);
                CollectionAssert.AreEquivalent(originalRoots,scene.GetRootGameObjects());
            }
            finally{UnityEngine.Object.DestroyImmediate(window);}
            Assert.That(EditorSceneManager.previewSceneCount,Is.EqualTo(sceneCount));
        }
        [UnityTest] public IEnumerator WorkspaceAutomaticallyRefreshesAfterAnAssetIsImported()
        {
            var probePath="Assets/EndoscopyTheme/Resources/ClinicalCourse/InspectionWorkspaceRefreshProbe_"+Guid.NewGuid().ToString("N")+".txt";
            var probeFile=System.IO.Path.GetFullPath(probePath);
            Assert.That(System.IO.File.Exists(probeFile),Is.False,"The refresh probe must not overwrite a workspace asset.");
            _runtime.Dispose();
            var type=Type.GetType("BotanicalGardenQR.Bootstrap.Editor.InspectionWorkspace, BotanicalGardenQR.Bootstrap.Editor",true);
            var window=(EditorWindow)ScriptableObject.CreateInstance(type);
            var refresh=type.GetMethod("RefreshRoom",BindingFlags.Instance|BindingFlags.NonPublic);
            var update=type.GetMethod("OnInspectorUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
            var roomField=type.GetField("_room",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                refresh.Invoke(window,null);
                update.Invoke(window,null); // Drain the initial queued refresh before importing anything.
                var first=((VirtualRoomEnvironment)roomField.GetValue(window)).Root;
                Assert.That(first,Is.Not.Null);
                System.IO.File.WriteAllText(probeFile,Guid.NewGuid().ToString("N"));
                AssetDatabase.ImportAsset(probePath,ImportAssetOptions.ForceSynchronousImport);
                for(int i=0;i<10 && first;i++)
                {
                    yield return null;
                    update.Invoke(window,null);
                }
                Assert.That(first==null,Is.True,"An imported asset must trigger disposal and automatic workspace rebuild.");
                var refreshed=((VirtualRoomEnvironment)roomField.GetValue(window)).Root;
                Assert.That(refreshed,Is.Not.Null);
                Assert.That(EditorSceneManager.IsPreviewScene(refreshed.scene),Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(probePath);
                if(window)UnityEngine.Object.DestroyImmediate(window);
            }
        }
        [Test] public void SharedEditedViewpointsDriveWorkspaceAndRuntimeWithoutMovingHead()
        {
            var config=InspectionViewConfiguration.Load();var data=config.Find("R00_LOBBY");
            bool wasDirty=EditorUtility.IsDirty(config);
            var initial=data.initial;var point=data.inspections[0];
            var initialPosition=initial.position;float initialYaw=initial.yaw;
            var pointPosition=point.position;float pointYaw=point.yaw;
            var type=Type.GetType("BotanicalGardenQR.Bootstrap.Editor.InspectionWorkspace, BotanicalGardenQR.Bootstrap.Editor",true);
            EditorWindow window=null;
            try
            {
                initial.position+=new Vector3(.3f,0,-.2f);initial.yaw+=15;
                point.position+=new Vector3(-.4f,0,.3f);point.yaw-=20;
                EditorUtility.SetDirty(config); // Match authoring edits; retain unsaved changes during asset release.
                _runtime.Dispose();
                window=(EditorWindow)ScriptableObject.CreateInstance(type);
                type.GetMethod("RefreshRoom",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
                var preview=(VirtualRoomEnvironment)type.GetField("_room",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
                var camera=(Camera)type.GetField("_camera",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
                AssertView(initial,preview.Root.transform,camera.transform);
                type.GetField("_viewIndex",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,1);
                type.GetMethod("FocusView",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
                AssertView(point,preview.Root.transform,camera.transform);
                UnityEngine.Object.DestroyImmediate(window);window=null;
                _runtime=new FullScriptJourneyRuntime(_bindings,null,_timing,_release);
                _runtime.StartExperience();Samples();Frames();
                AssertView(initial,_runtime.Visit.Room.Root.transform,_rig.centerEyeAnchor);
                var head=_rig.centerEyeAnchor.position;
                Assert.That(_runtime.RequestInspectionPoint(point.id,()=>{}),Is.True);Frames();
                AssertView(point,_runtime.Visit.Room.Root.transform,_rig.centerEyeAnchor);
                Assert.That(_rig.centerEyeAnchor.position,Is.EqualTo(head));
            }
            finally
            {
                if(window)UnityEngine.Object.DestroyImmediate(window);
                initial.position=initialPosition;initial.yaw=initialYaw;point.position=pointPosition;point.yaw=pointYaw;
                if(!wasDirty)EditorUtility.ClearDirty(config);
            }
        }
        static void AssertView(InspectionViewConfiguration.View view,Transform room,Transform eye)
        {
            var local=room.InverseTransformPoint(eye.position);
            Assert.That(local.x,Is.EqualTo(view.position.x).Within(.001f));
            Assert.That(local.z,Is.EqualTo(view.position.z).Within(.001f));
            Assert.That(Vector3.Angle(room.InverseTransformDirection(eye.forward),view.Forward),Is.LessThan(.01f));
        }
        [Test] public void VrBuildDoesNotRegisterMrukNativeOrGlobalUpdateStartup()
        {
            foreach(var pair in new[]{new[]{"Meta.XR.MRUtilityKit.MRUK","InitializeSharedLibrary"},new[]{"Meta.XR.MRUtilityKit.MRUKGlobalContext","CreateInstance"}})
            {
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(pair[0])).First(t=>t!=null);
                var method=type.GetMethod(pair[1],BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
                Assert.That(method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute),false),Is.Empty,
                    pair[0]+" must not register an unused native callback in the VR player.");
            }
        }
        void AssertNoArchivedModules()
        {
            var forbidden=new[]{"VideoController","PanoramaController","ModelController","NarrationController","ImageRingFrontend"};
            var found=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(b=>b && forbidden.Contains(b.GetType().Name)).Select(b=>b.GetType().Name).ToArray();
            Assert.That(found,Is.Empty,"Archived course modules must not be initialized by the production entry.");
            Assert.That(GameObject.Find("CollectionWorldPresentation"),Is.Null);
        }
        [Test] public void EntireNewRouteWorksWithoutMovingHeadOrHandsAndUsesLatestWashingOrder()
        {
            FinishOpening();
            var head=_rig.centerEyeAnchor.position;var left=_rig.leftHandAnchor.position;var right=_rig.rightHandAnchor.position;
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(1))
            {
                Assert.That(OpenDoor(),Is.True);
                Touch("Travel_"+room);Frames();
                Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo(room));
                Assert.That(_rig.centerEyeAnchor.position,Is.EqualTo(head));
                Assert.That(_rig.leftHandAnchor.position,Is.EqualTo(left));
                Assert.That(_rig.rightHandAnchor.position,Is.EqualTo(right));
                Assert.That(FindGuide().CurrentState?.Owner==VisitorDialogueOwner.Guidance || _runtime.Visit.Panel!=null,Is.True,
                    "A room must present its entry guide or its active task surface.");
                if(room==FullScriptRoomCatalog.Washing)
                {
                    Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("RE-01"));
                    foreach(var id in new[]{"RE-02","RE-03","RE-06","RE-04","RE-05"})
                    {Touch("NextTask");Frames();Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo(id));}
                }
            }
            Assert.That(_runtime.Session.HasVisitedAllMainlineRooms,Is.True);
        }
        [TestCase(2,"逐次测漏","本页列有3次模拟登记")]
        [TestCase(3,"生物学监测","资料待补")]
        [TestCase(4,"消毒剂监测","资料待核验")]
        [TestCase(5,"人员培训","模拟培训记录")]
        public void OfficeTaskOpensMatchingDocumentWithoutStartingRecordTask(int index,string title,string body)
        {
            FinishOpening();OpenDoor();Touch("Travel_R01_OFFICE");Frames();
            Touch("NextTask");
            _runtime.Session.TryGetTask("OF-01",out var before);
            var previousStatus=before.Status;
            Touch("OpenDocuments");
            Touch("Doc"+ClinicalTrainingRecords.DocumentIndexForTask("OF-"+index.ToString("D2")));
            Assert.That(_runtime.Visit.Panel.transform.Find("Title").GetComponent<TMPro.TMP_Text>().text,Does.EndWith(title));
            if(index==5)
            {
                var image=_runtime.Visit.Panel.transform.Find("DocumentImage").GetComponent<RawImage>();
                Assert.That(AssetDatabase.GetAssetPath(image.texture),Does.EndWith("/office-staff-training-record-v1.png"));
            }
            else Assert.That(_runtime.Visit.Panel.transform.Find("DocumentBody").GetComponent<TMPro.TMP_Text>().text,Does.Contain(body));
            Assert.That(_runtime.Visit.Panel.transform.Find("Footer"),Is.Null,"Office records must not repeat a fixed page footer.");
            if(index==2)
            {
                AssertLeakPaper(_runtime.Visit.Panel);
                Touch("Doc0");
                var disinfectionImage=_runtime.Visit.Panel.transform.Find("DocumentImage").GetComponent<RawImage>();
                Assert.That(AssetDatabase.GetAssetPath(disinfectionImage.texture),Does.EndWith("/office-disinfection-training-record-v1.png"));
                Assert.That(_runtime.Visit.Panel.transform.Find("DocumentBody"),Is.Null,
                    "The reviewed simulation record is read from its image, not duplicated as a large text page.");
            }
            if(index==3)
            {
                var biologicalCopy=_runtime.Visit.Panel.transform.Find("DocumentBody").GetComponent<TMPro.TMP_Text>().text;
                Assert.That(biologicalCopy,Does.Contain("本项暂不可用"));
                Assert.That(biologicalCopy,Does.Not.Contain("OF-03"));
            }
            if(index==4)
            {
                var disinfectantCopy=_runtime.Visit.Panel.transform.Find("DocumentBody").GetComponent<TMPro.TMP_Text>().text;
                Assert.That(disinfectantCopy,Does.Not.Contain("P05"));
            }
            if(index==5)
            {
                Assert.That(_runtime.Visit.Panel.transform.Find("DocumentBody"),Is.Null);
            }
            _runtime.Session.TryGetTask("OF-01",out var after);
            Assert.That(after.Status,Is.EqualTo(previousStatus),"Reading a different document must not begin OF-01");
            Assert.That(_runtime.OfficeFieldsViewed,Is.Empty);
            Assert.That(_runtime.OfficeRowsViewed,Is.Empty);
        }
        [TestCase(false)]
        public void WashingLeakRecordsReuseOfficeSourceWithoutAssessmentOrTerminal(bool independent)
        {
            FinishOpening();if(independent)Touch("ModeIndependent");
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(1).Take(6))
            {OpenDoor();Touch("Travel_"+room);}
            for(int i=0;i<5;i++)Touch("NextTask");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("RE-05"));
            Assert.That(_runtime.Visit.Panel.transform.Find("OpenLinkedLeakRecords"),Is.Null,"Packaging is a separate subitem");
            Touch("NextDetail");
            _runtime.Session.TryGetTask("OF-01",out var office);var officeStatus=office.Status;
            _runtime.Session.TryGetTask("RE-05",out var washing);var washingStatus=washing.Status;
            var fields=_runtime.OfficeFieldsViewed.Count;var rows=_runtime.OfficeRowsViewed.Count;
            Touch("OpenLinkedLeakRecords");
            var panel=_runtime.Visit.Panel;var position=panel.transform.position;
            Assert.That(GameObject.Find("OfficeRecordTerminal"),Is.Null);
            int document=BotanicalGardenQR.Experience.Application.ClinicalTrainingRecords.DocumentIndexForTask("OF-02");
            Assert.That(panel.transform.Find("DocumentBody").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo(BotanicalGardenQR.Experience.Application.ClinicalTrainingRecords.DocumentBody(document)));
            AssertLeakPaper(panel);
            Touch("BackToRows");
            Assert.That(panel.transform.Find("FindingNoIssue"),Is.Null);
            Assert.That(panel.transform.Find("CompleteOfficeLearning"),Is.Null);
            Assert.That(panel.transform.Find("RowInfo").GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("SIM-R001").And.Contain("请从原表选择对应登记").And.Not.Contain("SIM-L001"));
            Assert.That(panel.transform.Find("ReadStatus").GetComponent<TMPro.TMP_Text>().text,Does.Not.Contain("版本"));
            Assert.That(panel.transform.Find("ReadStatus").GetComponent<TMPro.TMP_Text>().text,
                Does.Not.Contain(BotanicalGardenQR.Experience.Application.ClinicalTrainingRecords.Version));
            Assert.That(panel.transform.Find("LinkedRecordScope").GetComponent<TMPro.TMP_Text>().text,Does.Contain("同一份模拟资料"));
            Touch("Row1Field0");
            Assert.That(panel.transform.Find("RowInfo").GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("SIM-R002").And.Contain("请从原表选择对应登记").And.Not.Contain("SIM-L002"));
            Assert.That(panel.transform.Find("PracticeLinkedLeak").GetComponent<Button>().interactable,Is.False,
                "选择另一条记录不等于从测漏原表定位本次使用。");
            Touch("RecordField0");Touch("FilterDate");Touch("OpenDocuments");
            Assert.That(panel.transform.position,Is.EqualTo(position));
            Assert.That(panel.transform.Find("DocumentBody").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo(BotanicalGardenQR.Experience.Application.ClinicalTrainingRecords.DocumentBody(document)));
            Assert.That(_runtime.OfficeFieldsViewed.Count,Is.EqualTo(fields));Assert.That(_runtime.OfficeRowsViewed.Count,Is.EqualTo(rows));
            _runtime.Session.TryGetTask("OF-01",out office);Assert.That(office.Status,Is.EqualTo(officeStatus));
            _runtime.Session.TryGetTask("RE-05",out washing);Assert.That(washing.Status,Is.EqualTo(washingStatus));
            Touch("ReturnToTerminal");Assert.That(panel==null,Is.True);Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("RE-05"));
            Touch("OpenLinkedLeakRecords");panel=_runtime.Visit.Panel;
            OpenDoor();Assert.That(panel==null,Is.True,"External navigation closes related media");
            Touch("Travel_R01_OFFICE");Assert.That(GameObject.Find("OfficeRecordsExpanded"),Is.Null);
        }
        static void AssertLeakPaper(GameObject panel)
        {
            var paper=panel.transform.Find("LeakRegisterPaper").GetComponent<RawImage>();
            Assert.That(paper.texture,Is.SameAs(Resources.Load<Texture2D>("ClinicalCourse/FullScriptVisuals/storage-cleaning-register-v1")));
            Assert.That(panel.GetComponentsInChildren<TMPro.TMP_Text>().Count(t=>t.name.StartsWith("LeakCell")),Is.EqualTo(15));
            Assert.That(panel.transform.Find("LeakCell1_1").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("SIM-R002\nDEMO-RESP-001"));
            Assert.That(panel.transform.Find("LeakCell2_2").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("09:55"));
            Assert.That(panel.transform.Find("LeakCell3_0"),Is.Null,"Unused fourth row is not a missing record");
            Assert.That(panel.transform.Find("DocumentBody").GetComponent<TMPro.TMP_Text>().text,Does.Contain("第4行未使用"));
            Assert.That(panel.transform.Find("DocumentBody").GetComponent<TMPro.TMP_Text>().text,Does.Contain("不是实际设备检测凭证"));
            Assert.That(panel.transform.Find("DocumentBody").GetComponent<TMPro.TMP_Text>().text,Does.Not.Contain("SIM-LEAK"));
            Assert.That(panel.transform.Find("DocumentBody").GetComponent<TMPro.TMP_Text>().text,Does.Not.Contain("无隐藏分页"));
            var provenance=panel.transform.Find("Provenance").GetComponent<TMPro.TMP_Text>().text;
            Assert.That(provenance,Is.EqualTo("模拟训练记录 · 非医院原表 / 非实拍"));
            Assert.That(provenance,Does.Not.Contain("SIM-LEAK"));
            Assert.That(provenance,Does.Not.Contain(BotanicalGardenQR.Experience.Application.ClinicalTrainingRecords.Version));
            var origin=panel.transform.Find("LeakPaperOrigin").GetComponent<TMPro.TMP_Text>().text;
            Assert.That(origin,Is.EqualTo("教学示例；登记结果不证明实际检测效果。"));
            Assert.That(origin,Does.Not.Contain("程序数据"));
            Assert.That(origin,Does.Not.Contain("生成空白纸面"));
            var visibleCopy=string.Join(" ",panel.GetComponentsInChildren<TMPro.TMP_Text>().Select(text=>text.text));
            Assert.That(visibleCopy,Does.Not.Contain(BotanicalGardenQR.Experience.Application.ClinicalTrainingRecords.Version));
            Assert.That(visibleCopy,Does.Not.Contain("SIM-LEAK"));
            Assert.That(visibleCopy,Does.Not.Contain("同源程序数据"));
            Assert.That(visibleCopy,Does.Not.Contain("复用生成空白纸面"));
            Assert.That(panel.transform.Find("Footer"),Is.Null,"Office records must not repeat a fixed page footer.");
        }
        [TestCase(false,false)]
        [TestCase(false,true)]
        public void LeakRowNearTouchLocatesExactUseAndReturnsWithoutAutoAssessment(bool independent,bool washing)
        {
            FinishOpening();if(independent)Touch("ModeIndependent");
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(1).Take(washing?6:1))
            {OpenDoor();Touch("Travel_"+room);}
            for(int i=0;i<(washing?5:1);i++)Touch("NextTask");
            if(washing)Touch("NextDetail");
            if(washing)Touch("OpenLinkedLeakRecords");
            else {Touch("OpenDocuments");Touch("Doc1");}
            var panel=_runtime.Visit.Panel;var position=panel.transform.position;var rotation=panel.transform.rotation;
            _runtime.Session.TryGetTask("OF-01",out var task);var initial=task.Status;
            int fields=_runtime.OfficeFieldsViewed.Count,rows=_runtime.OfficeRowsViewed.Count;
            for(int i=0;i<3;i++)
            {
                Touch("LeakUseLink"+i);
                string id="SIM-R00"+(i+1),leak="SIM-L00"+(i+1);
                Assert.That(panel.transform.Find("RowInfo").GetComponent<TMPro.TMP_Text>().text,Does.Contain(id).And.Contain(leak));
                Assert.That(panel.transform.Find("FilterDate").GetComponentInChildren<TMPro.TMP_Text>().text,Does.Contain("全部"));
                Assert.That(panel.transform.Find("FilterScope").GetComponentInChildren<TMPro.TMP_Text>().text,Does.Contain("全部"));
                Touch("FilterDate");Touch("FilterScope"); // Hide some uses before next explicit evidence jump.
                Touch("OpenDocuments");
                AssertLeakPaper(panel);
                Assert.That(panel.transform.Find("LeakLinkHint").GetComponent<TMPro.TMP_Text>().text,Does.Contain(id));
                Assert.That(panel.transform.Find("LeakUseLink"+i).GetComponent<Image>().color.a,Is.GreaterThan(0));
                Assert.That(panel.transform.position,Is.EqualTo(position));Assert.That(panel.transform.rotation,Is.EqualTo(rotation));
            }
            Assert.That(panel.transform.Find("LeakUseLink3"),Is.Null);
            Assert.That(_runtime.OfficeFieldsViewed.Count,Is.EqualTo(fields));Assert.That(_runtime.OfficeRowsViewed.Count,Is.EqualTo(rows));
            Assert.That(_runtime.Session.GetFindings("OF-01"),Is.Empty);
            _runtime.Session.TryGetTask("OF-01",out task);Assert.That(task.Status,Is.EqualTo(initial));
        }
        [TestCase(false)]
        public void OfficeLeakJudgementsAreExplicitPerUseAndDoNotModifySixFields(bool independent)
        {
            FinishOpening();if(independent)Touch("ModeIndependent");
            OpenDoor();Touch("Travel_R01_OFFICE");
            Touch("NextTask");Touch("OpenDocuments");Touch("Doc1");
            Touch("BackToRows");
            var panel=_runtime.Visit.Panel;
            Assert.That(panel.transform.Find("FindingNoIssue"),Is.Null);
            var initial=panel.transform.Find(independent?"LeakMatch":"CompleteLeakLearning").GetComponent<Button>();
            Assert.That(initial.interactable,Is.False);
            Touch("OpenDocuments");
            for(int i=0;i<3;i++)
            {
                Touch("LeakUseLink"+i);
                if(independent)
                {
                    if(i==1)Touch("LeakMissing");
                    Touch("LeakMatch");
                    var finding=_runtime.Session.GetFindings("OF-02").Single(f=>f.CriterionId=="leak-SIM-R00"+(i+1));
                    Assert.That(finding.EvidenceIds,Is.EquivalentTo(new[]{"SIM-R00"+(i+1),"SIM-L00"+(i+1)}));
                }
                else {Touch("CompleteLeakLearning");Touch("LearningNoIssue");Touch("LearningReturn");}
                if(i<2)Touch("OpenDocuments");
            }
            _runtime.Session.TryGetTask("OF-02",out var task);
            Assert.That(task.Status,Is.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Completed));
            Assert.That(_runtime.Session.GetFindings("OF-01"),Is.Empty);
            Assert.That(_runtime.OfficeFieldsViewed,Is.Empty);
            Assert.That(_runtime.OfficeRowsViewed,Is.Empty);
            if(independent)
            {
                Touch("FilterDate");
                Assert.That(panel.transform.Find("LeakMatch").GetComponent<Button>().interactable,Is.False,"Changing filters requires a fresh explicit use selection");
            }
        }
        [Test] public void SeatedOfficeOpensAtActualEyeHeightAndKeepsPagePose()
        {
            FinishOpening();OpenDoor();Touch("Travel_R01_OFFICE");Frames();
            Touch("NextTask");Frames();Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("OF-01"));
            var panel=_runtime.Visit.Panel;
            Assert.That(panel.name,Is.EqualTo("OfficeRecordsExpanded"));
            Assert.That(panel.transform.position.y,Is.LessThan(_rig.centerEyeAnchor.position.y));
            var pose=panel.transform.position;
            Touch("FilterDate");Assert.That(_runtime.Visit.Panel.transform.position,Is.EqualTo(pose));
        }
        [Test] public void DetailNavigationStopsAtLastItemAndHidesInternalModeLabel()
        {
            FinishOpening();OpenDoor();Touch("Travel_R01_OFFICE");Frames();OpenDoor();Touch("Travel_R02_STORAGE");Frames();Touch("StorageSample_ST-01");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("ST-01"));
            var step=_runtime.Visit.Panel.transform.Find("ScriptStep").GetComponent<TMPro.TMP_Text>();
            Assert.That(step.text,Does.Contain("细项 1/2"));
            Assert.That(step.text,Does.Not.Contain("安小卫带教"));
            Assert.That(step.text,Does.Not.Contain("独立核查"));
            Touch("NextDetail");Assert.That(_runtime.Visit.Panel.transform.Find("ScriptStep").GetComponent<TMPro.TMP_Text>().text,Does.Contain("细项 2/2"));
            var lastCopy=_runtime.Visit.Panel.transform.Find("ScriptBody").GetComponent<TMPro.TMP_Text>().text;
            Assert.That(_runtime.Visit.Panel.transform.Find("NextDetail"),Is.Null,
                "The last detail does not show a button that cannot move forward.");
            Assert.That(_runtime.Visit.Panel.transform.Find("ScriptStep").GetComponent<TMPro.TMP_Text>().text,Does.Contain("细项 2/2"));
            Assert.That(_runtime.Visit.Panel.transform.Find("ScriptBody").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo(lastCopy));
        }
        [Test] public void OfficeFieldSelectionRecordsViewAndDocumentReturnPreservesContext()
        {
            FinishOpening();OpenDoor();Touch("Travel_R01_OFFICE");Frames();Touch("NextTask");Frames();
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("OF-01"));
            var panel=_runtime.Visit.Panel;
            Assert.That(panel.name,Is.EqualTo("OfficeRecordsExpanded"));
            Assert.That(panel.transform.Find("ReadDetail"),Is.Null,
                "Field selection is the effective view action; a duplicate manual read button should not be shown.");
            Assert.That(panel.transform.Find("PreviousRow"),Is.Null);
            Assert.That(panel.transform.Find("NextRow"),Is.Null,
                "Each visible table row can be selected directly without paging buttons.");
            var position=panel.transform.position;var rotation=panel.transform.rotation;
            _runtime.Session.TryGetTask("OF-01",out var before);
            Touch("RecordField0");
            Assert.That(_runtime.OfficeFieldsViewed,Does.Contain(BotanicalGardenQR.Experience.Application.ClinicalTrainingRecords.Criterion(0)));
            Assert.That(_runtime.OfficeRowsViewed,Does.Contain("SIM-R001"));
            Touch("FilterDate");Touch("FilterScope");
            var rowInfo=panel.transform.Find("RowInfo").GetComponent<TMPro.TMP_Text>().text;
            Assert.That(rowInfo,Does.Contain("SIM-R001"));
            Touch("OpenDocuments");
            Assert.That(panel.transform.Find("Title").GetComponent<TMPro.TMP_Text>().text,Does.EndWith("清洗消毒"));
            Touch("BackToRows");

            Assert.That(panel.transform.position,Is.EqualTo(position));
            Assert.That(panel.transform.rotation,Is.EqualTo(rotation));
            Assert.That(panel.transform.Find("FilterDate").GetComponentInChildren<TMPro.TMP_Text>().text,Does.Contain("09-20"));
            Assert.That(panel.transform.Find("FilterScope").GetComponentInChildren<TMPro.TMP_Text>().text,Does.Contain("GI-001"));
            Assert.That(panel.transform.Find("RowInfo").GetComponent<TMPro.TMP_Text>().text,Does.Contain("SIM-R001"));
            Assert.That(_runtime.OfficeFieldsViewed,Has.Count.EqualTo(1));
            Assert.That(_runtime.OfficeRowsViewed,Is.EquivalentTo(new[]{"SIM-R001"}));
            Assert.That(_runtime.ScriptStepsViewed,Does.Not.Contain("OF-01:0"));
            Assert.That(_runtime.Session.GetFindings("OF-01"),Is.Empty,"Reading documents must not create a judgement.");
            _runtime.Session.TryGetTask("OF-01",out var after);
            Assert.That(after.Status,Is.Not.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Completed));
            Assert.That(after.Status,Is.EqualTo(before.Status));
        }
        [Test] public void OfficeDocumentGalleryShowsTheSelectedImageBesideAnUnobstructedControlSidebar()
        {
            FinishOpening();OpenDoor();Touch("Travel_R01_OFFICE");Frames();Touch("NextTask");Frames();
            var panel=_runtime.Visit.Panel;
            Touch("OpenDocuments");

            var imageNode=panel.transform.Find("DocumentImage");
            Assert.That(imageNode,Is.Not.Null,"A selected document should be presented as its own image.");
            var image=imageNode.GetComponent<RawImage>();
            Assert.That(AssetDatabase.GetAssetPath(image.texture),Does.EndWith("/office-disinfection-training-record-v1.png"));
            var imageRect=image.rectTransform;
            var gallery=(RectTransform)panel.transform.Find("DocumentGalleryControls");
            Assert.That(gallery,Is.Not.Null);
            var imageRight=imageRect.anchoredPosition.x+imageRect.rect.width*.5f;
            var controlsLeft=gallery.anchoredPosition.x-gallery.rect.width*.5f;
            Assert.That(imageRight,Is.LessThan(controlsLeft),"Navigation must remain outside the document image.");
            Assert.That(panel.transform.Find("DocumentGalleryControls/PreviousDocument"),Is.Null);
            Assert.That(panel.transform.Find("DocumentGalleryControls/NextDocument"),Is.Null);
            Assert.That(panel.transform.Find("DocumentGalleryControls/BackToRows"),Is.Not.Null);
            Assert.That(panel.transform.Find("DocumentGalleryControls/ReturnToTerminal"),Is.Null);
            Assert.That(panel.GetComponentsInChildren<Button>(),Has.Length.EqualTo(ClinicalTrainingRecords.DocumentCount+2),
                "The document gallery exposes six source choices, zoom, and one contextual return.");
            Assert.That(panel.transform.Find("DocumentGalleryControls/LeaveOfficeRecords"),Is.Null);
            for(int i=0;i<ClinicalTrainingRecords.DocumentCount;i++)
                Assert.That(panel.transform.Find("DocumentGalleryControls/Doc"+i),Is.Not.Null,
                    "Direct material selection replaces redundant previous/next controls.");

            _runtime.Session.TryGetTask("OF-01",out var before);
            var pose=panel.transform.position;
            var rotation=panel.transform.rotation;
            var originalWidth=imageRect.rect.width;
            Touch("ZoomDocument");
            var zoomedWidth=panel.transform.Find("DocumentImage").GetComponent<RawImage>().rectTransform.rect.width;
            Assert.That(zoomedWidth,Is.GreaterThan(originalWidth));
            Assert.That(((RectTransform)panel.transform).rect.height,Is.EqualTo(900));
            Assert.That(panel.transform.Find("DocumentGalleryControls"),Is.Null,
                "Focused reading should show the paper and one way back instead of the whole document menu.");
            Assert.That(panel.GetComponentsInChildren<Button>(),Has.Length.EqualTo(1));
            Assert.That(panel.transform.position,Is.EqualTo(pose));
            Assert.That(panel.transform.rotation,Is.EqualTo(rotation));
            Touch("ZoomDocument");
            Assert.That(((RectTransform)panel.transform).rect.height,Is.EqualTo(760));
            Assert.That(panel.transform.Find("DocumentGalleryControls"),Is.Not.Null);
            Touch("Doc4");
            Assert.That(panel.transform.Find("Title").GetComponent<TMPro.TMP_Text>().text,Does.EndWith("人员培训"));
            var trainingImage=panel.transform.Find("DocumentImage").GetComponent<RawImage>();
            Assert.That(AssetDatabase.GetAssetPath(trainingImage.texture),Does.EndWith("/office-staff-training-record-v1.png"));
            Touch("BackToRows");
            Assert.That(panel.transform.position,Is.EqualTo(pose));
            Assert.That(panel.transform.rotation,Is.EqualTo(rotation));
            _runtime.Session.TryGetTask("OF-01",out var after);
            Assert.That(after.Status,Is.EqualTo(before.Status),"Browsing a different document must not complete or alter the task.");
            Assert.That(_runtime.Session.GetFindings("OF-01"),Is.Empty);
        }
        [Test] public void WaitingUsesPublishedSourceMeshesAndAllowsUnobstructedObservation()
        {
            FinishOpening();
            foreach(var room in new[]{"R01_OFFICE","R02_STORAGE","R03_WAITING"})
            {OpenDoor();Touch("Travel_"+room);}
            Assert.That(_runtime.Visit.Map.roomResource,Is.EqualTo(FullScriptRoomCatalog.WaitingRoom));
            var root=_runtime.Visit.Room.Root;
            Assert.That(root.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("WaitingSeat")),Is.EqualTo(3));
            Assert.That(AssetDatabase.GetDependencies("Assets/EndoscopyTheme/Resources/FullScriptRooms/Waiting/WaitingRoom.prefab",true).Any(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)),Is.False);
            Assert.That(root.GetComponentsInChildren<Transform>().Any(t=>t.name=="PhysicalPartitionGlass"),Is.True);
            foreach(var mesh in root.GetComponentsInChildren<MeshFilter>())
                Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh),Does.StartWith("Assets/EndoscopyTheme/Resources/FullScriptRooms/"));
            var pose=_runtime.Visit.Panel.transform.position;
            var headPosition=_rig.centerEyeAnchor.position;
            var rigPosition=_rig.transform.position;
            ObserveTouch("InspectEnvironment");Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("StationaryRoomObservation"));
            var waitingPosition=root.transform.InverseTransformPoint(headPosition);
            Assert.That(waitingPosition.z,Is.EqualTo(-1.05f).Within(.001f));
            Assert.That(root.transform.InverseTransformDirection(_rig.centerEyeAnchor.forward).x,Is.LessThan(0),"The seated view must also face the chair/aisle side, not just the partition.");
            var partition=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="PhysicalPartitionBase").GetComponent<Renderer>();
            Assert.That(Vector3.Dot(partition.bounds.center-headPosition,_rig.centerEyeAnchor.forward),Is.GreaterThan(0));
            ObserveTouch("SwitchObservationSide");
            var corridorPosition=root.transform.InverseTransformPoint(headPosition);
            Assert.That(corridorPosition.z,Is.EqualTo(1.65f).Within(.001f));
            Assert.That(root.transform.InverseTransformDirection(_rig.centerEyeAnchor.forward).x,Is.GreaterThan(0));
            Assert.That(Vector3.Dot(partition.bounds.center-headPosition,_rig.centerEyeAnchor.forward),Is.GreaterThan(0));
            Assert.That(_rig.centerEyeAnchor.position,Is.EqualTo(headPosition));
            Assert.That(_rig.transform.position,Is.EqualTo(rigPosition));
            Assert.That(root.transform.localScale,Is.EqualTo(Vector3.one));
            var stablePosition=root.transform.position;var stableRotation=root.transform.rotation;Frames();
            Assert.That(root.transform.position,Is.EqualTo(stablePosition));Assert.That(root.transform.rotation,Is.EqualTo(stableRotation));
            ObserveTouch("SwitchObservationSide");
            Assert.That(root.transform.InverseTransformPoint(headPosition).z,Is.EqualTo(-1.05f).Within(.001f));
            Touch("ReturnToInspection");Assert.That(_runtime.Visit.Panel.transform.position,Is.EqualTo(pose));
            Assert.That(_runtime.ScriptStepsViewed,Is.Empty,"Looking around is not automatic completion.");
        }
        [TestCase(false)]
        public void StorageTeachingImageIsConsumedOnlyInGuidedModeAndCannotSignOff(bool independent)
        {
            FinishOpening();
            if(independent)Touch("ModeIndependent");
            foreach(var room in new[]{"R01_OFFICE","R02_STORAGE"})
            {OpenDoor();Touch("Travel_"+room);}
            Touch("StorageSample_ST-01");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("ST-01"));
            var picture=_runtime.Visit.Panel.transform.Find("ScriptReference");
            Assert.That(picture!=null,Is.EqualTo(!independent));
            if(!independent)
            {
                var texture=picture.GetComponent<RawImage>().texture;
                Assert.That(AssetDatabase.GetAssetPath(texture),Does.EndWith("/storage-cabinet-teaching-v1.png"));
                var pose=_runtime.Visit.Panel.transform.position;
                Touch("ExpandReference");
                Assert.That(_runtime.Visit.Panel.transform.Find("GeneratedReferenceOrigin").GetComponent<TMPro.TMP_Text>().text,Does.Contain("非实拍"));
                Assert.That(_runtime.Visit.Panel.transform.position,Is.EqualTo(pose));
                var imageRect=(RectTransform)_runtime.Visit.Panel.transform.Find("ScriptReference");
                Assert.That(imageRect.anchoredPosition.y-imageRect.rect.height/2,Is.GreaterThan(-210),"Expanded photo must not cover the action buttons.");
                Touch("ReadDetail");
            }
            else Assert.That(_runtime.Visit.Panel.transform.Find("ScriptBody").GetComponent<TMPro.TMP_Text>().text,Does.Not.Contain("三支"));
            _runtime.Session.TryGetTask("ST-01",out var task);
            Assert.That(task.Status,Is.Not.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Completed));
            Assert.That(_runtime.Session.IsContentAvailable("ST-01"),Is.False);
        }
        [TestCase(false)]
        public void StorageRecordsConsumeGeneratedPaperWithExplicitEvidenceAndFreezeAfterSubmission(bool independent)
        {
            FinishOpening();if(independent)Touch("ModeIndependent");
            foreach(var room in new[]{"R01_OFFICE","R02_STORAGE"})
            {OpenDoor();Touch("Travel_"+room);}
            Touch("StorageSample_ST-02");Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("ST-02"));
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("StorageRegisterObservation"));
            TouchMountedStorageRegister();
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("StorageCleaningRecords"));
            var pose=_runtime.Visit.Panel.transform.position;
            Assert.That(AssetDatabase.GetAssetPath(_runtime.Visit.Panel.transform.Find("StorageRegisterPaper").GetComponent<RawImage>().texture),Does.EndWith("/storage-cleaning-register-v1.png"));
            Assert.That(_runtime.Visit.Panel.transform.Find("RegisterScope").GetComponent<TMPro.TMP_Text>().text,Does.Contain("无另附登记"));
            Assert.That(_runtime.Visit.Panel.transform.Find("StorageCell2_1").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo(""));
            _runtime.Session.TryGetTask("ST-02",out var initial);
            Assert.That(initial.Status,Is.Not.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Completed));
            Assert.That(_runtime.Visit.Panel.transform.Find(independent?"StorageIssue":"CompleteStorageLearning").GetComponent<Button>().interactable,Is.False);
            for(int week=0;week<4;week++)
            {
                Touch("StorageWeek"+week);
                Assert.That(_runtime.Visit.Panel.transform.position,Is.EqualTo(pose));
                if(independent)Touch(week==2?"StorageIssue":"StorageNoIssue");
                else
                {
                    Touch("CompleteStorageLearning");
                    Touch(week==2?"LearningIssue":"LearningNoIssue");Touch("LearningReturn");
                }
            }
            _runtime.Session.TryGetTask("ST-02",out var completed);
            Assert.That(completed.Status,Is.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Completed));
            if(independent)
            {
                var findings=_runtime.Session.GetFindings("ST-02");Assert.That(findings,Has.Length.EqualTo(4));
                Assert.That(findings.Single(f=>f.CriterionId=="storage-week-3").EvidenceIds,Is.EqualTo(new[]{"SIM-ST-W3"}));
                Touch("StorageWeek2");Touch("StorageNoIssue");
                Assert.That(_runtime.Session.GetFindings("ST-02").Single(f=>f.CriterionId=="storage-week-3").Judgement,Is.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyJudgement.NoIssue));
                Touch("StorageIssue");
            }
            Touch("ReturnToInspection");Touch("StorageSample_ST-02");TouchMountedStorageRegister();
            Assert.That(_runtime.ScriptStepsViewed.Count(s=>s.StartsWith("ST-02:week:")),Is.EqualTo(4));
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(3))
            {OpenDoor();Touch("Travel_"+room);}
            Touch("SubmitJourney");Touch("SubmitJourney");
            Assert.That(_runtime.Session.IsFinished,Is.True);
            OpenDoor();Touch("Travel_R02_STORAGE");Touch("StorageSample_ST-02");TouchMountedStorageRegister();
            var oldCount=_runtime.ScriptStepsViewed.Count;Touch("StorageWeek2");
            Assert.That(_runtime.ScriptStepsViewed.Count,Is.EqualTo(oldCount));
            Assert.That(_runtime.Visit.Panel.transform.Find(independent?"StorageIssue":"CompleteStorageLearning").GetComponent<Button>().interactable,Is.False);
            Assert.That(_runtime.Visit.Panel.transform.Find("StorageRecordStatus").GetComponent<TMPro.TMP_Text>().text,Does.Contain("只读"));
            AssertNoArchivedModules();
        }
        [TestCase(false)]
        public void CabinetSideRegisterSharesDataHasGatedTouchAndReleasesWithRoom(bool independent)
        {
            FinishOpening();if(independent)Touch("ModeIndependent");
            foreach(var room in new[]{"R01_OFFICE","R02_STORAGE"})
            {OpenDoor();Touch("Travel_"+room);}
            Touch("StorageSample_ST-02");
            var root=_runtime.Visit.Room.Root;
            var mount=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="CabinetSideRegister").gameObject;
            var content=mount.transform.Find("RegisterContent");
            Assert.That(mount.transform.parent.name,Is.EqualTo("TrainingStorageCabinet"));
            Assert.That(mount.transform.localPosition,Is.EqualTo(new Vector3(-.622f,1.2f,0)));
            Assert.That(content.Find("StorageCell2_1").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo(""));
            var paper=content.Find("StorageRegisterPaper").GetComponent<RawImage>().texture;
            var head=_rig.centerEyeAnchor.position;var handPose=_rig.leftHandAnchor.position;
            var mountedButton=mount.transform.Find("OpenMountedRegister").GetComponent<Button>();
            var mountedTouch=mountedButton.GetComponent<BotanicalGardenQR.FrontendShell.Runtime.ClinicalNearTouch>();
            Assert.That(typeof(BotanicalGardenQR.FrontendShell.Runtime.ClinicalNearTouch).GetProperty("CanPress",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(mountedTouch),Is.True);
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("StorageRegisterObservation"));
            Assert.That(_rig.centerEyeAnchor.position,Is.EqualTo(head));Assert.That(_rig.leftHandAnchor.position,Is.EqualTo(handPose));
            Assert.That(Vector3.Dot(mount.transform.position-head,Vector3.ProjectOnPlane(_rig.centerEyeAnchor.forward,Vector3.up).normalized),Is.EqualTo(.6f).Within(.001f));
            AssertStorageLineOfSight(root,head,mount.transform.position);
            AssertStorageLineOfSight(root,head,_runtime.Visit.Panel.transform.Find("ReturnToInspection").position);
            var fixedPosition=mount.transform.position;var headRotation=_rig.centerEyeAnchor.localRotation;
            _rig.centerEyeAnchor.localRotation=Quaternion.Euler(0,20,0);Frames();
            Assert.That(mount.transform.position,Is.EqualTo(fixedPosition));_rig.centerEyeAnchor.localRotation=headRotation;
            using(var hand=new ClinicalHandFixture(mount,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(mountedButton);
            Frames();Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("StorageCleaningRecords"));
            Assert.That(_runtime.Visit.Panel.transform.Find("StorageRegisterPaper").GetComponent<RawImage>().texture,Is.SameAs(paper));
            AssertStorageLineOfSight(root,head,_runtime.Visit.Panel.transform.position);
            for(int row=0;row<4;row++)for(int column=0;column<5;column++)
                Assert.That(_runtime.Visit.Panel.transform.Find("StorageCell"+row+"_"+column).GetComponent<TMPro.TMP_Text>().text,
                    Is.EqualTo(content.Find("StorageCell"+row+"_"+column).GetComponent<TMPro.TMP_Text>().text));
            Assert.That(_runtime.ScriptStepsViewed.Any(s=>s.StartsWith("ST-02:week:")),Is.False);
            Touch("ReturnToInspection");
            Assert.That(root.transform.InverseTransformPoint(head).z,Is.EqualTo(0).Within(.001f));
            OpenDoor();Touch("Travel_R03_WAITING");
            Assert.That(mount==null,Is.True);Assert.That(GameObject.Find("CabinetSideRegister"),Is.Null);
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(4))
            {OpenDoor();Touch("Travel_"+room);}
            Touch("SubmitJourney");Touch("SubmitJourney");
            OpenDoor();Touch("Travel_R02_STORAGE");Touch("StorageSample_ST-02");
            mount=GameObject.Find("CabinetSideRegister");mountedButton=mount.transform.Find("OpenMountedRegister").GetComponent<Button>();
            using(var hand=new ClinicalHandFixture(mount,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(mountedButton);
            Frames();Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("StorageCleaningRecords"));
            Touch("StorageWeek2");
            Assert.That(_runtime.Visit.Panel.transform.Find(independent?"StorageIssue":"CompleteStorageLearning").GetComponent<Button>().interactable,Is.False);
            Assert.That(_runtime.Visit.Panel.transform.Find("StorageRecordStatus").GetComponent<TMPro.TMP_Text>().text,Does.Contain("只读"));
        }
        [Test] public void StorageUsesPublishedArchitectureAndActualHingedCabinetWithoutSigningOffMissingScopes()
        {
            FinishOpening();
            foreach(var room in new[]{"R01_OFFICE","R02_STORAGE"})
            {OpenDoor();Touch("Travel_"+room);}
            Touch("StorageSample_ST-01");
            Assert.That(_runtime.Visit.Map.roomResource,Is.EqualTo(FullScriptRoomCatalog.StorageRoom));
            var root=_runtime.Visit.Room.Root;
            Assert.That(root.GetComponentsInChildren<Transform>().Any(t=>t.name=="StorageCabinet" || t.name.StartsWith("WaitingSeat")),Is.False);
            foreach(var mesh in root.GetComponentsInChildren<MeshFilter>())
                Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh),Does.StartWith("Assets/EndoscopyTheme/Resources/FullScriptRooms/"));
            var dependencies=AssetDatabase.GetDependencies("Assets/EndoscopyTheme/Resources/FullScriptRooms/Storage/StorageRoom.prefab",true);
            Assert.That(dependencies.Any(p=>p.EndsWith("WaitingRoom.prefab")||p.EndsWith("Chair.prefab")||p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)),Is.False);
            Bounds LocalBounds(MeshFilter mesh)
            {
                var b=mesh.sharedMesh.bounds;var matrix=root.transform.worldToLocalMatrix*mesh.transform.localToWorldMatrix;
                var result=new Bounds(matrix.MultiplyPoint3x4(b.center),Vector3.zero);
                for(int corner=0;corner<8;corner++)result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3((corner&1)==0?b.min.x:b.max.x,(corner&2)==0?b.min.y:b.max.y,(corner&4)==0?b.min.z:b.max.z)));
                return result;
            }
            var cabinet=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="TrainingStorageCabinet");
            var cabinetParts=cabinet.GetComponentsInChildren<MeshFilter>();var cabinetBounds=LocalBounds(cabinetParts[0]);
            foreach(var part in cabinetParts.Skip(1))cabinetBounds.Encapsulate(LocalBounds(part));
            foreach(var wall in root.GetComponentsInChildren<MeshFilter>().Where(m=>m.GetComponent<MeshRenderer>().sharedMaterials.Any(material=>material.name=="PaintedWall")))
                Assert.That(cabinetBounds.Intersects(LocalBounds(wall)),Is.False,"Cabinet intersects source wall: "+wall.name);
            Touch("InspectStorageCabinet");
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("StorageCabinetObservation"));
            Assert.That(_runtime.ScriptActions.Contains("ST-01:cabinet-opened"),Is.False);
            var leaves=root.GetComponentsInChildren<Transform>().Where(t=>t.name=="LeftDoor"||t.name=="RightDoor").ToArray();
            Assert.That(leaves.Length,Is.EqualTo(2));
            foreach(var leaf in leaves)Assert.That(leaf.GetComponent<Oculus.Interaction.HandGrab.HandGrabInteractable>().enabled,Is.False);
            var head=_rig.centerEyeAnchor.position;
            Assert.That(root.transform.InverseTransformPoint(head).z,Is.EqualTo(0).Within(.001f));
            Touch("SwitchStorageDistance");Assert.That(_rig.centerEyeAnchor.position,Is.EqualTo(head));
            Assert.That(root.transform.InverseTransformPoint(head).z,Is.EqualTo(1).Within(.001f));
            foreach(var leaf in leaves)
            {
                var transformer=leaf.GetComponent<Oculus.Interaction.OneGrabRotateTransformer>();
                Assert.That(leaf.GetComponent<Oculus.Interaction.HandGrab.HandGrabInteractable>().enabled,Is.True);
                var pivot=transformer.ComputeWorldPivotPose();var handle=leaf.Find("HandleAnchor").position;
                var fixture=new CabinetGrabFixture(leaf,handle);transformer.Initialize(fixture);transformer.BeginTransform();
                float sign=leaf.name=="LeftDoor"?1:-1;
                foreach(float angle in new[]{35f,80f,130f})
                {
                    fixture.GrabPoints[0]=new Pose(pivot.position+Quaternion.AngleAxis(sign*angle,Vector3.up)*(handle-pivot.position),Quaternion.identity);
                    transformer.UpdateTransform();
                    Assert.That(Quaternion.Angle(leaf.localRotation,Quaternion.identity),Is.EqualTo(Mathf.Min(angle,105)).Within(.05f));
                    Assert.That(Vector3.Distance(leaf.position,pivot.position),Is.LessThan(.001f));
                }
                transformer.EndTransform();
            }
            Frames();Assert.That(_runtime.ScriptActions.Contains("ST-01:cabinet-opened"),Is.True);
            _runtime.Session.TryGetTask("ST-01",out var task);
            Assert.That(task.Status,Is.Not.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Completed));
            Assert.That(_runtime.Session.IsContentAvailable("ST-01"),Is.False);
            Touch("ReturnToInspection");
            foreach(var leaf in leaves)Assert.That(leaf.GetComponent<Oculus.Interaction.HandGrab.HandGrabInteractable>().enabled,Is.False);
            Assert.That(root.transform.InverseTransformPoint(head).z,Is.EqualTo(0).Within(.001f));
        }
        sealed class CabinetGrabFixture:Oculus.Interaction.IGrabbable
        {
            public List<Pose> GrabPoints { get; }=new List<Pose>();
            public Transform Transform { get; }
            public CabinetGrabFixture(Transform target,Vector3 position){Transform=target;GrabPoints.Add(new Pose(position,Quaternion.identity));}
        }
        void ObserveTouch(string name)
        {
            var panel=_runtime.Visit.Panel;
            var button=panel.GetComponentsInChildren<Button>().Single(b=>b.name==name);
            using(var hand=new ClinicalHandFixture(panel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(button);
            // The fixture deliberately looks toward the button before poking.
            // Freeze that user pose, then verify every transition frame itself.
            var position=_rig.centerEyeAnchor.position;var rotation=_rig.centerEyeAnchor.rotation;
            var rootPosition=_runtime.Visit.Room.Root.transform.position;
            _runtime.Tick(.05f);
            Assert.That(_runtime.InputAllowed,Is.False,"Observation switches must enter the curtain state machine.");
            Assert.That(_runtime.Visit.Room.Root.transform.position,Is.EqualTo(rootPosition),"No movement before fade-out.");
            Assert.That(_runtime.RequestWaitingObservation(false,()=>{}),Is.False,"Duplicate requests are blocked during transition.");
            for(int frame=0;frame<40;frame++)
            {
                _runtime.Tick(.05f);
                Assert.That(_rig.centerEyeAnchor.position,Is.EqualTo(position));
                Assert.That(_rig.centerEyeAnchor.rotation,Is.EqualTo(rotation));
            }
        }
        [Test] public void MissingTaskReadDoesNotSignOffAndReleaseBarrierStillBlocksLoading()
        {
            FinishOpening();OpenDoor();Touch("Travel_R01_OFFICE");Frames();
            Touch("ReadDetail");
            Assert.That(_runtime.ScriptStepsViewed.Contains("OF-00:0"),Is.False);
            Assert.That(_runtime.Session.LearningActionCount("OF-00",ClinicalLearningAction.Hint),Is.EqualTo(1));
            _runtime.Session.TryGetTask("OF-00",out var task);
            Assert.That(task.Status,Is.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Unstarted));
            _release.Complete=false;OpenDoor();
            var panel=_runtime.Visit.Panel;
            var travel=panel.GetComponentsInChildren<Button>().Single(button=>button.name=="Travel_R02_STORAGE");
            using(var hand=new ClinicalHandFixture(panel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))
                hand.Touch(travel);
            Frames();
            Assert.That(_runtime.Visit,Is.Null);Assert.That(_runtime.Session.CurrentRoomId,Is.EqualTo("R01_OFFICE"));
            _release.Complete=true;Frames();Assert.That(_runtime.Visit.RoomId,Is.EqualTo("R02_STORAGE"));
        }
        [Test] public void DoorCompletionUsesActualLeafAngleAndSummaryRequiresSecondConfirmation()
        {
            FinishOpening();
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(1))
            {
                OpenDoor();Touch("Travel_"+room);
                if(room==FullScriptRoomCatalog.Washing)
                {
                    var door=GameObject.Find("Door342041(Clone)");Assert.That(door,Is.Not.Null);
                    Assert.That(_runtime.ScriptActions.Contains("RE-01:door-closed"),Is.False);
                    var leaf=door.transform.Find("Leaf");
                    Assert.That(leaf.GetComponent<Oculus.Interaction.OneGrabRotateTransformer>(),Is.Not.Null);
                    leaf.localRotation=Quaternion.identity;Frames();
                    Assert.That(_runtime.ScriptActions.Contains("RE-01:door-closed"),Is.True);
                    _runtime.Session.TryGetTask("RE-01",out var task);
                    Assert.That(task.Status,Is.Not.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Completed));
                }
            }
            var summaryTitle=_runtime.Visit.Panel.transform.Find("RoomTitle").GetComponent<TMPro.TMP_Text>();
            var summaryBody=_runtime.Visit.Panel.transform.Find("RoomBrief").GetComponent<TMPro.TMP_Text>();
            var summaryHint=_runtime.Visit.Panel.transform.Find("WalkingHint").GetComponent<TMPro.TMP_Text>();
            Assert.That(summaryTitle.color,Is.EqualTo(ClinicalPanelStyle.TextPrimary),
                "Summary headings on the light panel must remain visible.");
            Assert.That(summaryBody.color,Is.EqualTo(ClinicalPanelStyle.TextPrimary),
                "Summary text on the light panel must remain visible.");
            Assert.That(summaryBody.text,Does.Contain("暂不可用"));
            Assert.That(summaryBody.text,Does.Contain("使用提示"));
            Assert.That(summaryBody.text,Does.Not.Contain("有效查阅"),
                "The decision page should keep learning help while omitting low-value operation counts.");
            Assert.That(summaryHint.color,Is.EqualTo(ClinicalPanelStyle.Muted),
                "The summary action hint must remain visible on the light panel.");
            var visibleSummaryButtons=_runtime.Visit.Panel.GetComponentsInChildren<Button>();
            Assert.That(visibleSummaryButtons.Length,Is.InRange(4,5),
                "The summary has details, submission, room review, and office review; field review appears when review data exists.");
            Touch("SubmitJourney");Assert.That(_runtime.Session.IsFinished,Is.False);
            Touch("SubmitJourney");Assert.That(_runtime.Session.IsFinished,Is.True);
            Assert.That(_runtime.Visit.Panel.transform.Find("SubmitJourney").gameObject.activeSelf,Is.False,
                "Submitted sessions show the review action instead of a disabled submit control.");
            Assert.That(_runtime.Visit.Panel.transform.Find("RecordReview").gameObject.activeSelf,Is.True);
        }
        [Test] public void StationaryThemeKeepsHistoricalSourceAndRemovesStandingHeightClamp()
        {
            var source=AssetDatabase.LoadAssetAtPath<BotanicalGardenQR.Configuration.Runtime.VisitorPrologueThemeAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/VisitorPrologueTheme.asset");
            Assert.That(source,Is.Not.Null);
            Assert.That(_bindings.Configuration.PrologueTheme,Is.Null,
                "The current stationary route must not bind the archived prologue theme.");
            source.Copy.TryGetEncounterPage(3,out var original);
            var clone=source.CreateStationaryVariant();
            try
            {
                clone.Copy.TryGetEncounterPage(3,out var updated);
                Assert.That(updated,Does.Contain("留在原位"));
                Assert.That(clone.MinimumStandingPanelCenterHeight,Is.LessThan(.4f));
                source.Copy.TryGetEncounterPage(3,out var unchanged);Assert.That(unchanged,Is.EqualTo(original));
            }
            finally{UnityEngine.Object.DestroyImmediate(clone);}
        }
        [TestCase(false)]
        public void LatestBrushDetailConsumesExistingImageWithoutRestoringOldCourse(bool independent)
        {
            FinishOpening();if(independent)Touch("ModeIndependent");
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(1))
            {
                OpenDoor();Touch("Travel_"+room);
                if(room==FullScriptRoomCatalog.Washing)break;
            }
            Touch("NextTask");Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("RE-02"));
            if(!independent)
            {
                Assert.That(AssetDatabase.GetAssetPath(_runtime.Visit.Panel.GetComponentInChildren<RawImage>().texture),Does.EndWith("/equipment-hotspot-teaching-v1.png"));
                Touch("CompareScriptSource");
                Assert.That(AssetDatabase.GetAssetPath(_runtime.Visit.Panel.GetComponentInChildren<RawImage>().texture),Does.EndWith("/script-equipment.png"));
                Touch("CompareScriptSource");
            }
            Touch("NextDetail");Touch("NextDetail");
            var pose=_runtime.Visit.Panel.transform.position;
            var image=_runtime.Visit.Panel.GetComponentInChildren<RawImage>();
            Assert.That(image!=null,Is.EqualTo(!independent));
            Assert.That(_runtime.Visit.Panel.transform.Find("BrushFocus")!=null,Is.EqualTo(!independent));
            if(!independent)
            {
                Assert.That(AssetDatabase.GetAssetPath(image.texture),Does.EndWith("/brush-inspection.png"));
                Assert.That(_runtime.Visit.Panel.transform.Find("ScriptBody").GetComponent<TMPro.TMP_Text>().text,Does.Contain("非实拍"));
                foreach(var expected in new[]{new Rect(.77f,.18f,.22f,.55f),new Rect(.43f,.20f,.40f,.53f),new Rect(0,0,1,1)})
                {
                    Touch("BrushFocus");image=_runtime.Visit.Panel.GetComponentInChildren<RawImage>();
                    Assert.That(image.uvRect,Is.EqualTo(expected));
                    Assert.That(_runtime.Visit.Panel.transform.position,Is.EqualTo(pose));
                    Assert.That(image.rectTransform.rect.width/image.rectTransform.rect.height,
                        Is.EqualTo(image.texture.width*expected.width/(image.texture.height*expected.height)).Within(.001f));
                }
                Touch("ExpandReference");
                Assert.That(_runtime.Visit.Panel.transform.Find("GeneratedReferenceOrigin"),Is.Not.Null);
                image=_runtime.Visit.Panel.GetComponentInChildren<RawImage>();
                Assert.That(image.rectTransform.anchoredPosition.y-image.rectTransform.rect.height/2,Is.GreaterThan(-210));
            }
            Touch("ReadDetail");Assert.That(_runtime.Session.LearningActionCount("RE-02",ClinicalLearningAction.Hint),Is.EqualTo(1));
            _runtime.Session.TryGetTask("RE-02",out var state);
            Assert.That(state.Status,Is.Not.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Completed));
            Touch("PreviousDetail");
            Assert.That(_runtime.Visit.Panel.transform.Find("BrushFocus"),Is.Null);
            if(!independent)
            {
                image=_runtime.Visit.Panel.GetComponentInChildren<RawImage>();
                Assert.That(AssetDatabase.GetAssetPath(image.texture),Does.EndWith("/equipment-hotspot-teaching-v1.png"));
                Assert.That(image.uvRect,Is.EqualTo(new Rect(0,0,1,1)));
                Assert.That(_runtime.Visit.Panel.transform.Find("ScriptBody").GetComponent<TMPro.TMP_Text>().text,Is.Not.Empty);
            }
            AssertNoArchivedModules();
            Touch("NextTask");Touch("NextTask");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo("RE-06"));
            var ppe=_runtime.Visit.Panel.GetComponentInChildren<RawImage>();
            Assert.That(AssetDatabase.GetAssetPath(ppe.texture),Does.EndWith("/ppe-teaching-plate-v2.png"));
            for(int region=0;region<6;region++)
            {
                Assert.That(_runtime.Visit.Panel.GetComponentInChildren<RawImage>().transform.Find("PPEFocus"),Is.Not.Null);
                if(region<5)Touch("NextDetail");
            }
            Touch("CompareScriptSource");
            Assert.That(AssetDatabase.GetAssetPath(_runtime.Visit.Panel.GetComponentInChildren<RawImage>().texture),Does.EndWith("/script-ppe.png"));
            OpenDoor();Touch("Travel_R01_OFFICE");
            Assert.That(UnityEngine.Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None)
                .Any(r=>r.texture && AssetDatabase.GetAssetPath(r.texture).EndsWith("/brush-inspection.png")),Is.False);
        }
        [TestCase("ST-03","R02_STORAGE","FullScriptRooms/Stationary/Gastroscope")]
        [TestCase("RE-04","R05_REPROCESSING","ClinicalCourse/Disinfectant/disinfectant-labeled")]
        public void SourceObjectsHaveDedicatedInspectionAndExplicitRecovery(string taskId,string roomId,string resource)
        {
            FinishOpening();
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(1))
            {
                OpenDoor();Touch("Travel_"+room);
                if(room==roomId)break;
            }
            if(roomId=="R02_STORAGE")Touch("StorageSample_ST-03");
            for(int step=0;_runtime.Visit.ScriptTaskId!=taskId && step<6;step++)Touch("NextTask");
            Assert.That(_runtime.Visit.ScriptTaskId,Is.EqualTo(taskId));
            Assert.That(GameObject.Find("ScriptInspectableModel"),Is.Null,"Do not hide the source object behind the reading panel.");
            var panelPose=_runtime.Visit.Panel.transform.position;
            Touch("InspectObject");var model=GameObject.Find("ScriptInspectableModel");
            Assert.That(model,Is.Not.Null);
            Assert.That(_runtime.Visit.Panel.name,Is.EqualTo("ScriptObjectObservation"));
            // Match the seated preview's neutral viewing direction, not the hand
            // fixture's deliberate LookAt toward a touched button.
            var camera=_rig.centerEyeAnchor.GetComponent<Camera>();var rotation=camera.transform.rotation;
            var fov=camera.fieldOfView;var aspect=camera.aspect;
            try
            {
                camera.transform.rotation=_runtime.Visit.Panel.transform.rotation;camera.fieldOfView=78;camera.aspect=4f/3;
                var corners=new Vector3[4];((RectTransform)_runtime.Visit.Panel.transform).GetWorldCorners(corners);
                foreach(var corner in corners)
                {
                    var viewport=camera.WorldToViewportPoint(corner);
                    Assert.That(viewport.z,Is.GreaterThan(0));
                    Assert.That(viewport.y,Is.InRange(.01f,.99f),"Object inspection toolbar must fit the neutral seated view.");
                }
                float toolbarTop=corners.Max(c=>camera.WorldToViewportPoint(c).y);
                float objectBottom=1;
                foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    var bounds=renderer.localBounds;
                    for(int c=0;c<8;c++)objectBottom=Mathf.Min(objectBottom,camera.WorldToViewportPoint(renderer.transform.TransformPoint(new Vector3(
                        (c&1)==0?bounds.min.x:bounds.max.x,(c&2)==0?bounds.min.y:bounds.max.y,(c&4)==0?bounds.min.z:bounds.max.z))).y);
                }
                Assert.That(objectBottom,Is.GreaterThan(toolbarTop+.01f),"Source model must not cover the observation caption.");
            }
            finally{camera.transform.rotation=rotation;camera.fieldOfView=fov;camera.aspect=aspect;}
            Assert.That(_runtime.Visit.Panel.transform.position.y,Is.LessThan(model.transform.position.y-.25f));
            Assert.That(model.GetComponent<Oculus.Interaction.HandGrab.HandGrabInteractable>().enabled,Is.True);
            var source=Resources.Load<GameObject>(resource);
            CollectionAssert.AreEquivalent(source.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh),model.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh));
            CollectionAssert.AreEquivalent(source.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials),model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials));
            var collider=model.GetComponent<BoxCollider>();
            Assert.That(Mathf.Max(collider.size.x,Mathf.Max(collider.size.y,collider.size.z)),Is.EqualTo(.38f).Within(.001f));
            var home=new Pose(model.transform.position,model.transform.rotation);
            model.transform.SetPositionAndRotation(home.position+Vector3.right*1.4f,Quaternion.Euler(20,50,80));
            Frames();Assert.That(model.transform.position,Is.EqualTo(home.position+Vector3.right*1.4f),"Distance must not silently teleport a held object.");
            Touch("RecoverObject");Assert.That(model.transform.position,Is.EqualTo(home.position));
            Assert.That(Quaternion.Angle(model.transform.rotation,home.rotation),Is.LessThan(.01f));
            _runtime.Session.TryGetTask(taskId,out var state);
            Assert.That(state.Status,Is.Not.EqualTo(BotanicalGardenQR.Experience.Contracts.ClinicalJourneyTaskStatus.Completed));
            Touch("ReturnToInspection");Assert.That(GameObject.Find("ScriptInspectableModel"),Is.Null);
            Assert.That(_runtime.Visit.Panel.transform.position,Is.EqualTo(panelPose));
            AssertNoArchivedModules();
            foreach(var room in _runtime.Definition.mainlineRoomIds.Skip(Array.IndexOf(_runtime.Definition.mainlineRoomIds,roomId)+1))
            {OpenDoor();Touch("Travel_"+room);}
            Touch("SubmitJourney");Touch("SubmitJourney");Assert.That(_runtime.Session.IsFinished,Is.True);
            OpenDoor();Touch("Travel_"+roomId);
            if(roomId=="R02_STORAGE")Touch("StorageSample_ST-03");
            for(int step=0;_runtime.Visit.ScriptTaskId!=taskId && step<6;step++)Touch("NextTask");
            Touch("InspectObject");model=GameObject.Find("ScriptInspectableModel");
            Assert.That(model,Is.Not.Null);
            Assert.That(model.GetComponent<Oculus.Interaction.HandGrab.HandGrabInteractable>().enabled,Is.False);
            Assert.That(model.GetComponent<Oculus.Interaction.Grabbable>().enabled,Is.False);
            Assert.That(_runtime.Visit.Panel.transform.Find("RecoverObject").GetComponent<Button>().interactable,Is.False);
            Assert.That(_runtime.Visit.Panel.transform.Find("ObjectObservationStatus").GetComponent<TMPro.TMP_Text>().text,Does.Contain("只读回看"));
            Touch("ReturnToInspection");Assert.That(GameObject.Find("ScriptInspectableModel"),Is.Null);
        }
        static void AssertStorageLineOfSight(GameObject root,Vector3 eye,Vector3 target)
        {
            var delta=target-eye;var ray=new Ray(eye,delta.normalized);
            bool backfaces=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
            try
            {
                foreach(var mesh in root.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer=mesh.GetComponent<Renderer>();
                    if(!renderer || renderer.sharedMaterials.All(m=>m && m.renderQueue>=3000))continue;
                    var probe=new GameObject("VisibilityRegressionProbe");probe.transform.SetParent(mesh.transform,false);
                    try
                    {
                        var collider=probe.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;Physics.SyncTransforms();
                        bool blocked=collider.Raycast(ray,out var hit,delta.magnitude-.001f);
                        Assert.That(blocked,Is.False,"Opaque mesh blocks register/UI: "+mesh.name+" at "+hit.distance);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(probe);}
                }
            }
            finally{Physics.queriesHitBackfaces=backfaces;}
        }
        void TouchMountedStorageRegister()
        {
            var mount=_runtime.Visit.Room.Root.GetComponentsInChildren<Transform>()
                .Single(transform=>transform.name=="CabinetSideRegister").gameObject;
            var button=mount.transform.Find("OpenMountedRegister").GetComponent<Button>();
            using(var hand=new ClinicalHandFixture(mount,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(button);
            Frames();
        }
        void Touch(string name)
        {
            var guide=FindGuide();
            if(guide && guide.CurrentState?.Owner==VisitorDialogueOwner.Guidance)TouchGuide(guide);
            var panel=_runtime.Visit.Panel;
            var button=panel.GetComponentsInChildren<Button>().Single(b=>b.name==name);
            if(name.StartsWith("Travel_",StringComparison.Ordinal))
            {
                // Browse as a visitor would before touching a card outside the visible trio.
                for(var step=0;step<7 && button.GetComponent<CanvasGroup>().alpha<.65f;step++)
                {
                    // EditMode's manual runtime ticks do not advance Time.unscaledTime;
                    // represent the natural pause between successive arrow presses.
                    typeof(FullScriptRoomVisit).GetField("_roomGallerySelectionEnableAt",
                        BindingFlags.Instance|BindingFlags.NonPublic).SetValue(_runtime.Visit,float.NegativeInfinity);
                    Touch(button.transform.localPosition.x>0 ? "GalleryNext" : "GalleryPrevious");
                }
                Assert.That(button.GetComponent<CanvasGroup>().alpha,Is.GreaterThanOrEqualTo(.65f),
                    name+" did not become visible after browsing.");
                typeof(FullScriptRoomVisit).GetField("_roomGallerySelectionEnableAt",
                    BindingFlags.Instance|BindingFlags.NonPublic).SetValue(_runtime.Visit,float.NegativeInfinity);
            }
            using(var hand=new ClinicalHandFixture(panel,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(button);
            Frames();
            if(name.StartsWith("Travel_",StringComparison.Ordinal))
            {
                guide=FindGuide();
                if(guide && guide.CurrentState?.Owner==VisitorDialogueOwner.Guidance)TouchGuide(guide);
                if(!_keepThemeSelector && _runtime.Visit.Panel?.name=="RoomThemeSelector")Touch("Theme_0");
                if(!_keepThemeSelector && _runtime.Visit.Panel?.name=="RoomThemeTasks")
                    Touch(_runtime.Visit.Panel.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("ThemeTask_",StringComparison.Ordinal)).name);
                if(!_keepThemeSelector && _runtime.Visit.Panel?.name=="WaitingObservationSelector")Touch("ThemeBack");
            }
        }
        bool OpenDoor()
        {
            var guide=FindGuide();
            if(guide && guide.CurrentState?.Owner==VisitorDialogueOwner.Guidance)TouchGuide(guide);
            return _runtime.Visit.TryOpen(FullScriptRoomCatalog.Door);
        }
        void FinishOpening()
        {
            Assert.That(_runtime.Prologue, Is.Null);
            var guide=FindGuide();
            Assert.That(guide,Is.Not.Null);
            if(guide.CurrentState?.Owner==VisitorDialogueOwner.Guidance)TouchGuide(guide);
            Assert.That(_runtime.Visit.Panel, Is.Not.Null);
        }
        VisitorCoachPresenter FindGuide()
            => UnityEngine.Object.FindObjectsByType<VisitorCoachPresenter>(FindObjectsInactive.Include,FindObjectsSortMode.None).SingleOrDefault();
        void TouchGuide(VisitorCoachPresenter guide)
        {
            var button=guide.GetComponentsInChildren<Button>(true).Single(candidate=>
                candidate.GetComponentInChildren<TMPro.TMP_Text>(true)?.text==guide.CurrentState.PrimaryActionLabel);
            using(var hand=new ClinicalHandFixture(guide.gameObject,_bindings.Platform.Viewer,_bindings.Presentation.HeadGaze))hand.Touch(button);
            Frames();
        }
        void Frames(){for(int i=0;i<40;i++)_runtime.Tick(.05f);}
        sealed class Timing:ITrackingOriginTiming
        {public bool Tracked=true;public bool TryGetSampleTime(out double t){t=9;return Tracked;}public bool TryGetChangeTime(OVRManager.TrackingOrigin o,out double t){t=10;return true;}}
        sealed class Release:IClinicalRoomAssetRelease
        {public bool Complete;public void Begin(){}public bool IsComplete=>Complete;}
    }
}
