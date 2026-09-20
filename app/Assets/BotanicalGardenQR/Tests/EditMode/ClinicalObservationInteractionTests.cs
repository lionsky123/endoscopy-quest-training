using System;
using System.Linq;
using System.Reflection;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.Panorama.Frontend;
using NUnit.Framework;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalObservationInteractionTests
    {
        GameObject _root, _viewer, _events;
        HeadGazeDwellController _gaze;
        PanoramaFrontend _frontend;
        ClinicalEvidenceLesson _lesson;
        float _yaw;
        int _completed;
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>("Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset").SharedFont;
        [SetUp] public void SetUp()
        {
            _root = new GameObject("Guided observation test"); _viewer = new GameObject("Viewer", typeof(Camera));
            _viewer.transform.position = new Vector3(0, 1.6f, 0);
            _events = new GameObject("Events", typeof(EventSystem)); _gaze = _events.AddComponent<HeadGazeDwellController>();
            _gaze.Configure(_viewer.GetComponent<Camera>(), _events.GetComponent<EventSystem>()); _gaze.SetHandOnly(true);
            _completed = 0;
        }
        void Open(bool formal = true)
        {
            var resolver = new PublishedSceneResolver(AssetDatabase.LoadAssetAtPath<ContentSceneLibrary>("Assets/BotanicalGardenQR/Content/Published/ContentSceneLibrary.asset"));
            ((IPanoramaDefinitionSource)resolver).TryGet(new SceneId("giant_saguaro"), out var definition);
            _yaw = definition.InitialYawDegrees;
            _lesson = JsonUtility.FromJson<ClinicalEvidenceLesson>(Resources.Load<TextAsset>("ClinicalEvidence/lesson").text);
            _frontend = _root.AddComponent<PanoramaFrontend>();
            _frontend.Bind(SessionToken.CreateNew(), _gaze, _viewer.transform, _root.transform, Font,
                () => Assert.Fail("Use the existing completion path, not exit."),
                definition.EnvironmentMoments, false, null, true, definition.Source.Texture,
                definition.TeachingComparisons, () => _completed++, definition.InitialYawDegrees);
            _frontend.SetVisible(true);
            if (formal)
            {
                using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
                hand.Touch(ActionButton("开始练习")); Tick(.02f);
                hand.Touch(ActionButton("完成教程")); Tick(.02f);
                hand.Touch(ActionButton("开始正式观察")); Tick(.02f);
            }
        }
        object Controls
        {
            get { var world = typeof(PanoramaFrontend).GetField("_spatialWorld", Flags).GetValue(_frontend); return world.GetType().GetField("_clinicalControls", Flags).GetValue(world); }
        }
        void Tick(float seconds) => Controls.GetType().GetMethod("Tick").Invoke(Controls, new object[] { seconds });
        void Frames(float seconds)
        {
            for (float left = seconds; left > .0001f; left -= .02f) { Token.Tick(.02f); Tick(.02f); }
        }
        int Count(string name) => (int)Controls.GetType().GetProperty(name, Flags).GetValue(Controls);
        bool Explaining => (bool)Controls.GetType().GetProperty("Explaining", Flags).GetValue(Controls);
        float Progress => (float)Controls.GetType().GetProperty("ObservationProgress", Flags).GetValue(Controls);
        ClinicalObservationToken Token => _root.GetComponentInChildren<ClinicalObservationToken>(true);
        Material Lens => Token.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Single(m => m.name == "InspectionMagnifier_LivePanorama");
        Button Button(string name) => _root.GetComponentsInChildren<Button>().Single(b => b.name == name);
        string Phase => (string)Controls.GetType().GetProperty("CurrentPhase", Flags).GetValue(Controls);
        Button ActionButton(string label) => _root.GetComponentsInChildren<Button>()
            .Single(b => b.GetComponentInChildren<TMP_Text>()?.text == label);
        Vector3 Target => ClinicalEvidenceSession.PanoramaDirection(_lesson.topics[Count("TopicIndex")].panoramaUv, _yaw);
        [TearDown] public void TearDown()
        {
            if (_frontend) _frontend.Unbind(); _gaze.Unconfigure();
            Object.DestroyImmediate(_root); Object.DestroyImmediate(_viewer); Object.DestroyImmediate(_events);
        }

        internal static Grabbable BeginHold(ClinicalObservationToken token)
        {
            var grab = token.GetComponent<Grabbable>();
            Assert.That(token.GetComponent<HandGrabInteractable>(), Is.Not.Null);
            if (!(bool)typeof(Grabbable).GetField("_started", Flags).GetValue(grab))
            { typeof(Grabbable).GetMethod("Awake", Flags).Invoke(grab, null); typeof(Grabbable).GetMethod("Start", Flags).Invoke(grab, null); }
            if (!token.GetComponent<RigidbodyKinematicLocker>())
            {
                var locker = token.gameObject.AddComponent<RigidbodyKinematicLocker>();
                typeof(RigidbodyKinematicLocker).GetMethod("Awake", Flags).Invoke(locker, null);
            }
            var pose = new Pose(token.transform.position, token.transform.rotation);
            grab.ProcessPointerEvent(new PointerEvent(731, PointerEventType.Hover, pose));
            grab.ProcessPointerEvent(new PointerEvent(731, PointerEventType.Select, pose));
            return grab;
        }
        internal static void EndHold(ClinicalObservationToken token, bool cancel = false)
            => token.GetComponent<Grabbable>().ProcessPointerEvent(new PointerEvent(731,
                cancel ? PointerEventType.Cancel : PointerEventType.Unselect, new Pose(token.transform.position, token.transform.rotation)));
        // Real SDK pointer events, not lesson methods or button callbacks.
        internal static void LiftAndRelease(ClinicalObservationToken token)
        {
            var grab = BeginHold(token);
            var pose = new Pose(token.transform.position + Vector3.up * .06f, token.transform.rotation);
            grab.ProcessPointerEvent(new PointerEvent(731, PointerEventType.Move, pose));
            EndHold(token);
        }
        internal static void MoveLens(ClinicalObservationToken token, Transform viewer, Vector3 ray, float distance = .32f, bool edgeOn = false)
        {
            var localCentre = token.transform.InverseTransformPoint(token.LensCenter);
            var localNormal = token.transform.InverseTransformDirection(token.LensNormal);
            if (Vector3.Dot(localNormal, Vector3.forward) < 0) localNormal = -localNormal;
            var rotation = Quaternion.LookRotation(ray) * Quaternion.FromToRotation(localNormal, Vector3.forward);
            if (edgeOn) rotation *= Quaternion.Euler(0, 90, 0);
            var position = viewer.position + ray.normalized * distance - rotation * localCentre;
            token.GetComponent<Grabbable>().ProcessPointerEvent(new PointerEvent(731, PointerEventType.Move, new Pose(position, rotation)));
        }
        [Test] public void LiveObservationHasALargeFixedWindowAndKeepsLastFrameAfterRelease()
        {
            Open(); _viewer.transform.rotation = Quaternion.LookRotation(Target);
            BeginHold(Token); MoveLens(Token, _viewer.transform, Target); Frames(.1f);
            var preview = _root.GetComponentsInChildren<RawImage>().SingleOrDefault(i => i.name == "LiveObservationView");
            Assert.That(preview, Is.Not.Null, "A small lens alone cannot show the observation region.");
            Assert.That(preview.rectTransform.rect.width * preview.transform.lossyScale.x, Is.GreaterThanOrEqualTo(.7f));
            var position = preview.transform.position;
            var center = preview.material.GetVector("_ViewCenter");
            MoveLens(Token, _viewer.transform, Quaternion.Euler(0, 6, 0) * Target); Frames(.04f);
            Assert.That(Vector4.Distance(preview.material.GetVector("_ViewCenter"), center), Is.GreaterThan(.005f));
            center = preview.material.GetVector("_ViewCenter");
            EndHold(Token); Frames(.1f);
            Assert.That(preview.gameObject.activeInHierarchy, Is.True);
            Assert.That(preview.transform.position, Is.EqualTo(position));
            Assert.That(preview.material.GetVector("_ViewCenter"), Is.EqualTo(center));
            Assert.That(Progress, Is.Zero, "A retained frame cannot complete observation.");
        }

        [Test] public void CompletedObservationWaitsForExplicitPictureTeaching()
        {
            Open(); _viewer.transform.rotation = Quaternion.LookRotation(Target);
            BeginHold(Token); MoveLens(Token, _viewer.transform, Target); Frames(1.3f);
            Assert.That(Phase, Is.EqualTo("Observed"));
            Frames(20); Assert.That(Phase, Is.EqualTo("Observed"));
            Assert.That(_root.GetComponentsInChildren<RawImage>().Any(i => i.name == "ObservedScenePhoto"), Is.False);
            EndHold(Token); Frames(.1f);
            using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            hand.Touch(ActionButton("查看图解"));
            Assert.That(Explaining, Is.True);
            Assert.That(_root.GetComponentsInChildren<RawImage>().Any(i => i.name == "LiveObservationView"), Is.False);
            Assert.That(Count("CompletedCount"), Is.Zero);
        }

        [Test] public void NextTargetReplacesTheRetainedFrameAndHelpHidesTheView()
        {
            Open(); Observe();
            EndHold(Token); Frames(.04f);
            using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            hand.Touch(Button("ContinuePictureTeaching")); Frames(.02f);
            var preview = _root.GetComponentsInChildren<RawImage>().Single(i => i.name == "LiveObservationView");
            var uv = preview.material.GetVector("_ViewCenter");
            Assert.That(uv.x, Is.EqualTo(_lesson.topics[1].panoramaUv.x).Within(.0001f));
            Assert.That(uv.y, Is.EqualTo(_lesson.topics[1].panoramaUv.y).Within(.0001f));
            Assert.That(_root.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "LiveObservationStatus").text, Does.Contain("预览"));
            hand.Touch(Button("ObservationHelp")); Frames(.02f);
            Assert.That(preview.gameObject.activeInHierarchy, Is.False);
            hand.Touch(ActionButton("继续观察")); Frames(.02f);
            Assert.That(preview.gameObject.activeInHierarchy, Is.True);
            Assert.That(Count("CompletedCount"), Is.EqualTo(1));
        }

        void Observe()
        {
            _viewer.transform.rotation = Quaternion.LookRotation(Target);
            if (!Token.IsHeld) BeginHold(Token);
            MoveLens(Token, _viewer.transform, Target);
            Frames(1.3f);
            Assert.That(Phase, Is.EqualTo("Observed"));
            using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            hand.Touch(ActionButton("查看图解"));
            Assert.That(Explaining, Is.True, "Picture teaching follows explicit hand confirmation.");
        }
        void CheckCopy()
        {
            foreach (var text in _root.GetComponentsInChildren<TMP_Text>().Where(t => new[] { "DialogueBody", "ActionCue", "DirectionCue", "ObservationTargetLabel", "ObservationTask", "ObservationProgressStatus", "LiveObservationStatus", "PictureTeachingBody", "PictureTeachingTitle", "PictureTeachingGuide" }.Contains(t.name)))
                Assert.That(text.GetPreferredValues(text.text, text.rectTransform.rect.width, 0).y,
                    Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.name + ": " + text.text);
        }

        [Test] public void EntryUsesOneHandPokeAndDoesNotPretendTheToolIsAStartButton()
        {
            using var entry = new ClinicalLessonPanel(_root.transform, Font, _gaze, () => _completed++);
            entry.Present(null, null, false); entry.SetVisible(true);
            Assert.That(Button("StartObservation").interactable, Is.False);
            entry.Present(null, null, true);
            Assert.That(Token, Is.Null, "Only create the real observation tool inside the panorama.");
            CheckCopy();
            _gaze.TickInput(5); Assert.That(_completed, Is.Zero);
            using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            hand.Touch(Button("StartObservation")); Assert.That(_completed, Is.EqualTo(1));
        }

        [Test] public void ReleasingMagnifierDoesNotStandInForObservation()
        {
            Open(); LiftAndRelease(Token); Frames(2);
            Assert.That(Explaining, Is.False, "Grab/release away from the subject is not observation.");
            Assert.That(Count("CompletedCount"), Is.Zero);
            Assert.That(_root.GetComponentsInChildren<RawImage>(true).Any(i => i.name == "LiveObservationView"), Is.True,
                "A separate wide view complements the physical magnifier.");
        }

        [TestCase("not-held")] [TestCase("wrong-region")] [TestCase("edge-on")]
        [TestCase("too-close")] [TestCase("too-far")] [TestCase("looking-away")]
        public void LookingRequiresHeldLensInUsablePoseAtCurrentSubject(string invalid)
        {
            Open(); var target = Target; _viewer.transform.rotation = Quaternion.LookRotation(target);
            BeginHold(Token);
            MoveLens(Token, _viewer.transform, invalid == "wrong-region" ? Quaternion.Euler(0, 20, 0) * target : target,
                invalid == "too-close" ? .08f : invalid == "too-far" ? .9f : .32f, invalid == "edge-on");
            if (invalid == "not-held") EndHold(Token);
            if (invalid == "looking-away") _viewer.transform.rotation *= Quaternion.Euler(0, 70, 0);
            Frames(2);
            Assert.That(Explaining, Is.False); Assert.That(Progress, Is.Zero); Assert.That(_completed, Is.Zero);
            Assert.That(Lens.GetFloat("_Active"), Is.EqualTo(invalid == "wrong-region" ? 1 : 0),
                "An off-target but usable lens still magnifies; invalid viewing poses do not.");
        }

        [Test] public void InterruptedObservationMustStabilizeAgainAndDoesNotTeleportHeldTool()
        {
            Open(); var target = Target; _viewer.transform.rotation = Quaternion.LookRotation(target);
            BeginHold(Token); MoveLens(Token, _viewer.transform, target);
            Frames(.6f); Assert.That(Progress, Is.InRange(.45f, .55f)); Assert.That(Explaining, Is.False);
            MoveLens(Token, _viewer.transform, Quaternion.Euler(0, 25, 0) * target);
            Frames(.1f); Assert.That(Progress, Is.Zero);
            MoveLens(Token, _viewer.transform, target); Frames(.7f); Assert.That(Explaining, Is.False);
            var held = new Pose(Token.transform.position, Token.transform.rotation);
            Frames(.6f); Assert.That(Phase, Is.EqualTo("Observed")); Assert.That(Token.IsHeld, Is.True);
            Assert.That(Token.transform.position, Is.EqualTo(held.position)); Assert.That(Token.transform.rotation, Is.EqualTo(held.rotation));
            Assert.That(Count("CompletedCount"), Is.Zero, "Observation enables explanation; explicit hand confirmation advances.");
            CheckCopy();
        }

        [Test] public void LongFrameCannotInstantlyCompleteObservation()
        {
            Open(); _viewer.transform.rotation = Quaternion.LookRotation(Target);
            BeginHold(Token); MoveLens(Token, _viewer.transform, Target); Tick(30);
            Assert.That(Explaining, Is.False); Assert.That(Progress, Is.LessThan(.05f));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void ThreeObservationsCompleteOrSkipWithOneContinuousTool(int skipMask)
        {
            Open(); var original = Token; using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            for (int topic = 0; topic < 3; topic++)
            {
                Assert.That(Count("TopicIndex"), Is.EqualTo(topic));
                Assert.That(Token, Is.SameAs(original));
                if ((skipMask & (1 << topic)) != 0)
                {
                    Tick(25); hand.Touch(Button("SkipObservation"));
                }
                else
                {
                    Observe(); CheckCopy(); Tick(25); CheckCopy();
                    var held = new Pose(Token.transform.position, Token.transform.rotation);
                    hand.Touch(Button("ContinuePictureTeaching"));
                    Assert.That(Token.IsHeld, Is.EqualTo(topic < 2), "Keep the same grasp until explicit finish dismisses it.");
                    Assert.That(Token.transform.position, Is.EqualTo(held.position)); Assert.That(Token.transform.rotation, Is.EqualTo(held.rotation));
                }
                if (topic < 2) Assert.That(_completed, Is.Zero);
            }
            Assert.That(original.IsHeld, Is.False, "Finish must cancel the grasp without requiring manual release.");
            if (skipMask != 7) Assert.That(original.GetComponent<Grabbable>().SelectingPointsCount, Is.Zero);
            Assert.That(_completed, Is.EqualTo(1));
            Assert.That(Count("CompletedCount") + Count("SkippedCount"), Is.EqualTo(3));
            Assert.That(Count("SkippedCount"), Is.EqualTo(Enumerable.Range(0, 3).Count(i => (skipMask & (1 << i)) != 0)));
            Tick(30); Assert.That(_completed, Is.EqualTo(1));
        }

        [Test] public void ReleasingLensParksItBesideViewerAndFreesPanelHand()
        {
            Open(); Observe();
            var panel = Button("ContinuePictureTeaching").transform.position;
            Assert.That(Token.IsHeld, Is.True, "Opening picture teaching must retain the actual grasp.");
            var expectedRest = (Pose)Controls.GetType().GetMethod("RestPose", Flags).Invoke(Controls, null);
            EndHold(Token); Frames(.04f);
            Assert.That(Vector3.Distance(Token.transform.position, expectedRest.position), Is.LessThan(.001f), "Release must park at the current viewer's side.");
            Assert.That(Token.IsHeld, Is.False);
            Assert.That(Token.GetComponent<Grabbable>().SelectingPointsCount, Is.Zero);
            var horizontal = Vector3.ProjectOnPlane(_viewer.transform.forward, Vector3.up).normalized;
            var offset = Token.transform.position - _viewer.transform.position;
            Assert.That(Vector3.Dot(offset, Vector3.Cross(Vector3.up, horizontal)), Is.LessThan(-.2f));
            Assert.That(offset.y, Is.LessThan(-.2f));
            Assert.That(Lens.GetFloat("_Active"), Is.Zero);
            Assert.That(Button("ContinuePictureTeaching").transform.position, Is.EqualTo(panel));
            using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            hand.Touch(Button("ContinuePictureTeaching"));
            Assert.That(Count("CompletedCount"), Is.EqualTo(1));
            BeginHold(Token); Assert.That(Token.IsHeld, Is.True);
        }

        [Test] public void OnlyIndexPinchCanRetainTheMagnifier()
        {
            Open();
            var grab = Token.GetComponent<HandGrabInteractable>();
            Assert.That(grab.SupportedGrabTypes, Is.EqualTo(Oculus.Interaction.Grab.GrabTypeFlags.Pinch));
            var rules = grab.PinchGrabRules;
            Assert.That(rules[Oculus.Interaction.Input.HandFinger.Index], Is.EqualTo(Oculus.Interaction.GrabAPI.FingerRequirement.Required));
            foreach (var finger in new[] { Oculus.Interaction.Input.HandFinger.Thumb, Oculus.Interaction.Input.HandFinger.Middle,
                Oculus.Interaction.Input.HandFinger.Ring, Oculus.Interaction.Input.HandFinger.Pinky })
                Assert.That(rules[finger], Is.EqualTo(Oculus.Interaction.GrabAPI.FingerRequirement.Ignored));
        }

        [Test] public void HelpAndRecallDoNotAdvanceAndRecallCannotPullARealGrasp()
        {
            Open(); Tick(25); using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            hand.Touch(Button("ObservationHelp")); Tick(.2f); CheckCopy();
            hand.Touch(ActionButton("继续观察"));
            Assert.That(Count("TopicIndex"), Is.Zero); Assert.That(Explaining, Is.False);
            Tick(25); hand.Touch(Button("RecallMagnifier"));
            BeginHold(Token); MoveLens(Token, _viewer.transform, _viewer.transform.forward);
            var held = Token.transform.position;
            Assert.That(Token.TryRecall(new Pose(Vector3.zero, Quaternion.identity)), Is.False);
            Token.Tick(1); Assert.That(Token.transform.position, Is.EqualTo(held));
            Observe(); Tick(25);
            hand.Touch(Button("LookBackAtScene"));
            hand.Touch(Button("ObservationHelp")); Tick(.2f);
            var body = _root.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "PictureTeachingBody");
            Assert.That(body.text, Is.EqualTo(_lesson.topics[0].method)); CheckCopy();
            Assert.That(Token.IsHeld, Is.True); Assert.That(Count("CompletedCount"), Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void HiddenOrPausedTrackingCannotFinishPartialObservation(bool pause)
        {
            Open(); _viewer.transform.rotation = Quaternion.LookRotation(Target);
            BeginHold(Token); MoveLens(Token, _viewer.transform, Target); Frames(.6f);
            if (pause) typeof(ClinicalObservationToken).GetMethod("OnApplicationPause", Flags).Invoke(Token, new object[] { true });
            else _frontend.SetVisible(false);
            Tick(3);
            Assert.That(Explaining, Is.False); Assert.That(Token.IsHeld, Is.False); Assert.That(Lens.GetFloat("_Active"), Is.Zero);
            Assert.That(_completed, Is.Zero);
        }

        [Test] public void FixedSubjectAndDirectionsRemainDiscoverableThroughFullTurn()
        {
            Open(); Tick(25);
            var marker = _root.GetComponentsInChildren<Transform>().Single(t => t.name == "ObservationTarget");
            var markerPosition = marker.position; var markerRotation = marker.rotation;
            var guides = _root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("ObservationDirection")).ToArray();
            var positions = guides.Select(t => t.position).ToArray();
            Assert.That(Vector3.Angle(markerPosition - _viewer.transform.position, Target), Is.LessThan(.01f));
            for (int heading = 0; heading < 360; heading += 15)
            {
                _viewer.transform.rotation = Quaternion.Euler(0, heading, 0); Tick(.02f);
                var angle = Vector3.Angle(Vector3.ProjectOnPlane(Target, Vector3.up), _viewer.transform.forward);
                var cues = _root.GetComponentsInChildren<TMP_Text>().Where(t => t.name == "DirectionCue").ToArray();
                Assert.That(cues.Length, Is.EqualTo(angle > 30.01f ? 1 : 0));
                if (cues.Length > 0) Assert.That(cues[0].text, Does.Contain("隔断与门"));
                Assert.That(guides.Select(t => t.position), Is.EqualTo(positions)); CheckCopy();
            }
            _viewer.transform.position += Vector3.right * .1f; Tick(.02f);
            Assert.That(marker.position, Is.EqualTo(markerPosition)); Assert.That(marker.rotation, Is.EqualTo(markerRotation));
            var visibleCopy = string.Join("\n", _root.GetComponentsInChildren<TMP_Text>().Select(t => t.text));
            Assert.That(visibleCopy, Does.Contain("隔断与门").And.Contain("镜片"));
            Assert.That(Explaining, Is.False);
        }

        [Test] public void LiveLensUsesSourcedGlassGeometryAndTracksThePhysicalLens()
        {
            Open(); var shader = Resources.Load<Shader>("ClinicalPanoramaMagnifier");
            Assert.That(shader, Is.Not.Null); Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            Assert.That(Token.GetComponentsInChildren<MeshFilter>().Any(m => AssetDatabase.GetAssetPath(m.sharedMesh).EndsWith("InspectionMagnifier.fbx")), Is.True);
            Assert.That(Lens.shader, Is.SameAs(shader)); Assert.That(Lens.GetFloat("_Magnification"), Is.EqualTo(4));
            Assert.That(Lens.GetFloat("_PanoramaRadius"), Is.EqualTo(PanoramaDefinition.RadiusMetres));
            Assert.That(Lens.GetFloat("_YawRadians"), Is.EqualTo(_yaw * Mathf.Deg2Rad));
            _viewer.transform.rotation = Quaternion.LookRotation(Target); BeginHold(Token); MoveLens(Token, _viewer.transform, Target);
            Tick(.02f); var first = Token.LensCenter;
            var mesh = Token.GetComponentInChildren<MeshFilter>();
            Assert.That(Vector3.Distance(mesh.transform.TransformPoint(Lens.GetVector("_LensCenterOS")), Token.LensCenter), Is.LessThan(.0001f),
                "The GPU derives the optical centre from the same mesh transform, including late hand updates.");
            Assert.That(Lens.GetFloat("_Active"), Is.EqualTo(1));
            MoveLens(Token, _viewer.transform, Quaternion.Euler(0, 8, 0) * Target); Tick(.02f);
            Assert.That(Vector3.Distance(first, Token.LensCenter), Is.GreaterThan(.03f), "Sampling must move with the actual mesh, not a fixed topic image.");
            EndHold(Token); Tick(.02f); Assert.That(Lens.GetFloat("_Active"), Is.Zero);
        }

        [Test] public void ExplanationAndContinueStayVisibleBesideTheHeldHandle()
        {
            Open(); var camera = _viewer.GetComponent<Camera>(); camera.fieldOfView = 85; camera.aspect = 4f / 3f;
            Observe(); Canvas.ForceUpdateCanvases();
            var body = _root.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "PictureTeachingBody");
            var corners = new Vector3[4]; body.rectTransform.GetWorldCorners(corners);
            var projected = corners.Select(p => camera.WorldToViewportPoint(p)).ToArray();
            foreach (var p in projected) { Assert.That(p.x, Is.InRange(0f, 1f)); Assert.That(p.y, Is.InRange(0f, 1f)); }
            var handle = camera.WorldToViewportPoint(Token.transform.TransformPoint(Token.GetComponent<BoxCollider>().center));
            var box = Rect.MinMaxRect(projected.Min(p => p.x), projected.Min(p => p.y), projected.Max(p => p.x), projected.Max(p => p.y));
            Assert.That(box.Contains(handle), Is.False, "Do not place the method text behind the handle while preserving the grasp.");
            Tick(25); ((RectTransform)Button("ContinuePictureTeaching").transform).GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var p = camera.WorldToViewportPoint(corner);
                Assert.That(p.x, Is.InRange(0f, 1f)); Assert.That(p.y, Is.InRange(0f, 1f));
            }
        }


        [Test] public void PickingUpMagnifierStartsPracticeAndClearsStartPanel()
        {
            Open(false);
            BeginHold(Token); Tick(.02f);
            Assert.That(Phase, Is.EqualTo("Practice"), "Picking up the offered tool must start its tutorial.");
            Assert.That(_root.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "开始练习"), Is.False,
                "The start panel must disappear once the visitor is using the magnifier.");
            Assert.That(ActionButton("完成教程"), Is.Not.Null);
            var halo = _root.GetComponentsInChildren<ClinicalHaloGraphic>().Single().transform;
            var direction = (halo.position - _viewer.transform.position).normalized;
            _viewer.transform.rotation = Quaternion.LookRotation(direction);
            MoveLens(Token, _viewer.transform, direction); Frames(1.3f);
            Assert.That(Phase, Is.EqualTo("Ready"));
            Assert.That(Count("CompletedCount") + Count("SkippedCount"), Is.Zero);
        }

        [Test] public void PracticeAndFormalObservationHaveAnExplicitBoundary()
        {
            Open(false); CheckCopy(); Assert.That(Phase, Is.EqualTo("Tutorial"));
            var original = Token;
            Frames(3);
            Assert.That(Phase, Is.EqualTo("Tutorial"), "Waiting alone must not start practice.");
            Assert.That(Count("CompletedCount"), Is.Zero);
            using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            hand.Touch(ActionButton("开始练习"));
            var halo = _root.GetComponentsInChildren<ClinicalHaloGraphic>().Single();
            var direction = (halo.transform.position - _viewer.transform.position).normalized;
            _viewer.transform.rotation = Quaternion.LookRotation(direction);
            BeginHold(Token); MoveLens(Token, _viewer.transform, direction); Frames(.6f);
            CheckCopy();
            Assert.That(Progress, Is.InRange(.45f, .55f));
            var status = _root.GetComponentsInChildren<TMP_Text>().Single(t => t.name == "ObservationProgressStatus");
            Assert.That(status.text, Does.Contain("%").And.Contain("完成教程"));
            Frames(.7f); Assert.That(Phase, Is.EqualTo("Ready")); CheckCopy();
            Frames(30); Assert.That(Phase, Is.EqualTo("Ready"), "The visitor decides when formal observation starts.");
            Assert.That(Count("CompletedCount") + Count("SkippedCount"), Is.Zero);
            Assert.That(_root.GetComponentsInChildren<ClinicalHaloGraphic>(), Is.Empty);
            var position = Token.transform.position;
            hand.Touch(ActionButton("开始正式观察"));
            Assert.That(Phase, Is.EqualTo("Seeking")); Assert.That(Token, Is.SameAs(original));
            Assert.That(Token.IsHeld, Is.True); Assert.That(Token.transform.position, Is.EqualTo(position));
            Assert.That(Count("CompletedCount") + Count("SkippedCount"), Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void FinishTutorialButtonWorksWithoutACompletedScan(bool held)
        {
            Open(false);
            using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            hand.Touch(ActionButton("开始练习"));
            if (held) BeginHold(Token);
            Frames(.2f);
            var finish = _root.GetComponentsInChildren<Button>()
                .SingleOrDefault(b => b.GetComponentInChildren<TMP_Text>()?.text == "完成教程");
            Assert.That(finish, Is.Not.Null, "A stuck practice must always have an explicit finish action.");
            hand.Touch(finish);
            Assert.That(Phase, Is.EqualTo("Ready"));
            Assert.That(Count("CompletedCount") + Count("SkippedCount"), Is.Zero);
            Assert.That(Token.IsHeld, Is.EqualTo(held));
            hand.Touch(ActionButton("开始正式观察"));
            Assert.That(Phase, Is.EqualTo("Seeking"));
            Assert.That(Count("TopicIndex"), Is.Zero);
        }

        [Test] public void TutorialHelpAlsoProvidesAnExplicitFinishAction()
        {
            Open(false);
            using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            hand.Touch(ActionButton("开始练习"));
            hand.Touch(Button("ObservationHelp")); Tick(.2f); CheckCopy();
            hand.Touch(ActionButton("完成教程"));
            Assert.That(Phase, Is.EqualTo("Ready"));
            Assert.That(Count("CompletedCount") + Count("SkippedCount"), Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void PracticeDoesNotKeepProgressAfterReleaseOrSustainedMisalignment(bool release)
        {
            Open(false);
            using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            hand.Touch(ActionButton("开始练习"));
            var halo = _root.GetComponentsInChildren<ClinicalHaloGraphic>().Single().transform;
            var direction = (halo.position - _viewer.transform.position).normalized;
            _viewer.transform.rotation = Quaternion.LookRotation(direction);
            BeginHold(Token); MoveLens(Token, _viewer.transform, direction); Frames(.6f);
            Assert.That(Progress, Is.GreaterThan(.4f));
            if (release) EndHold(Token);
            else MoveLens(Token, _viewer.transform, Quaternion.Euler(0, 8, 0) * direction);
            Frames(release ? .02f : .3f);
            Assert.That(Progress, Is.Zero); Assert.That(Phase, Is.EqualTo("Practice"));
        }

        [Test] public void PracticeCanFinishWithBriefHandJitter()
        {
            Open(false);
            using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            hand.Touch(ActionButton("开始练习"));
            var halo = _root.GetComponentsInChildren<ClinicalHaloGraphic>().Single().transform;
            var direction = (halo.position - _viewer.transform.position).normalized;
            _viewer.transform.rotation = Quaternion.LookRotation(direction);
            BeginHold(Token);
            for (int burst = 0; burst < 4 && Phase == "Practice"; burst++)
            {
                MoveLens(Token, _viewer.transform, direction); Frames(.36f);
                if (Phase != "Practice") break;
                MoveLens(Token, _viewer.transform, Quaternion.Euler(0, 8, 0) * direction); Frames(.08f);
            }
            Assert.That(Phase, Is.EqualTo("Ready"), "Brief hand jitter must not restart the whole tutorial indefinitely.");
            Assert.That(Count("CompletedCount") + Count("SkippedCount"), Is.Zero);
        }

        [Test] public void SmallHaloAndPhysicalHitBoundaryAgreeEvenAfterLeaning()
        {
            Open();
            var halo = _root.GetComponentsInChildren<ClinicalHaloGraphic>().Single();
            Assert.That(halo.TransparentCenter, Is.True);
            Assert.That(halo.rectTransform.rect.width * halo.transform.lossyScale.x, Is.EqualTo(.32f).Within(.001f));
            var target = halo.transform;
            _viewer.transform.position += Vector3.right * .1f;
            BeginHold(Token);
            foreach (var radius in new[] { .15f, .11f })
            {
                var direction = (target.position + target.right * radius - _viewer.transform.position).normalized;
                _viewer.transform.rotation = Quaternion.LookRotation(direction);
                MoveLens(Token, _viewer.transform, direction); Frames(.6f);
                if (radius > .1296f) Assert.That(Progress, Is.Zero);
                else Assert.That(Progress, Is.GreaterThan(.4f));
            }
            var bar = _root.GetComponentsInChildren<RectTransform>().Single(t => t.name == "ObservationProgress");
            Assert.That(bar.rect.height, Is.EqualTo(24));
            Assert.That(bar.rect.width, Is.GreaterThan(160));
        }

        [Test] public void PictureTeachingWaitsForVisitorAndReopensAtItsFixedPose()
        {
            Open(); Observe();
            var picture = _root.GetComponentsInChildren<RawImage>().Single(t => t.name == "ObservedScenePhoto");
            var board = picture.transform.parent;
            var pose = new Pose(board.position, board.rotation);
            Assert.That(picture.texture, Is.Not.Null);
            Assert.That(picture.material.GetVector("_ViewCenter").x, Is.EqualTo(_lesson.topics[0].panoramaUv.x));
            Frames(30);
            Assert.That(Count("TopicIndex"), Is.Zero); Assert.That(Count("CompletedCount"), Is.Zero);
            Assert.That(picture.gameObject.activeInHierarchy, Is.True, "Image teaching must never time out.");
            using var hand = new ClinicalHandFixture(_root, _viewer.transform, _gaze);
            hand.Touch(Button("LookBackAtScene"));
            Assert.That(picture.gameObject.activeInHierarchy, Is.False);
            Assert.That(Token.IsHeld, Is.True);
            _viewer.transform.position += Vector3.right * .12f;
            hand.Touch(Button("ObservationHelp"));
            Assert.That(picture.gameObject.activeInHierarchy, Is.True);
            Assert.That(board.position, Is.EqualTo(pose.position)); Assert.That(board.rotation, Is.EqualTo(pose.rotation));
            Assert.That(Count("CompletedCount"), Is.Zero);
            CheckCopy();
        }

        [Test] public void LargerMagnifierKeepsWorldSizeAndHandleGripUnderScaledParent()
        {
            _root.transform.localScale = Vector3.one * .00065f;
            var token = ClinicalObservationToken.Create(_root.transform);
            token.Place(new Pose(Vector3.up, Quaternion.identity));
            var renderers = token.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Assert.That(Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z), Is.EqualTo(.22f).Within(.001f));
            Assert.That(Vector3.Distance(token.LensCenter, token.transform.position), Is.LessThan(.11f));
            var grip = token.GetComponent<BoxCollider>();
            Assert.That(grip.size.y, Is.EqualTo(.085f * 22 / 18).Within(.001f));
            Assert.That(grip.center.y + grip.size.y / 2, Is.LessThan(0));
            BeginHold(token); Assert.That(token.IsHeld, Is.True); EndHold(token); Assert.That(token.IsHeld, Is.False);
        }
    }
}
