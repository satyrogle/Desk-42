using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Desk42.Institutional.Player;

namespace Desk42.Product.OfficeSlice
{
    public enum OfficeCampaignPhase
    {
        ActiveShift,
        ChooseUpgrade,
        ReadyForNextShift,
        CampaignResult,
    }

    public enum OfficeUpgradeFamily
    {
        FastTrays = 1,
        CalmChairs = 2,
        RedLabels = 3,
    }

    public sealed class OfficeCampaignUpgradeState
    {
        private readonly List<OfficeUpgradeFamily> _choices = new();
        private readonly IReadOnlyList<OfficeUpgradeFamily> _readOnlyChoices;

        public OfficeCampaignUpgradeState()
        {
            _readOnlyChoices = _choices.AsReadOnly();
        }

        public int FastTraysTier { get; private set; }
        public int CalmChairsTier { get; private set; }
        public int RedLabelsTier { get; private set; }
        public IReadOnlyList<OfficeUpgradeFamily> Choices => _readOnlyChoices;

        public int TransferDurationTicks => FastTraysTier switch
        {
            1 => 12,
            2 => 9,
            _ => OfficeQueueService.DefaultTransferDurationTicks,
        };

        public int MoodThresholdBonusTicks => CalmChairsTier * 90;
        public int CopyClearReductionTicks => RedLabelsTier >= 1 ? 15 : 0;
        public int MaximumCopyReduction => RedLabelsTier >= 2 ? 2 : 0;
        public int OriginalFindReductionTicks => RedLabelsTier >= 2 ? 15 : 0;

        public bool TryChoose(OfficeUpgradeFamily family)
        {
            int tier = Tier(family);
            if (tier >= 2) return false;
            switch (family)
            {
                case OfficeUpgradeFamily.FastTrays:
                    FastTraysTier++;
                    break;
                case OfficeUpgradeFamily.CalmChairs:
                    CalmChairsTier++;
                    break;
                case OfficeUpgradeFamily.RedLabels:
                    RedLabelsTier++;
                    break;
                default:
                    return false;
            }
            _choices.Add(family);
            return true;
        }

        public int Tier(OfficeUpgradeFamily family)
        {
            return family switch
            {
                OfficeUpgradeFamily.FastTrays => FastTraysTier,
                OfficeUpgradeFamily.CalmChairs => CalmChairsTier,
                OfficeUpgradeFamily.RedLabels => RedLabelsTier,
                _ => 0,
            };
        }

        internal OfficeCampaignUpgradeState Clone()
        {
            var clone = new OfficeCampaignUpgradeState();
            for (int i = 0; i < _choices.Count; i++)
                if (!clone.TryChoose(_choices[i]))
                    throw new InvalidOperationException(
                        "Campaign upgrade history is not replayable.");
            return clone;
        }

        public void AppendSnapshot(StringBuilder builder)
        {
            builder.Append("|upgrades=").Append(FastTraysTier).Append(':')
                .Append(CalmChairsTier).Append(':').Append(RedLabelsTier);
            for (int i = 0; i < _choices.Count; i++)
                builder.Append("|upgrade-choice=").Append(i + 1).Append(':')
                    .Append(_choices[i]);
        }
    }

    public sealed class OfficeCampaignCaseBinding
    {
        internal OfficeCampaignCaseBinding(OfficeCustomerDefinition customer)
        {
            CustomerId = customer.CustomerId;
            DisplayName = customer.DisplayName;
            AutomationClaimId = customer.LinkedAutomationClaimId;
            Problem = customer.Problem;
        }

        public string CustomerId { get; }
        public string DisplayName { get; }
        public string AutomationClaimId { get; }
        public string Problem { get; }
    }

    public sealed class OfficeCampaignShiftDefinition
    {
        private readonly List<OfficeCampaignCaseBinding> _caseBindings = new();
        private readonly IReadOnlyList<OfficeCampaignCaseBinding> _readOnlyCaseBindings;

        internal OfficeCampaignShiftDefinition(
            int ordinal,
            string title,
            string headlineCustomer,
            OfficeM2Scenario scenario)
        {
            Ordinal = ordinal;
            Title = title ?? string.Empty;
            HeadlineCustomer = headlineCustomer ?? string.Empty;
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            for (int i = 0; i < scenario.Customers.Count; i++)
                _caseBindings.Add(new OfficeCampaignCaseBinding(scenario.Customers[i]));
            _readOnlyCaseBindings = _caseBindings.AsReadOnly();
        }

