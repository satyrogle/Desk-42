using System;
using System.Collections.Generic;
using Desk42.Institutional.Player;
using UnityEngine;

namespace Desk42.Product.Automation
{
    internal sealed class AutomationFloorController : IDisposable
    {
        private readonly Transform _root;
        private readonly List<GameObject> _owned = new();
        private readonly List<Renderer> _flowRenderers = new();
        private readonly List<Renderer> _auxRouteRenderers = new();
        private readonly List<Renderer> _appealRouteRenderers = new();
        private AutomationFlowRuntime _flow;
        private AutomationAudioSystem _audio;
        private GameObject _auxSocket;
        private bool _placementArmed;
        private bool _flowOverlayVisible = true;
        private string _lastEvent = "FLOOR INITIALISING";
        private float _lastEventVisibleUntil;

        private static readonly Vector3 IntakePosition = new(-10.4f, 0.45f, 2.6f);
        private static readonly Vector3 SplitterPosition = new(-5.3f, 0.45f, 2.6f);
        private static readonly Vector3 VerifierPosition = new(-0.1f, 0.45f, 2.6f);
        private static readonly Vector3 AdjudicatorPosition = new(5.1f, 0.45f, 2.6f);
        private static readonly Vector3 OutputPosition = new(10.2f, 0.45f, 2.6f);
        private static readonly Vector3 LegalPosition = new(5.1f, 0.45f, -3.2f);
        private static readonly Vector3 AuxVerifierPosition = new(-0.1f, 0.45f, -3.2f);

        internal AutomationFloorController(Transform root)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
        }

