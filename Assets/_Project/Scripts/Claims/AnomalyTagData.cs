// ============================================================
// DESK 42 — Anomaly Tag Data (ScriptableObject)
//
// Defines one flavour of "strangeness" that can be attached to
// a generated claim. Multiple tags can stack on one claim;
// each increases corruption and may unlock a hidden trait
// when the player plays ANALYSE.
//
// Ship Tier catalogue (create one SO asset per tag):
//   temporal_discrepancy   — dates on the form are internally inconsistent
//   spectral_signatory     — signed by someone who is no longer living
//   recursive_reference    — the form cites itself as supporting evidence
//   impossible_injury      — described injury is anatomically impossible
//   pre_filed_claim        — submitted before the incident date
//   notarial_anomaly       — notarized by an office with no records of existence
//   void_jurisdiction      — falls under no registered legal jurisdiction
//   biometric_mismatch     — claimant's physical description changes between pages
//   memory_contamination   — all witness statements are word-for-word identical
//   form_corruption        — some sections appear in an unregistered character set
//   witness_absence        — all listed witnesses are "currently unavailable"
//   claim_echo             — identical to a claim filed exactly seven years prior
//
// Designer workflow:
//   Create > Desk42 > Claims > Anomaly Tag
//   Fill fields, assign HiddenTraitId to a key in MutationEngine.CounterTraitPool.
// ============================================================

using UnityEngine;
using Desk42.Core;

namespace Desk42.Claims
{
    [CreateAssetMenu(
        menuName = "Desk42/Claims/Anomaly Tag",
        fileName = "AnomalyTag_")]
    public sealed class AnomalyTagData : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Stable ID, used by ClaimGenerator and save system. " +
                 "Must be unique across all anomaly tag SOs.")]
        public string TagId;

        public string DisplayName = "Anomaly";

        [TextArea(2, 3)]
        [Tooltip("Short description shown in the claim viewer when this tag is detected.")]
        public string Description = "Something about this claim is not quite right.";

        // ── Hidden Trait ─────────────────────────────────────

        [Header("Hidden Trait")]
        [Tooltip("Counter-trait revealed when ANALYSE is played on this claim. " +
                 "Must match a key in MutationEngine.CounterTraitPool. " +
                 "Empty = this tag has no hidden trait.")]
        public string HiddenTraitId;

        [TextArea(2, 3)]
        [Tooltip("Flavour text shown when ANALYSE successfully reveals this tag's trait.")]
        public string RevealFlavourText = "A pattern emerges.";

        [Tooltip("Punch card types that are most effective once this hidden trait is known. " +
                 "Shown as a hint in the card UI after ANALYSE.")]
        public PunchCardType[] EffectivePunchCards;

        // ── Claim Properties ──────────────────────────────────

        [Header("Claim Properties")]
        [Range(0f, 0.5f)]
        [Tooltip("How much this tag adds to the claim's base corruption level (0-1).")]
        public float CorruptionContribution = 0.10f;

        [Tooltip("If true, claims with this tag always require an NDA " +
                 "(overrides the template's NDARequiredChance).")]
        public bool ForcesNDA = false;

        // ── Spawn ─────────────────────────────────────────────

        [Header("Spawn")]
        [Range(0f, 10f)]
        [Tooltip("Relative probability of being selected. Higher = more common.")]
        public float SpawnWeight = 1f;

        [Tooltip("This tag will not appear in claims generated before this shift number.")]
        public int MinShiftNumber = 1;

        // ── Validation ────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(TagId))
                TagId = name.ToLowerInvariant()
                    .Replace(" ", "_")
                    .Replace("anomalytag_", "");
        }
#endif
    }
}
