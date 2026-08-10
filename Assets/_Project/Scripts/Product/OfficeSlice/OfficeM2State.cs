using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Desk42.Institutional.Player;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeCustomerQueueState
    {
        NotArrived,
        Waiting,
        AtDesk,
        Complete,
    }

    public enum OfficeCustomerDeskState
    {
        None,
        Present,
        PapersChecked,
        MoneyTraced,
        DecisionMade,
    }

    public enum OfficeVisibleMoodState
    {
        Calm,
        Worried,
        Upset,
        Strange,
        Break,
    }

    public sealed class OfficeCustomerState
    {
        internal OfficeCustomerState(OfficeCustomerDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            QueueState = OfficeCustomerQueueState.NotArrived;
            DeskState = OfficeCustomerDeskState.None;
            VisibleMoodState = OfficeVisibleMoodState.Calm;
        }

        public OfficeCustomerDefinition Definition { get; }
        public string CustomerId => Definition.CustomerId;
        public string DisplayName => Definition.DisplayName;
        public string LinkedAutomationClaimId => Definition.LinkedAutomationClaimId;
        public long ArrivalTick => Definition.ArrivalTick;
        public string Problem => Definition.Problem;
        public string AuthoredOfficeTraitId => Definition.AuthoredOfficeTraitId;
        public OfficeCustomerQueueState QueueState { get; internal set; }
        public OfficeCustomerDeskState DeskState { get; internal set; }
        public OfficeVisibleMoodState VisibleMoodState { get; internal set; }
    }

    public sealed class OfficeCustomerScheduleState
    {
        private readonly List<OfficeCustomerState> _customers = new();
        private readonly Dictionary<string, OfficeCustomerState> _byId =
            new(StringComparer.Ordinal);

        public OfficeCustomerScheduleState(
            IReadOnlyList<OfficeCustomerDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            long priorArrival = -1L;
            for (int i = 0; i < definitions.Count; i++)
            {
                OfficeCustomerDefinition definition = definitions[i] ??
                    throw new ArgumentException("Null customer definition.", nameof(definitions));
                if (definition.ArrivalTick < priorArrival)
                    throw new InvalidOperationException(
                        "Customer arrivals must use deterministic authored order.");
                var customer = new OfficeCustomerState(definition);
                _customers.Add(customer);
                _byId.Add(customer.CustomerId, customer);
                priorArrival = definition.ArrivalTick;
            }
            AdvanceToTick(0L);
        }

        public IReadOnlyList<OfficeCustomerState> Customers =>
            new ReadOnlyCollection<OfficeCustomerState>(_customers);
        public OfficeCustomerState ActiveDeskCustomer { get; private set; }

        public void AdvanceToTick(long tick)
        {
            if (tick < 0L) throw new ArgumentOutOfRangeException(nameof(tick));
            for (int i = 0; i < _customers.Count; i++)
            {
                OfficeCustomerState customer = _customers[i];
                if (customer.QueueState == OfficeCustomerQueueState.NotArrived &&
                    tick >= customer.ArrivalTick)
                    customer.QueueState = OfficeCustomerQueueState.Waiting;
            }
            PromoteNextWaitingCustomer();
        }

        public OfficeCustomerState CustomerForClaim(string automationClaimId)
        {
            for (int i = 0; i < _customers.Count; i++)
                if (string.Equals(_customers[i].LinkedAutomationClaimId,
                        automationClaimId, StringComparison.Ordinal))
                    return _customers[i];
            return null;
        }

        public bool MarkPapersChecked(string automationClaimId)
        {
            OfficeCustomerState customer = CustomerForClaim(automationClaimId);
            if (customer == null || customer != ActiveDeskCustomer) return false;
            if (customer.DeskState < OfficeCustomerDeskState.PapersChecked)
                customer.DeskState = OfficeCustomerDeskState.PapersChecked;
            return true;
        }

        public bool MarkMoneyTraced(string automationClaimId)
        {
            OfficeCustomerState customer = CustomerForClaim(automationClaimId);
            if (customer == null || customer != ActiveDeskCustomer ||
                customer.DeskState < OfficeCustomerDeskState.PapersChecked) return false;
            customer.DeskState = OfficeCustomerDeskState.MoneyTraced;
            return true;
        }

        public bool MarkDecisionMade(string automationClaimId)
        {
            OfficeCustomerState customer = CustomerForClaim(automationClaimId);
            if (customer == null || customer != ActiveDeskCustomer ||
                customer.DeskState < OfficeCustomerDeskState.MoneyTraced) return false;
            customer.DeskState = OfficeCustomerDeskState.DecisionMade;
            customer.QueueState = OfficeCustomerQueueState.Complete;
            ActiveDeskCustomer = null;
            PromoteNextWaitingCustomer();
            return true;
        }

        public bool HasAtMostOneActiveDeskCustomer()
        {
            int count = 0;
            for (int i = 0; i < _customers.Count; i++)
                if (_customers[i].QueueState == OfficeCustomerQueueState.AtDesk) count++;
            return count <= 1 && (count == 0) == (ActiveDeskCustomer == null);
        }

        public void AppendSnapshot(StringBuilder builder)
        {
            for (int i = 0; i < _customers.Count; i++)
            {
                OfficeCustomerState customer = _customers[i];
                builder.Append("|customer=").Append(customer.CustomerId).Append(':')
                    .Append(customer.LinkedAutomationClaimId).Append(':')
                    .Append(customer.ArrivalTick).Append(':').Append(customer.QueueState)
                    .Append(':').Append(customer.DeskState).Append(':')
                    .Append(customer.VisibleMoodState);
            }
            builder.Append("|desk=").Append(ActiveDeskCustomer?.CustomerId ?? string.Empty);
        }

        private void PromoteNextWaitingCustomer()
        {
            if (ActiveDeskCustomer != null) return;
            for (int i = 0; i < _customers.Count; i++)
            {
                OfficeCustomerState customer = _customers[i];
                if (customer.QueueState != OfficeCustomerQueueState.Waiting) continue;
                customer.QueueState = OfficeCustomerQueueState.AtDesk;
                customer.DeskState = OfficeCustomerDeskState.Present;
                ActiveDeskCustomer = customer;
                return;
            }
        }
    }

    public enum OfficeManualTaskKind
    {
        Compare,
        Trace,
    }

    public sealed class OfficeCaseWorkRecord
    {
        internal OfficeCaseWorkRecord(string automationClaimId)
        {
            AutomationClaimId = automationClaimId;
        }

        public string AutomationClaimId { get; }
        public bool CompareComplete { get; internal set; }
        public bool CompareCorrect { get; internal set; }
        public int CompareAttempts { get; internal set; }
        public int ComparedEntry { get; internal set; } = -1;
        public string CompareReason { get; internal set; } = string.Empty;
        public bool TraceComplete { get; internal set; }
        public bool TraceCorrect { get; internal set; }
        public int TraceAttempts { get; internal set; }
        public int SelectedMoneyPath { get; internal set; } = -1;
        public string TraceResult { get; internal set; } = string.Empty;
        public string TracePathSummary { get; internal set; } = string.Empty;
    }

    public sealed class OfficeManualTaskState
    {
        private readonly OfficeM2Scenario _scenario;
        private readonly Dictionary<string, OfficeCaseWorkRecord> _records =
            new(StringComparer.Ordinal);

        public OfficeManualTaskState(OfficeM2Scenario scenario)
        {
            _scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            for (int i = 0; i < scenario.Cases.Cases.Count; i++)
            {
                string claimId = scenario.Cases.Cases[i].AutomationClaimId;
                _records.Add(claimId, new OfficeCaseWorkRecord(claimId));
            }
        }

        public bool IsActive { get; private set; }
        public OfficeManualTaskKind ActiveKind { get; private set; }
        public string ActiveCaseId { get; private set; } = string.Empty;
        public long StartedTick { get; private set; }

        public OfficeCaseWorkRecord RecordFor(string automationClaimId)
        {
            if (string.IsNullOrWhiteSpace(automationClaimId)) return null;
            _records.TryGetValue(automationClaimId, out OfficeCaseWorkRecord value);
            return value;
        }

        public bool TryStart(
            OfficeManualTaskKind kind,
            string automationClaimId,
            long currentTick,
            out string failure)
        {
            failure = string.Empty;
            if (IsActive)
            {
                failure = "WORK_ALREADY_ACTIVE";
                return false;
            }
            OfficeCaseWorkRecord record = RecordFor(automationClaimId);
            if (record == null)
            {
                failure = "UNKNOWN_FOLDER";
                return false;
            }
            if (kind == OfficeManualTaskKind.Compare && record.CompareComplete)
            {
                failure = "PAPERS_ALREADY_CHECKED";
                return false;
            }
            if (kind == OfficeManualTaskKind.Trace && !record.CompareComplete)
            {
                failure = "CHECK_PAPERS_FIRST";
                return false;
            }
            if (kind == OfficeManualTaskKind.Trace && record.TraceComplete)
            {
                failure = "MONEY_ALREADY_TRACED";
                return false;
            }
            IsActive = true;
            ActiveKind = kind;
            ActiveCaseId = automationClaimId;
            StartedTick = currentTick;
            return true;
        }

        public bool TrySubmit(int choice, out bool completed, out string result)
        {
            completed = false;
            result = string.Empty;
            if (!IsActive)
            {
                result = "NO_WORK_ACTIVE";
                return false;
            }
            OfficeCaseWorkDefinition definition = _scenario.WorkFor(ActiveCaseId);
            OfficeCaseWorkRecord record = RecordFor(ActiveCaseId);
            if (ActiveKind == OfficeManualTaskKind.Compare)
            {
                record.CompareAttempts++;
                record.ComparedEntry = choice;
                int correct = (int)definition.PaperAnswer;
                record.CompareCorrect = choice == correct;
                record.CompareReason = record.CompareCorrect
                    ? definition.PaperResult
                    : "CHECK THAT LINE AGAIN";
                result = record.CompareReason;
                if (!record.CompareCorrect) return true;
                record.CompareComplete = true;
            }
            else
            {
                record.TraceAttempts++;
                record.SelectedMoneyPath = choice;
                record.TraceCorrect = choice == definition.MoneyPathAnswer;
                record.TraceResult = record.TraceCorrect
                    ? definition.MoneyResultLabel
                    : "MONEY ROUTE NOT CONFIRMED";
                record.TracePathSummary = record.TraceCorrect
                    ? definition.MoneyPathSummary
                    : string.Empty;
                result = record.TraceResult;
                if (!record.TraceCorrect) return true;
                record.TraceComplete = true;
            }
            completed = true;
            Cancel();
            return true;
        }

        public void Cancel()
        {
            IsActive = false;
            ActiveKind = OfficeManualTaskKind.Compare;
            ActiveCaseId = string.Empty;
            StartedTick = 0L;
        }

        public void AppendSnapshot(StringBuilder builder, IReadOnlyList<OfficeCase> cases)
        {
            builder.Append("|work-active=").Append(IsActive).Append(':')
                .Append(ActiveKind).Append(':').Append(ActiveCaseId).Append(':')
                .Append(StartedTick);
            for (int i = 0; i < cases.Count; i++)
            {
                OfficeCaseWorkRecord record = RecordFor(cases[i].AutomationClaimId);
                builder.Append("|work=").Append(record.AutomationClaimId).Append(':')
                    .Append(record.CompareComplete).Append(':')
                    .Append(record.CompareCorrect).Append(':')
                    .Append(record.CompareAttempts).Append(':')
                    .Append(record.ComparedEntry).Append(':')
                    .Append(record.CompareReason).Append(':')
                    .Append(record.TraceComplete).Append(':')
                    .Append(record.TraceCorrect).Append(':')
                    .Append(record.TraceAttempts).Append(':')
                    .Append(record.SelectedMoneyPath).Append(':')
                    .Append(record.TraceResult).Append(':')
                    .Append(record.TracePathSummary);
            }
        }
    }

    public enum OfficeDecisionChoice
    {
        RejectCase,
        HelpCustomer,
    }

    public sealed class OfficeDecisionRecord
    {
        internal OfficeDecisionRecord(
            string automationClaimId,
            OfficeDecisionChoice choice,
            AutomationRulingResult result)
        {
            AutomationClaimId = automationClaimId;
            Choice = choice;
            RulingId = result.RulingId;
            Disposition = result.Disposition;
            Scope = result.Scope;
            TemporalReach = result.TemporalReach;
            Remedies = Copy(result.Remedies);
            DirectChanges = Copy(result.DirectInstitutionalChanges);
            Stamp = choice == OfficeDecisionChoice.HelpCustomer
                ? "HELP CUSTOMER"
                : "REJECT CASE";
        }

        public string AutomationClaimId { get; }
        public OfficeDecisionChoice Choice { get; }
        public string RulingId { get; }
        public string Disposition { get; }
        public string Scope { get; }
        public string TemporalReach { get; }
        public IReadOnlyList<string> Remedies { get; }
        public IReadOnlyList<string> DirectChanges { get; }
        public string Stamp { get; }

        private static IReadOnlyList<string> Copy(IReadOnlyList<string> source)
        {
            var values = new List<string>();
            if (source != null)
                for (int i = 0; i < source.Count; i++) values.Add(source[i]);
            return new ReadOnlyCollection<string>(values);
        }
    }

    public sealed class OfficeDecisionState
    {
        private static readonly IReadOnlyList<AutomationInstitutionalProcedure>
            NoProcedures = Array.Empty<AutomationInstitutionalProcedure>();
        private readonly InstitutionalAutomationSession _session;
        private readonly Dictionary<string, OfficeDecisionRecord> _records =
            new(StringComparer.Ordinal);

        public OfficeDecisionState(InstitutionalAutomationSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public PlayerScopeChoice DefaultScope => PlayerScopeChoice.Narrow;
        public IReadOnlyList<AutomationInstitutionalProcedure> DefaultProcedures =>
            NoProcedures;
        public int CommitCount => _records.Count;
        public OfficeDecisionRecord LastRecord { get; private set; }

        public OfficeDecisionRecord RecordFor(string automationClaimId)
        {
            if (string.IsNullOrWhiteSpace(automationClaimId)) return null;
            _records.TryGetValue(automationClaimId, out OfficeDecisionRecord value);
            return value;
        }

        public bool TryCommit(
            string automationClaimId,
            OfficeDecisionChoice choice,
            out OfficeDecisionRecord record,
            out string failure)
        {
            record = RecordFor(automationClaimId);
            if (record != null)
            {
                failure = "DECISION_ALREADY_MADE";
                return false;
            }
            PlayerRulingDisposition disposition = choice == OfficeDecisionChoice.HelpCustomer
                ? PlayerRulingDisposition.Recognised
                : PlayerRulingDisposition.Denied;
            AutomationRulingResult result;
            try
            {
                result = _session.Commit(
                    automationClaimId,
                    PlayerScopeChoice.Narrow,
                    disposition,
                    NoProcedures,
                    humanPrecedentReviewCompleted: false);
            }
            catch (Exception exception)
            {
                failure = "DECISION_REJECTED: " + exception.Message;
                return false;
            }
            record = new OfficeDecisionRecord(automationClaimId, choice, result);
            _records.Add(automationClaimId, record);
            LastRecord = record;
            failure = string.Empty;
            return true;
        }

        public void AppendSnapshot(StringBuilder builder, IReadOnlyList<OfficeCase> cases)
        {
            builder.Append("|decisions=").Append(CommitCount.ToString(
                CultureInfo.InvariantCulture)).Append(":last=")
                .Append(LastRecord?.AutomationClaimId ?? string.Empty);
            for (int i = 0; i < cases.Count; i++)
            {
                OfficeDecisionRecord record = RecordFor(cases[i].AutomationClaimId);
                if (record == null) continue;
                builder.Append("|decision=").Append(record.AutomationClaimId).Append(':')
                    .Append(record.Choice).Append(':').Append(record.RulingId).Append(':')
                    .Append(record.Disposition).Append(':').Append(record.Scope).Append(':')
                    .Append(record.TemporalReach).Append(':').Append(record.Stamp);
                for (int remedy = 0; remedy < record.Remedies.Count; remedy++)
                    builder.Append(":remedy=").Append(record.Remedies[remedy]);
                for (int change = 0; change < record.DirectChanges.Count; change++)
                    builder.Append(":change=").Append(record.DirectChanges[change]);
            }
        }
    }

    public sealed class OfficeCarryState
    {
        private readonly OfficeQueueService _queues;

        public OfficeCarryState(OfficeQueueService queues)
        {
            _queues = queues ?? throw new ArgumentNullException(nameof(queues));
        }

        public string CarriedFolderId => _queues.WardenCarriedFolderId;
        public bool IsCarrying => !string.IsNullOrWhiteSpace(CarriedFolderId);

        public bool TryTake(string caseId, OfficeRoomId room)
        {
            return _queues.TryTakeByWarden(caseId, room);
        }

        public bool TryDrop(OfficeRoomId room)
        {
            return _queues.TryDropByWarden(room);
        }

        public bool TrySend(OfficeRoomId destination, long currentTick)
        {
            return _queues.TrySendByWarden(destination, currentTick);
        }
    }
}
