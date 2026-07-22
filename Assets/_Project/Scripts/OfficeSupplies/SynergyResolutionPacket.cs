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
    public enum ModifierSourceKind
    {
        Supply,
        Archetype,
        Vow,
        Faction,
        Regulation,
        Environment,
        ClientState,
        CounterTrait,
    }

    public enum ModifierSourceSide
    {
        Office,
        Client,
    }

    public struct ModifierStep
    {
        /// <summary>
        /// Stable identity shared by projected and applied traces. UI uses this
        /// to light the same visible modifier before and after a slam.
        /// </summary>
        public string   SourceId;
        public ModifierSourceKind SourceKind;
        public ModifierSourceSide SourceSide;

        // Compatibility alias retained for supply-specific analytics/tests.
        public string   SupplyId;
        public string   DisplayName;
        public DeskZone Zone;
        public float    PrevValue;
        public float    NewValue;
        public float    Delta;

        public string SourceKey => $"{SourceKind}:{SourceId}";
        public bool Changed => System.Math.Abs(Delta) > 0.0001f;
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
