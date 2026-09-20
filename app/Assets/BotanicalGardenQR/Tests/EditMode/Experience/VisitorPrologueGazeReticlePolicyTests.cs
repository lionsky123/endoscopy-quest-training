using System;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.Fairy.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.VisitorCoach.Frontend;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using BotanicalGardenQR.VisitorPrologue.Frontend;
using BotanicalGardenQR.VisitorPrologue.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Experience
{
    public sealed class VisitorPrologueGazeReticlePolicyTests
    {
        [TestCase(VisitorProloguePhase.Invitation, false)]
        [TestCase(VisitorProloguePhase.Arrival, false)]
        [TestCase(VisitorProloguePhase.Encounter, false)]
        [TestCase(VisitorProloguePhase.ExplorationIdle, false)]
        [TestCase(VisitorProloguePhase.Failed, false)]
        public void VrDialogueUsesHandsWithoutGazeReticle(VisitorProloguePhase phase, bool visible)
        {
            var state = new VisitorPrologueViewState(1, 1, phase, true, false);
            Assert.That(VisitorPrologueStartupBinding.ShouldShowGazeReticle(state), Is.EqualTo(visible));
            if (phase == VisitorProloguePhase.Encounter)
                Assert.That(VisitorPrologueStartupBinding.ResolveDialogueInput(state), Is.EqualTo(VisitorDialogueInputMode.HandPoke));
        }
        [Test]
        public void OldFallbackFactsCannotRestoreTheVrGazeReticle()
        {
            Assert.That(VisitorPrologueStartupBinding.ShouldShowGazeReticle(
                new VisitorPrologueViewState(1, 1, VisitorProloguePhase.Invitation, false, true)), Is.False);
            Assert.That(VisitorPrologueStartupBinding.ShouldShowGazeReticle(
                new VisitorPrologueViewState(1, 1, VisitorProloguePhase.Invitation, true, true)), Is.False);
        }
        [TestCase(VisitorDialogueIntentKind.ChoosePrimary)]
        [TestCase(VisitorDialogueIntentKind.ChooseSecondary)]
        public void EncounterOffersRealResponsesAndExitsOnlyAfterTheAgreement(VisitorDialogueIntentKind reply)
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>("Assets/BotanicalGardenQR/Content/Authoring/VisitorCoachTheme.asset");
            var prologueTheme = AssetDatabase.LoadAssetAtPath<VisitorPrologueThemeAsset>("Assets/BotanicalGardenQR/Content/Authoring/VisitorPrologueTheme.asset");
            var ui = AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>("Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset");
            var viewer = new GameObject("Encounter viewer"); viewer.AddComponent<Camera>();
            var root = UnityEngine.Object.Instantiate(theme.PresentationPrefab);
            var dialogue = root.GetComponent<VisitorCoachPresenter>();
            var invitation = viewer.AddComponent<VisitorProloguePresenter>();
            using var prologue = VisitorPrologueModuleFactory.Create();
            try
            {
                dialogue.Configure(viewer.transform, theme, ui.SharedFont, new Registry());
                var exits = 0;
                using var startup = new VisitorPrologueStartupBinding(prologue, invitation, dialogue,
                    () => FairyResult.Success(), () => exits++, _ => { }, prologueTheme);
                startup.Begin(); prologue.ReportHandAvailability(true);
                var epoch = prologue.CurrentState.Epoch;
                prologue.RequestInvitation(epoch, VisitorPrologueInputModality.PalmHold);
                Assert.That(dialogue.CurrentState, Is.Null);
                prologue.ReportBookOpened(epoch);
                Assert.That(dialogue.CurrentState.PageIndex, Is.Zero);
                Assert.That(dialogue.ConfirmForTest(Time.unscaledTime + 1f), Is.True);
                Assert.That(dialogue.CurrentState.PrimaryIntent, Is.EqualTo(VisitorDialogueIntentKind.ChoosePrimary));
                Assert.That(dialogue.CurrentState.SecondaryIntent, Is.EqualTo(VisitorDialogueIntentKind.ChooseSecondary));
                if (reply == VisitorDialogueIntentKind.ChoosePrimary) dialogue.ConfirmForTest(Time.unscaledTime + 2f);
                else dialogue.ReplayForTest(Time.unscaledTime + 2f);
                Assert.That(dialogue.CurrentState.Body, Is.EqualTo(reply == VisitorDialogueIntentKind.ChoosePrimary
                    ? prologueTheme.Copy.CuriousResponse : prologueTheme.Copy.CompanionResponse));
                Assert.That(exits, Is.Zero);
                dialogue.ConfirmForTest(Time.unscaledTime + 3f);
                Assert.That(exits, Is.Zero);
                dialogue.ConfirmForTest(Time.unscaledTime + 4f);
                Assert.That(exits, Is.EqualTo(1));
                Assert.That(prologue.CurrentState.IsExplorationReady, Is.True);
            }
            finally { dialogue.Dispose(); UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(viewer); }
        }
        sealed class Registry : IFrontendGazeSurfaceRegistry
        {
            public IDisposable SuspendPanelInput() => new Registration();
            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(Transform root, int priority, string label) => new Registration();
        }
        sealed class Registration : IFrontendGazeSurfaceRegistration
        { public bool IsFocused => false; public void Invalidate() { } public void Dispose() { } }
    }
}
