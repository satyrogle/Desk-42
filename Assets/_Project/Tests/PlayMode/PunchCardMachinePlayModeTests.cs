using System.Collections;
using Desk42.BSM;
using Desk42.Cards;
using Desk42.Core;
using Desk42.RedTape;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Desk42.Tests.PlayMode
{
    public sealed class PunchCardMachinePlayModeTests
    {
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
                Object.Destroy(cardObject);
            }
            finally
            {
                Object.Destroy(card);
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
