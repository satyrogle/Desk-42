using Desk42.Core;
using Desk42.RedTape;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    public sealed class AppliedCardResolutionTests
    {
        [Test]
        public void InvalidCard_PublishesSpecificAppliedFailure()
        {
            var go = new GameObject("StateInjector_InvalidCardTest");
            var injector = go.AddComponent<StateInjector>();
            CardSlammedEvent received = default;
            bool didReceive = false;
            System.Action<CardSlammedEvent> handler = e =>
            {
                received = e;
                didReceive = true;
            };
            RumorMill.OnCardSlammed += handler;

            try
            {
                var result = injector.TrySlam(null, "missing-card");

                Assert.AreEqual(SlamOutcome.InvalidCard, result.Outcome);
                Assert.IsTrue(didReceive, "Invalid input must not become silent feedback.");
                Assert.AreEqual(CardSlamOutcome.InvalidCard, received.Outcome);
                Assert.AreEqual("missing-card", received.CardInstanceId);
                Assert.AreEqual("No punch card data was supplied.", received.FailureReason);
                Assert.AreEqual(0, received.CreditsDelta);
                Assert.AreEqual(0f, received.SanityDelta);
                Assert.AreEqual(0f, received.SoulIntegrityDelta);
            }
            finally
            {
                RumorMill.OnCardSlammed -= handler;
                Object.DestroyImmediate(go);
            }
        }
    }
}
