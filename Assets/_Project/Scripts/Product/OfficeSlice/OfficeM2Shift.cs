using System;
using System.Collections.Generic;
using System.Text;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeShiftPhase
    {
        Briefing,
        Open,
        Learn,
        Rush,
        Automate,
        Break,
        Recovery,
        Closing,
        Result,
    }

    public enum OfficeCausalEventKind
    {
        RuleTaught,
        CopiedFolderMatched,
        CopySentToMoney,
        MoneyFilled,
        MachineStopped,
        OriginalFound,
        RuleTwoTaught,
        PayrollFolderMatched,
        GhostClockStarted,
        ClockStopped,
        TimeSlipCleared,
        MissingRoomOpened,
        MissingRoomClosed,
        PromotionFormReceived,
        MachinePromoted,
        RunnerFollowedCopier,
        FolderDiverted,
        SupervisorStampRemoved,
        PromotionFormCleared,
        OriginalBadgeFound,
        OriginalBadgeReturned,
        RunnerReassigned,
        PromotionRecovered,
    }

    public sealed class OfficeCausalEvent
    {
        internal OfficeCausalEvent(
            string eventId,
            long tick,
            OfficeCausalEventKind kind,
            string playerText,
            string observableSourceId)
        {
            EventId = eventId;
            Tick = tick;
            Kind = kind;
            PlayerText = playerText;
            ObservableSourceId = observableSourceId;
        }

        public string EventId { get; }
        public long Tick { get; }
        public OfficeCausalEventKind Kind { get; }
        public string PlayerText { get; }
        public string ObservableSourceId { get; }
    }

    public sealed class OfficeCausalEventLog
    {
        private readonly List<OfficeCausalEvent> _events = new();
        private readonly IReadOnlyList<OfficeCausalEvent> _readOnlyEvents;
        private readonly HashSet<string> _eventIds = new(StringComparer.Ordinal);
        private int _observedRuleMatchCount;
        private int _observedPayrollMatchCount;
        private int _observedClearedSlipCount;
        private int _observedPromotionDiversionCount;
        private int _observedClearedPromotionFormCount;

        public OfficeCausalEventLog()
        {
            _readOnlyEvents = _events.AsReadOnly();
        }

        public IReadOnlyList<OfficeCausalEvent> Events => _readOnlyEvents;

        public void Capture(
            long currentTick,
            OfficeAutomationRuleState rule,
            OfficeBreakState breakState,
            OfficeQueueService queues,
            OfficePayrollRuleState payrollRule = null,
            OfficeGhostClockState ghostClock = null,
            OfficeMissingRoomAccessState missingRoom = null,
            OfficePromotionCascadeState promotion = null)
        {
            if (rule.Enabled)
                TryRecord("rule-taught", currentTick,
                    OfficeCausalEventKind.RuleTaught,
                    "YOU TAUGHT THE MACHINE TO SEND MATCHING REFUNDS TO MONEY",
                    "auto-sorter");

            while (_observedRuleMatchCount < rule.Matches.Count)
            {
                OfficeAutomationRuleMatch match = rule.Matches[_observedRuleMatchCount++];
                OfficeFolderState folder = queues.GetFolder(match.FolderId);
                if (!match.Matched || folder == null || !folder.IsCopy) continue;
                TryRecord("copy-matched:" + match.FolderId, match.Tick,
                    OfficeCausalEventKind.CopiedFolderMatched,
                    "THE COPIED FOLDER MATCHED THE RULE",
                    match.FolderId);
                TryRecord("copy-sent-money:" + match.FolderId, match.Tick,
                    OfficeCausalEventKind.CopySentToMoney,
                    "THE MACHINE SENT THE COPY TO MONEY",
                    match.FolderId);
            }

            int copiesAtMoney = 0;
            for (int i = 0; i < queues.FolderIds.Count; i++)
            {
                OfficeFolderState folder = queues.GetFolder(queues.FolderIds[i]);
                if (folder != null && folder.IsCopy &&
                    folder.OwnerKind != OfficeFolderOwnerKind.Cleared &&
                    ((folder.IsMoving && folder.DestinationRoom == OfficeRoomId.MoneyRoom) ||
                     (!folder.IsMoving && folder.CurrentRoom == OfficeRoomId.MoneyRoom)))
                    copiesAtMoney++;
            }
            if (copiesAtMoney >= 2)
                TryRecord("money-filled", currentTick,
                    OfficeCausalEventKind.MoneyFilled,
                    "COPIED FOLDERS FILLED MONEY",
                    "money-room.interact");
            if (breakState.Active && !breakState.CopierActive)
                TryRecord("machine-stopped", currentTick,
                    OfficeCausalEventKind.MachineStopped,
                    "YOU STOPPED THE COPY ECHO",
                    "weird-room.interact");
            if (breakState.OriginalFound)
                TryRecord("original-found", currentTick,
                    OfficeCausalEventKind.OriginalFound,
                    "YOU FOUND THE ORIGINAL FOLDER",
                    breakState.OriginalFolderId);
            if (payrollRule != null && payrollRule.Enabled)
                TryRecord("pay-rule-taught", currentTick,
                    OfficeCausalEventKind.RuleTwoTaught,
                    "YOU TAUGHT THE MACHINE TO SEND MATCHING PAY RECORDS TO MONEY",
                    "pay-sorter");
            while (payrollRule != null &&
                _observedPayrollMatchCount < payrollRule.Matches.Count)
            {
                OfficePayrollRuleMatch match =
                    payrollRule.Matches[_observedPayrollMatchCount++];
                if (!match.Matched) continue;
                TryRecord("pay-match:" + match.FolderId, match.Tick,
                    OfficeCausalEventKind.PayrollFolderMatched,
                    "A BADGE AND SHIFT LOG MATCHED THE PAY RULE",
                    match.FolderId);
            }
            if (ghostClock != null && ghostClock.HasTriggered)
                TryRecord("ghost-clock-started", ghostClock.TriggeredTick,
                    OfficeCausalEventKind.GhostClockStarted,
                    "THE CLOCK MADE ANOTHER TIME SLIP",
                    "paper-room.clock-terminal");
            if (ghostClock != null && ghostClock.HasTriggered &&
                !ghostClock.ClockTerminalActive)
                TryRecord("ghost-clock-stopped", currentTick,
                    OfficeCausalEventKind.ClockStopped,
                    "YOU STOPPED THE CLOCK TERMINAL",
                    "paper-room.clock-terminal");
            while (ghostClock != null &&
                _observedClearedSlipCount < ghostClock.ClearedSlipCount)
            {
                _observedClearedSlipCount++;
                TryRecord("time-slip-cleared:" + _observedClearedSlipCount,
                    currentTick,
                    OfficeCausalEventKind.TimeSlipCleared,
                    "YOU CLEARED A COPIED TIME SLIP",
                    "time-slip." + _observedClearedSlipCount.ToString("D3"));
            }
            if (missingRoom != null && missingRoom.HasTriggered)
                TryRecord("missing-room-opened", missingRoom.TriggeredTick,
                    OfficeCausalEventKind.MissingRoomOpened,
                    "IRIS'S OLD CARD OPENED THE MISSING ROOM",
                    "missing-room-door");
            if (missingRoom != null && missingRoom.Recovered)
                TryRecord("missing-room-closed", currentTick,
                    OfficeCausalEventKind.MissingRoomClosed,
                    "YOU CLOSED THE MISSING ROOM",
                    "missing-room-door");
            if (promotion != null && promotion.HasTriggered)
            {
                TryRecord("promotion-form-received", promotion.TriggeredTick,
                    OfficeCausalEventKind.PromotionFormReceived,
                    "THE COPIER RECEIVED A PROMOTION FORM",
                    "promotion-form.001");
                TryRecord("machine-promoted", promotion.TriggeredTick,
                    OfficeCausalEventKind.MachinePromoted,
                    "THE MACHINE BECAME A SUPERVISOR",
                    "supervisor-stamp");
                TryRecord("runner-followed-copier", promotion.TriggeredTick,
                    OfficeCausalEventKind.RunnerFollowedCopier,
                    "THE RUNNER FOLLOWED THE COPIER'S ORDERS",
                    OfficeStaffSystem.RunnerId);
            }
            while (promotion != null &&
                _observedPromotionDiversionCount <
                    promotion.DivertedFolderIds.Count)
            {
                string folderId = promotion.DivertedFolderIds[
                    _observedPromotionDiversionCount++];
                TryRecord("promotion-diverted:" + folderId, currentTick,
                    OfficeCausalEventKind.FolderDiverted,
                    "THE COPIER SENT A CASE TO THE WRONG ROOM",
                    folderId);
            }
            if (promotion != null && promotion.HasTriggered &&
                !promotion.CopierActive)
                TryRecord("promotion-copier-stopped", currentTick,
                    OfficeCausalEventKind.MachineStopped,
                    "YOU STOPPED THE COPY ECHO",
                    "weird-room.interact");
            if (promotion != null && promotion.HasTriggered &&
                !promotion.SupervisorStampActive)
                TryRecord("supervisor-stamp-removed", currentTick,
                    OfficeCausalEventKind.SupervisorStampRemoved,
                    "YOU REMOVED THE SUPERVISOR STAMP",
                    "supervisor-stamp");
            while (promotion != null &&
                _observedClearedPromotionFormCount <
                    promotion.ClearedPromotionFormCount)
            {
                _observedClearedPromotionFormCount++;
                TryRecord("promotion-form-cleared:" +
                        _observedClearedPromotionFormCount,
                    currentTick,
                    OfficeCausalEventKind.PromotionFormCleared,
                    "YOU CLEARED A COPIED PROMOTION FORM",
                    "promotion-form." +
                        _observedClearedPromotionFormCount.ToString("D3"));
            }
            if (promotion != null && promotion.OriginalBadgeFound)
                TryRecord("promotion-original-found", currentTick,
                    OfficeCausalEventKind.OriginalBadgeFound,
                    "YOU FOUND MARA'S ORIGINAL BADGE FILE",
                    promotion.MaraCaseId);
            if (promotion != null && promotion.OriginalBadgeReturned)
                TryRecord("promotion-original-returned", currentTick,
                    OfficeCausalEventKind.OriginalBadgeReturned,
                    "YOU RETURNED THE ORIGINAL BADGE FILE TO THE FRONT DESK",
                    promotion.MaraCaseId);
            if (promotion != null && promotion.RunnerReassigned)
                TryRecord("promotion-runner-reassigned", currentTick,
                    OfficeCausalEventKind.RunnerReassigned,
                    "YOU REASSIGNED THE RUNNER TO THE WARDEN",
                    OfficeStaffSystem.RunnerId);
            if (promotion != null && promotion.Recovered)
                TryRecord("promotion-recovered", currentTick,
                    OfficeCausalEventKind.PromotionRecovered,
                    "YOU STOPPED THE COPIER AND RESTORED THE ORIGINAL FILE",
                    promotion.MaraCaseId);
        }

        public bool ContainsOnlyObservableEvents()
        {
            for (int i = 0; i < _events.Count; i++)
            {
                OfficeCausalEvent value = _events[i];
                if (string.IsNullOrWhiteSpace(value.EventId) ||
                    string.IsNullOrWhiteSpace(value.PlayerText) ||
                    string.IsNullOrWhiteSpace(value.ObservableSourceId)) return false;
                switch (value.Kind)
                {
                    case OfficeCausalEventKind.RuleTaught:
                    case OfficeCausalEventKind.CopiedFolderMatched:
                    case OfficeCausalEventKind.CopySentToMoney:
                    case OfficeCausalEventKind.MoneyFilled:
                    case OfficeCausalEventKind.MachineStopped:
                    case OfficeCausalEventKind.OriginalFound:
                    case OfficeCausalEventKind.RuleTwoTaught:
                    case OfficeCausalEventKind.PayrollFolderMatched:
                    case OfficeCausalEventKind.GhostClockStarted:
                    case OfficeCausalEventKind.ClockStopped:
                    case OfficeCausalEventKind.TimeSlipCleared:
                    case OfficeCausalEventKind.MissingRoomOpened:
                    case OfficeCausalEventKind.MissingRoomClosed:
                    case OfficeCausalEventKind.PromotionFormReceived:
                    case OfficeCausalEventKind.MachinePromoted:
                    case OfficeCausalEventKind.RunnerFollowedCopier:
                    case OfficeCausalEventKind.FolderDiverted:
                    case OfficeCausalEventKind.SupervisorStampRemoved:
                    case OfficeCausalEventKind.PromotionFormCleared:
                    case OfficeCausalEventKind.OriginalBadgeFound:
                    case OfficeCausalEventKind.OriginalBadgeReturned:
                    case OfficeCausalEventKind.RunnerReassigned:
                    case OfficeCausalEventKind.PromotionRecovered:
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        public void AppendSnapshot(StringBuilder builder)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                OfficeCausalEvent value = _events[i];
                builder.Append("|cause=").Append(value.EventId).Append(':')
                    .Append(value.Tick).Append(':').Append(value.Kind).Append(':')
                    .Append(value.PlayerText).Append(':')
                    .Append(value.ObservableSourceId);
            }
        }

        private void TryRecord(
            string eventId,
            long tick,
            OfficeCausalEventKind kind,
            string playerText,
            string sourceId)
        {
            if (!_eventIds.Add(eventId)) return;
            _events.Add(new OfficeCausalEvent(
                eventId, tick, kind, playerText, sourceId));
        }
    }

    public sealed class OfficeShiftState
    {
        public const int FailureGraceTicks = 1800;

        public OfficeShiftState(int shiftOrdinal = 1)
        {
            if (shiftOrdinal < 1 || shiftOrdinal > 3)
                throw new ArgumentOutOfRangeException(nameof(shiftOrdinal));
            ShiftOrdinal = shiftOrdinal;
        }

        public int ShiftOrdinal { get; }
        public OfficeShiftPhase Phase { get; private set; } = OfficeShiftPhase.Briefing;
        public long PhaseStartedTick { get; private set; }
        public bool Failed { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;
        public bool RestartRequested { get; private set; }
        public bool Success => Phase == OfficeShiftPhase.Result && !Failed;

        public void ObserveCommand(OfficeCommand command, long currentTick)
        {
            if (command == null) return;
            if (Phase == OfficeShiftPhase.Briefing &&
                (command.Kind == OfficeCommandKind.Carry ||
                 command.Kind == OfficeCommandKind.Interact))
                Transition(OfficeShiftPhase.Open, currentTick);
        }

        public void Advance(
            long currentTick,
            OfficeCustomerScheduleState customers,
            OfficeDecisionState decisions,
            OfficeAutomationRuleState rule,
            OfficeBreakState breakState,
            OfficePayrollRuleState payrollRule = null,
            OfficeGhostClockState ghostClock = null,
            OfficeMissingRoomAccessState missingRoom = null,
            OfficePromotionCascadeState promotion = null)
        {
            if (Failed || Phase == OfficeShiftPhase.Result) return;
            if (ShiftOrdinal == 2)
            {
                AdvanceShiftTwo(
                    currentTick,
                    decisions,
                    payrollRule,
                    ghostClock,
                    missingRoom);
                return;
            }
            if (ShiftOrdinal == 3)
            {
                AdvanceShiftThree(
                    currentTick,
                    decisions,
                    rule,
                    payrollRule,
                    promotion);
                return;
            }
            OfficeCustomerState active = customers.ActiveDeskCustomer;
            if (breakState.Active && !breakState.Recovered && active != null &&
                active.VisibleMoodState == OfficeVisibleMoodState.Break &&
                currentTick - breakState.TriggeredTick > FailureGraceTicks)
            {
                Fail("THE OFFICE COULD NOT RECOVER THE ORIGINAL FOLDER");
                return;
            }

            switch (Phase)
            {
                case OfficeShiftPhase.Briefing:
                    break;
                case OfficeShiftPhase.Open:
                    if (decisions.CommitCount >= 1)
                        Transition(OfficeShiftPhase.Learn, currentTick);
                    break;
                case OfficeShiftPhase.Learn:
                    if (rule.Unlocked)
                        Transition(OfficeShiftPhase.Rush, currentTick);
                    break;
                case OfficeShiftPhase.Rush:
                    if (rule.Enabled)
                        Transition(OfficeShiftPhase.Automate, currentTick);
                    break;
                case OfficeShiftPhase.Automate:
                    if (breakState.Active)
                        Transition(OfficeShiftPhase.Break, currentTick);
                    break;
                case OfficeShiftPhase.Break:
                    if (!breakState.CopierActive ||
                        breakState.ClearedCopyCount > 0 || breakState.OriginalFound)
                        Transition(OfficeShiftPhase.Recovery, currentTick);
                    break;
                case OfficeShiftPhase.Recovery:
                    if (breakState.Recovered)
                        Transition(OfficeShiftPhase.Closing, currentTick);
                    break;
                case OfficeShiftPhase.Closing:
                    if (breakState.Recovered && decisions.CommitCount >= 6)
                        Transition(OfficeShiftPhase.Result, currentTick);
                    break;
            }
        }

        public bool TryRequestRestart()
        {
            if (!Failed && Phase > OfficeShiftPhase.Rush &&
                Phase != OfficeShiftPhase.Result) return false;
            RestartRequested = true;
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void ForceFailureForDevelopment()
        {
            Fail("DEVELOPMENT FAILURE CHECK");
        }
#endif

        public void AppendSnapshot(StringBuilder builder)
        {
            builder.Append("|shift=").Append(ShiftOrdinal).Append(':')
                .Append(Phase).Append(':')
                .Append(PhaseStartedTick).Append(':').Append(Failed).Append(':')
                .Append(FailureReason).Append(':').Append(RestartRequested);
        }

        private void AdvanceShiftTwo(
            long currentTick,
            OfficeDecisionState decisions,
            OfficePayrollRuleState payrollRule,
            OfficeGhostClockState ghostClock,
            OfficeMissingRoomAccessState missingRoom)
        {
            switch (Phase)
            {
                case OfficeShiftPhase.Briefing:
                    break;
                case OfficeShiftPhase.Open:
                    if (decisions.CommitCount >= 1)
                        Transition(OfficeShiftPhase.Learn, currentTick);
                    break;
                case OfficeShiftPhase.Learn:
                    if (payrollRule != null && payrollRule.Unlocked)
                        Transition(OfficeShiftPhase.Rush, currentTick);
                    break;
                case OfficeShiftPhase.Rush:
                    if (payrollRule != null && payrollRule.Enabled)
                        Transition(OfficeShiftPhase.Automate, currentTick);
                    break;
                case OfficeShiftPhase.Automate:
                    if (decisions.CommitCount >= 6 && ghostClock != null &&
                        ghostClock.HasTriggered && ghostClock.Recovered &&
                        missingRoom != null && missingRoom.HasTriggered &&
                        missingRoom.Recovered)
                        Transition(OfficeShiftPhase.Closing, currentTick);
                    break;
                case OfficeShiftPhase.Closing:
                    Transition(OfficeShiftPhase.Result, currentTick);
                    break;
            }
        }

        private void AdvanceShiftThree(
            long currentTick,
            OfficeDecisionState decisions,
            OfficeAutomationRuleState rule,
            OfficePayrollRuleState payrollRule,
            OfficePromotionCascadeState promotion)
        {
            if (promotion != null && promotion.Failed)
            {
                Fail(promotion.FailureReason);
                return;
            }
            switch (Phase)
            {
                case OfficeShiftPhase.Briefing:
                    break;
                case OfficeShiftPhase.Open:
                    if (decisions.CommitCount >= 1)
                        Transition(OfficeShiftPhase.Learn, currentTick);
                    break;
                case OfficeShiftPhase.Learn:
                    if (rule != null && rule.Unlocked &&
                        payrollRule != null && payrollRule.Unlocked)
                        Transition(OfficeShiftPhase.Rush, currentTick);
                    break;
                case OfficeShiftPhase.Rush:
                case OfficeShiftPhase.Automate:
                    if (promotion != null && promotion.Active)
                        Transition(OfficeShiftPhase.Break, currentTick);
                    break;
                case OfficeShiftPhase.Break:
                    if (promotion != null &&
                        (!promotion.CopierActive ||
                         !promotion.SupervisorStampActive ||
                         promotion.ClearedPromotionFormCount > 0 ||
                         promotion.MaraCalmed ||
                         promotion.OriginalBadgeFound ||
                         promotion.RunnerReassigned))
                        Transition(OfficeShiftPhase.Recovery, currentTick);
                    break;
                case OfficeShiftPhase.Recovery:
                    if (promotion != null && promotion.Recovered &&
                        decisions.CommitCount >= 6)
                        Transition(OfficeShiftPhase.Closing, currentTick);
                    break;
                case OfficeShiftPhase.Closing:
                    Transition(OfficeShiftPhase.Result, currentTick);
                    break;
            }
        }

        private void Transition(OfficeShiftPhase next, long currentTick)
        {
            Phase = next;
            PhaseStartedTick = currentTick;
        }

        private void Fail(string reason)
        {
            Failed = true;
            FailureReason = reason ?? string.Empty;
        }
    }
}
