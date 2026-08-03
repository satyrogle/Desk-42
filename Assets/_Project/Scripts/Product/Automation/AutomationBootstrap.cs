using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Desk42.Institutional.Player;
using UnityEngine;

namespace Desk42.Product.Automation
{
    [DisallowMultipleComponent]
    public sealed class AutomationBootstrap : MonoBehaviour
    {
        private const string CaptureArgument = "--desk42-automation-capture";
        private const string CaptureDelayArgument = "--desk42-automation-capture-delay";
        private const string AutoAuxArgument = "--desk42-automation-auto-aux";
        private const string PolicyArgument = "--desk42-automation-policy";
        private const string ProcedureArgument = "--desk42-automation-procedure";

        private AutomationFloorController _floor;
        private bool _paused;
        private bool _flowOverlay = true;
        private bool _precedentLedgerVisible;
        private GUIStyle _title;
        private GUIStyle _metric;
        private GUIStyle _small;
        private GUIStyle _pill;

        public bool Ready => _floor != null;
        public int ClaimsInFlight => _floor?.ClaimsInFlight ?? 0;
        public int ClaimsCompleted => _floor?.ClaimsCompleted ?? 0;
        public long SocietyTick => _floor?.SocietyTick ?? 0;
        public int InstitutionalRulings => _floor?.InstitutionalRulings ?? 0;
        public int PendingAppeals => _floor?.PendingAppeals ?? 0;
        public int PrecedentsInstalled => _floor?.PrecedentsInstalled ?? 0;
        public string FirstPrecedentMode =>
            (_floor?.Precedents.Count ?? 0) > 0
                ? _floor.Precedents[0].Mode.ToString()
                : string.Empty;
        public int AppealsReturned => _floor?.AppealsReturned ?? 0;
        public int AppealsResolved => _floor?.AppealsResolved ?? 0;
        public int OverdueClaims => _floor?.OverdueCount ?? 0;
        public int OperationalReworks => _floor?.ReworkCount ?? 0;
        public int MachineJams => _floor?.JamCount ?? 0;
        public int ActiveMachineJams => _floor?.ActiveJamCount ?? 0;
        public int SecondaryVerificationChecks => _floor?.SecondaryChecks ?? 0;
        public int CollectiveGrievancesProcessed =>
            _floor?.CollectiveCompleted ?? 0;
        public int UpgradeCredits => _floor?.Credits ?? 0;
        public string RoutePriority => _floor?.PriorityName ?? "BALANCED";
        public string AppealHandling => _floor?.AppealModeName ?? "FULL REHEARING";
        public int ProceduresBound => _floor?.ProceduresBound ?? 0;
        public int ProcedureTier(int procedureNumber)
        {
            return _floor?.ProcedureTier(procedureNumber) ?? 0;
        }
        public int CurrentPolicyNumber => _floor?.PolicyNumber ?? 2;
        public int CurrentShift => _floor?.ShiftOrdinal ?? 1;
        public bool AuxVerifierInstalled => _floor?.AuxVerifierPlaced ?? false;
        public string RunPhase => (_floor?.RunPhase ??
            AutomationRunPhase.DoctrineSelection).ToString();
        public bool DoctrineLocked => _floor?.DoctrineLocked == true;
        public int DraftChoiceCount => _floor?.DraftChoices.Count ?? 0;
        public string BranchOutcome => _floor?.BranchReview?.Outcome.ToString() ??
            string.Empty;

        public void SelectPolicy(int policyNumber)
        {
            _floor?.SetPolicy(policyNumber);
        }

        public void InstallAuxVerifier()
        {
            _floor?.InstallAuxVerifierForCapture();
        }

        public void CyclePriority()
        {
            _floor?.CycleRoutePriority();
        }

        public void CycleAppealHandling()
        {
            _floor?.CycleAppealMode();
        }

        public void SelectNextStation()
        {
            _floor?.SelectNextStation();
        }

        public bool UpgradeSelectedThroughput()
        {
            return _floor?.UpgradeSelected(AutomationUpgradeKind.Throughput) == true;
        }

        public bool RepairSelectedStation()
        {
            return _floor?.RepairSelected() == true;
        }

