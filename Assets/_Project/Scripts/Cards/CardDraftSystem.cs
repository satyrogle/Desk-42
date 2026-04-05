// ============================================================
// DESK 42 — Card Draft System
//
// Presents the player with a choice of cards after completing
// a client encounter or reaching a shift milestone.
//
// Draft flow:
//   1. Caller specifies a DraftOffer (context: post-encounter,
//      shop, milestone bonus, archetype pool).
//   2. DraftSystem builds a pool filtered by archetype affinity,
//      rarity weights, and exclusion rules.
//   3. SeedEngine picks N distinct cards from the pool.
//   4. Player picks one (or skips for a credit refund).
//   5. Chosen card added to Deck.
//
// Rarity weights:
//   Common   60%  Base pool for all archetypes
//   Uncommon 30%  Unlocked after Shift 3
//   Rare     9%   Unlocked after Shift 5
//   Cursed   1%   Always available; high risk/reward
//
// Archetype bonus pool: each archetype contributes 3 extra
// cards to the pool, weighted at 3× the rarity weight of their
// natural rarity tier.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;

namespace Desk42.Cards
{
    // ── Enums ─────────────────────────────────────────────────

    public enum CardRarity
    {
        Common,
        Uncommon,
        Rare,
        Cursed,
    }

    // ── Draft Offer ───────────────────────────────────────────

    public sealed class DraftOffer
    {
        public DraftTrigger      Trigger       = DraftTrigger.PostEncounter;
        public string            ArchetypeId   = "";
        public int               ChoiceCount   = 3;     // cards shown to player
        public int               ShiftNumber   = 1;     // gates rarity unlocks
        public bool              CanSkip       = true;
        public int               SkipCreditRefund = 5;
        public HashSet<string>   ExcludeCardIds = new(); // already in deck (avoid duplicates)
    }

    public enum DraftTrigger
    {
        PostEncounter,
        ShiftMilestone,
        ShopPurchase,
        ArchetypeBonus,     // special unlocks, e.g. "Auditor's Briefcase"
        ConspiracyReward,
    }

    // ── Draft Result ──────────────────────────────────────────

    public sealed class DraftResult
    {
        public PunchCardData ChosenCard;   // null if skipped
        public bool          WasSkipped;
        public int           CreditsAwarded; // skip refund if applicable
    }

    // ── Card Draft System ─────────────────────────────────────

    public sealed class CardDraftSystem
    {
        // ── Configuration ─────────────────────────────────────

        // Base rarity weights — index maps to CardRarity enum
        private static readonly float[] BaseWeights =
        {
            60f,  // Common
            30f,  // Uncommon
            9f,   // Rare
            1f,   // Cursed
        };

        // Archetype affinity multiplier applied to archetype-pool cards
        private const float ARCHETYPE_AFFINITY_MULT = 3f;

        // ── State ─────────────────────────────────────────────

        private readonly List<PunchCardData> _masterPool;  // full card library

        // ── Init ──────────────────────────────────────────────

        /// <summary>
        /// Construct with the full card library asset list.
        /// Typically loaded from an AddressableGroup or Resources folder.
        /// </summary>
        public CardDraftSystem(IEnumerable<PunchCardData> masterPool)
        {
            _masterPool = new List<PunchCardData>(masterPool);
        }

        // ── Generate Draft ────────────────────────────────────

        /// <summary>
        /// Build a draft offer and return N cards for the player to choose from.
        /// Returns fewer than ChoiceCount if the pool is too small.
        /// </summary>
        public List<PunchCardData> GenerateDraftChoices(DraftOffer offer)
        {
            var pool    = BuildPool(offer);
            var weights = BuildWeights(pool, offer);

            int count   = Mathf.Min(offer.ChoiceCount, pool.Count);
            var choices = new List<PunchCardData>(count);
            var picked  = new HashSet<int>();

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                // Weighted pick without replacement
                var availableWeights = FilterWeights(weights, picked);
                int idx = SeedEngine.WeightedRandom(
                    SeedStream.CardDraft, availableWeights);

                // Map filtered index back to pool index
                int poolIdx = NthUnpicked(picked, idx);
                picked.Add(poolIdx);
                choices.Add(pool[poolIdx]);
            }

            return choices;
        }

        /// <summary>
        /// Player confirms their pick. Adds card to deck.
        /// Returns a DraftResult for credit/event handling.
        /// </summary>
        public DraftResult ConfirmPick(
            PunchCardData chosenCard,
            DraftOffer    offer,
            Deck          deck)
        {
            if (chosenCard == null)
            {
                // Player skipped
                return new DraftResult
                {
                    WasSkipped     = true,
                    CreditsAwarded = offer.CanSkip ? offer.SkipCreditRefund : 0,
                };
            }

            deck.AddCard(chosenCard);

            Debug.Log($"[DraftSystem] Player picked: {chosenCard.DisplayName}");

            return new DraftResult
            {
                ChosenCard = chosenCard,
                WasSkipped = false,
            };
        }

