using System.Collections.Generic;
using Desk42.Product.OfficeSlice;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class OfficeM2GateCTests
    {
        [Test]
        public void AutoSorter_UsesStablePublicFields()
        {
            OfficeSimulationState first = CreateRuleEnabledState();
            OfficeSimulationState second = CreateRuleEnabledState();
            RouteKnownRuleCases(first);
            RouteKnownRuleCases(second);

            Assert.That(first.AutomationRule.Matches, Has.Count.EqualTo(3));
            Assert.That(first.AutomationRule.Matches[0].Matched, Is.True);
            Assert.That(first.AutomationRule.Matches[1].Matched, Is.False);
            Assert.That(first.AutomationRule.Matches[2].Matched, Is.True);
            Assert.That(second.OrderedStateSnapshot,
                Is.EqualTo(first.OrderedStateSnapshot));
        }

        [Test]
        public void AutoSorter_LogsWhyRuleMatched()
        {
            OfficeSimulationState state = CreateRuleEnabledState();
            string routineRefund = state.Cases.Cases[1].AutomationClaimId;
            Assert.That(state.Queues.TryTransferCase(
                routineRefund, OfficeRoomId.WeirdRoom, state.CurrentTick, 1), Is.True);
            state.AdvanceOneTick();

            Assert.That(state.AutomationRule.Matches, Has.Count.EqualTo(1));
            OfficeAutomationRuleMatch match = state.AutomationRule.Matches[0];
            Assert.That(match.Matched, Is.True);
            Assert.That(match.Reason,
                Is.EqualTo("PAPERS MATCH / REFUND PATH CLEAR"));
            Assert.That(match.Action, Is.EqualTo("SENT FRONT"));
        }

        [Test]
        public void Break_RequiresExactAuthoredConjunction()
        {
            Assert.That(OfficeBreakState.ExactAuthoredConjunction(
                OfficeVisibleMoodState.Upset, true, true), Is.True);
            Assert.That(OfficeBreakState.ExactAuthoredConjunction(
                OfficeVisibleMoodState.Worried, true, true), Is.False);
            Assert.That(OfficeBreakState.ExactAuthoredConjunction(
                OfficeVisibleMoodState.Strange, true, true), Is.False);
            Assert.That(OfficeBreakState.ExactAuthoredConjunction(
                OfficeVisibleMoodState.Upset, false, true), Is.False);
            Assert.That(OfficeBreakState.ExactAuthoredConjunction(
                OfficeVisibleMoodState.Upset, true, false), Is.False);
        }

        [Test]
        public void Break_ReplaysToSameChecksum()
        {
            OfficeSimulationState live = DriveToBreak();
            OfficeSimulationState replay =
                OfficeSimulationState.CreateM2Replay(live.CommandLog);
            replay.AdvanceTicks((int)live.CurrentTick);

            Assert.That(live.BreakState.Active, Is.True);
            Assert.That(replay.BreakState.Active, Is.True);
            Assert.That(replay.Checksum, Is.EqualTo(live.Checksum));
            Assert.That(replay.OrderedStateSnapshot,
                Is.EqualTo(live.OrderedStateSnapshot));
        }

        [Test]
        public void BreakRecovery_AllowsTwoOrders()
        {
            OfficeSimulationState calmFirst = DriveToBreak();
            OfficeSimulationState fixFirst = DriveToBreak();

            Recover(calmFirst, fixMachineFirst: false);
            Recover(fixFirst, fixMachineFirst: true);

            Assert.That(calmFirst.BreakState.Recovered, Is.True,
                calmFirst.OrderedStateSnapshot);
            Assert.That(fixFirst.BreakState.Recovered, Is.True,
                fixFirst.OrderedStateSnapshot);
            Assert.That(calmFirst.Queues.ActiveCopyCount, Is.Zero);
            Assert.That(fixFirst.Queues.ActiveCopyCount, Is.Zero);
            Assert.That(calmFirst.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
            Assert.That(fixFirst.Queues.HasSingleLogicalOwnerForEveryFolder(), Is.True);
        }

        internal static OfficeSimulationState DriveToBreak()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);

            CompleteActiveCase(state, intent, input);
            intent.BufferToggleRule(state.CurrentTick);
            input.AdvanceOneTick();
            Assert.That(state.AutomationRule.Enabled, Is.True);
            CompleteActiveCase(state, intent, input);
            Assert.That(state.Customers.ActiveDeskCustomer.CustomerId,
                Is.EqualTo(state.BreakState.CopyEchoCustomerId));

            for (int tick = 0; tick < 1200 && !state.BreakState.Active; tick++)
                input.AdvanceOneTick();
            Assert.That(state.BreakState.Active, Is.True);
            Assert.That(state.AutomationRule.LastAcceptedCopyId, Is.Not.Empty);
            return state;
        }

        internal static void Recover(
            OfficeSimulationState state,
            bool fixMachineFirst)
        {
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            if (!fixMachineFirst)
            {
                PressPrimary(state, intent, input); // CALM
                state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks);
            }

            NavigateTo(state, intent, input, "weird-room.interact");
            PressPrimary(state, intent, input); // FIX MACHINE
            NavigateTo(state, intent, input, "front-desk.interact");

            if (fixMachineFirst)
            {
                PressPrimary(state, intent, input); // CALM
                state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks);
            }

            int safety = 0;
            while (state.Queues.ActiveCopyCount > 0 && safety++ < 40)
            {
                if (state.PrimaryActionLabel == "CALM")
                {
                    PressPrimary(state, intent, input);
                    state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks);
                    continue;
                }
                if (string.IsNullOrWhiteSpace(
                        state.Queues.FirstActiveCopyAt(OfficeRoomId.FrontDesk)))
                {
                    state.AdvanceOneTick();
                    continue;
                }
                PressPrimary(state, intent, input); // FIX COPY
            }
            Assert.That(state.Queues.ActiveCopyCount, Is.Zero,
                state.OrderedStateSnapshot);
            if (state.PrimaryActionLabel == "CALM")
            {
                PressPrimary(state, intent, input);
                state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks);
            }
            Assert.That(state.PrimaryActionLabel, Is.EqualTo("TAKE FOLDER"),
                state.OrderedStateSnapshot);
            PressPrimary(state, intent, input); // TAKE ORIGINAL
            Assert.That(state.Carry.CarriedFolderId,
                Is.EqualTo(state.BreakState.OriginalFolderId));
            intent.BufferDrop(state.CurrentTick);
            input.AdvanceOneTick();
            state.AdvanceOneTick();
        }

        private static OfficeSimulationState CreateRuleEnabledState()
        {
            OfficeSimulationState state = OfficeSimulationState.CreateM2();
            CompleteManualRecord(state);
            state.AdvanceOneTick();
            Assert.That(state.AutomationRule.Unlocked, Is.True);
            Assert.That(state.AutomationRule.TryToggle(), Is.True);
            return state;
        }

        private static void RouteKnownRuleCases(OfficeSimulationState state)
        {
            string matchOne = state.Cases.Cases[1].AutomationClaimId;
            string nonRefund = state.Cases.Cases[4].AutomationClaimId;
            string matchTwo = state.Cases.Cases[5].AutomationClaimId;
            Assert.That(state.Queues.TryTransferCase(
                matchOne, OfficeRoomId.WeirdRoom, state.CurrentTick, 1), Is.True);
            Assert.That(state.Queues.TryTransferCase(
                nonRefund, OfficeRoomId.WeirdRoom, state.CurrentTick, 1), Is.True);
            Assert.That(state.Queues.TryTransferCase(
                matchTwo, OfficeRoomId.WeirdRoom, state.CurrentTick, 1), Is.True);
            state.AdvanceOneTick();
            state.AdvanceTicks(OfficeAutomationRuleState.TransferDurationTicks);
        }

        private static void CompleteManualRecord(OfficeSimulationState state)
        {
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;
            OfficeCaseWorkDefinition work = state.WorkDefinitionFor(caseId);
            Assert.That(state.ManualTasks.TryStart(
                OfficeManualTaskKind.Compare, caseId, state.CurrentTick, out _), Is.True);
            Assert.That(state.ManualTasks.TrySubmit((int)work.PaperAnswer,
                out bool compareComplete, out _), Is.True);
            Assert.That(compareComplete, Is.True);
            Assert.That(state.ManualTasks.TryStart(
                OfficeManualTaskKind.Trace, caseId, state.CurrentTick, out _), Is.True);
            Assert.That(state.ManualTasks.TrySubmit(work.MoneyPathAnswer,
                out bool traceComplete, out _), Is.True);
            Assert.That(traceComplete, Is.True);
        }

        private static void CompleteActiveCase(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            string caseId = state.Customers.ActiveDeskCustomer.LinkedAutomationClaimId;
            OfficeCaseWorkDefinition work = state.WorkDefinitionFor(caseId);
            NavigateTo(state, intent, input, "front-desk.interact");
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
            Assert.That(state.Decisions.RecordFor(caseId), Is.Not.Null);
        }

        private static void NavigateTo(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input,
            string interactionPointId)
        {
            OfficeInteractionPoint point = state.Grid.GetInteractionPoint(interactionPointId);
            Assert.That(state.Grid.TryFindPath(
                state.Warden.Cell(state.Grid), point.Cell, out List<OfficeCell> path), Is.True);
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