        internal int ClaimsInFlight => _flow?.InFlight ?? 0;
        internal int UrgentInFlight => _flow?.UrgentInFlight ?? 0;
        internal float NearestDeadline => _flow?.NearestDeadline ?? 0f;
        internal int ClaimsCompleted => _flow?.Completed ?? 0;
        internal int AppealsReturned => _flow?.AppealsReturned ?? 0;
        internal int AppealsResolved => _flow?.AppealsResolved ?? 0;
        internal int VerificationBacklog => _flow?.VerificationBacklog ?? 0;
        internal string Bottleneck => _flow?.Bottleneck ?? "STARTING";
        internal bool AuxVerifierPlaced => _flow?.AuxVerifierInstalled ?? false;
        internal bool PlacementArmed => _placementArmed;
        internal string RouteMode => _flow?.ParallelRouting == true ? "PARALLEL" : "PRIMARY";
        internal int PolicyNumber => _flow?.DoctrineLocked == true
            ? (int)_flow.Policy
            : 0;
        internal string PolicyName => _flow?.PolicyName ?? "RUBBER MILL";
        internal string PolicyHudName => PolicyNumber switch
        {
            1 => "PROOF",
            2 => "RUBBER",
            3 => "REFINERY",
            4 => "WELFARE",
            _ => "SELECT",
        };
        internal string PolicyDescription => _flow?.PolicyDescription ?? string.Empty;
        internal int PrecedentsInstalled => _flow?.PrecedentsInstalled ?? 0;
        internal int OverdueCount => _flow?.OverdueCount ?? 0;
        internal int ReworkCount => _flow?.ReworkCount ?? 0;
        internal int JamCount => _flow?.JamCount ?? 0;
        internal int ActiveJamCount => _flow?.ActiveJamCount ?? 0;
        internal int SecondaryChecks => _flow?.SecondaryChecks ?? 0;
        internal int CollectiveCompleted => _flow?.CollectiveCompleted ?? 0;
        internal int IdentityCompleted => _flow?.IdentityCompleted ?? 0;
        internal int DependencyCompleted => _flow?.DependencyCompleted ?? 0;
        internal int ProvisionalReliefGranted =>
            _flow?.ProvisionalReliefGranted ?? 0;
        internal int ReliefReserve => _flow?.ReliefReserve ?? 0;
        internal int RelianceExposure => _flow?.RelianceExposure ?? 0;
        internal int Credits => _flow?.Credits ?? 0;
        internal int ShiftOrdinal => _flow?.ShiftOrdinal ?? 1;
        internal long SocietyTick => _flow?.SocietyTick ?? 0;
        internal int InstitutionalRulings => _flow?.InstitutionalRulings ?? 0;
        internal int PendingAppeals => _flow?.PendingAppeals ?? 0;
        internal float ClaimsPerMinute => _flow?.ClaimsPerMinute ?? 0f;
        internal string PriorityName => _flow?.RoutePriorityName ?? "BALANCED";
        internal string AppealModeName => _flow?.AppealModeName ?? "FULL REHEARING";
        internal int ProceduresBound => _flow?.ProceduresBound ?? 0;
        internal AutomationRunPhase RunPhase => _flow?.Phase ??
            AutomationRunPhase.DoctrineSelection;
        internal bool DoctrineLocked => _flow?.DoctrineLocked == true;
        internal IReadOnlyList<AutomationProcedureDraftChoiceCheckpoint>
            DraftChoices => _flow?.DraftChoices ??
            Array.Empty<AutomationProcedureDraftChoiceCheckpoint>();
        internal AutomationShiftSummaryCheckpoint ShiftSummary =>
            _flow?.ShiftSummary;
        internal AutomationBranchReviewCheckpoint BranchReview =>
            _flow?.BranchReview;
        internal IReadOnlyList<AutomationPrecedentRecord> Precedents =>
            _flow?.Precedents ?? Array.Empty<AutomationPrecedentRecord>();
        internal string SelectedStationName =>
            _flow?.SelectedStation?.DisplayName?.ToUpperInvariant() ?? "NONE";
        internal string SelectedStationState => _flow?.SelectedStation == null
            ? string.Empty
            : (_flow.SelectedStation.IsJammed ? "JAMMED / " : string.Empty) +
              "HEAT " + Mathf.RoundToInt(_flow.SelectedStation.Heat) + "% / " +
              _flow.SelectedStation.UpgradeSummary + " / COST " +
              _flow.SelectedStation.UpgradeCost;
        internal bool SelectedStationJammed => _flow?.SelectedStation?.IsJammed == true;
        internal string LastEvent => Time.unscaledTime <= _lastEventVisibleUntil
            ? _lastEvent
            : string.Empty;
        internal bool FlowStabilised => ClaimsCompleted >= 5 &&
            VerificationBacklog <= 3 &&
            (PolicyNumber == 1 || AppealsResolved >= 1);
        internal string OnboardingObjective
        {
            get
            {
                if (!DoctrineLocked)
                    return "CHOOSE A BINDING DOCTRINE / KEYS 1-4";
                if (ShiftOrdinal == 1 && ClaimsCompleted < 2)
                    return "FOLLOW ONE DOSSIER / WATCH INTAKE > EVIDENCE > VERIFY > RULE";
                if (ShiftOrdinal == 1 && !AuxVerifierPlaced)
                    return "FIRST BOTTLENECK / BUILD THE AUX VERIFIER [B], THEN CLICK ITS BAY";
                if (ShiftOrdinal == 1)
                    return "SELECT A MACHINE / TRADE SPEED, CAPACITY OR RELIABILITY FOR FLOW";
                if (ShiftOrdinal == 2)
                    return "URGENT FILES ARE ARRIVING / CHANGE PRIORITY [Q] BEFORE SLA FAILURE";
                if (ShiftOrdinal <= 4)
                    return "YOUR PROCEDURES NOW CHANGE PHYSICAL ROUTES / READ THE QUEUES";
                return "THE SAME SOCIETY IS RETURNING / COMPOUND HOLDINGS WITHOUT COLLAPSE";
            }
        }

