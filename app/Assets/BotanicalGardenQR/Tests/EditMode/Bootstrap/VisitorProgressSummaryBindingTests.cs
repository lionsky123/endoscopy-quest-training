using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.Collection.Contracts;
using BotanicalGardenQR.Collection.Runtime;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.JourneyNavigation.Contracts;
using BotanicalGardenQR.JourneyNavigation.Runtime;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode.Bootstrap
{
    public sealed class VisitorProgressSummaryBindingTests
    {
        [Test]
        public void BindingCombinesCurrentAuthoritiesAcrossUpdateOrderAndReattachment()
        {
            var points = new[] { new SceneId("scene_second"), new SceneId("scene_first") };
            var collectionCatalog = SixArtifactCatalog();
            var session = JourneySessionId.CreateNew();

            using (var journey = new JourneyNavigationController())
            using (var collection = new CollectionProgressController())
            {
                journey.BeginJourney(session);
                collection.BeginSession(session, collectionCatalog);
                var presenter = new RecordingPresenter();
                using (var binding = new VisitorProgressSummaryBinding(
                           journey,
                           collection,
                           points.Length,
                           presenter))
                {
                    AssertSummary(presenter.Last, 0, 2, 0, 6);

                    Grant(collection, session, collectionCatalog, 0, 3);
                    AssertSummary(presenter.Last, 0, 2, 3, 6);

                    CompleteCurrentPoint(journey, session, points[0]);
                    AssertSummary(presenter.Last, 1, 2, 3, 6);
                }

                var reattachedPresenter = new RecordingPresenter();
                using (var reattached = new VisitorProgressSummaryBinding(
                           journey,
                           collection,
                           points.Length,
                           reattachedPresenter))
                {
                    AssertSummary(reattachedPresenter.Last, 1, 2, 3, 6);

                    CompleteCurrentPoint(journey, session, points[1]);
                    Grant(collection, session, collectionCatalog, 3, 6);

                    AssertSummary(reattachedPresenter.Last, 2, 2, 6, 6);
                    Assert.That(reattachedPresenter.Last.IsAllComplete, Is.True);
                }
            }
        }

        [Test]
        public void SummaryRejectsImpossibleCounts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new VisitorProgressSummary(3, 2, 0, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new VisitorProgressSummary(0, 2, 7, 6));
        }

        static CollectionCatalog SixArtifactCatalog()
        {
            var artifacts = new List<CollectionArtifactDefinition>();
            for (var index = 0; index < 6; index++)
                artifacts.Add(new CollectionArtifactDefinition(
                    $"artifact:{index}",
                    $"Artifact {index}",
                    string.Empty,
                    string.Empty,
                    index,
                    $"slot:{index}"));
            return new CollectionCatalog(artifacts);
        }

        static void Grant(
            ICollectionProgress collection,
            JourneySessionId session,
            CollectionCatalog catalog,
            int start,
            int end)
        {
            for (var index = start; index < end; index++)
                Assert.That(collection.GrantArtifact(
                    session,
                    catalog.Artifacts[index].ArtifactId).Succeeded, Is.True);
        }

        static void CompleteCurrentPoint(
            IJourneyNavigation journey,
            JourneySessionId session,
            SceneId point)
        {
            var contentSession = SessionToken.CreateNew();
            Assert.That(journey.AcceptContentOpened(new ContentOpenedFact(
                contentSession,
                session,
                point,
                new SourceKind("qr"),
                "verified_entry",
                false)), Is.True);
            Assert.That(journey.AcceptContentClosed(new ContentClosedFact(
                contentSession,
                session,
                point,
                true)), Is.True);
            Assert.That(journey.Dispatch(JourneyIntent.ContinueToNext).Succeeded, Is.True);
        }

        static void AssertSummary(
            VisitorProgressSummary summary,
            int mainCompleted,
            int mainTotal,
            int collectionCompleted,
            int collectionTotal)
        {
            Assert.That(summary, Is.Not.Null);
            Assert.That(summary.MainCompleted, Is.EqualTo(mainCompleted));
            Assert.That(summary.MainTotal, Is.EqualTo(mainTotal));
            Assert.That(summary.CollectionCompleted, Is.EqualTo(collectionCompleted));
            Assert.That(summary.CollectionTotal, Is.EqualTo(collectionTotal));
        }

        sealed class RecordingPresenter : IVisitorProgressSummaryPresenter
        {
            public VisitorProgressSummary Last { get; private set; }
            public void SetVisitorProgressSummary(VisitorProgressSummary summary) => Last = summary;
        }
    }
}