        public bool SelectFirstJammedStation()
        {
            return _floor?.SelectFirstJammedStation() == true;
        }

#if UNITY_INCLUDE_TESTS
        public bool CreateValidationJamOnSelectedStation()
        {
            return _floor?.CreateValidationJamOnSelected() == true;
        }
#endif

        public bool BindProcedure(int procedureNumber)
        {
            return _floor?.BindProcedure(procedureNumber) == true;
        }

        public bool ChooseDraft(int choiceIndex)
        {
            return _floor?.ChooseProcedureDraft(choiceIndex) == true;
        }

        public bool ContinueAfterShift()
        {
            return _floor?.ContinueAfterShift() == true;
        }

        public bool CycleFirstPrecedentMode()
        {
            return _floor?.CyclePrecedentMode(0) == true;
        }

        public void SaveRun(string path)
        {
            _floor?.SaveRun(path);
        }

        public void LoadRun(string path)
        {
            _floor?.LoadRun(path);
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            _floor = new AutomationFloorController(transform);
            _floor.BuildVisualFloor();
        }

        private IEnumerator Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string capturePath = ArgumentValue(arguments, CaptureArgument);
            if (string.IsNullOrWhiteSpace(capturePath)) yield break;

            string requestedPolicy = ArgumentValue(arguments, PolicyArgument);
            if (int.TryParse(requestedPolicy, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int policyNumber))
                _floor?.SetPolicy(policyNumber);
            string requestedProcedure = ArgumentValue(arguments, ProcedureArgument);
            if (int.TryParse(requestedProcedure, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int procedureNumber))
                _floor?.BindProcedure(procedureNumber);

