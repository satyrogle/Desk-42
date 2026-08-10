using System;
using System.Collections.Generic;
using Desk42.Institutional.Player;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.OfficeM3
{
    public sealed class OfficeM3GateBTests
    {
        [Test]
        public void ShiftTwoAuthoredCasesUseFourChangedSequences()
        {
            OfficeM2Scenario scenario = CreateShiftTwoScenario();
            int changed = 0;
            for (int i = 0; i < scenario.Cases.Cases.Count; i++)
            {
                OfficeCaseWorkDefinition work = scenario.WorkFor(
                    scenario.Cases.Cases[i].AutomationClaimId);
                if (work.RequiredSequence.Count == 3) changed++;
            }

            Assert.That(changed, Is.GreaterThanOrEqualTo(4));
            Assert.That(scenario.Customers[4].DisplayName, Is.EqualTo("TOMAS REED"));
            Assert.That(scenario.Customers[4].Problem,
                Is.EqualTo("I DIED ON FRIDAY. PAYROLL SAYS I WORKED SATURDAY."));
        }

        [Test]
        public void FastTraysChangesExactTransferTicks()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeM3TestDriver.EnterShiftTwo(
                campaign, OfficeUpgradeFamily.FastTrays);
            OfficeSimulationState state = campaign.CurrentSimulation;
            string caseId = state.Cases.Cases[0].AutomationClaimId;

            Assert.That(state.Queues.TransferDurationTicks, Is.EqualTo(12));
            Assert.That(state.Queues.TryTransferCase(
                caseId, OfficeRoomId.PaperRoom, state.CurrentTick), Is.True);
            state.AdvanceTicks(11);
            Assert.That(state.Queues.GetFolder(caseId).IsMoving, Is.True);
            state.AdvanceOneTick();
            Assert.That(state.Queues.GetFolder(caseId).IsMoving, Is.False);
        }

        [Test]
        public void CalmChairsChangesExactMoodThresholds()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeM3TestDriver.EnterShiftTwo(
                campaign, OfficeUpgradeFamily.CalmChairs);
            OfficeSimulationState state = campaign.CurrentSimulation;
            OfficeCustomerState customer = state.Customers.ActiveDeskCustomer;

            state.AdvanceTicks(539);
            Assert.That(customer.VisibleMoodState,
                Is.EqualTo(OfficeVisibleMoodState.Calm));
            state.AdvanceOneTick();
            Assert.That(customer.VisibleMoodState,
                Is.EqualTo(OfficeVisibleMoodState.Worried));
            Assert.That(campaign.Upgrades.MoodThresholdBonusTicks, Is.EqualTo(90));
        }

        [Test]
        public void RuleTwoAcceptsTwoKnownPayrollCases()
        {
            OfficeSimulationState state = CreateRuleTwoEnabledState();
            string owen = state.Cases.Cases[1].AutomationClaimId;
            string june = state.Cases.Cases[5].AutomationClaimId;
            Assert.That(state.Queues.TryTransferCase(
                owen, OfficeRoomId.WeirdRoom, state.CurrentTick, 1), Is.True);
            Assert.That(state.Queues.TryTransferCase(
                june, OfficeRoomId.WeirdRoom, state.CurrentTick, 1), Is.True);

            state.AdvanceOneTick();
            state.AdvanceTicks(OfficePayrollRuleState.TransferDurationTicks);

            Assert.That(state.PayrollRule.Matches, Has.Count.EqualTo(2));
            Assert.That(state.PayrollRule.Matches[0].Matched, Is.True);
            Assert.That(state.PayrollRule.Matches[1].Matched, Is.True);
            Assert.That(state.Queues.GetFolder(owen).CurrentRoom,
                Is.EqualTo(OfficeRoomId.MoneyRoom));
            Assert.That(state.Queues.GetFolder(june).CurrentRoom,
                Is.EqualTo(OfficeRoomId.MoneyRoom));
        }

        [Test]
        public void RuleTwoReasonLogUsesPublicDataOnly()
        {
            OfficeSimulationState state = CreateRuleTwoEnabledState();
            string owen = state.Cases.Cases[1].AutomationClaimId;
            Assert.That(state.Queues.TryTransferCase(
                owen, OfficeRoomId.WeirdRoom, state.CurrentTick, 1), Is.True);
            state.AdvanceOneTick();

            OfficePayrollRuleMatch match = state.PayrollRule.Matches[0];
            Assert.That(match.Reason,
                Is.EqualTo("BADGE ACTIVE / SHIFT LOG MATCHES"));
            Assert.That(match.Action, Is.EqualTo("SENT TO MONEY"));
            Assert.That(match.Reason, Does.Not.Contain("TRUTH"));
            Assert.That(match.Reason, Does.Not.Contain("AUTHORITY"));
            Assert.That(match.Reason, Does.Not.Contain("UTILITY"));
        }

        [Test]
        public void GhostClockRequiresExactConjunction()
        {
            Assert.That(OfficeGhostClockState.ExactAuthoredConjunction(
                OfficeVisibleMoodState.Upset, true, true), Is.True);
            Assert.That(OfficeGhostClockState.ExactAuthoredConjunction(
                OfficeVisibleMoodState.Worried, true, true), Is.False);
            Assert.That(OfficeGhostClockState.ExactAuthoredConjunction(
                OfficeVisibleMoodState.Strange, true, true), Is.False);
            Assert.That(OfficeGhostClockState.ExactAuthoredConjunction(
                OfficeVisibleMoodState.Upset, false, true), Is.False);
            Assert.That(OfficeGhostClockState.ExactAuthoredConjunction(
                OfficeVisibleMoodState.Upset, true, false), Is.False);
        }

        [Test]
        public void GhostClockNeverExceedsThreeSlips()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeM3TestDriver.EnterShiftTwo(campaign);
            OfficeSimulationState state = campaign.CurrentSimulation;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            CompleteFirstShiftTwoCases(state, intent, input, 4);
            TriggerGhostClock(state, intent, input);

            state.AdvanceTicks(OfficeGhostClockState.SlipIntervalTicks * 10);

            Assert.That(state.GhostClock.ActiveSlipCount,
                Is.EqualTo(OfficeGhostClockState.MaximumActiveSlips));
            Assert.That(state.GhostClock.SlipIds, Has.Count.EqualTo(3));
            for (int i = 0; i < state.GhostClock.SlipIds.Count; i++)
            {
                OfficeFolderState slip = state.Queues.GetFolder(
                    state.GhostClock.SlipIds[i]);
                Assert.That(slip.SourceCaseId,
                    Is.EqualTo(state.GhostClock.TomasCaseId));
                Assert.That(state.Cases.Get(slip.CaseId), Is.Null);
            }
        }

        [Test]
        public void MissingRoomAccessReroutesOnlyOneStaffPath()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeM3TestDriver.EnterShiftTwo(campaign);
            OfficeSimulationState state = campaign.CurrentSimulation;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            CompleteFirstShiftTwoCases(state, intent, input, 3);
            string irisCase = state.Customers.ActiveDeskCustomer
                .LinkedAutomationClaimId;
            OfficeM3TestDriver.NavigateTo(state, intent, input, "front-desk.interact");
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            OfficeM3TestDriver.NavigateTo(state, intent, input, "weird-room.interact");
            state.AdvanceTicks(state.Queues.TransferDurationTicks);
            state.AdvanceOneTick();
            Assert.That(state.MissingRoomAccess.Active, Is.True);

            string june = state.Cases.Cases[5].AutomationClaimId;
            Assert.That(state.Staff.TryAssign(
                OfficeStaffSystem.RunnerId,
                june,
                OfficeRoomId.MoneyRoom,
                state.CurrentTick,
                out string failure), Is.True, failure);
            OfficeStaffState runner = state.Staff.Get(OfficeStaffSystem.RunnerId);
            Assert.That(runner.DestinationRoom, Is.EqualTo(OfficeRoomId.WeirdRoom));
            Assert.That(state.Staff.RunnerDiversionCount, Is.EqualTo(1));
            Assert.That(state.Staff.RunnerDiversionActive, Is.False);
            Assert.That(state.MissingRoomAccess.TryCloseAt(
                OfficeRoomId.WeirdRoom, state.Staff), Is.True);
            Assert.That(state.MissingRoomAccess.Recovered, Is.True);
            Assert.That(irisCase, Is.EqualTo(state.MissingRoomAccess.IrisCaseId));
        }

        [Test]
        public void ShiftTwoHeadlineAndComplicationsCompleteWithNormalControls()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeM3TestDriver.EnterShiftTwo(campaign);

            OfficeM3TestDriver.DriveShiftTwoToResult(campaign);

            OfficeSimulationState state = campaign.CurrentSimulation;
            string tomas = state.Cases.Cases[4].AutomationClaimId;
            Assert.That(state.Decisions.RecordFor(tomas), Is.Not.Null);
            Assert.That(state.GhostClock.Recovered, Is.True);
            Assert.That(state.MissingRoomAccess.Recovered, Is.True);
            Assert.That(state.Decisions.CommitCount, Is.EqualTo(6));
            Assert.That(state.CausalEvents.ContainsOnlyObservableEvents(), Is.True);
        }

        [Test]
        public void SecondUpgradePersistsIntoShiftThree()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeM3TestDriver.EnterShiftTwo(
                campaign, OfficeUpgradeFamily.FastTrays);
            OfficeM3TestDriver.DriveShiftTwoToResult(campaign);

            OfficeM3TestDriver.ChooseUpgradeAndContinue(
                campaign, OfficeUpgradeFamily.CalmChairs);

            Assert.That(campaign.CurrentShiftOrdinal, Is.EqualTo(3));
            Assert.That(campaign.Upgrades.FastTraysTier, Is.EqualTo(1));
            Assert.That(campaign.Upgrades.CalmChairsTier, Is.EqualTo(1));
            Assert.That(campaign.CurrentSimulation.Queues.TransferDurationTicks,
                Is.EqualTo(12));
        }

        private static OfficeM2Scenario CreateShiftTwoScenario()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(6);
            session.ReleaseNextShift(6);
            return OfficeM2Scenario.CreateForCampaign(session, 2);
        }

        private static OfficeSimulationState CreateRuleTwoEnabledState()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateAuthoredShift(
                CreateShiftTwoScenario());
            string owen = state.Cases.Cases[1].AutomationClaimId;
            OfficeCaseWorkDefinition work = state.WorkDefinitionFor(owen);
            Assert.That(state.ManualTasks.TryStart(
                OfficeManualTaskKind.Compare, owen, state.CurrentTick, out _), Is.True);
            Assert.That(state.ManualTasks.TrySubmit(
                (int)work.PaperAnswer, out bool compared, out _), Is.True);
            Assert.That(compared, Is.True);
            Assert.That(state.ManualTasks.TryStart(
                OfficeManualTaskKind.Trace, owen, state.CurrentTick, out _), Is.True);
            Assert.That(state.ManualTasks.TrySubmit(
                work.MoneyPathAnswer, out bool traced, out _), Is.True);
            Assert.That(traced, Is.True);
            state.AdvanceOneTick();
            Assert.That(state.PayrollRule.Unlocked, Is.True);
            Assert.That(state.PayrollRule.TryToggle(), Is.True);
            return state;
        }

        private static void CompleteFirstShiftTwoCases(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input,
            int count)
        {
            while (state.Decisions.CommitCount < count)
            {
                OfficeM3TestDriver.CompleteActiveCaseInAuthoredOrder(
                    state, intent, input);
                if (state.Decisions.CommitCount == 2 && !state.PayrollRule.Enabled)
                {
                    intent.BufferToggleRule2(state.CurrentTick);
                    input.AdvanceOneTick();
                }
            }
        }

        private static void TriggerGhostClock(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            Assert.That(state.Customers.ActiveDeskCustomer.DisplayName,
                Is.EqualTo("TOMAS REED"));
            OfficeM3TestDriver.NavigateTo(state, intent, input, "front-desk.interact");
            while (state.PrimaryActionLabel == "CALM")
            {
                OfficeM3TestDriver.PressPrimary(state, intent, input);
                state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks);
                if (state.CustomerPressure.CalmCooldownRemainingTicks > 0)
                    state.AdvanceTicks(state.CustomerPressure.CalmCooldownRemainingTicks);
            }
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            OfficeM3TestDriver.NavigateTo(state, intent, input, "paper-room.interact");
            state.AdvanceTicks(state.Queues.TransferDurationTicks);
            for (int tick = 0; tick < 1200 && !state.GhostClock.HasTriggered; tick++)
                input.AdvanceOneTick();
            Assert.That(state.GhostClock.HasTriggered, Is.True,
                state.OrderedStateSnapshot);
        }
    }
}
