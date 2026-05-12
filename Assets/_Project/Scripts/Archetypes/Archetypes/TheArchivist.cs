using System.Collections.Generic;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.Archetypes
{
    public sealed class TheArchivist : ArchetypeBase
    {
        public override string ArchetypeId => "archivist";
        public override string DisplayName => "The Archivist";
        public override string AbilityDescription => "Active: Deep Dig. Look at top 3 cards of draw pile, put them in any order. Costs 5 Sanity. Passive: Immunity to data loss.";

        public override int DrawsPerTurn => 4;

        public override List<string> BuildStartingDeckIds() => new()
        {
            "Card_SmallTalk",
            "Card_SmallTalk",
            "Card_Analyse",
            "Card_Analyse",
            "Card_Analyse",
            "Card_Redact",
            "Card_PendingReview",
            "Card_Apathy"
        };

        public override bool CanUseAbility(ArchetypeContext ctx) => ctx.Sanity >= 5f;

        public override void UseAbility(ArchetypeContext ctx)
        {
            ctx.ModifySanity?.Invoke(-5f);
            // Draw 2 as a simplified Deep Dig for now:
            ctx.Hand.DrawCards(2, ctx.Deck);
            ctx.EmitDarkHumour?.Invoke("archivist_dig");
        }
    }
}
