// ============================================================
// DESK 42 — Moral Dilemma Data (ScriptableObject)
//
// Blueprint for one type of dilemma prompt.
// Dilemmas interrupt normal play — the player must choose
// before continuing. They are surfaced by MoralDilemmaSystem
// based on claim context, soul level, and SeedEngine rolls.
//
// A dilemma has exactly two choices:
//   Ethical:         morally correct, usually costs credits or time
//   Bureaucratic:    efficient, gains credits but costs soul
//
// Some dilemmas have no good choice — both options cost soul,
// but in different ways. These are the "trap" dilemmas used
// in the Whistleblower path.
//
// Designer workflow:
//   Create > Desk42 > Moral > Dilemma
// ============================================================

using UnityEngine;
using Desk42.Core;

namespace Desk42.MoralInjury
{
    [CreateAssetMenu(
        menuName = "Desk42/Moral/Dilemma",
        fileName = "Dilemma_")]
    public sealed class MoralDilemmaData : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────

        [Header("Identity")]
        public string DilemmaId;

        // ── Prompt ────────────────────────────────────────────

        [Header("Prompt")]
        [TextArea(3, 6)]
        [Tooltip("The dilemma description. Supports {clientName} and {claimAmount} tokens.")]
        public string PromptTemplate =
            "The claim references a financial discrepancy. You could flag it — " +
            "or quietly bury it. No one would ever know.";

        // ── Choices ───────────────────────────────────────────

        [Header("Ethical Choice (left)")]
        public string EthicalChoiceLabel    = "Flag the discrepancy.";
        public float  EthicalSoulDelta      = +2f;    // positive = soul recovered
        public int    EthicalCreditDelta    = -10;    // usually a cost
        public float  EthicalTimeDelta      = -30f;   // negative = loses time

        [Header("Bureaucratic Choice (right)")]
        public string BureaucraticChoiceLabel = "File it. Move on.";
        public float  BureaucraticSoulDelta   = -6f;
        public int    BureaucraticCreditDelta = +15;
        public float  BureaucraticTimeDelta   = 0f;

        // ── Context Filter ────────────────────────────────────

        [Header("Context Gating")]
        [Tooltip("Which action type this dilemma represents when the bureaucratic choice is taken.")]
        public UnethicalActionType ActionType = UnethicalActionType.EmpathyDenied;

        [Range(0f, 100f)]
        [Tooltip("Only surface this dilemma when soul integrity is at or below this value. 100 = always eligible.")]
        public float MaxSoulForEligibility = 100f;

        [Range(0f, 100f)]
        [Tooltip("Only surface this dilemma when soul integrity is at or above this value. 0 = always eligible.")]
        public float MinSoulForEligibility = 0f;

        [Tooltip("Only surface on Shift N or later. 0 = always.")]
        public int MinShiftNumber = 0;

        // ── Trap Dilemma ──────────────────────────────────────

        [Header("Trap (advanced)")]
        [Tooltip("If true, both choices cost soul — this is a no-win scenario.")]
        public bool IsTrapDilemma = false;

        [Tooltip("Special narrator reaction key for this dilemma.")]
        public string NarratorOverrideKey = "";

        // ── Probability ───────────────────────────────────────

        [Header("Spawn Weight")]
        [Range(0.01f, 1f)]
        [Tooltip("Relative weight when selecting from eligible pool.")]
        public float SpawnWeight = 0.5f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(DilemmaId))
                DilemmaId = name.ToLowerInvariant().Replace(" ", "_");
        }
#endif
    }
}
