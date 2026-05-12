using System.Collections.Generic;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.Archetypes
{
    public sealed class TheMiddleManager : ArchetypeBase
    {
        public override string ArchetypeId => "middle_manager";
        public override string DisplayName => "The Middle Manager";
        public override string AbilityDescription => "Active: Delegate. Discard hand and draw 5 cards. Costs 10 credits. Passive: Starts with a massive but highly fatigued deck.";

        public override int DrawsPerTurn => 4;

        public override List<string> BuildStartingDeckIds() => new()
        {
            "Card_SmallTalk", "Card_SmallTalk", "Card_SmallTalk", "Card_SmallTalk", "Card_SmallTalk",
            "Card_PendingReview", "Card_PendingReview", "Card_PendingReview",
            "Card_Analyse", "Card_Analyse", "Card_Analyse",
            "Card_Gaslight", "Card_Gaslight",
            "Card_Transfer", "Card_Transfer",
            "Card_Apathy", "Card_Apathy"
        };

        public override bool CanUseAbility(ArchetypeContext ctx) => ctx.Credits >= 10;

        public override void UseAbility(ArchetypeContext ctx)
        {
            ctx.SpendCredits?.Invoke(10);
            ctx.Hand.DiscardAll(ctx.Deck);
            ctx.Hand.DrawCards(5, ctx.Deck);
        }
    }
}
