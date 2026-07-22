using System;
using UnityEngine;

namespace Desk42.Core
{
    public enum ClaimResolutionKind
    {
        Unspecified = 0,
        Approve = 1,
        Deny = 2,
        Liquify = 3,
    }

    /// <summary>
    /// Complete, immutable consequence payload for one claim resolution.
    /// Resource mutation happens synchronously from this payload exactly once;
    /// ClaimResolvedEvent is the later notification copy of the same outcome.
    /// </summary>
    public readonly struct ClaimResolutionOutcome
    {
        public readonly ClaimResolutionKind Kind;
        public readonly int CreditsEarned;
        public readonly float SanityCost;
        public readonly float SoulCost;
        public readonly int DarkIntelligenceGain;

        public ClaimResolutionOutcome(ClaimResolutionKind kind,
            int creditsEarned,
            float sanityCost, float soulCost,
            int darkIntelligenceGain = 0)
        {
            Kind = kind;
            CreditsEarned = Mathf.Max(0, creditsEarned);
            SanityCost = Mathf.Max(0f, sanityCost);
            SoulCost = Mathf.Max(0f, soulCost);
            DarkIntelligenceGain = Mathf.Max(0, darkIntelligenceGain);
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
            ClaimResolutionKind kind,
            ActiveClaimData claim,
            int shiftNumber,
            int baseApprovalCredits,
            float payoutMultiplier)
        {
            int anomalyCount = claim?.AnomalyTagIds?.Length ?? 0;
            float sanityCost = BaseSanityCost +
                Mathf.Min(anomalyCount, MaxChargedAnomalies) * SanityPerAnomaly;

            switch (kind)
            {
                case ClaimResolutionKind.Approve:
                    int credits = Mathf.RoundToInt(
                        (baseApprovalCredits + Mathf.Max(1, shiftNumber) * 2) * payoutMultiplier);
                    return new ClaimResolutionOutcome(
                        ClaimResolutionKind.Approve, credits,
                        sanityCost, ApprovalSoulCost);

                case ClaimResolutionKind.Deny:
                    return new ClaimResolutionOutcome(
                        ClaimResolutionKind.Deny, 0, sanityCost, 0f);

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind,
                        "Normal claim resolution requires Approve or Deny.");
            }
        }

        public static ClaimResolutionOutcome Liquify()
            => new(ClaimResolutionKind.Liquify, 0, 0f, 0f,
                darkIntelligenceGain: 3);
    }
}
