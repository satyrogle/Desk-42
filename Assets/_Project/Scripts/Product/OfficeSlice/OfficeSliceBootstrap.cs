using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Desk42.Institutional.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Desk42.Product.OfficeSlice
{
    [DisallowMultipleComponent]
    public sealed class OfficeSliceBootstrap : MonoBehaviour
    {
        private const string OfficeSliceArgument = "--desk42-office-slice";
        private const string CaptureArgument = "--desk42-office-slice-capture";
        private const string PerformanceArgument =
            "--desk42-office-slice-performance-smoke";
        private const string CaptureDistributionArgument =
            "--desk42-office-slice-capture-distribution";

        private readonly Dictionary<string, Transform> _folderViews =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, TextMesh> _folderLabels =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, Renderer> _folderRenderers =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> _customerViews =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> _staffViews =
            new(StringComparer.Ordinal);
        private readonly List<Material> _runtimeMaterials = new();

        private OfficeCaseRepository _caseRepository;
        private OfficeCampaignState _campaignState;
        private OfficeSimulationState _simulationState;
        private OfficeTickDriver _tickDriver;
        private Transform _runtimeRoot;
        private Transform _wardenView;
        private Camera _camera;
        private bool _built;
        private string _lastDebugMessage = "BOOTING OFFICE SLICE";

        public OfficeCaseRepository CaseRepository => _caseRepository;
        public OfficeCampaignState CampaignState => _campaignState;
        public OfficeSimulationState SimulationState => _simulationState;
        public bool Ready => _built && _simulationState != null && _caseRepository != null;
        public bool CriticalRoutesValid => Ready && ValidateCriticalRoutes();
        public int VisibleFolderCount => _folderViews.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RouteDevelopmentPlayerToOfficeSlice()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            if (!HasArgument(arguments, OfficeSliceArgument)) return;

            int sceneIndex = SceneUtility.GetBuildIndexByScenePath(
                "Assets/_Project/Scenes/OfficeSlice.unity");
            if (sceneIndex >= 0 && SceneManager.GetActiveScene().buildIndex != sceneIndex)
                SceneManager.LoadScene(sceneIndex);
        }

        private void Awake()
        {
            if (_built) return;
            name = "Office Slice Bootstrap";
            _campaignState = OfficeCampaignState.Create();
            _simulationState = _campaignState.CurrentSimulation;
            _caseRepository = _simulationState.Cases;
            RebuildRuntimePresentation();
            _tickDriver = gameObject.AddComponent<OfficeTickDriver>();
            _tickDriver.Initialize(this, _simulationState);
            _built = true;
            RefreshPresentation();
            _lastDebugMessage = "SIX PUBLIC CASES READY";
        }

        private IEnumerator Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            string capturePath = ArgumentValue(arguments, CaptureArgument);
            string performancePath = ArgumentValue(arguments, PerformanceArgument);
            if (string.IsNullOrWhiteSpace(capturePath) &&
                string.IsNullOrWhiteSpace(performancePath)) yield break;

            yield return null;
            if (!string.IsNullOrWhiteSpace(performancePath))
            {
                yield return RunPerformanceSmoke(performancePath);
                yield break;
            }
            if (HasArgument(arguments, CaptureDistributionArgument))
                PrepareCaptureDistribution();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForEndOfFrame();

            string fullPath = Path.GetFullPath(capturePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
            if (File.Exists(fullPath)) File.Delete(fullPath);
            ScreenCapture.CaptureScreenshot(fullPath);
            for (int frame = 0; frame < 600 &&
                (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0); frame++)
                yield return null;
            if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
            {
                Debug.LogError("OFFICE_SLICE_CAPTURE_FAILED " + fullPath, this);
                Application.Quit(1);
                yield break;
            }
            Debug.Log("OFFICE_SLICE_CAPTURE_OK " + fullPath, this);
            Application.Quit(0);
        }

        private IEnumerator RunPerformanceSmoke(string outputPath)
        {
            const int warmupFrames = 60;
            const int sampleFrames = 600;
            const double targetFps = 60d;

            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
            var endOfFrame = new WaitForEndOfFrame();
            for (int frame = 0; frame < warmupFrames; frame++)
                yield return endOfFrame;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            double previousSeconds = stopwatch.Elapsed.TotalSeconds;
            double worstFrameSeconds = 0d;
            for (int frame = 0; frame < sampleFrames; frame++)
            {
                yield return endOfFrame;
                double currentSeconds = stopwatch.Elapsed.TotalSeconds;
                double frameSeconds = currentSeconds - previousSeconds;
                if (frameSeconds > worstFrameSeconds)
                    worstFrameSeconds = frameSeconds;
                previousSeconds = currentSeconds;
            }
            stopwatch.Stop();

            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            double averageFps = sampleFrames / elapsedSeconds;
            string fullPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
            var report = new StringBuilder(256);
            report.AppendLine("OFFICE_SLICE_PERFORMANCE_V1");
            report.Append("resolution=").Append(Screen.width).Append('x')
                .AppendLine(Screen.height.ToString(CultureInfo.InvariantCulture));
            report.Append("frames=").AppendLine(
                sampleFrames.ToString(CultureInfo.InvariantCulture));
            report.Append("elapsed_seconds=").AppendLine(
                elapsedSeconds.ToString("F6", CultureInfo.InvariantCulture));
            report.Append("average_fps=").AppendLine(
                averageFps.ToString("F2", CultureInfo.InvariantCulture));
            report.Append("worst_frame_ms=").AppendLine(
                (worstFrameSeconds * 1000d).ToString("F2", CultureInfo.InvariantCulture));
            report.Append("simulation_ticks=").AppendLine(
                _simulationState.CurrentTick.ToString(CultureInfo.InvariantCulture));
            report.Append("target_fps=").AppendLine(
                targetFps.ToString("F0", CultureInfo.InvariantCulture));
            report.Append("target_met=").AppendLine(
                (averageFps >= targetFps).ToString());
            File.WriteAllText(fullPath, report.ToString());

            Debug.Log("OFFICE_SLICE_PERFORMANCE_OK " +
                averageFps.ToString("F2", CultureInfo.InvariantCulture) +
                " FPS " + fullPath, this);
            Application.Quit(averageFps >= targetFps ? 0 : 2);
        }

        private void LateUpdate()
        {
            SynchronizeCampaignState();
            RefreshPresentation();
        }

        public bool SynchronizeCampaignState()
        {
            if (_campaignState == null ||
                ReferenceEquals(_simulationState,
                    _campaignState.CurrentSimulation)) return false;
            _simulationState = _campaignState.CurrentSimulation;
            _caseRepository = _simulationState.Cases;
            RebuildRuntimePresentation();
            if (_tickDriver != null)
                _tickDriver.ReplaceState(_simulationState, paused: false);
            _lastDebugMessage = "SHIFT " +
                _campaignState.CurrentShiftOrdinal + " READY / SIX PUBLIC CASES";
            return true;
        }

        public void RefreshPresentation()
        {
            if (!Ready) return;
            OfficeCell wardenCell = _simulationState.Warden.Cell(_simulationState.Grid);
            if (_wardenView != null)
            {
                _wardenView.position = new Vector3(
                    _simulationState.Warden.XSubunits / (float)OfficeGrid.LogicalSubunitsPerCell,
                    0.52f,
                    _simulationState.Warden.ZSubunits / (float)OfficeGrid.LogicalSubunitsPerCell);
            }

            IReadOnlyList<string> folderIds = _simulationState.Queues.FolderIds;
            for (int i = 0; i < folderIds.Count; i++)
            {
                string caseId = folderIds[i];
                OfficeFolderState folder = _simulationState.Queues.GetFolder(caseId);
                if (folder.IsCopy && !_folderViews.ContainsKey(caseId))
                    CreateCopyFolderView(folder);
                if (!_folderViews.TryGetValue(caseId, out Transform view)) continue;
                view.gameObject.SetActive(
                    folder.OwnerKind != OfficeFolderOwnerKind.Cleared);
                if (folder.OwnerKind == OfficeFolderOwnerKind.Cleared) continue;

                int queueIndex = QueueIndex(folder.CurrentRoom, caseId);
                Vector3 destination = SocketWorldPosition(folder.CurrentRoom, queueIndex);
                if (folder.OwnerKind == OfficeFolderOwnerKind.Warden &&
                    _wardenView != null)
                {
                    destination = _wardenView.position + new Vector3(0.55f, 0.15f, 0f);
                }
                else if (folder.OwnerKind == OfficeFolderOwnerKind.Runner &&
                    _staffViews.TryGetValue(folder.OwnerId, out Transform staffView))
                {
                    destination = staffView.position + new Vector3(0.55f, 0.15f, 0f);
                }
                else if (folder.IsMoving)
                {
                    Vector3 source = SocketWorldPosition(folder.SourceRoom, 0);
                    Vector3 target = SocketWorldPosition(folder.DestinationRoom, 0);
                    destination = Vector3.Lerp(
                        source,
                        target,
                        folder.ProgressAt(_simulationState.CurrentTick));
                }
                view.position = destination;
                if (_folderLabels.TryGetValue(caseId, out TextMesh label))
                    label.text = folder.IsCopy
                        ? caseId.StartsWith("time-slip.", StringComparison.Ordinal)
                            ? "TIME SLIP"
                            : "COPY"
                        : _caseRepository.Get(caseId)?.DisplayId ?? caseId;
                if (_folderRenderers.TryGetValue(caseId, out Renderer renderer))
                {
                    OfficeCase sourceCase = _caseRepository.Get(folder.SourceCaseId);
                    renderer.sharedMaterial.color = IsHighlightedFolder(folder)
                        ? new Color(1f, 0.88f, 0.22f)
                        : folder.IsCopy
                            ? caseId.StartsWith("time-slip.", StringComparison.Ordinal)
                                ? new Color(0.52f, 0.72f, 0.95f)
                                : new Color(0.92f, 0.35f, 0.32f)
                            : FolderColor(sourceCase.Urgency);
                }
            }

            RefreshCustomerViews();
            RefreshStaffViews();
            UpdateBillboards(wardenCell);
        }

        public void ForceAllFoldersThroughM1Route()
        {
            _simulationState.ForceAllFoldersThroughM1Route();
            _lastDebugMessage = "FORCED ROUTE COMPLETE / FRONT > PAPER > MONEY > WEIRD > FRONT";
            RefreshPresentation();
        }

        public void PrepareCaptureDistribution()
        {
            IReadOnlyList<string> folderIds = _simulationState.Queues.FolderIds;
            for (int i = 0; i < folderIds.Count; i++)
            {
                int stages = i % 4;
                for (int stage = 0; stage < stages; stage++)
                {
                    OfficeCommand command = _simulationState.CreateSendCommand(folderIds[i]);
                    if (!_simulationState.TryQueueCommand(command, out OfficeCommandFailure failure))
                        throw new InvalidOperationException(failure.ToString());
                    _simulationState.AdvanceOneTick();
                    _simulationState.AdvanceTicks(OfficeQueueService.DefaultTransferDurationTicks);
                }
            }
            _lastDebugMessage = "CAPTURE DISTRIBUTION / SIX STABLE FOLDERS";
            RefreshPresentation();
        }

        public void ReplayRecordedCommands()
        {
            OfficeCommandLog source = _simulationState.CommandLog;
            _simulationState = OfficeSimulationState.CreateM2Replay(source);
            _caseRepository = _simulationState.Cases;
            _tickDriver.ReplaceState(_simulationState);
            _lastDebugMessage = "REPLAY MODE / LIVE INPUT DISABLED";
            RefreshPresentation();
        }

        public bool RestartShift()
        {
            if (!Ready || !_simulationState.Shift.RestartRequested) return false;
            if (_campaignState != null)
            {
                if (!_campaignState.TryRestartCurrentShift()) return false;
                _simulationState = _campaignState.CurrentSimulation;
                _caseRepository = _simulationState.Cases;
                RebuildRuntimePresentation();
                _tickDriver.ReplaceState(_simulationState, paused: false);
                _lastDebugMessage = "SHIFT RESTARTED FROM CAMPAIGN CHECKPOINT";
                RefreshPresentation();
                return true;
            }
            RebuildAsStandaloneM2();
            return true;
        }

        private void RebuildAsStandaloneM2()
        {
            _simulationState = OfficeSimulationState.CreateM2();
            _caseRepository = _simulationState.Cases;
            RebuildRuntimePresentation();
            _tickDriver.ReplaceState(_simulationState, paused: false);
            _lastDebugMessage = "SHIFT RESTARTED FROM CLEAN CHECKPOINT";
            RefreshPresentation();
        }

        private void RebuildRuntimePresentation()
        {
            if (_runtimeRoot != null)
            {
                _runtimeRoot.gameObject.SetActive(false);
                Destroy(_runtimeRoot.gameObject);
            }
            for (int i = 0; i < _runtimeMaterials.Count; i++)
                if (_runtimeMaterials[i] != null) Destroy(_runtimeMaterials[i]);
            _runtimeMaterials.Clear();
            _folderViews.Clear();
            _folderLabels.Clear();
            _folderRenderers.Clear();
            _customerViews.Clear();
            _staffViews.Clear();
            _runtimeRoot = new GameObject("Office Slice Runtime").transform;
            _runtimeRoot.SetParent(transform, false);
            BuildGreybox();
        }

        public void SaveCommandLog()
        {
            string path = Path.Combine(
                Application.persistentDataPath,
                "desk42-office-slice-m1-commands.json");
            File.WriteAllText(path, _simulationState.CommandLog.ToJson());
            _lastDebugMessage = "COMMAND LOG SAVED / " + path;
        }

        public bool ValidateCriticalRoutes()
        {
            OfficeGrid grid = _simulationState.Grid;
            if (!grid.TryFindPath(grid.SpawnCell, grid.InteractionPoints[0].Cell,
                    out List<OfficeCell> ignored)) return false;
            for (int i = 0; i < grid.InteractionPoints.Count; i++)
            {
                OfficeInteractionPoint point = grid.InteractionPoints[i];
                if (!grid.TryFindPath(grid.SpawnCell, point.Cell, out ignored)) return false;
            }
            return true;
        }

        public string QueueSummary()
        {
            var builder = new System.Text.StringBuilder();
            foreach (OfficeRoomId room in Enum.GetValues(typeof(OfficeRoomId)))
            {
                if (builder.Length > 0) builder.Append(" / ");
                builder.Append(room).Append(':');
                IReadOnlyList<string> ids = _simulationState.Queues.GetQueue(room).CaseIds;
                for (int i = 0; i < ids.Count; i++)
                {
                    if (i > 0) builder.Append(',');
                    builder.Append(ids[i]);
                }
            }
            return builder.ToString();
        }

        private void BuildGreybox()
        {
            CreateCamera();
            CreateLighting();
            CreateFloorAndWalls();
            CreateRooms();
            CreateMachineViews();
            CreateWarden();
            CreateFolderViews();
            CreateCustomerViews();
            CreateStaffViews();
        }

        private void CreateCamera()
        {
            GameObject cameraObject = new("Office Slice Camera");
            cameraObject.transform.SetParent(_runtimeRoot, false);
            cameraObject.transform.position = new Vector3(12f, 18f, -16f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 0f));
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 12f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.035f, 0.045f, 0.06f);
            if (Camera.main == null) cameraObject.tag = "MainCamera";
        }

        private void CreateLighting()
        {
            GameObject lightObject = new("Office Slice Key Light");
            lightObject.transform.SetParent(_runtimeRoot, false);
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(0.84f, 0.9f, 1f);
        }

        private void CreateFloorAndWalls()
        {
            CreateCube("Greybox Floor", new Vector3(0f, -0.35f, 0f),
                new Vector3(29f, 0.5f, 19f), new Color(0.09f, 0.12f, 0.16f));
            Color wall = new(0.24f, 0.28f, 0.34f);
            CreateCube("North Wall", new Vector3(0f, 0.6f, 8.7f),
                new Vector3(28f, 1.4f, 0.35f), wall);
            CreateCube("South Wall", new Vector3(0f, 0.6f, -8.7f),
                new Vector3(28f, 1.4f, 0.35f), wall);
            CreateCube("West Wall", new Vector3(-13.7f, 0.6f, 0f),
                new Vector3(0.35f, 1.4f, 17f), wall);
            CreateCube("East Wall", new Vector3(13.7f, 0.6f, 0f),
                new Vector3(0.35f, 1.4f, 17f), wall);
        }

        private void CreateRooms()
        {
            CreateRoom(OfficeRoomId.FrontDesk, new Vector3(-9f, 0.03f, 5f),
                new Vector3(9f, 0.18f, 5.5f), new Color(0.14f, 0.29f, 0.31f));
            CreateRoom(OfficeRoomId.PaperRoom, new Vector3(0f, 0.03f, 5f),
                new Vector3(7f, 0.18f, 5.5f), new Color(0.27f, 0.29f, 0.18f));
            CreateRoom(OfficeRoomId.MoneyRoom, new Vector3(8.5f, 0.03f, 5f),
                new Vector3(6f, 0.18f, 5.5f), new Color(0.2f, 0.3f, 0.23f));
            CreateRoom(OfficeRoomId.WeirdRoom, new Vector3(8.5f, 0.03f, -3.5f),
                new Vector3(6f, 0.18f, 5.5f), new Color(0.3f, 0.2f, 0.3f));
            CreateRoom(OfficeRoomId.WaitingArea, new Vector3(-1f, 0.03f, -3.5f),
                new Vector3(7f, 0.18f, 5.5f), new Color(0.24f, 0.24f, 0.28f));

            for (int i = 0; i < _simulationState.Grid.InteractionPoints.Count; i++)
            {
                OfficeInteractionPoint point = _simulationState.Grid.InteractionPoints[i];
                Vector3 position = CellToWorld(point.Cell, 0.2f);
                CreateCube(point.Id + " / interaction", position,
                    new Vector3(0.5f, 0.12f, 0.5f), new Color(0.9f, 0.72f, 0.28f));
                CreateLabel(point.Id, position + Vector3.up * 0.25f, 0.055f);
            }

            for (int roomIndex = 0; roomIndex < 4; roomIndex++)
            {
                OfficeRoomId room = (OfficeRoomId)roomIndex;
                for (int socket = 0; socket < _caseRepository.Cases.Count; socket++)
                {
                    Vector3 position = CellToWorld(
                        _simulationState.Grid.SocketCell(room, socket), 0.22f);
                    CreateCube(room + " socket " + socket, position,
                        new Vector3(0.46f, 0.1f, 0.46f), new Color(0.22f, 0.55f, 0.56f));
                }
            }
        }

        private void CreateRoom(
            OfficeRoomId room,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            CreateCube(room + " greybox", position, scale, color);
            CreateLabel(RoomLabel(room), position + Vector3.up * 0.18f, 0.12f);
        }

        private void CreateWarden()
        {
            GameObject warden = CreateCube("Warden", Vector3.zero,
                new Vector3(0.55f, 0.9f, 0.55f), new Color(0.91f, 0.72f, 0.25f));
            _wardenView = warden.transform;
            CreateLabel("WARDEN", Vector3.up * 1.1f, 0.09f, _wardenView);
        }

        private void CreateFolderViews()
        {
            IReadOnlyList<string> ids = _simulationState.Queues.FolderIds;
            for (int i = 0; i < ids.Count; i++)
            {
                string caseId = ids[i];
                OfficeCase officeCase = _caseRepository.Get(caseId);
                GameObject folder = CreateCube("Folder " + officeCase.DisplayId,
                    Vector3.zero, new Vector3(0.62f, 0.16f, 0.42f),
                    FolderColor(officeCase.Urgency));
                _folderViews.Add(caseId, folder.transform);
                TextMesh label = CreateLabel(officeCase.DisplayId,
                    Vector3.up * 0.18f, 0.055f, folder.transform);
                _folderLabels.Add(caseId, label);
                _folderRenderers.Add(caseId, folder.GetComponent<Renderer>());
            }
        }

        private void CreateCopyFolderView(OfficeFolderState folder)
        {
            GameObject copy = CreateCube("Copied Folder " + folder.CaseId,
                Vector3.zero, new Vector3(0.62f, 0.16f, 0.42f),
                folder.CaseId.StartsWith("time-slip.", StringComparison.Ordinal)
                    ? new Color(0.52f, 0.72f, 0.95f)
                    : new Color(0.92f, 0.35f, 0.32f));
            _folderViews.Add(folder.CaseId, copy.transform);
            TextMesh label = CreateLabel(
                folder.CaseId.StartsWith("time-slip.", StringComparison.Ordinal)
                    ? "TIME SLIP"
                    : "COPY",
                Vector3.up * 0.18f,
                0.055f, copy.transform);
            _folderLabels.Add(folder.CaseId, label);
            _folderRenderers.Add(folder.CaseId, copy.GetComponent<Renderer>());
        }

        private void CreateMachineViews()
        {
            CreateCube("Auto Sorter", new Vector3(10f, 0.65f, -3.5f),
                new Vector3(1.3f, 1.3f, 1.3f), new Color(0.25f, 0.65f, 0.62f));
            CreateLabel("AUTO SORTER", new Vector3(10f, 1.55f, -3.5f), 0.07f);
            CreateCube("Copy Echo", new Vector3(5.5f, 0.65f, -3.5f),
                new Vector3(1.3f, 1.3f, 1.3f), new Color(0.62f, 0.35f, 0.58f));
            CreateLabel("COPY ECHO", new Vector3(5.5f, 1.55f, -3.5f), 0.07f);
            if (_campaignState != null && _campaignState.CurrentShiftOrdinal >= 2)
            {
                CreateCube("Clock Terminal", new Vector3(2.2f, 0.55f, 5f),
                    new Vector3(0.8f, 1.1f, 0.8f), new Color(0.3f, 0.52f, 0.78f));
                CreateLabel("GHOST CLOCK", new Vector3(2.2f, 1.35f, 5f), 0.06f);
                CreateCube("Missing Room Door", new Vector3(12f, 0.9f, -3.5f),
                    new Vector3(0.25f, 1.8f, 1.5f), new Color(0.5f, 0.4f, 0.25f));
                CreateLabel("MISSING ROOM", new Vector3(12f, 2f, -3.5f), 0.06f);
            }
            if (_campaignState?.Upgrades.FastTraysTier > 0)
            {
                CreateCube("Fast Tray Upgrade", new Vector3(-6.5f, 0.3f, 5f),
                    new Vector3(1.5f, 0.25f, 0.8f), new Color(0.2f, 0.75f, 0.72f));
                CreateLabel("FAST TRAYS", new Vector3(-6.5f, 0.75f, 5f), 0.055f);
            }
            if (_campaignState?.Upgrades.CalmChairsTier > 0)
            {
                CreateCube("Calm Chair Upgrade", new Vector3(-2f, 0.4f, -3.5f),
                    new Vector3(0.8f, 0.8f, 0.8f), new Color(0.4f, 0.62f, 0.45f));
                CreateLabel("CALM CHAIRS", new Vector3(-2f, 1.05f, -3.5f), 0.055f);
            }
        }

        private void CreateCustomerViews()
        {
            IReadOnlyList<OfficeCustomerState> customers =
                _simulationState.Customers.Customers;
            for (int i = 0; i < customers.Count; i++)
            {
                OfficeCustomerState customer = customers[i];
                GameObject body = CreateCube("Customer " + customer.DisplayName,
                    Vector3.zero, new Vector3(0.62f, 1.05f, 0.62f),
                    new Color(0.42f, 0.63f, 0.78f));
                CreateLabel(customer.DisplayName, Vector3.up * 1.25f,
                    0.065f, body.transform);
                _customerViews.Add(customer.CustomerId, body.transform);
            }
            RefreshCustomerViews();
        }

        private void RefreshCustomerViews()
        {
            if (_simulationState.Customers == null) return;
            IReadOnlyList<OfficeCustomerState> customers =
                _simulationState.Customers.Customers;
            int waitingIndex = 0;
            for (int i = 0; i < customers.Count; i++)
            {
                OfficeCustomerState customer = customers[i];
                if (!_customerViews.TryGetValue(customer.CustomerId,
                        out Transform view)) continue;
                bool visible = customer.QueueState == OfficeCustomerQueueState.AtDesk ||
                    customer.QueueState == OfficeCustomerQueueState.Waiting;
                view.gameObject.SetActive(visible);
                if (!visible) continue;
                view.position = customer.QueueState == OfficeCustomerQueueState.AtDesk
                    ? new Vector3(-10f, 0.55f, 7f)
                    : new Vector3(-3f + waitingIndex++ * 1.1f, 0.55f, -3f);
            }
        }

        private bool IsHighlightedFolder(OfficeFolderState folder)
        {
            OfficeCustomerState active = _simulationState.Customers.ActiveDeskCustomer;
            if (active == null || !string.Equals(active.LinkedAutomationClaimId,
                    folder.CaseId, StringComparison.Ordinal)) return false;
            if (folder.OwnerKind == OfficeFolderOwnerKind.Warden) return true;
            OfficeInteractionPoint point = _simulationState.Grid.ChooseClosestInteractionPoint(
                _simulationState.Warden.Cell(_simulationState.Grid));
            return point != null && !folder.IsMoving &&
                folder.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                folder.CurrentRoom == point.Room;
        }

        private void CreateStaffViews()
        {
            for (int i = 0; i < _simulationState.Staff.Staff.Count; i++)
            {
                OfficeStaffState staff = _simulationState.Staff.Staff[i];
                Color color = staff.Role == OfficeStaffRole.Runner
                    ? new Color(0.38f, 0.75f, 0.45f)
                    : new Color(0.72f, 0.48f, 0.82f);
                GameObject body = CreateCube(staff.DisplayName,
                    Vector3.zero, new Vector3(0.52f, 0.82f, 0.52f), color);
                CreateLabel(staff.DisplayName, Vector3.up * 1.02f,
                    0.06f, body.transform);
                _staffViews.Add(staff.StaffId, body.transform);
            }
            RefreshStaffViews();
        }

        private void RefreshStaffViews()
        {
            if (_simulationState.Staff == null) return;
            for (int i = 0; i < _simulationState.Staff.Staff.Count; i++)
            {
                OfficeStaffState staff = _simulationState.Staff.Staff[i];
                if (!_staffViews.TryGetValue(staff.StaffId, out Transform view)) continue;
                view.position = new Vector3(
                    staff.XSubunits / (float)OfficeGrid.LogicalSubunitsPerCell,
                    0.43f,
                    staff.ZSubunits / (float)OfficeGrid.LogicalSubunitsPerCell);
            }
        }

        private GameObject CreateCube(
            string objectName,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.transform.SetParent(_runtimeRoot, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            Collider collider = cube.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            cube.GetComponent<Renderer>().sharedMaterial = CreateMaterial(color);
            return cube;
        }

        private TextMesh CreateLabel(
            string text,
            Vector3 position,
            float characterSize,
            Transform parent = null)
        {
            GameObject labelObject = new(text);
            labelObject.transform.SetParent(parent ?? _runtimeRoot, false);
            labelObject.transform.position = position;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = 48;
            label.characterSize = characterSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.white;
            return label;
        }

        private Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Desk42/AutomationLit") ??
                Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            if (shader == null)
                throw new MissingReferenceException(
                    "A runtime-safe greybox shader could not be resolved.");
            var material = new Material(shader) { color = color };
            _runtimeMaterials.Add(material);
            return material;
        }

        private Vector3 SocketWorldPosition(OfficeRoomId room, int queueIndex)
        {
            return CellToWorld(_simulationState.Grid.SocketCell(room, queueIndex), 0.5f);
        }

        private Vector3 CellToWorld(OfficeCell cell, float y)
        {
            return new Vector3(cell.X, y, cell.Z);
        }

        private int QueueIndex(OfficeRoomId room, string caseId)
        {
            IReadOnlyList<string> ids = _simulationState.Queues.GetQueue(room).CaseIds;
            for (int i = 0; i < ids.Count; i++)
                if (string.Equals(ids[i], caseId, StringComparison.Ordinal)) return i;
            return 0;
        }

        private void UpdateBillboards(OfficeCell wardenCell)
        {
            if (_camera == null) return;
            for (int i = 0; i < _runtimeRoot.childCount; i++)
            {
                Transform child = _runtimeRoot.GetChild(i);
                TextMesh text = child.GetComponent<TextMesh>();
                if (text == null) continue;
                child.rotation = Quaternion.LookRotation(child.position - _camera.transform.position);
            }
        }

        private static string RoomLabel(OfficeRoomId room)
        {
            return room switch
            {
                OfficeRoomId.FrontDesk => "FRONT DESK",
                OfficeRoomId.PaperRoom => "PAPER ROOM",
                OfficeRoomId.MoneyRoom => "MONEY ROOM",
                OfficeRoomId.WeirdRoom => "WEIRD ROOM",
                OfficeRoomId.WaitingArea => "WAITING AREA",
                _ => room.ToString().ToUpperInvariant(),
            };
        }

        private static Color FolderColor(OfficeCaseUrgency urgency)
        {
            return urgency switch
            {
                OfficeCaseUrgency.Critical => new Color(0.9f, 0.28f, 0.22f),
                OfficeCaseUrgency.Urgent => new Color(0.95f, 0.55f, 0.2f),
                OfficeCaseUrgency.Elevated => new Color(0.75f, 0.78f, 0.3f),
                _ => new Color(0.45f, 0.74f, 0.78f),
            };
        }

        private void OnGUI()
        {
            if (!Ready) return;
            GUILayout.BeginArea(new Rect(16f, 16f, 540f, 440f), GUI.skin.box);
            GUILayout.Label("DESK 42 / TODAY'S DESK");
            if (_campaignState != null)
                GUILayout.Label("DAY " + _campaignState.CurrentShiftOrdinal +
                    " / " + _campaignState.CurrentShift.Title);
            GUILayout.Label("SHIFT: " +
                _simulationState.Shift.Phase.ToString().ToUpperInvariant());
            if (_campaignState != null &&
                _campaignState.Phase == OfficeCampaignPhase.ChooseUpgrade)
            {
                if (_campaignState.CurrentShiftOrdinal == 1)
                {
                    GUILayout.Label("THE OFFICE SURVIVED.");
                    GUILayout.Label("CHOOSE ONE CHANGE FOR TOMORROW.");
                }
                else
                {
                    GUILayout.Label("THE MACHINE NOW KNOWS TWO RULES.");
                    GUILayout.Label("CHOOSE WHAT THIS OFFICE BECOMES BETTER AT.");
                }
                GUILayout.Label("1 FAST TRAYS     2 CALM CHAIRS     3 RED LABELS");
            }
            else if (_campaignState != null &&
                _campaignState.Phase == OfficeCampaignPhase.ReadyForNextShift)
            {
                GUILayout.Label("OFFICE UPGRADE CHOSEN");
                GUILayout.Label("E / SPACE / A: NEXT SHIFT");
            }
            OfficeCustomerState customer =
                _simulationState.Customers.ActiveDeskCustomer;
            if (customer == null)
            {
                GUILayout.Label("NO CUSTOMER AT THE DESK");
            }
            else
            {
                GUILayout.Label("CUSTOMER: " + customer.DisplayName);
                GUILayout.Label(customer.Problem);
                GUILayout.Label("MOOD: " + customer.VisibleMoodState.ToString().ToUpperInvariant());
                OfficeCustomerPressureRecord pressure =
                    _simulationState.CustomerPressure.RecordFor(customer.CustomerId);
                GUILayout.Label("WHY: " + pressure.LastAuthoredCause);
                GUILayout.Label("FOLDER: " + FolderStatus(customer.LinkedAutomationClaimId));
            }
            GUILayout.Space(8f);
            DrawCurrentWork(customer);
            GUILayout.Space(8f);
            GUILayout.Label("E / SPACE / A: " + _simulationState.PrimaryActionLabel);
            GUILayout.Label("WASD / ARROWS / LEFT STICK: MOVE");
            GUILayout.Label("CHOICES: 1-4 / X-Y-LB-RB");
            GUILayout.Label("Q / B: PUT DOWN");
            GUILayout.Label("R / VIEW: AUTO SORTER " +
                (_simulationState.AutomationRule.Enabled ? "ON" :
                    _simulationState.AutomationRule.Unlocked ? "OFF" : "LOCKED"));
            if (_simulationState.AutomationRule.Unlocked)
                GUILayout.Label(OfficeAutomationRuleState.PlayerRule);
            if (_campaignState != null && _campaignState.CurrentShiftOrdinal >= 2)
            {
                GUILayout.Label("T / RIGHT STICK: PAY RULE " +
                    (_simulationState.PayrollRule.Enabled ? "ON" :
                        _simulationState.PayrollRule.Unlocked ? "OFF" : "LOCKED"));
                if (_simulationState.PayrollRule.Unlocked)
                    GUILayout.Label(OfficePayrollRuleState.PlayerRule);
            }
            GUILayout.Label("3 RUNNER     4 TALKER");
            for (int i = 0; i < _simulationState.Staff.Staff.Count; i++)
            {
                OfficeStaffState staff = _simulationState.Staff.Staff[i];
                GUILayout.Label(staff.DisplayName + ": " + staff.VisibleIntent);
            }
            if (_simulationState.CustomerPressure.CalmActive)
                GUILayout.Label("CALMING: " +
                    _simulationState.CustomerPressure.CalmRemainingTicks + " TICKS");
            if (_simulationState.BreakState.Active)
                GUILayout.Label(_simulationState.BreakState.Recovered
                    ? "OFFICE FIXED"
                    : "COPY ECHO: FIX MACHINE / CLEAR COPIES / FIND ORIGINAL");
            if (_simulationState.GhostClock.Active)
                GUILayout.Label("GHOST CLOCK: KEEP TOMAS CALM / STOP CLOCK / CLEAR SLIPS");
            if (_simulationState.MissingRoomAccess.Active)
                GUILayout.Label("MISSING ROOM OPEN: CLOSE DOOR OR FINISH IRIS'S CASE");
            if (_simulationState.Shift.Failed)
                GUILayout.Label(_simulationState.Shift.FailureReason +
                    " / ENTER OR START TO RESTART");
            if (_simulationState.Shift.Phase == OfficeShiftPhase.Result)
                DrawCausalRecap();
            GUILayout.EndArea();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.BeginArea(new Rect(16f, 470f, 700f, 205f), GUI.skin.box);
            GUILayout.Label(_campaignState == null
                ? "M2 ENGINEERING EVIDENCE"
                : "M3 CAMPAIGN ENGINEERING EVIDENCE");
            if (_campaignState != null)
                GUILayout.Label("CAMPAIGN " + _campaignState.Phase +
                    " / " + _campaignState.Checksum);
            GUILayout.Label("TICK " + _simulationState.CurrentTick +
                " / CHECKSUM " + _simulationState.Checksum);
            GUILayout.Label("WARDEN " + _simulationState.Warden.Cell(_simulationState.Grid) +
                " / COMMANDS " + _simulationState.CommandLog.Commands.Count);
            GUILayout.Label("ROUTES " + (CriticalRoutesValid ? "VALID" : "INVALID"));
            GUILayout.Label("QUEUES " + QueueSummary());
            GUILayout.Label("STATUS " + _lastDebugMessage);
            GUILayout.Label("P PAUSE | N STEP | F5 SAVE LOG | F7 REPLAY");
            GUILayout.EndArea();
#endif
        }

        private void DrawCurrentWork(OfficeCustomerState customer)
        {
            if (_simulationState.ManualTasks.IsActive)
            {
                string caseId = _simulationState.ManualTasks.ActiveCaseId;
                OfficeCaseWorkDefinition work =
                    _simulationState.WorkDefinitionFor(caseId);
                if (_simulationState.ManualTasks.ActiveKind ==
                    OfficeManualTaskKind.Compare)
                {
                    GUILayout.Label("CHECK PAPERS");
                    GUILayout.Label("1 CUSTOMER NAME: " + work.CustomerNameOnPaper);
                    GUILayout.Label("2 PAYMENT DATE: " + work.PaymentDateOnPaper);
                    GUILayout.Label("3 ACCOUNT MARK: " + work.AccountMarkOnPaper);
                    GUILayout.Label("4 THE PAPERS MATCH");
                }
                else if (_simulationState.ManualTasks.ActiveKind ==
                    OfficeManualTaskKind.Trace)
                {
                    GUILayout.Label("TRACE MONEY");
                    GUILayout.Label("1 COMPANY > PAYMENT RECORD > CUSTOMER ACCOUNT");
                    GUILayout.Label("2 COMPANY > PAYMENT RECORD > HOLDING ACCOUNT");
                    GUILayout.Label("3 COPIED FILE > NO PAYMENT RECORD > NO ACCOUNT");
                }
                else
                {
                    GUILayout.Label("CHECK WEIRD STUFF");
                    GUILayout.Label("1 CHECK THE OFFICE MARK");
                    GUILayout.Label("2 CHECK THE CLOCK MARK");
                    GUILayout.Label("3 CHECK THE ACCESS MARK");
                    GUILayout.Label("4 CHECK THE COPIER MARK");
                }
                return;
            }

            if (_simulationState.RoomWork.HelpActive)
            {
                OfficeRoomWorkJobState job = _simulationState.RoomWork.Job(
                    _simulationState.RoomWork.HelpJobId);
                if (job != null)
                    GUILayout.Label("HELPING: " + job.RemainingTicks + " TICKS LEFT");
            }

            if (customer == null) return;
            OfficeCaseWorkRecord record = _simulationState.ManualTasks.RecordFor(
                customer.LinkedAutomationClaimId);
            if (record.CompareAttempts > 0)
                GUILayout.Label("PAPERS: " + record.CompareReason);
            if (record.TraceAttempts > 0)
            {
                GUILayout.Label("MONEY: " + record.TraceResult);
                if (!string.IsNullOrWhiteSpace(record.TracePathSummary))
                    GUILayout.Label(record.TracePathSummary);
            }
            if (record.WeirdAttempts > 0)
                GUILayout.Label("WEIRD: " + record.WeirdResult);
            OfficeFolderState folder = _simulationState.Queues.GetFolder(
                customer.LinkedAutomationClaimId);
            if (_simulationState.ManualTasks.IsCaseComplete(
                    customer.LinkedAutomationClaimId) &&
                folder != null && !folder.IsMoving &&
                folder.OwnerKind == OfficeFolderOwnerKind.RoomQueue &&
                folder.CurrentRoom == OfficeRoomId.FrontDesk)
            {
                GUILayout.Label("DECIDE");
                GUILayout.Label("1 HELP CUSTOMER     2 REJECT CASE");
            }
            OfficeDecisionRecord decision = _simulationState.Decisions.RecordFor(
                customer.LinkedAutomationClaimId);
            if (decision != null) GUILayout.Label("STAMP: " + decision.Stamp);
            else if (_simulationState.Decisions.LastRecord != null)
                GUILayout.Label("LAST STAMP: " +
                    _simulationState.Decisions.LastRecord.Stamp);
        }

        private string FolderStatus(string caseId)
        {
            OfficeFolderState folder = _simulationState.Queues.GetFolder(caseId);
            if (folder == null) return "NOT HERE";
            if (folder.OwnerKind == OfficeFolderOwnerKind.Warden) return "CARRIED";
            if (folder.IsMoving) return "ON THE WAY TO " + RoomLabel(folder.DestinationRoom);
            return "AT " + RoomLabel(folder.CurrentRoom);
        }

        private void DrawCausalRecap()
        {
            GUILayout.Space(6f);
            GUILayout.Label("WHAT HAPPENED");
            for (int i = 0; i < _simulationState.CausalEvents.Events.Count; i++)
                GUILayout.Label("→ " +
                    _simulationState.CausalEvents.Events[i].PlayerText);
        }

        private static bool HasArgument(string[] arguments, string expected)
        {
            for (int i = 0; i < arguments.Length; i++)
                if (string.Equals(arguments[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string ArgumentValue(string[] arguments, string key)
        {
            for (int i = 0; i < arguments.Length; i++)
                if (string.Equals(arguments[i], key, StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < arguments.Length)
                    return arguments[i + 1];
            return string.Empty;
        }
    }
}
