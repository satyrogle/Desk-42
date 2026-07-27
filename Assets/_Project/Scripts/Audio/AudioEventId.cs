// ============================================================
// DESK 42 — Application audio event contract (D1B)
//
// ONE authoritative list of logical audio identities for the
// application, plus the single mapping layer to eventual FMOD paths.
// Gameplay code references the enum; it never contains an FMOD path
// string.
//
// AudioEventId is the APPLICATION-LEVEL namespace, not a proof-only one:
// it carries ordinary desk audio (PneumaticTubeThreat) alongside the
// proof identities. The proof subset is named explicitly in
// ProofAudioCatalog.ProofSubset rather than inferred from the type
// name, so adding a non-proof identity can never silently widen what
// counts as proof audio.
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
using UnityEngine;

namespace Desk42.Audio
{
    /// <summary>
    /// Stable logical audio identities for the proof slice. Values are
    /// explicit so reordering cannot silently change a saved/logged identity.
    /// </summary>
    public enum AudioEventId
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

        /// <summary>
        /// Pneumatic tube threat feedback. ORDINARY desk audio — deliberately
        /// NOT a proof identity: it is never a fallback for a proof event, is
        /// absent from the Shift 2 / Shift 5 causal mappings, and has no
        /// relationship to Elias or the control claimant.
        /// </summary>
        PneumaticTubeThreat = 6,
    }

    /// <summary>
    /// The single mapping layer between logical identities and FMOD paths.
    /// Kept as plain strings so it compiles without the FMOD package.
    /// </summary>
    public static class ProofAudioCatalog
    {
        private static readonly Dictionary<AudioEventId, string> Paths = new()
        {
            [AudioEventId.DeskInteraction]         = "event:/Desk/Interaction",
            [AudioEventId.ProcedureFeedback]       = "event:/Desk/ProcedureApplied",
            [AudioEventId.EliasRegistrationCausal] = "event:/Proof/EliasRegistration18A",
            [AudioEventId.ComplianceStreakConfirm] = "event:/Desk/ComplianceStreak",
            [AudioEventId.Shift5EliasReturn]       = "event:/Proof/EliasReturnGeneric",
            [AudioEventId.PneumaticTubeThreat]     = "event:/Threat/PneumaticTube",
        };

        /// <summary>Every identity in the application contract.</summary>
        public static IEnumerable<AudioEventId> All => Paths.Keys;

        /// <summary>
        /// The Five-Shift proof subset, stated explicitly. Membership is
        /// declared here, never inferred from the enum type name, so a future
        /// non-proof identity cannot quietly join the proof surface.
        /// </summary>
        public static readonly AudioEventId[] ProofSubset =
        {
            AudioEventId.DeskInteraction,
            AudioEventId.ProcedureFeedback,
            AudioEventId.EliasRegistrationCausal,
            AudioEventId.ComplianceStreakConfirm,
            AudioEventId.Shift5EliasReturn,
        };

        /// <summary>True when the identity belongs to the proof contract.</summary>
        public static bool IsProofIdentity(AudioEventId id)
            => System.Array.IndexOf(ProofSubset, id) >= 0;

        /// <summary>
        /// FMOD path for a logical identity, or null when unmapped.
        /// Returning null rather than throwing keeps a missing mapping a
        /// diagnosable no-op instead of a crash mid-encounter.
        /// </summary>
        public static string TryGetPath(AudioEventId id)
            => Paths.TryGetValue(id, out string path) ? path : null;

        /// <summary>
        /// True for the Shift 2 causal identity. Exists so the Shift 5
        /// suppression rule is directly assertable rather than assumed.
        /// </summary>
        public static bool IsCausalIdentity(AudioEventId id)
            => id == AudioEventId.EliasRegistrationCausal;

        /// <summary>
        /// Identities permitted during the scored Shift 5 return. The causal
        /// identity is excluded by construction, not by convention.
        /// </summary>
        public static bool IsPermittedOnShift5(AudioEventId id)
            => id != AudioEventId.EliasRegistrationCausal && id != AudioEventId.None;
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

        /// <summary>
        /// Optional world position for spatialised one-shots. Vector3 is a Unity
        /// type, not an FMOD one, so carrying it here keeps the boundary
        /// FMOD-free while preserving spatial intent rather than discarding it
        /// to fit the abstraction.
        /// </summary>
        public readonly Vector3? WorldPosition;

        public AudioRequestContext(
            int shiftNumber, string encounterId = null, string claimantStableId = null,
            Vector3? worldPosition = null)
        {
            ShiftNumber      = shiftNumber;
            EncounterId      = encounterId;
            ClaimantStableId = claimantStableId;
            WorldPosition    = worldPosition;
        }

        /// <summary>Spatial one-shot with no encounter context.</summary>
        public static AudioRequestContext AtPosition(int shiftNumber, Vector3 position)
            => new(shiftNumber, worldPosition: position);
    }

    /// <summary>Emitted on every request so observers need no FMOD coupling.</summary>
    public readonly struct AudioRequestObservation
    {
        public readonly AudioEventId Event;
        public readonly AudioRequestResult Result;
        public readonly AudioRequestContext Context;
        public readonly string ResolvedPath;

        public AudioRequestObservation(
            AudioEventId id, AudioRequestResult result,
            AudioRequestContext context, string resolvedPath)
        {
            Event        = id;
            Result       = result;
            Context      = context;
            ResolvedPath = resolvedPath;
        }
    }
}
