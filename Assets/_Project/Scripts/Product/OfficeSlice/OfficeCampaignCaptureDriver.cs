using System;
using System.Collections.Generic;

namespace Desk42.Product.OfficeSlice
{
    /// <summary>
    /// Deterministic command-driven setup used only by build capture and performance
    /// arguments. It uses the same public input intent and simulation commands as play.
    /// </summary>
    public static class OfficeCampaignCaptureDriver
    {
        public static void Prepare(
            OfficeCampaignState campaign,
            int shiftOrdinal,
            string stateName)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (shiftOrdinal < 1 || shiftOrdinal > 3)
                throw new ArgumentOutOfRangeException(nameof(shiftOrdinal));
            string state = (stateName ?? "opening").Trim().ToLowerInvariant();
            if (shiftOrdinal == 1)
            {
                switch (state)
                {
                    case "opening":
                    case "01-shift-1-opening":
                        return;
                    case "02-shift-1-paper-check":
                        DriveActiveCaseToWork(
                            campaign.CurrentSimulation, OfficeManualTaskKind.Compare);
                        return;
                    case "03-shift-1-money-trace":
                        DriveActiveCaseToWork(
                            campaign.CurrentSimulation, OfficeManualTaskKind.Trace);
                        return;
                    case "04-shift-1-copy-echo-warning":
                        DriveShiftOneToCopyWarning(campaign.CurrentSimulation);
                        return;
                    case "06-shift-1-upgrade-choice":
                        DriveShiftOneToUpgradeChoice(campaign);
                        return;
                    case "break":
                    case "rush":
                    case "05-shift-1-copy-echo-break":
                        DriveShiftOneToBreak(campaign.CurrentSimulation);
                        return;
                    default:
                        throw new ArgumentException(
                            "Unsupported Shift 1 capture state: " + state);
                }
            }

            EnterShiftTwo(campaign);
            if (shiftOrdinal == 2)
            {
                switch (state)
                {
                    case "opening":
                    case "07-shift-2-opening-upgrade-visible":
                        return;
                    case "rush":
                    case "08-shift-2-ghost-clock":
                        DriveShiftTwoToGhostClock(campaign.CurrentSimulation);
                        return;
                    case "09-shift-2-missing-room-access":
                        DriveShiftTwoToMissingRoom(campaign.CurrentSimulation);
                        return;
                    case "result":
                    case "10-shift-2-second-upgrade-choice":
                        DriveShiftTwoToResult(campaign);
                        return;
                    default:
                        throw new ArgumentException(
                            "Unsupported Shift 2 capture state: " + state);
                }
            }

            DriveShiftTwoToResult(campaign);
            ChooseUpgradeAndContinue(campaign, OfficeUpgradeFamily.CalmChairs);
            switch (state)
            {
                case "opening":
                case "11-shift-3-opening-both-rules":
                    return;
                case "12-shift-3-promotion-warning":
                    DriveShiftThreeToPromotionWarning(campaign.CurrentSimulation);
                    return;
                case "promotion-cascade":
                case "rush":
                case "13-shift-3-promotion-cascade":
                    TriggerPromotion(campaign.CurrentSimulation);
                    return;
                case "14-shift-3-recovery":
                {
                    OfficeSimulationState simulation = campaign.CurrentSimulation;
                    TriggerPromotion(simulation);
                    var intent = new OfficeInputIntent();
                    var input = new OfficeInputCommandGenerator(simulation, intent);
                    RecoverPromotion(simulation, intent, input);
                    return;
                }
                case "result":
                case "15-final-campaign-result":
                case "16-next-day-tease":
                    TriggerPromotion(campaign.CurrentSimulation);
                    DriveShiftThreeToResult(campaign);
                    return;
                default:
                    throw new ArgumentException(
                        "Unsupported Shift 3 capture state: " + state);
            }
        }

        private static void EnterShiftTwo(OfficeCampaignState campaign)
        {
            DriveShiftOneToUpgradeChoice(campaign);
            ChooseUpgradeAndContinue(campaign, OfficeUpgradeFamily.FastTrays);
        }

