using System;
using System.Collections;
using System.IO;
using Desk42.Institutional;
using Desk42.Institutional.Player;
using UnityEngine;

namespace Desk42.Product
{
    /// <summary>
    /// Unity composition root for the causal-legibility slice. Simulation authority
    /// remains behind CausalLegibilitySliceSession; this component owns lifecycle,
    /// command-line smoke behavior and the thin presentation adapter only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InstitutionalProductBootstrap : MonoBehaviour
    {
        private const string SmokeArgument = "--desk42-smoke";
        private const string CaptureArgument = "--desk42-capture";

        private CausalLegibilitySliceSession _session;
        private CausalLegibilitySliceView _view;
        private string _defaultSavePath;
        private string _lastStatus = "Initialising Branch 42...";

        public bool Ready => _session != null && _view != null;
        public PlayerInstitutionView CurrentView => _session?.View;
        public string LastStatus => _lastStatus;
        public string DefaultSavePath => _defaultSavePath;

        private void Awake()
        {
            try
            {
                _defaultSavePath = Path.Combine(
                    Application.persistentDataPath,
                    "causal-legibility-v0.1.json");
                _session = CausalLegibilitySliceSession.Create();
                _view = new CausalLegibilitySliceView(
                    () => _session.View,
                    CommitSelection,
                    ReplayFromPreRuling,
                    () => SaveTo(_defaultSavePath),
                    () => LoadFrom(_defaultSavePath),
                    () => _lastStatus);
                _lastStatus =
                    "CASE READY / Review what the institution knows, then rule.";
                Debug.Log(
                    $"[Desk42.Product] Causal Legibility Slice ready at cycle " +
                    $"{_session.View.CurrentCycle} with {_session.View.Cases.Count} case(s).",
                    this);
            }
            catch (Exception exception)
            {
                _lastStatus = "SLICE INITIALISATION FAILED / " + exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private IEnumerator Start()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            bool smoke = HasArgument(arguments, SmokeArgument);
            string capturePath = ArgumentValue(arguments, CaptureArgument);
            if (!smoke && string.IsNullOrWhiteSpace(capturePath)) yield break;

            yield return null;
            yield return new WaitForEndOfFrame();

            if (!Ready)
            {
                FailAutomatedValidation(new InvalidOperationException(_lastStatus));
                yield break;
            }

            try
            {
                if (smoke)
                {
                    CommitSelection(
                        PlayerScopeChoice.Broad,
                        RulingDisposition.Recognised);
                    SaveTo(_defaultSavePath);
                    LoadFrom(_defaultSavePath);
                    if (_session.View.Cases.Count != 2 ||
                        _session.View.Rulings.Count != 1)
                    {
                        throw new InvalidOperationException(
                            "Automated smoke did not reach the descendant-case review.");
                    }
                    Debug.Log(
                        "DESK42_SMOKE_OK causal-legibility save-load descendant-case",
                        this);
                }
            }
            catch (Exception exception)
            {
                FailAutomatedValidation(exception);
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(capturePath))
            {
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(capturePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
                    _view.SelectPanel(CausalLegibilityPanel.Docket);
                }
                catch (Exception exception)
                {
                    FailAutomatedValidation(exception);
                    yield break;
                }

                yield return new WaitForEndOfFrame();
                if (File.Exists(fullPath)) File.Delete(fullPath);
                ScreenCapture.CaptureScreenshot(fullPath);
                for (int frame = 0; frame < 120 && !File.Exists(fullPath); frame++)
                    yield return null;
                if (!File.Exists(fullPath))
                {
                    FailAutomatedValidation(
                        new IOException("Screenshot capture did not produce a file."));
                    yield break;
                }
                Debug.Log("DESK42_CAPTURE_OK " + fullPath, this);
            }

            Application.Quit(0);
        }

        private void FailAutomatedValidation(Exception exception)
        {
            _lastStatus = "AUTOMATED VALIDATION FAILED / " + exception.Message;
            Debug.LogException(exception, this);
            Application.Quit(1);
        }

        private void OnGUI()
        {
            if (_view == null)
            {
                GUI.Box(
                    new Rect(24f, 24f, Mathf.Max(320f, Screen.width - 48f), 96f),
                    _lastStatus);
                return;
            }
            _view.Draw();
        }

        public PlayerInstitutionView CommitSelection(
            PlayerScopeChoice scope,
            RulingDisposition disposition)
        {
            PlayerRulingDraft draft = _session.CreateDraft(scope, disposition);
            PlayerInstitutionView result = _session.Commit(draft);
            _lastStatus = result.Cases.Count > 1
                ? "NEW CASE / A later record has entered the docket."
                : "NO NEW CASE / The observation window closed without another filing.";
            _view?.SelectPanel(CausalLegibilityPanel.Consequences);
            return result;
        }

        public PlayerInstitutionView ReplayFromPreRuling()
        {
            PlayerInstitutionView result = _session.ReplayFromPreRuling();
            _lastStatus = "REPLAY RESTORED / Change the scope or disposition and rule again.";
            _view?.SelectPanel(CausalLegibilityPanel.Ruling);
            return result;
        }

        public void SaveTo(string path)
        {
            _session.Save(path);
            _lastStatus = "SAVED / Active history and pre-ruling replay point preserved.";
        }

        public PlayerInstitutionView LoadFrom(string path)
        {
            _session = CausalLegibilitySliceSession.Load(path);
            _lastStatus = "LOADED / Institutional history restored.";
            return _session.View;
        }

        public void SelectPanel(CausalLegibilityPanel panel)
        {
            _view?.SelectPanel(panel);
        }

        private static bool HasArgument(string[] arguments, string expected)
        {
            for (int i = 0; i < arguments.Length; i++)
                if (string.Equals(
                        arguments[i],
                        expected,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string ArgumentValue(string[] arguments, string key)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(
                        arguments[i],
                        key,
                        StringComparison.OrdinalIgnoreCase) &&
                    i + 1 < arguments.Length)
                    return arguments[i + 1];
                string prefix = key + "=";
                if (arguments[i].StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                    return arguments[i].Substring(prefix.Length);
            }
            return string.Empty;
        }
    }
}
