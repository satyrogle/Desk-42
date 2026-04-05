// ============================================================
// DESK 42 — Client Context
//
// The full world-state snapshot passed to every BSM state Tick,
// BT node Tick, and TransitionRule Evaluate.
//
// Assembled fresh each frame by ClientStateMachine before
// passing to the active state. Read-only from the state's
// perspective — mutations go through the public API on
// ClientStateMachine or RunStateController.
// ============================================================

using System.Collections.Generic;
using Desk42.Core;

namespace Desk42.BSM
{
    public sealed class ClientContext
    {
        // ── Client Identity ───────────────────────────────────
        public string         ClientVariantId;
        public string         ClientSpeciesId;
        public int            VisitCount;          // 0 = first visit, 1+ = repeat offender
        public List<string>   ActiveCounterTraits; // SO GUIDs from RepeatOffenderDB

        // ── Current BSM State ─────────────────────────────────
        public ClientStateID  CurrentMoodState;

        // ── Office Environment ────────────────────────────────
        public float          OfficeSanity;          // player's current sanity 0-100
        public float          SoulIntegrity;         // player's soul 0-100
        public float          ImpatienceTimerRatio;  // 0=full time, 1=expired
        public string         OfficeTemperatureState; // "PEAK_EFFICIENCY", "LUNCH_BREAK", "SYSTEM_CRASH"

        // ── Faction ───────────────────────────────────────────
        public float          FilingReputation;
        public float          LegalReputation;
        public float          OccultReputation;
        public float          AccountingReputation;
        public float          ManagementReputation;

        // ── Last Player Action ────────────────────────────────
        // The card type that was just slammed, or "" if none this tick.
        public string         LastCardTypeSlammed;
        // Card type currently being injected (set by ClientStateMachine.TryInject)
        public string         TriggerAction;

        // ── Claim State ───────────────────────────────────────
        public float          ClaimCorruption;      // 0-1
        public int            NDACountThisShift;
        public bool           HiddenTraitRevealed;

        // ── Behaviour Tree ────────────────────────────────────
        // Reference back to the client's BT so states can check
        // for blockers without needing a machine reference.
        public BehaviourTrees.BehaviourTree BaseBT;

        // ── Callbacks back to ClientStateMachine ─────────────
        // States call these to request transitions rather than
        // mutating state directly.

        public System.Action<ClientStateID>    RequestTransition;
        public System.Action                   RequestIdle;
        public System.Action<string, float>    TriggerTell;       // (tellType, intensity)
        public System.Action<string>           EmitDarkHumour;    // (humourKey)
    }
}