        private static void DriveActiveCaseToWork(
            OfficeSimulationState state,
            OfficeManualTaskKind targetKind)
        {
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            OfficeCustomerState customer = state.Customers.ActiveDeskCustomer;
            Require(customer != null, "Capture setup has no active customer.");
            OfficeCaseWorkDefinition work = state.WorkDefinitionFor(
                customer.LinkedAutomationClaimId);
            NavigateTo(state, intent, input, "front-desk.interact");
            CalmUntilActionable(state, intent, input);
            PressPrimary(state, intent, input);
            for (int stepIndex = 0; stepIndex < work.RequiredSequence.Count; stepIndex++)
            {
                OfficeManualTaskKind step = work.RequiredSequence[stepIndex];
                PressPrimary(state, intent, input);
                string pointId = step switch
                {
                    OfficeManualTaskKind.Compare => "paper-room.interact",
                    OfficeManualTaskKind.Trace => "money-room.interact",
                    _ => "weird-room.interact",
                };
                NavigateTo(state, intent, input, pointId);
                state.AdvanceTicks(state.Queues.TransferDurationTicks);
                ResolveComplications(state, intent, input);
                PressPrimary(state, intent, input);
                PressPrimary(state, intent, input);
                Require(state.ManualTasks.IsActive &&
                    state.ManualTasks.ActiveKind == step,
                    "Capture setup did not start the requested room work.");
                if (step == targetKind) return;
                int choice = step switch
                {
                    OfficeManualTaskKind.Compare => (int)work.PaperAnswer + 1,
                    OfficeManualTaskKind.Trace => work.MoneyPathAnswer + 1,
                    _ => work.WeirdChoiceAnswer + 1,
                };
                PressChoice(state, intent, input, choice);
            }
            throw new InvalidOperationException(
                "Requested capture work is not in the authored sequence: " + targetKind);
        }

        private static void DriveShiftOneToCopyWarning(OfficeSimulationState state)
        {
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            CompleteActiveCase(state, intent, input);
            intent.BufferToggleRule(state.CurrentTick);
            input.AdvanceOneTick();
            Require(state.AutomationRule.Enabled && !state.BreakState.Active,
                "Capture setup did not reach the Copy Echo warning.");
        }

        private static void DriveShiftOneToUpgradeChoice(OfficeCampaignState campaign)
        {
            OfficeSimulationState state = campaign.CurrentSimulation;
            DriveShiftOneToBreak(state);
            RecoverBreak(state);
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            while (state.Decisions.CommitCount < 6)
                CompleteActiveCase(state, intent, input);
            state.AdvanceOneTick();
            Require(campaign.Phase == OfficeCampaignPhase.ChooseUpgrade,
                "Shift 1 did not reach its upgrade choice.");
        }

        private static void EnsureBothRulesEnabled(OfficeSimulationState state)
        {
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            if (!state.AutomationRule.Enabled)
            {
                intent.BufferToggleRule(state.CurrentTick);
                input.AdvanceOneTick();
            }
            if (!state.PayrollRule.Enabled)
            {
                intent.BufferToggleRule2(state.CurrentTick);
                input.AdvanceOneTick();
            }
            Require(state.AutomationRule.Enabled && state.PayrollRule.Enabled,
                "Capture setup did not preserve both automation rules.");
        }

        private static void DriveShiftTwoToMissingRoom(OfficeSimulationState state)
        {
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            while (state.Decisions.CommitCount < 3)
            {
                CompleteActiveCase(state, intent, input);
                if (state.Decisions.CommitCount == 2 && !state.PayrollRule.Enabled)
                {
                    intent.BufferToggleRule2(state.CurrentTick);
                    input.AdvanceOneTick();
                }
            }
            Require(state.Customers.ActiveDeskCustomer?.DisplayName == "IRIS COLE",
                "Missing Room capture did not reach Iris.");
            NavigateTo(state, intent, input, "front-desk.interact");
            PressPrimary(state, intent, input);
            PressPrimary(state, intent, input);
            NavigateTo(state, intent, input, "weird-room.interact");
            state.AdvanceTicks(state.Queues.TransferDurationTicks);
            state.AdvanceOneTick();
            Require(state.MissingRoomAccess.Active,
                "Capture setup did not reach Missing Room access.");
        }

