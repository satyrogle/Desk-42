// ============================================================
// DESK 42 — IArchetype Interface
//
// The player's chosen role shapes everything:
//   - Starting deck composition
//   - Passive rule modifications
//   - Active ability (spendable resource or cooldown)
//   - Exclusive card pool unlocks
//   - Optional Vow constraints for higher meta-XP
//
// Archetype classes are pure C# (no MonoBehaviour).
// They are held by RunStateController and consulted at:
//   - Run start     : BuildStartingDeck(), ApplyPassives()
//   - Each slam     : OnCardSlammed() — passive triggers
//   - Each tick     : Tick() — cooldown management
//   - Hand size     : MaxHandSize property
//   - Draft         : ArchetypeId for pool affinity
//
// UI-visible info is on the matching ArchetypeData ScriptableObject
// (not here — keep logic and data separate).
// ============================================================

using System.Collections.Generic;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.Archetypes
{
    public interface IArchetype
    {
        // ── Identity ──────────────────────────────────────────

        /// <summary>Stable ID used by meta-progress and draft affinity.</summary>
        string ArchetypeId { get; }

        /// <summary>Display name shown in the UI.</summary>
        string DisplayName { get; }

        // ── Deck ──────────────────────────────────────────────

        /// <summary>
        /// Build the starting hand of cards for a fresh run.
        /// Returns card data asset names — caller resolves to SOs.
        /// </summary>
        List<string> BuildStartingDeckIds();

        /// <summary>Cards-per-turn drawn at shift start.</summary>
        int DrawsPerTurn { get; }

        /// <summary>Starting hand size cap.</summary>
        int MaxHandSize { get; }

        // ── Passives ──────────────────────────────────────────

        /// <summary>
        /// Called once when the run begins.
        /// Apply any persistent modifiers to run data, hand, etc.
        /// </summary>
        void OnRunStart(ArchetypeContext ctx);

        /// <summary>Called after every successful card slam.</summary>
        void OnCardSlammed(PunchCardType cardType, ArchetypeContext ctx);

        /// <summary>Called each game frame (for cooldown tick, meter drain, etc.).</summary>
        void Tick(float deltaTime, ArchetypeContext ctx);

        // ── Active Ability ────────────────────────────────────

        /// <summary>Human-readable description of the active ability.</summary>
        string AbilityDescription { get; }

        /// <summary>Whether the active ability can be used right now.</summary>
        bool CanUseAbility(ArchetypeContext ctx);

        /// <summary>Execute the active ability. Caller should check CanUseAbility first.</summary>
        void UseAbility(ArchetypeContext ctx);

        // ── Vow (optional hard-mode constraint) ──────────────

        /// <summary>The vow the player has accepted (null if none).</summary>
        IArchetypeVow ActiveVow { get; }

        void SetVow(IArchetypeVow vow);

        // ── Modifier Hooks ────────────────────────────────────

        /// <summary>
        /// Override or modify an injection duration before it reaches the BSM.
        /// Return the (possibly unchanged) duration.
        /// </summary>
        float ModifyInjectionDuration(PunchCardType cardType, float baseDuration);

        /// <summary>
        /// Override or modify a card's credit cost.
        /// Return the (possibly unchanged) cost.
        /// </summary>
        int ModifyCreditCost(PunchCardType cardType, int baseCost);
    }

    // ── Vow Interface ─────────────────────────────────────────

    public interface IArchetypeVow
    {
        string VowId          { get; }
        string Description    { get; }

        /// <summary>Returns true if the vow has been broken this run.</summary>
        bool IsBroken         { get; }

        /// <summary>Called on every card slam — check if vow is violated.</summary>
        void EvaluateOnSlam(PunchCardType cardType, ArchetypeContext ctx);

        /// <summary>Called on every organic BSM transition.</summary>
        void EvaluateOnStateTransition(ClientStateID newState, ArchetypeContext ctx);
    }

    // ── Archetype Context ─────────────────────────────────────
    // Snapshot of run state passed to archetype hooks.

    public sealed class ArchetypeContext
    {
        public Deck    Deck;
        public Hand    Hand;
        public float   Sanity;
        public float   SoulIntegrity;
        public float   Credits;
        public int     ShiftNumber;
        public int     TotalCardSlams;
        public string  ActiveClientVariantId;

        // Callbacks back into RunStateController
        public System.Action<float> ModifySanity;
        public System.Action<float> ModifySoulIntegrity;
        public System.Action<int>   AddCredits;
        public System.Action<int>   SpendCredits;
        public System.Action<string> EmitDarkHumour;
    }
}
