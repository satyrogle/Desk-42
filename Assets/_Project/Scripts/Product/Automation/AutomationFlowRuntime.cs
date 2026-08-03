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
        private float _spawnClock = 0.4f;
        private int _routeOrdinal;

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
                    break;
                case AutomationStationKind.Verification: _primaryVerifier = station; break;
                case AutomationStationKind.Adjudication: _adjudicator = station; break;
                case AutomationStationKind.Output: _output = station; break;
            }
        }

        internal void ToggleParallelRouting()
        {
            if (_auxVerifier == null) return;
            ParallelRouting = !ParallelRouting;
        }

        internal void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            _spawnClock -= deltaTime;
            if (Spawned < _institution.Claims.Count &&
                _spawnClock <= 0f && _intake != null)
            {
                SpawnClaim();
                _spawnClock = 1.35f;
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
                    SelectVerifier().Enqueue(item);
                    break;
                case AutomationStationKind.Verification:
                    _adjudicator.Enqueue(item);
                    break;
                case AutomationStationKind.Adjudication:
                    AutomationRulingResult ruling = _institution.Commit(
                        item.Claim.AutomationClaimId,
                        PlayerScopeChoice.Broad,
                        PlayerRulingDisposition.Recognised);
                    item.ApplyRuling(ruling);
                    _output.Enqueue(item);
                    break;
                case AutomationStationKind.Output:
                    _items.Remove(item);
                    item.Dispose();
                    Completed++;
                    break;
            }
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
                _remaining = ProcessDuration;
                RefreshVisualState();
            }

            if (_active == null) return;
            _active.MoveTowards(WorktopPosition(), deltaTime, 5f);
            if (!_active.AtTarget) return;
            _remaining -= deltaTime;
            _active.SetProcessingPulse(1f - Mathf.Clamp01(_remaining / ProcessDuration));
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

        internal AutomationPublicClaim Claim { get; }
        internal AutomationRulingResult Ruling { get; private set; }
        internal string ClaimId => Claim.DisplayId;
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
    }
}
