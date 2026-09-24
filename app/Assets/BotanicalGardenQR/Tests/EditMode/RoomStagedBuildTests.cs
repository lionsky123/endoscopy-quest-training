using System.Collections;
using BotanicalGardenQR.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class RoomStagedBuildTests
    {
        [UnityTest]
        public IEnumerator OfficeGeometryBuildYieldsFramesAndKeepsThePublishedRoomComplete()
        {
            var rig = new GameObject("Staged room rig", typeof(Camera));
            var map = FullScriptRoomCatalog.Map("R01_OFFICE", null, false, true);
            VirtualRoomEnvironment room = null;
            try
            {
                using (var build = VirtualRoomEnvironment.BeginBuild(rig, null, map))
                {
                    var frames = 0;
                    var deadline = System.DateTime.UtcNow.AddSeconds(25);
                    while (!build.IsComplete && System.DateTime.UtcNow < deadline)
                    {
                        frames++;
                        build.Tick();
                        yield return null;
                    }
                    Assert.That(build.IsComplete, Is.True, "The streamed room must eventually finish; phase=" + build.PhaseName + ", frames=" + frames);
                    Assert.That(frames, Is.GreaterThan(15), "Large geometry must not be built in one frame.");
                    room = build.TakeRoom();
                }
                Assert.That(room.Root.GetComponentsInChildren<MeshFilter>().Length, Is.GreaterThan(60));
                Assert.That(room.GuidePath, Is.Not.Null);
            }
            finally
            {
                room?.Dispose();
                Object.DestroyImmediate(rig);
            }
        }

        [UnityTest]
        public IEnumerator MismatchedGeometryDigestDisposesThePartialRoom()
        {
            var rig = new GameObject("Rejected room rig", typeof(Camera));
            var map = FullScriptRoomCatalog.Map("R01_OFFICE", null, false, true);
            map.modelDigest = "outdated-model";
            try
            {
                using (var build = VirtualRoomEnvironment.BeginBuild(rig, null, map))
                {
                    var rejected = false;
                    var deadline = System.DateTime.UtcNow.AddSeconds(25);
                    while (!rejected && System.DateTime.UtcNow < deadline)
                    {
                        try { build.Tick(); }
                        catch (System.InvalidOperationException) { rejected = true; }
                        if (!rejected) yield return null;
                    }
                    Assert.That(rejected, Is.True, "A stale model digest must stop a streamed room; phase=" + build.PhaseName);
                }
                Assert.That(GameObject.Find(map.mapId), Is.Null, "A failed room must not remain in the scene.");
            }
            finally { Object.DestroyImmediate(rig); }
        }
    }
}
