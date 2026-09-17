using BotanicalGardenQR.Experience.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode.Experience
{
    public sealed class ObservationCompletionFactTests
    {
        [Test]
        public void SingleChoiceRequiresAnExplicitAnswer()
        {
            Assert.Throws<System.ArgumentException>(() => new ObservationCompletedFact(
                SessionToken.CreateNew(),
                JourneySessionId.CreateNew(),
                new SceneId("giant_saguaro"),
                ObservationCompletionKind.SingleChoice,
                "observation:giant_saguaro"));
        }

        [Test]
        public void CompletedFactKeepsSessionAndChoiceIdentity()
        {
            var content = SessionToken.CreateNew();
            var journey = JourneySessionId.CreateNew();
            var fact = new ObservationCompletedFact(
                content,
                journey,
                new SceneId("giant_saguaro"),
                ObservationCompletionKind.SingleChoice,
                "observation:giant_saguaro",
                "bat");

            Assert.That(fact.ContentSession, Is.EqualTo(content));
            Assert.That(fact.JourneySession, Is.EqualTo(journey));
            Assert.That(fact.AnswerId, Is.EqualTo("bat"));
        }
    }
}