        private static void DriveShiftThreeToPromotionWarning(OfficeSimulationState state)
        {
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            DisablePayrollRuleForPromotion(state, intent, input);
            while (state.Decisions.CommitCount < 2)
                CompleteActiveCase(state, intent, input);
            for (int tick = 0; tick < 1800 &&
                state.PromotionCascade.PromotionFormIds.Count == 0; tick++)
                input.AdvanceOneTick();
            Require(state.PromotionCascade.PromotionFormIds.Count > 0 &&
                !state.PromotionCascade.HasTriggered,
                "Capture setup did not reach the Promotion warning.");
        }

        private static void DisablePayrollRuleForPromotion(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            if (!state.PayrollRule.Enabled) return;
            intent.BufferToggleRule2(state.CurrentTick);
            input.AdvanceOneTick();
            Require(!state.PayrollRule.Enabled,
                "Capture setup could not pause the pay rule for the Promotion route.");
        }

        private static void DriveShiftOneToBreak(OfficeSimulationState state)
        {
            if (state.BreakState.Active) return;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            CompleteActiveCase(state, intent, input);
            intent.BufferToggleRule(state.CurrentTick);
            input.AdvanceOneTick();
            CompleteActiveCase(state, intent, input);
            for (int tick = 0; tick < 1200 && !state.BreakState.Active; tick++)
                input.AdvanceOneTick();
            Require(state.BreakState.Active,
                "Capture setup did not reach the Copy Echo Break.");
        }

        private static void RecoverBreak(OfficeSimulationState state)
        {
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            NavigateTo(state, intent, input, "weird-room.interact");
            PressPrimary(state, intent, input);
            NavigateTo(state, intent, input, "front-desk.interact");
            CalmUntilActionable(state, intent, input);
            NavigateTo(state, intent, input, "money-room.interact");
            int safety = 0;
            while (state.Queues.ActiveCopyCount > 0 && safety++ < 80)
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
            Require(state.BreakState.Recovered,
                "Capture setup did not recover the Copy Echo Break.");
        }

        private static void ChooseUpgradeAndContinue(
            OfficeCampaignState campaign,
            OfficeUpgradeFamily family)
        {
            OfficeSimulationState state = campaign.CurrentSimulation;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            intent.BufferChoice((int)family, state.CurrentTick);
            input.AdvanceOneTick();
            intent.BufferInteraction(state.CurrentTick);
            input.AdvanceOneTick();
            Require(campaign.CurrentSimulation != state,
                "Capture setup did not enter the next shift.");
        }

        private static void DriveShiftTwoToGhostClock(OfficeSimulationState state)
        {
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            while (state.Decisions.CommitCount < 4)
            {
                CompleteActiveCase(state, intent, input);
                if (state.Decisions.CommitCount == 2 && !state.PayrollRule.Enabled)
                {
                    intent.BufferToggleRule2(state.CurrentTick);
                    input.AdvanceOneTick();
                }
            }
            Require(state.Customers.ActiveDeskCustomer.DisplayName == "TOMAS REED",
                "Ghost Clock capture did not reach Tomas.");
            NavigateTo(state, intent, input, "front-desk.interact");
            CalmUntilActionable(state, intent, input);
            PressPrimary(state, intent, input);
            PressPrimary(state, intent, input);
            NavigateTo(state, intent, input, "paper-room.interact");
            state.AdvanceTicks(state.Queues.TransferDurationTicks);
            for (int tick = 0; tick < 1200 &&
                !state.GhostClock.HasTriggered; tick++)
                input.AdvanceOneTick();
            Require(state.GhostClock.Active,
                "Capture setup did not reach the Ghost Clock.");
        }

