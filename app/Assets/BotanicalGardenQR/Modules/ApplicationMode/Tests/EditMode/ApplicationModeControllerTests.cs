using System;
using BotanicalGardenQR.ApplicationMode.Contracts;
using BotanicalGardenQR.ApplicationMode.Runtime;
using NUnit.Framework;

namespace BotanicalGardenQR.ApplicationMode.Tests.EditMode
{
    public sealed class ApplicationModeControllerTests
    {
        [Test]
        public void ShortPress_ReturnsToIdleWithoutLoadingScene()
        {
            var loader = new FakeSceneLoader();
            using (var controller = CreateController(ApplicationModeRole.Visitor, loader))
            {
                controller.Advance(0.6f, true);
                controller.Advance(0.59f, true);
                Assert.That(controller.CurrentState.Phase, Is.EqualTo(ApplicationModePhase.Holding));

                controller.Advance(0f, false);

                Assert.That(controller.CurrentState.Phase, Is.EqualTo(ApplicationModePhase.Idle));
                Assert.That(controller.CurrentState.Prompt, Is.EqualTo(ApplicationModePromptKind.None));
                Assert.That(loader.LoadCount, Is.Zero);
            }
        }

        [Test]
        public void VisitorFullHold_LoadsAdministratorDirectlyWithoutPrompt()
        {
            var loader = new FakeSceneLoader();
            using (var controller = CreateController(ApplicationModeRole.Visitor, loader))
            {
                controller.Advance(1.2f, true);

                Assert.That(controller.CurrentState.Phase, Is.EqualTo(ApplicationModePhase.LoadingScene));
                Assert.That(controller.CurrentState.Prompt, Is.EqualTo(ApplicationModePromptKind.None));
                Assert.That(loader.TargetRole, Is.EqualTo(ApplicationModeRole.Administrator));
                Assert.That(loader.LoadCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void VisitorLoadFailure_StaysUsableAndRequiresReleaseBeforeRetry()
        {
            var loader = new FakeSceneLoader();
            using (var controller = CreateController(ApplicationModeRole.Visitor, loader))
            {
                controller.Advance(1.2f, true);
                loader.Complete(ApplicationModeLoadResult.Failed);

                Assert.That(controller.CurrentState.Phase, Is.EqualTo(ApplicationModePhase.Idle));
                Assert.That(controller.CurrentState.Prompt, Is.EqualTo(ApplicationModePromptKind.None));
                Assert.That(controller.CurrentState.Fault, Is.EqualTo(ApplicationModeFault.SceneLoadFailed));

                controller.Advance(2f, true);
                Assert.That(loader.LoadCount, Is.EqualTo(1));

                controller.Advance(0f, false);
                controller.Advance(1.2f, true);
                Assert.That(loader.LoadCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void SuccessfulVisitorTransition_CompletesAsAdministrator()
        {
            var loader = new FakeSceneLoader();
            using (var controller = CreateController(ApplicationModeRole.Visitor, loader))
            {
                controller.Advance(1.2f, true);
                loader.Complete(ApplicationModeLoadResult.Success);

                Assert.That(controller.CurrentState.Role, Is.EqualTo(ApplicationModeRole.Administrator));
                Assert.That(controller.CurrentState.Phase, Is.EqualTo(ApplicationModePhase.Idle));
            }
        }

        [Test]
        public void AdministratorHold_UsesReturnConfirmation()
        {
            var loader = new FakeSceneLoader();
            using (var controller = CreateController(ApplicationModeRole.Administrator, loader))
            {
                controller.Advance(1.2f, true);

                Assert.That(controller.CurrentState.Phase, Is.EqualTo(ApplicationModePhase.Prompt));
                Assert.That(controller.CurrentState.Prompt, Is.EqualTo(ApplicationModePromptKind.ReturnToVisitor));
                Assert.That(controller.CurrentState.CanConfirm, Is.True);

                controller.Dispatch(ApplicationModeIntent.Confirm);

                Assert.That(loader.TargetRole, Is.EqualTo(ApplicationModeRole.Visitor));
            }
        }

        [Test]
        public void AdministratorLoadFailure_RestoresReturnPromptAndAllowsRetry()
        {
            var loader = new FakeSceneLoader();
            using (var controller = CreateController(ApplicationModeRole.Administrator, loader))
            {
                OpenReturnPromptAndRelease(controller);
                controller.Dispatch(ApplicationModeIntent.Confirm);
                loader.Complete(ApplicationModeLoadResult.Failed);

                Assert.That(controller.CurrentState.Phase, Is.EqualTo(ApplicationModePhase.Prompt));
                Assert.That(controller.CurrentState.Prompt, Is.EqualTo(ApplicationModePromptKind.ReturnToVisitor));
                Assert.That(controller.CurrentState.Fault, Is.EqualTo(ApplicationModeFault.SceneLoadFailed));

                controller.Dispatch(ApplicationModeIntent.Confirm);
                Assert.That(loader.LoadCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void ReturnPrompt_CanCancelOrTimeOutWithoutLoading()
        {
            var loader = new FakeSceneLoader();
            using (var controller = CreateController(ApplicationModeRole.Administrator, loader))
            {
                OpenReturnPromptAndRelease(controller);
                controller.Dispatch(ApplicationModeIntent.Cancel);
                Assert.That(controller.CurrentState.Phase, Is.EqualTo(ApplicationModePhase.Idle));

                OpenReturnPromptAndRelease(controller);
                controller.Advance(30f, false);
                Assert.That(controller.CurrentState.Phase, Is.EqualTo(ApplicationModePhase.Idle));
                Assert.That(loader.LoadCount, Is.Zero);
            }
        }

        [Test]
        public void Observe_WhenInitialSinkThrows_RemovesFailedSubscription()
        {
            var failed = new RecordingSink(throwOnCall: 1);
            using (var controller = CreateController(
                       ApplicationModeRole.Visitor,
                       new FakeSceneLoader()))
            {
                Assert.Throws<InvalidOperationException>(() => controller.Observe(failed));
                Assert.DoesNotThrow(() => controller.Advance(0.1f, true));
            }

            Assert.That(failed.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Publish_WhenOneSinkThrows_ContinuesToOtherSubscribers()
        {
            var failed = new RecordingSink(throwOnCall: 2);
            var healthy = new RecordingSink();
            using (var controller = CreateController(
                       ApplicationModeRole.Visitor,
                       new FakeSceneLoader()))
            using (controller.Observe(failed))
            using (controller.Observe(healthy))
            {
                Assert.DoesNotThrow(() => controller.Advance(0.1f, true));

                Assert.That(failed.CallCount, Is.EqualTo(2));
                Assert.That(healthy.CallCount, Is.EqualTo(2));
                Assert.That(healthy.LastState.Version, Is.GreaterThan(0));
                Assert.That(healthy.LastState.Phase, Is.EqualTo(ApplicationModePhase.Holding));
            }
        }

        static ApplicationModeController CreateController(
            ApplicationModeRole role,
            FakeSceneLoader loader) =>
            new ApplicationModeController(role, 1.2f, 30f, loader);

        static void OpenReturnPromptAndRelease(ApplicationModeController controller)
        {
            controller.Advance(1.2f, true);
            controller.Advance(0f, false);
        }

        sealed class FakeSceneLoader : IApplicationModeSceneLoader
        {
            Action<ApplicationModeLoadResult> _completed;

            public int LoadCount { get; private set; }
            public ApplicationModeRole TargetRole { get; private set; }

            public void Load(
                ApplicationModeRole targetRole,
                Action<ApplicationModeLoadResult> completed)
            {
                LoadCount++;
                TargetRole = targetRole;
                _completed = completed;
            }

            public void Complete(ApplicationModeLoadResult result)
            {
                var completed = _completed;
                _completed = null;
                completed?.Invoke(result);
            }
        }

        sealed class RecordingSink : IApplicationModeStateSink
        {
            readonly int _throwOnCall;

            public RecordingSink(int throwOnCall = -1) { _throwOnCall = throwOnCall; }

            public int CallCount { get; private set; }
            public ApplicationModeState LastState { get; private set; }

            public void OnApplicationModeStateChanged(ApplicationModeState state)
            {
                CallCount++;
                if (CallCount == _throwOnCall)
                    throw new InvalidOperationException("test sink failure");
                LastState = state;
            }
        }
    }
}
