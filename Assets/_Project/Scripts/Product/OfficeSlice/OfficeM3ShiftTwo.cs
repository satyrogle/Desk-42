using System;
using System.Collections.Generic;
using System.Text;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficePayrollRuleMatch
    {
        internal OfficePayrollRuleMatch(
            long tick,
            string folderId,
            string sourceCaseId,
            bool matched,
            string reason,
            string action)
        {
            Tick = tick;
            FolderId = folderId;
            SourceCaseId = sourceCaseId;
            Matched = matched;
            Reason = reason ?? string.Empty;
            Action = action ?? string.Empty;
        }

        public long Tick { get; }
        public string FolderId { get; }
        public string SourceCaseId { get; }
        public bool Matched { get; }
        public string Reason { get; }
        public string Action { get; }
    }

    public sealed class OfficePayrollRuleState
    {
        public const int TransferDurationTicks = 5;
        public const string PlayerRule =
            "IF BADGE ACTIVE AND SHIFT LOG MATCHES, SEND MONEY";

        private readonly OfficeM2Scenario _scenario;
        private readonly OfficeQueueService _queues;
        private readonly OfficeManualTaskState _manualTasks;
        private readonly HashSet<string> _evaluatedFolderIds =
            new(StringComparer.Ordinal);
        private readonly List<OfficePayrollRuleMatch> _matches = new();
        private readonly IReadOnlyList<OfficePayrollRuleMatch> _readOnlyMatches;

        public OfficePayrollRuleState(
            OfficeM2Scenario scenario,
            OfficeQueueService queues,
            OfficeManualTaskState manualTasks,
            bool unlocked,
            bool enabled)
        {
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            _queues = queues ?? throw new ArgumentNullException(nameof(queues));
            _manualTasks = manualTasks ?? throw new ArgumentNullException(nameof(manualTasks));
            Unlocked = unlocked;
            Enabled = unlocked && enabled;
            _readOnlyMatches = _matches.AsReadOnly();
        }

        public bool Unlocked { get; private set; }
        public bool Enabled { get; private set; }
        public IReadOnlyList<OfficePayrollRuleMatch> Matches => _readOnlyMatches;
        public string LastAcceptedCopiedPayrollId { get; private set; } = string.Empty;

        public bool TryToggle()
        {
            if (!Unlocked) return false;
            Enabled = !Enabled;
            return true;
        }

        public void RefreshUnlock()
        {
            if (Unlocked || _scenario.ShiftOrdinal < 2) return;
            for (int i = 0; i < _scenario.Cases.Cases.Count; i++)
            {
                string caseId = _scenario.Cases.Cases[i].AutomationClaimId;
                OfficeCaseWorkDefinition work = _scenario.WorkFor(caseId);
                if (work != null && work.PublicBadgeActive &&
                    work.PublicShiftLogMatches &&
                    _manualTasks.IsCaseComplete(caseId))
                {
                    Unlocked = true;
                    return;
                }
            }
        }

        public void AdvanceOneTick(long currentTick)
        {
            RefreshUnlock();
            if (!Enabled) return;
            IReadOnlyList<string> folderIds = _queues.FolderIds;
            for (int i = 0; i < folderIds.Count; i++)
            {
                OfficeFolderState folder = _queues.GetFolder(folderIds[i]);
                if (folder == null || folder.IsMoving ||
                    folder.OwnerKind != OfficeFolderOwnerKind.RoomQueue ||
                    folder.CurrentRoom != OfficeRoomId.WeirdRoom ||
                    !_evaluatedFolderIds.Add(folder.CaseId)) continue;
                OfficeCaseWorkDefinition work = _scenario.WorkFor(folder.SourceCaseId);
                bool matched = work != null && work.PublicBadgeActive &&
                    work.PublicShiftLogMatches;
                string reason = matched
                    ? "BADGE ACTIVE / SHIFT LOG MATCHES"
                    : work == null || !work.PublicBadgeActive
                        ? "BADGE NOT ACTIVE"
                        : "SHIFT LOG DOES NOT MATCH";
                string action = matched ? "SENT TO MONEY" : "LEFT FOR CHECK";
                _matches.Add(new OfficePayrollRuleMatch(
                    currentTick,
                    folder.CaseId,
                    folder.SourceCaseId,
                    matched,
                    reason,
                    action));
                if (!matched) continue;
                _queues.TryTransferCase(
                    folder.CaseId,
                    OfficeRoomId.MoneyRoom,
                    currentTick,
                    TransferDurationTicks);
                if (folder.IsCopy)
                    LastAcceptedCopiedPayrollId = folder.CaseId;
            }
        }

        public bool Accepted(string folderId)
        {
            for (int i = 0; i < _matches.Count; i++)
                if (_matches[i].Matched && string.Equals(
                        _matches[i].FolderId, folderId,
                        StringComparison.Ordinal)) return true;
            return false;
        }

        public void AppendSnapshot(StringBuilder builder)
        {
            builder.Append("|pay-rule=").Append(Unlocked).Append(':')
                .Append(Enabled).Append(':').Append(LastAcceptedCopiedPayrollId);
            for (int i = 0; i < _matches.Count; i++)
            {
                OfficePayrollRuleMatch match = _matches[i];
                builder.Append("|pay-rule-match=").Append(match.Tick).Append(':')
                    .Append(match.FolderId).Append(':').Append(match.SourceCaseId)
                    .Append(':').Append(match.Matched).Append(':')
                    .Append(match.Reason).Append(':').Append(match.Action);
            }
        }
    }

    public sealed class OfficeGhostClockState
    {
        public const int SlipIntervalTicks = 45;
        public const int MaximumActiveSlips = 3;

        private readonly OfficeM2Scenario _scenario;
        private readonly OfficeQueueService _queues;
        private readonly List<string> _slipIds = new();
        private readonly IReadOnlyList<string> _readOnlySlipIds;
        private readonly string _tomasCaseId;
        private readonly string _tomasCustomerId;
        private int _nextSlipOrdinal = 1;
        private long _lastSlipTick;

        public OfficeGhostClockState(
            OfficeM2Scenario scenario,
            OfficeQueueService queues)
        {
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            _queues = queues ?? throw new ArgumentNullException(nameof(queues));
            _readOnlySlipIds = _slipIds.AsReadOnly();
            for (int i = 0; i < scenario.Customers.Count; i++)
            {
                OfficeCustomerDefinition customer = scenario.Customers[i];
                if (!string.Equals(customer.DisplayName, "TOMAS REED",
                        StringComparison.Ordinal)) continue;
                _tomasCaseId = customer.LinkedAutomationClaimId;
                _tomasCustomerId = customer.CustomerId;
                break;
            }
        }

        public bool ClockTerminalActive { get; private set; } = true;
        public bool HasTriggered { get; private set; }
        public bool Active { get; private set; }
        public bool Recovered { get; private set; }
        public long TriggeredTick { get; private set; }
        public int ClearedSlipCount { get; private set; }
        public IReadOnlyList<string> SlipIds => _readOnlySlipIds;
        public string TomasCaseId => _tomasCaseId;
        public string TomasCustomerId => _tomasCustomerId;
        public int ActiveSlipCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slipIds.Count; i++)
                {
                    OfficeFolderState slip = _queues.GetFolder(_slipIds[i]);
                    if (slip != null &&
                        slip.OwnerKind != OfficeFolderOwnerKind.Cleared) count++;
                }
                return count;
            }
        }

        public static bool ExactAuthoredConjunction(
            OfficeVisibleMoodState mood,
            bool clockTerminalActive,
            bool caseReachedPaperOrMoney)
        {
            return mood == OfficeVisibleMoodState.Upset &&
                clockTerminalActive && caseReachedPaperOrMoney;
        }

        public void AdvanceOneTick(
            long currentTick,
            OfficeCustomerScheduleState customers,
            OfficeManualTaskState manualTasks)
        {
            if (_scenario.ShiftOrdinal != 2) return;
            OfficeCustomerState tomas = customers.CustomerForClaim(_tomasCaseId);
            OfficeFolderState folder = _queues.GetFolder(_tomasCaseId);
            OfficeCaseWorkRecord record = manualTasks.RecordFor(_tomasCaseId);
            bool reachedPaperOrMoney = record != null &&
                (record.CompareAttempts > 0 || record.TraceAttempts > 0);
            reachedPaperOrMoney |= folder != null &&
                (folder.CurrentRoom == OfficeRoomId.PaperRoom ||
                 folder.CurrentRoom == OfficeRoomId.MoneyRoom ||
                 (folder.IsMoving &&
                  (folder.DestinationRoom == OfficeRoomId.PaperRoom ||
                   folder.DestinationRoom == OfficeRoomId.MoneyRoom)));
            if (!HasTriggered && tomas != null && ExactAuthoredConjunction(
                    tomas.VisibleMoodState,
                    ClockTerminalActive,
                    reachedPaperOrMoney))
            {
                HasTriggered = true;
                Active = true;
                TriggeredTick = currentTick;
                CreateSlip(currentTick);
            }
            if (Active && ClockTerminalActive &&
                currentTick - _lastSlipTick >= SlipIntervalTicks &&
                ActiveSlipCount < MaximumActiveSlips)
                CreateSlip(currentTick);
            EvaluateRecovery(customers);
        }

        public bool TryFixAt(OfficeRoomId room, out string result)
        {
            result = string.Empty;
            if (!Active || Recovered || room != OfficeRoomId.PaperRoom)
                return false;
            if (ClockTerminalActive)
            {
                ClockTerminalActive = false;
                result = "CLOCK TERMINAL OFF";
                return true;
            }
            for (int i = 0; i < _slipIds.Count; i++)
            {
                OfficeFolderState slip = _queues.GetFolder(_slipIds[i]);
                if (slip == null || slip.IsMoving ||
                    slip.OwnerKind != OfficeFolderOwnerKind.RoomQueue ||
                    slip.CurrentRoom != room ||
                    !_queues.TryClearCopy(slip.CaseId)) continue;
                ClearedSlipCount++;
                result = "TIME SLIP CLEARED";
                return true;
            }
            return false;
        }

        public void EvaluateRecovery(OfficeCustomerScheduleState customers)
        {
            if (!Active || Recovered) return;
            OfficeCustomerState tomas = customers.CustomerForClaim(_tomasCaseId);
            bool calm = tomas != null &&
                tomas.VisibleMoodState <= OfficeVisibleMoodState.Worried;
            if (!ClockTerminalActive && ActiveSlipCount == 0 && calm)
            {
                Active = false;
                Recovered = true;
            }
        }

        public void AppendSnapshot(StringBuilder builder)
        {
            builder.Append("|ghost-clock=").Append(ClockTerminalActive).Append(':')
                .Append(HasTriggered).Append(':').Append(Active).Append(':')
                .Append(Recovered).Append(':').Append(TriggeredTick).Append(':')
                .Append(ClearedSlipCount).Append(':').Append(_nextSlipOrdinal)
                .Append(':').Append(_lastSlipTick);
            for (int i = 0; i < _slipIds.Count; i++)
                builder.Append("|time-slip=").Append(_slipIds[i]).Append('>')
                    .Append(_tomasCaseId);
        }

        private void CreateSlip(long currentTick)
        {
            if (ActiveSlipCount >= MaximumActiveSlips) return;
            string slipId = "time-slip." + (_nextSlipOrdinal++).ToString("D3");
            if (_queues.TryCreateCopy(
                    slipId, _tomasCaseId, OfficeRoomId.PaperRoom))
            {
                _slipIds.Add(slipId);
                _lastSlipTick = currentTick;
            }
        }
    }

    public sealed class OfficeMissingRoomAccessState
    {
        private readonly OfficeM2Scenario _scenario;
        private readonly OfficeQueueService _queues;
        private readonly string _irisCaseId;

        public OfficeMissingRoomAccessState(
            OfficeM2Scenario scenario,
            OfficeQueueService queues)
        {
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            _queues = queues ?? throw new ArgumentNullException(nameof(queues));
            for (int i = 0; i < scenario.Customers.Count; i++)
                if (string.Equals(scenario.Customers[i].DisplayName, "IRIS COLE",
                        StringComparison.Ordinal))
                    _irisCaseId = scenario.Customers[i].LinkedAutomationClaimId;
        }

        public bool HasTriggered { get; private set; }
        public bool Active { get; private set; }
        public bool DoorOpen { get; private set; }
        public bool Recovered { get; private set; }
        public long TriggeredTick { get; private set; }
        public string IrisCaseId => _irisCaseId;

        public static bool ExactAuthoredTrigger(
            bool irisAtDesk,
            bool oldAccessCardAtWeirdRoom,
            bool inactiveDoorClosed)
        {
            return irisAtDesk && oldAccessCardAtWeirdRoom && inactiveDoorClosed;
        }

        public void AdvanceOneTick(
            long currentTick,
            OfficeCustomerScheduleState customers,
            OfficeDecisionState decisions,
            OfficeStaffSystem staff)
        {
            if (_scenario.ShiftOrdinal != 2) return;
            OfficeCustomerState iris = customers.CustomerForClaim(_irisCaseId);
            OfficeFolderState folder = _queues.GetFolder(_irisCaseId);
            bool atWeird = folder != null &&
                ((!folder.IsMoving && folder.CurrentRoom == OfficeRoomId.WeirdRoom) ||
                 (folder.IsMoving && folder.DestinationRoom == OfficeRoomId.WeirdRoom));
            if (!HasTriggered && ExactAuthoredTrigger(
                    iris != null && iris.QueueState == OfficeCustomerQueueState.AtDesk,
                    atWeird,
                    !DoorOpen))
            {
                HasTriggered = true;
                Active = true;
                DoorOpen = true;
                TriggeredTick = currentTick;
                staff.SetRunnerDiversion(true);
            }
            if (Active && decisions.RecordFor(_irisCaseId) != null)
                CloseDoor(staff);
        }

        public bool TryCloseAt(OfficeRoomId room, OfficeStaffSystem staff)
        {
            if (!Active || room != OfficeRoomId.WeirdRoom) return false;
            CloseDoor(staff);
            return true;
        }

        public void AppendSnapshot(StringBuilder builder, OfficeStaffSystem staff)
        {
            builder.Append("|missing-room=").Append(HasTriggered).Append(':')
                .Append(Active).Append(':').Append(DoorOpen).Append(':')
                .Append(Recovered).Append(':').Append(TriggeredTick).Append(':')
                .Append(staff.RunnerDiversionCount);
        }

        private void CloseDoor(OfficeStaffSystem staff)
        {
            DoorOpen = false;
            Active = false;
            Recovered = true;
            staff.SetRunnerDiversion(false);
        }
    }
}
