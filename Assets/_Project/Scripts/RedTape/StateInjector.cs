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
        /// Projects a slam from the current live snapshot without mutating the
        /// client, resources, fatigue, supplies, statistics, or event bus.
        /// The UI labels this expected because state can move before processing.
        /// </summary>
        public ProjectedCardResolution PreviewSlam(
            PunchCardData card, string cardInstanceId)
        {
            if (card == null)
            {
                return new ProjectedCardResolution(
                    default, "INVALID CARD", cardInstanceId,
                    CardSlamOutcome.InvalidCard,
                    "No punch card data was supplied.",
                    null, null, 0, 0f, 0f, 0, 0, 0,
                    default, hasCascade: false);
            }

            var run = GameManager.Instance?.Run;
            int fatigueBefore = _fatigue?.GetFatigue(cardInstanceId) ?? 0;
            var notices = new List<string>();

            if (_activeClient == null)
            {
                return NewProjection(card, cardInstanceId,
                    CardSlamOutcome.NoActiveClient, "No active client.",
                    null, null, 0, 0f, 0f, 0,
                    fatigueBefore, fatigueBefore, default, false, notices);
            }

            ClientStateID stateBefore = _activeClient.CurrentMoodState;
            ClientStateID evaluationState = stateBefore;
            float atbSanityCost = 0f;

            if (_activeClient.Impatience >= ClientStateMachine.MaxImpatience)
            {
                evaluationState = ClientStateID.Agitated;
                atbSanityCost = ATB_OVERFLOW_SANITY_COST;
                notices.Add("Impatience overflow will force AGITATED before processing.");
            }

            if (_fatigue != null
                && !_fatigue.CanPlay(cardInstanceId, card, out string fatigueReason))
            {
                CardSlamOutcome fatigueOutcome = _fatigue.IsJammed(cardInstanceId)
                    ? CardSlamOutcome.CardJammed
                    : CardSlamOutcome.CardCrumpled;
                return NewProjection(card, cardInstanceId,
                    fatigueOutcome, fatigueReason,
                    stateBefore, evaluationState, 0,
                    run?.ProjectSanityDelta(-atbSanityCost) ?? 0f,
                    0f, 0, fatigueBefore, fatigueBefore,
                    default, false, notices);
            }

            SynergyResolutionPacket cascade = BuildCascade(card, previewOnly: true);
            int effectiveCost = ApplyFlatCreditModifiers(card, cascade.FinalCreditCost);
            float sanityCostFraction = Core.ComplianceVowSystem.GetSanityCostFraction();
            float convertedSanityCost = 0f;
            int creditPortion = effectiveCost;

            if (sanityCostFraction > 0f && effectiveCost > 0)
            {
                convertedSanityCost = effectiveCost * sanityCostFraction;
                creditPortion = Mathf.RoundToInt(effectiveCost * (1f - sanityCostFraction));
                notices.Add($"Zero-Based Budgeting converts {convertedSanityCost:0.##} cost to Sanity.");
            }

            AddCascadeNotices(cascade, notices);

            int availableCredits = run?.Credits ?? 0;
            if (creditPortion > 0 && availableCredits < creditPortion)
            {
                string reason = $"Balance ¢{availableCredits}; {card.DisplayName} requires ¢{creditPortion}.";
                return NewProjection(card, cardInstanceId,
                    CardSlamOutcome.InsufficientCredits, reason,
                    stateBefore, evaluationState, 0,
                    run?.ProjectSanityDelta(-atbSanityCost) ?? 0f,
                    0f, creditPortion, fatigueBefore, fatigueBefore,
                    cascade, true, notices);
            }

            float projectedSanityDelta = run?.ProjectSanityDelta(
                -(atbSanityCost + convertedSanityCost)) ?? 0f;
            float durationOverride = ApplyStateEffectiveness(
                card, cascade.FinalDuration, evaluationState);
            if (!Mathf.Approximately(durationOverride, cascade.FinalDuration))
                notices.Add($"Client or office state changes effect duration to {durationOverride:0.##}s.");

            var injection = _activeClient.PreviewInject(
                card.CardType.ToString(), out var targetState, evaluationState);
            bool hasConcealedBlockRisk = false;
            string disclosedBlockerId = null;
            bool blockerIsRevealed = false;

            if (injection == ClientStateMachine.InjectionResult.BlockedByCounterTrait)
            {
                GetBlockingDisclosure(card, out disclosedBlockerId,
                    out blockerIsRevealed);
                if (!blockerIsRevealed)
                {
                    // Project the rule the player can currently see, while
                    // truthfully marking that concealed client information may
                    // intercept it. Never leak the hidden trait identifier.
                    var visibleRulesResult = _activeClient.PreviewInject(
                        card.CardType.ToString(), out targetState,
                        evaluationState, ignoreCounterTrait: true);
                    if (visibleRulesResult == ClientStateMachine.InjectionResult.Success)
                    {
                        injection = visibleRulesResult;
                        hasConcealedBlockRisk = true;
                        notices.Add("A concealed client trait may block this card.");
                    }
                }
            }

            if (injection != ClientStateMachine.InjectionResult.Success)
            {
                string reason = DescribeInjectionFailure(
                    injection, card, evaluationState);
                return NewProjection(card, cardInstanceId,
                    MapInjectionOutcome(injection), reason,
                    stateBefore, evaluationState, 0,
                    projectedSanityDelta, 0f, 0,
                    fatigueBefore, fatigueBefore,
                    cascade, true, notices,
                    blockingModifierId: blockerIsRevealed ? disclosedBlockerId : null,
                    isBlockingModifierRevealed: blockerIsRevealed);
            }

            int fatigueAfter = fatigueBefore + 1;
            if (card.JamFatigue >= 0 && fatigueAfter == card.JamFatigue)
                notices.Add("This use will jam the card briefly.");
            if (card.MaxFatigue >= 0 && fatigueAfter >= card.MaxFatigue)
                notices.Add("This use will crumple the card.");

            TryDescribeInjectedEffect(card.CardType, durationOverride,
                out string clientEffect, out float clientEffectDuration);

            return NewProjection(card, cardInstanceId,
                CardSlamOutcome.Success, null,
                stateBefore, targetState, -creditPortion,
                projectedSanityDelta,
                run?.ProjectSoulIntegrityDelta(-cascade.FinalSoulCost) ?? 0f,
                0, fatigueBefore, fatigueAfter,
                cascade, true, notices, clientEffect, clientEffectDuration,
                blockingModifierId: null,
                isBlockingModifierRevealed: false,
                hasConcealedBlockRisk: hasConcealedBlockRisk);
        }

        /// <summary>
        /// Called by PunchCardMachine when a card is physically slammed.
        /// Returns the result so the machine can play appropriate feedback.
        /// </summary>
        public SlamResult TrySlam(PunchCardData card, string cardInstanceId)
        {
            if (card == null)
            {
                var invalid = new SlamResult(SlamOutcome.InvalidCard, null)
                {
                    Reason = "No punch card data was supplied."
                };
                var applied = new AppliedCardResolution(
                    default,
                    cardInstanceId,
                    _activeClient?.ClientVariantId,
                    CardSlamOutcome.InvalidCard,
                    invalid.Reason,
                    _activeClient?.CurrentMoodState,
                    _activeClient?.CurrentMoodState,
                    creditsDelta: 0,
                    sanityDelta: 0f,
                    soulIntegrityDelta: 0f,
                    requiredCredits: 0,
                    fatigueBefore: 0,
                    fatigueAfter: 0,
                    default,
                    hasCascade: false);
                RumorMill.Publish(new CardSlammedEvent(applied));
                return invalid;
            }

            var run = GameManager.Instance?.Run;
            int creditsBefore = run?.Credits ?? 0;
            float sanityBefore = run?.Sanity ?? 0f;
            float soulBefore = run?.SoulIntegrity ?? 0f;
            int fatigueBefore = _fatigue?.GetFatigue(cardInstanceId) ?? 0;

            if (_activeClient == null)
            {
                var noClient = new SlamResult(SlamOutcome.NoActiveClient, card)
                {
                    Reason = "No active client."
                };
                PublishAppliedCard(card, cardInstanceId, null,
                    CardSlamOutcome.NoActiveClient, noClient.Reason,
                    null, null, creditsBefore, sanityBefore, soulBefore,
                    requiredCredits: 0, fatigueBefore,
                    default, hasCascade: false);
                return noClient;
            }

            string clientId = _activeClient.ClientVariantId;
            ClientStateID stateBefore = _activeClient.CurrentMoodState;

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
                var result = new SlamResult(fatigueOutcome, card) { Reason = reason };
                PublishAppliedCard(card, cardInstanceId, clientId,
                    MapToEventOutcome(fatigueOutcome), result.Reason,
                    stateBefore, _activeClient.CurrentMoodState,
                    creditsBefore, sanityBefore, soulBefore,
                    requiredCredits: 0, fatigueBefore,
                    default, hasCascade: false);
                return result;
            }

            // ── Supply cascade: one evaluation feeds both mechanics & UI ──
            // Replaces the separate ApplyDurationModifiers / ApplyCreditCostModifiers
            // / ApplySoulCostModifiers calls. Each supply's (stateful) Modify* runs
            // exactly once in here; we consume the Finals for the mechanics below and
            // publish the per-step trace for the CascadePresenter. Calling the scalar
            // Apply* methods in addition would double-run consumable effects (e.g. the
            // Stapler discount), desyncing what's shown from what's actually charged.
            SynergyResolutionPacket cascade = BuildCascade(card, previewOnly: false);

            // ── Step 2: Credit check ──────────────────────────
            int effectiveCost = ApplyFlatCreditModifiers(card, cascade.FinalCreditCost);
            int creditsSpent = 0;

            // ── Vow: Zero-Based Budgeting — convert credit cost to sanity ──
            float sanityCostFraction = Core.ComplianceVowSystem.GetSanityCostFraction();
            if (sanityCostFraction > 0f && effectiveCost > 0)
            {
                float sanityCost = effectiveCost * sanityCostFraction;
                int creditPortion = Mathf.RoundToInt(effectiveCost * (1f - sanityCostFraction));

                if (creditPortion > 0 &&
                    !(run?.SpendCredits(creditPortion) ?? false))
                {
                    var insufficient = new SlamResult(SlamOutcome.InsufficientCredits, card)
                    {
                        Reason = $"Balance ¢{creditsBefore}; {card.DisplayName} requires ¢{creditPortion}."
                    };
                    PublishAppliedCard(card, cardInstanceId, clientId,
                        CardSlamOutcome.InsufficientCredits, insufficient.Reason,
                        stateBefore, _activeClient.CurrentMoodState,
                        creditsBefore, sanityBefore, soulBefore,
                        creditPortion, fatigueBefore, cascade, hasCascade: true);
                    return insufficient;
                }

                creditsSpent = creditPortion;

                run?.ModifySanity(-sanityCost);
            }
            else if (effectiveCost > 0 &&
                !(run?.SpendCredits(effectiveCost) ?? false))
            {
                var insufficient = new SlamResult(SlamOutcome.InsufficientCredits, card)
                {
                    Reason = $"Balance ¢{creditsBefore}; {card.DisplayName} requires ¢{effectiveCost}."
                };
                PublishAppliedCard(card, cardInstanceId, clientId,
                    CardSlamOutcome.InsufficientCredits, insufficient.Reason,
                    stateBefore, _activeClient.CurrentMoodState,
                    creditsBefore, sanityBefore, soulBefore,
                    effectiveCost, fatigueBefore, cascade, hasCascade: true);
                return insufficient;
            }
            else
            {
                creditsSpent = effectiveCost;
            }

            // ── Step 3: Attempt injection on client BSM ───────
            float durationOverride = ApplyStateEffectiveness(card, cascade.FinalDuration);
            var injectionResult = _activeClient.TryInject(
                card.CardType.ToString(), durationOverride);

            if (injectionResult != ClientStateMachine.InjectionResult.Success)
            {
                // Refund credits if injection failed
                if (creditsSpent > 0)
                    run?.RefundFailedSpend(creditsSpent);

                var mapped = MapInjectionFailure(
                    injectionResult, card, _activeClient.CurrentMoodState);
                GetBlockingDisclosure(card, out string blockingModifierId,
                    out _);
                if (!string.IsNullOrWhiteSpace(blockingModifierId))
                    _activeClient.RevealCounterTrait(blockingModifierId);
                PublishAppliedCard(card, cardInstanceId, clientId,
                    MapToEventOutcome(mapped.Outcome), mapped.Reason,
                    stateBefore, _activeClient.CurrentMoodState,
                    creditsBefore, sanityBefore, soulBefore,
                    requiredCredits: 0, fatigueBefore, cascade, hasCascade: true,
                    blockingModifierId: blockingModifierId);
                return mapped;
            }

            // ── Step 4: Record fatigue ────────────────────────
            var fatigueResult = _fatigue.RecordPlay(cardInstanceId, card);

            // ── Step 4b: Move card from Hand → Discard/Archive ────
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
                run?.ModifySoulIntegrity(-cascade.FinalSoulCost);

            // ── Step 5b: Render the resolution (FF-style cascade) ────
            RumorMill.Publish(new CardCascadeResolvedEvent(cascade, cardInstanceId));

            // ── Step 5b: Office environment card effect ─────────
            OfficeEnvironmentState.ApplyCardEffect(card.CardType);

            // ── Step 6: Publish to Rumor Mill ─────────────────
            run?.RecordCardSlam();

            PublishAppliedCard(card, cardInstanceId, clientId,
                CardSlamOutcome.Success, null,
                stateBefore, _activeClient.CurrentMoodState,
                creditsBefore, sanityBefore, soulBefore,
                requiredCredits: 0, fatigueBefore, cascade, hasCascade: true,
                clientEffect: GetInjectedEffectLabel(card.CardType),
                clientEffectDuration: GetInjectedEffectDuration(
                    card.CardType, durationOverride));

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
        private SynergyResolutionPacket BuildCascade(
            PunchCardData card, bool previewOnly)
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
                ? (previewOnly
                    ? resolver.PreviewCascade(card.CardType, baseDuration, baseCredit, baseSoul, card.TypeTags)
                    : resolver.ResolveCascade(card.CardType, baseDuration, baseCredit, baseSoul, card.TypeTags))
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
        private float ApplyStateEffectiveness(PunchCardData card, float duration,
            ClientStateID? moodOverride = null)
        {
            // BSM State Effectiveness 
            if (_activeClient != null)
            {
                var mood = moodOverride ?? _activeClient.CurrentMoodState;
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

        private SlamResult MapInjectionFailure(
            ClientStateMachine.InjectionResult result, PunchCardData card,
            ClientStateID evaluationState)
        {
            var mapped = result switch
            {
                ClientStateMachine.InjectionResult.BlockedByCounterTrait =>
                    new SlamResult(SlamOutcome.BlockedByPreFiledExemption, card)
                    { Reason = DescribeInjectionFailure(result, card, evaluationState) },

                ClientStateMachine.InjectionResult.ClientDissociating =>
                    new SlamResult(SlamOutcome.ClientNotResponding, card)
                    { Reason = DescribeInjectionFailure(result, card, evaluationState) },

                _ => new SlamResult(SlamOutcome.BlockedByCurrentState, card)
                    { Reason = DescribeInjectionFailure(result, card, evaluationState) }
            };
            return mapped;
        }

        private string DescribeInjectionFailure(
            ClientStateMachine.InjectionResult result, PunchCardData card,
            ClientStateID evaluationState)
        {
            string cardName = string.IsNullOrWhiteSpace(card?.DisplayName)
                ? card?.CardType.ToString() ?? "This card"
                : card.DisplayName;

            switch (result)
            {
                case ClientStateMachine.InjectionResult.BlockedByCounterTrait:
                    string trait = "a pre-filed exemption";
                    if (_activeClient?.BaseBT != null
                        && _activeClient.BaseBT.TryGetBlockerForCard(
                            card.CardType.ToString(), out var blocker)
                        && !string.IsNullOrWhiteSpace(blocker.CounterTraitId))
                    {
                        trait = blocker.CounterTraitId.Replace('_', ' ');
                    }
                    return $"Pre-filed exemption: {trait} blocks {cardName}.";

                case ClientStateMachine.InjectionResult.ClientDissociating:
                    return $"{cardName} cannot affect a DISSOCIATING client.";

                default:
                    return $"{cardName} has no transition from {evaluationState.ToString().ToUpperInvariant()}.";
            }
        }

        private static CardSlamOutcome MapInjectionOutcome(
            ClientStateMachine.InjectionResult result)
        {
            return result switch
            {
                ClientStateMachine.InjectionResult.BlockedByCounterTrait =>
                    CardSlamOutcome.BlockedByExemption,
                ClientStateMachine.InjectionResult.ClientDissociating =>
                    CardSlamOutcome.ClientNotResponding,
                _ => CardSlamOutcome.BlockedByState,
            };
        }

        private static ProjectedCardResolution NewProjection(
            PunchCardData card,
            string cardInstanceId,
            CardSlamOutcome outcome,
            string failureReason,
            ClientStateID? stateBefore,
            ClientStateID? stateAfter,
            int creditsDelta,
            float sanityDelta,
            float soulIntegrityDelta,
            int requiredCredits,
            int fatigueBefore,
            int fatigueAfter,
            SynergyResolutionPacket cascade,
            bool hasCascade,
            List<string> notices,
            string clientEffect = null,
            float clientEffectDuration = 0f,
            string blockingModifierId = null,
            bool isBlockingModifierRevealed = false,
            bool hasConcealedBlockRisk = false)
        {
            return new ProjectedCardResolution(
                card.CardType,
                card.DisplayName,
                cardInstanceId,
                outcome,
                failureReason,
                stateBefore,
                stateAfter,
                creditsDelta,
                sanityDelta,
                soulIntegrityDelta,
                requiredCredits,
                fatigueBefore,
                fatigueAfter,
                cascade,
                hasCascade,
                notices?.ToArray(),
                clientEffect,
                clientEffectDuration,
                blockingModifierId,
                isBlockingModifierRevealed,
                hasConcealedBlockRisk);
        }

        private void GetBlockingDisclosure(PunchCardData card,
            out string blockerId, out bool isRevealed)
        {
            blockerId = null;
            isRevealed = false;
            if (card == null || _activeClient == null) return;
            _activeClient.TryGetBlockingTrait(
                card.CardType.ToString(), out blockerId, out isRevealed);
        }

        private static bool TryDescribeInjectedEffect(
            PunchCardType cardType,
            float requestedDuration,
            out string effectLabel,
            out float effectDuration)
        {
            effectLabel = GetInjectedEffectLabel(cardType);
            effectDuration = GetInjectedEffectDuration(cardType, requestedDuration);
            return !string.IsNullOrEmpty(effectLabel);
        }

        private static string GetInjectedEffectLabel(PunchCardType cardType)
        {
            return cardType switch
            {
                PunchCardType.PendingReview => "CLIENT ACTIONS PAUSED FOR REVIEW",
                PunchCardType.LegalHold => "CLIENT ACTIONS SUSPENDED",
                PunchCardType.Expedite => "CLIENT PROCESSING EXPEDITED",
                PunchCardType.CooperationRoute => "CLIENT FORCED COOPERATIVE",
                _ => null,
            };
        }

        private static float GetInjectedEffectDuration(
            PunchCardType cardType, float requestedDuration)
        {
            if (requestedDuration > 0f) return requestedDuration;

            return cardType switch
            {
                PunchCardType.PendingReview => 10f,
                PunchCardType.LegalHold => 15f,
                PunchCardType.Expedite => 5f,
                PunchCardType.CooperationRoute => 8f,
                _ => 0f,
            };
        }

        private static void AddCascadeNotices(
            SynergyResolutionPacket cascade, List<string> notices)
        {
            if (notices == null) return;

            if (!Mathf.Approximately(cascade.BaseDuration, cascade.FinalDuration))
                notices.Add($"Desk supplies change duration {cascade.BaseDuration:0.##}s → {cascade.FinalDuration:0.##}s.");
            if (cascade.BaseCreditCost != cascade.FinalCreditCost)
                notices.Add($"Desk supplies change cost ¢{cascade.BaseCreditCost} → ¢{cascade.FinalCreditCost}.");
            if (!Mathf.Approximately(cascade.BaseSoulCost, cascade.FinalSoulCost))
                notices.Add($"Desk supplies change Soul cost {cascade.BaseSoulCost:0.##} → {cascade.FinalSoulCost:0.##}.");
        }

        private void PublishAppliedCard(
            PunchCardData card,
            string cardInstanceId,
            string clientId,
            CardSlamOutcome outcome,
            string failureReason,
            ClientStateID? stateBefore,
            ClientStateID? stateAfter,
            int creditsBefore,
            float sanityBefore,
            float soulBefore,
            int requiredCredits,
            int fatigueBefore,
            SynergyResolutionPacket cascade,
            bool hasCascade,
            string clientEffect = null,
            float clientEffectDuration = 0f,
            string blockingModifierId = null)
        {
            var run = GameManager.Instance?.Run;
            var applied = new AppliedCardResolution(
                card.CardType,
                cardInstanceId,
                clientId,
                outcome,
                failureReason,
                stateBefore,
                stateAfter,
                (run?.Credits ?? creditsBefore) - creditsBefore,
                (run?.Sanity ?? sanityBefore) - sanityBefore,
                (run?.SoulIntegrity ?? soulBefore) - soulBefore,
                requiredCredits,
                fatigueBefore,
                _fatigue?.GetFatigue(cardInstanceId) ?? fatigueBefore,
                cascade,
                hasCascade,
                clientEffect,
                clientEffectDuration,
                blockingModifierId);
            RumorMill.Publish(new CardSlammedEvent(applied));
        }

        private static CardSlamOutcome MapToEventOutcome(SlamOutcome outcome)
        {
            return outcome switch
            {
                SlamOutcome.BlockedByPreFiledExemption => CardSlamOutcome.BlockedByExemption,
                SlamOutcome.ClientNotResponding        => CardSlamOutcome.ClientNotResponding,
                SlamOutcome.InsufficientCredits        => CardSlamOutcome.InsufficientCredits,
                SlamOutcome.CardJammed                 => CardSlamOutcome.CardJammed,
                SlamOutcome.CardCrumpled               => CardSlamOutcome.CardCrumpled,
                SlamOutcome.NoActiveClient             => CardSlamOutcome.NoActiveClient,
                SlamOutcome.InvalidCard                => CardSlamOutcome.InvalidCard,
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
