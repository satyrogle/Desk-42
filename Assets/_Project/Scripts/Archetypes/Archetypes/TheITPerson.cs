// ============================================================
// DESK 42 — The IT Person
//
// "Have you tried turning it off and on again?"
//
// Playstyle: System manipulation and recovery. The IT Person
// treats the office as a machine to exploit — they can reset
// card fatigue, manipulate the jam timers, and intercept
// mutations before they lock in. Lowest initial damage output,
// highest durability and consistency.
//
// ── Passive: Debug Mode ───────────────────────────────────
//   Always starts each encounter with full knowledge of the
//   client's hidden trait (trait is pre-revealed, no Analyse
//   needed). Balancing cost: slightly increased mutation
//   chance (client "learns faster" with a prepared opponent).
//
// ── Passive: Diagnostic Read ─────────────────────────────
//   When a card jams (hits JamFatigue), instead of waiting
//   for the timer, the IT Person can instantly clear the jam
//   by paying 2 credits. Triggers automatically if credits
//   are available (toggle-able from settings).
//
// ── Active Ability: Hard Reset ────────────────────────────
//   Cooldown: once per encounter
//   Effect: Clear ALL jam timers on ALL cards in hand.
//           Reset the current client's BT to its initial
//           state (clears running state, re-evaluates from root).
//           Cost: 0 credits, 5 soul integrity.
//
// ── Starting Deck (10 cards) ─────────────────────────────
//   3× Analyse
//   2× Expedite
//   2× CooperationRoute
//   2× Forget
//   1× Redact
//
// ── Vow Option: "Factory Settings" ────────────────────────
//   Cannot use ThreatAudit or LegalHold.
//   The Hard Reset ability also refunds 5 soul integrity
//   (instead of costing 5). High discipline required.
//   Reward: +20% meta-XP per run.
//
// ── Note on IT Person + Archetype Synergy ────────────────
//   Debug tokens earned by TheAuditor can be passed to the
//   IT Person (future cross-archetype mechanic). The IT Person
//   uses those tokens to immediately clear ONE counter-trait
//   from the client's BT. This is the "Debug Token Transfer"
//   system planned for Tier 3 expansion.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Cards;
using Desk42.Core;
using Desk42.RedTape;

namespace Desk42.Archetypes
{
    public sealed class TheITPerson : ArchetypeBase
    {
        // ── Identity ──────────────────────────────────────────

        public override string ArchetypeId => "it_person";
        public override string DisplayName => "The IT Person";

        // ── Config ────────────────────────────────────────────

        private const int   DIAGNOSTIC_CLEAR_COST  = 2;  // credits to auto-clear a jam
        private const float HARD_RESET_SOUL_COST    = 5f;
        private const float HARD_RESET_SOUL_REFUND  = 5f; // when vow active

        // ── State ─────────────────────────────────────────────

        private bool _hardResetAvailable;
        private bool _autoClearJamsEnabled = true;

        // ── Deck ──────────────────────────────────────────────

        public override List<string> BuildStartingDeckIds() => new()
        {
            "Card_Analyse",
            "Card_Analyse",
            "Card_Analyse",
            "Card_Expedite",
            "Card_Expedite",
            "Card_CooperationRoute",
            "Card_CooperationRoute",
            "Card_Forget",
            "Card_Forget",
            "Card_Redact",
        };

        public override int DrawsPerTurn => 3;

        // ── Passive: Run Start ────────────────────────────────

        public override void OnRunStart(ArchetypeContext ctx)
        {
            _hardResetAvailable = true;
        }

        public void OnNewEncounter()
        {
            _hardResetAvailable = true;
        }

        // ── Passive: Debug Mode ───────────────────────────────

        /// <summary>
        /// Called by RunStateController when initialising the active claim.
        /// Returns true to signal the claim's hidden trait should be pre-revealed.
        /// </summary>
        public bool ShouldPreRevealHiddenTrait() => true;

        // ── Passive: Diagnostic Read ──────────────────────────

        /// <summary>
        /// Called by CardFatigueTracker (via RunStateController) whenever a card jams.
        /// Returns the credit cost to auto-clear, or -1 if auto-clear is disabled/unaffordable.
        /// </summary>
        public int TryAutoClearJam(string cardInstanceId,
            CardFatigueTracker fatigue, ArchetypeContext ctx)
        {
            if (!_autoClearJamsEnabled) return -1;
            if (ctx.Credits < DIAGNOSTIC_CLEAR_COST) return -1;

            fatigue.ResetCardFatigue(cardInstanceId);
            Debug.Log($"[ITPerson] Diagnostic Read: auto-cleared jam on {cardInstanceId} " +
                      $"(cost: {DIAGNOSTIC_CLEAR_COST} credits).");
            return DIAGNOSTIC_CLEAR_COST;
        }

        public void SetAutoClearJams(bool enabled) => _autoClearJamsEnabled = enabled;

        // ── Active: Hard Reset ────────────────────────────────

        public override string AbilityDescription =>
            _hardResetAvailable
                ? $"Hard Reset — clear all jams, reset client BT. Cost: {HARD_RESET_SOUL_COST} soul."
                : "Hard Reset — used this encounter.";

        public override bool CanUseAbility(ArchetypeContext ctx)
            => _hardResetAvailable;

        public override void UseAbility(ArchetypeContext ctx)
        {
            if (!CanUseAbility(ctx)) return;

            _hardResetAvailable = false;

            bool vowActive = ActiveVow is FactorySettingsVow;
            if (vowActive)
                ctx.ModifySoulIntegrity?.Invoke(+HARD_RESET_SOUL_REFUND);
            else
                ctx.ModifySoulIntegrity?.Invoke(-HARD_RESET_SOUL_COST);

            ctx.EmitDarkHumour?.Invoke("hard_reset_activated");

            RumorMill.PublishDeferred(new MilestoneReachedEvent(
                MilestoneID.HardResetUsed,
                ctx.ShiftNumber,
                "it_hard_reset"));

            Debug.Log($"[ITPerson] Hard Reset activated. " +
                      $"Soul delta: {(vowActive ? "+" : "-")}{HARD_RESET_SOUL_COST}.");
        }

        // ── Modifier: Expedite duration boost ─────────────────

        public override float ModifyInjectionDuration(PunchCardType cardType, float dur)
        {
            // IT Person makes EXPEDITE last 20% longer (they know the system)
            if (cardType == PunchCardType.Expedite)
                return dur * 1.2f;
            return dur;
        }

        // ── Vow: Factory Settings ─────────────────────────────

        public sealed class FactorySettingsVow : IArchetypeVow
        {
            public string VowId       => "factory_settings";
            public string Description => "Cannot play ThreatAudit or LegalHold. " +
                                         "Hard Reset refunds soul instead of costing it.";
            public bool   IsBroken    { get; private set; }

            public void EvaluateOnSlam(PunchCardType cardType, ArchetypeContext ctx)
            {
                if (cardType == PunchCardType.ThreatAudit ||
                    cardType == PunchCardType.LegalHold)
                {
                    IsBroken = true;
                    Debug.LogWarning("[Vow] Factory Settings BROKEN — illegal card used.");
                }
            }

            public void EvaluateOnStateTransition(ClientStateID state, ArchetypeContext ctx) { }
        }
    }
}
