using System;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Collection.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Collection.Tests.EditMode
{
    public sealed class CollectionProgressControllerTests
    {
        [Test]
        public void OfferThenValidGrabCollectsExactlyOnceAndEmitsReward()
        {
            var artifact = new CollectionArtifactDefinition(
                "artifact:giant_saguaro",
                "巨人柱果实",
                "记录巨人柱的发现。",
                "植物",
                0,
                "slot:0");
            var catalog = new CollectionCatalog(
                new[] { artifact },
                new[] { new CollectionMilestoneDefinition("first", CollectionMilestoneKind.FirstCollection) });
            using (var progress = new CollectionProgressController())
            {
                var journey = JourneySessionId.CreateNew();
                progress.BeginSession(journey, catalog);
                var offer = progress.OfferArtifact(
                    Completed(journey, "giant_saguaro", "completion:1"),
                    artifact.ArtifactId);

                Assert.That(offer.Succeeded, Is.True);
                var available = progress.CurrentState.Artifacts[0];
                Assert.That(available.State, Is.EqualTo(CollectionArtifactStatus.Available));
                Assert.That(available.InstanceToken.IsValid, Is.True);

                var collect = progress.CollectArtifact(artifact.ArtifactId, available.InstanceToken);
                Assert.That(collect.Succeeded, Is.True);
                Assert.That(progress.CurrentState.CollectedCount, Is.EqualTo(1));
                Assert.That(progress.CurrentState.PendingPresentation.Kind, Is.EqualTo(CollectionPresentationKind.Reward));
                Assert.That(progress.CurrentState.UnlockedMilestones, Does.Contain("first"));
                Assert.That(progress.CollectArtifact(artifact.ArtifactId, available.InstanceToken).Succeeded, Is.False);
            }
        }

        [Test]
        public void WrongJourneyAndWrongInstanceDoNotCollect()
        {
            var artifact = new CollectionArtifactDefinition("artifact:a", "A", "A", "", 0, "slot:a");
            var catalog = new CollectionCatalog(new[] { artifact });
            using (var progress = new CollectionProgressController())
            {
                var journey = JourneySessionId.CreateNew();
                progress.BeginSession(journey, catalog);
                Assert.That(progress.OfferArtifact(Completed(JourneySessionId.CreateNew(), "giant_saguaro", "completion:wrong"), artifact.ArtifactId).Succeeded, Is.False);
                Assert.That(progress.OfferArtifact(Completed(journey, "giant_saguaro", "completion:right"), artifact.ArtifactId).Succeeded, Is.True);
                var token = progress.CurrentState.Artifacts[0].InstanceToken;
                Assert.That(progress.CollectArtifact(artifact.ArtifactId, CollectionInstanceToken.CreateNew()).Succeeded, Is.False);
                Assert.That(progress.CurrentState.CollectedCount, Is.EqualTo(0));
                Assert.That(progress.CollectArtifact(artifact.ArtifactId, token).Succeeded, Is.True);
            }
        }

        [Test]
        public void DirectGrantCollectsWithoutOfferPresentationAndIsIdempotent()
        {
            var artifact = new CollectionArtifactDefinition("artifact:a", "A", "A", "", 0, "slot:a");
            var catalog = new CollectionCatalog(
                new[] { artifact },
                new[] { new CollectionMilestoneDefinition("first", CollectionMilestoneKind.FirstCollection) });
            using (var progress = new CollectionProgressController())
            {
                var journey = JourneySessionId.CreateNew();
                progress.BeginSession(journey, catalog);

                var granted = progress.GrantArtifact(journey, artifact.ArtifactId);

                Assert.That(granted.Succeeded, Is.True);
                Assert.That(granted.PresentationId.IsValid, Is.False);
                Assert.That(progress.CurrentState.Artifacts[0].State, Is.EqualTo(CollectionArtifactStatus.Collected));
                Assert.That(progress.CurrentState.CollectedCount, Is.EqualTo(1));
                Assert.That(progress.CurrentState.PendingPresentation, Is.Null);
                Assert.That(progress.CurrentState.UnlockedMilestones, Does.Contain("first"));

                Assert.That(progress.GrantArtifact(journey, artifact.ArtifactId).Succeeded, Is.True);
                Assert.That(progress.CurrentState.CollectedCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void DirectGrantRejectsWrongSessionAndUnknownArtifact()
        {
            var artifact = new CollectionArtifactDefinition("artifact:a", "A", "A", "", 0, "slot:a");
            using (var progress = new CollectionProgressController())
            {
                var journey = JourneySessionId.CreateNew();
                progress.BeginSession(journey, new CollectionCatalog(new[] { artifact }));

                Assert.That(
                    progress.GrantArtifact(JourneySessionId.CreateNew(), artifact.ArtifactId).FailureCode,
                    Is.EqualTo(CollectionMutationFailureCode.InvalidSession));
                Assert.That(
                    progress.GrantArtifact(journey, "artifact:missing").FailureCode,
                    Is.EqualTo(CollectionMutationFailureCode.InvalidArtifact));
                Assert.That(progress.CurrentState.CollectedCount, Is.Zero);
            }
        }

        [Test]
        public void OneHalfAndCompleteMilestonesUnlockExactlyOnceAtConfiguredCounts()
        {
            var artifacts = new CollectionArtifactDefinition[6];
            for (var index = 0; index < artifacts.Length; index++)
                artifacts[index] = new CollectionArtifactDefinition(
                    $"artifact:{index}",
                    $"Artifact {index}",
                    string.Empty,
                    string.Empty,
                    index,
                    $"slot:{index}");
            var catalog = new CollectionCatalog(
                artifacts,
                new[]
                {
                    new CollectionMilestoneDefinition("first", CollectionMilestoneKind.FirstCollection),
                    new CollectionMilestoneDefinition("half", CollectionMilestoneKind.CollectedCountReached, 3),
                    new CollectionMilestoneDefinition("complete", CollectionMilestoneKind.CollectionCompleted)
                });

            using (var progress = new CollectionProgressController())
            {
                var journey = JourneySessionId.CreateNew();
                progress.BeginSession(journey, catalog);
                for (var index = 0; index < artifacts.Length; index++)
                {
                    progress.OfferArtifact(
                        Completed(journey, $"scene_{index}", $"completion:{index}"),
                        artifacts[index].ArtifactId);
                    var artifact = progress.CurrentState.Artifacts[index];
                    progress.CollectArtifact(artifact.Definition.ArtifactId, artifact.InstanceToken);
                    var pending = progress.CurrentState.PendingPresentation;

                    if (index == 0)
                        Assert.That(pending.UnlockedMilestones, Is.EqualTo(new[] { "first" }));
                    else if (index == 2)
                        Assert.That(pending.UnlockedMilestones, Is.EqualTo(new[] { "half" }));
                    else if (index == 5)
                        Assert.That(pending.UnlockedMilestones, Is.EqualTo(new[] { "complete" }));
                    else
                        Assert.That(pending.UnlockedMilestones, Is.Empty);

                    progress.AcknowledgePresentation(pending.Id);
                }

                Assert.That(progress.CurrentState.UnlockedMilestones,
                    Is.EquivalentTo(new[] { "first", "half", "complete" }));
                Assert.That(progress.CurrentState.IsComplete, Is.True);
                Assert.That(progress.GrantArtifact(journey, artifacts[5].ArtifactId).Succeeded, Is.True);
                Assert.That(progress.CurrentState.UnlockedMilestones,
                    Is.EquivalentTo(new[] { "first", "half", "complete" }));
            }
        }

        [Test]
        public void CatalogRejectsUnreachableOrDuplicateSingularMilestones()
        {
            var artifact = new CollectionArtifactDefinition(
                "artifact:a", "A", string.Empty, string.Empty, 0, "slot:a");
            Assert.Throws<ArgumentException>(() => new CollectionCatalog(
                new[] { artifact },
                new[]
                {
                    new CollectionMilestoneDefinition(
                        "unreachable",
                        CollectionMilestoneKind.CollectedCountReached,
                        2)
                }));
            Assert.Throws<ArgumentException>(() => new CollectionCatalog(
                new[] { artifact },
                new[]
                {
                    new CollectionMilestoneDefinition("first_a", CollectionMilestoneKind.FirstCollection),
                    new CollectionMilestoneDefinition("first_b", CollectionMilestoneKind.FirstCollection)
                }));
        }

        [Test]
        public void DirectGrantOfAvailableArtifactRetiresItsGrabPresentation()
        {
            var artifact = new CollectionArtifactDefinition("artifact:a", "A", "A", "", 0, "slot:a");
            using (var progress = new CollectionProgressController())
            {
                var journey = JourneySessionId.CreateNew();
                progress.BeginSession(journey, new CollectionCatalog(new[] { artifact }));
                progress.OfferArtifact(Completed(journey, "a", "completion:a"), artifact.ArtifactId);
                Assert.That(progress.CurrentState.PendingPresentation.Kind,
                    Is.EqualTo(CollectionPresentationKind.ArtifactOffered));

                var granted = progress.GrantArtifact(journey, artifact.ArtifactId);

                Assert.That(granted.Succeeded, Is.True);
                Assert.That(progress.CurrentState.Artifacts[0].State, Is.EqualTo(CollectionArtifactStatus.Collected));
                Assert.That(progress.CurrentState.PendingPresentation, Is.Null);
            }
        }

        [Test]
        public void PresentationAcknowledgementIsIdempotentOnlyForCurrentPendingId()
        {
            var artifact = new CollectionArtifactDefinition("artifact:a", "A", "A", "", 0, "slot:a");
            using (var progress = new CollectionProgressController())
            {
                var journey = JourneySessionId.CreateNew();
                progress.BeginSession(journey, new CollectionCatalog(new[] { artifact }));
                progress.OfferArtifact(Completed(journey, "giant_saguaro", "completion:ack"), artifact.ArtifactId);
                var pending = progress.CurrentState.PendingPresentation.Id;
                Assert.That(progress.AcknowledgePresentation(new CollectionPresentationId(pending.Value + 1)).Succeeded, Is.False);
                Assert.That(progress.AcknowledgePresentation(pending).Succeeded, Is.True);
                Assert.That(progress.CurrentState.PendingPresentation, Is.Null);
                Assert.That(progress.AcknowledgePresentation(pending).Succeeded, Is.False);
            }
        }

        [Test]
        public void CompendiumProjectionUsesTheProgressSnapshotWithoutASecondStore()
        {
            var artifact = new CollectionArtifactDefinition(
                "artifact:a",
                "A",
                "摘要 A",
                "植物",
                0,
                "slot:a");
            using (var progress = new CollectionProgressController())
            {
                var journey = JourneySessionId.CreateNew();
                progress.BeginSession(journey, new CollectionCatalog(new[] { artifact }));
                progress.OfferArtifact(Completed(journey, "giant_saguaro", "completion:projection"), artifact.ArtifactId);

                var entries = CollectionEntryViewResolver.Resolve(progress.CurrentState);
                Assert.That(entries.Count, Is.EqualTo(1));
                Assert.That(entries[0].ArtifactId, Is.EqualTo(artifact.ArtifactId));
                Assert.That(entries[0].Status, Is.EqualTo(CollectionArtifactStatus.Available));
                Assert.That(entries[0].IsCollected, Is.False);
                Assert.That(entries[0].Definition, Is.SameAs(artifact));
            }
        }

        [Test]
        public void CurrentQrRewardReplacesOldOfferAndInvalidatesItsCallbacks()
        {
            var first = new CollectionArtifactDefinition("artifact:giant_saguaro", "Saguaro", "", "", 0, "slot:a");
            var second = new CollectionArtifactDefinition("artifact:bottle_tree", "Bottle", "", "", 1, "slot:b");
            using var progress = new CollectionProgressController();
            var journey = JourneySessionId.CreateNew();
            progress.BeginSession(journey, new CollectionCatalog(new[] { first, second }));
            progress.OfferArtifact(Completed(journey, "giant_saguaro", "first"), first.ArtifactId);
            var oldToken = progress.CurrentState.Artifacts[0].InstanceToken;
            var oldPresentation = progress.CurrentState.PendingPresentation.Id;
            progress.OfferArtifact(Completed(journey, "bottle_tree", "second"), second.ArtifactId);
            Assert.That(progress.CurrentState.PendingPresentation.ArtifactId, Is.EqualTo(second.ArtifactId));
            Assert.That(progress.CollectArtifact(first.ArtifactId, oldToken).Succeeded, Is.False);
            Assert.That(progress.AcknowledgePresentation(oldPresentation).Succeeded, Is.False);
            Assert.That(progress.CurrentState.Artifacts[0].State, Is.EqualTo(CollectionArtifactStatus.Available));
            progress.DeferAvailableArtifactPresentation(progress.CurrentState.PendingPresentation.Id);
            Assert.That(progress.CurrentState.PendingPresentation, Is.Null);
            Assert.That(progress.RequestAvailableArtifactPresentation(first.ArtifactId).Succeeded, Is.True);
            Assert.That(progress.CurrentState.CollectedCount, Is.Zero);
        }

        [Test]
        public void RecompletedObservationReoffersUncollectedArtifact()
        {
            var artifact = new CollectionArtifactDefinition("artifact:a", "A", "A", "植物", 0, "slot:a");
            using (var progress = new CollectionProgressController())
            {
                var journey = JourneySessionId.CreateNew();
                progress.BeginSession(journey, new CollectionCatalog(new[] { artifact }));
                Assert.That(progress.OfferArtifact(Completed(journey, "a", "completion:a1"), artifact.ArtifactId).Succeeded, Is.True);
                var firstPending = progress.CurrentState.PendingPresentation;

                var reoffered = progress.OfferArtifact(Completed(journey, "a", "completion:a2"), artifact.ArtifactId);

                Assert.That(reoffered.Succeeded, Is.True);
                Assert.That(progress.CurrentState.PendingPresentation.Id, Is.Not.EqualTo(firstPending.Id));
                Assert.That(progress.CurrentState.PendingPresentation.ArtifactId, Is.EqualTo(artifact.ArtifactId));
                Assert.That(progress.CurrentState.Artifacts[0].State, Is.EqualTo(CollectionArtifactStatus.Available));
            }
        }

        [Test]
        public void DeferHidesOnlyCurrentOfferAndPreservesAvailableToken()
        {
            var artifact = new CollectionArtifactDefinition("artifact:a", "A", "A", "植物", 0, "slot:a");
            using (var progress = new CollectionProgressController())
            {
                var journey = JourneySessionId.CreateNew();
                progress.BeginSession(journey, new CollectionCatalog(new[] { artifact }));
                progress.OfferArtifact(Completed(journey, "giant_saguaro", "completion:defer"), artifact.ArtifactId);
                var before = progress.CurrentState.Artifacts[0];
                var pending = progress.CurrentState.PendingPresentation.Id;

                var deferred = progress.DeferAvailableArtifactPresentation(pending);

                Assert.That(deferred.Succeeded, Is.True);
                Assert.That(progress.CurrentState.PendingPresentation, Is.Null);
                Assert.That(progress.CurrentState.Artifacts[0].State, Is.EqualTo(CollectionArtifactStatus.Available));
                Assert.That(progress.CurrentState.Artifacts[0].InstanceToken, Is.EqualTo(before.InstanceToken));
                Assert.That(progress.CurrentState.CollectedCount, Is.Zero);
                Assert.That(progress.DeferAvailableArtifactPresentation(pending).Succeeded, Is.False);

                var represented = progress.RequestAvailableArtifactPresentation(artifact.ArtifactId);
                Assert.That(represented.Succeeded, Is.True);
                Assert.That(progress.CurrentState.PendingPresentation.ArtifactId, Is.EqualTo(artifact.ArtifactId));
                Assert.That(progress.CurrentState.Artifacts[0].InstanceToken, Is.EqualTo(before.InstanceToken));
            }
        }

        [Test]
        public void RewardPriorityFollowsActualCompletionInsteadOfCatalogOrder()
        {
            var first = new CollectionArtifactDefinition("artifact:a", "A", "", "", 0, "slot:a");
            var second = new CollectionArtifactDefinition("artifact:b", "B", "", "", 1, "slot:b");
            using var progress = new CollectionProgressController();
            var journey = JourneySessionId.CreateNew();
            progress.BeginSession(journey, new CollectionCatalog(new[] { first, second }));
            progress.OfferArtifact(Completed(journey, "b", "b"), second.ArtifactId);
            progress.OfferArtifact(Completed(journey, "a", "a"), first.ArtifactId);
            Assert.That(progress.CurrentState.PendingPresentation.ArtifactId, Is.EqualTo(first.ArtifactId));
            progress.DeferAvailableArtifactPresentation(progress.CurrentState.PendingPresentation.Id);
            Assert.That(progress.CurrentState.PendingPresentation, Is.Null);
            Assert.That(progress.RequestAvailableArtifactPresentation(second.ArtifactId).Succeeded, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedCollectedQrDoesNotShowAnotherPlantsPendingReward(bool skip)
        {
            var bottle = new CollectionArtifactDefinition("artifact:bottle_tree", "Bottle", "", "", 0, "slot:a");
            var cactus = new CollectionArtifactDefinition("artifact:giant_saguaro", "Cactus", "", "", 1, "slot:b");
            using var progress = new CollectionProgressController();
            var journey = JourneySessionId.CreateNew();
            progress.BeginSession(journey, new CollectionCatalog(new[] { bottle, cactus }));
            progress.GrantArtifact(journey, bottle.ArtifactId);
            progress.OfferArtifact(Completed(journey, "giant_saguaro", "old"), cactus.ArtifactId);
            if (skip) progress.GrantArtifact(journey, bottle.ArtifactId);
            else progress.OfferArtifact(Completed(journey, "bottle_tree", "repeat"), bottle.ArtifactId);
            Assert.That(progress.CurrentState.PendingPresentation, Is.Null);
            Assert.That(progress.CurrentState.CollectedCount, Is.EqualTo(1));
            Assert.That(progress.CurrentState.Artifacts[1].State, Is.EqualTo(CollectionArtifactStatus.Available));
        }

        static ObservationCompletedFact Completed(JourneySessionId journey, string sceneId, string completionId)
            => new ObservationCompletedFact(
                SessionToken.CreateNew(),
                journey,
                new SceneId(sceneId),
                ObservationCompletionKind.Confirmation,
                completionId);
    }
}
