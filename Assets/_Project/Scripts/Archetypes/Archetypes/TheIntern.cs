using System.Collections.Generic;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.Archetypes
{
    public sealed class TheIntern : ArchetypeBase
    {
        public override string ArchetypeId => "intern";
        public override string DisplayName => "The Intern";
        public override string AbilityDescription => "Active: Unpaid Coffee Run. Gain +5 sanity and reshuffle discard into deck. Costs 10 credits. Passive: 5 draws per turn (fast), but injection durations are 20% longer (inexperienced).";

        public override int DrawsPerTurn => 5;

        public override List<string> BuildStartingDeckIds() => new()
        {
            "Card_SmallTalk",
            "Card_SmallTalk",
            "Card_SmallTalk",
            "Card_Empathise",
            "Card_Empathise",
            "Card_PendingReview",
            "Card_Analyse",
            "Card_Analyse",
            "Card_Transfer",
            "Card_CooperationRoute",
        };

        public override bool CanUseAbility(ArchetypeContext ctx)
        {
            return ctx.Credits >= 10;
        }

        public override void UseAbility(ArchetypeContext ctx)
        {
            ctx.SpendCredits?.Invoke(10);
            ctx.ModifySanity?.Invoke(5f);
            ctx.Deck.ReshuffleDiscardIntoDraw();
            ctx.Hand.DrawCards(2, ctx.Deck); // Draw 2 extra
            ctx.EmitDarkHumour?.Invoke("intern_coffee");
        }

        public override float ModifyInjectionDuration(PunchCardType cardType, float baseDuration)
        {
            // Inexperienced: things take 20% longer
            return baseDuration * 1.2f;
        }
    }
}
