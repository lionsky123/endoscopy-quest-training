using BotanicalGardenQR.Activation.Contracts;
using BotanicalGardenQR.Experience.Contracts;
using BotanicalGardenQR.SpatialHost.Contracts;
using BotanicalGardenQR.SpatialHost.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.SpatialHost
{
    public sealed class SpatialDisplayHostPlacementTests
    {
        [Test]
        public void ViewerFrontWithNearQrWall_StopsBeforeSourcePlane()
        {
            using (var fixture = PlacementFixture.Create())
            {
                var evidence = Evidence(
                    new Vector3(0f, 0f, 0.8f),
                    Quaternion.LookRotation(Vector3.back, Vector3.up));

                fixture.PrepareAndCommit(evidence);

                Assert.That(fixture.Root.position.z, Is.LessThan(0.8f));
                Assert.That(fixture.Root.position.z, Is.EqualTo(0.68f).Within(0.001f));
            }
        }

        [Test]
        public void ViewerFrontWithNearQrWall_StopsBeforePlaneWhenNormalIsReversed()
        {
            using (var fixture = PlacementFixture.Create())
            {
                var evidence = Evidence(
                    new Vector3(0f, 0f, 0.8f),
                    Quaternion.LookRotation(Vector3.forward, Vector3.up));

                fixture.PrepareAndCommit(evidence);

                Assert.That(fixture.Root.position.z, Is.LessThan(0.8f));
                Assert.That(fixture.Root.position.z, Is.EqualTo(0.68f).Within(0.001f));
            }
        }

        [Test]
        public void ViewerFrontWithDistantQrWall_KeepsPreferredViewerDistance()
        {
            using (var fixture = PlacementFixture.Create())
            {
                var evidence = Evidence(
                    new Vector3(0f, 0f, 3f),
                    Quaternion.LookRotation(Vector3.back, Vector3.up));

                fixture.PrepareAndCommit(evidence);

                Assert.That(fixture.Root.position.z, Is.EqualTo(1.65f).Within(0.001f));
            }
        }

        [Test]
        public void ViewerFrontWithoutSpatialEvidence_KeepsPreferredViewerDistance()
        {
            using (var fixture = PlacementFixture.Create())
            {
                fixture.PrepareAndCommit(null);

                Assert.That(fixture.Root.position.z, Is.EqualTo(1.65f).Within(0.001f));
            }
        }

        static SpatialEvidence Evidence(Vector3 position, Quaternion rotation)
            => new SpatialEvidence(position, rotation, true);

        sealed class PlacementFixture : System.IDisposable
        {
            readonly GameObject _viewerObject;
            readonly GameObject _rootObject;
            readonly DisplayProfile _profile;
            readonly ISpatialDisplayHost _host;

            PlacementFixture(GameObject viewerObject, GameObject rootObject, DisplayProfile profile)
            {
                _viewerObject = viewerObject;
                _rootObject = rootObject;
                _profile = profile;
                _host = SpatialHostModuleFactory.Create(viewerObject.transform, rootObject.transform);
            }

            public Transform Root => _rootObject.transform;

            public static PlacementFixture Create()
            {
                var viewerObject = new GameObject("Viewer");
                var rootObject = new GameObject("SpatialDisplayRoot");
                viewerObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                rootObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                return new PlacementFixture(viewerObject, rootObject, CreateViewerFrontProfile());
            }

            public void PrepareAndCommit(SpatialEvidence? evidence)
            {
                var result = _host.Prepare(SessionToken.CreateNew(), _profile, evidence);
                Assert.That(result.Succeeded, Is.True, result.Failure.ToString());
                result.Lease.Commit();
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_profile);
                Object.DestroyImmediate(_rootObject);
                Object.DestroyImmediate(_viewerObject);
            }

            static DisplayProfile CreateViewerFrontProfile()
            {
                var profile = ScriptableObject.CreateInstance<DisplayProfile>();
                var serialized = new SerializedObject(profile);
                serialized.FindProperty("_hostMode").intValue = (int)HostMode.ViewerFront;
                serialized.FindProperty("_distance").floatValue = 1.65f;
                serialized.FindProperty("_height").floatValue = 0f;
                serialized.FindProperty("_scale").floatValue = 1f;
                serialized.FindProperty("_orientation").intValue = (int)HostOrientation.FaceViewerUpright;
                serialized.FindProperty("_sourceLost").intValue = (int)SourceLostPolicy.KeepLastPose;
                serialized.FindProperty("_sourceLostGraceSeconds").floatValue = 1.5f;
                serialized.FindProperty("_environmentFallback").intValue = (int)EnvironmentFallback.None;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return profile;
            }
        }
    }
}
