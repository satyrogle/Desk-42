using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeWardenState
    {
        public const int MovementSubunitsPerTick = 4;

        public OfficeWardenState(OfficeCell spawn)
        {
            XSubunits = spawn.X * OfficeGrid.LogicalSubunitsPerCell;
            ZSubunits = spawn.Z * OfficeGrid.LogicalSubunitsPerCell;
        }

        public int XSubunits { get; private set; }
        public int ZSubunits { get; private set; }

        public OfficeCell Cell(OfficeGrid grid)
        {
            return grid.CellForLogicalPosition(XSubunits, ZSubunits);
        }

        public bool TryMove(int x, int z, OfficeGrid grid)
        {
            x = Math.Sign(x);
            z = Math.Sign(z);
            if (x == 0 && z == 0) return false;

            // M1 diagonal input is resolved horizontally first. This keeps keyboard
            // and controller streams identical and avoids diagonal speed advantage.
            if (x != 0 && z != 0) z = 0;

            int candidateX = XSubunits + x * MovementSubunitsPerTick;
            int candidateZ = ZSubunits + z * MovementSubunitsPerTick;
            if (!grid.IsWalkable(grid.CellForLogicalPosition(candidateX, candidateZ)))
                return false;

            XSubunits = candidateX;
            ZSubunits = candidateZ;
            return true;
        }
    }

    public sealed class OfficeSimulationState
    {
        private readonly List<OfficeCommandFailure> _failures = new();
        private readonly IReadOnlyList<OfficeCommandFailure> _readOnlyFailures;
        private int _nextSequence = 1;
        private readonly OfficeM2Scenario _m2Scenario;

        private OfficeSimulationState(
            OfficeCaseRepository cases,
            OfficeCommandLog commandLog,
            bool replayMode,
            OfficeM2Scenario m2Scenario = null,
            OfficeCampaignState campaign = null,
            OfficeCampaignUpgradeState authoredUpgrades = null,
            bool ruleOneAcceptedCopiedRefund = false,
            bool ruleTwoAcceptedCopiedPayroll = false)
        {
            _readOnlyFailures = _failures.AsReadOnly();
            Cases = cases ?? throw new ArgumentNullException(nameof(cases));
            _m2Scenario = m2Scenario;
            Campaign = campaign;
            Grid = OfficeGrid.CreateM1();
            Warden = new OfficeWardenState(Grid.SpawnCell);
            OfficeCampaignUpgradeState effectiveUpgrades =
                Campaign?.Upgrades ?? authoredUpgrades;
            Queues = new OfficeQueueService(
                Cases,
                effectiveUpgrades?.TransferDurationTicks ??
                    OfficeQueueService.DefaultTransferDurationTicks);
            Carry = new OfficeCarryState(Queues);
            CommandLog = commandLog ?? new OfficeCommandLog();
            ReplayMode = replayMode;
            if (_m2Scenario != null)
            {
                Customers = new OfficeCustomerScheduleState(_m2Scenario.Customers);
                ManualTasks = new OfficeManualTaskState(_m2Scenario);
                Decisions = new OfficeDecisionState(_m2Scenario.InstitutionalSession);
                RoomWork = new OfficeRoomWorkState();
                Staff = new OfficeStaffSystem(Grid, Queues, RoomWork, Customers);
                CustomerPressure = new OfficeCustomerPressureState(
                    Customers,
                    Queues,
                    ManualTasks,
                    effectiveUpgrades?.MoodThresholdBonusTicks ?? 0);
                AutomationRule = new OfficeAutomationRuleState(
                    _m2Scenario,
                    Queues,
                    ManualTasks,
                    Campaign?.Rules.Rule1Taught ?? false,
                    Campaign?.Rules.Rule1Enabled ?? false);
                PayrollRule = new OfficePayrollRuleState(
                    _m2Scenario,
                    Queues,
                    ManualTasks,
                    Campaign?.Rules.Rule2Taught ?? false,
                    Campaign?.Rules.Rule2Enabled ?? false,
                    seedAuthoredCopiedBadge: Campaign != null);
                BreakState = new OfficeBreakState(
                    _m2Scenario,
                    Queues,
                    effectiveUpgrades?.MaximumCopyReduction ?? 0);
                GhostClock = new OfficeGhostClockState(_m2Scenario, Queues);
                MissingRoomAccess = new OfficeMissingRoomAccessState(
                    _m2Scenario, Queues);
                PromotionCascade = new OfficePromotionCascadeState(
                    _m2Scenario,
                    Queues,
                    BreakState,
                    Staff,
                    effectiveUpgrades,
                    Campaign?.Rules,
                    ruleOneAcceptedCopiedRefund,
                    ruleTwoAcceptedCopiedPayroll);
                CausalEvents = new OfficeCausalEventLog();
                Shift = new OfficeShiftState(_m2Scenario.ShiftOrdinal);
            }
        }

        public OfficeGrid Grid { get; }
        public OfficeCaseRepository Cases { get; }
        public OfficeWardenState Warden { get; }
        public OfficeQueueService Queues { get; }
        public OfficeCarryState Carry { get; }
        public OfficeCustomerScheduleState Customers { get; }
        public OfficeManualTaskState ManualTasks { get; }
        public OfficeDecisionState Decisions { get; }
        public OfficeRoomWorkState RoomWork { get; }
        public OfficeStaffSystem Staff { get; }
        public OfficeCustomerPressureState CustomerPressure { get; }
        public OfficeAutomationRuleState AutomationRule { get; }
        public OfficePayrollRuleState PayrollRule { get; }
        public OfficeBreakState BreakState { get; }
        public OfficeGhostClockState GhostClock { get; }
        public OfficeMissingRoomAccessState MissingRoomAccess { get; }
        public OfficePromotionCascadeState PromotionCascade { get; }
        public OfficeCausalEventLog CausalEvents { get; }
        public OfficeShiftState Shift { get; }
        public OfficeCommandLog CommandLog { get; }
        public OfficeCampaignState Campaign { get; }
        public bool ReplayMode { get; }
        public bool M2Enabled => _m2Scenario != null;
        public bool M3Enabled => Campaign != null;
        public long CurrentTick { get; private set; }
        public int AppliedCommandCount { get; private set; }
        public int DecisionStubCount { get; private set; }
        public IReadOnlyList<OfficeCommandFailure> Failures => _readOnlyFailures;
        public string Checksum => OfficeStateChecksum.Compute(this);
        public string OrderedStateSnapshot => OfficeStateChecksum.Snapshot(this);
        public string PrimaryActionLabel
        {
            get
            {
                if (!M2Enabled) return "INTERACT";
                if (Campaign != null)
                {
                    if (Campaign.Phase == OfficeCampaignPhase.ChooseUpgrade)
                        return "CHOOSE AN OFFICE UPGRADE";
                    if (Campaign.Phase == OfficeCampaignPhase.ReadyForNextShift)
                        return "NEXT SHIFT";
                }
                OfficeInteractionPoint point = CurrentInteractionPoint();
                if (point == null) return "MOVE TO A WORK POINT";
                string promotionAction = PromotionCascade.ActionAt(point.Room);
                if (!string.IsNullOrWhiteSpace(promotionAction))
                    return promotionAction;
                if (GhostClock.Active && point.Room == OfficeRoomId.PaperRoom &&
                    (GhostClock.ClockTerminalActive ||
                     GhostClock.ActiveSlipCount > 0))
                    return GhostClock.ClockTerminalActive
                        ? "STOP CLOCK"
                        : "CLEAR TIME SLIP";
                if (MissingRoomAccess.Active &&
                    point.Room == OfficeRoomId.WeirdRoom)
                    return "CLOSE MISSING ROOM";
                if (BreakState.Active && point.Room == OfficeRoomId.WeirdRoom &&
                    BreakState.CopierActive) return "FIX MACHINE";
                OfficeCustomerState active = Customers.ActiveDeskCustomer;
                if (!Carry.IsCarrying && point.Room == OfficeRoomId.FrontDesk &&
                    active != null && active.VisibleMoodState >= OfficeVisibleMoodState.Upset &&
                    !CustomerPressure.CalmActive &&
                    CustomerPressure.CalmCooldownRemainingTicks == 0)
                    return "CALM";
                if (BreakState.Active && !string.IsNullOrWhiteSpace(
                        Queues.FirstActiveCopyAt(point.Room))) return "FIX COPY";
                if (Carry.IsCarrying)
                {
                    string carriedCaseId = Carry.CarriedFolderId;
                    OfficeManualTaskKind? next = ManualTasks.NextRequiredTask(
                        carriedCaseId);
                    if (point.Room == OfficeRoomId.FrontDesk)
                        return !next.HasValue
                            ? "PUT DOWN"
                            : "SEND TO " + WorkRoomLabel(next.Value);
                    if (point.Room == OfficeRoomId.PaperRoom)
                        return next == OfficeManualTaskKind.Compare
                            ? "CHECK PAPERS"
                            : "SEND TO " + WorkRoomLabel(next);
                    if (point.Room == OfficeRoomId.MoneyRoom)
                        return next == OfficeManualTaskKind.Trace
                            ? "TRACE MONEY"
                            : "SEND TO " + WorkRoomLabel(next);
                    if (point.Room == OfficeRoomId.WeirdRoom)
                        return next == OfficeManualTaskKind.WeirdCheck
                            ? "CHECK WEIRD STUFF"
                            : "SEND TO " + WorkRoomLabel(next);
                    return "SEND TO FRONT";
                }
                string activeCaseId = Customers.ActiveDeskCustomer?.LinkedAutomationClaimId;
                OfficeFolderState folder = Queues.GetFolder(activeCaseId);
                if (folder != null && !folder.IsMoving &&
                    folder.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                    folder.CurrentRoom == point.Room)
                    return "TAKE FOLDER";
                OfficeRoomWorkJobState roomJob = RoomWork.ActiveJobAt(point.Room);
                if (roomJob != null) return "HELP";
                return "NOTHING TO DO HERE";
            }
        }

        public OfficeCaseWorkDefinition WorkDefinitionFor(string automationClaimId)
        {
            return _m2Scenario?.WorkFor(automationClaimId);
        }

        public static OfficeSimulationState Create(OfficeCaseRepository cases)
        {
            return new OfficeSimulationState(cases, new OfficeCommandLog(), false);
        }

        public static OfficeSimulationState CreateM2()
        {
            OfficeM2Scenario scenario = OfficeM2Scenario.Create();
            return new OfficeSimulationState(
                scenario.Cases, new OfficeCommandLog(), false, scenario);
        }

        public static OfficeSimulationState CreateReplay(
            OfficeCaseRepository cases,
            OfficeCommandLog sourceLog)
        {
            if (sourceLog == null) throw new ArgumentNullException(nameof(sourceLog));
            return new OfficeSimulationState(cases, sourceLog.CloneForReplay(), true);
        }

        public static OfficeSimulationState CreateM2Replay(OfficeCommandLog sourceLog)
        {
            if (sourceLog == null) throw new ArgumentNullException(nameof(sourceLog));
            OfficeM2Scenario scenario = OfficeM2Scenario.Create();
            return new OfficeSimulationState(
                scenario.Cases, sourceLog.CloneForReplay(), true, scenario);
        }

        public static OfficeSimulationState CreateCampaignShift(
            OfficeM2Scenario scenario,
            OfficeCampaignState campaign)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            return new OfficeSimulationState(
                scenario.Cases,
                new OfficeCommandLog(),
                false,
                scenario,
                campaign);
        }

        public static OfficeSimulationState CreateAuthoredShift(
            OfficeM2Scenario scenario,
            OfficeCampaignUpgradeState upgrades = null,
            bool ruleOneAcceptedCopiedRefund = false,
            bool ruleTwoAcceptedCopiedPayroll = false)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            return new OfficeSimulationState(
                scenario.Cases,
                new OfficeCommandLog(),
                false,
                scenario,
                authoredUpgrades: upgrades,
                ruleOneAcceptedCopiedRefund: ruleOneAcceptedCopiedRefund,
                ruleTwoAcceptedCopiedPayroll: ruleTwoAcceptedCopiedPayroll);
        }

        public OfficeCommand CreateMoveCommand(int x, int z)
        {
            return OfficeCommand.Move(CurrentTick + 1, _nextSequence++, x, z);
        }

        public OfficeCommand CreateInteractCommand(string targetId = "")
        {
            return OfficeCommand.Interact(CurrentTick + 1, _nextSequence++, targetId);
        }

        public OfficeCommand CreateSendCommand(string caseId)
        {
            return OfficeCommand.Send(CurrentTick + 1, _nextSequence++, caseId);
        }

        public OfficeCommand CreateSendCommand(
            string caseId,
            OfficeRoomId destination)
        {
            return OfficeCommand.Send(
                CurrentTick + 1, _nextSequence++, caseId, destination);
        }

        public OfficeCommand CreateCarryCommand(string caseId)
        {
            return OfficeCommand.Carry(CurrentTick + 1, _nextSequence++, caseId);
        }

        public OfficeCommand CreateDropCommand()
        {
            return OfficeCommand.Drop(CurrentTick + 1, _nextSequence++);
        }

        public OfficeCommand CreateStartWorkCommand(
            string caseId,
            OfficeManualTaskKind kind)
        {
            return OfficeCommand.StartWork(
                CurrentTick + 1, _nextSequence++, caseId, kind);
        }

        public OfficeCommand CreateSubmitWorkChoiceCommand(int choice)
        {
            return OfficeCommand.SubmitWorkChoice(
                CurrentTick + 1, _nextSequence++, choice);
        }

        public OfficeCommand CreateCancelWorkCommand()
        {
            return OfficeCommand.CancelWork(CurrentTick + 1, _nextSequence++);
        }

        public OfficeCommand CreateHelpCommand(string jobId)
        {
            return OfficeCommand.Help(CurrentTick + 1, _nextSequence++, jobId);
        }

        public OfficeCommand CreateCalmCommand(string customerId)
        {
            return OfficeCommand.Calm(CurrentTick + 1, _nextSequence++, customerId);
        }

        public OfficeCommand CreateAssignStaffCommand(
            string staffId,
            string targetId,
            OfficeRoomId destination)
        {
            return OfficeCommand.AssignStaff(
                CurrentTick + 1, _nextSequence++, staffId, targetId, destination);
        }

        public OfficeCommand CreateToggleRuleCommand()
        {
            return OfficeCommand.ToggleRule(CurrentTick + 1, _nextSequence++);
        }

        public OfficeCommand CreateToggleRule2Command()
        {
            return OfficeCommand.ToggleRule2(CurrentTick + 1, _nextSequence++);
        }

        public OfficeCommand CreateFixCommand()
        {
            return OfficeCommand.Fix(CurrentTick + 1, _nextSequence++);
        }

        public OfficeCommand CreateRemoveSupervisorStampCommand()
        {
            return OfficeCommand.RemoveSupervisorStamp(
                CurrentTick + 1, _nextSequence++);
        }

        public OfficeCommand CreateReassignRunnerCommand()
        {
            return OfficeCommand.ReassignRunner(
                CurrentTick + 1, _nextSequence++);
        }

        public OfficeCommand CreateRestartCommand()
        {
            return OfficeCommand.Restart(CurrentTick + 1, _nextSequence++);
        }

        public OfficeCommand CreateChooseUpgradeCommand(OfficeUpgradeFamily family)
        {
            return OfficeCommand.ChooseUpgrade(
                CurrentTick + 1, _nextSequence++, family);
        }

        public OfficeCommand CreateContinueToNextShiftCommand()
        {
            return OfficeCommand.ContinueToNextShift(
                CurrentTick + 1, _nextSequence++);
        }

        public OfficeCommand CreateDecideCommand(string caseId)
        {
            return OfficeCommand.Decide(CurrentTick + 1, _nextSequence++, caseId);
        }

        public OfficeCommand CreateDecideCommand(
            string caseId,
            OfficeDecisionChoice choice)
        {
            return OfficeCommand.Decide(
                CurrentTick + 1, _nextSequence++, caseId, choice);
        }

        public OfficeCommand CreatePrimaryActionCommand()
        {
            if (!M2Enabled) return CreateInteractCommand();
            if (Campaign != null &&
                Campaign.Phase == OfficeCampaignPhase.ReadyForNextShift)
                return CreateContinueToNextShiftCommand();
            OfficeInteractionPoint point = CurrentInteractionPoint();
            if (point == null) return CreateInteractCommand();

            string promotionAction = PromotionCascade.ActionAt(point.Room);
            if (string.Equals(promotionAction, "REMOVE SUPERVISOR STAMP",
                    StringComparison.Ordinal))
                return CreateRemoveSupervisorStampCommand();
            if (string.Equals(promotionAction, "REASSIGN RUNNER",
                    StringComparison.Ordinal))
                return CreateReassignRunnerCommand();
            if (!string.IsNullOrWhiteSpace(promotionAction))
                return CreateFixCommand();

            if ((GhostClock.Active && point.Room == OfficeRoomId.PaperRoom &&
                 (GhostClock.ClockTerminalActive ||
                  GhostClock.ActiveSlipCount > 0)) ||
                (MissingRoomAccess.Active && point.Room == OfficeRoomId.WeirdRoom))
                return CreateFixCommand();

            if (BreakState.Active && point.Room == OfficeRoomId.WeirdRoom &&
                BreakState.CopierActive) return CreateFixCommand();

            OfficeCustomerState activeCustomer = Customers.ActiveDeskCustomer;
            if (!Carry.IsCarrying && point.Room == OfficeRoomId.FrontDesk &&
                activeCustomer != null &&
                activeCustomer.VisibleMoodState >= OfficeVisibleMoodState.Upset &&
                !CustomerPressure.CalmActive &&
                CustomerPressure.CalmCooldownRemainingTicks == 0)
                return CreateCalmCommand(activeCustomer.CustomerId);
            if (BreakState.Active && !string.IsNullOrWhiteSpace(
                    Queues.FirstActiveCopyAt(point.Room))) return CreateFixCommand();

            if (Carry.IsCarrying)
            {
                string caseId = Carry.CarriedFolderId;
                OfficeManualTaskKind? next = ManualTasks.NextRequiredTask(caseId);
                if (point.Room == OfficeRoomId.FrontDesk)
                {
                    if (!next.HasValue)
                        return CreateDropCommand();
                    return CreateSendCommand(caseId, RoomForWork(next.Value));
                }
                if (point.Room == OfficeRoomId.PaperRoom)
                    return next == OfficeManualTaskKind.Compare
                        ? CreateStartWorkCommand(caseId, OfficeManualTaskKind.Compare)
                        : CreateSendCommand(caseId,
                            next.HasValue ? RoomForWork(next.Value) : OfficeRoomId.FrontDesk);
                if (point.Room == OfficeRoomId.MoneyRoom)
                    return next == OfficeManualTaskKind.Trace
                        ? CreateStartWorkCommand(caseId, OfficeManualTaskKind.Trace)
                        : CreateSendCommand(caseId,
                            next.HasValue ? RoomForWork(next.Value) : OfficeRoomId.FrontDesk);
                if (point.Room == OfficeRoomId.WeirdRoom)
                    return next == OfficeManualTaskKind.WeirdCheck
                        ? CreateStartWorkCommand(caseId, OfficeManualTaskKind.WeirdCheck)
                        : CreateSendCommand(caseId,
                            next.HasValue ? RoomForWork(next.Value) : OfficeRoomId.FrontDesk);
                return CreateSendCommand(caseId,
                    next.HasValue ? RoomForWork(next.Value) : OfficeRoomId.FrontDesk);
            }

            string activeCaseId = Customers.ActiveDeskCustomer?.LinkedAutomationClaimId;
            OfficeFolderState folder = Queues.GetFolder(activeCaseId);
            if (folder != null && !folder.IsMoving &&
                folder.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                folder.CurrentRoom == point.Room)
                return CreateCarryCommand(activeCaseId);
            OfficeRoomWorkJobState roomJob = RoomWork.ActiveJobAt(point.Room);
            if (roomJob != null) return CreateHelpCommand(roomJob.JobId);
            return CreateInteractCommand(point.Id);
        }

        public OfficeCommand CreateChoiceCommand(int oneBasedChoice)
        {
            if (Campaign != null &&
                Campaign.Phase == OfficeCampaignPhase.ChooseUpgrade &&
                oneBasedChoice >= 1 && oneBasedChoice <= 3)
                return CreateChooseUpgradeCommand(
                    (OfficeUpgradeFamily)oneBasedChoice);
            if (ManualTasks != null && ManualTasks.IsActive)
                return CreateSubmitWorkChoiceCommand(oneBasedChoice - 1);
            string caseId = Customers?.ActiveDeskCustomer?.LinkedAutomationClaimId;
            if (!string.IsNullOrWhiteSpace(caseId))
            {
                OfficeCaseWorkRecord record = ManualTasks.RecordFor(caseId);
                if (ManualTasks.IsCaseComplete(caseId))
                {
                    OfficeDecisionChoice choice = oneBasedChoice == 1
                        ? OfficeDecisionChoice.HelpCustomer
                        : OfficeDecisionChoice.RejectCase;
                    return CreateDecideCommand(caseId, choice);
                }
                if (oneBasedChoice == 3)
                {
                    OfficeFolderState folder = Queues.GetFolder(caseId);
                    OfficeRoomId destination = folder == null
                        ? OfficeRoomId.FrontDesk
                        : OfficeQueueService.NextRoom(folder.CurrentRoom);
                    return CreateAssignStaffCommand(
                        OfficeStaffSystem.RunnerId, caseId, destination);
                }
                if (oneBasedChoice == 4)
                    return CreateAssignStaffCommand(
                        OfficeStaffSystem.TalkerId,
                        Customers.ActiveDeskCustomer.CustomerId,
                        OfficeRoomId.FrontDesk);
            }
            return CreateInteractCommand();
        }

        public bool TryQueueCommand(OfficeCommand command, out OfficeCommandFailure failure)
        {
            if (command == null)
            {
                failure = AddFailure(
                    CurrentTick,
                    0,
                    "MALFORMED_COMMAND",
                    "Command is null.");
                return false;
            }
            if (ReplayMode)
            {
                failure = AddFailure(
                    CurrentTick,
                    command.Sequence,
                    "LIVE_INPUT_DISABLED",
                    "Replay owns the command stream.");
                return false;
            }
            if (command.Tick <= CurrentTick)
            {
                failure = AddFailure(
                    CurrentTick,
                    command.Sequence,
                    "PAST_COMMAND",
                    "Command tick must be in the future.");
                return false;
            }
            if (!CommandLog.TryRecord(command, out string message))
            {
                failure = AddFailure(
                    CurrentTick,
                    command.Sequence,
                    "INVALID_COMMAND",
                    message);
                return false;
            }
            failure = null;
            return true;
        }

        public void AdvanceOneTick()
        {
            CurrentTick++;
            Queues.AdvanceToTick(CurrentTick);
            Customers?.AdvanceToTick(CurrentTick);
            IReadOnlyList<OfficeCommand> commands = CommandLog.Commands;
            for (int i = 0; i < commands.Count; i++)
            {
                OfficeCommand command = commands[i];
                if (command.Tick < CurrentTick) continue;
                if (command.Tick > CurrentTick) break;
                Execute(command);
            }
            if (M2Enabled)
            {
                Staff.AdvanceOneTick(CurrentTick);
                RoomWork.AdvanceOneTick(Warden.Cell(Grid));
                CustomerPressure.AdvanceOneTick(Warden.Cell(Grid), Staff);
                GhostClock.AdvanceOneTick(CurrentTick, Customers, ManualTasks);
                MissingRoomAccess.AdvanceOneTick(
                    CurrentTick, Customers, Decisions, Staff);
                BreakState.PrepareCopyCandidate(
                    CurrentTick, Customers, AutomationRule);
                AutomationRule.AdvanceOneTick(CurrentTick);
                PayrollRule.AdvanceOneTick(CurrentTick);
                BreakState.AdvanceAfterRule(
                    CurrentTick, Customers, AutomationRule);
                PromotionCascade.AdvanceOneTick(CurrentTick, Customers);
                CausalEvents.Capture(
                    CurrentTick,
                    AutomationRule,
                    BreakState,
                    Queues,
                    PayrollRule,
                    GhostClock,
                    MissingRoomAccess,
                    PromotionCascade);
                Shift.Advance(
                    CurrentTick,
                    Customers,
                    Decisions,
                    AutomationRule,
                    BreakState,
                    PayrollRule,
                    GhostClock,
                    MissingRoomAccess,
                    PromotionCascade);
                Campaign?.ObserveSimulationTick(this);
            }
        }

        public void AdvanceTicks(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            for (int i = 0; i < count; i++) AdvanceOneTick();
        }

        public void ForceAllFoldersThroughM1Route()
        {
            IReadOnlyList<string> folderIds = Queues.FolderIds;
            for (int folderIndex = 0; folderIndex < folderIds.Count; folderIndex++)
            {
                string caseId = folderIds[folderIndex];
                for (int stage = 0; stage < 4; stage++)
                {
                    OfficeCommand command = CreateSendCommand(caseId);
                    if (!TryQueueCommand(command, out OfficeCommandFailure failure))
                        throw new InvalidOperationException(failure.ToString());
                    AdvanceOneTick();
                    AdvanceTicks(OfficeQueueService.DefaultTransferDurationTicks);
                }
            }
        }

        private void Execute(OfficeCommand command)
        {
            AppliedCommandCount++;
            if (M2Enabled)
            {
                Shift.ObserveCommand(command, CurrentTick);
                if (command.Kind != OfficeCommandKind.Help)
                    RoomWork.CancelHelp();
                if (command.Kind != OfficeCommandKind.Calm)
                    CustomerPressure.CancelCalm();
            }
            switch (command.Kind)
            {
                case OfficeCommandKind.Move:
                    Warden.TryMove(command.Arg0, command.Arg1, Grid);
                    break;
                case OfficeCommandKind.Interact:
                    ExecuteInteract(command);
                    break;
                case OfficeCommandKind.Carry:
                    ExecuteCarry(command);
                    break;
                case OfficeCommandKind.Drop:
                    ExecuteDrop(command);
                    break;
                case OfficeCommandKind.Send:
                    ExecuteSend(command);
                    break;
                case OfficeCommandKind.StartWork:
                    ExecuteStartWork(command);
                    break;
                case OfficeCommandKind.SubmitWorkChoice:
                    ExecuteSubmitWorkChoice(command);
                    break;
                case OfficeCommandKind.CancelWork:
                    if (ManualTasks == null || !ManualTasks.IsActive)
                        AddFailure(CurrentTick, command.Sequence, "NO_WORK_ACTIVE",
                            "There is no active work to cancel.");
                    else
                        ManualTasks.Cancel();
                    break;
                case OfficeCommandKind.Help:
                    ExecuteHelp(command);
                    break;
                case OfficeCommandKind.Calm:
                    ExecuteCalm(command);
                    break;
                case OfficeCommandKind.AssignStaff:
                    ExecuteAssignStaff(command);
                    break;
                case OfficeCommandKind.ToggleRule:
                    if (!M2Enabled || !AutomationRule.TryToggle())
                        AddFailure(CurrentTick, command.Sequence, "RULE_LOCKED",
                            "CHECK and TRACE one folder before teaching the machine.");
                    break;
                case OfficeCommandKind.ToggleRule2:
                    if (!M2Enabled || PayrollRule == null || !PayrollRule.TryToggle())
                        AddFailure(CurrentTick, command.Sequence, "RULE_2_LOCKED",
                            "CHECK one badge and shift log before teaching the pay rule.");
                    break;
                case OfficeCommandKind.Fix:
                    ExecuteFix(command);
                    break;
                case OfficeCommandKind.RemoveSupervisorStamp:
                    ExecuteRemoveSupervisorStamp(command);
                    break;
                case OfficeCommandKind.ReassignRunner:
                    ExecuteReassignRunner(command);
                    break;
                case OfficeCommandKind.Restart:
                    if (!M2Enabled || !Shift.TryRequestRestart())
                        AddFailure(CurrentTick, command.Sequence, "RESTART_NOT_AVAILABLE",
                            "Finish the current recovery before restarting.");
                    break;
                case OfficeCommandKind.ChooseUpgrade:
                    if (Campaign == null ||
                        command.Arg0 < (int)OfficeUpgradeFamily.FastTrays ||
                        command.Arg0 > (int)OfficeUpgradeFamily.RedLabels ||
                        !Campaign.TryChooseUpgrade((OfficeUpgradeFamily)command.Arg0))
                        AddFailure(CurrentTick, command.Sequence,
                            "UPGRADE_NOT_AVAILABLE",
                            "Choose one available office upgrade after the shift.");
                    break;
                case OfficeCommandKind.ContinueToNextShift:
                    if (Campaign == null || !Campaign.TryContinueToNextShift())
                        AddFailure(CurrentTick, command.Sequence,
                            "NEXT_SHIFT_NOT_AVAILABLE",
                            "Finish the shift and choose an office upgrade first.");
                    break;
                case OfficeCommandKind.Decide:
                    ExecuteDecide(command);
                    break;
                default:
                    AddFailure(CurrentTick, command.Sequence, "UNKNOWN_COMMAND",
                        "Command kind is not supported.");
                    break;
            }
        }

        private void ExecuteInteract(OfficeCommand command)
        {
            if (M2Enabled)
            {
                AddFailure(CurrentTick, command.Sequence, "NO_CONTEXT_ACTION",
                    "Move beside the highlighted folder or work point.");
                return;
            }
            OfficeCell wardenCell = Warden.Cell(Grid);
            OfficeInteractionPoint point = string.IsNullOrWhiteSpace(command.TargetId)
                ? Grid.ChooseClosestInteractionPoint(wardenCell)
                : Grid.GetInteractionPoint(command.TargetId);
            if (point == null)
            {
                AddFailure(CurrentTick, command.Sequence, "MISSING_TARGET",
                    "No stable interaction point is available.");
                return;
            }
            int distance = Math.Abs(point.Cell.X - wardenCell.X) +
                Math.Abs(point.Cell.Z - wardenCell.Z);
            if (distance > 2)
            {
                AddFailure(CurrentTick, command.Sequence, "OUT_OF_RANGE",
                    "Interaction point is outside the six-tick interaction buffer range.");
                return;
            }
            if (!Queues.TrySendFromRoom(point.Room, CurrentTick))
                AddFailure(CurrentTick, command.Sequence, "EMPTY_QUEUE",
                    "The selected room has no folder ready to send.");
        }

        private void ExecuteCarry(OfficeCommand command)
        {
            if (!M2Enabled || string.IsNullOrWhiteSpace(command.TargetId))
            {
                AddFailure(CurrentTick, command.Sequence, "MISSING_TARGET",
                    "Take needs a highlighted folder.");
                return;
            }
            OfficeInteractionPoint point = CurrentInteractionPoint();
            OfficeCustomerState active = Customers.ActiveDeskCustomer;
            if (point == null || active == null ||
                !string.Equals(active.LinkedAutomationClaimId,
                    command.TargetId, StringComparison.Ordinal) ||
                !Carry.TryTake(command.TargetId, point.Room))
            {
                AddFailure(CurrentTick, command.Sequence, "INVALID_TAKE",
                    "That folder cannot be taken here.");
            }
        }

        private void ExecuteDrop(OfficeCommand command)
        {
            OfficeInteractionPoint point = CurrentInteractionPoint();
            if (!M2Enabled || point == null || !Carry.TryDrop(point.Room))
                AddFailure(CurrentTick, command.Sequence, "INVALID_DROP",
                    "The carried folder cannot be put down here.");
        }

        private void ExecuteSend(OfficeCommand command)
        {
            if (!M2Enabled)
            {
                if (string.IsNullOrWhiteSpace(command.TargetId) ||
                    !Queues.TrySendCase(command.TargetId, CurrentTick))
                    AddFailure(CurrentTick, command.Sequence, "MISSING_TARGET",
                        "Send target does not identify a queued folder.");
                return;
            }

            if (command.Arg0 < (int)OfficeRoomId.FrontDesk ||
                command.Arg0 > (int)OfficeRoomId.WaitingArea)
            {
                AddFailure(CurrentTick, command.Sequence, "MISSING_DESTINATION",
                    "Send needs an explicit room.");
                return;
            }
            OfficeInteractionPoint point = CurrentInteractionPoint();
            string carried = Carry.CarriedFolderId;
            OfficeRoomId destination = (OfficeRoomId)command.Arg0;
            if (point == null || string.IsNullOrWhiteSpace(carried) ||
                !string.Equals(carried, command.TargetId, StringComparison.Ordinal) ||
                destination == point.Room || !Carry.TrySend(destination, CurrentTick))
            {
                AddFailure(CurrentTick, command.Sequence, "INVALID_SEND",
                    "The folder stayed with its current owner.");
            }
        }

        private void ExecuteStartWork(OfficeCommand command)
        {
            if (!M2Enabled || ManualTasks.IsActive ||
                command.Arg0 < (int)OfficeManualTaskKind.Compare ||
                command.Arg0 > (int)OfficeManualTaskKind.WeirdCheck)
            {
                AddFailure(CurrentTick, command.Sequence, "INVALID_WORK",
                    "That work cannot start now.");
                return;
            }
            OfficeManualTaskKind kind = (OfficeManualTaskKind)command.Arg0;
            OfficeInteractionPoint point = CurrentInteractionPoint();
            OfficeRoomId requiredRoom = RoomForWork(kind);
            OfficeCustomerState active = Customers.ActiveDeskCustomer;
            if (point == null || point.Room != requiredRoom || active == null ||
                !string.Equals(active.LinkedAutomationClaimId,
                    command.TargetId, StringComparison.Ordinal) ||
                !string.Equals(Carry.CarriedFolderId,
                    command.TargetId, StringComparison.Ordinal))
            {
                AddFailure(CurrentTick, command.Sequence, "INVALID_WORK",
                    "Bring the active folder to the right room.");
                return;
            }
            if (!ManualTasks.TryStart(kind, command.TargetId, CurrentTick,
                    out string failure))
                AddFailure(CurrentTick, command.Sequence, "INVALID_WORK", failure);
        }

        private void ExecuteSubmitWorkChoice(OfficeCommand command)
        {
            if (!M2Enabled || !ManualTasks.IsActive)
            {
                AddFailure(CurrentTick, command.Sequence, "NO_WORK_ACTIVE",
                    "There is no paper or money choice to submit.");
                return;
            }
            OfficeManualTaskKind completedKind = ManualTasks.ActiveKind;
            string caseId = ManualTasks.ActiveCaseId;
            if (!ManualTasks.TrySubmit(command.Arg0, out bool completed,
                    out string result))
            {
                AddFailure(CurrentTick, command.Sequence, "INVALID_WORK_CHOICE", result);
                return;
            }
            if (!completed) return;
            if (completedKind == OfficeManualTaskKind.Compare)
                Customers.MarkPapersChecked(caseId);
            else if (completedKind == OfficeManualTaskKind.Trace)
                Customers.MarkMoneyTraced(caseId);
        }

        private void ExecuteDecide(OfficeCommand command)
        {
            if (!M2Enabled)
            {
                AddFailure(CurrentTick, command.Sequence, "DECIDE_UNAVAILABLE",
                    "This command log does not contain an active M2 case.");
                return;
            }
            OfficeCustomerState active = Customers.ActiveDeskCustomer;
            OfficeCaseWorkRecord work = ManualTasks.RecordFor(command.TargetId);
            OfficeFolderState folder = Queues.GetFolder(command.TargetId);
            if (active == null ||
                !string.Equals(active.LinkedAutomationClaimId,
                    command.TargetId, StringComparison.Ordinal) ||
                work == null || !ManualTasks.IsCaseComplete(command.TargetId) ||
                folder == null || folder.IsMoving ||
                folder.OwnerKind != OfficeFolderOwnerKind.RoomQueue ||
                folder.CurrentRoom != OfficeRoomId.FrontDesk ||
                command.Arg0 < (int)OfficeDecisionChoice.RejectCase ||
                command.Arg0 > (int)OfficeDecisionChoice.HelpCustomer)
            {
                AddFailure(CurrentTick, command.Sequence, "DECISION_NOT_READY",
                    "Check papers, trace money, and return the folder first.");
                return;
            }
            if (!Decisions.TryCommit(
                    command.TargetId,
                    (OfficeDecisionChoice)command.Arg0,
                    out OfficeDecisionRecord ignored,
                    out string failure))
            {
                AddFailure(CurrentTick, command.Sequence, "DECISION_REJECTED", failure);
                return;
            }
            Customers.MarkDecisionMade(command.TargetId);
        }

        private void ExecuteHelp(OfficeCommand command)
        {
            if (!M2Enabled)
            {
                AddFailure(CurrentTick, command.Sequence, "HELP_NOT_AVAILABLE",
                    "There is no active room task to help here.");
                return;
            }
            OfficeInteractionPoint point = CurrentInteractionPoint();
            OfficeRoomWorkJobState job = point == null
                ? null
                : RoomWork.ActiveJobAt(point.Room);
            if (point == null || job == null ||
                (!string.IsNullOrWhiteSpace(command.TargetId) &&
                    !string.Equals(job.JobId, command.TargetId,
                        StringComparison.Ordinal)) ||
                !RoomWork.TryStartHelp(point.Room, Warden.Cell(Grid)))
                AddFailure(CurrentTick, command.Sequence, "HELP_NOT_AVAILABLE",
                    "There is no active room task to help here.");
        }

        private void ExecuteCalm(OfficeCommand command)
        {
            OfficeInteractionPoint point = CurrentInteractionPoint();
            if (!M2Enabled || point == null || point.Room != OfficeRoomId.FrontDesk ||
                !CustomerPressure.TryStartCalm(
                    command.TargetId, Warden.Cell(Grid)))
                AddFailure(CurrentTick, command.Sequence, "CALM_NOT_AVAILABLE",
                    "The customer cannot be calmed here yet.");
        }

        private void ExecuteAssignStaff(OfficeCommand command)
        {
            if (!M2Enabled || command.Arg0 < (int)OfficeRoomId.FrontDesk ||
                command.Arg0 > (int)OfficeRoomId.WaitingArea)
            {
                AddFailure(CurrentTick, command.Sequence, "STAFF_ASSIGNMENT_REJECTED",
                    "That staff task is not available.");
                return;
            }
            if (!Staff.TryAssign(
                    command.TargetId,
                    command.TextArg,
                    (OfficeRoomId)command.Arg0,
                    CurrentTick,
                    out string failure))
                AddFailure(CurrentTick, command.Sequence,
                    "STAFF_ASSIGNMENT_REJECTED", failure);
        }

        private void ExecuteFix(OfficeCommand command)
        {
            OfficeInteractionPoint point = CurrentInteractionPoint();
            if (!M2Enabled || point == null)
            {
                AddFailure(CurrentTick, command.Sequence, "FIX_NOT_AVAILABLE",
                    "There is nothing to fix here.");
                return;
            }
            bool fixedSomething = PromotionCascade.TryFixAt(
                    point.Room, out string result) ||
                BreakState.TryFixAt(point.Room, out result) ||
                GhostClock.TryFixAt(point.Room, out result) ||
                MissingRoomAccess.TryCloseAt(point.Room, Staff);
            if (!fixedSomething)
                AddFailure(CurrentTick, command.Sequence, "FIX_NOT_AVAILABLE",
                    "There is nothing to fix here.");
        }

        private void ExecuteRemoveSupervisorStamp(OfficeCommand command)
        {
            OfficeInteractionPoint point = CurrentInteractionPoint();
            if (!M2Enabled || point == null ||
                !PromotionCascade.TryRemoveSupervisorStamp(point.Room))
                AddFailure(CurrentTick, command.Sequence, "STAMP_NOT_AVAILABLE",
                    "The supervisor stamp cannot be removed here.");
        }

        private void ExecuteReassignRunner(OfficeCommand command)
        {
            OfficeInteractionPoint point = CurrentInteractionPoint();
            if (!M2Enabled || point == null ||
                !PromotionCascade.TryReassignRunner(point.Room))
                AddFailure(CurrentTick, command.Sequence, "RUNNER_NOT_AVAILABLE",
                    "The Runner cannot be reassigned here yet.");
        }

        private static OfficeRoomId RoomForWork(OfficeManualTaskKind kind)
        {
            return kind switch
            {
                OfficeManualTaskKind.Compare => OfficeRoomId.PaperRoom,
                OfficeManualTaskKind.Trace => OfficeRoomId.MoneyRoom,
                OfficeManualTaskKind.WeirdCheck => OfficeRoomId.WeirdRoom,
                _ => OfficeRoomId.FrontDesk,
            };
        }

        private static string WorkRoomLabel(OfficeManualTaskKind? kind)
        {
            if (!kind.HasValue) return "FRONT";
            return kind.Value switch
            {
                OfficeManualTaskKind.Compare => "PAPERS",
                OfficeManualTaskKind.Trace => "MONEY",
                OfficeManualTaskKind.WeirdCheck => "WEIRD",
                _ => "FRONT",
            };
        }

        private OfficeInteractionPoint CurrentInteractionPoint()
        {
            return Grid.ChooseClosestInteractionPoint(Warden.Cell(Grid));
        }

        private OfficeCommandFailure AddFailure(
            long tick,
            int sequence,
            string code,
            string message)
        {
            var failure = new OfficeCommandFailure(tick, sequence, code, message);
            _failures.Add(failure);
            return failure;
        }
    }

    public sealed class OfficeSimulationClock
    {
        public const int TicksPerSecond = 30;
        public const int DefaultMaximumCatchUpTicks = 4;
        public const double TickDurationSeconds = 1d / TicksPerSecond;
        private const double TickComparisonEpsilon = 0.000000000001d;

        private double _accumulator;

        public OfficeSimulationClock(int maximumCatchUpTicks = DefaultMaximumCatchUpTicks)
        {
            if (maximumCatchUpTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumCatchUpTicks));
            MaximumCatchUpTicks = maximumCatchUpTicks;
        }

        public int MaximumCatchUpTicks { get; }
        public long CurrentTick { get; private set; }
        public bool Paused { get; private set; }

        public int Advance(double unscaledDeltaTime, Action tick)
        {
            if (tick == null) throw new ArgumentNullException(nameof(tick));
            if (Paused) return 0;

            _accumulator += Math.Max(0d, unscaledDeltaTime);
            int executed = 0;
            while (_accumulator + TickComparisonEpsilon >= TickDurationSeconds &&
                executed < MaximumCatchUpTicks)
            {
                tick();
                CurrentTick++;
                _accumulator -= TickDurationSeconds;
                if (_accumulator < 0d && _accumulator > -TickComparisonEpsilon)
                    _accumulator = 0d;
                executed++;
            }
            if (executed == MaximumCatchUpTicks && _accumulator > TickDurationSeconds)
                _accumulator = TickDurationSeconds;
            return executed;
        }

        public bool Step(Action tick)
        {
            if (tick == null) throw new ArgumentNullException(nameof(tick));
            tick();
            CurrentTick++;
            _accumulator = 0d;
            return true;
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            if (paused) _accumulator = 0d;
        }
    }

    public static class OfficeStateChecksum
    {
        public static string Compute(OfficeSimulationState state)
        {
            string snapshot = Snapshot(state);
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < snapshot.Length; i++)
            {
                hash ^= snapshot[i];
                hash *= 1099511628211UL;
            }
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        public static string Snapshot(OfficeSimulationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var builder = new StringBuilder();
            builder.Append("tick=").Append(state.CurrentTick);
            builder.Append("|warden=").Append(state.Warden.XSubunits).Append(',')
                .Append(state.Warden.ZSubunits);
            builder.Append("|commands=").Append(state.CommandLog.Commands.Count)
                .Append('|').Append(state.AppliedCommandCount);
            builder.Append("|transfer-duration=").Append(
                state.Queues.TransferDurationTicks);
            foreach (OfficeRoomId room in Enum.GetValues(typeof(OfficeRoomId)))
            {
                builder.Append("|queue=").Append(room).Append(':');
                IReadOnlyList<string> ids = state.Queues.GetQueue(room).CaseIds;
                for (int i = 0; i < ids.Count; i++)
                {
                    if (i > 0) builder.Append(',');
                    builder.Append(ids[i]);
                }
            }
            IReadOnlyList<string> folderIds = state.Queues.FolderIds;
            for (int i = 0; i < folderIds.Count; i++)
            {
                OfficeFolderState folder = state.Queues.GetFolder(folderIds[i]);
                builder.Append("|folder=").Append(folder.CaseId).Append(':')
                    .Append(folder.SourceCaseId).Append(':')
                    .Append(folder.CurrentRoom).Append(':').Append(folder.IsMoving)
                    .Append(':').Append(folder.SourceRoom).Append(':')
                    .Append(folder.DestinationRoom).Append(':')
                    .Append(folder.TransferStartTick).Append(':')
                    .Append(folder.TransferEndTick).Append(':')
                    .Append(folder.OwnerKind).Append(':').Append(folder.OwnerId);
            }
            if (state.M2Enabled)
            {
                state.Customers.AppendSnapshot(builder);
                state.ManualTasks.AppendSnapshot(builder, state.Cases.Cases);
                state.Decisions.AppendSnapshot(builder, state.Cases.Cases);
                state.RoomWork.AppendSnapshot(builder);
                state.Staff.AppendSnapshot(builder);
                state.CustomerPressure.AppendSnapshot(builder);
                state.AutomationRule.AppendSnapshot(builder);
                state.PayrollRule.AppendSnapshot(builder);
                state.BreakState.AppendSnapshot(builder);
                state.GhostClock.AppendSnapshot(builder);
                state.MissingRoomAccess.AppendSnapshot(builder, state.Staff);
                state.PromotionCascade.AppendSnapshot(builder);
                state.CausalEvents.AppendSnapshot(builder);
                state.Shift.AppendSnapshot(builder);
            }
            for (int i = 0; i < state.Failures.Count; i++)
                builder.Append("|failure=").Append(state.Failures[i]);
            return builder.ToString();
        }
    }
}
