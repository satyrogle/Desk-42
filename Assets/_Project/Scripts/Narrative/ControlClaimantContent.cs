// ============================================================
// DESK 42 — Control claimant (causal-confabulation control)
//
// Mara Kest exists so a tester can confidently attribute the Shift 5
// Elias outcome to the WRONG encounter. That only works if she is
// memorable: a forgettable filler claimant is not a control.
//
// Her anchor is a distinct administrative artifact —
//   MARA KEST
//   ROUTE 7C - MANUAL HOLD
// — which is memorable and procedurally weighty, but has no
// classification mechanic and no downstream dependent action.
//
// She is deliberately NOT mechanically equivalent to Elias:
//   - no 18A/18B classification
//   - no record-amendment procedure
//   - no branch, no aftermath, no dependent action
//   - never writes EliasProof state
//   - never shares an Elias reference id or appearance key
//   - cannot influence Shift 5 Elias state
//
// This type is data + guards only. It deliberately exposes no method
// that could mutate proof state, so the isolation is structural rather
// than a convention someone has to remember.
// ============================================================

using System;

namespace Desk42.Core
{
    public static class ControlClaimantContent
    {
        /// <summary>Stable id. Deliberately distinct from any Elias id.</summary>
        public const string StableClaimantId = "control_mara_kest";

        public const string DisplayName = "Mara Kest";
        public const string ClientSpeciesId = "unregistered_alien";

        /// <summary>Memory anchor. Not a classification.</summary>
        public const string RouteLabel = "ROUTE 7C";
        public const string HoldLabel  = "MANUAL HOLD";
        public const string Anchor     = "ROUTE 7C - MANUAL HOLD";

        /// <summary>Authored claim id. Not an Elias appearance key.</summary>
        public const string ClaimId = "control_mara_kest_claim";

        /// <summary>
        /// Shift she appears on: between the Shift 2 procedure and the Shift 5
        /// return, so she is available as a false-attribution candidate but is
        /// not adjacent to either causal endpoint.
        /// </summary>
        public const int AppearanceShiftNumber = 3;

        public const string IncidentText =
            "Routing correction: this file was diverted to Route 7C and is " +
            "held pending manual release. Mara Kest has asked, twice, which " +
            "desk releases it.";

        public const int ClaimAmount = 240;

        /// <summary>
        /// True when the id belongs to the control claimant. Used by the proof
        /// guards to refuse her any Elias-shaped operation.
        /// </summary>
        public static bool IsControlClaimant(string stableClaimantId)
            => string.Equals(stableClaimantId, StableClaimantId,
                StringComparison.Ordinal);

        /// <summary>
        /// The control claimant must never carry an Elias appearance key.
        /// Called by the scheduler when materialising her claim.
        /// </summary>
        public static void AssertNotEliasIdentity(
            string stableClaimantId, string appearanceKey)
        {
            if (!IsControlClaimant(stableClaimantId)) return;

            if (!string.IsNullOrWhiteSpace(appearanceKey))
            {
                throw new InvalidOperationException(
                    $"Control claimant '{StableClaimantId}' must never carry an " +
                    $"authored Elias appearance key (received '{appearanceKey}'). " +
                    "The control exists to be misattributed, not to participate " +
                    "in the causal chain.");
            }

            if (string.Equals(stableClaimantId,
                    EliasProofContent.CanonicalClaimantId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Control claimant id collides with the Elias canonical id.");
            }
        }

        /// <summary>Queue slot on her appearance shift.</summary>
        public const int QueuePosition = 2;

        /// <summary>
        /// Places the control claimant into the generated Shift 3 queue.
        ///
        /// Deliberately takes NO proof state and returns no proof handle: this
        /// method structurally cannot read or write EliasProof, so presenting
        /// or disposing Mara Kest cannot influence Elias's Shift 5 branch.
        ///
        /// A normal one-off presentation — no callback, no reminder, and no
        /// later appearance before attribution is measured.
        /// </summary>
        public static bool TryScheduleControlClaimant(
            System.Collections.Generic.IList<ActiveClaimData> generatedClaims,
            int shiftNumber,
            out ActiveClaimData scheduledClaim)
        {
            scheduledClaim = null;

            if (generatedClaims == null) return false;
            if (shiftNumber != AppearanceShiftNumber) return false;

            int slotIndex = QueuePosition - 1;
            if (slotIndex < 0 || slotIndex >= generatedClaims.Count)
                return false;

            scheduledClaim = BuildClaim();
            generatedClaims[slotIndex] = scheduledClaim;
            return true;
        }

        /// <summary>
        /// Materialises the control claim. No appearance key, no anomaly tags,
        /// no classification — she cannot enter the proof machinery.
        /// </summary>
        public static ActiveClaimData BuildClaim()
        {
            AssertNotEliasIdentity(StableClaimantId, null);

            return new ActiveClaimData
            {
                ClaimId               = ClaimId,
                ClientVariantId       = StableClaimantId,
                ClientSpeciesId       = ClientSpeciesId,
                TemplateId            = null,
                AuthoredAppearanceKey = null,   // never an Elias appearance
                AnomalyTagIds         = Array.Empty<string>(),
                HiddenTraitId         = null,
                TraitRevealed         = false,
                CorruptionLevel       = 0f,
                CorruptionSeed        = 0,
                NDARequired           = false,
                NDASigned             = false,
                IsResolved            = false,
                ResolutionKind        = ClaimResolutionKind.Unspecified,
                IncidentText          = IncidentText,
                ClaimantName          = DisplayName,
                ClaimAmount           = ClaimAmount,
            };
        }
    }
}
