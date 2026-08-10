using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.OfficeSlice
{
    public sealed class OfficeM6GateCTests
    {
        [Test]
        public void PlayerCopyCatalogContainsNoBannedInternalTerms()
        {
            foreach (string playerText in
                OfficeM6PlayerCopyCatalog.StaticPlayerStrings)
                foreach (string banned in
                    OfficeM6PlayerCopyCatalog.BannedInternalTerms)
                    Assert.That(playerText, Does.Not.Contain(banned).IgnoreCase,
                        playerText);
        }

        [Test]
        public void ActionLabelsStayWithinUiBudget()
        {
            foreach (string action in
                OfficeM6PlayerCopyCatalog.CanonicalActionLabels)
                Assert.That(action.Length,
                    Is.LessThanOrEqualTo(
                        OfficeM6PlayerCopyCatalog.ActionLabelCharacterBudget),
                    action);
        }

        [Test]
        public void WhatHappenedUsesObservablePlainLanguage()
        {
            var states = new[]
            {
                Prepared(1, "05-shift-1-copy-echo-break"),
                Prepared(2, "08-shift-2-ghost-clock"),
                Prepared(2, "09-shift-2-missing-room-access"),
                Prepared(3, "13-shift-3-promotion-cascade"),
            };

            for (int i = 0; i < states.Length; i++)
            {
                string text = OfficeM6PlayerCopyCatalog.WhatHappened(states[i]);
                Assert.That(text, Is.Not.Empty);
                Assert.That(text, Does.Contain("THE"));
                foreach (string banned in
                    OfficeM6PlayerCopyCatalog.BannedInternalTerms)
                    Assert.That(text, Does.Not.Contain(banned).IgnoreCase);
            }
        }

        [Test]
        public void RuleTextRemainsExactAndReadable()
        {
            Assert.That(OfficeM6PlayerCopyCatalog.RuleOne,
                Is.EqualTo(OfficeAutomationRuleState.PlayerRule));
            Assert.That(OfficeM6PlayerCopyCatalog.RuleTwo,
                Is.EqualTo(OfficePayrollRuleState.PlayerRule));
            Assert.That(OfficeM6PlayerCopyCatalog.RuleOne.Length,
                Is.LessThanOrEqualTo(64));
            Assert.That(OfficeM6PlayerCopyCatalog.RuleTwo.Length,
                Is.LessThanOrEqualTo(64));
        }

        [Test]
        public void PrimaryPromptMatchesCurrentAction()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeSimulationState state = campaign.CurrentSimulation;

            OfficeM6HudModel model = new OfficeM6HudPresenter().Project(
                state, campaign, OfficeM6ControlScheme.Keyboard);

            Assert.That(model.ActionPrompt, Is.EqualTo(
                "E - " + OfficeM6PlayerCopyCatalog.Action(
                    state.PrimaryActionLabel)));
        }

        [Test]
        public void ControllerPromptsReplaceKeyboardPrompts()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            var presenter = new OfficeM6HudPresenter();

            string keyboard = presenter.Project(campaign.CurrentSimulation,
                campaign, OfficeM6ControlScheme.Keyboard).ActionPrompt;
            string controller = presenter.Project(campaign.CurrentSimulation,
                campaign, OfficeM6ControlScheme.Controller).ActionPrompt;

            Assert.That(keyboard, Does.StartWith("E - "));
            Assert.That(controller, Does.StartWith("A - "));
            Assert.That(controller.Substring(4), Is.EqualTo(keyboard.Substring(4)));
        }

        [Test]
        public void UnavailableActionsAreNotShownAsPrimary()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeM6HudModel model = new OfficeM6HudPresenter().Project(
                campaign.CurrentSimulation,
                campaign,
                OfficeM6ControlScheme.Keyboard);

            Assert.That(model.ActionPrompt, Does.Not.Contain("FIX COPIER"));
            Assert.That(model.ActionPrompt, Does.Not.Contain("RETURN ORIGINAL"));
            Assert.That(model.ActionPrompt, Does.Not.Contain("REMOVE STAMP"));
        }

        [Test]
        public void BreakRecoveryPromptTargetsObservableAction()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(
                campaign, 1, "05-shift-1-copy-echo-break");
            OfficeSimulationState state = campaign.CurrentSimulation;

            OfficeM6HudModel model = new OfficeM6HudPresenter().Project(
                state, campaign, OfficeM6ControlScheme.Keyboard);

            Assert.That(model.BreakCardVisible, Is.True);
            Assert.That(model.ActionPrompt, Is.EqualTo(
                OfficeM6PlayerCopyCatalog.Prompt(
                    state.PrimaryActionLabel,
                    OfficeM6ControlScheme.Keyboard)));
        }

        [Test]
        public void PromptSwitchingDoesNotChangeCommandStream()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeSimulationState state = campaign.CurrentSimulation;
            string checksum = campaign.Checksum;
            int commandCount = state.CommandLog.Commands.Count;
            var presenter = new OfficeM6HudPresenter();

            presenter.Project(state, campaign, OfficeM6ControlScheme.Keyboard);
            presenter.Project(state, campaign, OfficeM6ControlScheme.Controller);

            Assert.That(campaign.Checksum, Is.EqualTo(checksum));
            Assert.That(state.CommandLog.Commands.Count, Is.EqualTo(commandCount));
        }

        private static OfficeSimulationState Prepared(int shift, string stateName)
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeCampaignCaptureDriver.Prepare(campaign, shift, stateName);
            return campaign.CurrentSimulation;
        }
    }
}
