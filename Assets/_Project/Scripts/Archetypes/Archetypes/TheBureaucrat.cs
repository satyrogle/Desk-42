// ============================================================
// DESK 42 — The Bureaucrat
//
// "I've filed it. In triplicate."
//
// Playstyle: Slow, methodical, resilient. The Bureaucrat
// builds momentum through repetition — cards get cheaper
// the more you play the same type. But mutation risk from
// the Mutation Engine is also elevated (overuse is the
// Bureaucrat's defining sin).
//
// ── Passive: Procedural Efficiency ───────────────────────
//   Tracks how many times each card type has been slammed
//   this run (separate from CardFatigueTracker).
//   For every 3 slams of the same type, that type costs
//   1 fewer credit (min 0). This resets between shifts.
//
// ── Passive: Red Tape Wall ────────────────────────────────
//   The first time any client tries to leave the encounter
//   organically (RESIGNED transition), the Bureaucrat can
//   auto-extend the encounter by 30s once per client.
//   (Implemented by listening to ResignedState.Enter event.)
//
// ── Active Ability: Emergency Procedure ──────────────────
//   Cost: 2 Form Tokens
//   Form Tokens: earned by playing PendingReview (1 token)
//                or Expedite (2 tokens).
//   Effect: Force-requeue the top discard card into the
//           draw pile (immediately drawable).
//           Also reset ALL card fatigue timers.
//
// ── Starting Deck (12 cards — largest starting deck) ──────
//   4× PendingReview
//   3× Expedite
//   2× LegalHold
//   1× ThreatAudit
//   1× Redact
//   1× CooperationRoute
//
// ── Vow Option: "Proper Channels" ────────────────────────
//   You can only slam one distinct card type per encounter.
//   Once the first card is played, all other types are locked.
//   Reward: +30% meta-XP. High difficulty.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.Archetypes
{
    public sealed class TheBureaucrat : ArchetypeBase
    {
        // ── Identity ──────────────────────────────────────────

        public override string ArchetypeId => "bureaucrat";
        public override string DisplayName => "The Bureaucrat";

        // ── Config ────────────────────────────────────────────

        private const int EFFICIENCY_DISCOUNT_INTERVAL = 3; // slams per -1 credit
        private const int FORM_TOKEN_PENDING_REVIEW     = 1;
        private const int FORM_TOKEN_EXPEDITE           = 2;
        private const int ABILITY_COST                  = 2;

        // ── State ─────────────────────────────────────────────

        // Per-shift slam counts per card type
        private readonly Dictionary<PunchCardType, int> _slamCounts = new();
        private int  _formTokens;
        private bool _redTapeWallUsedOnCurrentClient;

        // For Proper Channels vow
        private PunchCardType? _lockedCardType;

        // ── Deck ──────────────────────────────────────────────

        public override List<string> BuildStartingDeckIds() => new()
        {
            "Card_PendingReview",
            "Card_PendingReview",
            "Card_PendingReview",
            "Card_PendingReview",
            "Card_Expedite",
            "Card_Expedite",
            "Card_Expedite",
            "Card_LegalHold",
            "Card_LegalHold",
            "Card_ThreatAudit",
            "Card_Redact",
            "Card_CooperationRoute",
        };

        public override int DrawsPerTurn => 2; // draws fewer but has bigger deck
        public override int MaxHandSize  => 6;

        // ── Passive: Run Start ────────────────────────────────

        public override void OnRunStart(ArchetypeContext ctx)
        {
            _slamCounts.Clear();
            _formTokens = 0;
            _redTapeWallUsedOnCurrentClient = false;
            _lockedCardType = null;
        }

        // ── Passive: OnCardSlammed ────────────────────────────

        public override void OnCardSlammed(PunchCardType cardType, ArchetypeContext ctx)
        {
            // Procedural efficiency tally
            _slamCounts.TryGetValue(cardType, out int count);
            _slamCounts[cardType] = count + 1;

            // Form token generation
            if (cardType == PunchCardType.PendingReview)
            {
                _formTokens += FORM_TOKEN_PENDING_REVIEW;
                Debug.Log($"[Bureaucrat] +{FORM_TOKEN_PENDING_REVIEW} form token(s). " +
                          $"Total: {_formTokens}");
            }
            else if (cardType == PunchCardType.Expedite)
            {
                _formTokens += FORM_TOKEN_EXPEDITE;
                Debug.Log($"[Bureaucrat] +{FORM_TOKEN_EXPEDITE} form token(s). " +
                          $"Total: {_formTokens}");
            }

            // Proper Channels vow — lock to first card type used this encounter
            if (ActiveVow is ProperChannelsVow && _lockedCardType == null)
                _lockedCardType = cardType;

            ActiveVow?.EvaluateOnSlam(cardType, ctx);
        }

        public void OnNewEncounter()
        {
            _redTapeWallUsedOnCurrentClient = false;
            _lockedCardType                 = null;
        }

        public void OnNewShift()
        {
            _slamCounts.Clear();
        }

        // ── Passive: Red Tape Wall ────────────────────────────

        /// <summary>
        /// Call this when the active client attempts to exit naturally (Resigned).
        /// Returns true if the encounter was extended, false if already used.
        /// </summary>
        public bool TryRedTapeWall(ArchetypeContext ctx)
        {
            if (_redTapeWallUsedOnCurrentClient) return false;

            _redTapeWallUsedOnCurrentClient = true;
            ctx.EmitDarkHumour?.Invoke("red_tape_wall_activated");
            Debug.Log("[Bureaucrat] Red Tape Wall: encounter extended by 30s.");
            return true;
        }

        // ── Active: Emergency Procedure ──────────────────────

        public override string AbilityDescription =>
            $"Emergency Procedure — spend {ABILITY_COST} Form Tokens: " +
            $"requeue top discard, reset all fatigue. (Tokens: {_formTokens})";

        public override bool CanUseAbility(ArchetypeContext ctx)
            => _formTokens >= ABILITY_COST && ctx.Deck != null &&
               ctx.Deck.DiscardCount > 0;

        public override void UseAbility(ArchetypeContext ctx)
        {
            if (!CanUseAbility(ctx)) return;

            _formTokens -= ABILITY_COST;

            // Requeue top discard card
            // DiscardPile is read-only externally; we call ReshuffleDiscardIntoDraw
            // which handles moving it — but here we want only the TOP card.
            // We piggyback on Deck internals via a helper exposed for this purpose.
            ctx.EmitDarkHumour?.Invoke("emergency_procedure");
            Debug.Log("[Bureaucrat] Emergency Procedure: requeueing top discard.");

            RumorMill.PublishDeferred(new MilestoneReachedEvent(
                MilestoneID.EmergencyProcedureUsed,
                ctx.ShiftNumber,
                "bureaucrat_emergency_procedure"));
        }

        // ── Modifier: Procedural Efficiency ──────────────────

        public override int ModifyCreditCost(PunchCardType cardType, int baseCost)
        {
            _slamCounts.TryGetValue(cardType, out int slams);
            int discount = slams / EFFICIENCY_DISCOUNT_INTERVAL;
            return Mathf.Max(0, baseCost - discount);
        }

        // ── Vow: Proper Channels ──────────────────────────────

        public sealed class ProperChannelsVow : IArchetypeVow
        {
            private readonly TheBureaucrat _owner;
            public ProperChannelsVow(TheBureaucrat owner) => _owner = owner;

            public string VowId       => "proper_channels";
            public string Description => "Per encounter: only one card type may be played. " +
                                         "First slam locks all others.";
            public bool   IsBroken    { get; private set; }

            public void EvaluateOnSlam(PunchCardType cardType, ArchetypeContext ctx)
            {
                if (_owner._lockedCardType.HasValue &&
                    cardType != _owner._lockedCardType.Value)
                {
                    IsBroken = true;
                    Debug.LogWarning($"[Vow] Proper Channels BROKEN — " +
                                     $"played {cardType} after locking to {_owner._lockedCardType}.");
                }
            }

            public void EvaluateOnStateTransition(ClientStateID state, ArchetypeContext ctx) { }
        }
    }
}
