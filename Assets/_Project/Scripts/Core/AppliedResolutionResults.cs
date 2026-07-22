using Desk42.BSM;
using Desk42.Cards;
using Desk42.OfficeSupplies;

namespace Desk42.Core
{
    /// <summary>
    /// Immutable record of what one claim disposition actually changed after
    /// clamping, vows, and all other mutation rules were applied.
    /// </summary>
    public readonly struct AppliedClaimResolution
    {
        public readonly string ClaimId;
        public readonly string ClientVariantId;
        public readonly string ClientSpeciesId;
        public readonly ClaimResolutionKind Kind;
        public readonly int CreditsDelta;
        public readonly float SanityDelta;
        public readonly float SoulIntegrityDelta;
        public readonly int DarkIntelligenceDelta;
        public readonly int QuotaBefore;
        public readonly int QuotaAfter;
        public readonly int QuotaRequired;
        public readonly float ComplianceStreakBefore;
        public readonly float ComplianceStreakAfter;

        public AppliedClaimResolution(
            string claimId,
            string clientVariantId,
            string clientSpeciesId,
            ClaimResolutionKind kind,
            int creditsDelta,
            float sanityDelta,
            float soulIntegrityDelta,
            int darkIntelligenceDelta,
            int quotaBefore,
            int quotaAfter,
            int quotaRequired,
            float complianceStreakBefore,
            float complianceStreakAfter)
        {
            ClaimId = claimId;
            ClientVariantId = clientVariantId;
            ClientSpeciesId = clientSpeciesId;
            Kind = kind;
            CreditsDelta = creditsDelta;
            SanityDelta = sanityDelta;
            SoulIntegrityDelta = soulIntegrityDelta;
            DarkIntelligenceDelta = darkIntelligenceDelta;
            QuotaBefore = quotaBefore;
            QuotaAfter = quotaAfter;
            QuotaRequired = quotaRequired;
            ComplianceStreakBefore = complianceStreakBefore;
            ComplianceStreakAfter = complianceStreakAfter;
        }
    }

    /// <summary>
    /// Immutable record of what one card attempt actually changed. Failed
    /// attempts use the same schema so their reason cannot be lost or inferred.
    /// </summary>
    public readonly struct AppliedCardResolution
    {
        public readonly PunchCardType CardType;
        public readonly string CardInstanceId;
        public readonly string ClientVariantId;
        public readonly CardSlamOutcome Outcome;
        public readonly string FailureReason;
        public readonly ClientStateID? StateBefore;
        public readonly ClientStateID? StateAfter;
        public readonly int CreditsDelta;
        public readonly float SanityDelta;
        public readonly float SoulIntegrityDelta;
        public readonly int RequiredCredits;
        public readonly int FatigueBefore;
        public readonly int FatigueAfter;
        public readonly SynergyResolutionPacket Cascade;
        public readonly bool HasCascade;
        public readonly string ClientEffect;
        public readonly float ClientEffectDuration;
        public readonly string BlockingModifierId;

        public bool IsSuccess => Outcome == CardSlamOutcome.Success;

        public AppliedCardResolution(
            PunchCardType cardType,
            string cardInstanceId,
            string clientVariantId,
            CardSlamOutcome outcome,
            string failureReason,
            ClientStateID? stateBefore,
            ClientStateID? stateAfter,
            int creditsDelta,
            float sanityDelta,
            float soulIntegrityDelta,
            int requiredCredits,
            int fatigueBefore,
            int fatigueAfter,
            SynergyResolutionPacket cascade,
            bool hasCascade,
            string clientEffect = null,
            float clientEffectDuration = 0f,
            string blockingModifierId = null)
        {
            CardType = cardType;
            CardInstanceId = cardInstanceId;
            ClientVariantId = clientVariantId;
            Outcome = outcome;
            FailureReason = failureReason;
            StateBefore = stateBefore;
            StateAfter = stateAfter;
            CreditsDelta = creditsDelta;
            SanityDelta = sanityDelta;
            SoulIntegrityDelta = soulIntegrityDelta;
            RequiredCredits = requiredCredits;
            FatigueBefore = fatigueBefore;
            FatigueAfter = fatigueAfter;
            Cascade = cascade;
            HasCascade = hasCascade;
            ClientEffect = clientEffect;
            ClientEffectDuration = clientEffectDuration;
            BlockingModifierId = blockingModifierId;
        }
    }

