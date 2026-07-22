// ============================================================
// DESK 42 — Compliance Vow System
//
// Central registry for the 13 new Compliance Vows. Each vow is
// a system-level rule mutation with 3 intensity ranks.
//
// This MonoBehaviour lives as a child of GameManager. It
// subscribes to RumorMill events and queries the active vow
// list each time an event fires, applying the relevant mutation.
//
// Vows that need per-frame hooks (Agile Workflow, Micro-Transactions)
// are driven in Update(). Event-driven vows use RumorMill callbacks.
//
// Vow IDs (match RunData.ActiveVows.VowId):
//   reply_all_chain          cross_training_initiative
//   micro_transactions       sunk_cost_fallacy
//   commission_only          right_to_repair
//   news_cycle_24h           mandatory_mentorship
//   zero_based_budgeting     agile_workflow
//   open_floor_plan          transparency_initiative
//   non_euclidean_filing
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Core
{
    [DisallowMultipleComponent]
    public sealed class ComplianceVowSystem : MonoBehaviour
    {
        // ── Public Query API ──────────────────────────────────
        // Thin wrappers so other systems don't need to parse vow lists.

        /// <summary>Rank of the named vow (0 = inactive).</summary>
        public static int Rank(string vowId)
            => GameManager.Instance?.Run?.GetVowRank(vowId) ?? 0;

        public static bool IsActive(string vowId) => Rank(vowId) > 0;

        // ── Vow 1: Reply-All Chain ───────────────────────────
        // On Legal/Expedite card play, inject useless CC'd Memo into deck.
        // Rank 1 = 1 memo per legal card.
        // Rank 2 = also triggers on Analyse.
        // Rank 3 = 2 memos per trigger.

        private void OnEnable()
        {
            RumorMill.OnCardSlammed += HandleCardSlammed_ReplyAll;
            RumorMill.OnCardSlammed += HandleCardSlammed_CrossTraining;
        }

        private void OnDisable()
        {
            RumorMill.OnCardSlammed -= HandleCardSlammed_ReplyAll;
            RumorMill.OnCardSlammed -= HandleCardSlammed_CrossTraining;
        }

        private void HandleCardSlammed_ReplyAll(CardSlammedEvent e)
        {
            if (!e.Result.IsSuccess) return;

            int rank = Rank("reply_all_chain");
            if (rank <= 0) return;

            bool triggers = e.CardType == PunchCardType.Legal
                         || e.CardType == PunchCardType.Expedite;

            if (rank >= 2)
                triggers = triggers || e.CardType == PunchCardType.Analyse;

            if (!triggers) return;

            int memoCount = rank >= 3 ? 2 : 1;
            var deck = GameManager.Instance?.Run?.RawData?.Deck;
            if (deck == null) return;

            for (int i = 0; i < memoCount; i++)
            {
                deck.DrawPile.Add(new CardInstanceData
                {
                    CardId     = "Card_CCdMemo",
                    InstanceId = System.Guid.NewGuid().ToString("N"),
                    Fatigue    = 0,
                    IsJammed   = false,
                    IsCrumpled = false,
                });
            }

            Debug.Log($"[Vow:ReplyAll] Injected {memoCount} CC'd Memo(s) into deck.");
        }

        // ── Vow 4: Cross-Training Initiative ─────────────────
        // Played cards have a chance to transform into a random card from another archetype.
        // Rank 1 = 15%. Rank 2 = 25%. Rank 3 = 40%.

        private void HandleCardSlammed_CrossTraining(CardSlammedEvent e)
        {
            if (!e.Result.IsSuccess) return;

            int rank = Rank("cross_training_initiative");
            if (rank <= 0) return;

            float chance = rank switch { 1 => 0.15f, 2 => 0.25f, _ => 0.40f };

            if (SeedEngine.NextFloat(SeedStream.CardDraft) >= chance) return;

            // The card is already played — we swap the NEXT card drawn
            // by tagging a flag on RunData that PunchCardMachine reads.
            var run = GameManager.Instance?.Run?.RawData;
            if (run != null)
                run.CrossTrainingSwapPending = true;

            Debug.Log("[Vow:CrossTraining] Next card draw will transform.");
        }

        // ── Vow 7: Micro-Transactions (per-click cost) ──────
        // Handled externally by ClickCostInterceptor MonoBehaviour.
        // This system only exposes the cost query.

        /// <summary>Cost per click in credits (0 if vow inactive).</summary>
        public static int GetClickCost()
        {
            int rank = Rank("micro_transactions");
            return rank switch
            {
                1 => 1,  // card plays only (enforced by interceptor)
                2 => 1,  // all clicks
                3 => 2,
                _ => 0,
            };
        }

        /// <summary>True when ALL clicks cost resources (rank 2+).</summary>
        public static bool AllClicksCost() => Rank("micro_transactions") >= 2;

        // ── Vow 9: 24-Hour News Cycle ────────────────────────
        // All PublishDeferred calls become instant Publish.
        // Rank 1 = faction events instant.
        // Rank 2 = all events instant.
        // Rank 3 = events trigger twice.

        /// <summary>True when the deferred queue should be bypassed.</summary>
        public static bool ShouldBypassDeferredQueue()
            => Rank("news_cycle_24h") >= 2;

        /// <summary>True when faction events should fire immediately.</summary>
        public static bool ShouldBypassFactionDeferred()
            => Rank("news_cycle_24h") >= 1;

        /// <summary>True when events should fire twice.</summary>
        public static bool ShouldDoubleFireEvents()
            => Rank("news_cycle_24h") >= 3;

        // ── Vow 12: Commission Only ──────────────────────────
        // Base payout modified. Queried by EncounterManager.

        /// <summary>Multiplier applied to base credits (before combo).</summary>
        public static float GetBasePayoutMultiplier()
        {
            int rank = Rank("commission_only");
            return rank switch
            {
                1 => 0.5f,
                2 => 0f,
                3 => 0f,
                _ => 1f,
            };
        }

        /// <summary>At rank 3, combo resets every N claims.</summary>
        public static int GetComboResetInterval()
            => Rank("commission_only") >= 3 ? 3 : 0;

        // ── Vow 2: Zero-Based Budgeting ──────────────────────
        // Credits abolished. Costs paid in max sanity.

        /// <summary>Fraction of costs converted to sanity.</summary>
        public static float GetSanityCostFraction()
        {
            int rank = Rank("zero_based_budgeting");
            return rank switch
            {
                1 => 0.5f,
                2 => 1f,
                3 => 1.25f,
                _ => 0f,
            };
        }

        public static bool IsZeroBasedActive() => Rank("zero_based_budgeting") > 0;

        // ── Vow 3: Agile Workflow ────────────────────────────
        // Timer freezes while holding a card, accelerates when released.

        /// <summary>Timer acceleration multiplier when NOT holding a card.</summary>
        public static float GetAgileTimerAcceleration()
        {
            int rank = Rank("agile_workflow");
            return rank switch
            {
                1 => 2f,
                2 => 3f,
                3 => 4f,
                _ => 1f,
            };
        }

        /// <summary>Rank 3: hide remaining time from the UI.</summary>
        public static bool ShouldHideTimer() => Rank("agile_workflow") >= 3;

        /// <summary>True when timer should freeze (card is being held).</summary>
        public static bool ShouldFreezeTimer()
            => Rank("agile_workflow") > 0; // actual check for holding is in caller

        // ── Vow 5: Open Floor Plan ───────────────────────────
        // Zone boundaries erased. Supplies apply everywhere.

        public static bool AreZonesMerged() => Rank("open_floor_plan") >= 1;
        public static bool SuppliesSlideOnSlam() => Rank("open_floor_plan") >= 2;
        public static bool SuppliesCanFallOff() => Rank("open_floor_plan") >= 3;

        // ── Vow 6: Transparency Initiative ───────────────────
        // NDAs disabled. Analyse cards destroy random UI elements.

        public static bool AreNDAsDisabled() => Rank("transparency_initiative") >= 1;

        /// <summary>Which elements can be destroyed at the current rank.</summary>
        public static TransparencyDestructionScope GetDestructionScope()
        {
            int rank = Rank("transparency_initiative");
            return rank switch
            {
                1 => TransparencyDestructionScope.Cosmetic,
                2 => TransparencyDestructionScope.Gauges,
                3 => TransparencyDestructionScope.CardLabels,
                _ => TransparencyDestructionScope.None,
            };
        }

        // ── Vow 8: Mandatory Mentorship ──────────────────────
        // Handled by InternCompanion MonoBehaviour.

        /// <summary>What the intern companion plays from.</summary>
        public static MentorshipSource GetMentorshipSource()
        {
            int rank = Rank("mandatory_mentorship");
            return rank switch
            {
                1 => MentorshipSource.BasicCards,
                2 => MentorshipSource.PlayerDeck,
                3 => MentorshipSource.PlayerDeckDouble,
                _ => MentorshipSource.None,
            };
        }

        // ── Vow 10: Sunk Cost Fallacy ────────────────────────
        // Supplies can't be unequipped. Placing new on old absorbs %.

        public static bool SuppliesLocked() => Rank("sunk_cost_fallacy") >= 1;

        public static float GetAbsorptionRate()
        {
            int rank = Rank("sunk_cost_fallacy");
            return rank switch
            {
                1 => 0.30f,
                2 => 0.50f,
                3 => 0.75f,
                _ => 0f,
            };
        }

        // ── Vow 11: Right to Repair ─────────────────────────
        // Can't heal UI cracks in Meta-Hub. Sacrifice a card to patch.

        public static bool MetaHubRepairDisabled() => Rank("right_to_repair") >= 1;
        public static bool RepairRestoresSanity() => Rank("right_to_repair") >= 2;
        public static bool PatchedCracksGrantBonus() => Rank("right_to_repair") >= 3;

        // ── Vow 13: Non-Euclidean Filing ─────────────────────
        // No deck draw. Entire deck visible, text redacted.

        public static bool IsNonEuclideanActive() => Rank("non_euclidean_filing") >= 1;

        /// <summary>What visual information is hidden.</summary>
        public static NonEuclideanRedactionLevel GetRedactionLevel()
        {
            int rank = Rank("non_euclidean_filing");
            return rank switch
            {
                1 => NonEuclideanRedactionLevel.NamesHidden,
                2 => NonEuclideanRedactionLevel.ArtHidden,
                3 => NonEuclideanRedactionLevel.ShufflePositions,
                _ => NonEuclideanRedactionLevel.None,
            };
        }

        /// <summary>How often card positions shuffle at rank 3.</summary>
        public static int GetPositionShuffleInterval() => 3; // every 3 plays
    }

    // ── Supporting Enums ──────────────────────────────────────

    public enum TransparencyDestructionScope
    {
        None,
        Cosmetic,   // decorative elements only
        Gauges,     // includes timer, sanity bar, etc.
        CardLabels, // includes card text
    }

    public enum MentorshipSource
    {
        None,
        BasicCards,       // mini-deck of generic cards
        PlayerDeck,       // plays from your actual deck
        PlayerDeckDouble, // plays 2 cards from your deck
    }

    public enum NonEuclideanRedactionLevel
    {
        None,
        NamesHidden,
        ArtHidden,
        ShufflePositions,
    }
}

