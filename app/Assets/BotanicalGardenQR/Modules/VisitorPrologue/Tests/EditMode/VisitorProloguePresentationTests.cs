using System.Linq;
using BotanicalGardenQR.Configuration.Runtime;
using BotanicalGardenQR.VisitorPrologue.Frontend;
using NUnit.Framework;
using Oculus.Interaction;
using UnityEditor;
using UnityEngine;

namespace BotanicalGardenQR.VisitorPrologue.Tests.EditMode
{
    public sealed class VisitorProloguePresentationTests
    {
        const string ThemePath = "Assets/BotanicalGardenQR/Content/Authoring/VisitorPrologueTheme.asset";
        [Test] public void InvitationHandprintGlowsAtTheRealContactSurfaceAndInstructionsFit()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorPrologueThemeAsset>(ThemePath);
            var instance = Object.Instantiate(theme.PresentationPrefab);
            var ritual = instance.GetComponentInChildren<FieldbookInvitationRitual>(true);
            var seal = instance.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "PlantSeal");
            var material = seal.GetComponent<Renderer>().sharedMaterial;
            var originalScale = seal.transform.localScale;
            try
            {
                ritual.Configure(theme); ritual.PresentAvailable(); ritual.Tick(1.5f);
                Assert.That(seal.sharedMesh.name, Does.Contain("handprint"));
                Assert.That(seal.transform.position, Is.EqualTo(ritual.InvitationWorldOrigin));
                Assert.That(seal.gameObject.activeSelf, Is.True);
                var glow = seal.GetComponent<Renderer>().sharedMaterial;
                Assert.That(glow, Is.Not.SameAs(material)); Assert.That(glow.IsKeywordEnabled("_EMISSION"), Is.True);
                Assert.That(glow.GetColor("_EmissionColor").maxColorComponent, Is.GreaterThan(.5f));
                var text = instance.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t => t.name == "GuideSubHint");
                VisitorProloguePresenter.ConfigureInstructionLayout(text); text.text = ritual.Instruction;
                Assert.That(text.text, Does.Contain("手印").And.Contain("掌心").And.Contain("约1秒"));
                Assert.That(text.GetPreferredValues(text.text, text.rectTransform.rect.width, 0).y,
                    Is.LessThanOrEqualTo(text.rectTransform.rect.height));
                ritual.PresentOpening(); Assert.That(seal.gameObject.activeSelf, Is.False);
                ritual.Unconfigure();
                Assert.That(seal.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(seal.transform.localScale, Is.EqualTo(originalScale));
            }
            finally { ritual.Unconfigure(); Object.DestroyImmediate(instance); }
        }
        [Test]
        public void InvitationOwnsOneReachableBookAndCompleteAuthoredEncounter()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorPrologueThemeAsset>(ThemePath);
            Assert.That(theme.IsValid(out var error), Is.True, error);
            Assert.That(theme.ViewerDistance, Is.InRange(.45f, .65f));
            Assert.That(theme.Copy.EncounterPageCount, Is.EqualTo(4));
            Assert.That(theme.Copy.InvitationDetail, Does.Contain("手掌"));
            Assert.That(theme.Copy.CuriousResponse, Is.Not.EqualTo(theme.Copy.CompanionResponse));
            Assert.That(theme.PresentationPrefab.GetComponentsInChildren<FieldbookInvitationRitual>(true), Has.Length.EqualTo(1));
            Assert.That(theme.PresentationPrefab.GetComponentsInChildren<PokeInteractable>(true), Is.Empty);
            Assert.That(theme.PresentationPrefab.GetComponentsInChildren<Transform>(true).Any(t =>
                t.name == "AwakeningSeed" || t.name == "StartAction" || t.name == "SeedTouchSurface"), Is.False);
        }
        [Test]
        public void GazeAndPalmUseOneInvitationAndOpeningRestoresTheImmutableBookMesh()
        {
            var theme = AssetDatabase.LoadAssetAtPath<VisitorPrologueThemeAsset>(ThemePath);
            var instance = Object.Instantiate(theme.PresentationPrefab);
            var ritual = instance.GetComponentInChildren<FieldbookInvitationRitual>(true);
            var mesh = instance.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "InvitationBookVisual");
            var source = mesh.sharedMesh;
            var original = source.vertices;
            try
            {
                ritual.Configure(theme);
                Assert.That(instance.GetComponentsInChildren<Transform>(true).Any(t => t.name == "InvitationContract"),
                    Is.False, "Use the actual palm contact plane without a duplicate circular magic array.");
                var invitations = 0; var openings = 0;
                ritual.InvitationRequested += _ => { invitations++; ritual.PresentOpening(); };
                ritual.BookOpened += () => openings++;
                ritual.PresentAvailable();
                Assert.That(mesh.sharedMesh, Is.Not.SameAs(source));
                Assert.That(mesh.sharedMesh.vertices, Is.EqualTo(original), "The source prop already represents a closed book.");
                ritual.BeginGazeInvitation();
                Assert.That(invitations, Is.Zero, "The book must materialize before accepting an invitation.");
                ritual.Tick(1.5f);
                ritual.BeginGazeInvitation(); ritual.BeginGazeInvitation();
                Assert.That(invitations, Is.EqualTo(1));
                ritual.Tick(theme.BookOpenSeconds + .01f);
                ritual.Tick(.25f);
                Assert.That(mesh.gameObject.activeSelf, Is.True, "The book must visibly withdraw rather than vanish at confirmation.");
                ritual.Tick(.35f);
                Assert.That(mesh.gameObject.activeSelf, Is.False, "The invitation must clear the view before the other world opens.");
                ritual.PresentOpening();
                Assert.That(mesh.gameObject.activeSelf, Is.False, "A repeated arrival state must not bring the book back into the cinematic.");
                Assert.That(openings, Is.EqualTo(1));
                Assert.That(mesh.sharedMesh.vertices, Is.Not.EqualTo(original), "Opening moves only the owned front cover.");
                Assert.That(source.vertices, Is.EqualTo(original));
                ritual.PresentHidden();
                ritual.BeginGazeInvitation();
                Assert.That(invitations, Is.EqualTo(1));
                ritual.Unconfigure();
                Assert.That(mesh.sharedMesh, Is.SameAs(source));
            }
            finally { ritual.Unconfigure(); Object.DestroyImmediate(instance); }
        }
    }
}