        internal void BuildVisualFloor()
        {
            CreateCamera();
            CreateLighting();
            CreateRoom();
            GameObject audioObject = Own(new GameObject("Institutional Audio"));
            _audio = audioObject.AddComponent<AutomationAudioSystem>();
            _flow = new AutomationFlowRuntime(
                _root, InstitutionalAutomationSession.Create(12));
            _flow.Feedback += HandleFeedback;
            _flow.Register(CreateStation(AutomationStationKind.Intake,
                "PUBLIC INTAKE", IntakePosition, new Color(0.25f, 0.47f, 0.47f),
                "RECEIVE", 0.65f));
            _flow.Register(CreateStation(AutomationStationKind.EvidenceSplit,
                "EVIDENCE SPLIT", SplitterPosition,
                new Color(0.49f, 0.47f, 0.29f), "SEPARATE", 0.8f));
            _flow.Register(CreateStation(AutomationStationKind.Verification,
                "VERIFICATION", VerifierPosition,
                new Color(0.30f, 0.44f, 0.38f), "SCAN", 4.6f));
            _flow.Register(CreateStation(AutomationStationKind.Adjudication,
                "ADJUDICATION", AdjudicatorPosition,
                new Color(0.45f, 0.34f, 0.30f), "RULE", 1.2f));
            _flow.Register(CreateStation(AutomationStationKind.Output,
                "OUTPUT GATE", OutputPosition,
                new Color(0.34f, 0.43f, 0.28f), "RELEASE", 0.55f));
            _flow.Register(CreateStation(AutomationStationKind.Legal,
                "LEGAL / APPEALS", LegalPosition,
                new Color(0.39f, 0.31f, 0.42f), "RETURN", 2.5f));
            CreateRoute(new[]
            {
                new Vector3(-13f, 0.42f, 2.6f), IntakePosition,
                SplitterPosition, VerifierPosition, AdjudicatorPosition,
                OutputPosition, new Vector3(13f, 0.42f, 2.6f),
            }, new Color(0.26f, 0.64f, 0.58f), _flowRenderers);
            CreateRoute(new[]
            {
                SplitterPosition, new Vector3(-3.1f, 0.42f, -3.2f),
                AuxVerifierPosition, AdjudicatorPosition,
            }, new Color(0.24f, 0.52f, 0.48f), _auxRouteRenderers);
            CreateRoute(new[]
            {
                new Vector3(13f, 0.42f, -3.2f), LegalPosition,
                new Vector3(2.5f, 0.42f, -0.4f), VerifierPosition,
            }, new Color(0.68f, 0.24f, 0.21f), _appealRouteRenderers);
            for (int i = 0; i < _auxRouteRenderers.Count; i++)
                _auxRouteRenderers[i].enabled = false;
            CreateAuxVerifierSocket();
        }

        internal void Tick(float deltaTime)
        {
            _flow?.Tick(deltaTime);
            if (_flow != null)
                _audio?.SetOperationalState(
                    _flow.VerificationBacklog,
                    _flow.AverageMachineHeat,
                    _flow.PendingAppeals,
                    _flow.ShiftOrdinal);
        }

        internal void TogglePlacementMode()
        {
            if (AuxVerifierPlaced) return;
            _placementArmed = !_placementArmed;
        }

        internal void ToggleRouteMode()
        {
            _flow?.ToggleParallelRouting();
            RefreshAuxRouteAppearance();
        }

        internal void CycleRoutePriority()
        {
            _flow?.CycleRoutePriority();
        }

        internal void CycleAppealMode()
        {
            _flow?.CycleAppealMode();
        }

        internal void SelectNextStation()
        {
            _flow?.SelectNextStation();
        }

        internal bool SelectFirstJammedStation()
        {
            return _flow?.SelectFirstJammedStation() == true;
        }

        internal bool UpgradeSelected(AutomationUpgradeKind kind)
        {
            return _flow?.UpgradeSelected(kind) == true;
        }

        internal bool RepairSelected()
        {
            return _flow?.RepairSelected() == true;
        }

#if UNITY_INCLUDE_TESTS
        internal bool CreateValidationJamOnSelected()
        {
            return _flow?.CreateValidationJamOnSelected() == true;
        }
#endif

        internal bool BindProcedure(int procedureNumber)
        {
            if (procedureNumber < 1 || procedureNumber > 13) return false;
            return _flow?.ForceBindProcedureForTest(
                (AutomationProcedureKind)procedureNumber) == true;
        }

        internal bool IsProcedureBound(int procedureNumber)
        {
            if (procedureNumber < 1 || procedureNumber > 13) return false;
            return _flow?.IsProcedureBound(
                (AutomationProcedureKind)procedureNumber) == true;
        }

        internal int ProcedureTier(int procedureNumber)
        {
            if (procedureNumber < 1 || procedureNumber > 13) return 0;
            return _flow?.ProcedureTier(
                (AutomationProcedureKind)procedureNumber) ?? 0;
        }

        internal bool SetPolicy(int policyNumber)
        {
            if (policyNumber < 1 || policyNumber > 4) return false;
            return _flow?.ChooseDoctrine(
                (AutomationPolicyKind)policyNumber) == true;
        }

