using System.Reflection;
using BotanicalGardenQR.VisitorCoach.Frontend;
using NUnit.Framework;
using Oculus.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.VisitorCoach.Tests.EditMode
{
    public sealed class VisitorDialoguePointableLifecycleTests
    {
        [TestCase("disable")]
        [TestCase("focus")]
        [TestCase("pause")]
        [TestCase("first-activation")]
        public void ReenabledOpeningTargetAcceptsFreshTouchWhenReleaseWasMissed(string boundary)
        {
            var root = new GameObject("Opening dialogue touch", typeof(RectTransform), typeof(Image));
            root.SetActive(false);
            try
            {
                var source = root.AddComponent<PointableElement>();
                Invoke(source, "Awake");
                var target = root.AddComponent<VisitorDialoguePointableTarget>();
                var serialized = new SerializedObject(target);
                serialized.FindProperty("_pointableObject").objectReferenceValue = source;
                serialized.FindProperty("_feedbackGraphic").objectReferenceValue = root.GetComponent<Image>();
                var volumes = serialized.FindProperty("_hitVolumes");
                volumes.arraySize = 1;
                volumes.GetArrayElementAtIndex(0).objectReferenceValue = root.AddComponent<BoxCollider>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (boundary == "first-activation") target.SetArmed(true);
                Invoke(target, "Awake");
                Invoke(target, "OnEnable");
                if (boundary != "first-activation") target.SetArmed(true);
                var accepted = 0;
                target.Selected += () => accepted++;
                for (var cycle = 0; cycle < 20; cycle++)
                {
                    source.ProcessPointerEvent(new PointerEvent(cycle, PointerEventType.Hover, Pose.identity));
                    source.ProcessPointerEvent(new PointerEvent(cycle, PointerEventType.Select, Pose.identity));
                    Assert.That(accepted, Is.EqualTo(cycle + 1), "A fresh touch after reopening must advance the opening dialogue.");
                    source.ProcessPointerEvent(new PointerEvent(cycle, PointerEventType.Select, Pose.identity));
                    Assert.That(accepted, Is.EqualTo(cycle + 1), "Holding a finger still must not advance twice.");
                    if (boundary == "disable")
                    {
                        // Teardown ordering: the target unsubscribes before the hand cancels.
                        Invoke(target, "OnDisable");
                        source.ProcessPointerEvent(new PointerEvent(cycle, PointerEventType.Cancel, Pose.identity));
                        Invoke(target, "OnEnable");
                    }
                    else if (boundary != "first-activation")
                    {
                        // A suspended hand source may never deliver the previous release.
                        var method = boundary == "focus" ? "OnApplicationFocus" : "OnApplicationPause";
                        Invoke(target, method, false);
                        Invoke(target, method, true);
                    }
                    else source.ProcessPointerEvent(new PointerEvent(cycle, PointerEventType.Unselect, Pose.identity));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void Invoke(object target, string method, params object[] args)
            => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, args);
    }
}
