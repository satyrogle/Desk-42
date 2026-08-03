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
    }

    internal sealed class AutomationFlowRuntime : IDisposable
    {
        private readonly Transform _root;
        private readonly List<AutomationStationRuntime> _stations = new();
        private readonly List<AutomationFlowItem> _items = new();
        private readonly HashSet<AutomationProcedureKind> _procedures = new();
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
        internal int SecondaryChecks { get; private set; }
        internal int Credits { get; private set; } = 5;
        internal int ShiftOrdinal => _shiftOrdinal;
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
        internal int ProceduresBound => _procedures.Count;
        internal const int MaximumProcedures = 2;
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
                "Narrow holdings. Slow verification. No new scope appeals.",
            AutomationPolicyKind.RubberStampMill =>
                "Broad holdings. Fast intake. Appeals become the bottleneck.",
            AutomationPolicyKind.AppealRefinery =>
                "Broad holdings. Fast Legal. Resolved appeals accelerate verification.",
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
            return _procedures.Contains(kind);
        }

        internal bool BindProcedure(AutomationProcedureKind kind)
        {
            if (!Enum.IsDefined(typeof(AutomationProcedureKind), kind) ||
                _procedures.Contains(kind)) return false;
            if (_procedures.Count >= MaximumProcedures)
            {
                Emit(AutomationFeedbackKind.Jammed,
                    "PROCEDURE SLOTS FULL / 2 BINDING");
                return false;
            }
            const int cost = 4;
            if (Credits < cost)
            {
                Emit(AutomationFeedbackKind.Jammed,
                    "PROCEDURE BLOCKED / NEED 4 CREDITS");
                return false;
            }
            Credits -= cost;
            _procedures.Add(kind);
            if (kind == AutomationProcedureKind.AppealFastTrack)
                AppealMode = AutomationAppealMode.FastTrack;
            for (int i = 0; i < _items.Count; i++)
                ConfigureItemProcedures(_items[i]);
            ApplyPolicyTuning();
            Emit(AutomationFeedbackKind.ProcedureBound,
                AutomationProcedureNames.ShortName(kind) + " BOUND / " +
                AutomationProcedureNames.Effect(kind));
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
                AutomationUpgradeKind.Throughput => "+14% CYCLE SPEED",
                AutomationUpgradeKind.Capacity => "+2 SAFE QUEUE / COOLING",
                AutomationUpgradeKind.Reliability => "-4.5% FAULT RISK",
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
            Emit(AutomationFeedbackKind.Repaired,
                station.DisplayName.ToUpperInvariant() + " CLEARED / HEAT 25%");
            return true;
        }

        internal void SetPolicy(AutomationPolicyKind policy)
        {
            if (!Enum.IsDefined(typeof(AutomationPolicyKind), policy))
                throw new ArgumentOutOfRangeException(nameof(policy));
            Policy = policy;
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
            Emit(AutomationFeedbackKind.PolicyChanged, PolicyName + " BOUND");
        }

        internal void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            _elapsed += deltaTime;
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].TickAge(deltaTime))
                {
                    OverdueCount++;
                    Emit(AutomationFeedbackKind.DeadlineMissed,
                        _items[i].ClaimId + " MISSED DEADLINE");
                }
            _spawnClock -= deltaTime;
            if (_spawnClock <= 0f && _intake != null &&
                _shiftOrdinal <= MaximumShifts)
            {
                if (_batchSpawned >= _activeInstitution.Claims.Count)
                {
                    _shiftOrdinal++;
                    if (_shiftOrdinal <= MaximumShifts)
                    {
                        _activeInstitution = InstitutionalAutomationSession.Create(
                            ClaimsPerShift);
                        _batchSpawned = 0;
                        _spawnClock = 8f;
                        Emit(AutomationFeedbackKind.PolicyChanged,
                            "SHIFT " + _shiftOrdinal.ToString("D2") +
                            " DOCKET RELEASED");
                    }
                }
                else
                {
                    SpawnClaim();
                    _spawnClock = _spawnInterval;
                }
            }

            for (int i = 0; i < _stations.Count; i++)
                _stations[i].Tick(deltaTime);
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
            GameObject token = AutomationVisualFactory.CreateFolderToken(
                _root, "S" + _shiftOrdinal + "-" + claim.DisplayId,
                folders[(Spawned - 1) % folders.Length]);
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
                        item.ApplyAppealResolution();
                    }
                    else
                    {
                        AutomationRulingResult ruling = item.Institution.Commit(
                            item.Claim.AutomationClaimId,
                            Policy == AutomationPolicyKind.ProofFortress
                                ? PlayerScopeChoice.Narrow
                                : PlayerScopeChoice.Broad,
                            PlayerRulingDisposition.Recognised);
                        item.ApplyRuling(ruling);
                        Emit(AutomationFeedbackKind.RulingStamped,
                            item.Claim.DisplayId + " / " + PolicyName + " RULING");
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
                        if (Policy == AutomationPolicyKind.AppealRefinery &&
                            AppealMode != AutomationAppealMode.Settlement)
                        {
                            PrecedentsInstalled++;
                            ApplyPolicyTuning();
                        }
                        Emit(AutomationFeedbackKind.AppealResolved,
                            "APPEAL RESOLVED / PRECEDENT " +
                            PrecedentsInstalled.ToString("D2"));
                    }
                    else
                    {
                        Completed++;
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
                            _adjudicator.Enqueue(item);
                        }
                        else
                        {
                            SelectVerifier(item).Enqueue(item);
                        }
                    }
                    else if (AppealMode == AutomationAppealMode.Settlement)
                    {
                        item.ApplyAppealResolution();
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
            view.MarkAppeal();
            var item = new AutomationFlowItem(
                appeal, AutomationClaimProfile.ForAppeal(appeal), token, view, label);
            ConfigureItemProcedures(item);
            _items.Add(item);
            _legal.Enqueue(item);
            Emit(AutomationFeedbackKind.AppealReturned,
                label + " RETURNED THROUGH LEGAL");
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
            if (IsProcedureBound(AutomationProcedureKind.PrecedentReuse))
                verificationMultiplier *= Mathf.Max(
                    0.48f, 1f - PrecedentsInstalled * 0.055f);
            for (int i = 0; i < _stations.Count; i++)
            {
                AutomationStationRuntime station = _stations[i];
                float duration = station.Kind == AutomationStationKind.Verification
                    ? verificationMultiplier
                    : station.Kind == AutomationStationKind.Legal
                        ? legalMultiplier
                        : 1f;
                station.SetPolicyModifiers(
                    duration, reliabilityModifier, heatModifier);
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
                IsProcedureBound(AutomationProcedureKind.ProtectedEvidenceChannel));
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
                    (1.4f + overload * 2.8f) * _heatModifier);
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
                item.WorkMultiplier(Kind));
        }

        private bool ShouldMisclassify(AutomationFlowItem item)
        {
            if (Kind != AutomationStationKind.Verification || item == null) return false;
            float overload = Mathf.Max(0, Workload - SafeWorkload);
            float chance = (0.025f + overload * 0.055f +
                Mathf.Clamp01(_heat / 100f) * 0.11f) * _reliabilityModifier -
                _reliabilityLevel * 0.045f;
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
        private bool _adverseReviewCompleted;
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
            _view.ConfigureProfile(Profile);
        }

        internal AutomationFlowItem(
            AutomationAppealPacket appeal,
            AutomationClaimProfile profile,
            GameObject root,
            AutomationDossierView view,
            string displayId)
        {
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
        internal bool IsAppeal => Appeal != null;
        internal string DisplayId { get; }
        internal string ClaimId => DisplayId;
        internal bool AtTarget => _view.AtTarget;
        internal bool IsUrgent => Profile.Urgency == AutomationUrgency.Urgent;
        internal bool IsOverdue => _age >= Profile.DeadlineSeconds;
        internal float TimeRemaining => Profile.DeadlineSeconds - _age;
        internal int ReworkAttempts { get; private set; }
        internal int Sequence { get; }
        internal int VerificationPasses { get; private set; }
        internal bool AdverseReviewPending { get; private set; }
        internal bool NeedsProtectedChannel =>
            (Profile.EvidenceNeeds & AutomationEvidenceNeed.ChainOfCustody) != 0;
        internal float MisclassificationRiskMultiplier =>
            (_presumptionOfValidity ? 1.65f : 1f) *
            (_protectedEvidenceChannel && NeedsProtectedChannel ? 0.52f : 1f);

        internal void ConfigureProcedures(
            bool presumptionOfValidity,
            bool protectedEvidenceChannel)
        {
            _presumptionOfValidity = presumptionOfValidity;
            _protectedEvidenceChannel = protectedEvidenceChannel;
        }

        internal void RecordVerificationPass()
        {
            VerificationPasses++;
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
                _protectedEvidenceChannel && NeedsProtectedChannel) value *= 0.78f;
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
            _view.RevealEvidencePacket(
                Claim?.OfficialFactCount ?? 0,
                Claim?.AllegationCount ?? 0,
                Claim?.MissingEvidenceCount ?? Appeal?.MissingEvidenceCount ?? 0);
        }

        internal void ApplyRuling(AutomationRulingResult result)
        {
            Ruling = result ?? throw new ArgumentNullException(nameof(result));
            _view.ApplyRuling(result.Disposition);
        }

        internal void ApplyAppealResolution()
        {
            _view.ApplyAppealResolution();
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

        internal void ApplyRuling(string disposition)
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
        }

        internal void MarkAppeal()
        {
            if (_appealVisible) return;
            _appealVisible = true;
            AutomationVisualFactory.CreateBlock(transform, "Appeal Band",
                new Vector3(0f, 0.22f, -0.08f),
                new Vector3(0.76f, 0.08f, 0.20f),
                new Color(0.18f, 0.055f, 0.045f));
            if (_label != null) _label.color = new Color(1f, 0.45f, 0.34f);
        }

        internal void ApplyAppealResolution()
        {
            if (_rulingVisible) return;
            _rulingVisible = true;
            AutomationVisualFactory.CreateBlock(transform, "Appeal Resolution Seal",
                new Vector3(0f, 0.28f, 0.18f),
                new Vector3(0.50f, 0.09f, 0.50f),
                new Color(0.50f, 0.42f, 0.72f));
            if (_label != null) _label.color = new Color(0.74f, 0.66f, 0.96f);
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
    }
}
