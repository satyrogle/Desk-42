// ============================================================
// DESK 42 — Encounter baseline / working state split
//
// Handoff §3.4.
//
//   external reads              -> baseline  (immutable, captured at entry)
//   this encounter's mutations  -> working state (live)
//
// The distinction exists so that an encounter cannot change the
// meaning of the world it walked into, while still letting the
// player's own actions take effect immediately:
//
//   Elias enters, baseline says: not registered 18A
//   player applies required registration
//   working state says: registered 18A
//   disposition unlocks from working state IMMEDIATELY
//
// IMPORTANT (verified at a432a8b): the live half already behaves
// correctly. EliasProcedurePolicy reads EliasProofSessionState by
// reference, so registration applied mid-encounter is visible to the
// disposition gate on the same frame. This type therefore ADDS the
// missing immutable baseline for external reads; it deliberately does
// NOT interpose itself between the procedure and the disposition gate,
// because doing so would introduce the very staleness §3.4 forbids.
// ============================================================

using System;

namespace Desk42.Encounter
{
    /// <summary>
    /// Immutable snapshot of EXTERNAL pre-existing state, captured once when
    /// an encounter begins. Never mutated. Safe to read for "what was true
    /// when this claimant walked in".
    /// </summary>
    public readonly struct EncounterBaseline
    {
        public readonly string EncounterId;
        public readonly string ClientVariantId;
        public readonly string AuthoredAppearanceKey;
        public readonly int    ShiftNumber;

        /// <summary>Completed visits by this claimant BEFORE this encounter.</summary>
        public readonly int PriorVisits;

        /// <summary>Presentations of this claimant before this encounter.</summary>
        public readonly int PriorPresentations;

        /// <summary>True when this exact encounter had already committed before entry.</summary>
        public readonly bool AlreadyCommitted;

        public readonly bool IsValid;

        public EncounterBaseline(
            string encounterId,
            string clientVariantId,
            string authoredAppearanceKey,
            int shiftNumber,
            int priorVisits,
            int priorPresentations,
            bool alreadyCommitted)
        {
            EncounterId           = encounterId;
            ClientVariantId       = clientVariantId;
            AuthoredAppearanceKey = authoredAppearanceKey;
            ShiftNumber           = shiftNumber;
            PriorVisits           = Math.Max(0, priorVisits);
            PriorPresentations    = Math.Max(0, priorPresentations);
            AlreadyCommitted      = alreadyCommitted;
            IsValid               = !string.IsNullOrWhiteSpace(encounterId);
        }

        public static EncounterBaseline None => default;

        /// <summary>
        /// Human-facing visit number for THIS encounter (1-based), derived from
        /// the baseline rather than from a stored counter.
        /// </summary>
        public int CurrentVisitNumber => PriorVisits + 1;

        public override string ToString()
            => IsValid
                ? $"EncounterBaseline({EncounterId}, {ClientVariantId}, " +
                  $"priorVisits={PriorVisits}, priorPresentations={PriorPresentations})"
                : "EncounterBaseline(none)";
    }
}
