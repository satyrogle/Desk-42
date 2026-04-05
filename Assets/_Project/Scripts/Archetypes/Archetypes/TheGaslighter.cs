// ============================================================
// DESK 42 — The Gaslighter
//
// "I never said that. The form clearly states otherwise."
//
// Playstyle: Resource manipulation and psychological pressure.
// The Gaslighter exploits the client's confusion window to stack
// multiple injections before they can react. High risk/reward —
// each Gaslighter card stacks "Distortion" onto the client,
// and clients occasionally adapt to Distortion (mutation risk
// is higher for this archetype).
//
// ── Passive: Distortion Stack ────────────────────────────
//   Each slam adds 1 Distortion to the active client.
//   At 3 Distortion: the client's current BT is paused for
//   an extra 2s and cannot transition organically (window
//   for stacking a second injection).
//   At 6 Distortion: client enters DISSOCIATING (rare — use
//   with caution; soul cost is high).
//
// ── Passive: Gaslight Window ─────────────────────────────
//   When a client's mood changes (any transition), gain a
//   1-second "echo" window — the next slam within that window
//   costs 0 credits.
//
// ── Active Ability: Plausible Deniability ────────────────
//   Cooldown: 45 seconds (real time, not encounter time)
//   Effect: Clear all Distortion from the current client.
//           Extend current injected state duration by 3s.
//           Plays deniability_vo audio cue.
//
// ── Starting Deck (10 cards) ─────────────────────────────
//   3× ThreatAudit
//   2× Redact
//   2× CooperationRoute
//   1× PendingReview
//   1× Analyse
//   1× Forget
//
// ── Vow Option: "Plausibly Deniable" ─────────────────────
//   At 6 Distortion, you MUST use Plausible Deniability.
//   If the client enters DISSOCIATING through your actions,
//   the Vow breaks.
//   Reward: +25% meta-XP per run.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.Archetypes
{
    public sealed class TheGaslighter : ArchetypeBase
    {
        // ── Identity ──────────────────────────────────────────

        public override string ArchetypeId => "gaslighter";
        public override string DisplayName => "The Gaslighter";

        // ── Config ────────────────────────────────────────────

        private const int   DISTORTION_PAUSE_THRESHOLD = 3;
        private const int   DISTORTION_DISSOC_THRESHOLD = 6;
        private const float ECHO_WINDOW_DURATION       = 1.0f;   // seconds
        private const float ABILITY_COOLDOWN           = 45f;    // seconds
        private const float ABILITY_DURATION_EXTENSION = 3f;     // seconds

        // ── State ─────────────────────────────────────────────

        private int   _distortionStack;
        private float _echoWindowRemaining;
        private float _abilityCooldownRemaining;

        // ── Deck ──────────────────────────────────────────────

        public override List<string> BuildStartingDeckIds() => new()
        {
            "Card_ThreatAudit",
            "Card_ThreatAudit",
            "Card_ThreatAudit",
            "Card_Redact",
            "Card_Redact",
            "Card_CooperationRoute",
            "Card_CooperationRoute",
            "Card_PendingReview",
            "Card_Analyse",
            "Card_Forget",
        };

        public override int DrawsPerTurn => 4; // Gaslighter draws faster

        // ── Passive: Run start ────────────────────────────────

        public override void OnRunStart(ArchetypeContext ctx)
        {
            _distortionStack          = 0;
            _echoWindowRemaining      = 0f;
            _abilityCooldownRemaining = 0f;
        }

        // ── Passive: OnCardSlammed ────────────────────────────

        public override void OnCardSlammed(PunchCardType cardType, ArchetypeContext ctx)
        {
            _distortionStack++;

            if (_distortionStack == DISTORTION_PAUSE_THRESHOLD)
            {
                Debug.Log("[Gaslighter] Distortion threshold reached — extended injection window.");
                // Notify RunStateController / ClientStateMachine to pause organic BT for 2s
                RumorMill.PublishDeferred(new MilestoneReachedEvent(
                    MilestoneID.DistortionThreshold, ctx.ShiftNumber, "distortion_pause"));
            }
            else if (_distortionStack >= DISTORTION_DISSOC_THRESHOLD)
            {
                Debug.Log("[Gaslighter] Distortion peaked — triggering DISSOCIATING check.");
                RumorMill.PublishDeferred(new MilestoneReachedEvent(
                    MilestoneID.DistortionThreshold, ctx.ShiftNumber, "distortion_dissoc"));

                // Check vow
                ActiveVow?.EvaluateOnStateTransition(ClientStateID.Dissociating, ctx);
            }
        }

        /// <summary>Called by RunStateController on any organic BSM transition.</summary>
        public void OnClientTransition(ClientStateID newState, ArchetypeContext ctx)
        {
            // Open the echo window
            _echoWindowRemaining = ECHO_WINDOW_DURATION;
            Debug.Log($"[Gaslighter] Echo window opened: {ECHO_WINDOW_DURATION}s.");
            ActiveVow?.EvaluateOnStateTransition(newState, ctx);
        }

        public void OnNewEncounter()
        {
            _distortionStack     = 0;
            _echoWindowRemaining = 0f;
        }

        // ── Passive: Tick ─────────────────────────────────────

        public override void Tick(float dt, ArchetypeContext ctx)
        {
            if (_echoWindowRemaining > 0f)
                _echoWindowRemaining = System.Math.Max(0f, _echoWindowRemaining - dt);

            if (_abilityCooldownRemaining > 0f)
                _abilityCooldownRemaining = System.Math.Max(0f, _abilityCooldownRemaining - dt);
        }

        // ── Active: Plausible Deniability ─────────────────────

        public override string AbilityDescription =>
            _abilityCooldownRemaining > 0f
                ? $"Plausible Deniability — on cooldown ({_abilityCooldownRemaining:F0}s)"
                : "Plausible Deniability — clear Distortion, extend injection +3s";

        public override bool CanUseAbility(ArchetypeContext ctx)
            => _abilityCooldownRemaining <= 0f;

        public override void UseAbility(ArchetypeContext ctx)
        {
            if (!CanUseAbility(ctx)) return;

            int cleared  = _distortionStack;
            _distortionStack          = 0;
            _abilityCooldownRemaining = ABILITY_COOLDOWN;

            ctx.EmitDarkHumour?.Invoke("plausible_deniability");

            RumorMill.Publish(new MilestoneReachedEvent(
                MilestoneID.PlausibleDeniabilityUsed,
                ctx.ShiftNumber,
                $"cleared_{cleared}_distortion"));

            Debug.Log($"[Gaslighter] Plausible Deniability: cleared {cleared} distortion.");
        }

        // ── Modifier: echo window → free card ─────────────────

        public override int ModifyCreditCost(PunchCardType cardType, int baseCost)
        {
            if (_echoWindowRemaining > 0f && baseCost > 0)
            {
                _echoWindowRemaining = 0f; // consume the window
                Debug.Log("[Gaslighter] Echo window consumed — card is free.");
                return 0;
            }
            return baseCost;
        }

        // ── Vow: Plausibly Deniable ───────────────────────────

        public sealed class PlausiblyDeniableVow : IArchetypeVow
        {
            private readonly TheGaslighter _owner;
            public PlausiblyDeniableVow(TheGaslighter owner) => _owner = owner;

            public string VowId       => "plausibly_deniable";
            public string Description => "At max Distortion, must use Plausible Deniability. " +
                                         "Vow breaks if client enters DISSOCIATING.";
            public bool   IsBroken    { get; private set; }

            public void EvaluateOnSlam(PunchCardType cardType, ArchetypeContext ctx) { }

            public void EvaluateOnStateTransition(ClientStateID newState, ArchetypeContext ctx)
            {
                if (newState == ClientStateID.Dissociating)
                {
                    IsBroken = true;
                    Debug.LogWarning("[Vow] Plausibly Deniable BROKEN — client dissociated.");
                }
            }
        }
    }
}
