using System;
using Desk42.Institutional;
using Desk42.Institutional.Scenarios.WorkplaceIdentity;
using UnityEngine;

namespace Desk42.Product
{
    /// <summary>
    /// Minimal product-branch composition root. It proves that a Unity runtime
    /// can consume the public institutional boundary without loading any of the
    /// archived card-game systems. This diagnostic shell is not a gameplay loop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InstitutionalProductBootstrap : MonoBehaviour
    {
        private const float PanelWidth = 620f;
        private const float PanelHeight = 300f;

        private InstitutionalConsequenceReport _report;
        private string _status = "Starting institutional reference run...";

        private void Awake()
        {
            try
            {
                InstitutionalScenarioDefinition definition =
                    WorkplaceIdentityScenario.CreateDefinition();
                InstitutionalPolicyConfiguration policy =
                    WorkplaceIdentityScenario.CreatePrecedentPolicy();

                _report = InstitutionalScenarioEngine.RunScenario(definition, policy);
                _status = "INSTITUTIONAL BACKEND READY";
                Debug.Log(
                    $"[Desk42.Product] Reference run completed at cycle {_report.FinalCycle} " +
                    $"with {_report.Rulings.Count} rulings and " +
                    $"{_report.DescendantCases.Count} descendant case(s).");
            }
            catch (Exception exception)
            {
                _status = "INSTITUTIONAL BACKEND FAILED";
                Debug.LogException(exception, this);
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--desk42-smoke") >= 0)
                Application.Quit(_report == null ? 1 : 0);
        }

        private void OnGUI()
        {
            float width = Mathf.Min(PanelWidth, Mathf.Max(320f, Screen.width - 48f));
            float height = Mathf.Min(PanelHeight, Mathf.Max(220f, Screen.height - 48f));
            var area = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Space(16f);
            GUILayout.Label("DESK 42 / PRODUCT WORKSPACE");
            GUILayout.Space(12f);
            GUILayout.Label(_status);
            GUILayout.Space(12f);

            if (_report == null)
            {
                GUILayout.Label("No institutional report is available.");
            }
            else
            {
                GUILayout.Label($"Reference policy: {_report.PolicyConfigurationId}");
                GUILayout.Label($"Final cycle: {_report.FinalCycle}");
                GUILayout.Label($"Observed actions: {_report.ObservedAgentActions.Count}");
                GUILayout.Label($"Rulings: {_report.Rulings.Count}");
                GUILayout.Label($"Appeals: {_report.Appeals.Count}");
                GUILayout.Label($"Descendant cases: {_report.DescendantCases.Count}");
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label("Diagnostic shell only. Legacy card gameplay is archived.");
            GUILayout.Space(12f);
            GUILayout.EndArea();
        }
    }
}
