// ============================================================
// DESK 42 — Synergy Resolution Packet
//
// Per-step trace of a SynergyResolver modifier chain, for
// presentation (CascadePresenter) and test fixtures. One
// ModifierStep per active office supply instance that ran
// during a chain, in the same zone order the resolver applies.
// ============================================================

using System.Collections.Generic;
using Desk42.Core;

namespace Desk42.OfficeSupplies
{
    public struct ModifierStep
    {
        public string   SupplyId;
        public string   DisplayName;
        public DeskZone Zone;
        public float    PrevValue;
        public float    NewValue;
        public float    Delta;
    }

    public struct SynergyResolutionPacket
    {
        public PunchCardType      CardType;

        public float              BaseDuration;
        public float              FinalDuration;
        public List<ModifierStep> DurationSteps;

        public int                BaseCreditCost;
        public int                FinalCreditCost;
        public List<ModifierStep> CreditCostSteps;

        public float              BaseSoulCost;
        public float              FinalSoulCost;
        public List<ModifierStep> SoulCostSteps;
    }
}
