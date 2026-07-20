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

using System.Collections.Generic;
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
        // Sanity cost when a client's ATB gauge overflows before a card resolves.
        private const float ATB_OVERFLOW_SANITY_COST = 5f;

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

            // ── Step 0: ATB edge case ──────────────────────────
            // If the client's impatience gauge has overflowed, the state
            // change takes priority over the card: force the BSM transition
            // and apply the sanity hit BEFORE resolving injection, so the
            // card is evaluated against the post-overflow state.
            if (_activeClient.Impatience >= ClientStateMachine.MaxImpatience)
            {
                _activeClient.ForceState(ClientStateID.Agitated);
                GameManager.Instance?.Run?.ModifySanity(-ATB_OVERFLOW_SANITY_COST);
            }

            // ── Step 1: Fatigue check ─────────────────────────
            if (!_fatigue.CanPlay(cardInstanceId, card, out string reason))
            {
                var fatigueOutcome = _fatigue.IsJammed(cardInstanceId)
                    ? SlamOutcome.CardJammed
                    : SlamOutcome.CardCrumpled;
                PublishSlamEvent(card, cardInstanceId, CardSlamOutcome.CardJammed);
                return new SlamResult(fatigueOutcome, card) { Reason = reason };
            }

            // ── Supply cascade: one evaluation feeds both mechanics & UI ──
            // Replaces the separate ApplyDurationModifiers / ApplyCreditCostModifiers
            // / ApplySoulCostModifiers calls. Each supply's (stateful) Modify* runs
            // exactly once in here; we consume the Finals for the mechanics below and
            // publish the per-step trace for the CascadePresenter. Calling the scalar
            // Apply* methods in addition would double-run consumable effects (e.g. the
            // Stapler discount), desyncing what's shown from what's actually charged.
            SynergyResolutionPacket cascade = BuildCascade(card);

            // ── Step 2: Credit check ──────────────────────────
            int effectiveCost = ApplyFlatCreditModifiers(card, cascade.FinalCreditCost);

            // ── Vow: Zero-Based Budgeting — convert credit cost to sanity ──
            float sanityCostFraction = Core.ComplianceVowSystem.GetSanityCostFraction();
            if (sanityCostFraction > 0f && effectiveCost > 0)
            {
                float sanityCost = effectiveCost * sanityCostFraction;
                int creditPortion = Mathf.RoundToInt(effectiveCost * (1f - sanityCostFraction));

                if (creditPortion > 0 &&
                    !(GameManager.Instance?.Run?.SpendCredits(creditPortion) ?? false))
                {
                    PublishSlamEvent(card, cardInstanceId, CardSlamOutcome.InsufficientCredits, effectiveCost);
                    return new SlamResult(SlamOutcome.InsufficientCredits, card);
                }

                GameManager.Instance?.Run?.ModifySanity(-sanityCost);
            }
            else if (effectiveCost > 0 &&
                !(GameManager.Instance?.Run?.SpendCredits(effectiveCost) ?? false))
            {
                PublishSlamEvent(card, cardInstanceId, CardSlamOutcome.InsufficientCredits, effectiveCost);
                return new SlamResult(SlamOutcome.InsufficientCredits, card);
            }

            // ── Step 3: Attempt injection on client BSM ───────
            float durationOverride = ApplyStateEffectiveness(card, cascade.FinalDuration);
            var injectionResult = _activeClient.TryInject(
                card.CardType.ToString(), durationOverride);

            if (injectionResult != ClientStateMachine.InjectionResult.Success)
            {
                // Refund credits if injection failed
                if (effectiveCost > 0)
                    GameManager.Instance?.Run?.AddCredits(effectiveCost);

                var mapped = MapInjectionFailure(injectionResult, card);
                PublishSlamEvent(card, cardInstanceId, MapToEventOutcome(mapped.Outcome));
                return mapped;
            }

            // ── Step 4: Record fatigue ────────────────────────
            var fatigueResult = _fatigue.RecordPlay(cardInstanceId, card);

            // ── Step 4b: Move card from Hand → Discard/Archive ────
            var run = GameManager.Instance?.Run;
            if (run != null)
            {
                var cardInst = run.Hand.FindById(cardInstanceId);
                if (cardInst != null)
                    run.Hand.OnCardPlayed(cardInst, _fatigue, run.Deck);
            }

            // ── Step 5: Soul cost (resolved in the cascade above) ────
            // FinalSoulCost already has archetype + supply modifiers applied and
            // is zeroed below phase 3 (see BuildCascade), so the old phase /
            // SoulCost gate is preserved without re-running the chain.
            if (cascade.FinalSoulCost > 0f)
                GameManager.Instance?.Run?.ModifySoulIntegrity(-cascade.FinalSoulCost);

            // ── Step 5b: Render the resolution (FF-style cascade) ────
            RumorMill.Publish(new CardCascadeResolvedEvent(cascade, cardInstanceId));

            // ── Step 5b: Office environment card effect ─────────
            OfficeEnvironmentState.ApplyCardEffect(card.CardType);

            // ── Step 6: Publish to Rumor Mill ─────────────────
            GameManager.Instance?.Run?.RecordCardSlam();

            RumorMill.Publish(new CardSlammedEvent(
                card.CardType,
                cardInstanceId,
                _activeClient.ClientVariantId,
                _activeClient.CurrentMoodState,
                _fatigue.GetFatigue(cardInstanceId),
                CardSlamOutcome.Success));

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

        // ── Supply Cascade Build ──────────────────────────────

        /// <summary>
        /// Runs the single SynergyResolver cascade for this slam. Archetype
        /// modifiers hit the bases first (archetype → supplies is the established
        /// order), then ResolveCascade runs each supply's Modify* exactly once
        /// across the duration, credit and soul chains. The packet's Finals drive
        /// the mechanics in TrySlam; its Steps drive the CascadePresenter. Soul is
        /// phase-gated: below phase 3 the soul chain is suppressed so no phantom
        /// soul cost is shown or charged.
        /// </summary>
        private SynergyResolutionPacket BuildCascade(PunchCardData card)
        {
            var archetype = GameManager.Instance?.Run?.Archetype;

            // Post-archetype duration base
            float baseDuration = card.InjectionDuration;
            if (archetype != null)
                baseDuration = archetype.ModifyInjectionDuration(card.CardType, baseDuration);
            else
                baseDuration *= card.ArchetypeMultiplier;

            // Post-archetype credit base
            int baseCredit = card.CreditCost;
            if (archetype != null)
                baseCredit = archetype.ModifyCreditCost(card.CardType, baseCredit);

            // Post-archetype, phase-gated soul base
            float baseSoul = (GameManager.Phase >= 3 && card.SoulCost > 0f) ? card.SoulCost : 0f;
            if (archetype != null && baseSoul > 0f)
                baseSoul = archetype.ModifySoulCost(card.CardType, baseSoul);

            var resolver = GameManager.Instance?.Supplies?.Resolver;
            SynergyResolutionPacket packet = resolver != null
                ? resolver.ResolveCascade(card.CardType, baseDuration, baseCredit, baseSoul, card.TypeTags)
                : new SynergyResolutionPacket
                {
                    CardType        = card.CardType,
                    BaseDuration    = baseDuration, FinalDuration   = baseDuration,
                    BaseCreditCost  = baseCredit,   FinalCreditCost = baseCredit,
                    BaseSoulCost    = baseSoul,     FinalSoulCost   = baseSoul,
                    DurationSteps   = new List<ModifierStep>(),
                    CreditCostSteps = new List<ModifierStep>(),
                    SoulCostSteps   = new List<ModifierStep>(),
                };

            // Suppress phantom soul when soul cost is inactive this slam.
            if (baseSoul <= 0f)
            {
                packet.BaseSoulCost  = 0f;
                packet.FinalSoulCost = 0f;
                packet.SoulCostSteps?.Clear();
            }

            return packet;
        }

        // ── State Effectiveness (post-cascade duration) ───────

        /// <summary>
        /// Applies BSM mood effectiveness and mutation passives to the
        /// supply-resolved duration. Separate from the supply cascade because
        /// it depends on live client mood, not desk supplies.
        /// </summary>
        private float ApplyStateEffectiveness(PunchCardData card, float duration)
        {
            // BSM State Effectiveness 
            if (_activeClient != null)
            {
                var mood = _activeClient.CurrentMoodState;
                if (mood == ClientStateID.Suspicious && card.CardType == PunchCardType.CooperationRoute)
                    duration *= 0.5f;
                else if (mood == ClientStateID.Paranoid && 
                        (card.CardType == PunchCardType.Analyse || card.CardType == PunchCardType.Redact))
                    duration *= 0.5f;
                else if (mood == ClientStateID.Dissociating)
                    duration = 0f;

                // Form Predelegation mutation passive
                if (card.CardType == PunchCardType.PendingReview && 
                    (GameManager.Instance?.Meta?.HasCounterTrait(_activeClient.ClientVariantId, "form_predelegation") ?? false))
                {
                    duration *= 0.5f;
                }
            }

            duration *= Core.OfficeEnvironmentState.GetInjectionDurationMultiplier();

            return duration;
        }

        // ── Flat Credit Modifiers (post-cascade credit) ───────

        /// <summary>
        /// Applies the flat, non-supply credit adjustments (vow NDA surcharge,
        /// faction reputation, escalating-regulation penalty) on top of the
        /// supply-resolved cost. Order preserved from the original chain:
        /// archetype + supply (in the cascade) → these flat adjustments.
        /// </summary>
        private int ApplyFlatCreditModifiers(PunchCardData card, int cost)
        {

            int stricterNDARank = GameManager.Instance?.Run?.GetVowRank("stricter_nondisclosure") ?? 0;
            if (stricterNDARank > 0 && (card.CardType == PunchCardType.Redact || card.CardType == PunchCardType.NonDisclosure))
                cost += stricterNDARank * 2;

            // Faction Cost Modifiers
            if (GameManager.Instance?.Run != null)
            {
                var run = GameManager.Instance.Run;
                if (card.CardType == PunchCardType.LegalHold || card.CardType == PunchCardType.ThreatAudit) 
                {
                    if (run.GetFactionRep(Desk42.Core.FactionID.Legal) > 50) cost -= 2;
                    else if (run.GetFactionRep(Desk42.Core.FactionID.Legal) < -50) cost += 3;
                }

                if (run.GetFactionRep(Desk42.Core.FactionID.Accounting) > 50) cost -= 1; // Accounting cuts base costs
                else if (run.GetFactionRep(Desk42.Core.FactionID.Accounting) < -50) cost += 1;

                // ── Phase 3: Escalating Regulations ──
                if (!string.IsNullOrEmpty(run.EscalatingRegulationCardId) && 
                    card.CardType.ToString() == run.EscalatingRegulationCardId)
                {
                    cost *= 3; // Triple cost penalty!
                }
            }

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

        private void PublishSlamEvent(PunchCardData card, string cardInstanceId,
            CardSlamOutcome outcome, int creditCost = 0)
        {
            if (_activeClient == null) return;
            RumorMill.Publish(new CardSlammedEvent(
                card.CardType,
                cardInstanceId,
                _activeClient.ClientVariantId,
                _activeClient.CurrentMoodState,
                _fatigue?.GetFatigue(cardInstanceId) ?? 0,
                outcome,
                creditCost));
        }

        private static CardSlamOutcome MapToEventOutcome(SlamOutcome outcome)
        {
            return outcome switch
            {
                SlamOutcome.BlockedByPreFiledExemption => CardSlamOutcome.BlockedByExemption,
                SlamOutcome.ClientNotResponding        => CardSlamOutcome.ClientNotResponding,
                SlamOutcome.InsufficientCredits        => CardSlamOutcome.InsufficientCredits,
                SlamOutcome.CardJammed                 => CardSlamOutcome.CardJammed,
                _                                      => CardSlamOutcome.BlockedByState,
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
