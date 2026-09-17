using System;
using System.Collections.Generic;
using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.Panorama.Contracts;
using BotanicalGardenQR.Panorama.Frontend;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Panorama.Tests.EditMode
{
    public sealed class PanoramaSpatialTutorialTests
    {
        const string SharedFontAssetPath =
            "Assets/BotanicalGardenQR/Resources/BotanicalGardenQR/Fonts/SourceHanSansSC-Regular SDF.asset";

        [Test]
        public void FirstUseHintAndFocusLabelAreLocalNonPersistentSurfaces()
        {
            var runtimeRoot = new GameObject("PanoramaTutorialRuntime");
            var viewer = new GameObject("PanoramaTutorialViewer");
            var gaze = new RecordingGazeRegistry();
            var exitCount = 0;
            PanoramaSpatialWorld world = null;
            try
            {
                viewer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                world = new PanoramaSpatialWorld(
                    runtimeRoot.transform,
                    viewer.transform,
                    gaze,
                    AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SharedFontAssetPath),
                    () => exitCount++,
                    false,
                    null,
                    Array.Empty<PanoramaEnvironmentMomentDefinition>());

                world.SetTutorialHint("转动身体观察四周；看住气泡即可选择", true);
                world.SetVisible(true);
                Assert.That(world.TutorialHintVisible, Is.True);
                Assert.That(world.TutorialHintText,
                    Is.EqualTo("转动身体观察四周；看住气泡即可选择"));

                var exitRegistration = gaze["PanoramaExitBubble"];
                exitRegistration.IsFocusedValue = true;
                world.Tick();
                Assert.That(world.ExitFocusLabelVisible, Is.True);
                Assert.That(world.ExitFocusLabelText, Is.EqualTo("返回观察"));

                exitRegistration.IsFocusedValue = false;
                world.Tick();
                Assert.That(world.ExitFocusLabelVisible, Is.False);

                var exitButton = FindButton(runtimeRoot, "PanoramaExitBubble");
                Assert.That(exitButton, Is.Not.Null);
                exitButton.onClick.Invoke();
                Assert.That(exitCount, Is.EqualTo(1));

                world.SetControlsVisible(false);
                Assert.That(world.TutorialHintVisible, Is.False);
            }
            finally
            {
                world?.Dispose();
                Assert.That(gaze.AllDisposed, Is.True);
                UnityEngine.Object.DestroyImmediate(runtimeRoot);
                UnityEngine.Object.DestroyImmediate(viewer);
            }
        }

        static Button FindButton(GameObject root, string ancestorName)
        {
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (var index = 0; index < buttons.Length; index++)
            {
                for (var current = buttons[index].transform; current != null; current = current.parent)
                    if (string.Equals(current.name, ancestorName, StringComparison.Ordinal))
                        return buttons[index];
            }
            return null;
        }

        sealed class RecordingGazeRegistry : IFrontendGazeSurfaceRegistry
        {
            public System.IDisposable SuspendPanelInput() => new Suspension();
            sealed class Suspension : System.IDisposable { public void Dispose() { } }
            readonly Dictionary<string, Registration> _registrations =
                new Dictionary<string, Registration>(StringComparer.Ordinal);

            public Registration this[string label] => _registrations[label];
            public bool AllDisposed
            {
                get
                {
                    foreach (var registration in _registrations.Values)
                        if (!registration.Disposed) return false;
                    return true;
                }
            }

            public IFrontendGazeSurfaceRegistration RegisterGazeSurface(
                Transform surfaceRoot,
                int priority,
                string label)
            {
                var registration = new Registration();
                _registrations.Add(label, registration);
                return registration;
            }
        }

        sealed class Registration : IFrontendGazeSurfaceRegistration
        {
            public void Invalidate() { }

            public bool IsFocusedValue { get; set; }
            public bool IsFocused => IsFocusedValue;
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }
    }
}