    /// <summary>
    /// Side-effect-free projection of a claim disposition against the current
    /// run snapshot. UI must label this as expected: live state may change
    /// between intent and application. AppliedClaimResolution remains truth.
    /// </summary>
    public readonly struct ProjectedClaimResolution
    {
        public readonly ClaimResolutionKind Kind;
        public readonly int CreditsDelta;
        public readonly float SanityDelta;
        public readonly float SoulIntegrityDelta;
        public readonly int DarkIntelligenceDelta;
        public readonly int QuotaBefore;
        public readonly int QuotaAfter;
        public readonly int QuotaRequired;
        public readonly float ComplianceStreakBefore;
        public readonly float ComplianceStreakAfter;
        public readonly string[] Notices;

        public ProjectedClaimResolution(
            ClaimResolutionKind kind,
            int creditsDelta,
            float sanityDelta,
            float soulIntegrityDelta,
            int darkIntelligenceDelta,
            int quotaBefore,
            int quotaAfter,
            int quotaRequired,
            float complianceStreakBefore,
            float complianceStreakAfter,
            string[] notices = null)
        {
            Kind = kind;
            CreditsDelta = creditsDelta;
            SanityDelta = sanityDelta;
            SoulIntegrityDelta = soulIntegrityDelta;
            DarkIntelligenceDelta = darkIntelligenceDelta;
            QuotaBefore = quotaBefore;
            QuotaAfter = quotaAfter;
            QuotaRequired = quotaRequired;
            ComplianceStreakBefore = complianceStreakBefore;
            ComplianceStreakAfter = complianceStreakAfter;
            Notices = notices ?? System.Array.Empty<string>();
        }
    }

    /// <summary>
    /// Side-effect-free projection of one card attempt. It mirrors the applied
    /// result schema but is never published as an outcome or used for mutation.
    /// </summary>
    public readonly struct ProjectedCardResolution
    {
        public readonly PunchCardType CardType;
        public readonly string CardDisplayName;
        public readonly string CardInstanceId;
        public readonly CardSlamOutcome Outcome;
        public readonly string FailureReason;
        public readonly ClientStateID? StateBefore;
        public readonly ClientStateID? StateAfter;
        public readonly int CreditsDelta;
        public readonly float SanityDelta;
        public readonly float SoulIntegrityDelta;
        public readonly int RequiredCredits;
        public readonly int FatigueBefore;
        public readonly int FatigueAfter;
        public readonly SynergyResolutionPacket Cascade;
        public readonly bool HasCascade;
        public readonly string ClientEffect;
        public readonly float ClientEffectDuration;
        public readonly string BlockingModifierId;
        public readonly bool IsBlockingModifierRevealed;
        public readonly bool HasConcealedBlockRisk;
        public readonly string[] Notices;

        public bool IsExpectedSuccess => Outcome == CardSlamOutcome.Success;

        public ProjectedCardResolution(
            PunchCardType cardType,
            string cardDisplayName,
            string cardInstanceId,
            CardSlamOutcome outcome,
            string failureReason,
            ClientStateID? stateBefore,
            ClientStateID? stateAfter,
            int creditsDelta,
            float sanityDelta,
            float soulIntegrityDelta,
            int requiredCredits,
            int fatigueBefore,
            int fatigueAfter,
            SynergyResolutionPacket cascade,
            bool hasCascade,
            string[] notices = null,
            string clientEffect = null,
            float clientEffectDuration = 0f,
            string blockingModifierId = null,
            bool isBlockingModifierRevealed = false,
            bool hasConcealedBlockRisk = false)
        {
            CardType = cardType;
            CardDisplayName = cardDisplayName;
            CardInstanceId = cardInstanceId;
            Outcome = outcome;
            FailureReason = failureReason;
            StateBefore = stateBefore;
            StateAfter = stateAfter;
            CreditsDelta = creditsDelta;
            SanityDelta = sanityDelta;
            SoulIntegrityDelta = soulIntegrityDelta;
            RequiredCredits = requiredCredits;
            FatigueBefore = fatigueBefore;
            FatigueAfter = fatigueAfter;
            Cascade = cascade;
            HasCascade = hasCascade;
            ClientEffect = clientEffect;
            ClientEffectDuration = clientEffectDuration;
            BlockingModifierId = blockingModifierId;
            IsBlockingModifierRevealed = isBlockingModifierRevealed;
            HasConcealedBlockRisk = hasConcealedBlockRisk;
            Notices = notices ?? System.Array.Empty<string>();
        }
    }
}
