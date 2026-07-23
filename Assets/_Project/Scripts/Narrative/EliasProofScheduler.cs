using System;
using System.Collections.Generic;

namespace Desk42.Core
{
    /// <summary>
    /// Replaces one already-generated queue slot with the authored Elias
    /// appearance. It never draws from SeedEngine and never changes queue size.
    /// </summary>
    public static class EliasProofScheduler
    {
        private const string AuthoredTemplateId =
            "authored_elias_proof";
        private const int AuthoredClaimAmount = 42;

        public static bool TryReplaceScheduledClaim(
            IList<ActiveClaimData> generatedClaims,
            int shiftNumber,
            EliasProofContent content,
            EliasProofSessionState state,
            out ActiveClaimData scheduledClaim)
        {
            scheduledClaim = null;
            if (state?.IsActive != true)
                return false;
            if (content == null)
            {
                throw new InvalidOperationException(
                    "Elias proof content is required for authored scheduling.");
            }
            if (generatedClaims == null)
                throw new ArgumentNullException(nameof(generatedClaims));

            string appearanceKey = shiftNumber switch
            {
                1 => EliasProofContent.Shift1AppearanceKey,
                2 => EliasProofContent.Shift2AppearanceKey,
                5 => EliasProofContent.Shift5AppearanceKey,
                _ => null,
            };
            if (appearanceKey == null)
                return false;

            ValidatePrecedingProofState(shiftNumber, state);
            if (!content.TryGetAppearance(
                    appearanceKey, out EliasAuthoredAppearance appearance))
            {
                throw new InvalidOperationException(
                    $"Missing authored Elias appearance '{appearanceKey}'.");
            }

            int slotIndex = appearance.QueuePosition - 1;
            if (slotIndex < 0 || slotIndex >= generatedClaims.Count)
            {
                throw new InvalidOperationException(
                    $"Elias queue position {appearance.QueuePosition} is " +
                    $"outside a generated queue of {generatedClaims.Count}.");
            }

            string claimId = SelectClaimId(shiftNumber, appearance, state);
            scheduledClaim = BuildClaim(
                claimId, appearanceKey, shiftNumber, state.Shift2Branch,
                content);
            generatedClaims[slotIndex] = scheduledClaim;

            int eliasCount = 0;
            foreach (ActiveClaimData claim in generatedClaims)
            {
                if (string.Equals(
                        claim?.ClientVariantId,
                        EliasProofContent.CanonicalClaimantId,
                        StringComparison.Ordinal))
                {
                    eliasCount++;
                }
            }
            if (eliasCount != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one Elias claim after replacement, " +
                    $"found {eliasCount}.");
            }
            return true;
        }

        private static void ValidatePrecedingProofState(
            int shiftNumber, EliasProofSessionState state)
        {
            if (shiftNumber >= 2
                && state.Shift1Disposition == EliasShift1Disposition.None)
            {
                throw new InvalidOperationException(
                    "Shift 2 Elias cannot be scheduled before Shift 1 " +
                    "has a factual disposition.");
            }
            if (shiftNumber == 5)
            {
                if (state.Shift2Branch == EliasShift2Branch.None)
                {
                    throw new InvalidOperationException(
                        "Shift 5 Elias cannot be scheduled with no Shift 2 branch.");
                }
                if (state.Shift2FinalDisposition
                    == ClaimResolutionKind.Unspecified)
                {
                    throw new InvalidOperationException(
                        "Shift 5 Elias cannot be scheduled before the Shift 2 " +
                        "claim has a factual disposition.");
                }
            }
        }

        private static string SelectClaimId(
            int shiftNumber,
            EliasAuthoredAppearance appearance,
            EliasProofSessionState state)
        {
            if (shiftNumber != 5)
            {
                if (appearance.AuthoredClaimIds.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Appearance '{appearance.AppearanceKey}' must own " +
                        "exactly one authored claim ID.");
                }
                return appearance.AuthoredClaimIds[0];
            }

            int branchIndex = state.Shift2Branch switch
            {
                EliasShift2Branch.NormalisedAddress => 0,
                EliasShift2Branch.LegacyException => 1,
                EliasShift2Branch.PhysicalVerification => 2,
                _ => throw new InvalidOperationException(
                    "Shift 5 claim routing requires an Elias branch."),
            };
            if (appearance.AuthoredClaimIds.Length <= branchIndex)
            {
                throw new InvalidOperationException(
                    $"Appearance '{appearance.AppearanceKey}' has no authored " +
                    $"claim ID for branch {state.Shift2Branch}.");
            }
            return appearance.AuthoredClaimIds[branchIndex];
        }

        private static ActiveClaimData BuildClaim(
            string claimId,
            string appearanceKey,
            int shiftNumber,
            EliasShift2Branch branch,
            EliasProofContent content)
            => new()
            {
                ClaimId = claimId,
                ClientVariantId =
                    EliasProofContent.CanonicalClaimantId,
                ClientSpeciesId = content.ClientSpeciesId,
                TemplateId = AuthoredTemplateId,
                AuthoredAppearanceKey = appearanceKey,
                AnomalyTagIds = Array.Empty<string>(),
                HiddenTraitId = null,
                TraitRevealed = false,
                CorruptionLevel = 0f,
                CorruptionSeed = StableHash(claimId),
                NDARequired = false,
                NDASigned = false,
                IsResolved = false,
                ResolutionKind = ClaimResolutionKind.Unspecified,
                IncidentText = BuildIncidentText(
                    shiftNumber, branch),
                ClaimantName = content.DisplayName,
                ClaimAmount = AuthoredClaimAmount,
            };

        private static string BuildIncidentText(
            int shiftNumber, EliasShift2Branch branch)
        {
            if (shiftNumber == 1)
            {
                return "Household registration review: Elias Venn is listed " +
                    "at 18B Calder House. Process the claim.";
            }
            if (shiftNumber == 2)
            {
                return "Address amendment notice: 18B Calder House is being " +
                    "renumbered to 18A. Choose a record procedure, then " +
                    "disposition the claim.";
            }
            return branch switch
            {
                EliasShift2Branch.NormalisedAddress =>
                    "The 18A amendment now conflicts with M. Venn's " +
                    "independent household registration.",
                EliasShift2Branch.LegacyException =>
                    "The retained 18B exception has returned Elias's " +
                    "household file without clarification.",
                EliasShift2Branch.PhysicalVerification =>
                    "Physical verification remains open for Calder House. " +
                    "Elias's claim is held in the resulting backlog.",
                _ => throw new InvalidOperationException(
                    "Shift 5 copy requires an Elias branch."),
            };
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in value ?? string.Empty)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }
    }
}
