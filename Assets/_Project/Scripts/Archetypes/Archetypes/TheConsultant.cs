using System.Collections.Generic;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.Archetypes
{
    public sealed class TheConsultant : ArchetypeBase
    {
        public override string ArchetypeId => "consultant";
        public override string DisplayName => "The Consultant";
        public override string AbilityDescription => "Active: Synergy. Reduce impatience timer by 10s. Costs 25 credits. Passive: High starting credits but 0 base sanity.";

        public override int DrawsPerTurn => 4;

        public override List<string> BuildStartingDeckIds() => new()
        {
            "Card_SmallTalk",
            "Card_SmallTalk",
            "Card_SmallTalk",
            "Card_Analyse",
            "Card_Analyse",
            "Card_Gaslight",
            "Card_Apathy",
            "Card_Empathise",
            "Card_Redact"
        };

        public override void OnRunStart(ArchetypeContext ctx)
        {
            ctx.AddCredits?.Invoke(50);
            ctx.ModifySanity?.Invoke(-50f); // Starts stressed
        }

        public override bool CanUseAbility(ArchetypeContext ctx) => ctx.Credits >= 25;

        public override void UseAbility(ArchetypeContext ctx)
        {
            ctx.SpendCredits?.Invoke(25);
            ctx.ExtendTimer?.Invoke(10f);
        }
    }
}