        public int Ordinal { get; }
        public string Title { get; }
        public string HeadlineCustomer { get; }
        public OfficeM2Scenario Scenario { get; }
        public IReadOnlyList<OfficeCampaignCaseBinding> CaseBindings =>
            _readOnlyCaseBindings;
    }

    public static class OfficeCampaignScenario
    {
        public const int ShiftCount = 3;

        public static OfficeCampaignShiftDefinition CreateShift(
            InstitutionalAutomationSession session,
            int ordinal,
            IReadOnlyList<OfficeCampaignDecisionCallback> priorDecisions = null)
        {
            OfficeM2Scenario scenario = OfficeM2Scenario.CreateForCampaign(
                session, ordinal, priorDecisions);
            return ordinal switch
            {
                1 => new OfficeCampaignShiftDefinition(
                    1, "THE REFUND THAT ARRIVED YESTERDAY", "NIA BELL", scenario),
                2 => new OfficeCampaignShiftDefinition(
                    2, "THE EMPLOYEE WHO KEPT CLOCKING IN AFTER DEATH",
                    "TOMAS REED", scenario),
                3 => new OfficeCampaignShiftDefinition(
                    3, "THE COPIER PROMOTED ITSELF", "MARA VALE", scenario),
                _ => throw new ArgumentOutOfRangeException(nameof(ordinal)),
            };
        }
    }

    public sealed class OfficeCampaignDecisionCallback
    {
        internal OfficeCampaignDecisionCallback(
            int shiftOrdinal,
            string customerId,
            OfficeDecisionRecord decision)
        {
            ShiftOrdinal = shiftOrdinal;
            CustomerId = customerId ?? string.Empty;
            AutomationClaimId = decision.AutomationClaimId;
            Stamp = decision.Stamp;
            RulingId = decision.RulingId;
        }

        public int ShiftOrdinal { get; }
        public string CustomerId { get; }
        public string AutomationClaimId { get; }
        public string Stamp { get; }
        public string RulingId { get; }
    }

    public sealed class OfficeCampaignAutomationState
    {
        public bool Rule1Taught { get; private set; }
        public bool Rule1Enabled { get; private set; }
        public bool Rule2Taught { get; private set; }
        public bool Rule2Enabled { get; private set; }
        public bool Rule1AcceptedCopiedRefund { get; private set; }
        public bool Rule2AcceptedCopiedPayroll { get; private set; }

        internal void Observe(OfficeSimulationState simulation)
        {
            if (simulation == null) return;
            Rule1Taught |= simulation.AutomationRule != null &&
                simulation.AutomationRule.Unlocked;
            if (simulation.AutomationRule != null)
                Rule1Enabled = simulation.AutomationRule.Enabled;
            Rule2Taught |= simulation.PayrollRule != null &&
                simulation.PayrollRule.Unlocked;
            if (simulation.PayrollRule != null)
                Rule2Enabled = simulation.PayrollRule.Enabled;
            Rule1AcceptedCopiedRefund |= simulation.AutomationRule != null &&
                !string.IsNullOrWhiteSpace(
                    simulation.AutomationRule.LastAcceptedCopyId);
            Rule2AcceptedCopiedPayroll |= simulation.PayrollRule != null &&
                !string.IsNullOrWhiteSpace(
                    simulation.PayrollRule.LastAcceptedCopiedPayrollId);
        }

        internal OfficeCampaignAutomationState Clone()
        {
            return new OfficeCampaignAutomationState
            {
                Rule1Taught = Rule1Taught,
                Rule1Enabled = Rule1Enabled,
                Rule2Taught = Rule2Taught,
                Rule2Enabled = Rule2Enabled,
                Rule1AcceptedCopiedRefund = Rule1AcceptedCopiedRefund,
                Rule2AcceptedCopiedPayroll = Rule2AcceptedCopiedPayroll,
            };
        }

        public void AppendSnapshot(StringBuilder builder)
        {
            builder.Append("|campaign-rules=").Append(Rule1Taught).Append(':')
                .Append(Rule1Enabled).Append(':').Append(Rule2Taught).Append(':')
                .Append(Rule2Enabled).Append(':')
                .Append(Rule1AcceptedCopiedRefund).Append(':')
                .Append(Rule2AcceptedCopiedPayroll);
        }
    }

    /// <summary>
    /// Product-owned restart boundary. It wraps, but does not alter, the public
    /// institutional checkpoint and keeps campaign progression in a separate schema.
    /// </summary>
    public sealed class OfficeCampaignCheckpoint
    {
        public const int CurrentSchemaVersion = 1;