        internal bool ChooseProcedureDraft(int choiceIndex)
        {
            return _flow?.ChooseProcedureDraft(choiceIndex) == true;
        }

        internal bool ContinueAfterShift()
        {
            return _flow?.ContinueAfterShift() == true;
        }

        internal bool CyclePrecedentMode(int ledgerIndex)
        {
            return _flow?.CyclePrecedentMode(ledgerIndex) == true;
        }

        internal void SaveRun(string path)
        {
            if (_flow == null) throw new InvalidOperationException(
                "The automation floor is not ready to save.");
            AutomationRunStore.Save(path, _flow.CaptureCheckpoint());
            HandleFeedback(AutomationFeedbackKind.RunSaved,
                "RUN SAVED / " + System.IO.Path.GetFileName(path));
        }

        internal void LoadRun(string path)
        {
            AutomationRunCheckpoint checkpoint = AutomationRunStore.Load(path);
            bool savedAuxiliary = false;
            for (int i = 0; i < checkpoint.Flow.Stations.Count; i++)
                if (checkpoint.Flow.Stations[i].Kind ==
                        AutomationStationKind.Verification &&
                    checkpoint.Flow.Stations[i].IsAuxiliary)
                    savedAuxiliary = true;
            if (savedAuxiliary && !AuxVerifierPlaced) InstallAuxVerifier();
            _flow.RestoreCheckpoint(checkpoint);
            RefreshAuxRouteAppearance();
            SetFlowOverlayVisible(_flowOverlayVisible);
            HandleFeedback(AutomationFeedbackKind.RunLoaded,
                "RUN LOADED / " + System.IO.Path.GetFileName(path));
        }

