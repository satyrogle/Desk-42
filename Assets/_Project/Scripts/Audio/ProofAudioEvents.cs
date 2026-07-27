// ============================================================
// DESK 42 — Proof audio event contract (D1B)
//
// ONE authoritative list of logical audio identities for the
// Five-Shift proof slice, plus the single mapping layer to eventual
// FMOD paths. Gameplay code references the enum; it never contains an
// FMOD path string.
//
// This file has NO FMOD dependency and compiles with DESK42_FMOD
// undefined. The mapping is data only — resolving a path does not load,
// validate or play anything.
//
// NARRATIVE CONSTRAINT: EliasRegistrationCausal is an event SLOT ONLY.
// The Venn motif is authored content owned outside this bucket. Nothing
// here composes, generates or reinterprets it.
//
// SUPPRESSION (locked): there is deliberately NO mapping, alias, fallback
// or helper that can replay EliasRegistrationCausal on the Shift 5
// return. Shift5EliasReturn is a distinct identity with a distinct path,
// and IsCausalIdentity exists so callers can be tested for the mistake.
//
// Mercy Window and Flow are intentionally absent from this contract.
// ============================================================

using System;
using System.Collections.Generic;

namespace Desk42.Audio
{
    /// <summary>
    /// Stable logical audio identities for the proof slice. Values are
    /// explicit so reordering cannot silently change a saved/logged identity.
    /// </summary>
    public enum ProofAudioEvent
    {
        None = 0,

        /// <summary>Ordinary desk interaction — card select/slam feedback.</summary>
        DeskInteraction = 1,

        /// <summary>Ordinary procedure application feedback (non-causal).</summary>
        ProcedureFeedback = 2,

        /// <summary>
        /// Shift 2 Elias registration / causal identity. SLOT ONLY until the
        /// authored motif exists. Must never be requested on Shift 5.
        /// </summary>
        EliasRegistrationCausal = 3,

        /// <summary>Compliance Streak confirmation. Follows the causal anchor.</summary>
        ComplianceStreakConfirm = 4,

        /// <summary>
        /// Generic Shift 5 Elias return. Deliberately NOT the causal identity —
        /// the experiment must not acoustically name the earlier cause.
        /// </summary>
        Shift5EliasReturn = 5,
    }

    /// <summary>
    /// The single mapping layer between logical identities and FMOD paths.
    /// Kept as plain strings so it compiles without the FMOD package.
    /// </summary>
    public static class ProofAudioCatalog
    {
        private static readonly Dictionary<ProofAudioEvent, string> Paths = new()
        {
            [ProofAudioEvent.DeskInteraction]         = "event:/Desk/Interaction",
            [ProofAudioEvent.ProcedureFeedback]       = "event:/Desk/ProcedureApplied",
            [ProofAudioEvent.EliasRegistrationCausal] = "event:/Proof/EliasRegistration18A",
            [ProofAudioEvent.ComplianceStreakConfirm] = "event:/Desk/ComplianceStreak",
            [ProofAudioEvent.Shift5EliasReturn]       = "event:/Proof/EliasReturnGeneric",
        };

        /// <summary>Every identity in the proof contract.</summary>
        public static IEnumerable<ProofAudioEvent> All => Paths.Keys;

        /// <summary>
        /// FMOD path for a logical identity, or null when unmapped.
        /// Returning null rather than throwing keeps a missing mapping a
        /// diagnosable no-op instead of a crash mid-encounter.
        /// </summary>
        public static string TryGetPath(ProofAudioEvent id)
            => Paths.TryGetValue(id, out string path) ? path : null;

        /// <summary>
        /// True for the Shift 2 causal identity. Exists so the Shift 5
        /// suppression rule is directly assertable rather than assumed.
        /// </summary>
        public static bool IsCausalIdentity(ProofAudioEvent id)
            => id == ProofAudioEvent.EliasRegistrationCausal;

        /// <summary>
        /// Identities permitted during the scored Shift 5 return. The causal
        /// identity is excluded by construction, not by convention.
        /// </summary>
        public static bool IsPermittedOnShift5(ProofAudioEvent id)
            => id != ProofAudioEvent.EliasRegistrationCausal && id != ProofAudioEvent.None;
    }

    /// <summary>Outcome of an audio request. Never throws into gameplay.</summary>
    public enum AudioRequestResult
    {
        /// <summary>Handed to a backend. NOT a claim that sound was heard.</summary>
        Requested = 0,

        /// <summary>No backend available — FMOD absent or uninitialised.</summary>
        Unavailable = 1,

        /// <summary>Identity has no mapping in the catalog.</summary>
        UnknownEvent = 2,

        /// <summary>Refused by a locked rule, e.g. causal identity on Shift 5.</summary>
        Suppressed = 3,
    }

    /// <summary>Optional context, used for diagnostics and passive telemetry.</summary>
    public readonly struct AudioRequestContext
    {
        public readonly int ShiftNumber;
        public readonly string EncounterId;
        public readonly string ClaimantStableId;

        public AudioRequestContext(
            int shiftNumber, string encounterId = null, string claimantStableId = null)
        {
            ShiftNumber      = shiftNumber;
            EncounterId      = encounterId;
            ClaimantStableId = claimantStableId;
        }
    }

    /// <summary>Emitted on every request so observers need no FMOD coupling.</summary>
    public readonly struct AudioRequestObservation
    {
        public readonly ProofAudioEvent Event;
        public readonly AudioRequestResult Result;
        public readonly AudioRequestContext Context;
        public readonly string ResolvedPath;

        public AudioRequestObservation(
            ProofAudioEvent id, AudioRequestResult result,
            AudioRequestContext context, string resolvedPath)
        {
            Event        = id;
            Result       = result;
            Context      = context;
            ResolvedPath = resolvedPath;
        }
    }
}