        internal OfficeCampaignCheckpoint(
            int shiftOrdinal,
            InstitutionalAutomationCheckpoint institutional,
            OfficeCampaignUpgradeState upgrades,
            OfficeCampaignAutomationState rules)
        {
            SchemaVersion = CurrentSchemaVersion;
            ShiftOrdinal = shiftOrdinal;
            Institutional = institutional ??
                throw new ArgumentNullException(nameof(institutional));
            Upgrades = upgrades?.Clone() ??
                throw new ArgumentNullException(nameof(upgrades));
            Rules = rules?.Clone() ?? throw new ArgumentNullException(nameof(rules));
        }

        public int SchemaVersion { get; }
        public int ShiftOrdinal { get; }
        public InstitutionalAutomationCheckpoint Institutional { get; }
        public OfficeCampaignUpgradeState Upgrades { get; }
        public OfficeCampaignAutomationState Rules { get; }
    }

    public sealed class OfficeCampaignState
    {
        private readonly List<string> _completedShiftSnapshots = new();
        private readonly IReadOnlyList<string> _readOnlyCompletedShiftSnapshots;
        private readonly List<string> _completedShiftChecksums = new();
        private readonly IReadOnlyList<string> _readOnlyCompletedShiftChecksums;
        private readonly List<OfficeCampaignDecisionCallback> _decisionCallbacks = new();
        private readonly IReadOnlyList<OfficeCampaignDecisionCallback>
            _readOnlyDecisionCallbacks;
        private OfficeCampaignCheckpoint _shiftStartCheckpoint;

        private OfficeCampaignState(
            InstitutionalAutomationSession session,
            OfficeCampaignUpgradeState upgrades,
            OfficeCampaignAutomationState rules,
            int shiftOrdinal)
        {
            _readOnlyCompletedShiftSnapshots = _completedShiftSnapshots.AsReadOnly();
            _readOnlyCompletedShiftChecksums = _completedShiftChecksums.AsReadOnly();
            _readOnlyDecisionCallbacks = _decisionCallbacks.AsReadOnly();
            InstitutionalSession = session ??
                throw new ArgumentNullException(nameof(session));
            Upgrades = upgrades ?? throw new ArgumentNullException(nameof(upgrades));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            CurrentShiftOrdinal = shiftOrdinal;
            StartCurrentShift(captureCheckpoint: true);
        }

        public InstitutionalAutomationSession InstitutionalSession { get; private set; }
        public OfficeCampaignUpgradeState Upgrades { get; private set; }
        public OfficeCampaignAutomationState Rules { get; private set; }
        public int CurrentShiftOrdinal { get; private set; }
        public OfficeCampaignShiftDefinition CurrentShift { get; private set; }
        public OfficeSimulationState CurrentSimulation { get; private set; }
        public OfficeCampaignPhase Phase { get; private set; }
        public IReadOnlyList<string> CompletedShiftSnapshots =>
            _readOnlyCompletedShiftSnapshots;
        public IReadOnlyList<string> CompletedShiftChecksums =>
            _readOnlyCompletedShiftChecksums;
        public IReadOnlyList<OfficeCampaignDecisionCallback> DecisionCallbacks =>
            _readOnlyDecisionCallbacks;
        public OfficeCampaignCheckpoint ShiftStartCheckpoint => _shiftStartCheckpoint;
        public bool IsComplete => Phase == OfficeCampaignPhase.CampaignResult;
        public string OrderedStateSnapshot
        {
            get
            {
                var builder = new StringBuilder(4096);
                builder.Append("campaign-v1|shift=").Append(CurrentShiftOrdinal)
                    .Append("|phase=").Append(Phase)
                    .Append("|society-tick=").Append(
                        InstitutionalSession.SocietyTick.ToString(
                            CultureInfo.InvariantCulture));
                Upgrades.AppendSnapshot(builder);
                Rules.AppendSnapshot(builder);
                for (int i = 0; i < _completedShiftSnapshots.Count; i++)
                    builder.Append("|completed-shift=").Append(i + 1).Append(':')
                        .Append(_completedShiftChecksums[i]).Append(':')
                        .Append(_completedShiftSnapshots[i]);
                for (int i = 0; i < _decisionCallbacks.Count; i++)
                {
                    OfficeCampaignDecisionCallback callback = _decisionCallbacks[i];
                    builder.Append("|callback=").Append(callback.ShiftOrdinal).Append(':')
                        .Append(callback.CustomerId).Append(':')
                        .Append(callback.AutomationClaimId).Append(':')
                        .Append(callback.Stamp).Append(':').Append(callback.RulingId);
                }
                builder.Append("|current=").Append(
                    CurrentSimulation?.OrderedStateSnapshot ?? string.Empty);
                return builder.ToString();
            }
        }
        public string Checksum => ComputeChecksum(OrderedStateSnapshot);