            float captureDelay = 4f;
            string requestedDelay = ArgumentValue(arguments, CaptureDelayArgument);
            if (float.TryParse(requestedDelay, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float parsedDelay))
                captureDelay = Mathf.Clamp(parsedDelay, 1f, 60f);
            if (HasArgument(arguments, AutoAuxArgument))
            {
                yield return new WaitForSecondsRealtime(1f);
                _floor?.InstallAuxVerifierForCapture();
                captureDelay = Mathf.Max(0f, captureDelay - 1f);
            }
            yield return new WaitForSecondsRealtime(captureDelay);
            yield return new WaitForEndOfFrame();
            string fullPath = Path.GetFullPath(capturePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
            if (File.Exists(fullPath)) File.Delete(fullPath);
            ScreenCapture.CaptureScreenshot(fullPath);
            for (int frame = 0; frame < 240 && !File.Exists(fullPath); frame++)
                yield return null;
            if (!File.Exists(fullPath))
            {
                Debug.LogError("DESK42_AUTOMATION_CAPTURE_FAILED " + fullPath, this);
                Application.Quit(1);
                yield break;
            }
            Debug.Log("DESK42_AUTOMATION_CAPTURE_OK " + fullPath, this);
            Application.Quit(0);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                _paused = !_paused;
                Time.timeScale = _paused ? 0f : 1f;
            }
            if (Input.GetKeyDown(KeyCode.Tab)) _flowOverlay = !_flowOverlay;
            bool operating = _floor?.RunPhase ==
                AutomationRunPhase.ActiveProcessing;
            if (operating && Input.GetKeyDown(KeyCode.B))
                _floor?.TogglePlacementMode();
            if (operating && Input.GetKeyDown(KeyCode.R))
                _floor?.ToggleRouteMode();
            if (operating && Input.GetKeyDown(KeyCode.Q))
                _floor?.CycleRoutePriority();
            if (operating && Input.GetKeyDown(KeyCode.E))
                _floor?.CycleAppealMode();
            if (operating && Input.GetKeyDown(KeyCode.LeftBracket))
                _floor?.SelectNextStation();
            if (operating && Input.GetKeyDown(KeyCode.U))
                _floor?.UpgradeSelected(AutomationUpgradeKind.Throughput);
            if (operating && Input.GetKeyDown(KeyCode.C))
                _floor?.UpgradeSelected(AutomationUpgradeKind.Capacity);
            if (operating && Input.GetKeyDown(KeyCode.F))
                _floor?.UpgradeSelected(AutomationUpgradeKind.Reliability);
            if (operating && Input.GetKeyDown(KeyCode.X))
                _floor?.RepairSelected();
            if (Input.GetKeyDown(KeyCode.L))
                _precedentLedgerVisible = !_precedentLedgerVisible;
            if (Input.GetKeyDown(KeyCode.F9))
                _floor?.SaveRun(DefaultSavePath());
            if (Input.GetKeyDown(KeyCode.F10) &&
                File.Exists(DefaultSavePath()))
                _floor?.LoadRun(DefaultSavePath());
            if (_floor?.RunPhase == AutomationRunPhase.DoctrineSelection)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) _floor.SetPolicy(1);
                if (Input.GetKeyDown(KeyCode.Alpha2)) _floor.SetPolicy(2);
                if (Input.GetKeyDown(KeyCode.Alpha3)) _floor.SetPolicy(3);
            }
            else if (_floor?.RunPhase == AutomationRunPhase.ShiftClose)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                    _floor.ChooseProcedureDraft(0);
                if (Input.GetKeyDown(KeyCode.Alpha2))
                    _floor.ChooseProcedureDraft(1);
                if (Input.GetKeyDown(KeyCode.Alpha3))
                    _floor.ChooseProcedureDraft(2);
                if (Input.GetKeyDown(KeyCode.Return))
                    _floor.ContinueAfterShift();
            }
            if (operating && Input.GetMouseButtonDown(0) &&
                _floor?.TryPlaceAuxVerifier(Input.mousePosition) != true)
                _floor?.TrySelectStation(Input.mousePosition);
            _floor?.SetFlowOverlayVisible(_flowOverlay);
            _floor?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            _floor?.Dispose();
        }

        private void OnGUI()
        {
            EnsureStyles();
            float width = Mathf.Min(Screen.width - 32f, 1080f);
            GUILayout.BeginArea(new Rect(16f, 14f, width, 82f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(250f));
            GUILayout.Label("DESK42 / BRANCH AUTOMATION", _title);
            GUILayout.Label("SHIFT " + (_floor?.ShiftOrdinal ?? 1).ToString("D2") +
                " / WORLD T" + (_floor?.SocietyTick ?? 0).ToString("D2") +
                " / " + (_floor?.InstitutionalRulings ?? 0).ToString("D2") +
                " RULINGS / LIVE", _small);
            GUILayout.EndVertical();
            Metric("IN FLIGHT", ClaimsInFlight.ToString("00"));
            Metric("DONE / RATE", (_floor?.ClaimsCompleted ?? 0).ToString("00") + "/" +
                (_floor?.ClaimsPerMinute ?? 0f).ToString("0.0"));
            Metric("BACKLOG", (_floor?.VerificationBacklog ?? 0).ToString("00"));
            Metric("URGENT / SLA", (_floor?.UrgentInFlight ?? 0).ToString("00") + "/" +
                Mathf.CeilToInt(_floor?.NearestDeadline ?? 0f).ToString("00") + "s");
            Metric("OVERDUE", (_floor?.OverdueCount ?? 0).ToString("00"));
            Metric("REWORK/JAM", (_floor?.ReworkCount ?? 0).ToString("00") + "/" +
                (_floor?.JamCount ?? 0).ToString("00"));
            GUILayout.FlexibleSpace();
            string status = _paused
                ? "PAUSED"
                : _floor?.FlowStabilised == true
                    ? "FLOW STABILISED"
                    : "OPERATING";
            GUILayout.Label(status, _pill,
                GUILayout.Width(118f), GUILayout.Height(28f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(16f, Screen.height - 214f,
                    Mathf.Min(Screen.width - 32f, 1080f), 44f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("BINDING PROCEDURES " +
                (_floor?.ProceduresBound ?? 0) + "/2",
                _small, GUILayout.Width(135f));
            int shownProcedures = 0;
            for (int number = 1; number <= 6; number++)
            {
                int tier = _floor?.ProcedureTier(number) ?? 0;
                if (tier <= 0) continue;
                var kind = (AutomationProcedureKind)number;
                var content = new GUIContent(
                    AutomationProcedureNames.ShortName(kind) + " T" + tier,
                    AutomationProcedureNames.Effect(kind, tier));
                GUILayout.Label(content, _pill,
                    GUILayout.Width(170f), GUILayout.Height(27f));
                shownProcedures++;
            }
            if (shownProcedures == 0)
                GUILayout.Label("Draft developments at shift close.",
                    _small, GUILayout.Width(260f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("LEDGER [L]", GUILayout.Width(105f),
                    GUILayout.Height(27f)))
                _precedentLedgerVisible = !_precedentLedgerVisible;
            if (GUILayout.Button("SAVE [F9]", GUILayout.Width(92f),
                    GUILayout.Height(27f)))
                _floor?.SaveRun(DefaultSavePath());
            GUI.enabled = File.Exists(DefaultSavePath());
            if (GUILayout.Button("LOAD [F10]", GUILayout.Width(96f),
                    GUILayout.Height(27f)))
                _floor?.LoadRun(DefaultSavePath());
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(16f, Screen.height - 164f,
                    Mathf.Min(Screen.width - 32f, 1080f), 44f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            PolicyButton(1, "1  PROOF FORTRESS");
            PolicyButton(2, "2  RUBBER MILL");
            PolicyButton(3, "3  APPEAL REFINERY");
            string eventText = _floor?.LastEvent;
            string hoverText = GUI.tooltip;
            string readout = !string.IsNullOrEmpty(hoverText)
                ? hoverText
                : string.IsNullOrEmpty(eventText)
                ? (_floor?.PolicyDescription ?? string.Empty)
                : "EVENT  " + eventText;
            GUILayout.Label(readout, _small, GUILayout.ExpandWidth(true));
            GUILayout.Label("PRECEDENTS " + (_floor?.PrecedentsInstalled ?? 0).ToString("D2"),
                _small, GUILayout.Width(105f));
            GUILayout.Label("CREDITS " + (_floor?.Credits ?? 0).ToString("D2"),
                _small, GUILayout.Width(82f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(16f, Screen.height - 114f,
                    Mathf.Min(Screen.width - 32f, 1080f), 46f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            string buildLabel = _floor?.AuxVerifierPlaced == true
                ? "AUX VERIFIER ONLINE"
                : _floor?.PlacementArmed == true
                    ? "CLICK AUX BAY"
                    : "BUILD AUX VERIFIER [B]";
            GUI.enabled = _floor?.AuxVerifierPlaced != true;
            if (GUILayout.Button(buildLabel, GUILayout.Width(180f), GUILayout.Height(28f)))
                _floor?.TogglePlacementMode();
            GUI.enabled = _floor?.AuxVerifierPlaced == true;
            if (GUILayout.Button("ROUTE: " + (_floor?.RouteMode ?? "PRIMARY") + " [R]",
                    GUILayout.Width(150f), GUILayout.Height(28f)))
                _floor?.ToggleRouteMode();
            GUI.enabled = true;
            if (GUILayout.Button("PRIORITY: " + (_floor?.PriorityName ?? "BALANCED") + " [Q]",
                    GUILayout.Width(190f), GUILayout.Height(28f)))
                _floor?.CycleRoutePriority();
            if (GUILayout.Button("APPEALS: " + (_floor?.AppealModeName ?? "FULL REHEARING") + " [E]",
                    GUILayout.Width(210f), GUILayout.Height(28f)))
                _floor?.CycleAppealMode();
            GUILayout.Label("BOTTLENECK " + (_floor?.Bottleneck ?? "STARTING"),
                _small, GUILayout.Width(160f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(16f, Screen.height - 64f,
                    Mathf.Min(Screen.width - 32f, 1080f), 48f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SELECT NEXT [",
                    GUILayout.Width(105f), GUILayout.Height(28f)))
                _floor?.SelectNextStation();
            GUILayout.BeginVertical(GUILayout.Width(230f));
            GUILayout.Label(_floor?.SelectedStationName ?? "NONE", _small);
            GUILayout.Label(_floor?.SelectedStationState ?? string.Empty, _small);
            GUILayout.EndVertical();
            GUI.enabled = _floor?.SelectedStationJammed != true;
            if (GUILayout.Button("SPEED [U]", GUILayout.Width(90f), GUILayout.Height(28f)))
                _floor?.UpgradeSelected(AutomationUpgradeKind.Throughput);
            if (GUILayout.Button("CAPACITY [C]", GUILayout.Width(105f), GUILayout.Height(28f)))
                _floor?.UpgradeSelected(AutomationUpgradeKind.Capacity);
            if (GUILayout.Button("RELIABILITY [F]", GUILayout.Width(120f), GUILayout.Height(28f)))
                _floor?.UpgradeSelected(AutomationUpgradeKind.Reliability);
            GUI.enabled = _floor?.SelectedStationJammed == true;
            if (GUILayout.Button("REPAIR [X]", GUILayout.Width(92f), GUILayout.Height(28f)))
                _floor?.RepairSelected();
            GUI.enabled = true;
            GUILayout.Label("Click a machine to inspect.", _small,
                GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            DrawRunModal();
            if (_precedentLedgerVisible) DrawPrecedentLedger();
        }

        private void Metric(string label, string value)
        {
            GUILayout.BeginVertical(GUILayout.Width(112f));
            GUILayout.Label(label, _small);
            GUILayout.Label(value, _metric);
            GUILayout.EndVertical();
        }

        private void PolicyButton(int number, string label)
        {
            string decorated = _floor?.PolicyNumber == number
                ? "[ " + label + " ]"
                : label;
            GUI.enabled = _floor?.RunPhase ==
                AutomationRunPhase.DoctrineSelection;
            if (GUILayout.Button(decorated, GUILayout.Width(150f), GUILayout.Height(27f)))
                _floor?.SetPolicy(number);
            GUI.enabled = true;
        }

        private void DrawRunModal()
        {
            AutomationRunPhase phase = _floor?.RunPhase ??
                AutomationRunPhase.DoctrineSelection;
            if (phase == AutomationRunPhase.ActiveProcessing) return;
            float width = Mathf.Min(780f, Screen.width - 60f);
            float height = phase == AutomationRunPhase.BranchReview ? 440f : 380f;
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);
            GUILayout.BeginArea(rect, GUI.skin.window);
            if (phase == AutomationRunPhase.DoctrineSelection)
            {
                GUILayout.Label("SELECT THE INSTITUTION YOU WILL HAVE TO LIVE WITH",
                    _title);
                GUILayout.Space(8f);
                GUILayout.Label(
                    "Doctrine is binding for all eight shifts. Procedures and holdings " +
                    "will compound inside it.", _small);
                GUILayout.Space(16f);
                DoctrineChoice(1, "1  PROOF FORTRESS",
                    "High verification threshold / slower release / narrow holdings / " +
                    "low appeal exposure / expensive expansion");
                DoctrineChoice(2, "2  RUBBER MILL",
                    "Low recognition threshold / fast intake / broad scope / cheap capacity / " +
                    "appeals and liability become workload");
                DoctrineChoice(3, "3  APPEAL REFINERY",
                    "Moderate threshold / powerful Legal line / weak opening / " +
                    "real holdings accelerate the late run");
            }
            else if (phase == AutomationRunPhase.ShiftClose)
            {
                AutomationShiftSummaryCheckpoint summary = _floor?.ShiftSummary;
                GUILayout.Label("SHIFT " + (summary?.ShiftOrdinal ?? 0).ToString("D2") +
                    " / INSTITUTIONAL CLOSE", _title);
                GUILayout.Space(8f);
                GUILayout.BeginHorizontal();
                SummaryMetric("CLAIMS COMPLETED", summary?.ClaimsCompleted ?? 0);
                SummaryMetric("DEADLINES MISSED", summary?.DeadlinesMissed ?? 0);
                SummaryMetric("APPEALS CREATED", summary?.AppealsCreated ?? 0);
                SummaryMetric("APPEALS RESOLVED", summary?.AppealsResolved ?? 0);
                SummaryMetric("HOLDINGS", summary?.HoldingsEstablished ?? 0);
                SummaryMetric("SOCIETY CHANGES", summary?.SocietyChanges ?? 0);
                GUILayout.EndHorizontal();
                GUILayout.Space(16f);
                if ((_floor?.DraftChoices.Count ?? 0) > 0)
                {
                    GUILayout.Label("CHOOSE ONE INSTITUTIONAL DEVELOPMENT", _metric);
                    for (int i = 0; i < _floor.DraftChoices.Count; i++)
                    {
                        AutomationProcedureDraftChoiceCheckpoint choice =
                            _floor.DraftChoices[i];
                        string label = (i + 1) + "  " +
                            AutomationProcedureNames.ShortName(choice.Kind) +
                            " / TIER " + choice.ResultingTier;
                        var content = new GUIContent(label,
                            AutomationProcedureNames.Effect(
                                choice.Kind, choice.ResultingTier));
                        if (GUILayout.Button(content, GUILayout.Height(46f)))
                            _floor.ChooseProcedureDraft(i);
                        GUILayout.Label(AutomationProcedureNames.Effect(
                            choice.Kind, choice.ResultingTier), _small);
                    }
                }
                else if (GUILayout.Button(
                             "CONTINUE TO NEXT DOCKET [ENTER]",
                             GUILayout.Height(48f)))
                    _floor?.ContinueAfterShift();
            }
            else
            {
                AutomationBranchReviewCheckpoint review = _floor?.BranchReview;
                GUILayout.Label("BRANCH REVIEW / " +
                    OutcomeLabel(review?.Outcome ??
                        AutomationBranchOutcome.Certified), _title);
                GUILayout.Label(OutcomeDescription(review?.Outcome ??
                    AutomationBranchOutcome.Certified), _small);
                GUILayout.Space(14f);
                ReviewRow("Throughput", review?.Throughput ?? 0, false);
                ReviewRow("Deadline compliance",
                    review?.DeadlineCompliance ?? 0, false);
                ReviewRow("Avoidable error", review?.AvoidableError ?? 0, true);
                ReviewRow("Appeal reversal rate",
                    review?.AppealReversalRate ?? 0, true);
                ReviewRow("Unresolved liability",
                    review?.UnresolvedLiability ?? 0, true);
                ReviewRow("Society stability",
                    review?.SocietyStability ?? 0, false);
                ReviewRow("Institutional legitimacy",
                    review?.InstitutionalLegitimacy ?? 0, false);
                ReviewRow("Precedent consistency",
                    review?.PrecedentConsistency ?? 0, false);
                ReviewRow("Machine resilience",
                    review?.MachineResilience ?? 0, false);
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    "This result emerged from throughput, rulings, appeals, holdings, " +
                    "society state and machine resilience—not a narrative choice.", _small);
            }
            GUILayout.EndArea();
        }

        private void DrawPrecedentLedger()
        {
            IReadOnlyList<AutomationPrecedentRecord> precedents =
                _floor?.Precedents ?? Array.Empty<AutomationPrecedentRecord>();
            float width = Mathf.Min(720f, Screen.width - 40f);
            float height = Mathf.Min(460f, Screen.height - 130f);
            GUILayout.BeginArea(new Rect(
                Screen.width - width - 20f, 108f, width, height), GUI.skin.window);
            GUILayout.BeginHorizontal();
            GUILayout.Label("PRECEDENT LEDGER / REAL APPELLATE HOLDINGS", _title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("CLOSE [L]", GUILayout.Width(90f)))
                _precedentLedgerVisible = false;
            GUILayout.EndHorizontal();
            GUILayout.Label(
                "Choose how automated adjudication may use each holding. " +
                "Human review creates a physical Legal detour.", _small);
            GUILayout.Space(8f);
            if (precedents.Count == 0)
            {
                GUILayout.Label(
                    "No holdings installed. Resolve a qualifying appeal to create one.",
                    _small);
            }
            for (int i = 0; i < precedents.Count; i++)
            {
                AutomationPrecedentRecord precedent = precedents[i];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();
                GUILayout.Label(precedent.Issue.ToUpperInvariant(), _metric,
                    GUILayout.Width(180f));
                GUILayout.Label(precedent.Scope, _small, GUILayout.Width(150f));
                GUILayout.Label("MATCH " + precedent.CurrentMatchingCases +
                    " / USED " + precedent.AppliedCaseCount,
                    _small, GUILayout.Width(110f));
                GUILayout.Label("EXPOSURE " + precedent.LiabilityExposure,
                    _small, GUILayout.Width(92f));
                if (GUILayout.Button(ModeLabel(precedent.Mode),
                        GUILayout.Width(150f)))
                    _floor.CyclePrecedentMode(i);
                GUILayout.EndHorizontal();
                GUILayout.Label("Origin " + Compact(precedent.SourceAppealId) +
                    " / conflicts " + precedent.ConflictingHoldingCount,
                    _small);
                GUILayout.EndVertical();
            }
            GUILayout.EndArea();
        }

        private void DoctrineChoice(int number, string label, string description)
        {
            if (GUILayout.Button(label, GUILayout.Height(48f)))
                _floor?.SetPolicy(number);
            GUILayout.Label(description, _small);
            GUILayout.Space(8f);
        }

        private void SummaryMetric(string label, int value)
        {
            GUILayout.BeginVertical(GUILayout.Width(118f));
            GUILayout.Label(label, _small);
            GUILayout.Label(value.ToString("00"), _metric);
            GUILayout.EndVertical();
        }

        private void ReviewRow(string label, int value, bool lowerIsBetter)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label.ToUpperInvariant(), _small, GUILayout.Width(180f));
            GUILayout.HorizontalSlider(Mathf.Clamp(value, 0, 100), 0f, 100f,
                GUILayout.Width(420f));
            GUILayout.Label(value + (lowerIsBetter ? "% LOW" : "%"), _metric,
                GUILayout.Width(76f));
            GUILayout.EndHorizontal();
        }

        private static string ModeLabel(AutomationPrecedentMode mode)
        {
            return mode switch
            {
                AutomationPrecedentMode.MandatoryCitation => "MANDATORY",
                AutomationPrecedentMode.PermittedCitation => "PERMITTED",
                AutomationPrecedentMode.HumanReviewRequired => "HUMAN REVIEW",
                _ => "DO NOT AUTOMATE",
            };
        }

        private static string OutcomeLabel(AutomationBranchOutcome outcome)
        {
            return outcome switch
            {
                AutomationBranchOutcome.EfficientButHarmful =>
                    "EFFICIENT BUT HARMFUL",
                AutomationBranchOutcome.HumaneButInsolvent =>
                    "HUMANE BUT INSOLVENT",
                AutomationBranchOutcome.PrecedentCollapse =>
                    "PRECEDENT COLLAPSE",
                AutomationBranchOutcome.AdministrativeBlindness =>
                    "ADMINISTRATIVE BLINDNESS",
                _ => outcome.ToString().ToUpperInvariant(),
            };
        }

        private static string OutcomeDescription(AutomationBranchOutcome outcome)
        {
            return outcome switch
            {
                AutomationBranchOutcome.EfficientButHarmful =>
                    "The floor performs; the society absorbs the damage.",
                AutomationBranchOutcome.HumaneButInsolvent =>
                    "Consequences were contained, but the institution cannot sustain itself.",
                AutomationBranchOutcome.Captured =>
                    "One doctrine now dominates what the branch can recognise.",
                AutomationBranchOutcome.PrecedentCollapse =>
                    "Conflicting holdings have made automation institutionally unstable.",
                AutomationBranchOutcome.AdministrativeBlindness =>
                    "Official records look excellent while lived conditions diverge.",
                _ => "The branch is operationally stable and institutionally defensible.",
            };
        }

        private static string Compact(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "NONE";
            return value.Length <= 28 ? value : value.Substring(value.Length - 28);
        }

        private static string DefaultSavePath()
        {
            return Path.Combine(
                Application.persistentDataPath,
                "desk42-institutional-buildcraft-v0.4.json");
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.87f, 0.82f, 0.65f) },
            };
            _metric = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.88f, 0.67f, 0.28f) },
            };
            _small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.66f, 0.70f, 0.67f) },
            };
            _pill = new GUIStyle(GUI.skin.box)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.72f, 0.86f, 0.47f) },
            };
        }

        private static string ArgumentValue(string[] arguments, string key)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], key,
                        StringComparison.OrdinalIgnoreCase) && i + 1 < arguments.Length)
                    return arguments[i + 1];
                string prefix = key + "=";
                if (arguments[i].StartsWith(prefix,
                        StringComparison.OrdinalIgnoreCase))
                    return arguments[i].Substring(prefix.Length);
            }
            return string.Empty;
        }

        private static bool HasArgument(string[] arguments, string key)
        {
            for (int i = 0; i < arguments.Length; i++)
                if (string.Equals(arguments[i], key,
                        StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
