using Desk42.Core;
using Desk42.BSM;
using Desk42.Cards;
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

        [Test]
        public void NoEffectProjection_TeachesTheSpecificTransitionRule()
        {
            var clientGo = new GameObject("StateInjector_PreviewClient");
            var client = clientGo.AddComponent<ClientStateMachine>();
            client.Initialize("preview-client", "human", 0,
                new System.Collections.Generic.List<string>());

            var injectorGo = new GameObject("StateInjector_PreviewTest");
            var injector = injectorGo.AddComponent<StateInjector>();
            injector.Initialize(client, new CardFatigueTracker(), new MutationEngine());

            var card = ScriptableObject.CreateInstance<PunchCardData>();
            card.CardType = PunchCardType.Analyse;
            card.DisplayName = "ANALYSE";
            card.CreditCost = 0;

            CardSlammedEvent received = default;
            System.Action<CardSlammedEvent> handler = e => received = e;
            RumorMill.OnCardSlammed += handler;

            try
            {
                var projection = injector.PreviewSlam(card, "analyse-1");

                Assert.AreEqual(CardSlamOutcome.BlockedByState, projection.Outcome);
                StringAssert.Contains("ANALYSE", projection.FailureReason);
                StringAssert.Contains("PENDING", projection.FailureReason);
                Assert.AreEqual(ClientStateID.Pending, client.CurrentMoodState,
                    "Preview must not alter the client.");

                var applied = injector.TrySlam(card, "analyse-1");

                Assert.AreEqual(SlamOutcome.BlockedByCurrentState, applied.Outcome);
                Assert.AreEqual(projection.FailureReason, received.FailureReason,
                    "The failure teaching shown before commitment must match the receipt.");
            }
            finally
            {
                RumorMill.OnCardSlammed -= handler;
                Object.DestroyImmediate(card);
                Object.DestroyImmediate(injectorGo);
                Object.DestroyImmediate(clientGo);
                RumorMill.ClearAllSubscriptions();
            }
        }

        [Test]
        public void InjectedStateProjection_NamesTheEffectAndMatchesTheReceipt()
        {
            var clientGo = new GameObject("StateInjector_InjectedPreviewClient");
            var client = clientGo.AddComponent<ClientStateMachine>();
            client.Initialize("preview-client", "human", 0,
                new System.Collections.Generic.List<string>());

            var injectorGo = new GameObject("StateInjector_InjectedPreviewTest");
            var injector = injectorGo.AddComponent<StateInjector>();
            injector.Initialize(client, new CardFatigueTracker(), new MutationEngine());

            var card = ScriptableObject.CreateInstance<PunchCardData>();
            card.CardType = PunchCardType.CooperationRoute;
            card.DisplayName = "COOPERATION ROUTE";
            card.InjectionDuration = 8f;
            card.CreditCost = 0;

            CardSlammedEvent received = default;
            System.Action<CardSlammedEvent> handler = e => received = e;
            RumorMill.OnCardSlammed += handler;

            try
            {
                var projection = injector.PreviewSlam(card, "cooperate-1");

                Assert.AreEqual(CardSlamOutcome.Success, projection.Outcome);
                Assert.AreEqual("CLIENT FORCED COOPERATIVE", projection.ClientEffect);
                Assert.AreEqual(8f, projection.ClientEffectDuration);
                Assert.AreEqual(ClientStateID.Pending, projection.StateBefore);
                Assert.AreEqual(ClientStateID.Pending, projection.StateAfter);

                var applied = injector.TrySlam(card, "cooperate-1");

                Assert.AreEqual(SlamOutcome.Success, applied.Outcome);
                Assert.AreEqual(projection.ClientEffect, received.Result.ClientEffect);
                Assert.AreEqual(projection.ClientEffectDuration,
                    received.Result.ClientEffectDuration);
            }
            finally
            {
                RumorMill.OnCardSlammed -= handler;
                Object.DestroyImmediate(card);
                Object.DestroyImmediate(injectorGo);
                Object.DestroyImmediate(clientGo);
                RumorMill.ClearAllSubscriptions();
            }
        }

        [Test]
        public void ConcealedBlocker_ProjectsRiskWithoutLeaking_ThenRevealsOnBlock()
        {
            var clientGo = new GameObject("StateInjector_ConcealedBlockClient");
            var client = clientGo.AddComponent<ClientStateMachine>();
            client.Initialize("concealed-client", "human", 0,
                new System.Collections.Generic.List<string>());
            client.BaseBT.InsertBlockerForCard(
                nameof(PunchCardType.PendingReview), "pre_filed_exemption");

            var injectorGo = new GameObject("StateInjector_ConcealedBlockTest");
            var injector = injectorGo.AddComponent<StateInjector>();
            injector.Initialize(client, new CardFatigueTracker(), new MutationEngine());

            var card = ScriptableObject.CreateInstance<PunchCardData>();
            card.CardType = PunchCardType.PendingReview;
            card.DisplayName = "PENDING REVIEW";
            card.InjectionDuration = 10f;
            card.CreditCost = 0;
            card.JamFatigue = 3;
            card.MaxFatigue = 5;

            CardSlammedEvent received = default;
            System.Action<CardSlammedEvent> handler = e => received = e;
            RumorMill.OnCardSlammed += handler;

            try
            {
                var before = injector.PreviewSlam(card, "pending-hidden");

                Assert.AreEqual(CardSlamOutcome.Success, before.Outcome,
                    "The face should show the visible-rules outcome, not disclose a secret deterministic block.");
                Assert.IsTrue(before.HasConcealedBlockRisk);
                Assert.IsFalse(before.IsBlockingModifierRevealed);
                Assert.IsNull(before.BlockingModifierId);
                StringAssert.DoesNotContain("pre_filed_exemption", before.FailureReason ?? "");
                StringAssert.DoesNotContain("pre filed exemption",
                    string.Join(" ", before.Notices).ToLowerInvariant());

                var applied = injector.TrySlam(card, "pending-hidden");

                Assert.AreEqual(SlamOutcome.BlockedByPreFiledExemption, applied.Outcome);
                Assert.AreEqual("pre_filed_exemption", received.BlockingModifierId);

                var after = injector.PreviewSlam(card, "pending-hidden");
                Assert.AreEqual(CardSlamOutcome.BlockedByExemption, after.Outcome);
                Assert.IsFalse(after.HasConcealedBlockRisk);
                Assert.IsTrue(after.IsBlockingModifierRevealed);
                Assert.AreEqual("pre_filed_exemption", after.BlockingModifierId);
            }
            finally
            {
                RumorMill.OnCardSlammed -= handler;
                Object.DestroyImmediate(card);
                Object.DestroyImmediate(injectorGo);
                Object.DestroyImmediate(clientGo);
                RumorMill.ClearAllSubscriptions();
            }
        }

        [Test]
        public void PersistedCounterTrait_IsInstalledAndKnownWhenEncounterLoads()
        {
            var clientGo = new GameObject("PersistedCounterTraitClient");
            var client = clientGo.AddComponent<ClientStateMachine>();
            var meta = new MetaProgressData();
            meta.AddCounterTrait("repeat-client", "pre_filed_exemption");
            client.Initialize("repeat-client", "human", 2,
                meta.GetOrCreateProfile("repeat-client").CounterTraitIds);

            try
            {
                var mutation = new MutationEngine();
                mutation.LoadExistingCounterTraits(client, meta);

                Assert.IsTrue(client.TryGetBlockingTrait(
                    nameof(PunchCardType.PendingReview),
                    out string blockerId, out bool isRevealed));
                Assert.AreEqual("pre_filed_exemption", blockerId);
                Assert.IsTrue(isRevealed);
                Assert.AreEqual(
                    ClientStateMachine.InjectionResult.BlockedByCounterTrait,
                    client.PreviewInject(nameof(PunchCardType.PendingReview), out _));
            }
            finally
            {
                Object.DestroyImmediate(clientGo);
            }
        }
    }
}
