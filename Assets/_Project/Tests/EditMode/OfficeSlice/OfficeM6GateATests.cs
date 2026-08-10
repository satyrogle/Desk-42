using System;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM6GateATests
    {
        private static readonly (int Shift, string State)[] NormalStates =
        {
            (1, "01-shift-1-opening"),
            (1, "02-shift-1-paper-check"),
            (1, "03-shift-1-money-trace"),
            (1, "05-shift-1-copy-echo-break"),
            (2, "08-shift-2-ghost-clock"),
            (2, "09-shift-2-missing-room-access"),
            (3, "13-shift-3-promotion-cascade"),
            (3, "15-final-campaign-result"),
        };

        [Test]
        public void NormalHudContainsNoDebugIdentifiers()
        {
            var presenter = new OfficeM6HudPresenter();
            string[] forbidden =
            {
                "CHECKSUM", "CURRENTTICK", "CLAIM.", "CASE.", "CUSTOMER.",
                "INSTITUTIONAL", "AUTHORITY STATE", "PUBLIC BOUNDARY",
                "CHECKPOINT SCHEMA",
            };

            for (int i = 0; i < NormalStates.Length; i++)
            {
                OfficeCampaignState campaign = OfficeCampaignState.Create();
                OfficeCampaignCaptureDriver.Prepare(
                    campaign, NormalStates[i].Shift, NormalStates[i].State);
                string text = presenter.Project(
                    campaign.CurrentSimulation,
                    campaign,
                    OfficeM6ControlScheme.Keyboard).AllNormalPlayerText();
                for (int term = 0; term < forbidden.Length; term++)
                    Assert.That(text, Does.Not.Contain(forbidden[term])
                        .IgnoreCase, NormalStates[i].State);
            }
        }

        [Test]
        public void HudFitsAtAllTargetResolutions()
        {
            var presenter = new OfficeM6HudPresenter();
            Assert.That(presenter.Fits(1280, 720), Is.True);
            Assert.That(presenter.Fits(1600, 900), Is.True);
            Assert.That(presenter.Fits(1920, 1080), Is.True);
        }

        [Test]
        public void CriticalTargetsRemainVisible()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                campaign, 3, "13-shift-3-promotion-cascade");
            var presenter = new OfficeM6HudPresenter();
            OfficeM6HudModel model = presenter.Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard);

            Assert.That(presenter.CriticalTargetsRemainVisible(model, 1280, 720),
                Is.True);
            Assert.That(presenter.CriticalTargetsRemainVisible(model, 1600, 900),
                Is.True);
            Assert.That(presenter.CriticalTargetsRemainVisible(model, 1920, 1080),
                Is.True);
        }

        [Test]
        public void BreakPanelAppearsOnlyDuringBreak()
        {
            var presenter = new OfficeM6HudPresenter();
            OfficeCampaignState opening = OfficeCampaignState.Create();
            OfficeM6HudModel calm = presenter.Project(
                opening.CurrentSimulation,
                opening,
                OfficeM6ControlScheme.Keyboard);
            OfficeCampaignState broken = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                broken, 1, "05-shift-1-copy-echo-break");
            OfficeM6HudModel breakModel = presenter.Project(
                broken.CurrentSimulation,
                broken,
                OfficeM6ControlScheme.Keyboard);

            Assert.That(calm.BreakCardVisible, Is.False);
            Assert.That(breakModel.BreakCardVisible, Is.True);
            Assert.That(breakModel.RecoveryItems, Is.Not.Empty);
        }

        [Test]
        public void ResultScreenSuppressesOperationalHud()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                campaign, 3, "15-final-campaign-result");

            OfficeM6HudModel model = new OfficeM6HudPresenter().Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard);

            Assert.That(model.ResultVisible, Is.True);
            Assert.That(model.CustomerCardVisible, Is.False);
            Assert.That(model.CaseCardVisible, Is.False);
            Assert.That(model.RuleCardVisible, Is.False);
            Assert.That(model.BreakCardVisible, Is.False);
        }

        [Test]
        public void DeveloperHudIsOptIn()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            var presenter = new OfficeM6HudPresenter();

            Assert.That(presenter.DevelopmentHudVisible, Is.False);
            Assert.That(presenter.Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard).DevelopmentHudVisible,
                Is.False);

            presenter.SetDevelopmentHudVisible(true);
            Assert.That(presenter.Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard).DevelopmentHudVisible,
                Is.True);
        }
    }
}
