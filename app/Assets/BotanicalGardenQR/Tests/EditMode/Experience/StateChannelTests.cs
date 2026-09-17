using System;
using System.Collections.Generic;
using System.Threading;
using BotanicalGardenQR.Experience.Contracts;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode.Experience
{
    public sealed class StateChannelTests
    {
        [Test]
        public void Observe_PublishesCurrentSnapshotImmediately()
        {
            using var channel = CreateChannel();
            VersionedState observed = default;

            using var subscription = channel.Observe(state => observed = state);

            Assert.That(observed.Version, Is.Zero);
            Assert.That(observed.Value, Is.EqualTo("initial"));
        }

        [Test]
        public void ThrowingSubscriber_IsRemovedAndHealthySubscriberContinues()
        {
            using var channel = CreateChannel();
            var throwingCalls = 0;
            var healthyVersions = new List<long>();

            using var throwing = channel.Observe(_ =>
            {
                throwingCalls++;
                throw new InvalidOperationException("injected subscriber failure");
            });
            using var healthy = channel.Observe(state => healthyVersions.Add(state.Version));

            channel.Publish(new VersionedState(1, "next"));

            Assert.That(throwingCalls, Is.EqualTo(1));
            Assert.That(healthyVersions, Is.EqualTo(new long[] { 0, 1 }));
        }

        [Test]
        public void Callback_CanObserveAndDisposeWithoutHoldingChannelLock()
        {
            using var channel = CreateChannel();
            IDisposable first = null;
            var nestedVersions = new List<long>();
            first = channel.Observe(state =>
            {
                if (state.Version == 0) return;
                using var nested = channel.Observe(value => nestedVersions.Add(value.Version));
                first.Dispose();
            });

            Assert.That(
                () => channel.Publish(new VersionedState(1, "next")),
                Throws.Nothing);
            channel.Publish(new VersionedState(2, "last"));

            Assert.That(nestedVersions, Is.EqualTo(new long[] { 1 }));
        }

        [Test]
        public void Dispose_IsIdempotentAndStopsFutureCallbacks()
        {
            using var channel = CreateChannel();
            var calls = 0;
            var subscription = channel.Observe(_ => calls++);

            subscription.Dispose();
            subscription.Dispose();
            channel.Publish(new VersionedState(1, "next"));

            Assert.That(calls, Is.EqualTo(1));
        }

        static StateChannel<VersionedState> CreateChannel()
            => new StateChannel<VersionedState>(
                new VersionedState(0, "initial"),
                state => state.Version,
                Thread.CurrentThread.ManagedThreadId);

        readonly struct VersionedState
        {
            public VersionedState(long version, string value)
            {
                Version = version;
                Value = value;
            }

            public long Version { get; }
            public string Value { get; }
        }
    }
}
