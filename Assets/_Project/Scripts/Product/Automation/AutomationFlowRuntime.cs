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
        PolicyChanged,
    }

    internal sealed class AutomationFlowRuntime : IDisposable
    {
        private readonly Transform _root;
        private readonly InstitutionalAutomationSession _institution;
        private readonly List<AutomationStationRuntime> _stations = new();
        private readonly List<AutomationFlowItem> _items = new();
        private AutomationStationRuntime _intake;
        private AutomationStationRuntime _splitter;
        private AutomationStationRuntime _primaryVerifier;
        private AutomationStationRuntime _auxVerifier;
        private AutomationStationRuntime _adjudicator;
        private AutomationStationRuntime _output;
        private AutomationStationRuntime _legal;
        private float _spawnClock = 0.4f;
        private float _spawnInterval = 1.1f;
        private int _routeOrdinal;
        private bool _jamReported;

        internal AutomationFlowRuntime(
            Transform root,
            InstitutionalAutomationSession institution)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
            _institution = institution ??
                throw new ArgumentNullException(nameof(institution));
        }

        internal int Spawned { get; private set; }
        internal int Completed { get; private set; }
        internal int AppealsReturned { get; private set; }
        internal int AppealsResolved { get; private set; }
        internal int PrecedentsInstalled { get; private set; }
        internal int InFlight => _items.Count;
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

        internal void SetPolicy(AutomationPolicyKind policy)
        {
            if (!Enum.IsDefined(typeof(AutomationPolicyKind), policy))
                throw new ArgumentOutOfRangeException(nameof(policy));
            Policy = policy;
            switch (policy)
            {
                case AutomationPolicyKind.ProofFortress:
                    _spawnInterval = 1.75f;
                    break;
                case AutomationPolicyKind.RubberStampMill:
                    _spawnInterval = 1.05f;
                    break;
                case AutomationPolicyKind.AppealRefinery:
                    _spawnInterval = 1.30f;
                    break;
            }
            ApplyPolicyTuning();
            Emit(AutomationFeedbackKind.PolicyChanged, PolicyName + " BOUND");
        }

        internal void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            _spawnClock -= deltaTime;
            if (Spawned < _institution.Claims.Count &&
                _spawnClock <= 0f && _intake != null)
            {
                SpawnClaim();
                _spawnClock = _spawnInterval;
            }

            for (int i = 0; i < _stations.Count; i++)
                _stations[i].Tick(deltaTime);

            bool jammed = VerificationBacklog >= 6;
            if (jammed && !_jamReported)
                Emit(AutomationFeedbackKind.Jammed, "VERIFICATION JAM / QUEUE " +
                    VerificationBacklog.ToString("D2"));
            _jamReported = jammed;
        }

        public void Dispose()
        {
            for (int i = _items.Count - 1; i >= 0; i--)
                _items[i].Dispose();
            _items.Clear();
            for (int i = 0; i < _stations.Count; i++)
                _stations[i].Completed -= HandleStationCompleted;
            _stations.Clear();
        }

        private void SpawnClaim()
        {
            Spawned++;
            AutomationPublicClaim claim = _institution.Claims[Spawned - 1];
            Color[] folders =
            {
                new(0.82f, 0.70f, 0.43f),
                new(0.48f, 0.64f, 0.57f),
                new(0.66f, 0.47f, 0.38f),
                new(0.48f, 0.55f, 0.67f),
            };
            GameObject token = AutomationVisualFactory.CreateFolderToken(
                _root, claim.DisplayId, folders[(Spawned - 1) % folders.Length]);
            token.transform.position = new Vector3(-13f, 0.42f, 2.6f);
            var view = token.AddComponent<AutomationDossierView>();
            var item = new AutomationFlowItem(claim, token, view);
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
                    SelectVerifier().Enqueue(item);
                    break;
                case AutomationStationKind.Verification:
                    _adjudicator.Enqueue(item);
                    break;
                case AutomationStationKind.Adjudication:
                    if (item.IsAppeal)
                    {
                        item.ApplyAppealResolution();
                    }
                    else
                    {
                        AutomationRulingResult ruling = _institution.Commit(
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
                        if (Policy == AutomationPolicyKind.AppealRefinery)
                        {
                            PrecedentsInstalled++;
                            ApplyPolicyTuning();
                        }
                        Emit(AutomationFeedbackKind.AppealResolved,
                            "APPEAL RESOLVED / PRECEDENT " +
                            PrecedentsInstalled.ToString("D2"));
                    }
                    else Completed++;
                    if (appeal != null) SpawnAppeal(appeal);
                    break;
                case AutomationStationKind.Legal:
                    if (Policy == AutomationPolicyKind.AppealRefinery &&
                        _auxVerifier != null && ParallelRouting)
                        SelectVerifier().Enqueue(item);
                    else
                        _primaryVerifier.Enqueue(item);
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
            var item = new AutomationFlowItem(appeal, token, view);
            _items.Add(item);
            _legal.Enqueue(item);
            Emit(AutomationFeedbackKind.AppealReturned,
                label + " RETURNED THROUGH LEGAL");
        }

        private void ApplyPolicyTuning()
        {
            float verificationMultiplier;
            float legalMultiplier;
            switch (Policy)
            {
                case AutomationPolicyKind.ProofFortress:
                    verificationMultiplier = 1.22f;
                    legalMultiplier = 1f;
                    break;
                case AutomationPolicyKind.RubberStampMill:
                    verificationMultiplier = 0.88f;
                    legalMultiplier = 1.18f;
                    break;
                case AutomationPolicyKind.AppealRefinery:
                    verificationMultiplier = Mathf.Max(
                        0.38f, 0.76f - PrecedentsInstalled * 0.08f);
                    legalMultiplier = 0.36f;
                    break;
                default:
                    verificationMultiplier = 1f;
                    legalMultiplier = 1f;
                    break;
            }
            _primaryVerifier?.SetDurationMultiplier(verificationMultiplier);
            _auxVerifier?.SetDurationMultiplier(verificationMultiplier);
            _legal?.SetDurationMultiplier(legalMultiplier);
        }

        private void Emit(AutomationFeedbackKind kind, string message)
        {
            Feedback?.Invoke(kind, message ?? string.Empty);
        }

        private AutomationStationRuntime SelectVerifier()
        {
            if (!ParallelRouting || _auxVerifier == null) return _primaryVerifier;
            _routeOrdinal++;
            if (_routeOrdinal % 2 == 0) return _auxVerifier;
            return _primaryVerifier.Workload <= _auxVerifier.Workload
                ? _primaryVerifier
                : _auxVerifier;
        }
    }

    internal sealed class AutomationStationRuntime
    {
        private readonly List<AutomationFlowItem> _queue = new();
        private readonly Renderer _machineLight;
        private readonly TextMesh _queueLabel;
        private readonly Color _idleColour = new(0.83f, 0.58f, 0.17f);
        private AutomationFlowItem _active;
        private float _remaining;
        private float _durationMultiplier = 1f;

        internal AutomationStationRuntime(
            AutomationStationKind kind,
            string displayName,
            Vector3 position,
            float processDuration,
            bool isAuxiliary,
            Renderer machineLight,
            TextMesh queueLabel)
        {
            Kind = kind;
            DisplayName = displayName;
            Position = position;
            ProcessDuration = Mathf.Max(0.1f, processDuration);
            IsAuxiliary = isAuxiliary;
            _machineLight = machineLight;
            _queueLabel = queueLabel;
            RefreshVisualState();
        }

        internal event Action<AutomationStationRuntime, AutomationFlowItem> Completed;

        internal AutomationStationKind Kind { get; }
        internal string DisplayName { get; }
        internal Vector3 Position { get; }
        internal float ProcessDuration { get; }
        internal bool IsAuxiliary { get; }
        internal int Workload => _queue.Count + (_active != null ? 1 : 0);

        internal void SetDurationMultiplier(float multiplier)
        {
            _durationMultiplier = Mathf.Clamp(multiplier, 0.25f, 3f);
        }

        internal void Enqueue(AutomationFlowItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            _queue.Add(item);
            item.BeginTransit(this);
            RefreshVisualState();
        }

        internal void Tick(float deltaTime)
        {
            for (int i = 0; i < _queue.Count; i++)
                _queue[i].MoveTowards(QueuePosition(i), deltaTime, 3.6f);

            if (_active == null && _queue.Count > 0 && _queue[0].AtTarget)
            {
                _active = _queue[0];
                _queue.RemoveAt(0);
                _active.BeginProcessing(WorktopPosition());
                _remaining = ProcessDuration * _durationMultiplier;
                RefreshVisualState();
            }

            if (_active == null) return;
            _active.MoveTowards(WorktopPosition(), deltaTime, 5f);
            if (!_active.AtTarget) return;
            _remaining -= deltaTime;
            float duration = ProcessDuration * _durationMultiplier;
            _active.SetProcessingPulse(1f - Mathf.Clamp01(_remaining / duration));
            if (_remaining > 0f) return;

            AutomationFlowItem completed = _active;
            _active = null;
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
            if (_queueLabel != null) _queueLabel.text = "Q " + Workload.ToString("00");
            if (_machineLight == null) return;
            Color colour = _active != null
                ? new Color(0.43f, 0.78f, 0.38f)
                : _idleColour;
            if (Workload >= 4) colour = new Color(0.88f, 0.24f, 0.18f);
            _machineLight.material.color = colour;
        }
    }

    internal sealed class AutomationFlowItem : IDisposable
    {
        private readonly GameObject _root;
        private readonly AutomationDossierView _view;

        internal AutomationFlowItem(
            AutomationPublicClaim claim,
            GameObject root,
            AutomationDossierView view)
        {
            Claim = claim ?? throw new ArgumentNullException(nameof(claim));
            _root = root;
            _view = view;
        }

        internal AutomationFlowItem(
            AutomationAppealPacket appeal,
            GameObject root,
            AutomationDossierView view)
        {
            Appeal = appeal ?? throw new ArgumentNullException(nameof(appeal));
            _root = root;
            _view = view;
        }

        internal AutomationPublicClaim Claim { get; }
        internal AutomationAppealPacket Appeal { get; }
        internal AutomationRulingResult Ruling { get; private set; }
        internal bool IsAppeal => Appeal != null;
        internal string ClaimId => IsAppeal ? Appeal.AppealId : Claim.DisplayId;
        internal bool AtTarget => _view.AtTarget;

        internal void RevealEvidencePacket()
        {
            _view.RevealEvidencePacket(
                Claim.OfficialFactCount,
                Claim.AllegationCount,
                Claim.MissingEvidenceCount);
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

        internal bool AtTarget => (transform.position - _target).sqrMagnitude < 0.003f;

        private void Awake()
        {
            _target = transform.position;
            _baseScale = transform.localScale;
            _label = GetComponentInChildren<TextMesh>();
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
    }
}