        private static void DriveShiftTwoToResult(OfficeCampaignState campaign)
        {
            OfficeSimulationState state = campaign.CurrentSimulation;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            while (state.Decisions.CommitCount < 6)
            {
                if (state.Decisions.CommitCount == 5)
                    EnsureBothRulesEnabled(state);
                if (state.Customers.ActiveDeskCustomer?.DisplayName == "TOMAS REED" &&
                    state.PayrollRule.Enabled)
                {
                    intent.BufferToggleRule2(state.CurrentTick);
                    input.AdvanceOneTick();
                }
                CompleteActiveCase(state, intent, input);
                if (state.Decisions.CommitCount == 2 && !state.PayrollRule.Enabled)
                {
                    intent.BufferToggleRule2(state.CurrentTick);
                    input.AdvanceOneTick();
                }
            }
            state.AdvanceTicks(2);
            Require(campaign.Phase == OfficeCampaignPhase.ChooseUpgrade,
                "Shift 2 did not reach its upgrade choice.");
        }

        private static void TriggerPromotion(OfficeSimulationState state)
        {
            if (state.PromotionCascade.HasTriggered) return;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            DisablePayrollRuleForPromotion(state, intent, input);
            while (state.Decisions.CommitCount < 2)
                CompleteActiveCase(state, intent, input);
            for (int tick = 0; tick < 1800 &&
                !state.PromotionCascade.HasTriggered; tick++)
                input.AdvanceOneTick();
            Require(state.PromotionCascade.Active,
                "Capture setup did not reach the Promotion Cascade.");
        }

        private static void DriveShiftThreeToResult(OfficeCampaignState campaign)
        {
            OfficeSimulationState state = campaign.CurrentSimulation;
            var intent = new OfficeInputIntent();
            var input = new OfficeInputCommandGenerator(state, intent);
            RecoverPromotion(state, intent, input);
            while (state.Decisions.CommitCount < 6)
                CompleteActiveCase(state, intent, input);
            state.AdvanceTicks(2);
            Require(campaign.IsComplete,
                "Capture setup did not reach the campaign result.");
        }

        private static void RecoverPromotion(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            NavigateTo(state, intent, input, "weird-room.interact");
            PressPrimary(state, intent, input);
            PressPrimary(state, intent, input);
            ClearPromotionForms(state, intent, input);
            NavigateTo(state, intent, input, "front-desk.interact");
            CalmUntilActionable(state, intent, input);
            CompleteActiveCase(state, intent, input);
            for (int tick = 0; tick < 1200 &&
                state.PromotionCascade.DivertedFolderIds.Count < 2; tick++)
                input.AdvanceOneTick();
            NavigateTo(state, intent, input, "waiting-area.interact");
            PressPrimary(state, intent, input);
            state.AdvanceOneTick();
            Require(state.PromotionCascade.Recovered,
                "Capture setup did not recover the Promotion Cascade.");
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
            for (int i = 0; i < points.Length; i++)
            {
                NavigateTo(state, intent, input, points[i]);
                int safety = 0;
                while (state.PrimaryActionLabel == "CLEAR PROMOTION FORM" &&
                    safety++ < 12)
                {
                    PressPrimary(state, intent, input);
                    state.AdvanceTicks(
                        state.PromotionCascade.RecoveryChannelRemainingTicks);
                }
            }
        }

