using UnityEngine;

namespace Desk42.Core
{
    public enum ClaimResolutionKind
    {
        Approve,
        Deny,
        Liquify,
    }

    /// <summary>
    /// Complete, immutable consequence payload for one claim resolution.
    /// Resource mutation happens synchronously from this payload exactly once;
    /// ClaimResolvedEvent is the later notification copy of the same outcome.
    /// </summary>
    public readonly struct ClaimResolutionOutcome
    {
        public readonly ClaimResolutionKind Kind;
        public readonly bool ResolvedCorrectly;
        public readonly int CreditsEarned;
        public readonly float SanityCost;
        public readonly float SoulCost;

        public ClaimResolutionOutcome(ClaimResolutionKind kind,
            bool resolvedCorrectly, int creditsEarned,
            float sanityCost, float soulCost)
        {
            Kind = kind;
            ResolvedCorrectly = resolvedCorrectly;
            CreditsEarned = Mathf.Max(0, creditsEarned);
            SanityCost = Mathf.Max(0f, sanityCost);
            SoulCost = Mathf.Max(0f, soulCost);
        }
    }

    /// <summary>Pure policy for all normal and alternate claim exits.</summary>
    public static class ClaimResolutionConsequencePolicy
    {
        public const float BaseSanityCost = 3f;
        public const float SanityPerAnomaly = 1f;
        public const int MaxChargedAnomalies = 2;
        public const float ApprovalSoulCost = 1f;

        public static ClaimResolutionOutcome Resolve(
            bool approved,
            ActiveClaimData claim,
            int shiftNumber,
            int baseApprovalCredits,
            float payoutMultiplier)
        {
            int anomalyCount = claim?.AnomalyTagIds?.Length ?? 0;
            float sanityCost = BaseSanityCost +
                Mathf.Min(anomalyCount, MaxChargedAnomalies) * SanityPerAnomaly;

            if (!approved)
                return new ClaimResolutionOutcome(
                    ClaimResolutionKind.Deny, false, 0, sanityCost, 0f);

            int credits = Mathf.RoundToInt(
                (baseApprovalCredits + Mathf.Max(1, shiftNumber) * 2) * payoutMultiplier);
            return new ClaimResolutionOutcome(
                ClaimResolutionKind.Approve, true, credits,
                sanityCost, ApprovalSoulCost);
        }

        public static ClaimResolutionOutcome Liquify()
            => new(ClaimResolutionKind.Liquify, false, 0, 0f, 0f);
    }
}
