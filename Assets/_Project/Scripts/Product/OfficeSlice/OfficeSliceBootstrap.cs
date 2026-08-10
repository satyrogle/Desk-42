using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private readonly List<Material> _runtimeMaterials = new();

        private OfficeCaseRepository _caseRepository;
        private OfficeSimulationState _simulationState;
        private OfficeTickDriver _tickDriver;
        private Transform _runtimeRoot;
        private Transform _wardenView;
        private Camera _camera;
        private bool _built;
        private string _lastDebugMessage = "BOOTING OFFICE SLICE";

        public OfficeCaseRepository CaseRepository => _caseRepository;
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
            _simulationState = OfficeSimulationState.CreateM2();
            _caseRepository = _simulationState.Cases;
            _runtimeRoot = new GameObject("Office Slice Runtime").transform;
            _runtimeRoot.SetParent(transform, false);
            BuildGreybox();
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
            if (string.IsNullOrWhiteSpace(capturePath)) yield break;

            yield return null;
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

        private void LateUpdate()
        {
            RefreshPresentation();
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
                if (!_folderViews.TryGetValue(caseId, out Transform view)) continue;

                int queueIndex = QueueIndex(folder.CurrentRoom, caseId);
                Vector3 destination = SocketWorldPosition(folder.CurrentRoom, queueIndex);
                if (folder.OwnerKind == OfficeFolderOwnerKind.Warden &&
                    _wardenView != null)
                {
                    destination = _wardenView.position + new Vector3(0.55f, 0.15f, 0f);
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
                    label.text = _caseRepository.Get(caseId)?.DisplayId ?? caseId;
                if (_folderRenderers.TryGetValue(caseId, out Renderer renderer))
                {
                    renderer.sharedMaterial.color = IsHighlightedFolder(folder)
                        ? new Color(1f, 0.88f, 0.22f)
                        : FolderColor(_caseRepository.Get(caseId).Urgency);
                }
            }

            RefreshCustomerViews();
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
            CreateWarden();
            CreateFolderViews();
            CreateCustomerViews();
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
            GUILayout.BeginArea(new Rect(16f, 16f, 540f, 330f), GUI.skin.box);
            GUILayout.Label("DESK 42 / TODAY'S DESK");
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
                GUILayout.Label("FOLDER: " + FolderStatus(customer.LinkedAutomationClaimId));
            }
            GUILayout.Space(8f);
            DrawCurrentWork(customer);
            GUILayout.Space(8f);
            GUILayout.Label("E / SPACE / A: " + _simulationState.PrimaryActionLabel);
            GUILayout.Label("WASD / ARROWS / LEFT STICK: MOVE");
            GUILayout.Label("CHOICES: 1-4 / X-Y-LB-RB");
            GUILayout.EndArea();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GUILayout.BeginArea(new Rect(16f, 360f, 700f, 205f), GUI.skin.box);
            GUILayout.Label("M2 ENGINEERING EVIDENCE");
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
                else
                {
                    GUILayout.Label("TRACE MONEY");
                    GUILayout.Label("1 COMPANY > PAYMENT RECORD > CUSTOMER ACCOUNT");
                    GUILayout.Label("2 COMPANY > PAYMENT RECORD > HOLDING ACCOUNT");
                    GUILayout.Label("3 COPIED FILE > NO PAYMENT RECORD > NO ACCOUNT");
                }
                return;
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
            OfficeFolderState folder = _simulationState.Queues.GetFolder(
                customer.LinkedAutomationClaimId);
            if (record.CompareComplete && record.TraceComplete &&
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
