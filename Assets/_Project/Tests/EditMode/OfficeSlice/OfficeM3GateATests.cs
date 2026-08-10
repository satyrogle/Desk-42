using System.Collections.Generic;
using Desk42.Institutional.Player;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode.OfficeM3
{
    public sealed class OfficeM3GateATests
    {
        [Test]
        public void CampaignUsesOneContinuingInstitutionalSession()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            InstitutionalAutomationSession session = campaign.InstitutionalSession;

            OfficeM3TestDriver.DriveShiftOneToResult(campaign);
            OfficeM3TestDriver.ChooseUpgradeAndContinue(
                campaign, OfficeUpgradeFamily.FastTrays);

            Assert.That(campaign.InstitutionalSession, Is.SameAs(session));
            Assert.That(campaign.CurrentShiftOrdinal, Is.EqualTo(2));
        }

        [Test]
        public void EachShiftReleasesSixDistinctPublicClaims()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(6);
            var ids = new HashSet<string>();
            for (int shift = 1; shift <= 3; shift++)
            {
                if (shift > 1) session.ReleaseNextShift(6);
                Assert.That(session.Claims, Has.Count.EqualTo(6));
                for (int i = 0; i < session.Claims.Count; i++)
                    Assert.That(ids.Add(session.Claims[i].AutomationClaimId), Is.True);
            }
            Assert.That(ids, Has.Count.EqualTo(18));
        }

        [Test]
        public void SameSixCustomerIdsPersistAcrossThreeShifts()
        {
            InstitutionalAutomationSession session =
                InstitutionalAutomationSession.Create(6);
            OfficeCampaignShiftDefinition first =
                OfficeCampaignScenario.CreateShift(session, 1);
            session.ReleaseNextShift(6);
            OfficeCampaignShiftDefinition second =
                OfficeCampaignScenario.CreateShift(session, 2);
            session.ReleaseNextShift(6);
            OfficeCampaignShiftDefinition third =
                OfficeCampaignScenario.CreateShift(session, 3);

            for (int i = 0; i < 6; i++)
            {
                Assert.That(second.CaseBindings[i].CustomerId,
                    Is.EqualTo(first.CaseBindings[i].CustomerId));
                Assert.That(third.CaseBindings[i].CustomerId,
                    Is.EqualTo(first.CaseBindings[i].CustomerId));
                Assert.That(second.CaseBindings[i].DisplayName,
                    Is.EqualTo(first.CaseBindings[i].DisplayName));
                Assert.That(third.CaseBindings[i].DisplayName,
                    Is.EqualTo(first.CaseBindings[i].DisplayName));
            }
        }

        [Test]
        public void ShiftOneM2CriticalPathRemainsValid()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();

            OfficeM3TestDriver.DriveShiftOneToResult(campaign);

            Assert.That(campaign.CurrentSimulation.Shift.Success, Is.True);
            Assert.That(campaign.CurrentSimulation.Decisions.CommitCount, Is.EqualTo(6));
            Assert.That(campaign.CurrentSimulation.BreakState.Recovered, Is.True);
            Assert.That(campaign.CurrentSimulation.Queues
                .HasSingleLogicalOwnerForEveryFolder(), Is.True);
            Assert.That(campaign.Phase, Is.EqualTo(OfficeCampaignPhase.ChooseUpgrade));
        }

        [Test]
        public void FirstUpgradePersistsIntoShiftTwo()
        {
            OfficeCampaignState campaign = OfficeCampaignState.Create();
            string[] firstClaims = OfficeM3TestDriver.CurrentClaimIds(campaign);
            OfficeM3TestDriver.DriveShiftOneToResult(campaign);

            OfficeM3TestDriver.ChooseUpgradeAndContinue(
                campaign, OfficeUpgradeFamily.FastTrays);

            Assert.That(campaign.CurrentShiftOrdinal, Is.EqualTo(2));
            Assert.That(campaign.Upgrades.FastTraysTier, Is.EqualTo(1));
            Assert.That(campaign.Upgrades.TransferDurationTicks, Is.EqualTo(12));
            Assert.That(campaign.CurrentSimulation.CurrentTick, Is.Zero);
            Assert.That(campaign.CurrentSimulation.Customers.Customers[0].CustomerId,
                Is.EqualTo("customer.m2.01"));
            string[] secondClaims = OfficeM3TestDriver.CurrentClaimIds(campaign);
            CollectionAssert.AreNotEquivalent(firstClaims, secondClaims);
        }

        [Test]
        public void CommandSchemaThreeExplicitlyRejectsCampaignCommandsInOldLogs()
        {
            var oldMove = new OfficeCommand(
                2, 1, 1, OfficeCommandKind.Move,
                "warden", string.Empty, 1, 0, string.Empty);
            var oldCampaign = new OfficeCommand(
                2, 2, 2, OfficeCommandKind.ChooseUpgrade,
                "warden", "office-upgrade", 1, 0, "FastTrays");
            var log = new OfficeCommandLog();

            Assert.That(log.TryRecord(oldMove, out string moveFailure), Is.True,
                moveFailure);
            Assert.That(log.TryRecord(oldCampaign, out string campaignFailure), Is.False);
            Assert.That(campaignFailure, Does.Contain("cannot contain M3"));
            Assert.That(OfficeCommandLog.CurrentSchemaVersion, Is.EqualTo(3));
        }
    }

    internal static class OfficeM3TestDriver
    {
        public static string[] CurrentClaimIds(OfficeCampaignState campaign)
        {
            var ids = new string[campaign.CurrentSimulation.Cases.Cases.Count];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = campaign.CurrentSimulation.Cases.Cases[i].AutomationClaimId;
            return ids;
        }

        public static void DriveShiftOneToResult(OfficeCampaignState campaign)
        {
            OfficeSimulationState state = campaign.CurrentSimulation;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            CompleteActiveCase(state, intent, input);
            intent.BufferToggleRule(state.CurrentTick);
            input.AdvanceOneTick();
            CompleteActiveCase(state, intent, input);
            for (int tick = 0; tick < 1200 && !state.BreakState.Active; tick++)
                input.AdvanceOneTick();
            Assert.That(state.BreakState.Active, Is.True, state.OrderedStateSnapshot);
            RecoverBreak(state, intent, input);
            while (state.Decisions.CommitCount < 6)
                CompleteActiveCase(state, intent, input);
            state.AdvanceOneTick();
            Assert.That(state.Shift.Phase, Is.EqualTo(OfficeShiftPhase.Result));
            Assert.That(campaign.Phase, Is.EqualTo(OfficeCampaignPhase.ChooseUpgrade));
        }

        public static void ChooseUpgradeAndContinue(
            OfficeCampaignState campaign,
            OfficeUpgradeFamily family)
        {
            OfficeSimulationState oldState = campaign.CurrentSimulation;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(oldState, intent);
            intent.BufferChoice((int)family, oldState.CurrentTick);
            input.AdvanceOneTick();
            Assert.That(campaign.Phase,
                Is.EqualTo(OfficeCampaignPhase.ReadyForNextShift));
            intent.BufferInteraction(oldState.CurrentTick);
            input.AdvanceOneTick();
            Assert.That(campaign.CurrentSimulation, Is.Not.SameAs(oldState));
            Assert.That(campaign.Phase, Is.EqualTo(OfficeCampaignPhase.ActiveShift));
        }

        private static void CompleteActiveCase(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;
            OfficeCaseWorkDefinition work = state.WorkDefinitionFor(caseId);
            NavigateTo(state, intent, input, "front-desk.interact");
            CalmUntilActionable(state, intent, input);
            PressPrimary(state, intent, input);
            PressPrimary(state, intent, input);
            NavigateTo(state, intent, input, "paper-room.interact");
            PressPrimary(state, intent, input);
            PressPrimary(state, intent, input);
            PressChoice(state, intent, input, (int)work.PaperAnswer + 1);
            PressPrimary(state, intent, input);
            NavigateTo(state, intent, input, "money-room.interact");
            PressPrimary(state, intent, input);
            PressPrimary(state, intent, input);
            PressChoice(state, intent, input, work.MoneyPathAnswer + 1);
            PressPrimary(state, intent, input);
            NavigateTo(state, intent, input, "front-desk.interact");
            PressChoice(state, intent, input, 1);
            Assert.That(state.Decisions.RecordFor(caseId), Is.Not.Null,
                state.OrderedStateSnapshot);
        }

        private static void RecoverBreak(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            NavigateTo(state, intent, input, "weird-room.interact");
            PressPrimary(state, intent, input);
            NavigateTo(state, intent, input, "front-desk.interact");
            CalmUntilActionable(state, intent, input);
            NavigateTo(state, intent, input, "money-room.interact");
            int safety = 0;
            while (state.Queues.ActiveCopyCount > 0 && safety++ < 60)
            {
                if (string.IsNullOrWhiteSpace(
                        state.Queues.FirstActiveCopyAt(OfficeRoomId.MoneyRoom)))
                    state.AdvanceOneTick();
                else
                    PressPrimary(state, intent, input);
            }
            NavigateTo(state, intent, input, "front-desk.interact");
            CalmUntilActionable(state, intent, input);
            PressPrimary(state, intent, input);
            intent.BufferDrop(state.CurrentTick);
            input.AdvanceOneTick();
            state.AdvanceOneTick();
            Assert.That(state.BreakState.Recovered, Is.True,
                state.OrderedStateSnapshot);
        }

        private static void CalmUntilActionable(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            int safety = 0;
            while (state.PrimaryActionLabel == "CALM" && safety++ < 8)
            {
                PressPrimary(state, intent, input);
                state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks);
                if (state.PrimaryActionLabel == "CALM" &&
                    state.CustomerPressure.CalmCooldownRemainingTicks > 0)
                    state.AdvanceTicks(
                        state.CustomerPressure.CalmCooldownRemainingTicks);
            }
        }

        private static void NavigateTo(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input,
            string interactionPointId)
        {
            OfficeInteractionPoint point = state.Grid.GetInteractionPoint(
                interactionPointId);
            Assert.That(state.Grid.TryFindPath(
                state.Warden.Cell(state.Grid), point.Cell,
                out List<OfficeCell> path), Is.True);
            int ticksPerCell = OfficeGrid.LogicalSubunitsPerCell /
                OfficeWardenState.MovementSubunitsPerTick;
            for (int cell = 1; cell < path.Count; cell++)
            {
                int x = path[cell].X - path[cell - 1].X;
                int z = path[cell].Z - path[cell - 1].Z;
                intent.SetMovement(x < 0 ? OfficeInputDirection.Left :
                    x > 0 ? OfficeInputDirection.Right :
                    z < 0 ? OfficeInputDirection.Down : OfficeInputDirection.Up);
                for (int tick = 0; tick < ticksPerCell; tick++)
                    input.AdvanceOneTick();
            }
            intent.SetMovement(OfficeInputDirection.None);
        }

        private static void PressPrimary(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            intent.BufferInteraction(state.CurrentTick);
            input.AdvanceOneTick();
        }

        private static void PressChoice(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input,
            int oneBasedChoice)
        {
            intent.BufferChoice(oneBasedChoice, state.CurrentTick);
            input.AdvanceOneTick();
        }
    }
}
