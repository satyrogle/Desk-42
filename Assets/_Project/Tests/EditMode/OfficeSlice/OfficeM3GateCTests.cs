using Desk42.Institutional.Player;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.OfficeM3
{
    public sealed class OfficeM3GateCTests
    {
        [Test]
        public void ShiftThreeAuthoredCasesAndBothRulesCarryForward()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeM3TestDriver.EnterShiftThree(campaign);
            OfficeSimulationState state = campaign.CurrentSimulation;

            Assert.That(state.Customers.Customers[2].DisplayName,
                Is.EqualTo("MARA VALE"));
            Assert.That(state.Customers.Customers[2].Problem,
                Is.EqualTo("THE COPIER HAS MY BADGE, TOMAS'S HOURS, " +
                    "AND A MANAGER'S STAMP."));
            Assert.That(state.WorkDefinitionFor(
                    state.Cases.Cases[0].AutomationClaimId)
                .PriorObservableRecord, Does.StartWith("SHIFT 1 RECORD / "));
            Assert.That(state.WorkDefinitionFor(
                    state.Cases.Cases[1].AutomationClaimId)
                .PriorObservableRecord, Does.StartWith("SHIFT 2 RECORD / "));
            Assert.That(state.AutomationRule.Unlocked, Is.True);
            Assert.That(state.PayrollRule.Unlocked, Is.True);
            bool ruleOneBefore = state.AutomationRule.Enabled;
            bool ruleTwoBefore = state.PayrollRule.Enabled;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            intent.BufferToggleRule(state.CurrentTick);
            input.AdvanceOneTick();
            intent.BufferToggleRule2(state.CurrentTick);
            input.AdvanceOneTick();
            Assert.That(state.AutomationRule.Enabled, Is.Not.EqualTo(ruleOneBefore));
            Assert.That(state.PayrollRule.Enabled, Is.Not.EqualTo(ruleTwoBefore));
        }

        [Test]
        public void RedLabelsChangesExactCopyRecoveryValues()
        {
            OfficeSimulationState baseline = CreateShiftThreeState();
            var tierOneUpgrades = new OfficeCampaignUpgradeState();
            Assert.That(tierOneUpgrades.TryChoose(
                OfficeUpgradeFamily.RedLabels), Is.True);
            OfficeSimulationState tierOne = CreateShiftThreeState(
                tierOneUpgrades);
            var tierTwoUpgrades = new OfficeCampaignUpgradeState();
            Assert.That(tierTwoUpgrades.TryChoose(
                OfficeUpgradeFamily.RedLabels), Is.True);
            Assert.That(tierTwoUpgrades.TryChoose(
                OfficeUpgradeFamily.RedLabels), Is.True);
            OfficeSimulationState tierTwo = CreateShiftThreeState(
                tierTwoUpgrades);

            Assert.That(
                baseline.PromotionCascade.CopyClearDurationTicks -
                tierOne.PromotionCascade.CopyClearDurationTicks,
                Is.EqualTo(15));
            Assert.That(tierOne.PromotionCascade.OriginalFindDurationTicks,
                Is.EqualTo(
                    baseline.PromotionCascade.OriginalFindDurationTicks));
            Assert.That(
                tierOne.PromotionCascade.OriginalFindDurationTicks -
                tierTwo.PromotionCascade.OriginalFindDurationTicks,
                Is.EqualTo(15));
            Assert.That(
                baseline.PromotionCascade.MaximumActivePromotionForms -
                tierTwo.PromotionCascade.MaximumActivePromotionForms,
                Is.EqualTo(2));
            Assert.That(
                baseline.BreakState.MaximumActiveCopyLimit -
                tierTwo.BreakState.MaximumActiveCopyLimit,
                Is.EqualTo(2));
        }

        [Test]
        public void PromotionCascadeRequiresEveryLockedCondition()
        {
            Assert.That(OfficePromotionCascadeState.ExactAuthoredConjunction(
                true, true, true, true, OfficeVisibleMoodState.Upset), Is.True);
            Assert.That(OfficePromotionCascadeState.ExactAuthoredConjunction(
                false, true, true, true, OfficeVisibleMoodState.Upset), Is.False);
            Assert.That(OfficePromotionCascadeState.ExactAuthoredConjunction(
                true, false, true, true, OfficeVisibleMoodState.Upset), Is.False);
            Assert.That(OfficePromotionCascadeState.ExactAuthoredConjunction(
                true, true, false, true, OfficeVisibleMoodState.Upset), Is.False);
            Assert.That(OfficePromotionCascadeState.ExactAuthoredConjunction(
                true, true, true, false, OfficeVisibleMoodState.Upset), Is.False);
            Assert.That(OfficePromotionCascadeState.ExactAuthoredConjunction(
                true, true, true, true, OfficeVisibleMoodState.Worried), Is.False);
        }

        [Test]
        public void PromotionCascadeDoesNotUseRandomness()
        {
            OfficeSimulationState first = CreateShiftThreeState();
            OfficeSimulationState second = CreateShiftThreeState();
            TriggerPromotion(first);
            TriggerPromotion(second);

            first.AdvanceTicks(360);
            second.AdvanceTicks(360);

            Assert.That(first.Checksum, Is.EqualTo(second.Checksum));
            Assert.That(first.OrderedStateSnapshot,
                Is.EqualTo(second.OrderedStateSnapshot));
        }

        [Test]
        public void PromotionCascadeCopyGrowthIsBounded()
        {
            OfficeSimulationState state = CreateShiftThreeState();
            TriggerPromotion(state);

            state.AdvanceTicks(
                OfficePromotionCascadeState.PromotionFormIntervalTicks * 20);

            Assert.That(state.PromotionCascade.ActivePromotionFormCount,
                Is.EqualTo(
                    state.PromotionCascade.MaximumActivePromotionForms));
            Assert.That(state.PromotionCascade.PromotionFormIds.Count,
                Is.EqualTo(
                    state.PromotionCascade.MaximumActivePromotionForms));
            Assert.That(state.Queues.HasSingleLogicalOwnerForEveryFolder(),
                Is.True);
        }

        [Test]
        public void MachineFirstRecoveryCompletes()
        {
            OfficeSimulationState state = CreateShiftThreeState();
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            TriggerPromotion(state, intent, input);

            RecoverMachineFirst(state, intent, input);

            Assert.That(state.PromotionCascade.Recovered, Is.True,
                state.OrderedStateSnapshot);
            Assert.That(state.CausalEvents.ContainsOnlyObservableEvents(), Is.True);
        }

        [Test]
        public void PeopleFirstRecoveryCompletes()
        {
            OfficeSimulationState state = CreateShiftThreeState();
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            TriggerPromotion(state, intent, input);

            RecoverPeopleFirst(state, intent, input);

            Assert.That(state.PromotionCascade.Recovered, Is.True,
                state.OrderedStateSnapshot);
            Assert.That(state.CausalEvents.ContainsOnlyObservableEvents(), Is.True);
        }

        [Test]
        public void ShiftRestartPreservesPriorUpgradeChoices()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeM3TestDriver.EnterShiftThree(
                campaign,
                OfficeUpgradeFamily.FastTrays,
                OfficeUpgradeFamily.RedLabels);
            OfficeSimulationState failed = campaign.CurrentSimulation;
            TriggerPromotion(failed);
            failed.AdvanceTicks(OfficePromotionCascadeState.FailureGraceTicks + 2);
            RequestRestart(failed);

            Assert.That(campaign.TryRestartCurrentShift(), Is.True);

            Assert.That(campaign.Upgrades.FastTraysTier, Is.EqualTo(1));
            Assert.That(campaign.Upgrades.RedLabelsTier, Is.EqualTo(1));
            Assert.That(campaign.CurrentShiftOrdinal, Is.EqualTo(3));
            Assert.That(campaign.Rules.Rule1AcceptedCopiedRefund, Is.True);
            Assert.That(campaign.Rules.Rule2AcceptedCopiedPayroll, Is.True);
        }

        [Test]
        public void ShiftRestartRemovesCurrentShiftRuntimeState()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            OfficeM3TestDriver.EnterShiftThree(campaign);
            OfficeSimulationState failed = campaign.CurrentSimulation;
            TriggerPromotion(failed);
            failed.AdvanceTicks(OfficePromotionCascadeState.FailureGraceTicks + 2);
            Assert.That(failed.Shift.Failed, Is.True);
            RequestRestart(failed);

            Assert.That(campaign.TryRestartCurrentShift(), Is.True);
            OfficeSimulationState restarted = campaign.CurrentSimulation;

            Assert.That(restarted, Is.Not.SameAs(failed));
            Assert.That(restarted.CurrentTick, Is.Zero);
            Assert.That(restarted.PromotionCascade.HasTriggered, Is.False);
            Assert.That(restarted.PromotionCascade.PromotionFormIds, Is.Empty);
            Assert.That(restarted.Staff.Staff, Has.Count.EqualTo(2));
            Assert.That(restarted.Customers.Customers, Has.Count.EqualTo(6));
            Assert.That(restarted.Queues.FolderIds, Has.Count.EqualTo(6));
            Assert.That(restarted.CausalEvents.Events, Is.Empty);
            Assert.That(restarted.Queues.HasSingleLogicalOwnerForEveryFolder(),
                Is.True);
        }

        internal static OfficeSimulationState CreateShiftThreeState(
            OfficeCampaignUpgradeState upgrades = null)
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(6);
            session.ReleaseNextShift(6);
            session.ReleaseNextShift(6);
            OfficeM2Scenario scenario = OfficeM2Scenario.CreateForCampaign(
                session, 3);
            return OfficeSimulationState.CreateAuthoredShift(
                scenario,
                upgrades,
                ruleOneAcceptedCopiedRefund: true,
                ruleTwoAcceptedCopiedPayroll: true);
        }

        internal static void TriggerPromotion(OfficeSimulationState state)
        {
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            TriggerPromotion(state, intent, input);
        }

        internal static void TriggerPromotion(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            while (state.Decisions.CommitCount < 2)
                OfficeM3TestDriver.CompleteActiveCaseInAuthoredOrder(
                    state, intent, input);
            Assert.That(state.Customers.ActiveDeskCustomer.DisplayName,
                Is.EqualTo("MARA VALE"));
            for (int tick = 0; tick < 1800 &&
                !state.PromotionCascade.HasTriggered; tick++)
                input.AdvanceOneTick();
            Assert.That(state.PromotionCascade.HasTriggered, Is.True,
                state.OrderedStateSnapshot);
            Assert.That(state.PromotionCascade.Active, Is.True);
            Assert.That(state.PromotionCascade.SupervisorStampActive, Is.True);
            Assert.That(state.Staff.RunnerTaskSourceId,
                Is.EqualTo(OfficeStaffSystem.CopierTaskSourceId));
        }

        internal static void RecoverMachineFirst(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            OfficeM3TestDriver.NavigateTo(
                state, intent, input, "weird-room.interact");
            Assert.That(state.PrimaryActionLabel, Is.EqualTo("STOP COPIER"));
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            Assert.That(state.PrimaryActionLabel,
                Is.EqualTo("REMOVE SUPERVISOR STAMP"));
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            ClearPromotionForms(state, intent, input);
            OfficeM3TestDriver.NavigateTo(
                state, intent, input, "front-desk.interact");
            OfficeM3TestDriver.CalmActiveCustomer(state, intent, input);
            OfficeM3TestDriver.CompleteActiveCaseInAuthoredOrder(
                state, intent, input);
            ReassignRunner(state, intent, input);
            state.AdvanceOneTick();
        }

        internal static void RecoverPeopleFirst(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            OfficeM3TestDriver.NavigateTo(
                state, intent, input, "front-desk.interact");
            OfficeM3TestDriver.CalmActiveCustomer(state, intent, input);
            string maraCaseId = state.PromotionCascade.MaraCaseId;
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            OfficeM3TestDriver.NavigateTo(
                state, intent, input, "paper-room.interact");
            state.AdvanceTicks(state.Queues.TransferDurationTicks);
            Assert.That(state.PrimaryActionLabel,
                Is.EqualTo("FIND ORIGINAL BADGE"));
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            state.AdvanceTicks(
                state.PromotionCascade.RecoveryChannelRemainingTicks);
            Assert.That(state.PromotionCascade.OriginalBadgeFound, Is.True);
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            Assert.That(state.Carry.CarriedFolderId, Is.EqualTo(maraCaseId));
            OfficeCommand sendFront = state.CreateSendCommand(
                maraCaseId, OfficeRoomId.FrontDesk);
            Assert.That(state.TryQueueCommand(
                sendFront, out OfficeCommandFailure failure), Is.True,
                failure?.ToString());
            input.AdvanceOneTick();
            OfficeM3TestDriver.NavigateTo(
                state, intent, input, "front-desk.interact");
            state.AdvanceTicks(state.Queues.TransferDurationTicks);
            state.AdvanceOneTick();
            Assert.That(state.PromotionCascade.OriginalBadgeReturned, Is.True);
            ReassignRunner(state, intent, input);
            OfficeM3TestDriver.NavigateTo(
                state, intent, input, "weird-room.interact");
            Assert.That(state.PrimaryActionLabel, Is.EqualTo("STOP COPIER"));
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            Assert.That(state.PrimaryActionLabel,
                Is.EqualTo("REMOVE SUPERVISOR STAMP"));
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            ClearPromotionForms(state, intent, input);
            state.AdvanceOneTick();
        }

        private static void ClearPromotionForms(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            string[] points =
            {
                "weird-room.interact",
                "money-room.interact",
                "paper-room.interact",
                "front-desk.interact",
            };
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                OfficeM3TestDriver.NavigateTo(
                    state, intent, input, points[pointIndex]);
                int safety = 0;
                while (state.PrimaryActionLabel == "CLEAR PROMOTION FORM" &&
                    safety++ < 12)
                {
                    OfficeM3TestDriver.PressPrimary(state, intent, input);
                    state.AdvanceTicks(
                        state.PromotionCascade.RecoveryChannelRemainingTicks);
                }
            }
            Assert.That(state.PromotionCascade.ActivePromotionFormCount,
                Is.Zero, state.OrderedStateSnapshot);
        }

        private static void ReassignRunner(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            for (int tick = 0; tick < 1200 &&
                state.PromotionCascade.DivertedFolderIds.Count < 2; tick++)
                input.AdvanceOneTick();
            Assert.That(state.PromotionCascade.DivertedFolderIds,
                Has.Count.EqualTo(2), state.OrderedStateSnapshot);
            OfficeM3TestDriver.NavigateTo(
                state, intent, input, "waiting-area.interact");
            Assert.That(state.PrimaryActionLabel, Is.EqualTo("REASSIGN RUNNER"));
            OfficeM3TestDriver.PressPrimary(state, intent, input);
            Assert.That(state.PromotionCascade.RunnerReassigned, Is.True);
        }

        private static void RequestRestart(OfficeSimulationState state)
        {
            Assert.That(state.Shift.Failed, Is.True, state.OrderedStateSnapshot);
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            intent.BufferRestart(state.CurrentTick);
            input.AdvanceOneTick();
            Assert.That(state.Shift.RestartRequested, Is.True);
        }
    }
}
