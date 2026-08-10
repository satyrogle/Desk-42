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

        public OfficeCausalEventLog()
        {
            _readOnlyEvents = _events.AsReadOnly();
        }

        public IReadOnlyList<OfficeCausalEvent> Events => _readOnlyEvents;

        public void Capture(
            long currentTick,
            OfficeAutomationRuleState rule,
            OfficeBreakState breakState,
            OfficeQueueService queues)
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
            OfficeBreakState breakState)
        {
            if (Failed || Phase == OfficeShiftPhase.Result) return;
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
            builder.Append("|shift=").Append(Phase).Append(':')
                .Append(PhaseStartedTick).Append(':').Append(Failed).Append(':')
                .Append(FailureReason).Append(':').Append(RestartRequested);
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
