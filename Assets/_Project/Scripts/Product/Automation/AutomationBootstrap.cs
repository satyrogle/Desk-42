using System;
using System.Collections;
using System.Globalization;
using System.IO;
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
        private GUIStyle _title;
        private GUIStyle _metric;
        private GUIStyle _small;
        private GUIStyle _pill;

        public bool Ready => _floor != null;
        public int ClaimsInFlight => _floor?.ClaimsInFlight ?? 0;
        public int ClaimsCompleted => _floor?.ClaimsCompleted ?? 0;
        public long SocietyTick => _floor?.SocietyTick ?? 0;
        public int InstitutionalRulings => _floor?.InstitutionalRulings ?? 0;
        public int PrecedentsInstalled => _floor?.PrecedentsInstalled ?? 0;
        public int AppealsReturned => _floor?.AppealsReturned ?? 0;
        public int AppealsResolved => _floor?.AppealsResolved ?? 0;
        public int OverdueClaims => _floor?.OverdueCount ?? 0;
        public int OperationalReworks => _floor?.ReworkCount ?? 0;
        public int MachineJams => _floor?.JamCount ?? 0;
        public int SecondaryVerificationChecks => _floor?.SecondaryChecks ?? 0;
        public int UpgradeCredits => _floor?.Credits ?? 0;
        public string RoutePriority => _floor?.PriorityName ?? "BALANCED";
        public string AppealHandling => _floor?.AppealModeName ?? "FULL REHEARING";
        public int ProceduresBound => _floor?.ProceduresBound ?? 0;
        public int CurrentPolicyNumber => _floor?.PolicyNumber ?? 2;
        public bool AuxVerifierInstalled => _floor?.AuxVerifierPlaced ?? false;

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

        public bool BindProcedure(int procedureNumber)
        {
            return _floor?.BindProcedure(procedureNumber) == true;
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
            if (Input.GetKeyDown(KeyCode.B)) _floor?.TogglePlacementMode();
            if (Input.GetKeyDown(KeyCode.R)) _floor?.ToggleRouteMode();
            if (Input.GetKeyDown(KeyCode.Q)) _floor?.CycleRoutePriority();
            if (Input.GetKeyDown(KeyCode.E)) _floor?.CycleAppealMode();
            if (Input.GetKeyDown(KeyCode.LeftBracket)) _floor?.SelectNextStation();
            if (Input.GetKeyDown(KeyCode.U))
                _floor?.UpgradeSelected(AutomationUpgradeKind.Throughput);
            if (Input.GetKeyDown(KeyCode.C))
                _floor?.UpgradeSelected(AutomationUpgradeKind.Capacity);
            if (Input.GetKeyDown(KeyCode.F))
                _floor?.UpgradeSelected(AutomationUpgradeKind.Reliability);
            if (Input.GetKeyDown(KeyCode.X)) _floor?.RepairSelected();
            if (Input.GetKeyDown(KeyCode.F1)) _floor?.BindProcedure(1);
            if (Input.GetKeyDown(KeyCode.F2)) _floor?.BindProcedure(2);
            if (Input.GetKeyDown(KeyCode.F3)) _floor?.BindProcedure(3);
            if (Input.GetKeyDown(KeyCode.F4)) _floor?.BindProcedure(4);
            if (Input.GetKeyDown(KeyCode.F5)) _floor?.BindProcedure(5);
            if (Input.GetKeyDown(KeyCode.F6)) _floor?.BindProcedure(6);
            if (Input.GetKeyDown(KeyCode.Alpha1)) _floor?.SetPolicy(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) _floor?.SetPolicy(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) _floor?.SetPolicy(3);
            if (Input.GetMouseButtonDown(0) &&
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
            GUILayout.Label("BIND PROCEDURES " + (_floor?.ProceduresBound ?? 0) + "/2",
                _small, GUILayout.Width(105f));
            ProcedureButton(1, "F1 SECOND CHECK");
            ProcedureButton(2, "F2 PRESUME VALID");
            ProcedureButton(3, "F3 ADVERSE REVIEW");
            ProcedureButton(4, "F4 PROTECTED LANE");
            ProcedureButton(5, "F5 APPEAL FAST");
            ProcedureButton(6, "F6 PRECEDENT REUSE");
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
            if (GUILayout.Button(decorated, GUILayout.Width(150f), GUILayout.Height(27f)))
                _floor?.SetPolicy(number);
        }

        private void ProcedureButton(int number, string label)
        {
            bool bound = _floor?.IsProcedureBound(number) == true;
            GUI.enabled = !bound && (_floor?.ProceduresBound ?? 0) < 2;
            string decorated = bound ? "[ " + label + " ]" : label;
            var content = new GUIContent(decorated,
                AutomationProcedureNames.Effect((AutomationProcedureKind)number));
            if (GUILayout.Button(content, GUILayout.Width(150f), GUILayout.Height(27f)))
                _floor?.BindProcedure(number);
            GUI.enabled = true;
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
