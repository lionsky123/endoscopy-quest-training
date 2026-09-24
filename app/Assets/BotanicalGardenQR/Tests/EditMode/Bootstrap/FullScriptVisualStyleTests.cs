using System.Reflection;
using BotanicalGardenQR.FrontendShell.Runtime;
using BotanicalGardenQR.Configuration.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class FullScriptVisualStyleTests
    {
        [Test]
        public void SharedClinicalPanelsSeparateDarkShellTintedChoicesAndReadablePaper()
        {
            Assert.That(ClinicalPanelStyle.Shell.r, Is.LessThan(.2f));
            Assert.That(ClinicalPanelStyle.Surface.r, Is.GreaterThan(.9f));
            Assert.That(ClinicalPanelStyle.Surface.g, Is.GreaterThan(.9f));
            Assert.That(ClinicalPanelStyle.Surface.b, Is.GreaterThan(.88f));
            Assert.That(Contrast(ClinicalPanelStyle.ShellText, ClinicalPanelStyle.Shell), Is.GreaterThanOrEqualTo(7f));
            Assert.That(Contrast(ClinicalPanelStyle.TextPrimary, ClinicalPanelStyle.SurfaceSubtle), Is.GreaterThanOrEqualTo(7f));
            Assert.That(Contrast(ClinicalPanelStyle.ShellText, ClinicalPanelStyle.Accent), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(Contrast(ClinicalPanelStyle.TextPrimary, ClinicalPanelStyle.Surface), Is.GreaterThanOrEqualTo(7f));
            Assert.That(Contrast(ClinicalPanelStyle.Muted, ClinicalPanelStyle.Surface), Is.GreaterThanOrEqualTo(4.5f));
            Assert.That(Contrast(ClinicalPanelStyle.Accent, ClinicalPanelStyle.Surface), Is.GreaterThanOrEqualTo(4.5f));
        }

        [Test]
        public void WelcomeDialogueUsesTheSameSurfaceTextAndAccentRolesAsTheRoomAndOfficePanels()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorCoachThemeAsset>(
                "Assets/BotanicalGardenQR/Content/Authoring/VisitorCoachTheme.asset");

            Assert.That(theme, Is.Not.Null);
            AssertColor(theme.PanelColor, ClinicalPanelStyle.Surface);
            AssertColor(theme.TextColor, ClinicalPanelStyle.TextPrimary);
            AssertColor(theme.DetailTextColor, ClinicalPanelStyle.Muted);
            AssertColor(theme.AccentColor, ClinicalPanelStyle.Accent);
        }

        [Test]
        public void DisabledActionStopsLookingLikeAnAvailablePrimaryButton()
        {
            var root=new GameObject("Disabled visual",typeof(RectTransform),typeof(Image),typeof(Button));
            try
            {
                ((RectTransform)root.transform).sizeDelta=new Vector2(220,60);
                var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));
                label.transform.SetParent(root.transform,false);
                var button=root.GetComponent<Button>();
                button.targetGraphic=root.GetComponent<Image>();
                var visual=root.AddComponent<ClinicalChoiceVisual>();
                visual.Initialize(button);
                visual.SetState(false,true);
                Assert.That(button.targetGraphic.color,Is.EqualTo(ClinicalPanelStyle.Accent));
                button.interactable=false;
                typeof(ClinicalChoiceVisual).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic)
                    .Invoke(visual,null);
                Assert.That(button.targetGraphic.color.r,Is.GreaterThan(.7f));
                Assert.That(label.GetComponent<TMP_Text>().color,Is.EqualTo(ClinicalPanelStyle.Muted));
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.001f));
        }

        static float Contrast(Color foreground, Color background)
        {
            var a = Luminance(foreground);
            var b = Luminance(background);
            return (Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f);
        }

        static float Luminance(Color color)
        {
            static float Linear(float value) => value <= .04045f ? value / 12.92f : Mathf.Pow((value + .055f) / 1.055f, 2.4f);
            return .2126f * Linear(color.r) + .7152f * Linear(color.g) + .0722f * Linear(color.b);
        }
    }
}
