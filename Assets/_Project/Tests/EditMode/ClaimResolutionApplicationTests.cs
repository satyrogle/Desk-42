using System.Reflection;
using Desk42.Core;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    public sealed class ClaimResolutionApplicationTests
    {
        [Test]
        public void DeferredNotification_DoesNotReapplyResourcesOrQuota()
        {
            var go = new GameObject("RunStateController_Test");
            var run = go.AddComponent<RunStateController>();
            var data = new RunData
            {
                Sanity = 2f,
                SoulIntegrity = 0.5f,
                CorporateCredits = 0,
                ComboMultiplier = 1f,
            };
            typeof(RunStateController)
                .GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(run, data);

            int sanityEvents = 0;
            System.Action<SanityChangedEvent> sanityHandler = _ => sanityEvents++;
            RumorMill.OnSanityChanged += sanityHandler;
            try
            {
                var outcome = new ClaimResolutionOutcome(
                    ClaimResolutionKind.Approve,
                    creditsEarned: 12, sanityCost: 4f, soulCost: 1f);
                var applied = run.ApplyClaimResolution(
                    outcome, "claim", "client", "human");

                Assert.AreEqual(12, data.CorporateCredits);
                Assert.AreEqual(0f, data.Sanity);
                Assert.AreEqual(0f, data.SoulIntegrity);
                Assert.AreEqual(1, data.ClaimsProcessedThisAnte);
                Assert.AreEqual(1, data.Stats.ClaimsProcessed);
                Assert.AreEqual(1, data.Stats.ApprovedClaims);
                Assert.AreEqual(1, sanityEvents);
                Assert.AreEqual(12, applied.CreditsDelta);
                Assert.AreEqual(-2f, applied.SanityDelta);
                Assert.AreEqual(-0.5f, applied.SoulIntegrityDelta);
                Assert.AreEqual(0, applied.QuotaBefore);
                Assert.AreEqual(1, applied.QuotaAfter);
                Assert.AreEqual(1f, applied.ComplianceStreakBefore);
                Assert.AreEqual(1.1f, applied.ComplianceStreakAfter, 0.001f);

                // ClaimResolvedEvent is now past tense and notification-only.
                RumorMill.PublishDeferred(new ClaimResolvedEvent(applied));
                typeof(RumorMill)
                    .GetMethod("DrainQueue", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, null);

                Assert.AreEqual(12, data.CorporateCredits);
                Assert.AreEqual(0f, data.Sanity);
                Assert.AreEqual(0f, data.SoulIntegrity);
                Assert.AreEqual(1, data.ClaimsProcessedThisAnte);
                Assert.AreEqual(1, data.Stats.ClaimsProcessed);
                Assert.AreEqual(1, sanityEvents);
            }
            finally
            {
                RumorMill.OnSanityChanged -= sanityHandler;
                RumorMill.FlushQueue();
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Liquify_AppliedResultOwnsDarkIntelligenceAndFactualBucket()
        {
            var go = new GameObject("RunStateController_LiquifyTest");
            var run = go.AddComponent<RunStateController>();
            var data = new RunData
            {
                DarkIntelligence = 7,
                ComboMultiplier = 1.4f,
                QuotaForCurrentAnte = 3,
            };
            typeof(RunStateController)
                .GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(run, data);

            try
            {
                var applied = run.ApplyClaimResolution(
                    ClaimResolutionConsequencePolicy.Liquify(),
                    "claim-liquify", "client", "moth");

                Assert.AreEqual(ClaimResolutionKind.Liquify, applied.Kind);
                Assert.AreEqual(3, applied.DarkIntelligenceDelta);
                Assert.AreEqual(10, data.DarkIntelligence);
                Assert.AreEqual(1, data.Stats.LiquifiedClaims);
                Assert.AreEqual(0, data.Stats.ApprovedClaims);
                Assert.AreEqual(0, data.Stats.DeniedClaims);
                Assert.AreEqual(1, applied.QuotaAfter);
                Assert.AreEqual(3, applied.QuotaRequired);
                Assert.AreEqual(1.4f, applied.ComplianceStreakBefore, 0.001f);
                Assert.AreEqual(1f, applied.ComplianceStreakAfter, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FailedSpendRefund_RestoresBalanceWithoutCreatingIncome()
        {
            var go = new GameObject("RunStateController_RefundTest");
            var run = go.AddComponent<RunStateController>();
            var data = new RunData { CorporateCredits = 20 };
            typeof(RunStateController)
                .GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(run, data);

            try
            {
                Assert.IsTrue(run.SpendCredits(6));
                run.RefundFailedSpend(6);

                Assert.AreEqual(20, data.CorporateCredits);
                Assert.AreEqual(0, data.Stats.CreditsSpent);
                Assert.AreEqual(0, data.Stats.CreditsEarned);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
