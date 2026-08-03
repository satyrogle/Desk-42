using System;
using System.Collections.Generic;
using Desk42.Institutional.Player;
using UnityEngine;

namespace Desk42.Product.Automation
{
    internal enum AutomationStationKind
    {
        Intake,
        EvidenceSplit,
        Verification,
        Adjudication,
        Output,
        Legal,
    }

    internal enum AutomationPolicyKind
    {
        ProofFortress = 1,
        RubberStampMill = 2,
        AppealRefinery = 3,
    }

    internal enum AutomationFeedbackKind
    {
        ClaimArrived,
        EvidenceSplit,
        RulingStamped,
        AppealReturned,
        AppealResolved,
        Jammed,
        Repaired,
        Misclassified,
        DeadlineMissed,
        UpgradeInstalled,
        PriorityChanged,
        AppealModeChanged,
        ProcedureBound,
        PolicyChanged,
        ShiftClosed,
        ProcedureDrafted,
        ProcedureUpgraded,
        HoldingCreated,
        PrecedentCited,
        BranchReviewed,
        RunSaved,
        RunLoaded,
    }

    internal sealed class AutomationFlowRuntime : IDisposable
    {
        private readonly Transform _root;
        private readonly List<AutomationStationRuntime> _stations = new();
        private readonly List<AutomationFlowItem> _items = new();
        private readonly Dictionary<AutomationProcedureKind, int>
            _procedureTiers = new();
        private readonly List<AutomationProcedureDraftChoiceCheckpoint>
            _draftChoices = new();
        private readonly HashSet<string> _verificationPatterns =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> _adverseReviewPatterns =
            new(StringComparer.Ordinal);
        private InstitutionalAutomationSession _activeInstitution;
        private AutomationStationRuntime _intake;
        private AutomationStationRuntime _splitter;
        private AutomationStationRuntime _primaryVerifier;
        private AutomationStationRuntime _auxVerifier;
        private AutomationStationRuntime _adjudicator;
        private AutomationStationRuntime _output;
        private AutomationStationRuntime _legal;
        private float _spawnClock = 0.4f;
        private float _spawnInterval = 4.8f;
        private float _elapsed;
        private int _batchSpawned;
        private int _shiftOrdinal = 1;
        private int _routeOrdinal;
        private int _stationSelectionIndex;
        private int _repairCount;
        private int _shiftStartCompleted;
        private int _shiftStartOverdue;
        private int _shiftStartAppealsReturned;
        private int _shiftStartAppealsResolved;
        private int _shiftStartRulings;
        private int _shiftStartHoldings;
        private long _shiftStartSocietyTick;
        private AutomationShiftSummaryCheckpoint _shiftSummary;
        private AutomationBranchReviewCheckpoint _branchReview;
        private const int ClaimsPerShift = 12;
        private const int MaximumShifts = 8;

        internal AutomationFlowRuntime(
            Transform root,
            InstitutionalAutomationSession institution)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _activeInstitution = institution ??
                throw new ArgumentNullException(nameof(institution));
        }

        internal int Spawned { get; private set; }
        internal int Completed { get; private set; }
        internal int AppealsReturned { get; private set; }
        internal int AppealsResolved { get; private set; }
        internal int PrecedentsInstalled { get; private set; }
        internal int OverdueCount { get; private set; }
        internal int ReworkCount { get; private set; }
        internal int JamCount { get; private set; }
        internal int RepairCount => _repairCount;
        internal int SecondaryChecks { get; private set; }
        internal int PossessionCompleted { get; private set; }
        internal int AccessCompleted { get; private set; }
        internal int CollectiveCompleted { get; private set; }
        internal int Credits { get; private set; } = 5;
        internal int ShiftOrdinal => _shiftOrdinal;
        internal long SocietyTick => _activeInstitution?.SocietyTick ?? 0;
        internal int InstitutionalRulings =>
            _activeInstitution?.CommittedRulingCount ?? 0;
        internal int PendingAppeals =>
            _activeInstitution?.PendingAppealCount ?? 0;
        internal float ClaimsPerMinute => _elapsed <= 0.01f
            ? 0f
            : Completed / _elapsed * 60f;
        internal int InFlight => _items.Count;
        internal int UrgentInFlight
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _items.Count; i++)
                    if (_items[i].IsUrgent) count++;
                return count;
            }
        }
        internal float NearestDeadline
        {
            get
            {
                if (_items.Count == 0) return 0f;
                float nearest = float.MaxValue;
                for (int i = 0; i < _items.Count; i++)
                    nearest = Mathf.Min(nearest, _items[i].TimeRemaining);
                return Mathf.Max(0f, nearest);
            }
        }
        internal int VerificationBacklog =>
            (_primaryVerifier?.Workload ?? 0) + (_auxVerifier?.Workload ?? 0);
        internal int ActiveJamCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _stations.Count; i++)
                    if (_stations[i].IsJammed) count++;
                return count;
            }
        }
        internal string Bottleneck
        {
            get
            {
                AutomationStationRuntime worst = null;
                for (int i = 0; i < _stations.Count; i++)
                    if (worst == null || _stations[i].Workload > worst.Workload)
                        worst = _stations[i];
                return worst != null && worst.Workload > 1
                    ? worst.DisplayName.ToUpperInvariant()
                    : "FLOWING";
            }
        }

        internal bool AuxVerifierInstalled => _auxVerifier != null;
        internal bool ParallelRouting { get; private set; }
        internal AutomationRoutePriority RoutePriority { get; private set; } =
            AutomationRoutePriority.Balanced;
        internal AutomationAppealMode AppealMode { get; private set; } =
            AutomationAppealMode.FullRehearing;
        internal AutomationStationRuntime SelectedStation =>
            _stations.Count == 0 ? null : _stations[
                Mathf.Clamp(_stationSelectionIndex, 0, _stations.Count - 1)];
        internal int ProceduresBound => _procedureTiers.Count;
        internal const int MaximumProcedures = 2;
        internal AutomationRunPhase Phase { get; private set; } =
            AutomationRunPhase.DoctrineSelection;
        internal bool DoctrineLocked { get; private set; }
        internal IReadOnlyList<AutomationProcedureDraftChoiceCheckpoint>
            DraftChoices => _draftChoices;
        internal AutomationShiftSummaryCheckpoint ShiftSummary => _shiftSummary;
        internal AutomationBranchReviewCheckpoint BranchReview => _branchReview;
        internal IReadOnlyList<AutomationPrecedentRecord> Precedents =>
            _activeInstitution?.Precedents ??
            Array.Empty<AutomationPrecedentRecord>();
        internal AutomationPolicyKind Policy { get; private set; } =
            AutomationPolicyKind.RubberStampMill;
        internal string PolicyName => Policy switch
        {
            AutomationPolicyKind.ProofFortress => "PROOF FORTRESS",
            AutomationPolicyKind.RubberStampMill => "RUBBER MILL",
            AutomationPolicyKind.AppealRefinery => "APPEAL REFINERY",
            _ => "UNKNOWN",
        };
        internal string PolicyDescription => Policy switch
        {
            AutomationPolicyKind.ProofFortress =>
                "High public-evidence threshold. Weak files hold for Legal or deny.",
            AutomationPolicyKind.RubberStampMill =>
                "Presume valid. Broad recognition turns later appeals into load.",
            AutomationPolicyKind.AppealRefinery =>
                "Moderate threshold. Ambiguous files feed a specialised appeal line.",
            _ => string.Empty,
        };

        internal event Action<AutomationFeedbackKind, string> Feedback;

        internal void Register(AutomationStationRuntime station)
        {
            if (station == null) throw new ArgumentNullException(nameof(station));
            _stations.Add(station);
            station.Completed += HandleStationCompleted;
            station.Jammed += HandleStationJammed;
            station.Misclassified += HandleStationMisclassified;
            station.SetRoutePriority(RoutePriority);
            station.SetSelected(_stations.Count == 1);
            switch (station.Kind)
            {
                case AutomationStationKind.Intake: _intake = station; break;
                case AutomationStationKind.EvidenceSplit: _splitter = station; break;
                case AutomationStationKind.Verification when station.IsAuxiliary:
                    _auxVerifier = station;
                    ParallelRouting = true;
                    ApplyPolicyTuning();
                    break;
                case AutomationStationKind.Verification: _primaryVerifier = station; break;
                case AutomationStationKind.Adjudication: _adjudicator = station; break;
                case AutomationStationKind.Output: _output = station; break;
                case AutomationStationKind.Legal: _legal = station; break;
            }
        }

        internal void ToggleParallelRouting()
        {
            if (_auxVerifier == null) return;
            ParallelRouting = !ParallelRouting;
        }

        internal void CycleRoutePriority()
        {
            RoutePriority = RoutePriority switch
            {
                AutomationRoutePriority.Balanced => AutomationRoutePriority.UrgentFirst,
                AutomationRoutePriority.UrgentFirst => AutomationRoutePriority.DeadlineFirst,
                _ => AutomationRoutePriority.Balanced,
            };
            for (int i = 0; i < _stations.Count; i++)
                _stations[i].SetRoutePriority(RoutePriority);
            Emit(AutomationFeedbackKind.PriorityChanged,
                "ROUTE PRIORITY / " + RoutePriorityName);
        }

        internal void CycleAppealMode()
        {
            if (IsProcedureBound(AutomationProcedureKind.AppealFastTrack))
            {
                AppealMode = AutomationAppealMode.FastTrack;
                Emit(AutomationFeedbackKind.AppealModeChanged,
                    "APPEAL FAST TRACK IS BINDING");
                return;
            }
            AppealMode = AppealMode switch
            {
                AutomationAppealMode.FullRehearing => AutomationAppealMode.FastTrack,
                AutomationAppealMode.FastTrack => AutomationAppealMode.Settlement,
                _ => AutomationAppealMode.FullRehearing,
            };
            ApplyPolicyTuning();
            Emit(AutomationFeedbackKind.AppealModeChanged,
                "APPEALS / " + AppealModeName);
        }

        internal string RoutePriorityName => RoutePriority switch
        {
            AutomationRoutePriority.UrgentFirst => "URGENT FIRST",
            AutomationRoutePriority.DeadlineFirst => "DEADLINE FIRST",
            _ => "BALANCED",
        };

        internal string AppealModeName => AppealMode switch
        {
            AutomationAppealMode.FastTrack => "FAST TRACK",
            AutomationAppealMode.Settlement => "SETTLEMENT",
            _ => "FULL REHEARING",
        };

        internal bool IsProcedureBound(AutomationProcedureKind kind)
        {
            return _procedureTiers.ContainsKey(kind);
        }

        internal int ProcedureTier(AutomationProcedureKind kind)
        {
            return _procedureTiers.TryGetValue(kind, out int tier) ? tier : 0;
        }

        internal bool ForceBindProcedureForTest(AutomationProcedureKind kind)
        {
            if (!Enum.IsDefined(typeof(AutomationProcedureKind), kind)) return false;
            if (!_procedureTiers.ContainsKey(kind) &&
                _procedureTiers.Count >= MaximumProcedures) return false;
            int tier = Mathf.Clamp(ProcedureTier(kind) + 1, 1, 3);
            _procedureTiers[kind] = tier;
            if (kind == AutomationProcedureKind.AppealFastTrack)
                AppealMode = AutomationAppealMode.FastTrack;
            for (int i = 0; i < _items.Count; i++)
                ConfigureItemProcedures(_items[i]);
            ApplyPolicyTuning();
            Emit(AutomationFeedbackKind.ProcedureBound,
                AutomationProcedureNames.ShortName(kind) + " T" + tier +
                " / " + AutomationProcedureNames.Effect(kind, tier));
            return true;
        }

        internal bool ChooseProcedureDraft(int choiceIndex)
        {
            if (Phase != AutomationRunPhase.ShiftClose ||
                choiceIndex < 0 || choiceIndex >= _draftChoices.Count) return false;
            AutomationProcedureDraftChoiceCheckpoint choice =
                _draftChoices[choiceIndex];
            int current = ProcedureTier(choice.Kind);
            if (current == 0 && _procedureTiers.Count >= MaximumProcedures)
                return false;
            _procedureTiers[choice.Kind] = choice.ResultingTier;
            if (choice.Kind == AutomationProcedureKind.AppealFastTrack)
                AppealMode = AutomationAppealMode.FastTrack;
            for (int i = 0; i < _items.Count; i++)
                ConfigureItemProcedures(_items[i]);
            ApplyPolicyTuning();
            Emit(current == 0
                    ? AutomationFeedbackKind.ProcedureBound
                    : AutomationFeedbackKind.ProcedureUpgraded,
                AutomationProcedureNames.ShortName(choice.Kind) + " T" +
                choice.ResultingTier + " / " +
                AutomationProcedureNames.Effect(
                    choice.Kind, choice.ResultingTier));
            BeginNextShift();
            return true;
        }

        internal bool ContinueAfterShift()
        {
            if (Phase != AutomationRunPhase.ShiftClose ||
                _draftChoices.Count != 0) return false;
            BeginNextShift();
            return true;
        }

        internal void SelectNextStation()
        {
            if (_stations.Count == 0) return;
            _stations[_stationSelectionIndex].SetSelected(false);
            _stationSelectionIndex = (_stationSelectionIndex + 1) % _stations.Count;
            _stations[_stationSelectionIndex].SetSelected(true);
        }

        internal bool SelectFirstJammedStation()
        {
            for (int i = 0; i < _stations.Count; i++)
            {
                if (!_stations[i].IsJammed) continue;
                SelectedStation?.SetSelected(false);
                _stationSelectionIndex = i;
                SelectedStation.SetSelected(true);
                return true;
            }
            return false;
        }

        internal bool SelectStationNear(Vector3 worldPosition)
        {
            if (_stations.Count == 0) return false;
            int bestIndex = -1;
            float bestDistance = 2.6f * 2.6f;
            for (int i = 0; i < _stations.Count; i++)
            {
                Vector3 delta = _stations[i].Position - worldPosition;
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestIndex = i;
            }
            if (bestIndex < 0) return false;
            SelectedStation?.SetSelected(false);
            _stationSelectionIndex = bestIndex;
            SelectedStation.SetSelected(true);
            return true;
        }

        internal bool UpgradeSelected(AutomationUpgradeKind kind)
        {
            AutomationStationRuntime station = SelectedStation;
            if (station == null) return false;
            int cost = station.UpgradeCost;
            if (Credits < cost)
            {
                Emit(AutomationFeedbackKind.Jammed,
                    "UPGRADE BLOCKED / NEED " + cost + " CREDITS");
                return false;
            }
            if (!station.TryUpgrade(kind)) return false;
            Credits -= cost;
            ApplyPolicyTuning();
            string effect = kind switch
            {
                AutomationUpgradeKind.Throughput =>
                    "+14% CYCLE SPEED / +13% HEAT / +1.8% FAULT",
                AutomationUpgradeKind.Capacity =>
                    "+2 SAFE QUEUE / FASTER COOLING",
                AutomationUpgradeKind.Reliability =>
                    "-4.5% FAULT / +5.5% CYCLE TIME",
                _ => string.Empty,
            };
            Emit(AutomationFeedbackKind.UpgradeInstalled,
                station.DisplayName.ToUpperInvariant() + " / " + kind.ToString().ToUpperInvariant() +
                " T" + station.UpgradeLevel(kind) + " / " + effect);
            return true;
        }

        internal bool RepairSelected()
        {
            AutomationStationRuntime station = SelectedStation;
            if (station == null || !station.Repair()) return false;
            _repairCount++;
            Emit(AutomationFeedbackKind.Repaired,
                station.DisplayName.ToUpperInvariant() + " CLEARED / HEAT 25%");
            return true;
        }

