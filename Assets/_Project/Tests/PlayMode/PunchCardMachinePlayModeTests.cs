using System.Collections;
using Desk42.BSM;
using Desk42.Cards;
using Desk42.Core;
using Desk42.RedTape;
using Desk42.OfficeSupplies;
using Desk42.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Desk42.Tests.PlayMode
{
    public sealed class PunchCardMachinePlayModeTests
    {
        [UnityTest]
        public IEnumerator Cascade_PresentsOnlyModifiersThatChangedTheResult()
        {
            var presenterObject = new GameObject("ChangedOnlyCascade");
            var presenter = presenterObject.AddComponent<CascadePresenter>();
            int presented = 0;
            string presentedSource = null;
            presenter.ModifierPresented += (_, step) =>
            {
                presented++;
                presentedSource = step.SourceId;
            };

            var packet = new SynergyResolutionPacket
            {
                CardType = PunchCardType.PendingReview,
                BaseDuration = 10f,
                FinalDuration = 15f,
                BaseCreditCost = 0,
                FinalCreditCost = 0,
                BaseSoulCost = 0f,
                FinalSoulCost = 0f,
                DurationSteps = new System.Collections.Generic.List<ModifierStep>
                {
                    new()
                    {
                        SourceId = "no_op",
                        SourceKind = ModifierSourceKind.Supply,
                        SourceSide = ModifierSourceSide.Office,
                        DisplayName = "No-op supply",
                        PrevValue = 10f,
                        NewValue = 10f,
                        Delta = 0f,
                    },
                    new()
                    {
                        SourceId = "stapler",
                        SourceKind = ModifierSourceKind.Supply,
                        SourceSide = ModifierSourceSide.Office,
                        DisplayName = "Stapler",
                        PrevValue = 10f,
                        NewValue = 15f,
                        Delta = 5f,
                    },
                },
                CreditCostSteps = new System.Collections.Generic.List<ModifierStep>(),
                SoulCostSteps = new System.Collections.Generic.List<ModifierStep>(),
            };

            try
            {
                presenter.PlaySequence(packet);
                float deadline = Time.realtimeSinceStartup + 1.5f;
                while (presented == 0 && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.AreEqual(1, presented,
                    "Balatro-style sequencing should fire contributors, not no-op clutter.");
                Assert.AreEqual("stapler", presentedSource);
            }
            finally
            {
                Object.Destroy(presenterObject);
                RumorMill.ClearAllSubscriptions();
            }
        }

        [UnityTest]
        public IEnumerator ClickAndDrag_UseOneImpactSequenceAndOneResultEach()
        {
            SeedEngine.Init(424242);
            var clientObject = new GameObject("UnifiedSlamClient");
            var client = clientObject.AddComponent<ClientStateMachine>();
            client.Initialize("unified-client", "human", 0,
                new System.Collections.Generic.List<string>());

            var machineObject = new GameObject("UnifiedSlamMachine");
            var machine = machineObject.AddComponent<PunchCardMachine>();
            machine.SetActiveClient(client);
            var feedbackObject = new GameObject("LifecycleSafeCardFeedback");
            var feedback = feedbackObject.AddComponent<Desk42.UI.CardSlamFeedback>();
            yield return null;

            var card = ScriptableObject.CreateInstance<PunchCardData>();
            card.CardType = PunchCardType.PendingReview;
            card.DisplayName = "PENDING REVIEW";
            card.InjectionDuration = 10f;
            card.CreditCost = 0;
            card.JamFatigue = 10;
            card.MaxFatigue = 20;

            int resolvedCount = 0;
            float resolvedAt = 0f;
            machine.OnSlamResolved += _ =>
            {
                resolvedCount++;
                resolvedAt = Time.realtimeSinceStartup;
            };

            try
            {
                float clickStarted = Time.realtimeSinceStartup;
                machine.SlamCard(card, "click-card");
                yield return WaitForResolution(() => resolvedCount == 1);
                float clickImpactDelay = resolvedAt - clickStarted;
                yield return new WaitForSeconds(0.25f);

                var cardObject = new GameObject("DraggedCard");
                var cardView = cardObject.AddComponent<CardView>();
                cardView.Initialize(card, machine);

                float dragStarted = Time.realtimeSinceStartup;
                machine.OnCardDropped(cardView);
                yield return WaitForResolution(() => resolvedCount == 2);
                float dragImpactDelay = resolvedAt - dragStarted;

                Assert.AreEqual(2, resolvedCount,
                    "Each input route must produce exactly one semantic result.");
                Assert.AreEqual(clickImpactDelay, dragImpactDelay, 0.08f,
                    "Click and drag must reach punch impact on the same timing path.");
                machine.ClearActiveClient();
                StringAssert.Contains("APPLIED", feedback.RenderedImpactText,
                    "Post-impact confirmation must survive machine coroutine cleanup.");
                Object.Destroy(cardObject);
            }
            finally
            {
                Object.Destroy(card);
                Object.Destroy(feedbackObject);
                Object.Destroy(machineObject);
                Object.Destroy(clientObject);
                RumorMill.ClearAllSubscriptions();
            }
        }

        private static IEnumerator WaitForResolution(System.Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(predicate(), "Slam did not resolve before the timeout.");
        }
    }
}
