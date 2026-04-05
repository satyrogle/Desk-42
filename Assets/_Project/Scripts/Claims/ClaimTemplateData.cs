// ============================================================
// DESK 42 — Claim Template Data (ScriptableObject)
//
// Blueprint for one category of insurance claim. ClaimGenerator
// picks a template (weighted by ShiftNumber and SpawnWeight) and
// fills in the procedural details: claimant name, incident text,
// anomaly tags, corruption level, and hidden trait.
//
// Ship Tier catalogue (create one SO asset per template):
//   workplace_injury     — slip-and-fall, strain (shift 1+, 1 anomaly slot)
//   property_damage      — vehicle, home, equipment (shift 1+, 1 anomaly slot)
//   medical_expense      — healthcare reimbursement (shift 1+, 1 anomaly slot)
//   wrongful_termination — employment dispute (shift 2+, 1-2 anomaly slots)
//   data_breach          — privacy / information leak (shift 2+, 2 anomaly slots)
//   spectral_hazard      — occult containment incident (shift 3+, 2 slots)
//   temporal_incident    — time-adjacent event (shift 4+, 2-3 anomaly slots)
//   void_adjacent        — reality-adjacent event (shift 5+, 3 anomaly slots)
//
// Incident text tokens:
//   {claimant}  — replaced with a name from ClaimantNamePool
//   {amount}    — replaced with a formatted credit amount
//   {dept}      — replaced with the affiliated department name
//
// Designer workflow:
//   Create > Desk42 > Claims > Claim Template
//   Fill fields. Assign to ShiftManager._claimTemplates in the Shift scene.
// ============================================================

using UnityEngine;
using Desk42.Core;

namespace Desk42.Claims
{
    [CreateAssetMenu(
        menuName = "Desk42/Claims/Claim Template",
        fileName = "ClaimTemplate_")]
    public sealed class ClaimTemplateData : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Stable ID, used by ClaimGenerator. Must be unique.")]
        public string TemplateId;

        public string DisplayName = "Standard Claim";

        // ── Text Generation ───────────────────────────────────

        [Header("Text Generation")]
        [Tooltip("Incident description variants. Tokens: {claimant}, {amount}, {dept}. " +
                 "One is picked per generated claim.")]
        [TextArea(2, 4)]
        public string[] IncidentTextVariants =
        {
            "Claimant {claimant} submits a claim for losses totalling {amount}. " +
            "The incident occurred on the premises of {dept}.",
        };

        [Tooltip("Pool of claimant names. One is drawn per claim.")]
        public string[] ClaimantNamePool =
        {
            "T. Marlowe",  "B. Havisham", "C. Penrose",  "D. Ashford",
            "E. Vaux",     "F. Kemble",   "G. Thornton", "H. Dalby",
            "I. Quince",   "J. Arden",    "K. Severn",   "L. Blackwood",
        };

        [Tooltip("Department names used for {dept} token replacement.")]
        public string[] DeptNamePool =
        {
            "Claims Processing",   "Administrative Services",
            "Compliance Division", "Risk Management",
            "Facilities",          "Document Retention",
        };

        // ── Client ────────────────────────────────────────────

        [Header("Client")]
        [Tooltip("Client species IDs that can appear with this claim type. " +
                 "These are matched to client prefab configurations by the encounter system.\n" +
                 "Ship Tier species: human_standard, human_distressed, human_litigious, " +
                 "corporate_entity, anomalous_adjacent")]
        public string[] SpeciesPool = { "human_standard" };

        // ── Claim Properties ──────────────────────────────────

        [Header("Claim Properties")]
        [Range(0f, 1f)]
        [Tooltip("Base corruption level before anomaly tags are added.")]
        public float BaseCorruption = 0.15f;

        [Range(0, 4)]
        [Tooltip("Maximum number of anomaly tags that can be attached to a generated claim.")]
        public int AnomalyTagSlots = 1;

        [Tooltip("If non-empty, only these anomaly tag IDs are eligible for this template. " +
                 "Empty = any tag is valid.")]
        public string[] AnomalyTagFilter;

        [Tooltip("Claim amount range (inclusive). Fills the {amount} text token " +
                 "and affects credit reward scaling.")]
        public int ClaimAmountMin = 500;
        public int ClaimAmountMax = 5000;

        [Tooltip("Base credits awarded regardless of resolution outcome.")]
        public int BaseCreditReward = 5;

        [Range(0f, 20f)]
        [Tooltip("Soul integrity cost when resolved bureaucratically " +
                 "(i.e. the player denies a valid claim).")]
        public float BaseSoulCostBureaucratic = 2f;

        [Range(0f, 1f)]
        [Tooltip("Probability this claim requires the player to sign an NDA " +
                 "before it can be resolved.")]
        public float NDARequiredChance = 0f;

        // ── Faction ───────────────────────────────────────────

        [Header("Faction Impact")]
        [Tooltip("Whether resolving this claim affects a faction's reputation.")]
        public bool HasFactionAffinity = false;
        public FactionID FactionAffinity = FactionID.Filing;

        [Tooltip("Reputation change on humane resolution (positive = more friendly).")]
        public float FactionDeltaHumane = 2f;

        [Tooltip("Reputation change on bureaucratic resolution (usually negative).")]
        public float FactionDeltaBureaucratic = -1f;

        // ── Spawn ─────────────────────────────────────────────

        [Header("Spawn")]
        [Range(0f, 10f)]
        [Tooltip("Relative probability of being picked by ClaimGenerator. " +
                 "Higher = more common in the queue.")]
        public float SpawnWeight = 1f;

        [Tooltip("This template will not be selected before this shift number.")]
        public int MinShiftNumber = 1;

        // ── Validation ────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(TemplateId))
                TemplateId = name.ToLowerInvariant()
                    .Replace(" ", "_")
                    .Replace("claimtemplate_", "");

            if (ClaimAmountMax < ClaimAmountMin)
                ClaimAmountMax = ClaimAmountMin;
        }
#endif
    }
}
