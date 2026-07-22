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
                Sanity = 100f,
                SoulIntegrity = 100f,
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
                run.ApplyClaimResolution(outcome);

                Assert.AreEqual(12, data.CorporateCredits);
                Assert.AreEqual(96f, data.Sanity);
                Assert.AreEqual(99f, data.SoulIntegrity);
                Assert.AreEqual(1, data.ClaimsProcessedThisAnte);
                Assert.AreEqual(1, data.Stats.ClaimsProcessed);
                Assert.AreEqual(1, data.Stats.ApprovedClaims);
                Assert.AreEqual(1, sanityEvents);

                // ClaimResolvedEvent is now past tense and notification-only.
                RumorMill.PublishDeferred(new ClaimResolvedEvent(
                    "claim", outcome, "client", "human"));
                typeof(RumorMill)
                    .GetMethod("DrainQueue", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, null);

                Assert.AreEqual(12, data.CorporateCredits);
                Assert.AreEqual(96f, data.Sanity);
                Assert.AreEqual(99f, data.SoulIntegrity);
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
    }
}
