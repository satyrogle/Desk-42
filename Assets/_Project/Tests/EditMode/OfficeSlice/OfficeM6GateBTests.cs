using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM6GateBTests
    {
        [Test]
        public void FreshProfileReceivesFirstShiftGuidance()
        {
            var onboarding = new OfficeM6Onboarding();

            Assert.That(onboarding.Visible, Is.True);
            Assert.That(onboarding.Step, Is.EqualTo(OfficeM6TutorialStep.Move));
            Assert.That(onboarding.CurrentSentence, Is.Not.Empty);
            Assert.That(onboarding.CurrentSentence, Does.Not.Contain("\n"));
            Assert.That(onboarding.HighlightId, Is.EqualTo("warden"));
        }

        [Test]
        public void GuidanceAdvancesOnlyAfterObservedAction()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeSimulationState state = campaign.CurrentSimulation;
            var onboarding = new OfficeM6Onboarding();

            onboarding.Observe(state, campaign);
            Assert.That(onboarding.Step, Is.EqualTo(OfficeM6TutorialStep.Move));

            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            intent.SetMovement(OfficeInputDirection.Right);
            input.AdvanceOneTick();
            onboarding.Observe(state, campaign);

            Assert.That(onboarding.Step,
                Is.EqualTo(OfficeM6TutorialStep.TakeFile));
            Assert.That(onboarding.CurrentSentence,
                Does.Contain("TAKE THE FILE"));
        }

        [Test]
        public void GuidanceDoesNotRepeatAfterCompletion()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            var onboarding = new OfficeM6Onboarding(completedPreviously: true);

            onboarding.Observe(campaign.CurrentSimulation, campaign);

            Assert.That(onboarding.Complete, Is.True,
                "Observed tutorial step: " + onboarding.Step);
            Assert.That(onboarding.Visible, Is.False);
            Assert.That(onboarding.CurrentSentence, Is.Empty);
        }

        [Test]
        public void ReturningPlayerCanDisableGuidance()
        {
            var onboarding = new OfficeM6Onboarding();

            onboarding.SetHintsEnabled(false);

            Assert.That(onboarding.HintsEnabled, Is.False);
            Assert.That(onboarding.Visible, Is.False);
            Assert.That(onboarding.CurrentSentence, Is.Empty);
        }

        [Test]
        public void TutorialStateDoesNotChangeSimulationChecksum()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            string checksum = campaign.Checksum;
            var onboarding = new OfficeM6Onboarding();

            onboarding.Observe(campaign.CurrentSimulation, campaign);
            onboarding.SetHintsEnabled(false);
            onboarding.SetHintsEnabled(true);

            Assert.That(campaign.Checksum, Is.EqualTo(checksum));
        }

        [Test]
        public void FirstShiftCanCompleteWithoutDeveloperHud()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            var onboarding = new OfficeM6Onboarding();
            var presenter = new OfficeM6HudPresenter();

            OfficeCampaignCaptureDriver.Prepare(
                campaign, 1, "06-shift-1-upgrade-choice");
            onboarding.Observe(campaign.CurrentSimulation, campaign);
            OfficeM6HudModel model = presenter.Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard);
            onboarding.ApplyTo(model);

            Assert.That(campaign.Phase,
                Is.EqualTo(OfficeCampaignPhase.ChooseUpgrade));
            Assert.That(presenter.DevelopmentHudVisible, Is.False);
            Assert.That(model.DevelopmentHudVisible, Is.False);
            Assert.That(campaign.CurrentSimulation.Shift.Phase,
                Is.EqualTo(OfficeShiftPhase.Result));
        }
    }
}