        public static OfficeCampaignState Create()
        {
            return new OfficeCampaignState(
                InstitutionalAutomationSession.Create(6),
                new OfficeCampaignUpgradeState(),
                new OfficeCampaignAutomationState(),
                1);
        }

        internal void ObserveSimulationTick(OfficeSimulationState simulation)
        {
            if (!ReferenceEquals(simulation, CurrentSimulation) ||
                Phase != OfficeCampaignPhase.ActiveShift) return;
            Rules.Observe(simulation);
            if (!simulation.Shift.Success) return;
            Phase = CurrentShiftOrdinal < OfficeCampaignScenario.ShiftCount
                ? OfficeCampaignPhase.ChooseUpgrade
                : OfficeCampaignPhase.CampaignResult;
        }

        internal bool TryChooseUpgrade(OfficeUpgradeFamily family)
        {
            if (Phase != OfficeCampaignPhase.ChooseUpgrade ||
                CurrentShiftOrdinal >= OfficeCampaignScenario.ShiftCount ||
                !Upgrades.TryChoose(family)) return false;
            Phase = OfficeCampaignPhase.ReadyForNextShift;
            return true;
        }

        internal bool TryContinueToNextShift()
        {
            if (Phase != OfficeCampaignPhase.ReadyForNextShift ||
                CurrentShiftOrdinal >= OfficeCampaignScenario.ShiftCount) return false;
            CaptureCompletedShift();
            InstitutionalSession.ReleaseNextShift(6);
            CurrentShiftOrdinal++;
            StartCurrentShift(captureCheckpoint: true);
            return true;
        }

        public bool TryRestartCurrentShift()
        {
            if (_shiftStartCheckpoint == null || CurrentSimulation == null ||
                !CurrentSimulation.Shift.RestartRequested ||
                _shiftStartCheckpoint.SchemaVersion !=
                    OfficeCampaignCheckpoint.CurrentSchemaVersion) return false;
            InstitutionalSession = InstitutionalAutomationSession.Restore(
                _shiftStartCheckpoint.Institutional);
            Upgrades = _shiftStartCheckpoint.Upgrades.Clone();
            Rules = _shiftStartCheckpoint.Rules.Clone();
            CurrentShiftOrdinal = _shiftStartCheckpoint.ShiftOrdinal;
            StartCurrentShift(captureCheckpoint: true);
            return true;
        }

        private void StartCurrentShift(bool captureCheckpoint)
        {
            CurrentShift = OfficeCampaignScenario.CreateShift(
                InstitutionalSession,
                CurrentShiftOrdinal,
                _decisionCallbacks);
            Phase = OfficeCampaignPhase.ActiveShift;
            CurrentSimulation = OfficeSimulationState.CreateCampaignShift(
                CurrentShift.Scenario, this);
            if (captureCheckpoint)
                _shiftStartCheckpoint = new OfficeCampaignCheckpoint(
                    CurrentShiftOrdinal,
                    InstitutionalSession.CreateCheckpoint(),
                    Upgrades,
                    Rules);
        }

        private void CaptureCompletedShift()
        {
            _completedShiftSnapshots.Add(CurrentSimulation.OrderedStateSnapshot);
            _completedShiftChecksums.Add(CurrentSimulation.Checksum);
            for (int i = 0; i < CurrentSimulation.Customers.Customers.Count; i++)
            {
                OfficeCustomerState customer =
                    CurrentSimulation.Customers.Customers[i];
                OfficeDecisionRecord decision = CurrentSimulation.Decisions.RecordFor(
                    customer.LinkedAutomationClaimId);
                if (decision != null)
                    _decisionCallbacks.Add(new OfficeCampaignDecisionCallback(
                        CurrentShiftOrdinal, customer.CustomerId, decision));
            }
        }

        private static string ComputeChecksum(string snapshot)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < snapshot.Length; i++)
            {
                hash ^= snapshot[i];
                hash *= 1099511628211UL;
            }
            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }
    }
}
