// ============================================================
// DESK 42 — The Auditor
//
// "Every form has a weakness. I find it."
//
// Playstyle: High-information, low-resource pressure.
// The Auditor profits from client mistakes and anomalies.
// The longer the encounter runs, the more advantage they
// accumulate — but they need time, which the impatience timer
// threatens to cut short.
//
// ── Passive: Anomaly Dividend ─────────────────────────────
//   Every time a hidden trait is revealed on a claim, gain
//   +3 credits. Scales with ClaimCorruption level.
//
// ── Passive: Efficiency Meter ────────────────────────────
//   Tracks successful consecutive slams without a failure.
//   At 4 consecutive successes, the next ANALYSE card is free.
//
// ── Active Ability: Deep Audit ────────────────────────────
//   Cost: 1 Debug Token (earned passively, see below)
//   Effect: Force-reveal the hidden trait on the active claim.
//            Also adds +1 to impatience timer (buys time).
//   Cooldown: 1 per encounter.
//
// ── Debug Tokens ─────────────────────────────────────────
//   Earned: 1 token per shift start + 1 per hidden trait found.
//   Spent:  Deep Audit, or hand to IT Person archetype ally
//           (future cross-archetype mechanic).
//
// ── Starting Deck (10 cards) ─────────────────────────────
//   3× Analyse
//   2× PendingReview
//   2× Redact
//   1× ThreatAudit
//   1× LegalHold
//   1× CooperationRoute
//
// ── Vow Option: "By The Book" ────────────────────────────
//   Cannot use ThreatAudit or LegalHold cards.
//   Reward: +20% meta-XP per run.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.Archetypes
{
    public sealed class TheAuditor : ArchetypeBase
    {
        // ── Identity ──────────────────────────────────────────

        public override string ArchetypeId => "auditor";
        public override string DisplayName => "The Auditor";

        // ── Config ────────────────────────────────────────────

        private const int   EFFICIENCY_STREAK_TARGET  = 4;
        private const float ANOMALY_DIVIDEND_PER_TRAIT = 3f;
        private const int   TOKENS_PER_SHIFT_START    = 1;

        // ── State ─────────────────────────────────────────────

        private int  _debugTokens;
        private int  _consecutiveSuccessStreak;
        private bool _nextAnalyseIsFree;
        private bool _deepAuditUsedThisEncounter;

        // ── Deck ──────────────────────────────────────────────

        public override List<string> BuildStartingDeckIds() => new()
        {
            "Card_Analyse",
            "Card_Analyse",
            "Card_Analyse",
            "Card_PendingReview",
            "Card_PendingReview",
            "Card_Redact",
            "Card_Redact",
            "Card_ThreatAudit",
            "Card_LegalHold",
            "Card_CooperationRoute",
        };

        public override int DrawsPerTurn => 3;

        // ── Passive: Run Start ────────────────────────────────

        public override void OnRunStart(ArchetypeContext ctx)
        {
            _debugTokens = TOKENS_PER_SHIFT_START;
            _consecutiveSuccessStreak = 0;
            _nextAnalyseIsFree        = false;
            _deepAuditUsedThisEncounter = false;
            Debug.Log($"[Auditor] Run start. Tokens: {_debugTokens}");
        }

        // ── Passive: OnCardSlammed ────────────────────────────

        public override void OnCardSlammed(PunchCardType cardType, ArchetypeContext ctx)
        {
            // Efficiency meter: track consecutive success streak
            // (Caller passes Success slams only — failures reset the count via
            //  RumorMill SlamResult event, but we don't track failure here;
            //  RunStateController resets streak on slam outcome.)
            _consecutiveSuccessStreak++;
            if (_consecutiveSuccessStreak >= EFFICIENCY_STREAK_TARGET)
            {
                _nextAnalyseIsFree = true;
                _consecutiveSuccessStreak = 0;
                Debug.Log("[Auditor] Efficiency streak! Next Analyse is free.");
            }
        }

        /// <summary>Called by RunStateController when a slam fails (state machine rejected).</summary>
        public void OnSlamFailed()
        {
            _consecutiveSuccessStreak = 0;
        }

        /// <summary>
        /// Called by RunStateController when a hidden trait is revealed on a claim.
        /// </summary>
        public void OnHiddenTraitRevealed(ArchetypeContext ctx)
        {
            float credits = ANOMALY_DIVIDEND_PER_TRAIT;
            ctx.AddCredits((int)credits);
            _debugTokens++;
            Debug.Log($"[Auditor] Anomaly Dividend: +{credits} credits. Tokens: {_debugTokens}.");
        }

        public void OnNewEncounter()
        {
            _deepAuditUsedThisEncounter = false;
        }

        // ── Active Ability: Deep Audit ────────────────────────

        public override string AbilityDescription =>
            $"Deep Audit — Spend 1 Debug Token: reveal hidden claim trait, " +
            $"+60s impatience reprieve. (Tokens: {_debugTokens})";

        public override bool CanUseAbility(ArchetypeContext ctx)
            => _debugTokens > 0 && !_deepAuditUsedThisEncounter;

        public override void UseAbility(ArchetypeContext ctx)
        {
            if (!CanUseAbility(ctx)) return;

            _debugTokens--;
            _deepAuditUsedThisEncounter = true;

            // Signal RunStateController to reveal claim's hidden trait
            // and extend the impatience timer by 60s
            ctx.EmitDarkHumour?.Invoke("deep_audit_activated");

            RumorMill.Publish(new MilestoneReachedEvent(
                MilestoneID.DeepAuditUsed,
                ctx.ShiftNumber,
                "deep_audit"));

            Debug.Log($"[Auditor] Deep Audit used. Tokens remaining: {_debugTokens}.");
        }

        // ── Modifier: Analyse is free when streak bonus active ─

        public override int ModifyCreditCost(PunchCardType cardType, int baseCost)
        {
            if (cardType == PunchCardType.Analyse && _nextAnalyseIsFree)
            {
                _nextAnalyseIsFree = false;
                return 0;
            }
            return baseCost;
        }

        // ── Vow: By The Book ──────────────────────────────────

        public sealed class ByTheBookVow : IArchetypeVow
        {
            public string VowId       => "by_the_book";
            public string Description => "Cannot play ThreatAudit or LegalHold cards.";
            public bool   IsBroken    { get; private set; }

            public void EvaluateOnSlam(PunchCardType cardType, ArchetypeContext ctx)
            {
                if (cardType == PunchCardType.ThreatAudit ||
                    cardType == PunchCardType.LegalHold)
                {
                    IsBroken = true;
                    Debug.LogWarning("[Vow] By The Book BROKEN — illegal card used.");
                }
            }

            public void EvaluateOnStateTransition(ClientStateID state, ArchetypeContext ctx)
            { /* no restriction on state transitions */ }
        }
    }
}
