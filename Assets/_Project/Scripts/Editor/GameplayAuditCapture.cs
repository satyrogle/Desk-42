#if UNITY_EDITOR
using System.IO;
using Desk42.Core;
using Desk42.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Desk42.Editor
{
    /// <summary>
    /// Captures the real Shift game view for visual regression checks.
    /// The command-line entry point exits Unity after the PNG is written.
    /// </summary>
    [InitializeOnLoad]
    public static class GameplayAuditCapture
    {
        private const string StageKey = "Desk42.GameplayAudit.Stage";
        private const string StartedKey = "Desk42.GameplayAudit.Started";
        private const string BuildAuditArgument = "-desk42BuildAudit";
        private const string CaptureMethodArgument =
            "Desk42.Editor.GameplayAuditCapture.CaptureFromCommandLine";
        private const int WaitingForGame = 1;
        private const int WaitingForShift = 2;
        private const int WaitingForFile = 3;
        private const int LeavingPlayMode = 4;

        private static string OutputPath => Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "tmp", "audit", "01-shift-current.png"));

        static GameplayAuditCapture()
        {
            if (HasCommandLineArgument(BuildAuditArgument))
            {
                EditorApplication.delayCall += BuildAuditPlayerFromCommandLine;
                return;
            }

            if (!HasCommandLineArgument(CaptureMethodArgument))
            {
                SessionState.EraseInt(StageKey);
                SessionState.EraseFloat(StartedKey);
                return;
            }

            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0)
                return;

            float started = SessionState.GetFloat(StartedKey, 0f);
            bool staleFromPreviousProcess = started <= 0f
                || started > EditorApplication.timeSinceStartup;
            if (staleFromPreviousProcess)
            {
                SessionState.EraseInt(StageKey);
                SessionState.EraseFloat(StartedKey);
                Debug.LogWarning(
                    "[GameplayAudit] Cleared stale command-line capture state.");
                return;
            }

            Subscribe();
        }

        private static bool HasCommandLineArgument(string expected)
        {
            foreach (string argument in System.Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, expected, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        [MenuItem("Tools/Desk 42/Visual Identity/Capture Current Shift")]
        public static void CaptureFromMenu()
        {
            BeginCapture();
        }

        public static void CaptureFromCommandLine()
        {
            BeginCapture();
        }

        public static void BuildAuditPlayerFromCommandLine()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputPath = Path.Combine(projectRoot, "tmp", "audit", "Desk42Audit.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            EditorBuildSettingsScene[] scenes = System.Array.FindAll(
                EditorBuildSettings.scenes,
                scene => scene.enabled);
            string[] scenePaths = System.Array.ConvertAll(scenes, scene => scene.path);

            // Keep automated visual captures out of the player's real save folder.
            string originalProductName = PlayerSettings.productName;
            BuildReport report;
            try
            {
                PlayerSettings.productName = "Desk 42 Audit";
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenePaths,
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
            }
            finally
            {
                PlayerSettings.productName = originalProductName;
            }

            EditorApplication.Exit(
                report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static void BeginCapture()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            if (File.Exists(OutputPath))
                File.Delete(OutputPath);

            SessionState.SetInt(StageKey, WaitingForGame);
            SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Boot.unity", OpenSceneMode.Single);
            Subscribe();
            EditorApplication.isPlaying = true;
        }

        private static void Subscribe()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0)
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (EditorApplication.timeSinceStartup
                - SessionState.GetFloat(StartedKey, 0f) > 45d)
            {
                Fail("Timed out waiting for a capturable Shift frame.");
                return;
            }

            if (stage == WaitingForGame && EditorApplication.isPlaying
                && SceneManager.GetActiveScene().name == "MainMenu")
            {
                GameManager manager = GameManager.Instance
                    ?? Object.FindObjectOfType<GameManager>(true);
                if (manager == null)
                    return;

                Screen.SetResolution(1920, 1080, false);
                Debug.Log("[GameplayAudit] Starting deterministic audit run.");
                manager.StartNewRun("auditor");
                SessionState.SetInt(StageKey, WaitingForShift);
                return;
            }

            if (stage == WaitingForShift && EditorApplication.isPlaying
                && SceneManager.GetActiveScene().name == "Shift")
            {
                // Visual regression captures inspect the playable surface, not the
                // first-run memo. Hiding this transient canvas does not complete or
                // persist the tutorial for the player.
                GameObject tutorialCanvas = GameObject.Find("TutorialCanvas");
                if (tutorialCanvas != null)
                    tutorialCanvas.SetActive(false);

                ClientView view = Object.FindObjectOfType<ClientView>(true);
                Image portrait = view != null
                    ? view.transform.Find("ClientPortrait")?.GetComponent<Image>()
                    : null;

                if (portrait == null || portrait.sprite == null)
                    return;

                Debug.Log("[GameplayAudit] Capturing populated Shift frame.");
                ScreenCapture.CaptureScreenshot(OutputPath, 1);
                SessionState.SetInt(StageKey, WaitingForFile);
                return;
            }

            if (stage == WaitingForFile && File.Exists(OutputPath)
                && new FileInfo(OutputPath).Length > 0)
            {
                Debug.Log($"[GameplayAudit] Captured {OutputPath}");
                SessionState.SetInt(StageKey, LeavingPlayMode);
                EditorApplication.isPlaying = false;
                return;
            }

            if (stage == LeavingPlayMode && !EditorApplication.isPlaying)
            {
                Complete(0);
            }
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[GameplayAudit] {message}");
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
            Complete(1);
        }

        private static void Complete(int exitCode)
        {
            SessionState.EraseInt(StageKey);
            SessionState.EraseFloat(StartedKey);
            EditorApplication.update -= Tick;
            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
        }
    }
}
#endif
