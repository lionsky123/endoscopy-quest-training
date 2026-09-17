using System;
using System.Reflection;
using BotanicalGardenQR.Bootstrap;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode.Activation
{
    public sealed class VisitorDiagnosticsSanitizationTests
    {
        [Test]
        public void QrPayload_IsRemovedFromVisitorDiagnosticDetail()
        {
            var sanitize = typeof(VisitorInstaller).GetMethod(
                "Sanitize",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(sanitize, Is.Not.Null);
            var qrResult = (string)sanitize.Invoke(null, new object[] { "qr payload: zone:entrance_001" });

            Assert.That(qrResult, Does.Not.Contain("zone:entrance_001"));
            Assert.That(qrResult, Does.Contain("<redacted>"));
        }

        [Test]
        public void RepeatedUnityDiagnostic_IsSuppressedUntilIntervalExpires()
        {
            var throttle = new VisitorDiagnosticThrottle(TimeSpan.FromSeconds(5d), 4);
            var first = DateTimeOffset.Parse("2026-08-06T00:00:00Z");

            Assert.That(throttle.TryAccept("same-error", first, out var firstSuppressed), Is.True);
            Assert.That(firstSuppressed, Is.Zero);
            Assert.That(throttle.TryAccept("same-error", first.AddSeconds(1d), out _), Is.False);
            Assert.That(throttle.TryAccept("same-error", first.AddSeconds(2d), out _), Is.False);
            Assert.That(throttle.TryAccept("same-error", first.AddSeconds(5d), out var suppressed), Is.True);
            Assert.That(suppressed, Is.EqualTo(2));
        }

        [Test]
        public void DistinctUnityDiagnostics_AreAcceptedIndependently()
        {
            var throttle = new VisitorDiagnosticThrottle(TimeSpan.FromSeconds(5d), 2);
            var timestamp = DateTimeOffset.Parse("2026-08-06T00:00:00Z");

            Assert.That(throttle.TryAccept("error-a", timestamp, out _), Is.True);
            Assert.That(throttle.TryAccept("error-b", timestamp, out _), Is.True);
        }
    }
}
