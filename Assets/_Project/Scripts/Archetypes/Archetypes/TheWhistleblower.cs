using System.Collections.Generic;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.Archetypes
{
    public sealed class TheWhistleblower : ArchetypeBase
    {
        public override string ArchetypeId => "whistleblower";
        public override string DisplayName => "The Whistleblower";
        public override string AbilityDescription => "Active: Leak Documents. Re-draws full hand but costs 15 credits. Passive: Immune to soul drain on standard actions, but flat +2 credit cost penalty on everything.";

        public override int DrawsPerTurn => 4;

        public override List<string> BuildStartingDeckIds() => new()
        {
            "Card_Analyse",
            "Card_Analyse",
            "Card_PendingReview",
            "Card_PendingReview",
            "Card_Redact",
            "Card_NonDisclosure",
            "Card_Gaslight",
            "Card_CooperationRoute",
            "Card_Transfer",
            "Card_Reeducate",
        };

        public override bool CanUseAbility(ArchetypeContext ctx)
        {
            return ctx.Credits >= 15;
        }

        public override void UseAbility(ArchetypeContext ctx)
        {
            ctx.SpendCredits?.Invoke(15);
            ctx.Hand.DiscardAll(ctx.Deck);
            ctx.Hand.DrawCards(MaxHandSize, ctx.Deck);
            ctx.EmitDarkHumour?.Invoke("whistleblower_leak");

            // Track leaked document toward the Whistleblower ending
            GameManager.Instance?.Milestones?.RecordLeakedDocument();
        }

        public override int ModifyCreditCost(PunchCardType cardType, int baseCost)
        {
            return baseCost + 2; // Flat 2 credit tax due to scrutiny
        }

        public override float ModifySoulCost(PunchCardType cardType, float baseCost)
        {
            // Low soul drain passive - whistleblower feels justified
            float modified = baseCost - 5f;
            return modified < 0f ? 0f : modified;
        }
    }
}
