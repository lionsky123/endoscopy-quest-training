using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BotanicalGardenQR.Tests.EditMode.Bootstrap
{
    public sealed class SilentVisitorAudioTests
    {
        [Test]
        public void SilentVisitorSatisfiesSdkRoomDiscoveryWithoutCallingNativeAudio()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/BotanicalGardenQR/Scenes/Visitor/BotanicalGardenVisitor.unity", OpenSceneMode.Additive);
            try
            {
                var rooms = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<MetaXRAudioRoomAcousticProperties>(true)).ToArray();
                Assert.That(rooms, Has.Length.EqualTo(1));
                Assert.That(rooms[0].gameObject.activeInHierarchy, Is.True,
                    "The SDK startup scan must discover the disabled room component.");
                Assert.That(rooms[0].enabled, Is.False, "The silent experience must never run the acoustic Update.");
                // The installed SDK otherwise creates a temporary component, calls native
                // audio immediately, and leaks that updater if the disabled library throws.
                var initialize = typeof(MetaXRAudioRoomAcousticProperties).GetMethod(
                    "CheckSceneHasRoom", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(initialize, Is.Not.Null);
                Assert.DoesNotThrow(() => initialize.Invoke(null, null));
                Assert.That(Object.FindObjectsByType<MetaXRAudioRoomAcousticProperties>(FindObjectsSortMode.None),
                    Has.Length.EqualTo(1));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
