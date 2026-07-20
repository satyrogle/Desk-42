using System.Collections;
using System.IO;
using Desk42.Core;
using Desk42.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Desk42.Debugging
{
    /// <summary>
    /// Opt-in development-player screenshot path used for visual regression checks.
    /// It is inert unless -desk42CapturePath is supplied on the command line.
    /// </summary>
    public sealed class GameplayScreenshotBootstrap : MonoBehaviour
    {
        private const string CaptureArgument = "-desk42CapturePath";
        private const string CaptureStateArgument = "-desk42CaptureState";
        private const string CaptureSanityArgument = "-desk42CaptureSanity";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfRequested()
        {
            string outputPath = ReadArgument(CaptureArgument);
            if (string.IsNullOrWhiteSpace(outputPath))
                return;

            // Automated audit players are launched without stealing desktop focus.
            // Keep rendering while that opt-in capture window is hidden.
            Application.runInBackground = true;
            Debug.Log($"[GameplayScreenshot] Audit capture requested: {outputPath}");

            var host = new GameObject(nameof(GameplayScreenshotBootstrap));
            DontDestroyOnLoad(host);
            host.AddComponent<GameplayScreenshotBootstrap>().StartCoroutine(
                host.GetComponent<GameplayScreenshotBootstrap>().Capture(
                    outputPath,
                    ReadArgument(CaptureStateArgument),
                    ReadArgument(CaptureSanityArgument)));
        }

        private IEnumerator Capture(
            string outputPath, string requestedState, string requestedSanity)
        {
            float deadline = Time.realtimeSinceStartup + 45f;

            while ((GameManager.Instance == null
                    || SceneManager.GetActiveScene().name != "MainMenu")
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (GameManager.Instance == null)
            {
                Debug.LogError("[GameplayScreenshot] GameManager never became ready.");
                Application.Quit(1);
                yield break;
            }

            if (string.Equals(requestedState, "MainMenu",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                yield return CaptureAndQuit(outputPath, 1f);
                yield break;
            }

            if (string.Equals(requestedState, "InternalAudit",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                GameManager.Instance.ContinueToMetaHub();
                while (SceneManager.GetActiveScene().name != "InternalAudit"
                       && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                if (SceneManager.GetActiveScene().name != "InternalAudit")
                {
                    Debug.LogError("[GameplayScreenshot] Internal Audit never loaded.");
                    Application.Quit(1);
                    yield break;
                }

                yield return CaptureAndQuit(outputPath, 1.5f);
                yield break;
            }

            GameManager.Instance.StartNewRun("auditor");

            Image portrait = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (SceneManager.GetActiveScene().name == "Shift")
                {
                    ClientView view = FindObjectOfType<ClientView>(true);
                    portrait = view != null
                        ? view.transform.Find("ClientPortrait")?.GetComponent<Image>()
                        : null;
                    if (portrait != null && portrait.sprite != null)
                        break;
                }

                yield return null;
            }

            if (portrait == null || portrait.sprite == null)
            {
                Debug.LogError("[GameplayScreenshot] Shift never reached a populated frame.");
                Application.Quit(1);
                yield break;
            }

            // Keep visual-regression frames focused on the playable desk. This only
            // hides the transient capture instance; it does not complete the tutorial
            // or mutate the player's meta save.
            GameObject tutorialCanvas = GameObject.Find("TutorialCanvas");
            if (tutorialCanvas != null)
                tutorialCanvas.SetActive(false);

            // Optional audit tier for proving the sanity-driven room grammar. Normal
            // players still reach these states exclusively through gameplay.
            float settleSeconds = 1f;
            if (float.TryParse(requestedSanity,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float targetSanity))
            {
                RunStateController run = GameManager.Instance.Run;
                float target = Mathf.Clamp(targetSanity, 0f, 100f);
                run.ModifySanity(target - run.Sanity);
                yield return null;
                settleSeconds = 4f;
            }

            yield return CaptureAndQuit(outputPath, settleSeconds);
        }

        private static IEnumerator CaptureAndQuit(string outputPath, float settleSeconds)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            yield return new WaitForSecondsRealtime(settleSeconds);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(outputPath);
            Debug.Log($"[GameplayScreenshot] Capturing {outputPath}");

            float fileDeadline = Time.realtimeSinceStartup + 10f;
            while ((!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                   && Time.realtimeSinceStartup < fileDeadline)
            {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(0.25f);
            Application.Quit(File.Exists(outputPath) ? 0 : 1);
        }

        private static string ReadArgument(string key)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == key)
                    return args[i + 1];
            }

            return null;
        }
    }
}
