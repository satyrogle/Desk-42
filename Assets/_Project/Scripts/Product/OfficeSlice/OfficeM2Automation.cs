using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Desk42.Product.OfficeSlice
{
    public sealed class OfficeAutomationRuleMatch
    {
        internal OfficeAutomationRuleMatch(
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
            Reason = reason;
            Action = action;
        }

        public long Tick { get; }
        public string FolderId { get; }
        public string SourceCaseId { get; }
        public bool Matched { get; }
        public string Reason { get; }
        public string Action { get; }
    }

    public sealed class OfficeAutomationRuleState
    {
        public const int TransferDurationTicks = 5;
        public const string PlayerRule =
            "IF PAPERS MATCH AND REFUND PATH CLEAR, SEND MONEY";

        private readonly OfficeM2Scenario _scenario;
        private readonly OfficeQueueService _queues;
        private readonly OfficeManualTaskState _manualTasks;
        private readonly HashSet<string> _evaluatedFolderIds =
            new(StringComparer.Ordinal);
        private readonly List<OfficeAutomationRuleMatch> _matches = new();
        private readonly IReadOnlyList<OfficeAutomationRuleMatch> _readOnlyMatches;

        public OfficeAutomationRuleState(
            OfficeM2Scenario scenario,
            OfficeQueueService queues,
            OfficeManualTaskState manualTasks,
            bool unlocked = false,
            bool enabled = false)
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
        public IReadOnlyList<OfficeAutomationRuleMatch> Matches => _readOnlyMatches;
        public string LastAcceptedCopyId { get; private set; } = string.Empty;

        public bool TryToggle()
        {
            if (!Unlocked) return false;
            Enabled = !Enabled;
            return true;
        }

        public void RefreshUnlock()
        {
            if (Unlocked) return;
            for (int i = 0; i < _scenario.Cases.Cases.Count; i++)
            {
                OfficeCaseWorkRecord record = _manualTasks.RecordFor(
                    _scenario.Cases.Cases[i].AutomationClaimId);
                if (record.CompareComplete && record.TraceComplete)
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
                bool matched = work != null && work.RefundFile &&
                    work.PublicPapersMatch && work.PublicRefundPathClear;
                string reason = matched
                    ? "PAPERS MATCH / REFUND PATH CLEAR"
                    : work == null || !work.RefundFile
                        ? "NOT A REFUND FILE"
                        : !work.PublicPapersMatch
                            ? "PAPERS DO NOT MATCH"
                            : "REFUND PATH NOT CLEAR";
                string action = matched ? "SENT TO MONEY" : "LEFT FOR CHECK";
                _matches.Add(new OfficeAutomationRuleMatch(
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
                if (folder.IsCopy) LastAcceptedCopyId = folder.CaseId;
            }
        }

        public bool Accepted(string folderId)
        {
            for (int i = 0; i < _matches.Count; i++)
                if (_matches[i].Matched && string.Equals(
                        _matches[i].FolderId, folderId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        public void AppendSnapshot(StringBuilder builder)
        {
            builder.Append("|auto-sorter=").Append(Unlocked).Append(':')
                .Append(Enabled).Append(':').Append(LastAcceptedCopyId);
            for (int i = 0; i < _matches.Count; i++)
            {
                OfficeAutomationRuleMatch match = _matches[i];
                builder.Append("|rule-match=").Append(match.Tick).Append(':')
                    .Append(match.FolderId).Append(':').Append(match.SourceCaseId)
                    .Append(':').Append(match.Matched).Append(':')
                    .Append(match.Reason).Append(':').Append(match.Action);
            }
        }
    }

    public sealed class OfficeBreakState
    {
        public const int CopyIntervalTicks = 30;
        public const int MaximumActiveCopies = 6;

        private readonly OfficeM2Scenario _scenario;
        private readonly OfficeQueueService _queues;
        private readonly List<string> _copyIds = new();
        private readonly IReadOnlyList<string> _readOnlyCopyIds;
        private readonly string _copyEchoCaseId;
        private readonly string _copyEchoCustomerId;
        private string _candidateCopyId = string.Empty;
        private int _nextCopyOrdinal = 1;
        private long _lastCopyTick;

        public OfficeBreakState(OfficeM2Scenario scenario, OfficeQueueService queues)
        {
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            _queues = queues ?? throw new ArgumentNullException(nameof(queues));
            _readOnlyCopyIds = _copyIds.AsReadOnly();
            for (int i = 0; i < scenario.Customers.Count; i++)
            {
                OfficeCustomerDefinition customer = scenario.Customers[i];
                if (!string.Equals(customer.AuthoredOfficeTraitId,
                        "trait.watches-copier", StringComparison.Ordinal)) continue;
                _copyEchoCaseId = customer.LinkedAutomationClaimId;
                _copyEchoCustomerId = customer.CustomerId;
                break;
            }
            if (string.IsNullOrWhiteSpace(_copyEchoCaseId))
                throw new InvalidOperationException("The Copy Echo customer is required.");
        }

        public bool CopierActive { get; private set; } = true;
        public bool Active { get; private set; }
        public bool Recovered { get; private set; }
        public long TriggeredTick { get; private set; }
        public bool OriginalMarkedHard { get; private set; }
        public bool OriginalFound { get; private set; }
        public int ClearedCopyCount { get; private set; }
        public string OriginalFolderId => _copyEchoCaseId;
        public string CopyEchoCustomerId => _copyEchoCustomerId;
        public IReadOnlyList<string> CopyIds => _readOnlyCopyIds;

        public static bool ExactAuthoredConjunction(
            OfficeVisibleMoodState mood,
            bool copierActive,
            bool autoSorterAcceptedCopiedRefund)
        {
            return mood == OfficeVisibleMoodState.Upset &&
                copierActive && autoSorterAcceptedCopiedRefund;
        }

        public void PrepareCopyCandidate(
            long currentTick,
            OfficeCustomerScheduleState customers,
            OfficeAutomationRuleState rule)
        {
            if (Active || !string.IsNullOrWhiteSpace(_candidateCopyId) ||
                !CopierActive || !rule.Enabled) return;
            OfficeCustomerState customer = customers.CustomerForClaim(_copyEchoCaseId);
            if (customer == null || customer.QueueState != OfficeCustomerQueueState.AtDesk ||
                customer.VisibleMoodState != OfficeVisibleMoodState.Upset) return;
            _candidateCopyId = CreateCopy(currentTick);
        }

        public void AdvanceAfterRule(
            long currentTick,
            OfficeCustomerScheduleState customers,
            OfficeAutomationRuleState rule)
        {
            OfficeCustomerState customer = customers.CustomerForClaim(_copyEchoCaseId);
            bool accepted = !string.IsNullOrWhiteSpace(_candidateCopyId) &&
                rule.Accepted(_candidateCopyId);
            if (!Active && customer != null && ExactAuthoredConjunction(
                    customer.VisibleMoodState, CopierActive, accepted))
            {
                Active = true;
                TriggeredTick = currentTick;
                OriginalMarkedHard = true;
                _lastCopyTick = currentTick;
            }
            if (Active && CopierActive &&
                currentTick - _lastCopyTick >= CopyIntervalTicks &&
                _queues.ActiveCopyCount < MaximumActiveCopies)
            {
                CreateCopy(currentTick);
                _lastCopyTick = currentTick;
            }
            ObserveOriginal();
            EvaluateRecovery(customers);
        }

        public bool TryFixAt(OfficeRoomId room, out string result)
        {
            result = string.Empty;
            if (!Active || Recovered) return false;
            if (room == OfficeRoomId.WeirdRoom && CopierActive)
            {
                CopierActive = false;
                result = "MACHINE STOPPED";
                return true;
            }
            string copyId = _queues.FirstActiveCopyAt(room);
            if (string.IsNullOrWhiteSpace(copyId) || !_queues.TryClearCopy(copyId))
                return false;
            ClearedCopyCount++;
            result = "COPY CLEARED";
            return true;
        }

        public void ObserveOriginal()
        {
            if (!Active) return;
            OfficeFolderState original = _queues.GetFolder(_copyEchoCaseId);
            if (original != null && original.OwnerKind == OfficeFolderOwnerKind.Warden)
                OriginalFound = true;
        }

        public void EvaluateRecovery(OfficeCustomerScheduleState customers)
        {
            if (!Active || Recovered) return;
            OfficeCustomerState customer = customers.CustomerForClaim(_copyEchoCaseId);
            OfficeFolderState original = _queues.GetFolder(_copyEchoCaseId);
            bool customerCalm = customer != null &&
                customer.VisibleMoodState <= OfficeVisibleMoodState.Worried;
            bool originalAtFront = original != null && !original.IsMoving &&
                original.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                original.CurrentRoom == OfficeRoomId.FrontDesk;
            if (!CopierActive && _queues.ActiveCopyCount == 0 && customerCalm &&
                OriginalFound && originalAtFront)
                Recovered = true;
        }

        public void AppendSnapshot(StringBuilder builder)
        {
            builder.Append("|break=").Append(CopierActive).Append(':')
                .Append(Active).Append(':').Append(Recovered).Append(':')
                .Append(TriggeredTick).Append(':').Append(OriginalMarkedHard)
                .Append(':').Append(OriginalFound).Append(':')
                .Append(ClearedCopyCount).Append(':').Append(_candidateCopyId)
                .Append(':').Append(_nextCopyOrdinal).Append(':')
                .Append(_lastCopyTick);
            for (int i = 0; i < _copyIds.Count; i++)
                builder.Append("|copy-lineage=").Append(_copyIds[i]).Append('>')
                    .Append(_copyEchoCaseId);
        }

        private string CreateCopy(long currentTick)
        {
            string copyId = "copy.echo." + (_nextCopyOrdinal++).ToString("D3");
            if (!_queues.TryCreateCopy(
                    copyId, _copyEchoCaseId, OfficeRoomId.WeirdRoom))
                return string.Empty;
            _copyIds.Add(copyId);
            _lastCopyTick = currentTick;
            return copyId;
        }
    }
}
