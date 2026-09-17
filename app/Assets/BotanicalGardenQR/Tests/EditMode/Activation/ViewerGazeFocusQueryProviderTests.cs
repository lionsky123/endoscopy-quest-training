using System;
using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Activation.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Activation
{
    public sealed class ViewerGazeFocusQueryProviderTests
    {
        [Test]
        public void ValidViewerProducesNormalizedForwardRayAndPadding()
        {
            var rotation = Quaternion.Euler(0f, 30f, 0f);
            var provider = CreateProvider(rotation, 2f);

            Assert.That(provider.TryGetQuery(out var query), Is.True);
            Assert.That(query.Ray.origin, Is.EqualTo(Vector3.zero));
            Assert.That(Vector3.Angle(query.Ray.direction, rotation * Vector3.forward), Is.LessThan(0.001f));
            Assert.That(query.AngularPaddingDegrees, Is.EqualTo(2f));
        }

        [Test]
        public void MissingViewerFailsClosed()
        {
            var provider = new ViewerGazeFocusQueryProvider(() => null, 2f);

            Assert.That(provider.TryGetQuery(out _), Is.False);
        }

        [Test]
        public void InvalidPaddingIsRejected()
        {
            Assert.That(
                () => new ViewerGazeFocusQueryProvider(() => Evidence(Vector3.zero, Quaternion.identity), 10.1f),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        static ViewerGazeFocusQueryProvider CreateProvider(
            Quaternion viewerRotation,
            float paddingDegrees)
            => new ViewerGazeFocusQueryProvider(
                () => Evidence(Vector3.zero, viewerRotation),
                paddingDegrees);

        static SpatialEvidence Evidence(Vector3 position, Quaternion rotation)
            => new SpatialEvidence(position, rotation, true);
    }
}
