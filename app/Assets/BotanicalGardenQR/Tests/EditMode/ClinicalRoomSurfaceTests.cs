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
        [TestCase("R02_STORAGE")]
        [TestCase("R03_WAITING")]
        public void DerivedRoomLightingIsBoundedRoomOwnedAndRestoresEnvironment(string id)
        {
            _room.Dispose();
            var mode=UnityEngine.RenderSettings.ambientMode;
            var sky=RenderSettings.ambientSkyColor;var equator=RenderSettings.ambientEquatorColor;var ground=RenderSettings.ambientGroundColor;
            var map=FullScriptRoomCatalog.Map(id,null,false,true);
            _room=VirtualRoomEnvironment.Create(_rig,null,map);
            Assert.That(RenderSettings.ambientMode,Is.EqualTo(UnityEngine.Rendering.AmbientMode.Trilight));
            var lights=_room.Root.GetComponentsInChildren<Light>();Assert.That(lights.Length,Is.EqualTo(4));
            var source=Resources.Load<GameObject>(map.roomResource);
            CollectionAssert.AreEquivalent(source.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials),
                _room.Root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials));
            foreach(var light in lights)
            {
                Assert.That(light.type,Is.EqualTo(LightType.Spot));Assert.That(light.shadows,Is.EqualTo(LightShadows.None));
                Assert.That(light.transform.localPosition.y,Is.EqualTo(2.70f));
                Assert.That(light.range,Is.LessThanOrEqualTo(4.5f));
                Assert.That(Vector3.Dot(light.transform.forward,-_room.Root.transform.up),Is.GreaterThan(.99f));
                var position=light.transform.position;_rig.transform.position+=Vector3.right*.1f;
                Assert.That(light.transform.position,Is.EqualTo(position),"Room illumination must not follow the viewer.");
            }
            _room.Dispose();
            Assert.That(lights.All(light=>!light),Is.True,"Unloading the room must remove every local emitter.");
            Assert.That(RenderSettings.ambientMode,Is.EqualTo(mode));
            Assert.That(RenderSettings.ambientSkyColor,Is.EqualTo(sky));
            Assert.That(RenderSettings.ambientEquatorColor,Is.EqualTo(equator));
            Assert.That(RenderSettings.ambientGroundColor,Is.EqualTo(ground));
        }
        [Serializable] sealed class Manifest { public Entry[] materials; }
        [Serializable] sealed class Entry { public string name, texture; public float[] color; }
    }
}