        internal bool TryPlaceAuxVerifier(Vector3 screenPosition)
        {
            if (!_placementArmed || AuxVerifierPlaced || Camera.main == null) return false;
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);
            var floor = new Plane(Vector3.up, Vector3.zero);
            if (!floor.Raycast(ray, out float distance)) return false;
            Vector3 hit = ray.GetPoint(distance);
            if (Vector3.Distance(new Vector3(hit.x, 0f, hit.z),
                    new Vector3(AuxVerifierPosition.x, 0f, AuxVerifierPosition.z)) > 2.2f)
                return false;
            InstallAuxVerifier();
            return true;
        }

        internal bool TrySelectStation(Vector3 screenPosition)
        {
            if (_placementArmed || Camera.main == null) return false;
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);
            var floor = new Plane(Vector3.up, Vector3.zero);
            if (!floor.Raycast(ray, out float distance)) return false;
            return _flow?.SelectStationNear(ray.GetPoint(distance)) == true;
        }

        internal void InstallAuxVerifierForCapture()
        {
            if (!AuxVerifierPlaced) InstallAuxVerifier();
        }

        internal void SetFlowOverlayVisible(bool visible)
        {
            _flowOverlayVisible = visible;
            for (int i = 0; i < _flowRenderers.Count; i++)
                if (_flowRenderers[i] != null)
                    _flowRenderers[i].enabled = visible;
            for (int i = 0; i < _auxRouteRenderers.Count; i++)
                if (_auxRouteRenderers[i] != null)
                    _auxRouteRenderers[i].enabled = visible && AuxVerifierPlaced;
            for (int i = 0; i < _appealRouteRenderers.Count; i++)
                if (_appealRouteRenderers[i] != null)
                    _appealRouteRenderers[i].enabled = visible;
        }

        public void Dispose()
        {
            if (_flow != null) _flow.Feedback -= HandleFeedback;
            _flow?.Dispose();
            _flow = null;
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) UnityEngine.Object.Destroy(_owned[i]);
            _owned.Clear();
        }

        private void CreateCamera()
        {
            GameObject cameraObject = Own(new GameObject("Automation Camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            camera.orthographic = true;
            camera.orthographicSize = 8.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.064f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(0f, 18f, -18f);
            camera.transform.LookAt(new Vector3(0f, 0.75f, 0f));
            camera.tag = "MainCamera";
        }

        private void CreateLighting()
        {
            GameObject keyObject = Own(new GameObject("Fluorescent Key"));
            Light key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(0.80f, 0.86f, 0.72f);
            key.intensity = 1.15f;
            key.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

            GameObject fillObject = Own(new GameObject("Sodium Fill"));
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(1f, 0.55f, 0.22f);
            fill.intensity = 3.5f;
            fill.range = 18f;
            fill.transform.position = new Vector3(3f, 7f, -1f);
        }

        private void CreateRoom()
        {
            GameObject floor = AutomationVisualFactory.CreateBlock(
                _root, "Institutional Floor", new Vector3(0f, -0.18f, 0f),
                new Vector3(28f, 0.35f, 14f), new Color(0.18f, 0.22f, 0.20f));
            _owned.Add(floor);
            for (int x = -13; x <= 13; x += 2)
            {
                GameObject grid = AutomationVisualFactory.CreateBlock(
                    _root, "Floor Grid", new Vector3(x, 0.01f, 0f),
                    new Vector3(0.025f, 0.02f, 13.6f),
                    new Color(0.25f, 0.30f, 0.27f));
                _owned.Add(grid);
            }
            for (int z = -6; z <= 6; z += 2)
            {
                GameObject grid = AutomationVisualFactory.CreateBlock(
                    _root, "Floor Grid", new Vector3(0f, 0.012f, z),
                    new Vector3(27.6f, 0.02f, 0.025f),
                    new Color(0.25f, 0.30f, 0.27f));
                _owned.Add(grid);
            }
            _owned.Add(AutomationVisualFactory.CreateBlock(
                _root, "Back Wall", new Vector3(0f, 1.4f, 6.8f),
                new Vector3(28f, 2.8f, 0.4f), new Color(0.15f, 0.19f, 0.18f)));
            _owned.Add(AutomationVisualFactory.CreateBlock(
                _root, "Left Wall", new Vector3(-14f, 1.15f, 0f),
                new Vector3(0.35f, 2.3f, 14f), new Color(0.12f, 0.15f, 0.145f)));
            _owned.Add(AutomationVisualFactory.CreateBlock(
                _root, "Right Wall", new Vector3(14f, 1.15f, 0f),
                new Vector3(0.35f, 2.3f, 14f), new Color(0.12f, 0.15f, 0.145f)));
            for (int bay = -2; bay <= 2; bay++)
            {
                float x = bay * 5.2f;
                _owned.Add(AutomationVisualFactory.CreateBlock(
                    _root, "Ceiling Rail " + bay, new Vector3(x, 4.65f, 0f),
                    new Vector3(0.18f, 0.18f, 13.2f),
                    new Color(0.075f, 0.09f, 0.085f)));
                _owned.Add(AutomationVisualFactory.CreateBlock(
                    _root, "Fluorescent Housing " + bay,
                    new Vector3(x, 4.48f, 1.2f),
                    new Vector3(2.4f, 0.12f, 0.55f),
                    new Color(0.20f, 0.22f, 0.19f)));
                _owned.Add(AutomationVisualFactory.CreateBlock(
                    _root, "Fluorescent Tube " + bay,
                    new Vector3(x, 4.39f, 1.2f),
                    new Vector3(2.1f, 0.05f, 0.35f),
                    new Color(0.72f, 0.83f, 0.65f)));
            }
            for (int stripe = -6; stripe <= 6; stripe += 2)
            {
                GameObject warning = AutomationVisualFactory.CreateBlock(
                    _root, "Intake Warning " + stripe,
                    new Vector3(-12.5f, 0.025f, stripe * 0.5f),
                    new Vector3(0.16f, 0.025f, 0.80f),
                    stripe % 4 == 0
                        ? new Color(0.80f, 0.55f, 0.14f)
                        : new Color(0.075f, 0.085f, 0.08f));
                warning.transform.localRotation = Quaternion.Euler(0f, 28f, 0f);
                _owned.Add(warning);
            }
            _owned.Add(AutomationVisualFactory.CreateCylinder(
                _root, "Pneumatic Archive Main", new Vector3(0f, 3.65f, 6.15f),
                new Vector3(0.22f, 6.5f, 0.22f),
                new Color(0.21f, 0.25f, 0.23f), new Vector3(0f, 0f, 90f)));
            _owned.Add(AutomationVisualFactory.CreateWorldLabel(
                _root, "BRANCH 42 / AUTOMATED CLAIMS DIVISION",
                new Vector3(0f, 2.1f, 6.55f), 0.115f,
                new Color(0.73f, 0.74f, 0.59f), TextAnchor.MiddleCenter));
            _owned.Add(AutomationVisualFactory.CreateWorldLabel(
                _root, "LIVED EVENTS ARE NOT OFFICIAL FACTS",
                new Vector3(0f, 1.45f, 6.53f), 0.066f,
                new Color(0.43f, 0.54f, 0.46f), TextAnchor.MiddleCenter));
        }

        private AutomationStationRuntime CreateStation(
            AutomationStationKind kind,
            string name,
            Vector3 position,
            Color colour,
            string verb,
            float processDuration,
            bool isAuxiliary = false)
        {
            GameObject station = AutomationVisualFactory.CreateStation(
                _root, name, position, colour, verb);
            _owned.Add(station);
            _owned.Add(AutomationVisualFactory.CreateStaff(
                _root, name + " Operator", position + new Vector3(0f, 0f, -1.25f),
                colour * 0.9f));
            Renderer light = station.transform.Find("Machine Light")
                ?.GetComponent<Renderer>();
            Renderer selection = station.transform.Find("Selection Plinth")
                ?.GetComponent<Renderer>();
            TextMesh queue = station.transform.Find("Label Q 00")
                ?.GetComponent<TextMesh>();
            return new AutomationStationRuntime(kind, name, position,
                processDuration, isAuxiliary, light, selection, queue);
        }

        private void CreateRoute(
            IReadOnlyList<Vector3> points,
            Color colour,
            ICollection<Renderer> renderers)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 from = points[i];
                Vector3 to = points[i + 1];
                Vector3 delta = to - from;
                float length = delta.magnitude;
                GameObject segment = AutomationVisualFactory.CreateBlock(
                    _root, "Route", (from + to) * 0.5f + Vector3.down * 0.22f,
                    new Vector3(0.22f, 0.05f, length), colour);
                segment.transform.rotation = Quaternion.LookRotation(delta.normalized);
                _owned.Add(segment);
                Renderer renderer = segment.GetComponent<Renderer>();
                renderers.Add(renderer);
            }
        }

        private void CreateAuxVerifierSocket()
        {
            _auxSocket = AutomationVisualFactory.CreateBlock(
                _root, "Aux Verifier Socket",
                new Vector3(AuxVerifierPosition.x, 0.03f, AuxVerifierPosition.z),
                new Vector3(3.1f, 0.07f, 2.15f),
                new Color(0.24f, 0.25f, 0.20f));
            _owned.Add(_auxSocket);
            AutomationVisualFactory.CreateWorldLabel(
                _auxSocket.transform, "AUX VERIFIER BAY / CLICK TO INSTALL",
                new Vector3(0f, 0.16f, 0f), 0.06f,
                new Color(0.75f, 0.59f, 0.26f), TextAnchor.MiddleCenter);
        }

        private void InstallAuxVerifier()
        {
            _placementArmed = false;
            if (_auxSocket != null) _auxSocket.SetActive(false);
            _flow.Register(CreateStation(AutomationStationKind.Verification,
                "AUX VERIFICATION", AuxVerifierPosition,
                new Color(0.25f, 0.53f, 0.47f), "PARALLEL SCAN", 3.25f, true));
            RefreshAuxRouteAppearance();
        }

        private void RefreshAuxRouteAppearance()
        {
            bool active = _flow?.ParallelRouting == true;
            Color colour = active
                ? new Color(0.32f, 0.78f, 0.66f)
                : new Color(0.20f, 0.28f, 0.26f);
            for (int i = 0; i < _auxRouteRenderers.Count; i++)
            {
                Renderer renderer = _auxRouteRenderers[i];
                if (renderer == null) continue;
                renderer.enabled = _flowOverlayVisible && AuxVerifierPlaced;
                renderer.material.color = colour;
            }
        }

        private void HandleFeedback(AutomationFeedbackKind kind, string message)
        {
            _lastEvent = message ?? string.Empty;
            _lastEventVisibleUntil = Time.unscaledTime +
                (kind == AutomationFeedbackKind.Jammed ||
                 kind == AutomationFeedbackKind.Misclassified ||
                 kind == AutomationFeedbackKind.DeadlineMissed ||
                 kind == AutomationFeedbackKind.AppealReturned ? 3.2f : 1.8f);
            _audio?.Play(kind);
        }

        private GameObject Own(GameObject value)
        {
            value.transform.SetParent(_root, false);
            _owned.Add(value);
            return value;
        }
    }

}
