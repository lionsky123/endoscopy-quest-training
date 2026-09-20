using System.Reflection;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.VisitorPrologue.Contracts;
using BotanicalGardenQR.VisitorPrologue.Frontend;
using BotanicalGardenQR.VisitorPrologue.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode.Experience
{
    public sealed class VisitorHandRecoveryNoticeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject _root, _viewer;
        VisitorHandReadinessAdapter _adapter;
        VisitorPrologueController _prologue;
        GameObject Notice => (GameObject)typeof(VisitorHandReadinessAdapter).GetField("_trackingNotice", Private).GetValue(_adapter);

        [SetUp] public void SetUp()
        {
            _root = new GameObject("Hand recovery test");
            _viewer = new GameObject("Viewer", typeof(Camera));
            _viewer.transform.position = Vector3.up * 1.6f;
            _adapter = _root.AddComponent<VisitorHandReadinessAdapter>();
            _prologue = VisitorPrologueModuleFactory.Create();
            Set("_prologue", _prologue);
            Set("_viewer", _viewer.transform);
            Set("_font", AssetDatabase.LoadAssetAtPath<GlobalUiDefaults>("Assets/BotanicalGardenQR/Content/Authoring/GlobalUiDefaults.asset").SharedFont);
            _prologue.Begin();
        }

        void Set(string name, object value) => typeof(VisitorHandReadinessAdapter).GetField(name, Private).SetValue(_adapter, value);
        // Samples enter after the SDK reliability check; this verifies UI lifetime,
        // not camera recognition or a simulated hand gesture.
        void Sample(bool reliable, float seconds) => typeof(VisitorHandReadinessAdapter).GetMethod("UpdateAvailability", Private).Invoke(_adapter, new object[] { reliable, seconds });
        void EnterEncounter()
        {
            Sample(true, .2f);
            var epoch = _prologue.CurrentState.Epoch;
            _prologue.RequestInvitation(epoch, VisitorPrologueInputModality.PalmHold);
            _prologue.ReportArrivalReady(epoch); _prologue.ReportBookOpened(epoch);
            _prologue.BeginDialogue(epoch, 4);
        }
        void EnterExploration()
        {
            EnterEncounter(); var epoch = _prologue.CurrentState.Epoch;
            _prologue.AdvanceDialogue(epoch, _prologue.CurrentState.Version);
            _prologue.ChooseReply(epoch, _prologue.CurrentState.Version, VisitorEncounterReply.PromiseToExplore);
            _prologue.AdvanceDialogue(epoch, _prologue.CurrentState.Version);
            _prologue.AdvanceDialogue(epoch, _prologue.CurrentState.Version);
            Assert.That(_prologue.CurrentState.IsExplorationReady, Is.True);
        }

        [Test] public void ExplorationKeepsHandMonitoringAndShowsOnlySustainedLoss()
        {
            EnterExploration();
            Assert.That(_adapter.enabled, Is.True, "Monitoring must survive the prologue.");
            Sample(false, 1.9f); Assert.That(Notice, Is.Null);
            Sample(false, .2f); Assert.That(Notice.activeSelf, Is.True);
            Assert.That(_prologue.CurrentState.HasReliableHand, Is.False);
            Assert.That(_prologue.CurrentState.GazeFallbackAvailable, Is.False);
            Assert.That(Notice.GetComponentsInChildren<Button>(), Is.Empty);
            var position = Notice.transform.position; var rotation = Notice.transform.rotation;
            _viewer.transform.position += Vector3.right;
            _viewer.transform.rotation = Quaternion.Euler(0, 50, 0);
            Sample(false, 5);
            Assert.That(Notice.transform.position, Is.EqualTo(position));
            Assert.That(Notice.transform.rotation, Is.EqualTo(rotation));
            Sample(true, .2f);
            Assert.That(Notice.activeSelf, Is.False);
            Assert.That(_prologue.CurrentState.HasReliableHand, Is.True);
            Sample(false, 1); Assert.That(Notice.activeSelf, Is.False, "A new loss has its own grace period.");
        }

        [Test] public void EncounterShowsRecoveryAndDisablingHidesIt()
        {
            EnterEncounter(); Sample(false, 2.1f);
            Assert.That(Notice.activeSelf, Is.True);
            _adapter.enabled = false;
            // EditMode does not drive normal MonoBehaviour lifecycle callbacks.
            typeof(VisitorHandReadinessAdapter).GetMethod("OnDisable", Private).Invoke(_adapter, null);
            Assert.That(Notice.activeSelf, Is.False);
            _adapter.Unconfigure(); Assert.That(Notice, Is.Null);
        }

        [Test] public void InvitationUsesExistingHandInstructionWithoutAnotherNotice()
        {
            Sample(false, 5);
            Assert.That(Notice, Is.Null);
            Assert.That(_prologue.CurrentState.GazeFallbackAvailable, Is.False);
        }

        [TearDown] public void TearDown()
        {
            _adapter.Unconfigure(); _prologue.Dispose();
            Object.DestroyImmediate(_root); Object.DestroyImmediate(_viewer);
        }
    }
}