#if UNITY_INCLUDE_TESTS
        internal bool CreateValidationJamOnSelected()
        {
            AutomationStationRuntime station = SelectedStation;
            return station != null && station.CreateValidationJam();
        }
#endif

        internal bool ChooseDoctrine(AutomationPolicyKind policy)
        {
            if (!Enum.IsDefined(typeof(AutomationPolicyKind), policy))
                throw new ArgumentOutOfRangeException(nameof(policy));
            if (DoctrineLocked || Phase != AutomationRunPhase.DoctrineSelection)
            {
                Emit(AutomationFeedbackKind.Jammed,
                    "DOCTRINE IS BINDING FOR THIS RUN");
                return false;
            }
            Policy = policy;
            DoctrineLocked = true;
            Phase = AutomationRunPhase.ActiveProcessing;
            switch (policy)
            {
                case AutomationPolicyKind.ProofFortress:
                    _spawnInterval = 5.8f;
                    break;
                case AutomationPolicyKind.RubberStampMill:
                    _spawnInterval = 3.9f;
                    break;
                case AutomationPolicyKind.AppealRefinery:
                    _spawnInterval = 4.7f;
                    break;
            }
            ApplyPolicyTuning();
            CaptureShiftBaseline();
            Emit(AutomationFeedbackKind.PolicyChanged,
                PolicyName + " / DOCTRINE BOUND FOR EIGHT SHIFTS");
            return true;
        }

        internal void Tick(float deltaTime)
        {
            if (deltaTime <= 0f ||
                Phase != AutomationRunPhase.ActiveProcessing) return;
            _elapsed += deltaTime;
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].TickAge(deltaTime))
                {
                    OverdueCount++;
                    Emit(AutomationFeedbackKind.DeadlineMissed,
                        _items[i].ClaimId + " MISSED DEADLINE");
                }
            _spawnClock -= deltaTime;
            if (_spawnClock <= 0f && _intake != null)
            {
                if (_batchSpawned < _activeInstitution.Claims.Count)
                {
                    SpawnClaim();
                    _spawnClock = _spawnInterval;
                }
            }

            for (int i = 0; i < _stations.Count; i++)
                _stations[i].Tick(deltaTime);
            if (_batchSpawned >= _activeInstitution.Claims.Count &&
                _items.Count == 0)
                CloseShift();
        }

        internal bool CyclePrecedentMode(int ledgerIndex)
        {
            IReadOnlyList<AutomationPrecedentRecord> precedents = Precedents;
            if (ledgerIndex < 0 || ledgerIndex >= precedents.Count) return false;
            AutomationPrecedentRecord precedent = precedents[ledgerIndex];
            AutomationPrecedentMode next = precedent.Mode switch
            {
                AutomationPrecedentMode.MandatoryCitation =>
                    AutomationPrecedentMode.PermittedCitation,
                AutomationPrecedentMode.PermittedCitation =>
                    AutomationPrecedentMode.HumanReviewRequired,
                AutomationPrecedentMode.HumanReviewRequired =>
                    AutomationPrecedentMode.DoNotAutomate,
                _ => AutomationPrecedentMode.MandatoryCitation,
            };
            _activeInstitution.SetPrecedentMode(precedent.HoldingId, next);
            Emit(AutomationFeedbackKind.PrecedentCited,
                "LEDGER / " + precedent.Issue.ToUpperInvariant() + " / " +
                next.ToString().ToUpperInvariant());
            return true;
        }

        private void CaptureShiftBaseline()
        {
            _shiftStartCompleted = Completed;
            _shiftStartOverdue = OverdueCount;
            _shiftStartAppealsReturned = AppealsReturned;
            _shiftStartAppealsResolved = AppealsResolved;
            _shiftStartRulings = InstitutionalRulings;
            _shiftStartHoldings = PrecedentsInstalled;
            _shiftStartSocietyTick = SocietyTick;
        }

        private void CloseShift()
        {
            if (Phase != AutomationRunPhase.ActiveProcessing) return;
            PrecedentsInstalled = _activeInstitution.HoldingCount;
            _shiftSummary = new AutomationShiftSummaryCheckpoint
            {
                ShiftOrdinal = _shiftOrdinal,
                ClaimsCompleted = Completed - _shiftStartCompleted,
                DeadlinesMissed = OverdueCount - _shiftStartOverdue,
                AppealsCreated = AppealsReturned - _shiftStartAppealsReturned,
                AppealsResolved = AppealsResolved - _shiftStartAppealsResolved,
                HoldingsEstablished = PrecedentsInstalled - _shiftStartHoldings,
                SocietyChanges = Mathf.Max(0,
                    (int)(SocietyTick - _shiftStartSocietyTick)),
            };
            Emit(AutomationFeedbackKind.ShiftClosed,
                "SHIFT " + _shiftOrdinal.ToString("D2") +
                " CLOSED / " + _shiftSummary.ClaimsCompleted +
                " CLAIMS / " + _shiftSummary.AppealsCreated + " APPEALS");

            if (_shiftOrdinal >= MaximumShifts)
            {
                BuildBranchReview();
                Phase = AutomationRunPhase.BranchReview;
                Emit(AutomationFeedbackKind.BranchReviewed,
                    "BRANCH REVIEW / " + _branchReview.Outcome.ToString().ToUpperInvariant());
                return;
            }

            Phase = AutomationRunPhase.ShiftClose;
            _draftChoices.Clear();
            if (ShouldDraftAfterShift(_shiftOrdinal)) GenerateProcedureDraft();
        }

        private void GenerateProcedureDraft()
        {
            var eligible = new List<AutomationProcedureKind>();
            for (int number = 1; number <= 6; number++)
            {
                var kind = (AutomationProcedureKind)number;
                int tier = ProcedureTier(kind);
                if (tier > 0 && tier < 3 ||
                    tier == 0 && _procedureTiers.Count < MaximumProcedures)
                    eligible.Add(kind);
            }
            if (eligible.Count == 0) return;
            int start = (_shiftOrdinal * 2 + (int)Policy) % eligible.Count;
            int offered = Mathf.Min(3, eligible.Count);
            for (int offset = 0; offset < offered; offset++)
            {
                AutomationProcedureKind kind =
                    eligible[(start + offset) % eligible.Count];
                _draftChoices.Add(new AutomationProcedureDraftChoiceCheckpoint
                {
                    Kind = kind,
                    ResultingTier = Mathf.Clamp(ProcedureTier(kind) + 1, 1, 3),
                });
            }
            Emit(AutomationFeedbackKind.ProcedureDrafted,
                "INSTITUTIONAL DEVELOPMENT / CHOOSE ONE OF " +
                _draftChoices.Count);
        }

        private void BeginNextShift()
        {
            if (Phase != AutomationRunPhase.ShiftClose ||
                _shiftOrdinal >= MaximumShifts) return;
            _shiftOrdinal++;
            _activeInstitution.ReleaseNextShift(ClaimsPerShift);
            _batchSpawned = 0;
            _spawnClock = 2.5f;
            _shiftSummary = null;
            _draftChoices.Clear();
            Phase = AutomationRunPhase.ActiveProcessing;
            CaptureShiftBaseline();
            Emit(AutomationFeedbackKind.PolicyChanged,
                "SHIFT " + _shiftOrdinal.ToString("D2") +
                " / SAME SOCIETY / DOCKET RELEASED");
        }

        private void BuildBranchReview()
        {
            _activeInstitution.ValidateCurrentState();
            AutomationSocietyMetrics society = _activeInstitution.SocietyMetrics;
            IReadOnlyList<AutomationPrecedentRecord> precedents = Precedents;
            int liability = _activeInstitution.PendingAppealCount * 8;
            int conflicts = 0;
            for (int i = 0; i < precedents.Count; i++)
            {
                liability += precedents[i].LiabilityExposure;
                conflicts += precedents[i].ConflictingHoldingCount;
            }
            int throughput = Spawned == 0
                ? 0
                : Mathf.RoundToInt(Completed * 100f / Spawned);
            int deadlineCompliance = Completed == 0
                ? 0
                : Mathf.Clamp(100 - Mathf.RoundToInt(
                    OverdueCount * 100f / Completed), 0, 100);
            int error = Completed == 0
                ? 0
                : Mathf.Clamp(Mathf.RoundToInt(
                    ReworkCount * 100f / Completed), 0, 100);
            int reversal = AppealsResolved == 0
                ? 0
                : Mathf.Clamp(Mathf.RoundToInt(
                    _activeInstitution.AppealReversalCount * 100f /
                    AppealsResolved), 0, 100);
            int stability = Mathf.Clamp(
                society.AverageInstitutionalTrust -
                society.TotalRelationshipFear / Mathf.Max(1, society.AgentCount * 4) +
                society.RecognisedCollectiveMembers * 3,
                0,
                100);
            int legitimacy = Mathf.Clamp(
                (deadlineCompliance + stability + (100 - reversal)) / 3,
                0,
                100);
            int consistency = Mathf.Clamp(100 - conflicts * 18, 0, 100);
            int resilience = Mathf.Clamp(
                72 + RepairCount * 5 - JamCount * 4 +
                TotalStationUpgradeLevel() * 2,
                0,
                100);

            AutomationBranchOutcome outcome;
            if (conflicts >= 2) outcome = AutomationBranchOutcome.PrecedentCollapse;
            else if (throughput >= 78 && stability < 42)
                outcome = AutomationBranchOutcome.EfficientButHarmful;
            else if (stability >= 68 && Credits < 3)
                outcome = AutomationBranchOutcome.HumaneButInsolvent;
            else if (Policy == AutomationPolicyKind.RubberStampMill &&
                     precedents.Count >= 3)
                outcome = AutomationBranchOutcome.Captured;
            else if (throughput >= 72 && legitimacy < 50)
                outcome = AutomationBranchOutcome.AdministrativeBlindness;
            else outcome = AutomationBranchOutcome.Certified;

            _branchReview = new AutomationBranchReviewCheckpoint
            {
                Outcome = outcome,
                Throughput = throughput,
                DeadlineCompliance = deadlineCompliance,
                AvoidableError = error,
                AppealReversalRate = reversal,
                UnresolvedLiability = liability,
                SocietyStability = stability,
                InstitutionalLegitimacy = legitimacy,
                PrecedentConsistency = consistency,
                MachineResilience = resilience,
            };
        }

        private int TotalStationUpgradeLevel()
        {
            int total = 0;
            for (int i = 0; i < _stations.Count; i++)
                total += _stations[i].TotalUpgradeLevel;
            return total;
        }

        private static bool ShouldDraftAfterShift(int shiftOrdinal)
        {
            return shiftOrdinal == 1 || shiftOrdinal == 2 ||
                   shiftOrdinal == 4 || shiftOrdinal == 6;
        }

        internal AutomationRunCheckpoint CaptureCheckpoint()
        {
            var flow = new AutomationFlowCheckpoint
            {
                Phase = Phase,
                Policy = Policy,
                DoctrineLocked = DoctrineLocked,
                Spawned = Spawned,
                Completed = Completed,
                AppealsReturned = AppealsReturned,
                AppealsResolved = AppealsResolved,
                OverdueCount = OverdueCount,
                ReworkCount = ReworkCount,
                JamCount = JamCount,
                RepairCount = _repairCount,
                SecondaryChecks = SecondaryChecks,
                PossessionCompleted = PossessionCompleted,
                AccessCompleted = AccessCompleted,
                CollectiveCompleted = CollectiveCompleted,
                Credits = Credits,
                Elapsed = _elapsed,
                SpawnClock = _spawnClock,
                SpawnInterval = _spawnInterval,
                BatchSpawned = _batchSpawned,
                ShiftOrdinal = _shiftOrdinal,
                RouteOrdinal = _routeOrdinal,
                StationSelectionIndex = _stationSelectionIndex,
                ParallelRouting = ParallelRouting,
                RoutePriority = RoutePriority,
                AppealMode = AppealMode,
                ShiftStartCompleted = _shiftStartCompleted,
                ShiftStartOverdue = _shiftStartOverdue,
                ShiftStartAppealsReturned = _shiftStartAppealsReturned,
                ShiftStartAppealsResolved = _shiftStartAppealsResolved,
                ShiftStartRulings = _shiftStartRulings,
                ShiftStartHoldings = _shiftStartHoldings,
                ShiftStartSocietyTick = _shiftStartSocietyTick,
                ShiftSummary = _shiftSummary,
                BranchReview = _branchReview,
            };
            foreach (KeyValuePair<AutomationProcedureKind, int> procedure in
                     _procedureTiers)
                flow.Procedures.Add(new AutomationProcedureTierCheckpoint
                {
                    Kind = procedure.Key,
                    Tier = procedure.Value,
                });
            flow.Procedures.Sort((left, right) =>
                left.Kind.CompareTo(right.Kind));
            flow.VerificationPatternIssues.AddRange(_verificationPatterns);
            flow.VerificationPatternIssues.Sort(StringComparer.Ordinal);
            flow.AdverseReviewPatternIssues.AddRange(_adverseReviewPatterns);
            flow.AdverseReviewPatternIssues.Sort(StringComparer.Ordinal);
            for (int i = 0; i < _draftChoices.Count; i++)
                flow.DraftChoices.Add(new AutomationProcedureDraftChoiceCheckpoint
                {
                    Kind = _draftChoices[i].Kind,
                    ResultingTier = _draftChoices[i].ResultingTier,
                });
            for (int i = 0; i < _stations.Count; i++)
                flow.Stations.Add(_stations[i].CaptureCheckpoint());
            for (int i = 0; i < _items.Count; i++)
                flow.Items.Add(_items[i].CaptureCheckpoint());
            flow.Items.Sort((left, right) =>
                left.Sequence.CompareTo(right.Sequence));
            return new AutomationRunCheckpoint
            {
                Institution = _activeInstitution.CreateCheckpoint(),
                Flow = flow,
            };
        }

        internal void RestoreCheckpoint(AutomationRunCheckpoint checkpoint)
        {
            if (checkpoint?.Flow == null || checkpoint.Institution == null)
                throw new ArgumentException(
                    "A complete automation checkpoint is required.",
                    nameof(checkpoint));
            AutomationFlowCheckpoint saved = checkpoint.Flow;
            for (int i = 0; i < _stations.Count; i++)
                _stations[i].ResetRuntime();
            for (int i = _items.Count - 1; i >= 0; i--)
                _items[i].Dispose();
            _items.Clear();

            _activeInstitution = InstitutionalAutomationSession.Restore(
                checkpoint.Institution);
            Phase = saved.Phase;
            Policy = saved.Policy;
            DoctrineLocked = saved.DoctrineLocked;
            Spawned = saved.Spawned;
            Completed = saved.Completed;
            AppealsReturned = saved.AppealsReturned;
            AppealsResolved = saved.AppealsResolved;
            PrecedentsInstalled = _activeInstitution.HoldingCount;
            OverdueCount = saved.OverdueCount;
            ReworkCount = saved.ReworkCount;
            JamCount = saved.JamCount;
            _repairCount = saved.RepairCount;
            SecondaryChecks = saved.SecondaryChecks;
            PossessionCompleted = saved.PossessionCompleted;
            AccessCompleted = saved.AccessCompleted;
            CollectiveCompleted = saved.CollectiveCompleted;
            Credits = saved.Credits;
            _elapsed = saved.Elapsed;
            _spawnClock = saved.SpawnClock;
            _spawnInterval = saved.SpawnInterval;
            _batchSpawned = saved.BatchSpawned;
            _shiftOrdinal = saved.ShiftOrdinal;
            _routeOrdinal = saved.RouteOrdinal;
            _stationSelectionIndex = Mathf.Clamp(
                saved.StationSelectionIndex, 0, Mathf.Max(0, _stations.Count - 1));
            ParallelRouting = saved.ParallelRouting;
            RoutePriority = saved.RoutePriority;
            AppealMode = saved.AppealMode;
            _shiftStartCompleted = saved.ShiftStartCompleted;
            _shiftStartOverdue = saved.ShiftStartOverdue;
            _shiftStartAppealsReturned = saved.ShiftStartAppealsReturned;
            _shiftStartAppealsResolved = saved.ShiftStartAppealsResolved;
            _shiftStartRulings = saved.ShiftStartRulings;
            _shiftStartHoldings = saved.ShiftStartHoldings;
            _shiftStartSocietyTick = saved.ShiftStartSocietyTick;
            _shiftSummary = saved.ShiftSummary;
            _branchReview = saved.BranchReview;

            _procedureTiers.Clear();
            for (int i = 0; i < saved.Procedures.Count; i++)
            {
                AutomationProcedureTierCheckpoint procedure = saved.Procedures[i];
                if (procedure == null || !Enum.IsDefined(
                        typeof(AutomationProcedureKind), procedure.Kind) ||
                    procedure.Tier < 1 || procedure.Tier > 3 ||
                    _procedureTiers.ContainsKey(procedure.Kind))
                    throw new InvalidOperationException(
                        "Run save contains an invalid procedure build.");
                _procedureTiers.Add(procedure.Kind, procedure.Tier);
            }
            if (_procedureTiers.Count > MaximumProcedures)
                throw new InvalidOperationException(
                    "Run save exceeds the binding procedure slots.");
            RestoreStableSet(
                saved.VerificationPatternIssues, _verificationPatterns);
            RestoreStableSet(
                saved.AdverseReviewPatternIssues, _adverseReviewPatterns);
            _draftChoices.Clear();
            for (int i = 0; i < saved.DraftChoices.Count; i++)
                _draftChoices.Add(new AutomationProcedureDraftChoiceCheckpoint
                {
                    Kind = saved.DraftChoices[i].Kind,
                    ResultingTier = saved.DraftChoices[i].ResultingTier,
                });

            var itemById = new Dictionary<string, AutomationFlowItem>(
                StringComparer.Ordinal);
            for (int i = 0; i < saved.Items.Count; i++)
            {
                AutomationFlowItem item = CreateRestoredItem(saved.Items[i]);
                if (itemById.ContainsKey(item.FlowItemId))
                    throw new InvalidOperationException(
                        "Run save contains a duplicate flow item identity.");
                itemById.Add(item.FlowItemId, item);
                _items.Add(item);
            }
            for (int i = 0; i < saved.Stations.Count; i++)
            {
                AutomationStationCheckpoint stationState = saved.Stations[i];
                AutomationStationRuntime station = FindStation(
                    stationState.Kind, stationState.IsAuxiliary) ??
                    throw new InvalidOperationException(
                        "Run save references an unavailable station.");
                station.RestoreCheckpoint(stationState, itemById);
            }
            for (int i = 0; i < _stations.Count; i++)
            {
                _stations[i].SetRoutePriority(RoutePriority);
                _stations[i].SetSelected(i == _stationSelectionIndex);
            }
            for (int i = 0; i < _items.Count; i++)
                ConfigureItemProcedures(_items[i]);
            ApplyPolicyTuning();
            Emit(AutomationFeedbackKind.RunLoaded,
                "RUN RESTORED / SHIFT " + _shiftOrdinal.ToString("D2") +
                " / " + _items.Count + " DOSSIERS LIVE");
        }

        private AutomationFlowItem CreateRestoredItem(
            AutomationFlowItemCheckpoint saved)
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.FlowItemId))
                throw new InvalidOperationException(
                    "Run save contains an invalid flow item.");
            AutomationFlowItem item;
            if (saved.IsAppeal)
            {
                AutomationAppealPacket appeal =
                    _activeInstitution.GetAppealPacket(saved.AppealId) ??
                    throw new InvalidOperationException(
                        "Run save appeal is missing from the continuing docket.");
                GameObject token = AutomationVisualFactory.CreateFolderToken(
                    _root,
                    saved.DisplayId,
                    new Color(0.68f, 0.22f, 0.18f));
                token.transform.position = new Vector3(13f, 0.42f, -3.2f);
                var view = token.AddComponent<AutomationDossierView>();
                view.MarkAppeal(appeal.OriginatingRulingId);
                item = new AutomationFlowItem(
                    _activeInstitution,
                    appeal,
                    AutomationClaimProfile.ForAppeal(appeal),
                    token,
                    view,
                    saved.DisplayId);
            }
            else
            {
                AutomationPublicClaim claim = _activeInstitution.FindClaim(
                    saved.AutomationClaimId) ?? throw new InvalidOperationException(
                    "Run save claim is missing from the current docket batch.");
                Color colour = claim.Issue.IndexOf(
                    "Collective", StringComparison.OrdinalIgnoreCase) >= 0
                    ? new Color(0.50f, 0.26f, 0.48f)
                    : claim.Issue.IndexOf(
                        "Access", StringComparison.OrdinalIgnoreCase) >= 0
                        ? new Color(0.30f, 0.50f, 0.66f)
                        : new Color(0.82f, 0.70f, 0.43f);
                GameObject token = AutomationVisualFactory.CreateFolderToken(
                    _root, saved.DisplayId, colour);
                token.transform.position = new Vector3(-13f, 0.42f, 2.6f);
                var view = token.AddComponent<AutomationDossierView>();
                item = new AutomationFlowItem(
                    _activeInstitution,
                    claim,
                    AutomationClaimProfile.Create(claim, _shiftOrdinal),
                    token,
                    view,
                    saved.DisplayId);
            }
            item.RestoreCheckpoint(
                saved,
                saved.IsAppeal
                    ? null
                    : _activeInstitution.GetRulingResult(
                        saved.AutomationClaimId),
                saved.IsAppeal
                    ? _activeInstitution.GetAppealResolutionResult(saved.AppealId)
                    : null);
            return item;
        }

        private AutomationStationRuntime FindStation(
            AutomationStationKind kind,
            bool isAuxiliary)
        {
            for (int i = 0; i < _stations.Count; i++)
                if (_stations[i].Kind == kind &&
                    _stations[i].IsAuxiliary == isAuxiliary)
                    return _stations[i];
            return null;
        }

        private static void RestoreStableSet(
            IReadOnlyList<string> source,
            HashSet<string> destination)
        {
            destination.Clear();
            for (int i = 0; i < source.Count; i++)
                if (string.IsNullOrWhiteSpace(source[i]) ||
                    !destination.Add(source[i]))
                    throw new InvalidOperationException(
                        "Run save contains an invalid retained issue pattern.");
        }

        public void Dispose()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
                _items[i].Dispose();
            _items.Clear();
            for (int i = 0; i < _stations.Count; i++)
            {
                _stations[i].Completed -= HandleStationCompleted;
                _stations[i].Jammed -= HandleStationJammed;
                _stations[i].Misclassified -= HandleStationMisclassified;
            }
            _stations.Clear();
        }

        private void SpawnClaim()
        {
            Spawned++;
            AutomationPublicClaim claim = _activeInstitution.Claims[_batchSpawned++];
            Color[] folders =
            {
                new(0.82f, 0.70f, 0.43f),
                new(0.48f, 0.64f, 0.57f),
                new(0.66f, 0.47f, 0.38f),
                new(0.48f, 0.55f, 0.67f),
            };
            Color folderColour = claim.Issue.IndexOf(
                "Collective", StringComparison.OrdinalIgnoreCase) >= 0
                ? new Color(0.50f, 0.26f, 0.48f)
                : claim.Issue.IndexOf(
                    "Access", StringComparison.OrdinalIgnoreCase) >= 0
                    ? new Color(0.30f, 0.50f, 0.66f)
                    : folders[(Spawned - 1) % folders.Length];
            GameObject token = AutomationVisualFactory.CreateFolderToken(
                _root, "S" + _shiftOrdinal + "-" + claim.DisplayId,
                folderColour);
            token.transform.position = new Vector3(-13f, 0.42f, 2.6f);
            var view = token.AddComponent<AutomationDossierView>();
            AutomationClaimProfile profile = AutomationClaimProfile.Create(
                claim, _shiftOrdinal);
            var item = new AutomationFlowItem(
                _activeInstitution, claim, profile, token, view,
                "S" + _shiftOrdinal + "-" + claim.DisplayId);
            ConfigureItemProcedures(item);
            _items.Add(item);
            _intake.Enqueue(item);
            Emit(AutomationFeedbackKind.ClaimArrived,
                claim.DisplayId + " ENTERED INTAKE");
        }

        private void HandleStationCompleted(
            AutomationStationRuntime station, AutomationFlowItem item)
        {
            switch (station.Kind)
            {
                case AutomationStationKind.Intake:
                    _splitter.Enqueue(item);
                    break;
                case AutomationStationKind.EvidenceSplit:
                    item.RevealEvidencePacket();
                    Emit(AutomationFeedbackKind.EvidenceSplit,
                        item.Claim.DisplayId + " / RECORD SEPARATED FROM ALLEGATION");
                    SelectVerifier(item).Enqueue(item);
                    break;
                case AutomationStationKind.Verification:
                    item.RecordVerificationPass();
                    if (item.VerificationPasses >= 2 && ProcedureTier(
                            AutomationProcedureKind.
                                MandatorySecondaryVerification) >= 3 &&
                        _verificationPatterns.Add(item.Profile.IssueFamily))
                        Emit(AutomationFeedbackKind.PrecedentCited,
                            item.Profile.IssueFamily.ToUpperInvariant() +
                            " / VERIFICATION PATTERN RETAINED");
                    if (item.ConsumeMisclassificationForRework())
                    {
                        ReworkCount++;
                        _legal.Enqueue(item);
                        Emit(AutomationFeedbackKind.Misclassified,
                            item.ClaimId + " FLAGGED / LEGAL REWORK " +
                            item.ReworkAttempts.ToString("D2"));
                        break;
                    }
                    if (IsProcedureBound(
                            AutomationProcedureKind.MandatorySecondaryVerification) &&
                        item.VerificationPasses < 2)
                    {
                        SecondaryChecks++;
                        if (item.IsUrgent && ProcedureTier(
                                AutomationProcedureKind.
                                    MandatorySecondaryVerification) >= 2)
                            item.GrantDeadlineGrace(8f);
                        AutomationStationRuntime secondary =
                            station == _primaryVerifier && _auxVerifier != null
                                ? _auxVerifier
                                : _primaryVerifier;
                        secondary.Enqueue(item);
                        Emit(AutomationFeedbackKind.EvidenceSplit,
                            item.ClaimId + " / MANDATORY SECOND CHECK");
                        break;
                    }
                    if (IsProcedureBound(
                            AutomationProcedureKind.AutomaticAdverseReview) &&
                        item.IsOverdue && item.BeginAdverseReview())
                    {
                        if (ProcedureTier(
                                AutomationProcedureKind.AutomaticAdverseReview) >= 2)
                            item.PauseDeadline(10f);
                        _legal.Enqueue(item);
                        Emit(AutomationFeedbackKind.AppealReturned,
                            item.ClaimId + " / AUTOMATIC ADVERSE REVIEW");
                        break;
                    }
                    _adjudicator.Enqueue(item);
                    break;
                case AutomationStationKind.Adjudication:
                    if (item.IsAppeal)
                    {
                        ResolveAppeal(item);
                    }
                    else
                    {
                        if (ProcedureTier(
                                AutomationProcedureKind.ProtectedEvidenceChannel) >= 3 &&
                            item.Claim.Issue.IndexOf(
                                "Access", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            !item.AdverseReviewCompleted &&
                            item.BeginAdverseReview())
                        {
                            _legal.Enqueue(item);
                            Emit(AutomationFeedbackKind.AppealReturned,
                                item.ClaimId +
                                " / PROTECTED ACCESS RESTORATION REVIEW");
                            break;
                        }
                        if (_activeInstitution.RequiresHumanPrecedentReview(
                                item.Claim.AutomationClaimId) &&
                            !item.AdverseReviewCompleted &&
                            item.BeginAdverseReview())
                        {
                            _legal.Enqueue(item);
                            Emit(AutomationFeedbackKind.PrecedentCited,
                                item.ClaimId +
                                " / HOLDING REQUIRES HUMAN REVIEW");
                            break;
                        }
                        if (Policy == AutomationPolicyKind.ProofFortress &&
                            NeedsLegalHold(item) && item.BeginAdverseReview())
                        {
                            _legal.Enqueue(item);
                            Emit(AutomationFeedbackKind.AppealReturned,
                                item.ClaimId + " / EVIDENCE HOLD / LEGAL REVIEW");
                            break;
                        }
                        PlayerRulingDisposition disposition =
                            SelectAutomaticDisposition(item);
                        AutomationRulingResult ruling = item.Institution.Commit(
                            item.Claim.AutomationClaimId,
                            Policy == AutomationPolicyKind.ProofFortress
                                ? PlayerScopeChoice.Narrow
                                : PlayerScopeChoice.Broad,
                            disposition,
                            InstitutionalProcedures(),
                            item.AdverseReviewCompleted);
                        item.ApplyRuling(ruling);
                        if (ruling.CitedHoldingCount > 0)
                            Emit(AutomationFeedbackKind.PrecedentCited,
                                item.Claim.DisplayId + " / " +
                                ruling.CitedHoldingCount +
                                " HOLDING(S) CITED");
                        Emit(AutomationFeedbackKind.RulingStamped,
                            item.Claim.DisplayId + " / " + PolicyName + " / " +
                            disposition.ToString().ToUpperInvariant());
                    }
                    _output.Enqueue(item);
                    break;
                case AutomationStationKind.Output:
                    AutomationAppealPacket appeal = item.Ruling?.Appeal;
                    bool wasAppeal = item.IsAppeal;
                    _items.Remove(item);
                    item.Dispose();
                    if (wasAppeal)
                    {
                        AppealsResolved++;
                        Credits++;
                        if (ProcedureTier(
                                AutomationProcedureKind.AppealFastTrack) >= 3 &&
                            item.AppealResolution?.EstablishedHolding == true)
                            Credits++;
                        PrecedentsInstalled = item.Institution?.HoldingCount ??
                            PrecedentsInstalled;
                        ApplyPolicyTuning();
                        Emit(AutomationFeedbackKind.AppealResolved,
                            "APPEAL RESOLVED / PRECEDENT " +
                            PrecedentsInstalled.ToString("D2"));
                        if (item.AppealResolution?.EstablishedHolding == true)
                            Emit(AutomationFeedbackKind.HoldingCreated,
                                "HOLDING CREATED / LEDGER UPDATED");
                    }
                    else
                    {
                        Completed++;
                        RecordCompletedFamily(item.Claim);
                        if (!item.IsOverdue) Credits++;
                    }
                    if (appeal != null) SpawnAppeal(appeal);
                    break;
                case AutomationStationKind.Legal:
                    if (!item.IsAppeal)
                    {
                        if (item.AdverseReviewPending)
                        {
                            item.CompleteAdverseReview();
                            if (ProcedureTier(
                                    AutomationProcedureKind.
                                        AutomaticAdverseReview) >= 3)
                                _adverseReviewPatterns.Add(
                                    item.Profile.IssueFamily);
                            _adjudicator.Enqueue(item);
                        }
                        else
                        {
                            SelectVerifier(item).Enqueue(item);
                        }
                    }
                    else if (AppealMode == AutomationAppealMode.Settlement)
                    {
                        ResolveAppeal(item);
                        _output.Enqueue(item);
                    }
                    else if (AppealMode == AutomationAppealMode.FastTrack)
                    {
                        _adjudicator.Enqueue(item);
                    }
                    else if (Policy == AutomationPolicyKind.AppealRefinery &&
                             _auxVerifier != null && ParallelRouting)
                    {
                        SelectVerifier(item).Enqueue(item);
                    }
                    else
                    {
                        _primaryVerifier.Enqueue(item);
                    }
                    break;
            }
        }

        private void SpawnAppeal(AutomationAppealPacket appeal)
        {
            if (appeal == null || _legal == null) return;
            AppealsReturned++;
            string label = "APPEAL 42-" + AppealsReturned.ToString("D2");
            GameObject token = AutomationVisualFactory.CreateFolderToken(
                _root, label, new Color(0.68f, 0.22f, 0.18f));
            token.transform.position = new Vector3(13f, 0.42f, -3.2f);
            var view = token.AddComponent<AutomationDossierView>();
            view.MarkAppeal(appeal.OriginatingRulingId);
            var item = new AutomationFlowItem(
                _activeInstitution,
                appeal,
                AutomationClaimProfile.ForAppeal(appeal),
                token,
                view,
                label);
            ConfigureItemProcedures(item);
            _items.Add(item);
            _legal.Enqueue(item);
            Emit(AutomationFeedbackKind.AppealReturned,
                label + " RETURNED THROUGH LEGAL");
        }

        private void ResolveAppeal(AutomationFlowItem item)
        {
            if (item == null || !item.IsAppeal || item.Institution == null)
                throw new InvalidOperationException(
                    "A returned appeal requires its continuing institution.");
            AutomationAppealProcedure procedure = AppealMode switch
            {
                AutomationAppealMode.FastTrack =>
                    AutomationAppealProcedure.FastTrack,
                AutomationAppealMode.Settlement =>
                    AutomationAppealProcedure.Settlement,
                _ => AutomationAppealProcedure.FullRehearing,
            };
            AutomationAppealResolutionResult resolution =
                item.Institution.ResolveAppeal(
                    item.Appeal,
                    procedure,
                    establishHolding:
                        Policy == AutomationPolicyKind.AppealRefinery ||
                        IsProcedureBound(
                            AutomationProcedureKind.PrecedentReuse));
            item.ApplyAppealResolution(resolution);
            PrecedentsInstalled = item.Institution.HoldingCount;
            ApplyPolicyTuning();
        }

        private IReadOnlyList<AutomationInstitutionalProcedure>
            InstitutionalProcedures()
        {
            var result = new List<AutomationInstitutionalProcedure>();
            foreach (AutomationProcedureKind procedure in _procedureTiers.Keys)
            {
                result.Add(procedure switch
                {
                    AutomationProcedureKind.MandatorySecondaryVerification =>
                        AutomationInstitutionalProcedure.MandatorySecondaryVerification,
                    AutomationProcedureKind.PresumptionOfValidity =>
                        AutomationInstitutionalProcedure.PresumptionOfValidity,
                    AutomationProcedureKind.AutomaticAdverseReview =>
                        AutomationInstitutionalProcedure.AutomaticAdverseReview,
                    AutomationProcedureKind.ProtectedEvidenceChannel =>
                        AutomationInstitutionalProcedure.ProtectedEvidenceChannel,
                    AutomationProcedureKind.AppealFastTrack =>
                        AutomationInstitutionalProcedure.AppealFastTrack,
                    AutomationProcedureKind.PrecedentReuse =>
                        AutomationInstitutionalProcedure.PrecedentReuse,
                    _ => throw new ArgumentOutOfRangeException(),
                });
            }
            result.Sort();
            return result;
        }

        private bool NeedsLegalHold(AutomationFlowItem item)
        {
            if (item?.Claim == null || item.AdverseReviewCompleted) return false;
            int requiredPasses = IsProcedureBound(
                AutomationProcedureKind.MandatorySecondaryVerification) ? 2 : 1;
            return item.VerificationPasses < requiredPasses ||
                   item.Claim.CitableEvidenceCount == 0 ||
                   item.Claim.EvidenceSupportMinimum < 52;
        }

        private PlayerRulingDisposition SelectAutomaticDisposition(
            AutomationFlowItem item)
        {
            AutomationPublicClaim claim = item?.Claim ??
                throw new InvalidOperationException(
                    "Only a public claim envelope can receive an initial ruling.");
            int presumptionBonus = IsProcedureBound(
                AutomationProcedureKind.PresumptionOfValidity) ? 15 : 0;
            bool recognise = Policy switch
            {
                AutomationPolicyKind.ProofFortress =>
                    claim.CitableEvidenceCount > 0 &&
                    claim.EvidenceSupportMinimum + presumptionBonus >= 52,
                AutomationPolicyKind.RubberStampMill =>
                    claim.EvidenceSupportMaximum + presumptionBonus >= 45,
                AutomationPolicyKind.AppealRefinery =>
                    claim.EvidenceSupportMaximum + presumptionBonus >= 65,
                _ => false,
            };
            return recognise
                ? PlayerRulingDisposition.Recognised
                : PlayerRulingDisposition.Denied;
        }

        private void ApplyPolicyTuning()
        {
            float verificationMultiplier;
            float legalMultiplier;
            float reliabilityModifier;
            float heatModifier;
            switch (Policy)
            {
                case AutomationPolicyKind.ProofFortress:
                    verificationMultiplier = 1.22f;
                    legalMultiplier = 1f;
                    reliabilityModifier = 0.55f;
                    heatModifier = 0.78f;
                    break;
                case AutomationPolicyKind.RubberStampMill:
                    verificationMultiplier = 0.88f;
                    legalMultiplier = 1.18f;
                    reliabilityModifier = 1.35f;
                    heatModifier = 1.35f;
                    break;
                case AutomationPolicyKind.AppealRefinery:
                    verificationMultiplier = Mathf.Max(
                        0.38f, 0.76f - PrecedentsInstalled * 0.08f);
                    legalMultiplier = 0.36f;
                    reliabilityModifier = 0.92f;
                    heatModifier = 1.05f;
                    break;
                default:
                    verificationMultiplier = 1f;
                    legalMultiplier = 1f;
                    reliabilityModifier = 1f;
                    heatModifier = 1f;
                    break;
            }
            if (AppealMode == AutomationAppealMode.FastTrack)
                legalMultiplier *= 0.56f;
            else if (AppealMode == AutomationAppealMode.Settlement)
                legalMultiplier *= 0.38f;
            int precedentTier = ProcedureTier(
                AutomationProcedureKind.PrecedentReuse);
            if (precedentTier > 0)
                verificationMultiplier *= Mathf.Max(
                    0.48f, 1f - PrecedentsInstalled * 0.055f);
            if (precedentTier >= 2)
                verificationMultiplier *= Mathf.Max(
                    0.62f, 1f - PrecedentsInstalled * 0.035f);
            if (precedentTier >= 3 && PrecedentsInstalled >= 2)
            {
                verificationMultiplier *= 0.78f;
                reliabilityModifier *= 1.16f;
                heatModifier *= 1.14f;
            }
            if (ProcedureTier(
                    AutomationProcedureKind.PresumptionOfValidity) >= 3)
            {
                reliabilityModifier *= 1.12f;
                heatModifier *= 1.18f;
            }
            for (int i = 0; i < _stations.Count; i++)
            {
                AutomationStationRuntime station = _stations[i];
                float duration = station.Kind == AutomationStationKind.Verification
                    ? verificationMultiplier
                    : station.Kind == AutomationStationKind.Legal
                        ? legalMultiplier
                        : 1f;
                float stationHeat = heatModifier;
                if (station.Kind == AutomationStationKind.Legal &&
                    ProcedureTier(
                        AutomationProcedureKind.AppealFastTrack) >= 2)
                    stationHeat *= 0.68f;
                station.SetPolicyModifiers(
                    duration, reliabilityModifier, stationHeat);
            }
        }

        private void Emit(AutomationFeedbackKind kind, string message)
        {
            Feedback?.Invoke(kind, message ?? string.Empty);
        }

        private AutomationStationRuntime SelectVerifier(AutomationFlowItem item)
        {
            if (item != null && _auxVerifier != null &&
                IsProcedureBound(AutomationProcedureKind.ProtectedEvidenceChannel) &&
                item.NeedsProtectedChannel)
                return _auxVerifier;
            if (!ParallelRouting || _auxVerifier == null) return _primaryVerifier;
            _routeOrdinal++;
            if (_routeOrdinal % 2 == 0) return _auxVerifier;
            return _primaryVerifier.Workload <= _auxVerifier.Workload
                ? _primaryVerifier
                : _auxVerifier;
        }

        private void ConfigureItemProcedures(AutomationFlowItem item)
        {
            if (item == null) return;
            item.ConfigureProcedures(
                IsProcedureBound(AutomationProcedureKind.PresumptionOfValidity),
                IsProcedureBound(AutomationProcedureKind.ProtectedEvidenceChannel),
                ProcedureTier(AutomationProcedureKind.PresumptionOfValidity),
                ProcedureTier(AutomationProcedureKind.ProtectedEvidenceChannel),
                _verificationPatterns.Contains(item.Profile.IssueFamily),
                _adverseReviewPatterns.Contains(item.Profile.IssueFamily));
        }

        private void HandleStationJammed(AutomationStationRuntime station)
        {
            JamCount++;
            Emit(AutomationFeedbackKind.Jammed,
                station.DisplayName.ToUpperInvariant() +
                " JAMMED / SELECT + REPAIR");
        }

        private void HandleStationMisclassified(
            AutomationStationRuntime station,
            AutomationFlowItem item)
        {
            Emit(AutomationFeedbackKind.Misclassified,
                station.DisplayName.ToUpperInvariant() + " MISCLASSIFIED " +
                item.ClaimId);
        }

        private void RecordCompletedFamily(AutomationPublicClaim claim)
        {
            string issue = claim?.Issue ?? string.Empty;
            if (issue.IndexOf(
                    "Collective", StringComparison.OrdinalIgnoreCase) >= 0)
                CollectiveCompleted++;
            else if (issue.IndexOf(
                         "Access", StringComparison.OrdinalIgnoreCase) >= 0)
                AccessCompleted++;
            else
                PossessionCompleted++;
        }
    }

    internal sealed class AutomationStationRuntime
    {
        private readonly List<AutomationFlowItem> _queue = new();
        private readonly Renderer _machineLight;
        private readonly Renderer _selectionPlinth;
        private readonly TextMesh _queueLabel;
        private readonly Color _idleColour = new(0.83f, 0.58f, 0.17f);
        private AutomationFlowItem _active;
        private float _remaining;
        private float _durationMultiplier = 1f;
        private float _reliabilityModifier = 1f;
        private float _heatModifier = 1f;
        private AutomationRoutePriority _routePriority =
            AutomationRoutePriority.Balanced;
        private int _throughputLevel;
        private int _capacityLevel;
        private int _reliabilityLevel;
        private bool _isJammed;
        private float _heat;
        private float _visualRefreshClock;

        internal AutomationStationRuntime(
            AutomationStationKind kind,
            string displayName,
            Vector3 position,
            float processDuration,
            bool isAuxiliary,
            Renderer machineLight,
            Renderer selectionPlinth,
            TextMesh queueLabel)
        {
            Kind = kind;
            DisplayName = displayName;
            Position = position;
            ProcessDuration = Mathf.Max(0.1f, processDuration);
            IsAuxiliary = isAuxiliary;
            _machineLight = machineLight;
            _selectionPlinth = selectionPlinth;
            _queueLabel = queueLabel;
            RefreshVisualState();
        }

        internal event Action<AutomationStationRuntime, AutomationFlowItem> Completed;
        internal event Action<AutomationStationRuntime> Jammed;
        internal event Action<AutomationStationRuntime, AutomationFlowItem> Misclassified;

        internal AutomationStationKind Kind { get; }
        internal string DisplayName { get; }
        internal Vector3 Position { get; }
        internal float ProcessDuration { get; }
        internal bool IsAuxiliary { get; }
        internal int Workload => _queue.Count + (_active != null ? 1 : 0);
        internal bool IsJammed => _isJammed;
        internal float Heat => _heat;
        internal int TotalUpgradeLevel =>
            _throughputLevel + _capacityLevel + _reliabilityLevel;
        internal int UpgradeCost => 2 + TotalUpgradeLevel;
        internal int SafeWorkload => 2 + _capacityLevel * 2;
        internal string UpgradeSummary => "SPD " + _throughputLevel +
            " / CAP " + _capacityLevel + " / REL " + _reliabilityLevel;

        internal void SetPolicyModifiers(
            float durationMultiplier,
            float reliabilityModifier,
            float heatModifier)
        {
            _durationMultiplier = Mathf.Clamp(durationMultiplier, 0.25f, 3f);
            _reliabilityModifier = Mathf.Clamp(reliabilityModifier, 0.25f, 2.5f);
            _heatModifier = Mathf.Clamp(heatModifier, 0.25f, 2.5f);
        }

        internal void SetRoutePriority(AutomationRoutePriority priority)
        {
            _routePriority = priority;
            SortQueue();
        }

        internal int UpgradeLevel(AutomationUpgradeKind kind)
        {
            return kind switch
            {
                AutomationUpgradeKind.Throughput => _throughputLevel,
                AutomationUpgradeKind.Capacity => _capacityLevel,
                AutomationUpgradeKind.Reliability => _reliabilityLevel,
                _ => 0,
            };
        }

        internal bool TryUpgrade(AutomationUpgradeKind kind)
        {
            switch (kind)
            {
                case AutomationUpgradeKind.Throughput when _throughputLevel < 3:
                    _throughputLevel++;
                    break;
                case AutomationUpgradeKind.Capacity when _capacityLevel < 3:
                    _capacityLevel++;
                    break;
                case AutomationUpgradeKind.Reliability when _reliabilityLevel < 3:
                    _reliabilityLevel++;
                    break;
                default:
                    return false;
            }
            RefreshVisualState();
            return true;
        }

        internal bool Repair()
        {
            if (!_isJammed) return false;
            _isJammed = false;
            _heat = 25f;
            RefreshVisualState();
            return true;
        }

#if UNITY_INCLUDE_TESTS
        internal bool CreateValidationJam()
        {
            if (_isJammed) return false;
            _heat = 100f;
            _isJammed = true;
            RefreshVisualState();
            Jammed?.Invoke(this);
            return true;
        }
#endif

        internal AutomationStationCheckpoint CaptureCheckpoint()
        {
            var checkpoint = new AutomationStationCheckpoint
            {
                Kind = Kind,
                IsAuxiliary = IsAuxiliary,
                ThroughputLevel = _throughputLevel,
                CapacityLevel = _capacityLevel,
                ReliabilityLevel = _reliabilityLevel,
                IsJammed = _isJammed,
                Heat = _heat,
                Remaining = _remaining,
                ActiveItemId = _active?.FlowItemId ?? string.Empty,
            };
            for (int i = 0; i < _queue.Count; i++)
                checkpoint.QueuedItemIds.Add(_queue[i].FlowItemId);
            return checkpoint;
        }

        internal void ResetRuntime()
        {
            _queue.Clear();
            _active = null;
            _remaining = 0f;
            _isJammed = false;
            _heat = 0f;
            RefreshVisualState();
        }

        internal void RestoreCheckpoint(
            AutomationStationCheckpoint checkpoint,
            IReadOnlyDictionary<string, AutomationFlowItem> itemById)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (checkpoint.Kind != Kind || checkpoint.IsAuxiliary != IsAuxiliary ||
                checkpoint.ThroughputLevel < 0 || checkpoint.ThroughputLevel > 3 ||
                checkpoint.CapacityLevel < 0 || checkpoint.CapacityLevel > 3 ||
                checkpoint.ReliabilityLevel < 0 || checkpoint.ReliabilityLevel > 3 ||
                checkpoint.Heat < 0f || checkpoint.Heat > 110f ||
                checkpoint.QueuedItemIds == null)
            {
                throw new InvalidOperationException(
                    "Station checkpoint is incompatible with the floor.");
            }
            ResetRuntime();
            _throughputLevel = checkpoint.ThroughputLevel;
            _capacityLevel = checkpoint.CapacityLevel;
            _reliabilityLevel = checkpoint.ReliabilityLevel;
            _heat = checkpoint.Heat;
            _isJammed = checkpoint.IsJammed;
            _remaining = Mathf.Max(0f, checkpoint.Remaining);
            if (!string.IsNullOrWhiteSpace(checkpoint.ActiveItemId))
            {
                if (!itemById.TryGetValue(
                        checkpoint.ActiveItemId, out _active))
                    throw new InvalidOperationException(
                        "Station checkpoint lost its active dossier.");
                _active.BeginProcessing(WorktopPosition());
            }
            for (int i = 0; i < checkpoint.QueuedItemIds.Count; i++)
            {
                if (!itemById.TryGetValue(
                        checkpoint.QueuedItemIds[i], out AutomationFlowItem item))
                    throw new InvalidOperationException(
                        "Station checkpoint lost a queued dossier.");
                _queue.Add(item);
                item.BeginTransit(this);
            }
            RefreshVisualState();
        }

        internal void SetSelected(bool selected)
        {
            if (_selectionPlinth != null)
                _selectionPlinth.material.color = selected
                    ? new Color(0.92f, 0.61f, 0.16f)
                    : new Color(0.16f, 0.18f, 0.17f);
        }

        internal void Enqueue(AutomationFlowItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            _queue.Add(item);
            SortQueue();
            item.BeginTransit(this);
            RefreshVisualState();
        }

        internal void Tick(float deltaTime)
        {
            float overload = Mathf.Max(0, Workload - SafeWorkload);
            if (_active != null)
                _heat = Mathf.Min(110f, _heat + deltaTime *
                    (1.4f + overload * 2.8f) * _heatModifier *
                    (1f + _throughputLevel * 0.13f));
            else
                _heat = Mathf.Max(0f, _heat - deltaTime *
                    (5f + _capacityLevel * 2.2f));

            if (!_isJammed && _heat >= 100f)
            {
                _isJammed = true;
                RefreshVisualState();
                Jammed?.Invoke(this);
            }

            _visualRefreshClock -= deltaTime;
            if (_visualRefreshClock <= 0f)
            {
                _visualRefreshClock = 0.25f;
                RefreshVisualState();
            }

            for (int i = 0; i < _queue.Count; i++)
                _queue[i].MoveTowards(QueuePosition(i), deltaTime, 3.6f);

            if (_isJammed) return;

            if (_active == null && _queue.Count > 0 && _queue[0].AtTarget)
            {
                _active = _queue[0];
                _queue.RemoveAt(0);
                _active.BeginProcessing(WorktopPosition());
                _remaining = ActiveDuration(_active);
                RefreshVisualState();
            }

            if (_active == null) return;
            _active.MoveTowards(WorktopPosition(), deltaTime, 5f);
            if (!_active.AtTarget) return;
            _remaining -= deltaTime;
            float duration = ActiveDuration(_active);
            _active.SetProcessingPulse(1f - Mathf.Clamp01(_remaining / duration));
            if (_remaining > 0f) return;

            AutomationFlowItem completed = _active;
            _active = null;
            if (ShouldMisclassify(completed))
            {
                completed.MarkMisclassified();
                Misclassified?.Invoke(this, completed);
            }
            completed.EndProcessing();
            RefreshVisualState();
            Completed?.Invoke(this, completed);
        }

        private Vector3 QueuePosition(int index)
        {
            float laneDirection = IsAuxiliary ? -1f : 1f;
            return Position + new Vector3(0f, 0.28f,
                laneDirection * (1.45f + index * 0.56f));
        }

        private Vector3 WorktopPosition()
        {
            return Position + new Vector3(0f, 1.72f, 0f);
        }

        private void RefreshVisualState()
        {
            if (_queueLabel != null)
                _queueLabel.text = _isJammed
                    ? "JAM"
                    : "Q " + Workload.ToString("00") + " H" +
                      Mathf.RoundToInt(_heat).ToString("00");
            if (_machineLight == null) return;
            Color colour = _isJammed
                ? new Color(0.95f, 0.08f, 0.05f)
                : _active != null
                ? new Color(0.43f, 0.78f, 0.38f)
                : _idleColour;
            if (!_isJammed && (Workload > SafeWorkload || _heat >= 72f))
                colour = new Color(0.88f, 0.24f, 0.18f);
            _machineLight.material.color = colour;
        }

        private float ActiveDuration(AutomationFlowItem item)
        {
            float speed = 1f - _throughputLevel * 0.14f;
            return Mathf.Max(0.18f, ProcessDuration * _durationMultiplier * speed *
                (1f + _reliabilityLevel * 0.055f) *
                item.WorkMultiplier(Kind));
        }

        private bool ShouldMisclassify(AutomationFlowItem item)
        {
            if (Kind != AutomationStationKind.Verification || item == null) return false;
            float overload = Mathf.Max(0, Workload - SafeWorkload);
            float chance = (0.025f + overload * 0.055f +
                Mathf.Clamp01(_heat / 100f) * 0.11f) * _reliabilityModifier -
                _reliabilityLevel * 0.045f;
            chance += _throughputLevel * 0.018f;
            chance *= item.MisclassificationRiskMultiplier;
            if (chance <= 0f) return false;
            uint hash = 2166136261;
            string key = item.ClaimId + ":" + item.ReworkAttempts + ":" + DisplayName;
            for (int i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619;
            }
            float roll = (hash % 10000) / 10000f;
            return roll < Mathf.Clamp(chance, 0f, 0.72f);
        }

        private void SortQueue()
        {
            if (_routePriority == AutomationRoutePriority.Balanced) return;
            _queue.Sort((left, right) =>
            {
                int compared = _routePriority == AutomationRoutePriority.UrgentFirst
                    ? right.IsUrgent.CompareTo(left.IsUrgent)
                    : left.TimeRemaining.CompareTo(right.TimeRemaining);
                if (compared != 0) return compared;
                return left.Sequence.CompareTo(right.Sequence);
            });
        }
    }

    internal sealed class AutomationFlowItem : IDisposable
    {
        private static int _nextSequence;
        private readonly GameObject _root;
        private readonly AutomationDossierView _view;
        private bool _misclassified;
        private bool _deadlineReported;
        private bool _presumptionOfValidity;
        private bool _protectedEvidenceChannel;
        private bool _reusableVerificationPattern;
        private bool _repeatedAdversePattern;
        private int _presumptionTier;
        private int _protectedChannelTier;
        private bool _adverseReviewCompleted;
        private bool _evidenceRevealed;
        private float _age;

        internal AutomationFlowItem(
            InstitutionalAutomationSession institution,
            AutomationPublicClaim claim,
            AutomationClaimProfile profile,
            GameObject root,
            AutomationDossierView view,
            string displayId)
        {
            Institution = institution ?? throw new ArgumentNullException(nameof(institution));
            Claim = claim ?? throw new ArgumentNullException(nameof(claim));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _root = root;
            _view = view;
            DisplayId = displayId ?? claim.DisplayId;
            Sequence = ++_nextSequence;
            _view.ConfigureClaim(claim, Profile);
        }

        internal AutomationFlowItem(
            InstitutionalAutomationSession institution,
            AutomationAppealPacket appeal,
            AutomationClaimProfile profile,
            GameObject root,
            AutomationDossierView view,
            string displayId)
        {
            Institution = institution ?? throw new ArgumentNullException(nameof(institution));
            Appeal = appeal ?? throw new ArgumentNullException(nameof(appeal));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _root = root;
            _view = view;
            DisplayId = displayId ?? appeal.AppealId;
            Sequence = ++_nextSequence;
            _view.ConfigureProfile(Profile);
        }

        internal InstitutionalAutomationSession Institution { get; }
        internal AutomationPublicClaim Claim { get; }
        internal AutomationAppealPacket Appeal { get; }
        internal AutomationClaimProfile Profile { get; }
        internal AutomationRulingResult Ruling { get; private set; }
        internal AutomationAppealResolutionResult AppealResolution { get; private set; }
        internal bool IsAppeal => Appeal != null;
        internal string DisplayId { get; }
        internal string FlowItemId => DisplayId;
        internal string ClaimId => DisplayId;
        internal bool AtTarget => _view.AtTarget;
        internal bool IsUrgent => Profile.Urgency == AutomationUrgency.Urgent;
        internal bool IsOverdue => _age >= Profile.DeadlineSeconds;
        internal float TimeRemaining => Profile.DeadlineSeconds - _age;
        internal int ReworkAttempts { get; private set; }
        internal int Sequence { get; private set; }
        internal int VerificationPasses { get; private set; }
        internal bool AdverseReviewPending { get; private set; }
        internal bool AdverseReviewCompleted => _adverseReviewCompleted;
        internal bool NeedsProtectedChannel =>
            (Profile.EvidenceNeeds & AutomationEvidenceNeed.ChainOfCustody) != 0;
        internal float MisclassificationRiskMultiplier =>
            (_presumptionOfValidity
                ? _presumptionTier >= 3 ? 1.90f : 1.65f
                : 1f) *
            (_protectedEvidenceChannel && NeedsProtectedChannel
                ? _protectedChannelTier >= 2 ? 0.35f : 0.52f
                : 1f);

        internal void ConfigureProcedures(
            bool presumptionOfValidity,
            bool protectedEvidenceChannel,
            int presumptionTier,
            int protectedChannelTier,
            bool reusableVerificationPattern,
            bool repeatedAdversePattern)
        {
            _presumptionOfValidity = presumptionOfValidity;
            _protectedEvidenceChannel = protectedEvidenceChannel;
            _presumptionTier = presumptionTier;
            _protectedChannelTier = protectedChannelTier;
            _reusableVerificationPattern = reusableVerificationPattern;
            _repeatedAdversePattern = repeatedAdversePattern;
        }

        internal void RecordVerificationPass()
        {
            VerificationPasses++;
        }

        internal void GrantDeadlineGrace(float seconds)
        {
            _age = Mathf.Max(0f, _age - Mathf.Max(0f, seconds));
        }

        internal void PauseDeadline(float seconds)
        {
            GrantDeadlineGrace(seconds);
        }

        internal bool BeginAdverseReview()
        {
            if (_adverseReviewCompleted || AdverseReviewPending) return false;
            AdverseReviewPending = true;
            _view.MarkAdverseReview();
            return true;
        }

        internal void CompleteAdverseReview()
        {
            AdverseReviewPending = false;
            _adverseReviewCompleted = true;
        }

        internal bool TickAge(float deltaTime)
        {
            _age += deltaTime;
            _view.SetDeadlineProgress(_age / Profile.DeadlineSeconds, IsOverdue);
            if (!IsOverdue || _deadlineReported) return false;
            _deadlineReported = true;
            return true;
        }

        internal float WorkMultiplier(AutomationStationKind kind)
        {
            float value = kind switch
            {
                AutomationStationKind.EvidenceSplit =>
                    0.80f + Profile.EvidenceNeedCount * 0.12f,
                AutomationStationKind.Verification => Profile.VerificationWork,
                AutomationStationKind.Legal when IsAppeal => 1.15f,
                _ => 1f,
            };
            if (kind == AutomationStationKind.Verification &&
                _presumptionOfValidity) value *= 0.66f;
            if (kind == AutomationStationKind.Verification &&
                _presumptionTier >= 2 && !IsUrgent) value *= 0.78f;
            if (kind == AutomationStationKind.Verification &&
                _reusableVerificationPattern) value *= 0.76f;
            if (kind == AutomationStationKind.Verification &&
                _protectedEvidenceChannel && NeedsProtectedChannel) value *= 0.78f;
            if (kind == AutomationStationKind.Legal &&
                _repeatedAdversePattern) value *= 0.72f;
            return value;
        }

        internal void MarkMisclassified()
        {
            _misclassified = true;
        }

        internal bool ConsumeMisclassificationForRework()
        {
            if (!_misclassified || ReworkAttempts >= 2) return false;
            _misclassified = false;
            ReworkAttempts++;
            _view.MarkRework(ReworkAttempts);
            return true;
        }

        internal void RevealEvidencePacket()
        {
            _evidenceRevealed = true;
            _view.RevealEvidencePacket(
                Claim?.OfficialFactCount ?? 0,
                Claim?.AllegationCount ?? 0,
                Claim?.MissingEvidenceCount ?? Appeal?.MissingEvidenceCount ?? 0);
        }

        internal void ApplyRuling(AutomationRulingResult result)
        {
            Ruling = result ?? throw new ArgumentNullException(nameof(result));
            _view.ApplyRuling(result.Disposition, result.CitedHoldingCount);
        }

        internal void ApplyAppealResolution(
            AutomationAppealResolutionResult resolution)
        {
            AppealResolution = resolution ??
                throw new ArgumentNullException(nameof(resolution));
            _view.ApplyAppealResolution(
                resolution.Disposition,
                resolution.EstablishedHolding);
        }

        internal void BeginTransit(AutomationStationRuntime station)
        {
            _view.SetStage(station.DisplayName, false);
        }

        internal void BeginProcessing(Vector3 target)
        {
            _view.SetStage("PROCESSING", true);
            _view.SetTarget(target);
        }

        internal void MoveTowards(Vector3 target, float deltaTime, float speed)
        {
            _view.SetTarget(target);
            _view.TickMovement(deltaTime, speed);
        }

        internal void SetProcessingPulse(float progress)
        {
            _view.SetProcessingPulse(progress);
        }

        internal void EndProcessing()
        {
            _view.SetProcessingPulse(0f);
        }

        internal AutomationFlowItemCheckpoint CaptureCheckpoint()
        {
            return new AutomationFlowItemCheckpoint
            {
                FlowItemId = FlowItemId,
                IsAppeal = IsAppeal,
                AutomationClaimId = Claim?.AutomationClaimId ?? string.Empty,
                AppealId = Appeal?.AppealId ?? string.Empty,
                DisplayId = DisplayId,
                Sequence = Sequence,
                Age = _age,
                DeadlineReported = _deadlineReported,
                Misclassified = _misclassified,
                ReworkAttempts = ReworkAttempts,
                VerificationPasses = VerificationPasses,
                AdverseReviewPending = AdverseReviewPending,
                AdverseReviewCompleted = _adverseReviewCompleted,
                PresumptionOfValidity = _presumptionOfValidity,
                ProtectedEvidenceChannel = _protectedEvidenceChannel,
                PresumptionTier = _presumptionTier,
                ProtectedChannelTier = _protectedChannelTier,
                RulingApplied = Ruling != null,
                AppealResolutionApplied = AppealResolution != null,
                EvidenceRevealed = _evidenceRevealed,
            };
        }

        internal void RestoreCheckpoint(
            AutomationFlowItemCheckpoint checkpoint,
            AutomationRulingResult ruling,
            AutomationAppealResolutionResult appealResolution)
        {
            if (checkpoint == null || checkpoint.FlowItemId != FlowItemId ||
                checkpoint.Sequence < 1 || checkpoint.Age < 0f ||
                checkpoint.ReworkAttempts < 0 ||
                checkpoint.VerificationPasses < 0)
                throw new InvalidOperationException(
                    "Flow item checkpoint is incompatible with its dossier.");
            Sequence = checkpoint.Sequence;
            _nextSequence = Mathf.Max(_nextSequence, Sequence);
            _age = checkpoint.Age;
            _deadlineReported = checkpoint.DeadlineReported;
            _misclassified = checkpoint.Misclassified;
            ReworkAttempts = checkpoint.ReworkAttempts;
            VerificationPasses = checkpoint.VerificationPasses;
            AdverseReviewPending = checkpoint.AdverseReviewPending;
            _adverseReviewCompleted = checkpoint.AdverseReviewCompleted;
            _presumptionOfValidity = checkpoint.PresumptionOfValidity;
            _protectedEvidenceChannel = checkpoint.ProtectedEvidenceChannel;
            _presumptionTier = checkpoint.PresumptionTier;
            _protectedChannelTier = checkpoint.ProtectedChannelTier;
            _view.SetDeadlineProgress(
                _age / Profile.DeadlineSeconds, IsOverdue);
            if (checkpoint.EvidenceRevealed) RevealEvidencePacket();
            for (int attempt = 1; attempt <= ReworkAttempts; attempt++)
                _view.MarkRework(attempt);
            if (AdverseReviewPending || _adverseReviewCompleted)
                _view.MarkAdverseReview();
            if (checkpoint.RulingApplied)
            {
                Ruling = ruling ?? throw new InvalidOperationException(
                    "Committed flow item lost its institutional ruling.");
                _view.ApplyRuling(
                    Ruling.Disposition, Ruling.CitedHoldingCount);
            }
            if (checkpoint.AppealResolutionApplied)
            {
                AppealResolution = appealResolution ??
                    throw new InvalidOperationException(
                        "Resolved appeal flow item lost its appellate ruling.");
                _view.ApplyAppealResolution(
                    AppealResolution.Disposition,
                    AppealResolution.EstablishedHolding);
            }
        }

        public void Dispose()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }
    }

    internal sealed class AutomationDossierView : MonoBehaviour
    {
        private Vector3 _target;
        private Vector3 _baseScale;
        private TextMesh _label;
        private bool _processing;
        private bool _evidenceVisible;
        private bool _rulingVisible;
        private bool _appealVisible;
        private Renderer _deadlineFill;
        private Renderer _urgencyTab;

        internal bool AtTarget => (transform.position - _target).sqrMagnitude < 0.003f;

        private void Awake()
        {
            _target = transform.position;
            _baseScale = transform.localScale;
            _label = GetComponentInChildren<TextMesh>();
            _urgencyTab = transform.Find("Urgency Tab")?.GetComponent<Renderer>();
        }

        internal void ConfigureProfile(AutomationClaimProfile profile)
        {
            if (profile == null) return;
            if (_urgencyTab != null)
                _urgencyTab.material.color = profile.Urgency == AutomationUrgency.Urgent
                    ? new Color(0.95f, 0.18f, 0.12f)
                    : new Color(0.34f, 0.62f, 0.45f);

            AutomationVisualFactory.CreateBlock(transform, "Deadline Track",
                new Vector3(0f, 0.20f, 0.51f),
                new Vector3(0.66f, 0.06f, 0.08f),
                new Color(0.12f, 0.14f, 0.13f));
            GameObject fill = AutomationVisualFactory.CreateBlock(transform, "Deadline Fill",
                new Vector3(-0.30f, 0.24f, 0.51f),
                new Vector3(0.06f, 0.05f, 0.07f),
                profile.Urgency == AutomationUrgency.Urgent
                    ? new Color(0.95f, 0.34f, 0.15f)
                    : new Color(0.31f, 0.72f, 0.52f));
            _deadlineFill = fill.GetComponent<Renderer>();

            AutomationEvidenceNeed[] needs =
            {
                AutomationEvidenceNeed.Identity,
                AutomationEvidenceNeed.OfficialRecord,
                AutomationEvidenceNeed.Witness,
                AutomationEvidenceNeed.ChainOfCustody,
            };
            Color[] colours =
            {
                new(0.33f, 0.67f, 0.82f),
                new(0.35f, 0.72f, 0.57f),
                new(0.84f, 0.58f, 0.20f),
                new(0.67f, 0.38f, 0.72f),
            };
            int visible = 0;
            for (int i = 0; i < needs.Length; i++)
            {
                if ((profile.EvidenceNeeds & needs[i]) == 0) continue;
                AutomationVisualFactory.CreateBlock(transform,
                    "Verification Need " + needs[i],
                    new Vector3(-0.27f + visible * 0.18f, 0.20f, -0.49f),
                    new Vector3(0.13f, 0.07f, 0.12f), colours[i]);
                visible++;
            }
        }

        internal void ConfigureClaim(
            AutomationPublicClaim claim,
            AutomationClaimProfile profile)
        {
            ConfigureProfile(profile);
            if (claim == null || profile == null) return;
            Color issueColour = claim.Issue.IndexOf(
                "Collective", StringComparison.OrdinalIgnoreCase) >= 0
                ? new Color(0.82f, 0.30f, 0.70f)
                : claim.Issue.IndexOf(
                    "Access", StringComparison.OrdinalIgnoreCase) >= 0
                    ? new Color(0.24f, 0.64f, 0.90f)
                    : new Color(0.88f, 0.66f, 0.24f);
            AutomationVisualFactory.CreateBlock(transform, "Issue Family Band",
                new Vector3(0f, 0.22f, 0.42f),
                new Vector3(0.72f, 0.055f, 0.08f), issueColour);
            int linked = Mathf.Min(4, profile.LinkedDossierCount);
            for (int i = 1; i < linked; i++)
                AutomationVisualFactory.CreateBlock(transform,
                    "Linked Dossier " + i,
                    new Vector3(-0.28f + i * 0.18f, 0.10f + i * 0.025f, 0.04f),
                    new Vector3(0.13f, 0.04f, 0.68f),
                    new Color(issueColour.r * 0.72f,
                        issueColour.g * 0.72f,
                        issueColour.b * 0.72f));
            if (profile.Descendant)
                AutomationVisualFactory.CreateBlock(transform,
                    "Descendant Lineage Marker",
                    new Vector3(0.30f, 0.27f, -0.24f),
                    new Vector3(0.12f, 0.13f, 0.34f),
                    new Color(0.91f, 0.39f, 0.18f));
            if (_label != null)
                _label.text += "\n" + ShortIssue(claim.Issue);
        }

        internal void SetDeadlineProgress(float progress, bool overdue)
        {
            if (_deadlineFill == null) return;
            float width = Mathf.Lerp(0.06f, 0.66f, Mathf.Clamp01(progress));
            Transform fill = _deadlineFill.transform;
            fill.localScale = new Vector3(width, 0.05f, 0.07f);
            fill.localPosition = new Vector3(-0.33f + width * 0.5f,
                0.24f, 0.51f);
            if (overdue) _deadlineFill.material.color =
                new Color(1f, 0.06f, 0.03f);
        }

        internal void SetTarget(Vector3 target)
        {
            _target = target;
        }

        internal void TickMovement(float deltaTime, float speed)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, _target, deltaTime * speed);
            if (!_processing) transform.localScale = _baseScale;
        }

        internal void SetStage(string stage, bool processing)
        {
            _processing = processing;
            if (_label != null) _label.gameObject.SetActive(processing);
            if (_label != null && processing) _label.color = new Color(1f, 0.80f, 0.35f);
            else if (_label != null) _label.color = new Color(0.95f, 0.90f, 0.73f);
        }

        internal void SetProcessingPulse(float progress)
        {
            _processing = progress > 0f;
            float pulse = _processing ? Mathf.Sin(Time.time * 11f) * 0.045f : 0f;
            transform.localScale = _baseScale * (1f + pulse);
        }

        internal void RevealEvidencePacket(
            int officialFacts, int allegations, int missingEvidence)
        {
            if (_evidenceVisible) return;
            _evidenceVisible = true;
            if (officialFacts > 0)
                AutomationVisualFactory.CreateBlock(transform, "Official Record Tab",
                    new Vector3(-0.26f, 0.18f, -0.43f),
                    new Vector3(0.24f, 0.08f, 0.30f),
                    new Color(0.25f, 0.67f, 0.59f));
            if (allegations > 0)
                AutomationVisualFactory.CreateBlock(transform, "Allegation Tab",
                    new Vector3(0f, 0.19f, -0.45f),
                    new Vector3(0.24f, 0.09f, 0.34f),
                    new Color(0.86f, 0.57f, 0.17f));
            if (missingEvidence > 0)
                AutomationVisualFactory.CreateBlock(transform, "Missing Evidence Tab",
                    new Vector3(0.27f, 0.18f, -0.42f),
                    new Vector3(0.24f, 0.08f, 0.28f),
                    new Color(0.62f, 0.21f, 0.19f));
        }

        internal void ApplyRuling(string disposition, int citedHoldingCount)
        {
            if (_rulingVisible) return;
            _rulingVisible = true;
            bool recognised = disposition.IndexOf(
                "recogn", StringComparison.OrdinalIgnoreCase) >= 0;
            AutomationVisualFactory.CreateBlock(transform, "Ruling Stamp",
                new Vector3(0f, 0.25f, 0.04f),
                new Vector3(0.46f, 0.08f, 0.46f),
                recognised
                    ? new Color(0.35f, 0.68f, 0.35f)
                    : new Color(0.72f, 0.22f, 0.18f));
            if (_label != null) _label.color = recognised
                ? new Color(0.55f, 0.88f, 0.48f)
                : new Color(0.95f, 0.43f, 0.34f);
            if (citedHoldingCount > 0)
            {
                AutomationVisualFactory.CreateBlock(transform,
                    "Precedent Citation Pulse",
                    new Vector3(0f, 0.34f, 0.32f),
                    new Vector3(0.60f, 0.07f, 0.12f),
                    new Color(1f, 0.77f, 0.16f));
                if (_label != null)
                    _label.text += "\nCITE x" + citedHoldingCount;
            }
        }

        internal void MarkAppeal(string originatingRulingId)
        {
            if (_appealVisible) return;
            _appealVisible = true;
            AutomationVisualFactory.CreateBlock(transform, "Appeal Band",
                new Vector3(0f, 0.22f, -0.08f),
                new Vector3(0.76f, 0.08f, 0.20f),
                new Color(0.18f, 0.055f, 0.045f));
            if (_label != null) _label.color = new Color(1f, 0.45f, 0.34f);
            if (_label != null && !string.IsNullOrWhiteSpace(originatingRulingId))
                _label.text += "\nFROM " + CompactId(originatingRulingId);
        }

        internal void ApplyAppealResolution(
            string disposition,
            bool establishedHolding)
        {
            if (_rulingVisible) return;
            _rulingVisible = true;
            AutomationVisualFactory.CreateBlock(transform, "Appeal Resolution Seal",
                new Vector3(0f, 0.28f, 0.18f),
                new Vector3(0.50f, 0.09f, 0.50f),
                establishedHolding
                    ? new Color(0.80f, 0.62f, 0.18f)
                    : new Color(0.50f, 0.42f, 0.72f));
            if (_label != null) _label.color = establishedHolding
                ? new Color(1f, 0.82f, 0.30f)
                : new Color(0.74f, 0.66f, 0.96f);
            if (_label != null && !string.IsNullOrWhiteSpace(disposition))
                _label.text += "\n" + disposition.ToUpperInvariant();
        }

        internal void MarkRework(int attempt)
        {
            AutomationVisualFactory.CreateBlock(transform, "Rework Stripe " + attempt,
                new Vector3(0f, 0.27f + attempt * 0.035f, -0.18f),
                new Vector3(0.72f, 0.045f, 0.10f),
                new Color(0.94f, 0.18f, 0.50f));
            if (_label != null) _label.color = new Color(1f, 0.32f, 0.58f);
        }

        internal void MarkAdverseReview()
        {
            AutomationVisualFactory.CreateBlock(transform, "Adverse Review Flag",
                new Vector3(-0.31f, 0.30f, 0.30f),
                new Vector3(0.14f, 0.12f, 0.28f),
                new Color(0.98f, 0.30f, 0.08f));
        }

        private static string ShortIssue(string issue)
        {
            if (string.IsNullOrWhiteSpace(issue)) return "UNCLASSIFIED";
            if (issue.IndexOf("Collective", StringComparison.OrdinalIgnoreCase) >= 0)
                return "COLLECTIVE";
            if (issue.IndexOf("Access", StringComparison.OrdinalIgnoreCase) >= 0)
                return "ACCESS";
            return "POSSESSION";
        }

        private static string CompactId(string value)
        {
            if (value.Length <= 16) return value.ToUpperInvariant();
            return value.Substring(value.Length - 16).ToUpperInvariant();
        }
    }
}
