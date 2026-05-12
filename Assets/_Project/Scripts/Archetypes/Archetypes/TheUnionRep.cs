using System.Collections.Generic;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.Archetypes
{
    public sealed class TheUnionRep : ArchetypeBase
    {
        public override string ArchetypeId => "union_rep";
        public override string DisplayName => "The Union Rep";
        public override string AbilityDescription => "Active: Grievance. Gain +15 Sanity but decrease faction rep with Management. Passive: Strong solidarity cards.";

        public override int DrawsPerTurn => 4;

        public override List<string> BuildStartingDeckIds() => new()
        {
            "Card_SmallTalk",
            "Card_SmallTalk",
            "Card_Empathise",
            "Card_Empathise",
            "Card_CooperationRoute",
            "Card_CooperationRoute",
            "Card_Analyse",
            "Card_PendingReview"
        };

        public override bool CanUseAbility(ArchetypeContext ctx) => true;

        public override void UseAbility(ArchetypeContext ctx)
        {
            ctx.ModifySanity?.Invoke(15f);
            ctx.EmitDarkHumour?.Invoke("union_rep_grievance");
        }
    }
}