        // ── Pool Building ─────────────────────────────────────

        private List<PunchCardData> BuildPool(DraftOffer offer)
        {
            var pool = new List<PunchCardData>(_masterPool.Count);

            foreach (var card in _masterPool)
            {
                // Skip excluded cards (duplicates already in deck)
                if (offer.ExcludeCardIds.Contains(card.name)) continue;

                // Gate uncommon/rare by shift number
                if (card.Rarity == CardRarity.Uncommon && offer.ShiftNumber < 3) continue;
                if (card.Rarity == CardRarity.Rare      && offer.ShiftNumber < 5) continue;

                pool.Add(card);
            }

            if (pool.Count == 0)
                Debug.LogWarning("[DraftSystem] Pool is empty after filtering.");

            return pool;
        }

        private static float[] BuildWeights(List<PunchCardData> pool, DraftOffer offer)
        {
            var weights = new float[pool.Count];
            for (int i = 0; i < pool.Count; i++)
            {
                var card   = pool[i];
                float base_w = BaseWeights[(int)card.Rarity];

                // Archetype affinity boost
                bool isAffinity = !string.IsNullOrEmpty(offer.ArchetypeId)
                                  && card.ArchetypeId == offer.ArchetypeId;
                weights[i] = isAffinity
                    ? base_w * ARCHETYPE_AFFINITY_MULT
                    : base_w;
            }
            return weights;
        }

        private static float[] FilterWeights(float[] weights, HashSet<int> picked)
        {
            // Count unpicked
            int count = 0;
            for (int i = 0; i < weights.Length; i++)
                if (!picked.Contains(i)) count++;

            var filtered = new float[count];
            int j = 0;
            for (int i = 0; i < weights.Length; i++)
                if (!picked.Contains(i)) filtered[j++] = weights[i];

            return filtered;
        }

        /// <summary>Returns the pool index of the Nth unpicked entry.</summary>
        private static int NthUnpicked(HashSet<int> picked, int n)
        {
            int count = 0;
            // n is the index within the filtered (unpicked) list
            for (int i = 0; ; i++)
            {
                if (!picked.Contains(i))
                {
                    if (count == n) return i;
                    count++;
                }
            }
        }

        // ── Archetype Bonus Pool ──────────────────────────────

        /// <summary>
        /// Build a draft offer from the archetype's exclusive bonus card pool.
        /// Used for archetype-specific rewards (Auditor's Briefcase, etc.)
        /// </summary>
        public List<PunchCardData> GenerateArchetypeBonusChoices(
            string archetypeId, int shiftNumber, int count = 3)
        {
            return GenerateDraftChoices(new DraftOffer
            {
                Trigger      = DraftTrigger.ArchetypeBonus,
                ArchetypeId  = archetypeId,
                ChoiceCount  = count,
                ShiftNumber  = shiftNumber,
                CanSkip      = false,
            });
        }

        // ── Shop ──────────────────────────────────────────────

        /// <summary>
        /// Build a shop inventory (fixed-price cards per shift).
        /// Uses ShopInventory stream for determinism distinct from draft.
        /// </summary>
        public List<(PunchCardData Card, int Price)> GenerateShopInventory(
            int shiftNumber, string archetypeId, int slots = 4)
        {
            var offer = new DraftOffer
            {
                Trigger     = DraftTrigger.ShopPurchase,
                ArchetypeId = archetypeId,
                ChoiceCount = slots,
                ShiftNumber = shiftNumber,
                CanSkip     = false,
            };

            var pool    = BuildPool(offer);
            var weights = BuildWeights(pool, offer);
            var picked  = new HashSet<int>();
            var result  = new List<(PunchCardData, int)>(slots);

            for (int i = 0; i < slots && pool.Count - picked.Count > 0; i++)
            {
                var available = FilterWeights(weights, picked);
                int idx = SeedEngine.WeightedRandom(SeedStream.ShopInventory, available);
                int poolIdx = NthUnpicked(picked, idx);
                picked.Add(poolIdx);

                var card  = pool[poolIdx];
                int price = ComputeShopPrice(card, shiftNumber);
                result.Add((card, price));
            }

            return result;
        }

        private static int ComputeShopPrice(PunchCardData card, int shiftNumber)
        {
            int basePrice = card.Rarity switch
            {
                CardRarity.Common   => 10,
                CardRarity.Uncommon => 20,
                CardRarity.Rare     => 35,
                CardRarity.Cursed   => 5,   // cursed cards are suspiciously cheap
                _                   => 10,
            };

            // Price scales mildly with shift depth
            float shiftMult = 1f + (shiftNumber - 1) * 0.1f;
            return Mathf.RoundToInt(basePrice * shiftMult);
        }
    }
}
