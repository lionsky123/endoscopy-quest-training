using BotanicalGardenQR.Fairy.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Fairy
{
    public sealed class FairyArrivalResonanceTests
    {
        GameObject _root, _viewer;
        FairyArrivalResonance _echo;
        Vector3? _hand;
        int _connected;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ResonanceTest"); _viewer = new GameObject("Viewer");
            _viewer.transform.position = new Vector3(0, 1.6f, 0);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/BotanicalGardenQR/Content/Shared/Fairy/Arrival/FairyArrivalDiscoveryMask.prefab");
            _echo = _root.AddComponent<FairyArrivalResonance>();
            EditorUtility.CopySerialized(prefab.GetComponent<FairyArrivalResonance>(), _echo);
            _echo.Initialize(_viewer.transform, new Vector3(0, .8f, 2), _ => _hand);
            _echo.Changed += key => { if (key == "echo-connected") _connected++; };
            _hand = null; _connected = 0;
            Tick(30);
        }
        void Tick(int frames) { for (var i = 0; i < frames; i++) _echo.Tick(.05f, true); }
        [Test]
        public void OneTouchStartsFeedback_ThenConnectsOnceAfterTheRupture()
        {
            Tick(600);
            Assert.That(_echo.Touched, Is.False);
            Assert.That(_echo.Completed, Is.False);
            _hand = _echo.Position; Tick(1);
            Assert.That(_echo.Touched, Is.True, "Touch requires no hold or displacement.");
            Assert.That(_echo.Completed, Is.False, "The portal waits for the spatial discharge.");
            _hand = null; Tick(30);
            Assert.That(_echo.Completed, Is.False);
            Tick(35);
            Assert.That(_echo.Completed, Is.True);
            Tick(40);
            Assert.That(_connected, Is.EqualTo(1));
        }

        [Test]
        public void MissingOrDistantTrackingDoesNotTouchTheArtifact()
        {
            _hand = null; Tick(10);
            _hand = _echo.Position + Vector3.forward; Tick(12);
            Assert.That(_echo.Touched, Is.False);
            _hand = new Vector3(float.NaN, 0, 0); Tick(1);
            Assert.That(_echo.Touched, Is.False);
            _hand = _echo.Position; Tick(1);
            Assert.That(_echo.Touched, Is.True);
        }

        [Test]
        public void GazeFallbackRequiresSustainedLookingAtTheVisibleEcho()
        {
            Tick(180);
            Assert.That(_echo.Completed, Is.False);
            _viewer.transform.LookAt(_echo.Position); Tick(12);
            _viewer.transform.rotation = Quaternion.identity; Tick(2);
            Assert.That(_echo.Completed, Is.False);
            _viewer.transform.LookAt(_echo.Position); Tick(32);
            Assert.That(_echo.Touched, Is.True); Tick(65);
            Assert.That(_echo.Completed, Is.True);
            Assert.That(_connected, Is.EqualTo(1));
        }
        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_root); Object.DestroyImmediate(_viewer); }
    }
}
