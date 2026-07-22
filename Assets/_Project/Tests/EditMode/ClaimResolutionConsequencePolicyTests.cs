using Desk42.Core;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    public sealed class ClaimResolutionConsequencePolicyTests
    {
        [Test]
        public void Approve_OwnsCreditSanityAndSoulMath()
        {
            var claim = new ActiveClaimData
            {
                AnomalyTagIds = new[] { "spectral", "temporal", "extra" },
            };

            var result = ClaimResolutionConsequencePolicy.Resolve(
                ClaimResolutionKind.Approve, claim, shiftNumber: 3,
                baseApprovalCredits: 10, payoutMultiplier: 1.5f);

            Assert.AreEqual(ClaimResolutionKind.Approve, result.Kind);
            Assert.AreEqual(24, result.CreditsEarned);
            Assert.AreEqual(5f, result.SanityCost);
            Assert.AreEqual(1f, result.SoulCost);
        }

        [Test]
        public void Deny_HasNoPayoutOrSoulCost_ButStillCostsSanity()
        {
            var result = ClaimResolutionConsequencePolicy.Resolve(
                ClaimResolutionKind.Deny, new ActiveClaimData(), shiftNumber: 5,
                baseApprovalCredits: 10, payoutMultiplier: 2f);

            Assert.AreEqual(ClaimResolutionKind.Deny, result.Kind);
            Assert.AreEqual(0, result.CreditsEarned);
            Assert.AreEqual(3f, result.SanityCost);
            Assert.AreEqual(0f, result.SoulCost);
        }

        [Test]
        public void Liquify_IsAnExplicitZeroResourceOutcome()
        {
            var result = ClaimResolutionConsequencePolicy.Liquify();

            Assert.AreEqual(ClaimResolutionKind.Liquify, result.Kind);
            Assert.AreEqual(0, result.CreditsEarned);
            Assert.AreEqual(0f, result.SanityCost);
            Assert.AreEqual(0f, result.SoulCost);
        }

        [Test]
        public void Unspecified_CannotResolveANormalClaim()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                ClaimResolutionConsequencePolicy.Resolve(
                    ClaimResolutionKind.Unspecified,
                    new ActiveClaimData(),
                    shiftNumber: 1,
                    baseApprovalCredits: 10,
                    payoutMultiplier: 1f));
        }
    }
}
