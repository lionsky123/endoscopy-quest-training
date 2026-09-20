using System;
using System.Linq;
using BotanicalGardenQR.Bootstrap;
using BotanicalGardenQR.MapNavigation.Contracts;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class ClinicalRoomSurfaceTests
    {
        GameObject _rig;
        VirtualRoomEnvironment _room;
        [SetUp] public void SetUp()
        {
            _rig = new GameObject("Room surface test", typeof(Camera));
            _rig.GetComponent<Camera>().nearClipPlane = .3f;
            var definition = JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json").text);
            _room = VirtualRoomEnvironment.Create(_rig, null, definition);
        }
        [TearDown] public void TearDown() { _room?.Dispose(); Object.DestroyImmediate(_rig); }

        [Test] public void RoomKeepsAuthoredTintWhenTextureIsPresent()
        {
            var manifest = JsonUtility.FromJson<Manifest>(Resources.Load<TextAsset>("EndoscopyRoom/manifest").text);
            var materials = GameObject.Find("VirtualWashingRoom").GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            foreach (var source in manifest.materials.Where(m => !string.IsNullOrEmpty(m.texture)))
            {
                var actual = materials.Single(m => m.name == source.name);
                Assert.That(actual.mainTexture, Is.Not.Null, source.name);
                Assert.That(actual.color.r, Is.EqualTo(source.color[0]).Within(.001f), source.name + " must not become a full-white albedo multiplier when its texture loads.");
                Assert.That(actual.color.g, Is.EqualTo(source.color[1]).Within(.001f));
                Assert.That(actual.color.b, Is.EqualTo(source.color[2]).Within(.001f));
            }
        }

        [Test] public void RoomSurfacesOccludeFromBothSides()
        {
            var materials = GameObject.Find("VirtualWashingRoom").GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Distinct();
            foreach (var material in materials)
            {
                Assert.That(material.GetFloat("_Cull"), Is.EqualTo(0), material.name + " must remain visible on the reverse side of the imported room surface.");
                Assert.That(material.GetFloat("_Surface"), Is.EqualTo(0));
                Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(1));
            }
        }

        [TestCase(.3f)] [TestCase(.1f)] [TestCase(.01f)]
        public void RoomCameraDoesNotCutAwayNearbyWalls(float originalNear)
        {
            _room.Dispose();
            var camera = _rig.GetComponent<Camera>(); camera.nearClipPlane = originalNear;
            var definition = JsonUtility.FromJson<MapDefinition>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/BotanicalGardenQR/Content/Published/VisitorMapDefinition.json").text);
            _room = VirtualRoomEnvironment.Create(_rig, null, definition);
            Assert.That(camera.nearClipPlane, Is.EqualTo(Mathf.Min(originalNear, .03f)).Within(.0001f), "Cap inherited MR distances without increasing an already smaller near plane.");
        }
        [Serializable] sealed class Manifest { public Entry[] materials; }
        [Serializable] sealed class Entry { public string name, texture; public float[] color; }
    }
}
