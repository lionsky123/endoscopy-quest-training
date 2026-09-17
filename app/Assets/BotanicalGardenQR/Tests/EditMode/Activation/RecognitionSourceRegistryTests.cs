using System;
using System.Collections.Generic;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Activation.Runtime;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode.Activation
{
    public sealed class RecognitionSourceRegistryTests
    {
        [Test]
        public void DuplicateSourceKinds_AreRejectedAtConstruction()
        {
            var sources = new[]
            {
                new FakeSource("qr", new List<string>()),
                new FakeSource("qr", new List<string>())
            };

            Assert.That(
                () => new RecognitionSourceRegistry(sources),
                Throws.ArgumentException);
        }

        [Test]
        public void Dispose_ReleasesRegistrationsInReverseStartOrder()
        {
            var calls = new List<string>();
            var registry = new RecognitionSourceRegistry(new[]
            {
                new FakeSource("qr", calls),
                new FakeSource("spatial_anchor", calls)
            });
            registry.Start(new NullObservationSink());

            registry.Dispose();

            Assert.That(
                calls,
                Is.EqualTo(new[]
                {
                    "qr.start",
                    "spatial_anchor.start",
                    "spatial_anchor.dispose",
                    "qr.dispose"
                }));
        }

        [Test]
        public void PartialStartFailure_ReleasesEarlierRegistration_AndPreservesStartFailure()
        {
            var calls = new List<string>();
            var registry = new RecognitionSourceRegistry(new[]
            {
                new FakeSource("qr", calls, throwOnDispose: true),
                new FakeSource("spatial_anchor", calls, throwOnStart: true)
            });

            Assert.That(
                () => registry.Start(new NullObservationSink()),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.EqualTo("spatial_anchor start failed"));
            Assert.That(
                calls,
                Is.EqualTo(new[]
                {
                    "qr.start",
                    "spatial_anchor.start",
                    "qr.dispose"
                }));
        }

        [Test]
        public void RegistrationDisposeFailure_DoesNotBlockRemainingCleanupOrEscape()
        {
            var calls = new List<string>();
            var registry = new RecognitionSourceRegistry(new[]
            {
                new FakeSource("qr", calls),
                new FakeSource("spatial_anchor", calls, throwOnDispose: true)
            });
            registry.Start(new NullObservationSink());

            Assert.That(() => registry.Dispose(), Throws.Nothing);
            Assert.That(
                calls,
                Is.EqualTo(new[]
                {
                    "qr.start",
                    "spatial_anchor.start",
                    "spatial_anchor.dispose",
                    "qr.dispose"
                }));
            Assert.That(() => registry.Dispose(), Throws.Nothing);
        }

        sealed class FakeSource : IRecognitionSource
        {
            readonly List<string> _calls;
            readonly bool _throwOnStart;
            readonly bool _throwOnDispose;

            public FakeSource(
                string kind,
                List<string> calls,
                bool throwOnStart = false,
                bool throwOnDispose = false)
            {
                Kind = new SourceKind(kind);
                _calls = calls;
                _throwOnStart = throwOnStart;
                _throwOnDispose = throwOnDispose;
            }

            public SourceKind Kind { get; }

            public IDisposable Start(IRecognitionObservationSink sink)
            {
                _calls.Add($"{Kind}.start");
                if (_throwOnStart)
                    throw new InvalidOperationException($"{Kind} start failed");
                return new Registration(this);
            }

            sealed class Registration : IDisposable
            {
                FakeSource _owner;

                public Registration(FakeSource owner) => _owner = owner;

                public void Dispose()
                {
                    var owner = _owner;
                    if (owner == null) return;
                    _owner = null;
                    owner._calls.Add($"{owner.Kind}.dispose");
                    if (owner._throwOnDispose)
                        throw new InvalidOperationException($"{owner.Kind} dispose failed");
                }
            }
        }

        sealed class NullObservationSink : IRecognitionObservationSink
        {
            public void Publish(RecognitionObservation observation) { }
        }
    }
}
