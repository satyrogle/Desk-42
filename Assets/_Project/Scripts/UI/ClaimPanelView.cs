// ============================================================
// DESK 42 — Claim Panel View (MonoBehaviour)
//
// Displays the current claim document. Set by EncounterManager
// when a ClaimQueuedEvent arrives. All fields are optional —
// unassigned labels are simply skipped.
// ============================================================

using UnityEngine;
using TMPro;
using Desk42.Core;

namespace Desk42.UI
{
    [DisallowMultipleComponent]
    public sealed class ClaimPanelView : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Claim Document Labels")]
        [SerializeField] private TMP_Text   _incidentText;
        [SerializeField] private TMP_Text   _claimantLabel;
        [SerializeField] private TMP_Text   _amountLabel;
        [SerializeField] private TMP_Text   _speciesLabel;
        [SerializeField] private TMP_Text   _anomalyTagsLabel;
        [SerializeField] private TMP_Text   _ndaLabel;
        [SerializeField] private TMP_Text   _claimIdLabel;

        [Header("Panel Root")]
        [SerializeField] private GameObject _panelRoot;

        // ── API ───────────────────────────────────────────────

        public void SetClaim(ActiveClaimData claim)
        {
            if (_panelRoot) _panelRoot.SetActive(true);

            if (_incidentText)
                _incidentText.text  = claim.IncidentText  ?? "Incident details unavailable.";

            if (_claimantLabel)
                _claimantLabel.text = claim.ClaimantName  ?? "Unknown";

            if (_amountLabel)
                _amountLabel.text   = $"¢{claim.ClaimAmount:N0}";

            if (_speciesLabel)
                _speciesLabel.text  = FormatSpecies(claim.ClientSpeciesId);

            if (_anomalyTagsLabel)
                _anomalyTagsLabel.text = FormatTags(claim.AnomalyTagIds);

            if (_ndaLabel)
                _ndaLabel.text = claim.NDARequired ? "⚠ NDA REQUIRED" : "";

            if (_claimIdLabel)
                _claimIdLabel.text = $"#{claim.ClaimId?[..8] ?? "????????"}";
        }

        public void Clear()
        {
            if (_panelRoot) _panelRoot.SetActive(false);
        }

        // ── Helpers ───────────────────────────────────────────

        private static string FormatSpecies(string id)
        {
            if (string.IsNullOrEmpty(id)) return "—";
            // Convert "kobold_variant_a" → "Kobold"
            var parts = id.Split('_');
            return parts.Length > 0
                ? char.ToUpper(parts[0][0]) + parts[0][1..]
                : id;
        }

        private static string FormatTags(string[] tags)
        {
            if (tags == null || tags.Length == 0) return "";
            // Show raw tag IDs for now; replace with DisplayName once SOs exist
            return string.Join("  •  ", tags);
        }
    }
}
