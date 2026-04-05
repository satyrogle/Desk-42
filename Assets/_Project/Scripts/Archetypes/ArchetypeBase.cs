// ============================================================
// DESK 42 — Archetype Base
//
// Default implementations for the boilerplate parts of
// IArchetype so concrete archetypes only override what they
// change. All modifier hooks default to pass-through.
// ============================================================

using System.Collections.Generic;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.Archetypes
{
    public abstract class ArchetypeBase : IArchetype
    {
        // ── Identity ──────────────────────────────────────────

        public abstract string ArchetypeId  { get; }
        public abstract string DisplayName  { get; }

        // ── Deck Defaults ─────────────────────────────────────

        public abstract List<string> BuildStartingDeckIds();
        public virtual int DrawsPerTurn => 3;
        public virtual int MaxHandSize  => 5;

        // ── Lifecycle Defaults ────────────────────────────────

        public virtual void OnRunStart(ArchetypeContext ctx)    { }
        public virtual void OnCardSlammed(PunchCardType t, ArchetypeContext ctx) { }
        public virtual void Tick(float dt, ArchetypeContext ctx) { }

        // ── Active Ability Defaults ───────────────────────────

        public abstract string AbilityDescription { get; }
        public abstract bool   CanUseAbility(ArchetypeContext ctx);
        public abstract void   UseAbility(ArchetypeContext ctx);

        // ── Vow ───────────────────────────────────────────────

        public IArchetypeVow ActiveVow { get; private set; }

        public void SetVow(IArchetypeVow vow) => ActiveVow = vow;

        // ── Modifier Pass-throughs ────────────────────────────

        public virtual float ModifyInjectionDuration(PunchCardType t, float dur)  => dur;
        public virtual int   ModifyCreditCost(PunchCardType t, int cost)          => cost;
    }
}
