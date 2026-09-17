using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Activation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Activation
{
    public sealed class QrFocusConfirmationEngineTests
    {
        [Test]
        public void DetectedButNotHit_DoesNotStartFocus()
        {
            var engine = Engine();
            var target = Target(Guid.NewGuid(), new Vector3(1f, 0f, 2f));

            var frame = engine.Tick(0d, Query(Vector3.forward), new[] { target });

            Assert.That(frame.Current.HasValue, Is.False);
            Assert.That(frame.Cleared.HasValue, Is.False);
        }

        [Test]
        public void FocusAccumulatesAndConfirmsExactlyOnce()
        {
            var engine = Engine();
            var id = Guid.NewGuid();
            var targets = new[] { Target(id, new Vector3(0f, 0f, 2f)) };
            var query = Query(Vector3.forward);

            Assert.That(engine.Tick(0d, query, targets).Current.Value.Progress, Is.Zero);
            Assert.That(Advance(engine, 0d, 0.5d, query, targets).Current.Value.Progress, Is.EqualTo(0.5f).Within(0.001f));
            var confirmed = Advance(engine, 0.5d, 1d, query, targets).Current;
            Assert.That(confirmed.Value.ObservationId, Is.EqualTo(id));
            Assert.That(confirmed.Value.IsConfirmation, Is.True);
            Assert.That(engine.Tick(1.05d, Query(Vector3.forward), targets).Current.HasValue, Is.False);
        }

        [Test]
        public void EmptyFocusPausesThenClearsWithoutAccumulating()
        {
            var engine = Engine();
            var id = Guid.NewGuid();
            var targets = new[] { Target(id, new Vector3(0f, 0f, 2f)) };

            var query = Query(Vector3.forward);
            engine.Tick(0d, query, targets);
            Advance(engine, 0d, 0.4d, query, targets);
            Assert.That(engine.Tick(0.45d, null, targets).Cleared.HasValue, Is.False);
            Assert.That(engine.Tick(0.55d, query, targets).Current.HasValue, Is.False);
            Assert.That(engine.Tick(0.6d, query, targets).Current.Value.Progress, Is.EqualTo(0.45f).Within(0.001f));
            engine.Tick(0.65d, null, targets);
            var cleared = engine.Tick(0.81d, null, targets).Cleared;

            Assert.That(cleared.Value.ObservationId, Is.EqualTo(id));
            Assert.That(cleared.Value.Progress, Is.Zero);
        }

        [Test]
        public void LookingAtBImmediatelyReplacesAWhileAGraceIsActive()
        {
            var engine = Engine();
            var a = Target(Guid.NewGuid(), new Vector3(-0.5f, 0f, 2f));
            var b = Target(Guid.NewGuid(), new Vector3(0.5f, 0f, 2f));
            var targets = new[] { a, b };

            var queryA = Query(a.Evidence.Position);
            engine.Tick(0d, queryA, targets);
            Advance(engine, 0d, 0.4d, queryA, targets);
            engine.Tick(0.45d, null, targets);
            var switched = engine.Tick(0.46d, Query(b.Evidence.Position), targets);

            Assert.That(switched.Cleared.Value.ObservationId, Is.EqualTo(a.ObservationId));
            Assert.That(switched.Current.Value.ObservationId, Is.EqualTo(b.ObservationId));
            Assert.That(switched.Current.Value.Progress, Is.Zero);
        }

        [Test]
        public void ConfirmedAThenBThenA_AllCreateIndependentSessions()
        {
            var engine = Engine();
            var a = Target(Guid.NewGuid(), new Vector3(-0.5f, 0f, 2f));
            var b = Target(Guid.NewGuid(), new Vector3(0.5f, 0f, 2f));
            var targets = new[] { a, b };

            var queryA = Query(a.Evidence.Position);
            var queryB = Query(b.Evidence.Position);
            engine.Tick(0d, queryA, targets);
            Assert.That(Advance(engine, 0d, 1d, queryA, targets).Current.Value.IsConfirmation, Is.True);
            engine.Tick(1.1d, queryB, targets);
            Assert.That(Advance(engine, 1.1d, 2.1d, queryB, targets).Current.Value.IsConfirmation, Is.True);
            engine.Tick(2.2d, queryA, targets);
            var confirmedAgain = Advance(engine, 2.2d, 3.2d, queryA, targets).Current;

            Assert.That(confirmedAgain.Value.ObservationId, Is.EqualTo(a.ObservationId));
            Assert.That(confirmedAgain.Value.IsConfirmation, Is.True);
        }

        [Test]
        public void SelectionUsesNearestHitAndKeepsCurrentOnExactTie()
        {
            var engine = Engine();
            var current = Target(Guid.NewGuid(), new Vector3(0f, 0f, 2f));
            var farther = Target(Guid.NewGuid(), new Vector3(0f, 0f, 3f));

            var first = engine.Tick(0d, Query(Vector3.forward), new[] { farther, current });
            Assert.That(first.Current.Value.ObservationId, Is.EqualTo(current.ObservationId));

            var tied = Target(Guid.NewGuid(), current.Evidence.Position);
            var stable = engine.Tick(0.1d, Query(Vector3.forward), new[] { tied, current });
            Assert.That(stable.Cleared.HasValue, Is.False);
        }

        [Test]
        public void ConfirmedObservationIsConsumedSoAnotherObservationCanFocusWithoutOscillation()
        {
            var engine = Engine();
            var a = Target(Guid.NewGuid(), new Vector3(0f, 0f, 2f));
            var b = Target(Guid.NewGuid(), a.Evidence.Position);
            var query = Query(Vector3.forward);

            engine.Tick(0d, query, new[] { a });
            Assert.That(Advance(engine, 0d, 1d, query, new[] { a }).Current.Value.IsConfirmation, Is.True);

            var switched = engine.Tick(1.05d, query, new[] { b, a });

            Assert.That(switched.Cleared.Value.ObservationId, Is.EqualTo(a.ObservationId));
            Assert.That(switched.Current.Value.ObservationId, Is.EqualTo(b.ObservationId));
            Assert.That(switched.Current.Value.Progress, Is.Zero);

            var confirmedB = Advance(engine, 1.05d, 2.05d, query, new[] { b, a }).Current;
            Assert.That(confirmedB.Value.ObservationId, Is.EqualTo(b.ObservationId));
            Assert.That(confirmedB.Value.IsConfirmation, Is.True);
            Assert.That(engine.Tick(2.1d, query, new[] { a, b }).Current.HasValue, Is.False);
        }

        [Test]
        public void ConsumedObservationCannotRestartUntilExplicitlyRearmed()
        {
            var engine = Engine();
            var a = Target(Guid.NewGuid(), new Vector3(0f, 0f, 2f));
            var query = Query(Vector3.forward);

            engine.Tick(0d, query, new[] { a });
            Assert.That(Advance(engine, 0d, 1d, query, new[] { a }).Current.Value.IsConfirmation, Is.True);
            Assert.That(engine.Tick(1.05d, query, new[] { a }).Current.HasValue, Is.False);
            Assert.That(engine.Tick(1.21d, query, new[] { a }).Cleared.Value.ObservationId,
                Is.EqualTo(a.ObservationId));
            Assert.That(engine.Tick(1.22d, query, new[] { a }).Current.HasValue, Is.False);

            Assert.That(engine.Rearm(a.ObservationId), Is.True);
            var restarted = engine.Tick(1.23d, query, new[] { a }).Current;
            Assert.That(restarted.Value.ObservationId, Is.EqualTo(a.ObservationId));
            Assert.That(restarted.Value.Progress, Is.Zero);
            Assert.That(restarted.Value.IsConfirmation, Is.False);
        }

        [Test]
        public void RearmingConsumedAWhileBFocusingDoesNotResetB()
        {
            var engine = Engine();
            var a = Target(Guid.NewGuid(), new Vector3(0f, 0f, 2f));
            var b = Target(Guid.NewGuid(), a.Evidence.Position);
            var query = Query(Vector3.forward);

            engine.Tick(0d, query, new[] { a });
            Assert.That(Advance(engine, 0d, 1d, query, new[] { a }).Current.Value.IsConfirmation, Is.True);
            Assert.That(engine.Tick(1.05d, query, new[] { a, b }).Current.Value.ObservationId,
                Is.EqualTo(b.ObservationId));

            Assert.That(engine.Rearm(a.ObservationId), Is.True);
            var continued = engine.Tick(1.1d, query, new[] { a, b });

            Assert.That(continued.Cleared.HasValue, Is.False);
            Assert.That(continued.Current.Value.ObservationId, Is.EqualTo(b.ObservationId));
            Assert.That(continued.Current.Value.Progress, Is.GreaterThan(0f));
        }

        [Test]
        public void ExactTieWithoutCurrentIsIndependentOfInputOrder()
        {
            var low = Target(Guid.Parse("00000000-0000-0000-0000-000000000001"), new Vector3(0f, 0f, 2f));
            var high = Target(Guid.Parse("00000000-0000-0000-0000-000000000002"), low.Evidence.Position);
            var query = Query(Vector3.forward);

            var forwardOrder = Engine().Tick(0d, query, new[] { low, high }).Current.Value.ObservationId;
            var reverseOrder = Engine().Tick(0d, query, new[] { high, low }).Current.Value.ObservationId;

            Assert.That(forwardOrder, Is.EqualTo(low.ObservationId));
            Assert.That(reverseOrder, Is.EqualTo(low.ObservationId));
        }

        [Test]
        public void SmallAngularPaddingExpandsThePhysicalPlaneWithoutUsingAWideCone()
        {
            var target = new QrFocusTarget(
                Guid.NewGuid(),
                Evidence(new Vector3(0.2f, 0f, 2f)),
                new Rect(-0.05f, -0.05f, 0.1f, 0.1f));

            Assert.That(
                Engine().Tick(0d, Query(Vector3.forward), new[] { target }).Current.HasValue,
                Is.False);
            var padded = new RecognitionFocusQuery(new Ray(Vector3.zero, Vector3.forward), 5f);
            Assert.That(
                Engine().Tick(0d, padded, new[] { target }).Current.Value.ObservationId,
                Is.EqualTo(target.ObservationId));
        }

        [Test]
        public void RearmAndLongClockGapRestartAtZero()
        {
            var engine = Engine();
            var target = Target(Guid.NewGuid(), new Vector3(0f, 0f, 2f));
            var targets = new[] { target };
            var query = Query(Vector3.forward);

            engine.Tick(0d, query, targets);
            Advance(engine, 0d, 1d, query, targets);
            Assert.That(engine.Rearm(target.ObservationId), Is.True);
            Assert.That(engine.Tick(1.01d, query, targets).Current.Value.Progress, Is.Zero);
            Advance(engine, 1.01d, 1.2d, query, targets);
            var afterGap = engine.Tick(1.8d, query, targets).Current;

            Assert.That(afterGap.Value.ObservationId, Is.EqualTo(target.ObservationId));
            Assert.That(afterGap.Value.Progress, Is.Zero);
            Assert.That(afterGap.Value.IsConfirmation, Is.False);
        }

        [Test]
        public void MissingQueryAndInvalidPlaneFailClosed()
        {
            var engine = Engine();
            var invalid = new QrFocusTarget(
                Guid.NewGuid(),
                Evidence(new Vector3(0f, 0f, 2f)),
                new Rect(0f, 0f, 0f, 0f));

            Assert.That(engine.Tick(0d, null, new[] { invalid }).Current.HasValue, Is.False);
            Assert.That(engine.Tick(0.1d, Query(Vector3.forward), new[] { invalid }).Current.HasValue, Is.False);

            var valid = Target(Guid.NewGuid(), new Vector3(0f, 0f, 2f));
            engine.Tick(0.2d, Query(Vector3.forward), new[] { valid });
            Assert.That(engine.Tick(double.NaN, Query(Vector3.forward), new[] { valid }).Cleared.Value.ObservationId,
                Is.EqualTo(valid.ObservationId));
        }

        static QrFocusConfirmationEngine Engine()
            => new QrFocusConfirmationEngine(1f, 0.15f);

        static QrFocusFrame Advance(
            QrFocusConfirmationEngine engine,
            double from,
            double to,
            RecognitionFocusQuery query,
            QrFocusTarget[] targets)
        {
            var frame = default(QrFocusFrame);
            for (var now = from + 0.05d; now < to - 0.000001d; now += 0.05d)
                frame = engine.Tick(now, query, targets);
            return engine.Tick(to, query, targets);
        }

        static RecognitionFocusQuery Query(Vector3 direction)
            => new RecognitionFocusQuery(new Ray(Vector3.zero, direction), 0f);

        static QrFocusTarget Target(Guid id, Vector3 position)
            => new QrFocusTarget(
                id,
                Evidence(position),
                new Rect(-0.1f, -0.1f, 0.2f, 0.2f));

        static SpatialEvidence Evidence(Vector3 position)
            => new SpatialEvidence(position, Quaternion.Euler(0f, 180f, 0f), true);
    }
}
