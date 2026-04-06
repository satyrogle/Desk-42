// ============================================================
// DESK 42 — Client View (MonoBehaviour)
//
// Displays the active client's identity and BSM mood state.
// Subscribes to ClientStateMachine.OnStateChanged and repaints
// the mood indicator color + label whenever the state changes.
//
// Wire all UI refs in the Inspector. All fields are optional.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Desk42.Core;
using Desk42.BSM;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class ClientView : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Identity")]
        [SerializeField] private TMP_Text _speciesLabel;
        [SerializeField] private TMP_Text _variantLabel;

        [Header("Mood")]
        [SerializeField] private TMP_Text _moodLabel;
        [SerializeField] private Image    _moodIndicator;
        [SerializeField] private TMP_Text _injectionLabel; // shows "INJECTED" when stack active

        // ── Mood Color Table ──────────────────────────────────

        private static readonly Dictionary<ClientStateID, Color> MoodColors = new()
        {
            [ClientStateID.Pending]      = new Color(0.80f, 0.80f, 0.80f),
            [ClientStateID.Cooperative]  = new Color(0.35f, 0.78f, 0.35f),
            [ClientStateID.Agitated]     = new Color(1.00f, 0.55f, 0.15f),
            [ClientStateID.Litigious]    = new Color(0.90f, 0.15f, 0.15f),
            [ClientStateID.Suspicious]   = new Color(0.80f, 0.70f, 0.10f),
            [ClientStateID.Resigned]     = new Color(0.45f, 0.45f, 0.70f),
            [ClientStateID.Paranoid]     = new Color(0.65f, 0.25f, 0.80f),
            [ClientStateID.Dissociating] = new Color(0.25f, 0.25f, 0.25f),
            [ClientStateID.Smug]         = new Color(0.55f, 0.90f, 0.55f),
        };

        // ── State ─────────────────────────────────────────────

        private ClientStateMachine _csm;

        // ── API ───────────────────────────────────────────────

        public void SetClient(ClientStateMachine csm, string speciesId, string variantId)
        {
            // Unsub from previous client if any
            Clear();

            _csm = csm;

            if (_speciesLabel) _speciesLabel.text = FormatSpecies(speciesId);
            if (_variantLabel) _variantLabel.text = variantId ?? "—";

            UpdateMood(csm.CurrentMoodState, csm.IsInInjectedState);

            csm.OnStateChanged += HandleStateChanged;
        }

        public void Clear()
        {
            if (_csm != null)
            {
                _csm.OnStateChanged -= HandleStateChanged;
                _csm = null;
            }

            if (_speciesLabel)   _speciesLabel.text  = "";
            if (_variantLabel)   _variantLabel.text  = "";
            if (_moodLabel)      _moodLabel.text     = "";
            if (_injectionLabel) _injectionLabel.text = "";
            if (_moodIndicator)  _moodIndicator.color = Color.grey;
        }

        // ── Event Handler ─────────────────────────────────────

        private void HandleStateChanged(ClientStateID _, ClientStateID newState)
            => UpdateMood(newState, _csm?.IsInInjectedState ?? false);

        // ── Display ───────────────────────────────────────────

        private void UpdateMood(ClientStateID state, bool injected)
        {
            if (_moodLabel)
                _moodLabel.text = state.ToString().ToUpper();

            if (_moodIndicator)
                _moodIndicator.color = MoodColors.TryGetValue(state, out var col)
                    ? col : Color.grey;

            if (_injectionLabel)
                _injectionLabel.text = injected ? "[ FORM FILED ]" : "";
        }

        private static string FormatSpecies(string id)
        {
            if (string.IsNullOrEmpty(id)) return "—";
            var parts = id.Split('_');
            return parts.Length > 0
                ? char.ToUpper(parts[0][0]) + parts[0][1..]
                : id;
        }
    }
}
