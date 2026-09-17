using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.Experience.Application;
using BotanicalGardenQR.Fairy.Backend;
using BotanicalGardenQR.Fairy.Contracts;
using BotanicalGardenQR.VisitorCoach.Contracts;
using BotanicalGardenQR.VisitorCoach.Runtime;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using BotanicalGardenQR.VisitorPrologue.Frontend;
using BotanicalGardenQR.VisitorPrologue.Runtime;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.VisitorCoach.Frontend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Bootstrap
{
    public sealed class VisitorCoachBindingTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void DialogueCompositionPreservesOneContextAcrossViewerMotionAndReplay(bool bindAfterPresent)
        {
            using var fixture = new DialogueFixture();
            var presenter = fixture.Presenter;
            var controller = fixture.Controller;
            var viewer = fixture.Viewer;
            var theme = fixture.Theme;
            var fairy = fixture.Fairy;
            VisitorDialogueFairyBinding binding = null;
            try
            {
                var context = new VisitorDialogueContextId("composition:first");
                if (!bindAfterPresent)
                    binding = new VisitorDialogueFairyBinding(presenter, fairy);
                presenter.Present(DialoguePage(context, 0));
                if (bindAfterPresent)
                    binding = new VisitorDialogueFairyBinding(presenter, fairy);
                Assert.That(controller.Cues, Is.Not.Empty, "A late subscriber must receive the active composition.");
                var originalStage = presenter.transform.position;
                var originalRotation = presenter.transform.rotation;
                var originalTarget = controller.Cues[controller.Cues.Count - 1].WorldPosition;
                var expectedTarget = viewer.transform.position + Vector3.forward * theme.ViewerDistance +
                                     Vector3.right * theme.FairyDialogueHorizontalOffset.x +
                                     Vector3.forward * theme.FairyDialogueHorizontalOffset.y;
                expectedTarget.y = 0f;
                Assert.That(Vector3.Distance(originalTarget, expectedTarget), Is.LessThan(0.0001f));

                viewer.transform.SetPositionAndRotation(new Vector3(2f, 1.8f, 3f), Quaternion.Euler(20f, 75f, 0f));
                presenter.Present(DialoguePage(context, 1));
                Assert.That(presenter.transform.position, Is.EqualTo(originalStage));
                Assert.That(presenter.transform.rotation, Is.EqualTo(originalRotation));
                Assert.That(Vector3.Distance(controller.Cues[controller.Cues.Count - 1].WorldPosition, originalTarget),
                    Is.LessThan(0.0001f), "Page changes must not independently resample the Fairy target.");

                presenter.Present(DialoguePage(context, 1, VisitorDialogueSurfaceMode.Replay));
                Assert.That(controller.Cues[controller.Cues.Count - 1].Kind, Is.EqualTo(FairyCompanionCueKind.Idle));
                presenter.Present(DialoguePage(context, 0));
                Assert.That(controller.Cues[controller.Cues.Count - 1].WorldPosition, Is.EqualTo(originalTarget));
                var replacement = new VisitorDialogueContextId("composition:replacement");
                presenter.Present(DialoguePage(replacement, 0));
                Assert.That(Vector3.Distance(presenter.transform.position, originalStage), Is.GreaterThan(1f));
                Assert.That(Vector3.Distance(controller.Cues[controller.Cues.Count - 1].WorldPosition, originalTarget),
                    Is.GreaterThan(1f));
                var cueCount = controller.Cues.Count;
                presenter.Hide(context);
                Assert.That(controller.Cues.Count, Is.EqualTo(cueCount), "An old context cannot release a new cue.");
                presenter.Dispose();
                Assert.That(controller.Cues[controller.Cues.Count - 1].Kind, Is.EqualTo(FairyCompanionCueKind.Idle));
                cueCount = controller.Cues.Count;
                binding.Dispose();
                Assert.That(controller.Cues.Count, Is.EqualTo(cueCount));
                Assert.That(controller.SpeechCalls, Is.Zero);
            }
            finally { binding?.Dispose(); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReentrantSurfaceReplacementPublishesOnlyTheCurrentComposition(bool duringCompositionEvent)
        {
            using var fixture = new DialogueFixture();
            var first = new VisitorDialogueContextId("composition:stale");
            var replacement = new VisitorDialogueContextId("composition:current");
            void Replace()
            {
                fixture.Viewer.transform.position = new Vector3(3f, 1.7f, 2f);
                fixture.Presenter.Present(DialoguePage(replacement, 0));
            }
            if (duringCompositionEvent)
                fixture.Presenter.CompositionChanged += composition =>
                {
                    if (composition?.Context == first) Replace();
                };
            else
                fixture.Presenter.SurfaceStateChanged += state =>
                {
                    if (state?.Context == first) Replace();
                };
            using var binding = new VisitorDialogueFairyBinding(fixture.Presenter, fixture.Fairy);
            fixture.Presenter.Present(DialoguePage(first, 0));
            Assert.That(fixture.Presenter.CurrentComposition.Context, Is.EqualTo(replacement));
            Assert.That(fixture.Controller.Cues, Is.Not.Empty);
            foreach (var cue in fixture.Controller.Cues)
            {
                Assert.That(cue.Kind, Is.EqualTo(FairyCompanionCueKind.DialogueFocus));
                Assert.That(cue.WorldPosition, Is.EqualTo(fixture.Presenter.CurrentComposition.FairyWorldPosition));
            }
            fixture.Presenter.Hide(replacement);
            Assert.That(fixture.Controller.Cues[fixture.Controller.Cues.Count - 1].Kind,
                Is.EqualTo(FairyCompanionCueKind.Idle));
            Assert.That(fixture.Presenter.CurrentComposition, Is.Null);
        }

        [Test]
        public void FailedFocusDoesNotClaimOwnershipAndLaterDialogueCanRetry()
        {
            using var fixture = new DialogueFixture();
            using var binding = new VisitorDialogueFairyBinding(fixture.Presenter, fixture.Fairy);
            var context = new VisitorDialogueContextId("composition:retry");
            fixture.Controller.CueResult = FairyResult.Failure(FairyFailureCode.NotVisible, "test.focus.failed");
            fixture.Presenter.Present(DialoguePage(context, 0));
            Assert.That(fixture.Presenter.CurrentState.Context, Is.EqualTo(context),
                "A Fairy failure must leave text presentation active.");
            fixture.Presenter.Present(DialoguePage(context, 0, VisitorDialogueSurfaceMode.Replay));
            Assert.That(fixture.Controller.Cues.Count, Is.EqualTo(1), "Failed focus must not trigger an owned Idle release.");
            fixture.Controller.CueResult = FairyResult.Success();
            fixture.Presenter.Present(DialoguePage(context, 0));
            fixture.Presenter.Hide(context);
            Assert.That(fixture.Controller.Cues[fixture.Controller.Cues.Count - 1].Kind,
                Is.EqualTo(FairyCompanionCueKind.Idle));
            var count = fixture.Controller.Cues.Count;
            binding.Dispose();
            fixture.Presenter.Present(DialoguePage(new VisitorDialogueContextId("composition:after-dispose"), 0));
            Assert.That(fixture.Controller.Cues.Count, Is.EqualTo(count));
        }

        sealed class DialogueFixture : IDisposable
        {
            readonly GameObject _fairyPrefab;
            readonly AudioClip _clip;
            readonly GameObject _stage;
            public VisitorCoachThemeAsset Theme { get; }
            public GameObject Viewer { get; }
            public VisitorCoachPresenter Presenter { get; }
            public RecordingFairyController Controller { get; } = new RecordingFairyController();
            public FairyCompanionBinding Fairy { get; }

            public DialogueFixture()
            {
                try
                {
                    Theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(
                        "Assets/BotanicalGardenQR/Content/Authoring/VisitorCoachTheme.asset");
                    var defaults = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>(
                        "Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset");
                    Viewer = new GameObject("DialogueCompositionViewer");
                    Viewer.AddComponent<Camera>().enabled = false;
                    Viewer.transform.position = new Vector3(0f, 1.6f, 0f);
                    _stage = UnityEngine.Object.Instantiate(Theme.PresentationPrefab);
                    Presenter = _stage.GetComponentInChildren<VisitorCoachPresenter>(true);
                    Presenter.Configure(Viewer.transform, Theme, defaults.SharedFont, new DialogueGazeRegistry());
                    Presenter.SetInputMode(VisitorDialogueInputMode.HeadGaze);
                    _fairyPrefab = new GameObject("DialogueCompositionFairy");
                    _fairyPrefab.AddComponent<ParticleSystem>();
                    _clip = AudioClip.Create("DialogueCompositionFeedback", 8, 1, 8000, false);
                    Fairy = new FairyCompanionBinding(Controller, new FairyDefinition(
                        _fairyPrefab, FairyBehavior.Guide, 1f, _fairyPrefab, _fairyPrefab,
                        new FairyCompanionFeedbackDefinition(
                            _fairyPrefab, Vector3.zero, 1f,
                            _clip, 0.5f, 0.5f, 1, _clip, 0.5f, 0.5f, 1,
                            _clip, 0.5f, 0.5f, 1, _clip, 0.5f, 0.5f, 1,
                            new[] { _clip }, 0.2f, 4f, 9f, 0.25f, 1), 0f, 0f));
                }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                Presenter?.Dispose();
                Fairy?.Dispose();
                if (_stage != null) UnityEngine.Object.DestroyImmediate(_stage);
                if (_clip != null) UnityEngine.Object.DestroyImmediate(_clip);
                if (_fairyPrefab != null) UnityEngine.Object.DestroyImmediate(_fairyPrefab);
                if (Viewer != null) UnityEngine.Object.DestroyImmediate(Viewer);
            }
        }

        static VisitorDialogueSurfaceState DialoguePage(
            VisitorDialogueContextId context, int page, VisitorDialogueSurfaceMode mode = VisitorDialogueSurfaceMode.Dialogue)
            => new VisitorDialogueSurfaceState(page + 1, context, VisitorDialogueOwner.Coach,
                mode, "探索教学", "小精灵", "请观察身边的植物。", page, 2);

        [Test]
        public void QrCapabilityRequiresACommittedNonRecallQrContentOpen()
        {
            using (var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default))
            {
                var coachSession = new VisitorCoachSessionId("visitor-a");
                var journey = JourneySessionId.CreateNew();
                var lifecycle = new RecordingContentLifecycleSource();
                coach.BeginSession(coachSession);
                using (var binding = new VisitorCoachQrBinding(
                           coach,
                           coachSession,
                           journey,
                           lifecycle))
                {
                    Assert.That(binding.BeginFirstScanOpportunity().Succeeded, Is.True);
                    Assert.That(binding.BeginFirstScanOpportunity().Succeeded, Is.True,
                        "Opening the Recognition gate twice must not duplicate the first-scan mission.");
                    Assert.That(coach.Advance(coachSession, 0f, false).Succeeded, Is.True);
                    Assert.That(coach.CurrentState.IsCueVisible, Is.True);
                    Assert.That(coach.CurrentState.Capability, Is.EqualTo(VisitorCoachCapability.QrConfirm));
                    Assert.That(coach.CurrentState.CueKey, Is.EqualTo(VisitorCoachCueKeys.QrConfirm));
                    Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.QrConfirm),
                        Is.EqualTo(VisitorCoachMastery.Prompted));

                    lifecycle.Publish(OpenFact(journey, new SourceKind("spatial_anchor"), isRecall: false));
                    lifecycle.Publish(OpenFact(journey, RecognitionSourceKinds.Qr, isRecall: true));
                    lifecycle.Publish(OpenFact(JourneySessionId.CreateNew(), RecognitionSourceKinds.Qr, isRecall: false));
                    Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.QrConfirm),
                        Is.EqualTo(VisitorCoachMastery.Prompted));
                    Assert.That(coach.CurrentState.IsCueVisible, Is.True);

                    lifecycle.Publish(OpenFact(journey, RecognitionSourceKinds.Qr, isRecall: false));
                    Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.QrConfirm),
                        Is.EqualTo(VisitorCoachMastery.Proven));
                    Assert.That(coach.CurrentState.IsCueVisible, Is.False);
                }

                Assert.That(lifecycle.HasSink, Is.False,
                    "Disposed Bootstrap bindings must not receive later content callbacks.");
            }
        }

        [Test]
        public void DisposingFirstScanBindingWithdrawsAnUnfinishedOpportunity()
        {
            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var coachSession = new VisitorCoachSessionId("visitor-a");
            var lifecycle = new RecordingContentLifecycleSource();
            coach.BeginSession(coachSession);
            coach.Advance(coachSession, 0f, false);

            using (var binding = new VisitorCoachQrBinding(
                       coach,
                       coachSession,
                       JourneySessionId.CreateNew(),
                       lifecycle))
            {
                binding.BeginFirstScanOpportunity();
                Assert.That(coach.CurrentState.IsCueVisible, Is.True);
            }

            Assert.That(coach.CurrentState.IsCueVisible, Is.False);
            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.QrConfirm),
                Is.EqualTo(VisitorCoachMastery.Prompted),
                "Ending a cue must never forge proof of QR mastery.");
        }

        [Test]
        public void PalmRecallProofDoesNotCreateAnAutomaticTeachingOpportunity()
        {
            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var session = new VisitorCoachSessionId("visitor-atlas-a");
            var hub = new RecordingAtlasHubController();
            coach.BeginSession(session);
            coach.Advance(session, 0f, false);

            using var binding = new VisitorCoachAtlasHubBinding(coach, session, hub);
            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.PalmRecall),
                Is.EqualTo(VisitorCoachMastery.Unknown));

            hub.PublishPalmStage(VisitorAtlasHubPalmStage.TurnPalmUp);
            Assert.That(binding.LastObservedPalmStage, Is.EqualTo(VisitorAtlasHubPalmStage.TurnPalmUp));
            coach.ReportProven(session, VisitorCoachCapability.ArtifactPlace);
            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.PalmRecall),
                Is.EqualTo(VisitorCoachMastery.Unknown));
            Assert.That(coach.CurrentState.IsCueVisible, Is.False,
                "Finishing collection must not start the old palm lesson.");

            hub.PublishSummoned();
            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.PalmRecall),
                Is.EqualTo(VisitorCoachMastery.Proven));
            Assert.That(coach.CurrentState.IsCueVisible, Is.False);
        }

        [Test]
        public void SelfSummonPreservesMasteryWithoutOpeningTeaching()
        {
            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var session = new VisitorCoachSessionId("visitor-atlas-b");
            var hub = new RecordingAtlasHubController();
            coach.BeginSession(session);
            coach.Advance(session, 0f, false);

            using var binding = new VisitorCoachAtlasHubBinding(coach, session, hub);
            hub.PublishSummoned();

            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.PalmRecall),
                Is.EqualTo(VisitorCoachMastery.Proven));
            Assert.That(coach.CurrentState.IsCueVisible, Is.False,
                "A visitor who already summoned the Hub must not be taught the same gesture later.");
        }

        [Test]
        public void ActualEncounterGazeInputProvesOnlyGazeAndNeverTheInvitationPalm()
        {
            using var fixture = new DialogueFixture();
            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var session = new VisitorCoachSessionId("encounter-proof"); coach.BeginSession(session);
            using var prologue = VisitorPrologueModuleFactory.Create();
            prologue.Begin(); prologue.ReportHandAvailability(true);
            var epoch = prologue.CurrentState.Epoch;
            prologue.RequestInvitation(epoch, VisitorPrologueInputModality.PalmHold);
            prologue.ReportBookOpened(epoch); prologue.ReportArrivalReady(epoch);
            var theme = AssetDatabase.LoadAssetAtPath<VisitorPrologueThemeAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/VisitorPrologueTheme.asset");
            var instance = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
            var presenter = instance.GetComponent<VisitorProloguePresenter>();
            try
            {
                presenter.Configure(fixture.Viewer.transform, new DialogueGazeRegistry(), theme);
                presenter.Bind(prologue);
                using var proof = new VisitorCoachPrologueBinding(coach, session, presenter);
                using var startup = new VisitorPrologueStartupBinding(prologue, presenter, fixture.Presenter,
                    () => FairyResult.Success(), () => { }, _ => { }, theme);
                Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.GazeDwell), Is.EqualTo(VisitorCoachMastery.Unknown));
                fixture.Presenter.Tick(20f);
                Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.GazeDwell), Is.EqualTo(VisitorCoachMastery.Unknown));
                Assert.That(fixture.Presenter.ConfirmForTest(float.MaxValue), Is.True);
                Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.GazeDwell), Is.EqualTo(VisitorCoachMastery.Proven));
                Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.Poke), Is.EqualTo(VisitorCoachMastery.Unknown));
                Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.PalmRecall), Is.EqualTo(VisitorCoachMastery.Unknown));
            }
            finally { presenter.Dispose(); UnityEngine.Object.DestroyImmediate(instance); }
        }

        [Test]
        public void ToolDialogueWaitsForRealSummonAndDismissBeforeExplicitDeparture()
        {
            using var fixture = new DialogueFixture();
            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var session = new VisitorCoachSessionId("tool-preparation");
            coach.BeginSession(session);
            var hub = new RecordingAtlasHubController();
            using var proof = new VisitorCoachAtlasHubBinding(coach, session, hub);
            using var preparation = new VisitorToolPreparation();
            var requests = 0;
            using var binding = new VisitorToolPreparationBinding(preparation, hub,
                fixture.Presenter, fixture.Theme, session, () => requests++);
            binding.Begin();
            Assert.That(fixture.Presenter.CurrentOwner, Is.EqualTo(VisitorDialogueOwner.ToolPreparation));
            Assert.That(fixture.Presenter.BodyFits, Is.True);
            fixture.Presenter.Tick(90f);
            Assert.That(preparation.IsExplanationOpen, Is.True);
            Assert.That(requests, Is.Zero);
            Assert.That(fixture.Presenter.ConfirmForTest(float.MaxValue), Is.True);
            Assert.That(preparation.HasExited, Is.False);
            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.PalmRecall), Is.EqualTo(VisitorCoachMastery.Unknown));
            Assert.That(fixture.Presenter.CurrentState.Mode, Is.EqualTo(VisitorDialogueSurfaceMode.Replay));
            Assert.That(fixture.Presenter.ConfirmForTest(float.MaxValue), Is.False);
            hub.PublishSummoned();
            Assert.That(preparation.Phase, Is.EqualTo(VisitorToolPreparationPhase.AwaitingDismiss));
            Assert.That(requests, Is.Zero);
            hub.Hide();
            Assert.That(fixture.Presenter.BodyFits, Is.True);
            Assert.That(fixture.Presenter.ConfirmForTest(float.MaxValue), Is.True);
            Assert.That(preparation.HasExited, Is.True);
            Assert.That(requests, Is.EqualTo(1));
            binding.Begin();
            hub.PublishSummoned();
            hub.Hide();
            Assert.That(coach.CurrentState.GetMastery(VisitorCoachCapability.PalmRecall), Is.EqualTo(VisitorCoachMastery.Proven));
            Assert.That(requests, Is.EqualTo(1));
            binding.Replay();
            Assert.That(preparation.HasExited, Is.True);
            Assert.That(fixture.Presenter.ConfirmForTest(float.MaxValue), Is.True);
            Assert.That(fixture.Presenter.CurrentState, Is.Null);
            Assert.That(requests, Is.EqualTo(1));
        }

        [Test]
        public void ToolPracticeCanBeDeferredWithoutPretendingThePalmWorked()
        {
            using var fixture = new DialogueFixture();
            using var preparation = new VisitorToolPreparation();
            var hub = new RecordingAtlasHubController();
            var departures = 0;
            using var binding = new VisitorToolPreparationBinding(preparation, hub, fixture.Presenter,
                fixture.Theme, new VisitorCoachSessionId("practice-defer"), () => departures++);
            binding.Begin();
            hub.PublishSummoned(); // A stale/out-of-context event while reading is not a practice result.
            Assert.That(preparation.HasSummoned, Is.False);
            hub.Hide();
            fixture.Presenter.ConfirmForTest(float.MaxValue);
            Assert.That(preparation.IsPracticing, Is.True);
            Assert.That(fixture.Presenter.DeferForTest(float.MaxValue), Is.True);
            Assert.That(preparation.HasSummoned, Is.False);
            Assert.That(preparation.HasExited, Is.False);
            Assert.That(fixture.Presenter.BodyFits, Is.True);
            fixture.Presenter.ConfirmForTest(float.MaxValue);
            Assert.That(departures, Is.EqualTo(1));
            hub.PublishSummoned(); hub.Hide();
            Assert.That(departures, Is.EqualTo(1));
        }

        [Test]
        public void FairyAttentionIsVisualOnlyAndNeverInvokesTutorialSpeech()
        {
            var sourceIndependentCue = FairyCompanionCue.CoachAttention(3f);
            Assert.That(sourceIndependentCue.WorldPosition, Is.EqualTo(Vector3.zero));
            var dialogueTarget = new Vector3(-0.35f, 1.32f, 0.68f);
            var dialogueCue = FairyCompanionCue.DialogueFocus(dialogueTarget);
            Assert.That(dialogueCue.WorldPosition, Is.EqualTo(dialogueTarget));

            using var coach = VisitorCoachModuleFactory.Create(VisitorCoachTiming.Default);
            var session = new VisitorCoachSessionId("visitor-a");
            var prefab = new GameObject("CoachFairyPrefab");
            var arrival = new GameObject("CoachFairyArrival");
            var mask = new GameObject("CoachFairyMask");
            var clip = AudioClip.Create("CoachFairyFeedback", 8, 1, 8000, false);
            var effect = new GameObject("CoachFairyCompanionEffect");
            effect.AddComponent<ParticleSystem>();
            var controller = new RecordingFairyController();
            FairyCompanionBinding fairy = null;
            try
            {
                fairy = new FairyCompanionBinding(
                    controller,
                    new FairyDefinition(
                        prefab,
                        FairyBehavior.Guide,
                        1f,
                        arrival,
                        mask,
                        new FairyCompanionFeedbackDefinition(
                            effect,
                            new Vector3(0f, 0.4f, 0f),
                            1f,
                            clip,
                            0.55f,
                            0.55f,
                            6,
                            clip,
                            0.3f,
                            0.35f,
                            4,
                            clip,
                            0.6f,
                            0.65f,
                            8,
                            clip,
                            0.72f,
                            1.2f,
                            12,
                            new[] { clip },
                            0.16f,
                            4f,
                            9f,
                            0.25f,
                            2),
                        0f,
                        0f));

                coach.BeginSession(session);
                coach.Advance(session, 0f, false);
                using var binding = new VisitorCoachFairyBinding(coach, session, fairy, 3f);
                var opportunity = new VisitorCoachOpportunity(
                    session,
                    new VisitorCoachOpportunityId("first-qr"),
                    VisitorCoachCapability.QrConfirm,
                    VisitorCoachCueKeys.QrConfirm,
                    100,
                    3);

                coach.OfferOpportunity(opportunity);
                coach.Advance(session, 4f, false);
                coach.Advance(session, 4f, false);
                coach.Advance(session, 0f, true);
                coach.Advance(session, 0f, false);

                Assert.That(controller.Cues.Count, Is.EqualTo(1),
                    "Escalation and modal restoration must not replay the Fairy attention animation.");
                Assert.That(controller.Cues[0].Kind, Is.EqualTo(FairyCompanionCueKind.CoachAttention));
                Assert.That(controller.SpeechCalls, Is.Zero,
                    "Text-first tutorial bindings must never call Fairy speech.");

                coach.OfferOpportunity(new VisitorCoachOpportunity(
                    session,
                    new VisitorCoachOpportunityId("artifact-grab"),
                    VisitorCoachCapability.ArtifactGrab,
                    VisitorCoachCueKeys.ArtifactGrab,
                    200,
                    2));
                Assert.That(controller.SpeechCalls, Is.Zero);
            }
            finally
            {
                fairy?.Dispose();
                UnityEngine.Object.DestroyImmediate(effect);
                UnityEngine.Object.DestroyImmediate(clip);
                UnityEngine.Object.DestroyImmediate(mask);
                UnityEngine.Object.DestroyImmediate(arrival);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        static ContentOpenedFact OpenFact(
            JourneySessionId journey,
            SourceKind source,
            bool isRecall)
            => new ContentOpenedFact(
                SessionToken.CreateNew(),
                journey,
                new SceneId("scene_coach_test"),
                source,
                "route-coach-test",
                isRecall);

        sealed class RecordingContentLifecycleSource : IContentLifecycleSource
        {
            IContentLifecycleSink _sink;
            public bool HasSink => _sink != null;

            public IDisposable Observe(IContentLifecycleSink sink)
            {
                _sink = sink ?? throw new ArgumentNullException(nameof(sink));
                return new CallbackRegistration(() => _sink = null);
            }

            public void Publish(ContentOpenedFact fact) => _sink?.OnContentOpened(fact);
        }

        sealed class RecordingAtlasHubController : IVisitorAtlasHubController
        {
            public VisitorAtlasHubPhase Phase { get; private set; } = VisitorAtlasHubPhase.ReadyHidden;
            public bool MapRequested => false;
            public bool IsMapVisible => false;
            public void SetMapSuppressed(bool suppressed) { }
            public VisitorAtlasHubBookState BookState => VisitorAtlasHubBookState.ClosedInteractive;
            public bool InteractionGateOpen => true;
            public bool IsVisible => Phase == VisitorAtlasHubPhase.Visible;
            public event Action<VisitorAtlasHubPhase> PhaseChanged;
            public event Action<VisitorAtlasHubPalmStage> PalmStageChanged;
            public event Action Summoned;
            public event Action BookSelected { add { } remove { } }
            public event Action<int> OpenCollectionRequested { add { } remove { } }
            public event Action Hidden;
            public event Action<VisitorAtlasHubFailure> Failed { add { } remove { } }

            public void PublishPalmStage(VisitorAtlasHubPalmStage stage) => PalmStageChanged?.Invoke(stage);

            public void PublishSummoned()
            {
                Phase = VisitorAtlasHubPhase.Visible;
                PhaseChanged?.Invoke(Phase);
                Summoned?.Invoke();
            }

            public void SetInteractionGate(bool open) { }
            public void Tick(float unscaledDeltaSeconds) { }
            public bool TryBeginBookOpening(out int generation) { generation = 0; return false; }
            public void ConfirmCollectionOpened(int generation) { }
            public void CancelBookOpening(int generation = 0) { }
            public void NotifyCollectionClosed() { }
            public void RejectBookSelection() { }
            public void Hide() { if (!IsVisible) return; Phase = VisitorAtlasHubPhase.ReadyHidden; Hidden?.Invoke(); }
            public void Dispose() { }
        }

        sealed class CallbackRegistration : IDisposable
        {
            Action _dispose;
            public CallbackRegistration(Action dispose) => _dispose = dispose;
            public void Dispose()
            {
                var dispose = _dispose;
                _dispose = null;
                dispose?.Invoke();
            }
        }

        sealed class RecordingFairyController : IFairyController
        {
            public System.Collections.Generic.List<FairyCompanionCue> Cues { get; } =
                new System.Collections.Generic.List<FairyCompanionCue>();
            public FairyResult CueResult { get; set; } = FairyResult.Success();
            public int SpeechCalls { get; private set; }
            public AudioClip LastSpeechClip { get; private set; }
            public Guid LastSpeechRequest { get; private set; }
            public System.Collections.Generic.List<Guid> CancelledSpeechRequests { get; } = new();
            readonly System.Collections.Generic.Dictionary<Guid, Action<FairyResult>> _speechCompletions = new();

            public FairyResult Open(SessionToken session, FairyDefinition definition) => FairyResult.Success();
            public FairyResult Dispatch(SessionToken session, FairyIntent intent, UnityEngine.Vector3? arrivalOrigin = null) => FairyResult.Success();
            public FairyResult Speak(SessionToken session, FairySpeech speech, Action<FairyResult> completion = null)
            {
                SpeechCalls++;
                LastSpeechClip = speech.Clip;
                LastSpeechRequest = speech.RequestId;
                if (completion != null) _speechCompletions[speech.RequestId] = completion;
                return FairyResult.Success();
            }
            public void CompleteSpeech(Guid requestId, FairyResult result)
            {
                if (!_speechCompletions.TryGetValue(requestId, out var completion)) return;
                _speechCompletions.Remove(requestId);
                completion(result);
            }
            public FairyResult PresentCompanionCue(SessionToken session, FairyCompanionCue cue)
            {
                Cues.Add(cue);
                return CueResult;
            }
            public FairyResult CancelSpeech(SessionToken session, Guid requestId)
            {
                CancelledSpeechRequests.Add(requestId);
                return FairyResult.Success();
            }
            public FairyResult SetAmbientAudioSuppressed(SessionToken session, bool suppressed)
                => FairyResult.Success();
            public FairyResult Close(SessionToken session) => FairyResult.Success();
            public IDisposable Observe(IFairyStateSink sink) => EmptyDisposable.Instance;
        }

        sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new EmptyDisposable();
            public void Dispose() { }
        }

        sealed class DialogueGazeRegistry : BotanicalGardenQR.FrontendShell.Contracts.IFrontendGazeSurfaceRegistry
        {
            public System.IDisposable SuspendPanelInput() => new Suspension();
            sealed class Suspension : System.IDisposable { public void Dispose() { } }
            public BotanicalGardenQR.FrontendShell.Contracts.IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform root, int priority, string label)
                => new Registration();
            sealed class Registration : BotanicalGardenQR.FrontendShell.Contracts.IFrontendGazeSurfaceRegistration
            {
                public bool IsFocused => false;
                public void Invalidate() { }
                public void Dispose() { }
            }
        }
    }
}
