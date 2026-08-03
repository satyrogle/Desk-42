using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Desk42.Product.Automation
{
    [DisallowMultipleComponent]
    public sealed class AutomationBootstrap : MonoBehaviour
    {
        private const string CaptureArgument = "--desk42-automation-capture";

        private AutomationFloorController _floor;
        private bool _paused;
        private bool _flowOverlay = true;
        private GUIStyle _title;
        private GUIStyle _metric;
        private GUIStyle _small;
        private GUIStyle _pill;

        public bool Ready => _floor != null;
        public int ClaimsInFlight => _floor?.ClaimsInFlight ?? 0;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _floor = new AutomationFloorController(transform);
            _floor.BuildVisualFloor();
        }

        private IEnumerator Start()
        {
            string capturePath = ArgumentValue(
                Environment.GetCommandLineArgs(), CaptureArgument);
            if (string.IsNullOrWhiteSpace(capturePath)) yield break;

            yield return new WaitForSecondsRealtime(4f);
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
            _floor?.SetFlowOverlayVisible(_flowOverlay);
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
            GUILayout.BeginArea(new Rect(16f, 14f, width, 78f), GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(250f));
            GUILayout.Label("DESK42 / BRANCH AUTOMATION", _title);
            GUILayout.Label("INSTITUTIONAL PROCESSING FLOOR  •  LIVE", _small);
            GUILayout.EndVertical();
            Metric("IN FLIGHT", ClaimsInFlight.ToString("00"));
            Metric("ROUTE", "INTAKE → OUTPUT");
            Metric("POLICY", "PROOF FORTRESS");
            GUILayout.FlexibleSpace();
            GUILayout.Label(_paused ? "PAUSED" : "OPERATING", _pill,
                GUILayout.Width(92f), GUILayout.Height(28f));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(16f, Screen.height - 54f, 470f, 38f),
                GUI.skin.box);
            GUILayout.Label(
                "SPACE pause  •  TAB flow overlay  •  claims enter automatically",
                _small);
            GUILayout.EndArea();
        }

        private void Metric(string label, string value)
        {
            GUILayout.BeginVertical(GUILayout.Width(130f));
            GUILayout.Label(label, _small);
            GUILayout.Label(value, _metric);
            GUILayout.EndVertical();
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
    }
}
