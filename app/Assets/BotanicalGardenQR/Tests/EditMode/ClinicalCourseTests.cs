using System;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalCourseTests
    {
        [TestCase("hidden")]
        [TestCase("feedback")]
        [TestCase("failed")]
        public void LateVideoPreparationCannotRestartInactivePlayback(string state)
        {
            var root = new GameObject("Video lifecycle test");
            var viewer = new GameObject("Video viewer", typeof(Camera));
            var events = new GameObject("Video events", typeof(EventSystem));
            var gaze = events.AddComponent<HeadGazeDwellController>(); gaze.Configure(viewer.GetComponent<Camera>(), events.GetComponent<EventSystem>());
            var font = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>("Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset").SharedFont;
            var panel = new ClinicalCoursePanel(root.transform, font, gaze, _ => true);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo Field(string name) => typeof(ClinicalCoursePanel).GetField(name, flags);
            void Call(string name, params object[] args) => typeof(ClinicalCoursePanel).GetMethod(name, flags).Invoke(panel, args);
            try
            {
                panel.Present(SessionToken.CreateNew(), "bottle_tree"); panel.SetVisible(true);
                panel.Session.Continue(); Call("Render");
                var player = root.GetComponentInChildren<VideoPlayer>();
                // Replay only the asynchronous boundary; real decoding is a separate media check.
                Field("_prepareTime").SetValue(panel, 3f);
                Field("_videoPlaybackRequested").SetValue(panel, true);
                if (state == "hidden") panel.SetVisible(false);
                else if (state == "feedback")
                {
                    panel.Session.Select(panel.Session.Step.correct); Assert.That(panel.Session.Submit(), Is.True); Call("Render");
                    Assert.That(root.GetComponentsInChildren<Button>(true).Single(b => b.name == "MediaAction").interactable, Is.False);
                }
                else Call("VideoError", player, "simulated decode failure");
                var body = root.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "EvidenceBody");
                var copy = body.text;
                Call("VideoReady", player);
                Assert.That((float)Field("_prepareTime").GetValue(panel), Is.Zero);
                Assert.That((bool)Field("_videoPlaybackRequested").GetValue(panel), Is.False);
                Assert.That(player.isPlaying, Is.False);
                Assert.That(body.text, Is.EqualTo(copy), "A late callback must not erase the question or failure context.");
                if (state != "failed")
                    Assert.That(root.GetComponentsInChildren<RawImage>(true).Single().texture, Is.EqualTo(player.targetTexture));
                panel.SetVisible(true); panel.Tick(15);
                Assert.That((bool)Field("_videoFailed").GetValue(panel), Is.EqualTo(state == "failed"), "Preparation completed while hidden must not time out on return.");
            }
            finally { panel.Dispose(); gaze.Unconfigure(); Object.DestroyImmediate(root); Object.DestroyImmediate(viewer); Object.DestroyImmediate(events); }
        }

        [TestCase("baobab")]
        [TestCase("bottle_tree")]
        [TestCase("ceiba")]
        [TestCase("macrozamia")]
        [TestCase("welwitschia")]
        public void EveryLaterStationCanRetryOrSkipWithoutFalseCredit(string scene)
        {
            var lesson = ClinicalCourseCatalog.Load().Find(scene);
            for (int skipMask = 0; skipMask < 1 << lesson.steps.Length; skipMask++)
            {
                var session = new ClinicalCourseSession(lesson);
                Assert.That(session.Submit(), Is.False); session.Skip();
                Assert.That(session.Phase, Is.EqualTo(ClinicalCoursePhase.Introduction)); session.Continue();
                int skipped = 0;
                for (int i = 0; i < lesson.steps.Length; i++)
                {
                    Assert.That(session.Selected, Is.EqualTo(-1));
                    if ((skipMask & (1 << i)) != 0) { session.Skip(); skipped++; continue; }
                    if (session.Step.mode == "evidence")
                    {
                        session.Select(session.Step.correct);
                        Assert.That(session.Submit(), Is.False, "Three sources must be read before awarding an evidence judgement.");
                        for (int card = 0; card < 3; card++) session.ReadEvidence(card);
                    }
                    if (session.Step.mode == "sequence")
                    {
                        foreach (var card in session.Step.sequenceOrder.Reverse()) session.ToggleSequence(card);
                        Assert.That(session.Submit(), Is.False); Assert.That(session.Sequence.Count, Is.EqualTo(5));
                        foreach (var card in session.Sequence.ToArray()) session.ToggleSequence(card);
                        foreach (var card in session.Step.sequenceOrder) session.ToggleSequence(card);
                    }
                    else
                    {
                        var wrong = (session.Step.correct + 1) % 3;
                        session.Select(wrong); Assert.That(session.Submit(), Is.False);
                        Assert.That(session.Selected, Is.EqualTo(wrong));
                        session.Select(session.Step.correct);
                    }
                    Assert.That(session.Phase, Is.EqualTo(ClinicalCoursePhase.Task));
                    Assert.That(session.Submit(), Is.True);
                    Assert.That(session.Submit(), Is.False, "No duplicate credit while feedback is displayed."); session.Continue();
                }
                Assert.That(session.Phase, Is.EqualTo(ClinicalCoursePhase.Finished));
                Assert.That(session.SkippedCount, Is.EqualTo(skipped));
                Assert.That(session.CorrectCount, Is.EqualTo(lesson.steps.Length - skipped));
                session.Skip(); session.Continue();
                Assert.That(session.CorrectCount + session.SkippedCount, Is.EqualTo(lesson.steps.Length));
            }
        }

        [TestCase("baobab", true)]
        [TestCase("bottle_tree", true)]
        [TestCase("ceiba", true)]
        [TestCase("macrozamia", true)]
        [TestCase("welwitschia", true)]
        public void ActualHandsRunAllMediaLessonsWithFixedReadableUi(string scene, bool useHands)
        {
            var root = new GameObject("Course test");
            var cameraObject = new GameObject("Course camera", typeof(Camera)); cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            var events = new GameObject("Course events", typeof(EventSystem));
            var gaze = events.AddComponent<HeadGazeDwellController>(); gaze.Configure(camera, events.GetComponent<EventSystem>());
            var font = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>("Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset").SharedFont;
            int completed = 0; bool closeAllowed = false;
            var panel = new ClinicalCoursePanel(root.transform, font, gaze, token => { if (!closeAllowed) return false; completed++; return true; });
            using var hand = new ClinicalHandFixture(root, camera.transform, gaze);
            Button Find(string name) => root.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
            void Confirm(string name) => hand.Touch(Find(name));
            void TextFits()
            {
                Canvas.ForceUpdateCanvases();
                foreach (var text in root.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate(); Assert.That(text.isTextOverflowing, Is.False, scene + ": " + text.name + " " + text.text);
                }
            }
            try
            {
                var token = SessionToken.CreateNew(); panel.Present(token, scene); panel.SetVisible(true);
                var board = root.transform.Find("ClinicalCoursePanel"); var position = board.position;
                TextFits(); panel.Tick(15); Confirm("ContinueCourse");
                Assert.That(root.GetComponentsInChildren<TMP_Text>().Any(t => t.name == "BriefFairyTip"), Is.True, "Reading the intro must not consume the task tutorial timer.");
                while (panel.Session.Phase != ClinicalCoursePhase.Finished)
                {
                    TextFits();
                    Assert.That(Find("SkipCourse").GetComponent<ClinicalNearTouch>(), Is.Not.Null);
                    if (panel.Session.Step.mode == "image")
                    {
                        var resource = Resources.Load(panel.Session.Step.media);
                        Assert.That(resource, Is.InstanceOf<Texture2D>(), "Image resource type: " + resource?.GetType().Name);
                        Assert.That(root.GetComponentInChildren<RawImage>(), Is.Not.Null, "Loaded image must be visible.");
                        Assert.That(root.GetComponentInChildren<RawImage>().texture, Is.Not.Null);
                    }
                    if (panel.Session.Step.mode == "video")
                    {
                        var player = root.GetComponentInChildren<VideoPlayer>();
                        Assert.That(player.clip, Is.Not.Null); Assert.That(player.audioOutputMode, Is.EqualTo(VideoAudioOutputMode.None));
                        Assert.That(player.clip.length, Is.InRange(10d, 60d));
                    }
                    if (panel.Session.Step.mode == "model")
                    {
                        var grab = root.GetComponentInChildren<Oculus.Interaction.HandGrab.HandGrabInteractable>();
                        Assert.That(grab, Is.Not.Null); Assert.That(grab.Rigidbody.isKinematic, Is.True);
                        var meshes = grab.GetComponentsInChildren<MeshFilter>(); Assert.That(meshes.Length, Is.GreaterThan(0));
                        var bounds = meshes[0].GetComponent<Renderer>().bounds;
                        foreach (var mesh in meshes.Skip(1)) bounds.Encapsulate(mesh.GetComponent<Renderer>().bounds);
                        Assert.That(bounds.size.y, Is.InRange(.15f, .25f), "Imported bottle should be within hand reach at teaching scale.");
                        Confirm("MediaAction");
                        var grabbable = grab.GetComponent<Oculus.Interaction.Grabbable>();
                        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        typeof(Oculus.Interaction.Grabbable).GetMethod("Awake", flags).Invoke(grabbable, null);
                        typeof(Oculus.Interaction.Grabbable).GetMethod("Start", flags).Invoke(grabbable, null);
                        var locker = grabbable.GetComponent<Oculus.Interaction.RigidbodyKinematicLocker>();
                        if (!locker)
                        {
                            locker = grabbable.gameObject.AddComponent<Oculus.Interaction.RigidbodyKinematicLocker>();
                            typeof(Oculus.Interaction.RigidbodyKinematicLocker).GetMethod("Awake", flags).Invoke(locker, null);
                        }
                        var pose = new Pose(grabbable.transform.position, grabbable.transform.rotation);
                        grabbable.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(917, Oculus.Interaction.PointerEventType.Hover, pose));
                        grabbable.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(917, Oculus.Interaction.PointerEventType.Select, pose));
                        grabbable.transform.localPosition = new Vector3(1400, 0, 0);
                        var heldPosition = grabbable.transform.position;
                        panel.Tick(.1f); Confirm("MediaAction");
                        Assert.That(grabbable.transform.position, Is.EqualTo(heldPosition), "Neither distance recovery nor the other hand's recall button can pull a held bottle away.");
                        grabbable.ProcessPointerEvent(new Oculus.Interaction.PointerEvent(917, Oculus.Interaction.PointerEventType.Cancel, pose));
                        panel.Tick(.1f);
                        Assert.That(grabbable.transform.localPosition, Is.EqualTo(new Vector3(-280, 15, -160)));
                    }
                    if (panel.Session.Step.mode == "evidence") for (int card = 0; card < 3; card++) { Confirm("EvidenceTab" + card); TextFits(); }
                    if (panel.Session.Step.mode == "sequence")
                    {
                        foreach (int card in panel.Session.Step.sequenceOrder.Reverse()) Confirm("ProcessCard" + card);
                        Confirm("SubmitCourse"); Assert.That(panel.Session.LastIncorrect, Is.True); TextFits();
                        foreach (int card in panel.Session.Sequence.ToArray()) Confirm("ProcessCard" + card);
                        Assert.That(Find("SubmitCourse").interactable, Is.False);
                        foreach (int card in panel.Session.Step.sequenceOrder) Confirm("ProcessCard" + card);
                        Confirm("SubmitCourse");
                    }
                    else
                    {
                        Confirm("CourseChoice" + ((panel.Session.Step.correct + 1) % 3)); Confirm("SubmitCourse");
                        Assert.That(panel.Session.LastIncorrect, Is.True); TextFits();
                        Confirm("CourseChoice" + panel.Session.Step.correct); Confirm("SubmitCourse");
                    }
                    Assert.That(panel.Session.Phase, Is.EqualTo(ClinicalCoursePhase.Feedback)); TextFits();
                    Confirm("ContinueCourse");
                    Assert.That(board.position, Is.EqualTo(position), "State changes and hand/model actions cannot reposition the board.");
                }
                TextFits(); Confirm("ContinueCourse"); Assert.That(completed, Is.Zero);
                closeAllowed = true; Confirm("ContinueCourse"); Confirm("ContinueCourse"); Assert.That(completed, Is.EqualTo(1));
                panel.Present(SessionToken.CreateNew(), scene); panel.SetVisible(true);
                Confirm("ContinueCourse"); panel.Tick(9);
                Assert.That(root.GetComponentsInChildren<TMP_Text>().Any(t => t.name == "BriefFairyTip"), Is.False);
                while (panel.Session.Phase != ClinicalCoursePhase.Finished) Confirm("SkipCourse");
                Assert.That(panel.Session.CorrectCount, Is.Zero);
                Assert.That(root.GetComponentsInChildren<Button>(true).Any(b => b.name.Contains("Exit")), Is.False);
                Confirm("ContinueCourse"); Assert.That(completed, Is.EqualTo(2));
            }
            finally
            {
                hand.Dispose(); panel.Dispose(); gaze.Unconfigure(); Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(events);
            }
        }
    }
}
