using BotanicalGardenQR.FrontendShell.Contracts;
using BotanicalGardenQR.Bootstrap;
using NUnit.Framework;

namespace BotanicalGardenQR.Tests.EditMode
{
    public sealed class FullScriptGuideBindingTests
    {
        [Test]
        public void FirstLobbyCopyWelcomesAndStartsTheGuidedRoute()
        {
            var copy = FullScriptGuideCopy.Resolve("大厅", firstLobbyVisit: true, finalOfficeVisit: false);

            Assert.That(copy.Chapter, Is.EqualTo("欢迎"));
            Assert.That(copy.Body, Does.Contain("原地观察、查阅和操作"));
            Assert.That(copy.Body, Does.Contain("先从办公室开始"));
            Assert.That(copy.Body.Length, Is.LessThanOrEqualTo(75));
            Assert.That(copy.ActionLabel, Is.EqualTo("开始学习"));
        }

        [Test]
        public void RoomEntryCopyLeadsDirectlyToThatRoomsInspection()
        {
            var copy = FullScriptGuideCopy.Resolve("清洗消毒室", firstLobbyVisit: false, finalOfficeVisit: false);

            Assert.That(copy.Chapter, Is.EqualTo("清洗消毒室"));
            Assert.That(copy.Body, Does.Contain("查看本室检查项目"));
            Assert.That(copy.Body, Does.Contain("观察和操作"));
            Assert.That(copy.Body, Does.Not.Contain("门口"));
            Assert.That(copy.ActionLabel, Is.EqualTo("开始本室检查"));
        }

        [Test]
        public void FinalOfficeCopyInvitesReviewBeforeSubmission()
        {
            var copy = FullScriptGuideCopy.Resolve("办公室", firstLobbyVisit: false, finalOfficeVisit: true);

            Assert.That(copy.Body, Does.Contain("逐项回看"));
            Assert.That(copy.Body, Does.Contain("未完成和待补内容"));
            Assert.That(copy.ActionLabel, Is.EqualTo("查看汇总"));
        }

        [Test]
        public void EntrySurfaceIsSinglePageWithOneHandActionAndNoReplay()
        {
            var context = new VisitorDialogueContextId("guide:test");
            var copy = FullScriptGuideCopy.Resolve("大厅", firstLobbyVisit: true, finalOfficeVisit: false);

            var state = FullScriptGuideBinding.CreateSurfaceState(1, context, copy);

            Assert.That(state.Owner, Is.EqualTo(VisitorDialogueOwner.Guidance));
            Assert.That(state.Speaker, Is.EqualTo("安小卫"));
            Assert.That(state.Mode, Is.EqualTo(VisitorDialogueSurfaceMode.Dialogue));
            Assert.That(state.PageIndex, Is.Zero);
            Assert.That(state.PageCount, Is.EqualTo(1));
            Assert.That(state.AllowRestart, Is.False);
            Assert.That(state.AllowDefer, Is.False);
            Assert.That(state.PrimaryIntent, Is.EqualTo(VisitorDialogueIntentKind.ChoosePrimary));
            Assert.That(state.PrimaryActionLabel, Is.EqualTo("开始学习"));
            Assert.That(state.Expression, Is.EqualTo(VisitorDialogueExpression.Welcome));
        }
    }
}
