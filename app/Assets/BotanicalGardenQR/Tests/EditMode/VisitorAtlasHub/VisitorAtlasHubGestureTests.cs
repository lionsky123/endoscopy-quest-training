using BotanicalGardenQR.VisitorAtlasHub.Backend;
using BotanicalGardenQR.VisitorAtlasHub.Contracts;
using BotanicalGardenQR.VisitorAtlasHub.Frontend;
using NUnit.Framework;
using Oculus.Interaction.Input;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.VisitorAtlasHub
{
    public sealed class VisitorAtlasHubGestureTests
    {
        static readonly VisitorAtlasHubPalmSample Candidate =
            new VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage.ReadyToSummon, true);
        static readonly VisitorAtlasHubPalmSample Released =
            new VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage.TurnPalmUp, true);
        static readonly VisitorAtlasHubPalmSample TrackingLost =
            new VisitorAtlasHubPalmSample(VisitorAtlasHubPalmStage.NoReliableHand, false);

        [Test]
        public void StablePalmCommitsOnceAndRequiresObservedRelease()
        {
            var latch = new PalmSummonLatch(0.45f, 0.20f, 0.18f);

            Assert.That(latch.Advance(Candidate, 0.225f), Is.False);
            Assert.That(latch.Advance(Candidate, 0.225f), Is.True);
            Assert.That(latch.Advance(Candidate, 1f), Is.False);
            Assert.That(latch.Advance(Released, 0.10f), Is.False);
            Assert.That(latch.Advance(Candidate, 0.45f), Is.False);
            Assert.That(latch.Advance(Released, 0.18f), Is.False);
            Assert.That(latch.Advance(Candidate, 0.45f), Is.True);
        }

        [Test]
        public void ShortTrackingLossPreservesHoldButLongLossResetsIt()
        {
            var latch = new PalmSummonLatch(0.45f, 0.20f, 0.18f);

            Assert.That(latch.Advance(Candidate, 0.225f), Is.False);
            Assert.That(latch.Advance(TrackingLost, 0.10f), Is.False);
            Assert.That(latch.Advance(Candidate, 0.225f), Is.True);

            latch.Reset();
            Assert.That(latch.Advance(Candidate, 0.225f), Is.False);
            Assert.That(latch.Advance(TrackingLost, 0.21f), Is.False);
            Assert.That(latch.Advance(Candidate, 0.225f), Is.False);
        }

        [Test]
        public void PrimeHeldPalmRequiresReleaseBeforeFirstCommit()
        {
            var latch = new PalmSummonLatch(0.45f, 0.20f, 0.18f);
            latch.Prime(true);

            Assert.That(latch.Advance(Candidate, 1f), Is.False);
            Assert.That(latch.Advance(Released, 0.18f), Is.False);
            Assert.That(latch.Advance(Candidate, 0.45f), Is.True);
        }

        [Test]
        public void WaveLikeBriefPalmSegmentsNeverAccumulateAcrossReliableNonCandidateFrames()
        {
            var latch = new PalmSummonLatch(0.45f, 0.20f, 0.18f);

            Assert.That(latch.Advance(Candidate, 0.22f), Is.False);
            Assert.That(latch.Advance(Released, 0.02f), Is.False);
            Assert.That(latch.Advance(Candidate, 0.44f), Is.False,
                "A reliable non-candidate frame must reset the previous brief palm-up segment.");
            Assert.That(latch.Advance(Candidate, 0.01f), Is.True);
        }

        [TestCase(Handedness.Left)]
        [TestCase(Handedness.Right)]
        public void CandidateUsesPalmBasisReliabilityIndexPinchAndViewerDropOnly(Handedness handedness)
        {
            var localPalmar = handedness == Handedness.Left ? Constants.LeftPalmar : Constants.RightPalmar;
            var palmUp = Quaternion.FromToRotation(localPalmar, Vector3.up);
            var wrist = new Pose(new Vector3(0f, 1.30f, 0.35f), palmUp);

            var sample = AtlasHubPalmCandidateSource.Evaluate(
                connected: true,
                trackedDataValid: true,
                highConfidence: true,
                indexPinching: false,
                hasWristPose: true,
                handedness,
                wrist,
                viewerHeight: 1.60f,
                minimumHandDrop: 0.12f,
                minimumPalmUpAlignment: 0.75f);

            Assert.That(sample.IsCandidate, Is.True,
                "Middle/Ring/Pinky state and hand velocity are deliberately absent from the qualification contract.");
            Assert.That(AtlasHubPalmCandidateSource.Evaluate(
                true, true, true, true, true, handedness, wrist, 1.60f, 0.12f, 0.75f).IsCandidate,
                Is.False, "Index Pinch must reserve the hand for direct interaction.");
            Assert.That(AtlasHubPalmCandidateSource.Evaluate(
                true, true, true, true, true, handedness, wrist, 1.60f, 0.12f, 0.75f).HasReliableTracking,
                Is.True, "Pinch is a reliable non-candidate and must reset hold instead of receiving tracking grace.");
            Assert.That(AtlasHubPalmCandidateSource.Evaluate(
                true, true, true, false, true, handedness,
                new Pose(new Vector3(0f, 1.55f, 0.35f), palmUp),
                1.60f, 0.12f, 0.75f).Stage,
                Is.EqualTo(VisitorAtlasHubPalmStage.PositionAtChest));

            var belowNewThreshold = Quaternion.FromToRotation(
                localPalmar,
                new Vector3(Mathf.Sqrt(1f - 0.70f * 0.70f), 0.70f, 0f));
            Assert.That(AtlasHubPalmCandidateSource.Evaluate(
                true, true, true, false, true, handedness,
                new Pose(new Vector3(0f, 1.20f, 0.35f), belowNewThreshold),
                1.60f, 0.12f, 0.75f).Stage,
                Is.EqualTo(VisitorAtlasHubPalmStage.TurnPalmUp),
                "The old broad 0.45-style tilt must not qualify under the tightened threshold.");
        }
    }
}
