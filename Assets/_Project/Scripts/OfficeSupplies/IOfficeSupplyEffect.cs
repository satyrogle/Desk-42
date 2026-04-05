// ============================================================
// DESK 42 — Office Supply Effect Interface + Context
//
// Each office supply has one IOfficeSupplyEffect implementation.
// The effect is pure C# — no MonoBehaviour, no Unity lifecycle.
// OfficeSupplyManager calls the appropriate methods based on
// the supply's SupplyTrigger.
//
// SupplyContext is assembled by OfficeSupplyManager each time
// an effect is triggered and carries enough state to implement
// any planned supply without needing direct GameManager access.
// ============================================================

using System.Collections.Generic;
using Desk42.Cards;
using Desk42.Core;
using Desk42.RedTape;

namespace Desk42.OfficeSupplies
{
    // ── Supply Context ─────────────────────────────────────────
    // Snapshot + callbacks passed to each supply on trigger.

    public sealed class SupplyContext
    {
        // ── Trigger info ──────────────────────────────────────
        public PunchCardType?  LastCardType;          // set on OnCardSlammed
        public string          LastCardInstanceId;
        public ClientStateID?  NewState;              // set on OnStateTransition
        public ClientStateID?  PrevState;
        public bool            ClaimWasHumane;        // set on OnClaimResolved
        public OfficeHazardType? HazardType;          // set on OnHazard

        // ── Run state (read) ──────────────────────────────────
        public float  Sanity;
        public float  SoulIntegrity;
        public int    Credits;
        public int    ShiftNumber;
        public int    TotalCardSlams;
        public float  DeskEntropy;
        public int    TriggerCount;         // how many times THIS supply has triggered

        // ── Card system (read/write via callbacks) ────────────
        public Deck   Deck;
        public Hand   Hand;
        public CardFatigueTracker Fatigue;

        // ── Callbacks into RunStateController ─────────────────
        public System.Action<float>  ModifySanity;
        public System.Action<float>  ModifySoulIntegrity;
        public System.Action<int>    AddCredits;
        public System.Action<float>  ExtendTimer;
        public System.Action<string> EmitDarkHumour;
        public System.Action<string> DrawSpecificCard;  // card asset name → add to hand
        public System.Action         DrawOneCard;
        public System.Func<PunchCardType, float, float> ModifyNextDuration;

        // ── Modifier output (for SynergyResolver chain) ───────
        // Supplies that modify duration/cost set these; SynergyResolver reads them.
        public float  DurationMultiplier  = 1f;
        public float  DurationFlatBonus   = 0f;
        public int    CreditCostDelta     = 0;
        public bool   NextCardIsFree      = false;
        public int    BonusDrawCount      = 0;
    }

    // ── Supply Effect Interface ────────────────────────────────

    public interface IOfficeSupplyEffect
    {
        string SupplyId { get; }

        /// <summary>
        /// Called once when the supply is placed on the desk.
        /// Use for one-time setup (e.g. increasing max hand size).
        /// </summary>
        void OnPlace(SupplyContext ctx);

        /// <summary>
        /// Called once when the supply is removed (sold, consumed, or shift ends).
        /// Undo any permanent changes applied in OnPlace.
        /// </summary>
        void OnRemove(SupplyContext ctx);

        /// <summary>
        /// Called every frame for Passive supplies.
        /// All other supplies get called from event handlers, not Tick.
        /// </summary>
        void Tick(float dt, SupplyContext ctx);

        /// <summary>
        /// Called by OfficeSupplyManager after a successful card slam.
        /// </summary>
        void OnCardSlammed(SupplyContext ctx);

        /// <summary>
        /// Called by OfficeSupplyManager on any BSM state transition.
        /// </summary>
        void OnStateTransition(SupplyContext ctx);

        /// <summary>
        /// Called by OfficeSupplyManager when a claim is resolved.
        /// </summary>
        void OnClaimResolved(SupplyContext ctx);

        /// <summary>
        /// Called by OfficeSupplyManager when an office hazard fires.
        /// </summary>
        void OnHazard(SupplyContext ctx);

        /// <summary>
        /// Called by OfficeSupplyManager when a new client encounter starts.
        /// </summary>
        void OnEncounterStart(SupplyContext ctx);

        /// <summary>
        /// Called by OfficeSupplyManager when a client encounter ends.
        /// </summary>
        void OnEncounterEnd(SupplyContext ctx);

        /// <summary>
        /// Called by OfficeSupplyManager at the start of each shift.
        /// </summary>
        void OnShiftStart(SupplyContext ctx);

        /// <summary>
        /// Called by SynergyResolver to apply duration modifiers.
        /// Return the modified duration.
        /// </summary>
        float ModifyInjectionDuration(PunchCardType cardType, float currentDuration,
            IReadOnlyList<string> cardTags);

        /// <summary>
        /// Called by SynergyResolver to apply credit cost modifiers.
        /// Return the modified cost (clamped ≥0 by caller).
        /// </summary>
        int ModifyCreditCost(PunchCardType cardType, int currentCost);

        /// <summary>
        /// Called by SynergyResolver to apply soul cost modifiers.
        /// Return the modified soul cost (clamped ≥0 by caller).
        /// Paper Weight reduces all soul costs by 1; future supplies may also hook here.
        /// </summary>
        float ModifySoulCost(float currentCost);
    }

    // ── Supply Effect Base ─────────────────────────────────────
    // Default no-op implementations so concrete supplies only
    // override the methods they care about.

    public abstract class OfficeSupplyEffectBase : IOfficeSupplyEffect
    {
        public abstract string SupplyId { get; }

        public virtual void OnPlace(SupplyContext ctx)            { }
        public virtual void OnRemove(SupplyContext ctx)           { }
        public virtual void Tick(float dt, SupplyContext ctx)     { }
        public virtual void OnCardSlammed(SupplyContext ctx)      { }
        public virtual void OnStateTransition(SupplyContext ctx)  { }
        public virtual void OnClaimResolved(SupplyContext ctx)    { }
        public virtual void OnHazard(SupplyContext ctx)           { }
        public virtual void OnEncounterStart(SupplyContext ctx)   { }
        public virtual void OnEncounterEnd(SupplyContext ctx)     { }
        public virtual void OnShiftStart(SupplyContext ctx)       { }

        public virtual float ModifyInjectionDuration(
            PunchCardType cardType, float duration, IReadOnlyList<string> tags)
            => duration;

        public virtual int ModifyCreditCost(PunchCardType cardType, int cost)
            => cost;

        public virtual float ModifySoulCost(float cost) => cost;
    }
}
