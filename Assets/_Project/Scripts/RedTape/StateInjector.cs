// ============================================================
// DESK 42 — State Injector
//
// The bridge between the PunchCardMachine and the
// ClientStateMachine. When a card is slammed:
//
//   1. Validate: card is in hand, not jammed/crumpled, credits OK.
//   2. Check BT blockers (mutation counter-nodes).
//   3. Attempt injection on the active ClientStateMachine.
//   4. Record fatigue, deduct credits.
//   5. Publish events to RumorMill.
//   6. Signal MutationEngine to check if a counter-trait should fire.
//
// Lives on the PunchCardMachine GameObject.
// ============================================================

using UnityEngine;
using Desk42.Core;
using Desk42.Cards;
using Desk42.BSM;
using Desk42.OfficeSupplies;

namespace Desk42.RedTape
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PunchCardMachine))]
    public sealed class StateInjector : MonoBehaviour
    {
        // ── Dependencies ──────────────────────────────────────

        // Set by encounter setup
        private ClientStateMachine _activeClient;
        private CardFatigueTracker _fatigue;
        private MutationEngine     _mutation;

        // ── Init ──────────────────────────────────────────────

        public void Initialize(ClientStateMachine client,
            CardFatigueTracker fatigue,
            MutationEngine mutation)
        {
            _activeClient = client;
            _fatigue      = fatigue;
            _mutation     = mutation;
        }

        public void ClearClient() => _activeClient = null;

        // ── Main Entry Point ──────────────────────────────────

        /// <summary>
        /// Called by PunchCardMachine when a card is physically slammed.
        /// Returns the result so the machine can play appropriate feedback.
        /// </summary>
        public SlamResult TrySlam(PunchCardData card, string cardInstanceId)
        {
            if (_activeClient == null)
                return new SlamResult(SlamOutcome.NoActiveClient, card);

            if (card == null)
                return new SlamResult(SlamOutcome.InvalidCard, null);

            // ── Step 1: Fatigue check ─────────────────────────
            if (!_fatigue.CanPlay(cardInstanceId, card, out string reason))
            {
                var fatigueOutcome = _fatigue.IsJammed(cardInstanceId)
                    ? SlamOutcome.CardJammed
                    : SlamOutcome.CardCrumpled;
                return new SlamResult(fatigueOutcome, card) { Reason = reason };
            }

            // ── Step 2: Credit check ──────────────────────────
            int effectiveCost = ComputeCreditCost(card);
            if (effectiveCost > 0 &&
                !(GameManager.Instance?.Run?.SpendCredits(effectiveCost) ?? false))
            {
                return new SlamResult(SlamOutcome.InsufficientCredits, card);
            }

            // ── Step 3: Attempt injection on client BSM ───────
            float durationOverride = ComputeDurationOverride(card);
            var injectionResult = _activeClient.TryInject(
                card.CardType.ToString(), durationOverride);

            if (injectionResult != ClientStateMachine.InjectionResult.Success)
            {
                // Refund credits if injection failed
                if (effectiveCost > 0)
                    GameManager.Instance?.Run?.AddCredits(effectiveCost);

                return MapInjectionFailure(injectionResult, card);
            }

            // ── Step 4: Record fatigue ────────────────────────
            var fatigueResult = _fatigue.RecordPlay(cardInstanceId, card);

            // ── Step 5: Soul cost (after supply modifiers) ───────────
            if (card.SoulCost > 0f)
            {
                float soulCost = card.SoulCost;
                var soulResolver = GameManager.Instance?.Supplies?.Resolver;
                if (soulResolver != null)
                    soulCost = soulResolver.ApplySoulCostModifiers(soulCost);
                if (soulCost > 0f)
                    GameManager.Instance?.Run?.ModifySoulIntegrity(-soulCost);
            }

            // ── Step 6: Publish to Rumor Mill ─────────────────
            GameManager.Instance?.Run?.RecordCardSlam();

            RumorMill.Publish(new CardSlammedEvent(
                card.CardType,
                cardInstanceId,
                _activeClient.ClientVariantId,
                _activeClient.CurrentMoodState,
                _fatigue.GetFatigue(cardInstanceId)));

            // ── Step 7: Update Repeat Offender DB ────────────
            GameManager.Instance?.Meta?.RecordCardUsed(
                _activeClient.ClientVariantId, card.CardType);

            // ── Step 8: Check mutation ────────────────────────
            _mutation?.CheckAndMutate(
                _activeClient,
                card.CardType,
                GameManager.Instance?.Meta);

            return new SlamResult(SlamOutcome.Success, card)
            {
                FatigueOutcome = fatigueResult,
                NewFatigue     = _fatigue.GetFatigue(cardInstanceId),
            };
        }

        // ── Duration Override ─────────────────────────────────

        /// <summary>
        /// Computes the effective injection duration after applying
        /// archetype and active office supply modifier chains.
        /// </summary>
        private float ComputeDurationOverride(PunchCardData card)
        {
            // Base duration from the card SO
            float duration = card.InjectionDuration;

            // Archetype multiplier (e.g. Bureaucrat efficiency meter boost)
            if (GameManager.Instance?.Run?.Archetype != null)
                duration = GameManager.Instance.Run.Archetype
                    .ModifyInjectionDuration(card.CardType, duration);
            else
                duration *= card.ArchetypeMultiplier;

            // Office supply synergy chain
            var resolver = GameManager.Instance?.Supplies?.Resolver;
            if (resolver != null)
                duration = resolver.ApplyDurationModifiers(
                    card.CardType, duration, card.TypeTags);

            return duration;
        }

        /// <summary>
        /// Computes the effective credit cost after archetype and supply modifiers.
        /// </summary>
        private int ComputeCreditCost(PunchCardData card)
        {
            int cost = card.CreditCost;

            if (GameManager.Instance?.Run?.Archetype != null)
                cost = GameManager.Instance.Run.Archetype
                    .ModifyCreditCost(card.CardType, cost);

            var resolver = GameManager.Instance?.Supplies?.Resolver;
            if (resolver != null)
                cost = resolver.ApplyCreditCostModifiers(card.CardType, cost);

            return Mathf.Max(0, cost);
        }

        // ── Failure Mapping ───────────────────────────────────

        private static SlamResult MapInjectionFailure(
            ClientStateMachine.InjectionResult result, PunchCardData card)
        {
            return result switch
            {
                ClientStateMachine.InjectionResult.BlockedByCounterTrait =>
                    new SlamResult(SlamOutcome.BlockedByPreFiledExemption, card)
                    { Reason = "Pre-Filed Exemption on file." },

                ClientStateMachine.InjectionResult.ClientDissociating =>
                    new SlamResult(SlamOutcome.ClientNotResponding, card)
                    { Reason = "Client is not responsive." },

                _ => new SlamResult(SlamOutcome.BlockedByCurrentState, card)
            };
        }
    }

    // ── Result Types ─────────────────────────────────────────

    public enum SlamOutcome
    {
        Success,
        CardJammed,
        CardCrumpled,
        InsufficientCredits,
        NoActiveClient,
        InvalidCard,
        BlockedByPreFiledExemption,  // mutation counter-node fired
        BlockedByCurrentState,
        ClientNotResponding,         // DISSOCIATING
    }

    public sealed class SlamResult
    {
        public readonly SlamOutcome   Outcome;
        public readonly PunchCardData Card;
        public string   Reason;
        public CardFatigueTracker.FatigueOutcome FatigueOutcome;
        public int      NewFatigue;

        public bool IsSuccess => Outcome == SlamOutcome.Success;

        public SlamResult(SlamOutcome outcome, PunchCardData card)
        {
            Outcome = outcome;
            Card    = card;
        }
    }
}
