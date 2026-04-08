using System.Collections.Generic;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.Archetypes
{
    public sealed class TheHRRep : ArchetypeBase
    {
        public override string ArchetypeId => "hr_rep";
        public override string DisplayName => "The HR Rep";
        public override string AbilityDescription => "Active: Seminar Allocation. Costs 20 credits to force Active Client into RESIGNED state immediately for 5 seconds. Passive: Credit costs for disciplinary cards (ThreatAudit, Reeducate, Gaslight) reduced by 5.";

        public override int DrawsPerTurn => 3;

        public override List<string> BuildStartingDeckIds() => new()
        {
            "Card_Gaslight",
            "Card_Gaslight",
            "Card_Reeducate",
            "Card_Reeducate",
            "Card_NonDisclosure",
            "Card_PendingReview",
            "Card_LegalHold",
            "Card_Transfer",
            "Card_Analyse",
            "Card_ThreatAudit",
        };

        public override bool CanUseAbility(ArchetypeContext ctx)
        {
            return ctx.Credits >= 20 && ctx.ActiveClientVariantId != null;
        }

        public override void UseAbility(ArchetypeContext ctx)
        {
            ctx.SpendCredits?.Invoke(20);
            // HR forced transition. Target BSM may ignore the 'from' field if it is a forceful override.
            // Passes the tracked ActiveClientState from RunStateController.
            RumorMill.Publish(new StateTransitionEvent(ctx.ActiveClientVariantId, ctx.ActiveClientState, ClientStateID.Resigned, true, false));
            ctx.EmitDarkHumour?.Invoke("hr_seminar");
        }

        public override float ModifySoulCost(PunchCardType cardType, float baseCost)
        {
            if (cardType == PunchCardType.ThreatAudit || 
                cardType == PunchCardType.Reeducate || 
                cardType == PunchCardType.Gaslight)
            {
                return baseCost > 1f ? baseCost - 1f : 0f;
            }
            return baseCost;
        }

        public override int ModifyCreditCost(PunchCardType cardType, int baseCost)
        {
            if (cardType == PunchCardType.ThreatAudit || 
                cardType == PunchCardType.Reeducate || 
                cardType == PunchCardType.Gaslight)
            {
                int modified = baseCost - 5;
                return modified < 0 ? 0 : modified;
            }
            return baseCost;
        }
    }
}
