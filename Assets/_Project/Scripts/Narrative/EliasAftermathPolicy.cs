using System;
using System.Collections.Generic;
using System.Linq;

namespace Desk42.Core
{
    public readonly struct EliasAftermathDefinition
    {
        public readonly string ModifierId;
        public readonly string[] ClaimIds;

        public EliasAftermathDefinition(
            string modifierId, string[] claimIds)
        {
            ModifierId = modifierId;
            ClaimIds = claimIds ?? Array.Empty<string>();
        }
    }

    public readonly struct AppliedEliasAftermath
    {
        public readonly string ProofSessionId;
        public readonly EliasShift2Branch Branch;
        public readonly string ModifierId;
        public readonly string ClaimId;
        public readonly int AppliedCount;
        public readonly int TotalClaimCount;
        public readonly int RemainingClaimCount;

        public AppliedEliasAftermath(
            string proofSessionId,
            EliasShift2Branch branch,
            string modifierId,
            string claimId,
            int appliedCount,
            int totalClaimCount,
            int remainingClaimCount)
        {
            ProofSessionId = proofSessionId;
            Branch = branch;
            ModifierId = modifierId;
            ClaimId = claimId;
            AppliedCount = appliedCount;
            TotalClaimCount = totalClaimCount;
            RemainingClaimCount = remainingClaimCount;
        }
    }

    /// <summary>
    /// The deliberately proof-specific mapping from the Shift 2 branch to
    /// exact Shift 5 follow-up claim IDs. It is not a generic tag or next-N
    /// condition engine.
    /// </summary>
    public static class EliasAftermathPolicy
    {
        public const string HouseholdDuplicateReview =
            "HouseholdDuplicateReview";
        public const string InternalAuditLockdown =
            "InternalAuditLockdown";
        public const string VerificationBacklog =
            "VerificationBacklog";

        public static EliasAftermathDefinition ForBranch(
            EliasProofContent content, EliasShift2Branch branch)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            string modifierId;
            string claimPrefix;
            int expectedCount;
            switch (branch)
            {
                case EliasShift2Branch.NormalisedAddress:
                    modifierId = HouseholdDuplicateReview;
                    claimPrefix = "elias_aftermath_5a_";
                    expectedCount = 2;
                    break;
                case EliasShift2Branch.LegacyException:
                    modifierId = InternalAuditLockdown;
                    claimPrefix = "elias_aftermath_5b_";
                    expectedCount = 1;
                    break;
                case EliasShift2Branch.PhysicalVerification:
                    modifierId = VerificationBacklog;
                    claimPrefix = "elias_aftermath_5c_";
                    expectedCount = 2;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Elias aftermath requires an established Shift 2 branch.");
            }

            string[] claimIds = content.AuthoredFollowUpClaimIds
                .Where(id => !string.IsNullOrWhiteSpace(id)
                    && id.StartsWith(
                        claimPrefix, StringComparison.Ordinal))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (claimIds.Length != expectedCount
                || claimIds.Distinct(StringComparer.Ordinal).Count()
                    != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Aftermath '{modifierId}' requires {expectedCount} " +
                    $"unique authored IDs with prefix '{claimPrefix}', " +
                    $"found {claimIds.Length}.");
            }

            return new EliasAftermathDefinition(
                modifierId, claimIds);
        }

        public static void GetClaimCopy(
            string claimId,
            out string claimantName,
            out string incidentText)
        {
            switch (claimId)
            {
                case "elias_aftermath_5a_1":
                    claimantName = "Miriam Venn";
                    incidentText =
                        "Household registry review: Elias Venn and M. Venn " +
                        "are both registered at 18A Calder House. Process " +
                        "the duplicate-household file.";
                    return;
                case "elias_aftermath_5a_2":
                    claimantName = "Calder House Registry";
                    incidentText =
                        "A second 18A household claim is held under the " +
                        "same duplicate-household review.";
                    return;
                case "elias_aftermath_5b_1":
                    claimantName = "Internal Audit";
                    incidentText =
                        "Internal Audit has locked the retained 18B " +
                        "exception. Process this claim under the lockdown.";
                    return;
                case "elias_aftermath_5c_1":
                    claimantName = "Verification Desk";
                    incidentText =
                        "Calder House physical verification remains open. " +
                        "This claim is waiting in the resulting backlog.";
                    return;
                case "elias_aftermath_5c_2":
                    claimantName = "Calder House Applicant";
                    incidentText =
                        "A second claim is held behind the unresolved " +
                        "Calder House verification.";
                    return;
                default:
                    throw new ArgumentException(
                        $"Unknown authored Elias aftermath claim '{claimId}'.",
                        nameof(claimId));
            }
        }
    }
}