        private static void CompleteActiveCase(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            OfficeCustomerState customer = state.Customers.ActiveDeskCustomer;
            Require(customer != null, "Capture setup has no active customer.");
            string caseId = customer.LinkedAutomationClaimId;
            OfficeCaseWorkDefinition work = state.WorkDefinitionFor(caseId);
            NavigateTo(state, intent, input, "front-desk.interact");
            CalmUntilActionable(state, intent, input);
            PressPrimary(state, intent, input);
            for (int stepIndex = 0; stepIndex < work.RequiredSequence.Count; stepIndex++)
            {
                OfficeManualTaskKind step = work.RequiredSequence[stepIndex];
                string pointId = step switch
                {
                    OfficeManualTaskKind.Compare => "paper-room.interact",
                    OfficeManualTaskKind.Trace => "money-room.interact",
                    _ => "weird-room.interact",
                };
                PressPrimary(state, intent, input);
                NavigateTo(state, intent, input, pointId);
                state.AdvanceTicks(state.Queues.TransferDurationTicks);
                if (state.Shift.ShiftOrdinal == 2 &&
                    customer.DisplayName == "TOMAS REED" &&
                    step == OfficeManualTaskKind.Compare &&
                    !state.GhostClock.HasTriggered)
                    for (int tick = 0; tick < 1200 &&
                        !state.GhostClock.HasTriggered; tick++)
                        input.AdvanceOneTick();
                ResolveComplications(state, intent, input);
                PressPrimary(state, intent, input);
                PressPrimary(state, intent, input);
                int choice = step switch
                {
                    OfficeManualTaskKind.Compare => (int)work.PaperAnswer + 1,
                    OfficeManualTaskKind.Trace => work.MoneyPathAnswer + 1,
                    _ => work.WeirdChoiceAnswer + 1,
                };
                PressChoice(state, intent, input, choice);
            }
            PressPrimary(state, intent, input);
            NavigateTo(state, intent, input, "front-desk.interact");
            state.AdvanceTicks(state.Queues.TransferDurationTicks);
            CalmUntilActionable(state, intent, input);
            state.AdvanceOneTick();
            PressChoice(state, intent, input, 1);
            Require(state.Decisions.RecordFor(caseId) != null,
                "Capture setup did not commit the active case.");
        }

        private static void ResolveComplications(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            int safety = 0;
            while (safety++ < 24)
            {
                string action = state.PrimaryActionLabel;
                bool complication = action == "STOP CLOCK" ||
                    action == "CLEAR TIME SLIP" ||
                    action == "CLOSE MISSING ROOM" ||
                    action == "STOP COPIER" ||
                    action == "REMOVE SUPERVISOR STAMP" ||
                    action == "CLEAR PROMOTION FORM" ||
                    action == "FIND ORIGINAL BADGE";
                if (!complication) return;
                PressPrimary(state, intent, input);
                if (state.PromotionCascade.RecoveryChannelActive)
                    state.AdvanceTicks(
                        state.PromotionCascade.RecoveryChannelRemainingTicks);
            }
        }

        private static void CalmUntilActionable(
            OfficeSimulationState state,
            OfficeInputIntent intent,
            OfficeInputCommandGenerator input)
        {
            int safety = 0;
            OfficeCustomerState active = state.Customers.ActiveDeskCustomer;
            while (active != null &&
                active.VisibleMoodState >= OfficeVisibleMoodState.Upset &&
                safety++ < 8)
            {
                if (state.CustomerPressure.CalmCooldownRemainingTicks > 0)
                    state.AdvanceTicks(
                        state.CustomerPressure.CalmCooldownRemainingTicks);
                Require(state.PrimaryActionLabel == "CALM",
                    "Capture setup could not calm the active customer.");
                PressPrimary(state, intent, input);
                state.AdvanceTicks(OfficeCustomerPressureState.CalmDurationTicks);
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
            Require(point != null, "Capture setup interaction point is missing.");
            Require(state.Grid.TryFindPath(
                    state.Warden.Cell(state.Grid),
                    point.Cell,
                    out List<OfficeCell> path),
                "Capture setup route is blocked.");
            int ticksPerCell = OfficeGrid.LogicalSubunitsPerCell /
                OfficeWardenState.MovementSubunitsPerTick;
            for (int cell = 1; cell < path.Count; cell++)
            {
                int x = path[cell].X - path[cell - 1].X;
                int z = path[cell].Z - path[cell - 1].Z;
                OfficeInputDirection direction = x < 0
                    ? OfficeInputDirection.Left
                    : x > 0
                        ? OfficeInputDirection.Right
                        : z < 0
                            ? OfficeInputDirection.Down
                            : OfficeInputDirection.Up;
                intent.SetMovement(direction);
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

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
