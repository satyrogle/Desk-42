using System;
using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Product.Automation
{
    internal sealed class AutomationFloorController : IDisposable
    {
        private readonly Transform _root;
        private readonly List<GameObject> _owned = new();
        private readonly List<Renderer> _flowRenderers = new();
        private readonly List<Vector3> _primaryRoute = new();
        private AutomationDossierView _dossier;

        internal AutomationFloorController(Transform root)
        {
            _root = root != null ? root : throw new ArgumentNullException(nameof(root));
        }

        internal int ClaimsInFlight => _dossier != null ? 1 : 0;

        internal void BuildVisualFloor()
        {
            CreateCamera();
            CreateLighting();
            CreateRoom();

            Vector3 intake = new(-10.4f, 0.45f, 2.6f);
            Vector3 splitter = new(-5.3f, 0.45f, 2.6f);
            Vector3 verifier = new(-0.1f, 0.45f, 2.6f);
            Vector3 adjudicator = new(5.1f, 0.45f, 2.6f);
            Vector3 output = new(10.2f, 0.45f, 2.6f);
            Vector3 legal = new(5.1f, 0.45f, -3.2f);

            CreateStation("PUBLIC INTAKE", intake, new Color(0.25f, 0.47f, 0.47f),
                "RECEIVE");
            CreateStation("EVIDENCE SPLIT", splitter,
                new Color(0.49f, 0.47f, 0.29f), "SEPARATE");
            CreateStation("VERIFICATION", verifier,
                new Color(0.30f, 0.44f, 0.38f), "SCAN");
            CreateStation("ADJUDICATION", adjudicator,
                new Color(0.45f, 0.34f, 0.30f), "RULE");
            CreateStation("OUTPUT GATE", output,
                new Color(0.34f, 0.43f, 0.28f), "RELEASE");
            CreateStation("LEGAL / APPEALS", legal,
                new Color(0.39f, 0.31f, 0.42f), "RETURN");

            _primaryRoute.Add(new Vector3(-13f, 0.42f, 2.6f));
            _primaryRoute.Add(intake);
            _primaryRoute.Add(splitter);
            _primaryRoute.Add(verifier);
            _primaryRoute.Add(adjudicator);
            _primaryRoute.Add(output);
            _primaryRoute.Add(new Vector3(13f, 0.42f, 2.6f));
            CreateRoute(_primaryRoute, new Color(0.26f, 0.64f, 0.58f));
            CreateRoute(new[]
            {
                new Vector3(12.8f, 0.42f, -3.2f), legal,
                new Vector3(-0.1f, 0.42f, -3.2f), verifier,
            }, new Color(0.67f, 0.31f, 0.30f));

            GameObject token = AutomationVisualFactory.CreateFolderToken(
                _root, "DOSSIER 42-A", new Color(0.84f, 0.74f, 0.50f));
            _owned.Add(token);
            _dossier = token.AddComponent<AutomationDossierView>();
            _dossier.Initialise(_primaryRoute);
        }

        internal void SetFlowOverlayVisible(bool visible)
        {
            for (int i = 0; i < _flowRenderers.Count; i++)
                if (_flowRenderers[i] != null)
                    _flowRenderers[i].enabled = visible;
        }

        public void Dispose()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
                if (_owned[i] != null) UnityEngine.Object.Destroy(_owned[i]);
            _owned.Clear();
        }

        private void CreateCamera()
        {
            GameObject cameraObject = Own(new GameObject("Automation Camera"));
            Camera camera = cameraObject.AddComponent<Camera>();
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
            _owned.Add(AutomationVisualFactory.CreateWorldLabel(
                _root, "BRANCH 42 / AUTOMATED CLAIMS DIVISION",
                new Vector3(0f, 2.1f, 6.55f), 0.18f,
                new Color(0.73f, 0.74f, 0.59f), TextAnchor.MiddleCenter));
        }

        private void CreateStation(string name, Vector3 position, Color colour,
            string verb)
        {
            GameObject station = AutomationVisualFactory.CreateStation(
                _root, name, position, colour, verb);
            _owned.Add(station);
            _owned.Add(AutomationVisualFactory.CreateStaff(
                _root, name + " Operator", position + new Vector3(0f, 0f, -1.25f),
                colour * 0.9f));
        }

        private void CreateRoute(IReadOnlyList<Vector3> points, Color colour)
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
                _flowRenderers.Add(renderer);
            }
        }

        private GameObject Own(GameObject value)
        {
            value.transform.SetParent(_root, false);
            _owned.Add(value);
            return value;
        }
    }

    internal sealed class AutomationDossierView : MonoBehaviour
    {
        private readonly List<Vector3> _route = new();
        private int _targetIndex;
        private float _hold;
        private Vector3 _baseScale;

        internal void Initialise(IReadOnlyList<Vector3> route)
        {
            _route.Clear();
            for (int i = 0; i < route.Count; i++) _route.Add(route[i]);
            transform.position = _route[0];
            _targetIndex = 1;
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            if (_route.Count < 2) return;
            if (_hold > 0f)
            {
                _hold -= Time.deltaTime;
                transform.localScale = _baseScale *
                    (1f + Mathf.Sin(Time.time * 10f) * 0.035f);
                return;
            }
            Vector3 target = _route[_targetIndex];
            transform.position = Vector3.MoveTowards(
                transform.position, target, Time.deltaTime * 2.25f);
            transform.localScale = _baseScale;
            if ((transform.position - target).sqrMagnitude > 0.0025f) return;
            _hold = _targetIndex == 0 || _targetIndex == _route.Count - 1
                ? 0.5f
                : 1.05f;
            _targetIndex++;
            if (_targetIndex >= _route.Count)
            {
                _targetIndex = 0;
                transform.position = _route[0];
                _targetIndex = 1;
            }
        }
    }
}
