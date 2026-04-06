// ============================================================
// DESK 42 — Synergy Resolver
//
// Applies all active office supply modifier chains to a card
// before it is injected. Called by StateInjector during
// ComputeDurationOverride and credit cost calculation.
//
// Chain model — Duration:
//   Applied in zone order (Inbox→Lamp→Clock→Tray→Corner),
//   each supply sees the running duration so a Paperclip
//   doubling + a flat bonus compose naturally.
//
// Chain model — Credit Cost (additive-only, no cascade):
//   Every supply is called with the ORIGINAL base cost, not
//   the accumulated running total. Each supply's contribution
//   is (returnedCost − baseCost). All deltas are summed once
//   and applied to baseCost. This prevents multiplicative
//   stacking: two supplies that each halve the cost cannot
//   compound to a quarter — they combine to a full half-off.
//
// Design rationale: zone-ordered application gives the player
// a mental model for how supplies interact. The additive-only
// credit rule prevents runaway cost reduction in expansion tier
// while keeping each supply's stated effect legible.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Cards;
using Desk42.Core;

namespace Desk42.OfficeSupplies
{
    public sealed class SynergyResolver
    {
        private readonly OfficeSupplyManager _manager;

        // Zone application order: deterministic left-to-right
        private static readonly DeskZone[] ZONE_ORDER =
        {
            DeskZone.Inbox,
            DeskZone.Lamp,
            DeskZone.Clock,
            DeskZone.Tray,
            DeskZone.Corner,
        };

        public SynergyResolver(OfficeSupplyManager manager)
        {
            _manager = manager;
        }

        // ── Duration Modifier Chain ───────────────────────────

        /// <summary>
        /// Compute the final injection duration after all active supply modifiers.
        /// Call order: archetype multiplier → supply chain.
        /// </summary>
        public float ApplyDurationModifiers(PunchCardType cardType,
            float baseDuration, IReadOnlyList<string> cardTags = null)
        {
            if (_manager == null || _manager.ActiveCount == 0)
                return baseDuration;

            float duration = baseDuration;

            // Apply each active supply in zone order
            foreach (var zone in ZONE_ORDER)
            {
                var inst = _manager.GetSupplyInZone(zone);
                if (inst == null) continue;

                float prev = duration;
                duration   = inst.Effect.ModifyInjectionDuration(cardType, duration, cardTags);

                if (!Mathf.Approximately(prev, duration))
                    Debug.Log($"[SynergyResolver] {inst.Data.DisplayName}: " +
                              $"duration {prev:F2}s → {duration:F2}s ({cardType})");
            }

            return Mathf.Max(0f, duration);
        }

        // ── Credit Cost Modifier Chain ────────────────────────

        /// <summary>
        /// Compute the final credit cost after all active supply modifiers.
        /// Additive-only: each supply's delta is computed against baseCost
        /// independently, then all deltas are summed. Multiplicative effects
        /// from multiple supplies cannot compound. Returns at minimum 0.
        /// </summary>
        public int ApplyCreditCostModifiers(PunchCardType cardType, int baseCost)
        {
            if (_manager == null || _manager.ActiveCount == 0)
                return baseCost;

            int totalDelta = 0;

            foreach (var zone in ZONE_ORDER)
            {
                var inst = _manager.GetSupplyInZone(zone);
                if (inst == null) continue;

                // Pass baseCost to every supply so deltas are independent
                int result = inst.Effect.ModifyCreditCost(cardType, baseCost);
                int delta  = result - baseCost;

                if (delta != 0)
                {
                    totalDelta += delta;
                    Debug.Log($"[SynergyResolver] {inst.Data.DisplayName}: " +
                              $"cost delta {delta:+0;-0} ({cardType})");
                }
            }

            return Mathf.Max(0, baseCost + totalDelta);
        }

        // ── Bonus Draw Query ──────────────────────────────────

        /// <summary>
        /// Sum of extra cards to draw this turn from all active supplies.
        /// Consumed on query — call once per draw phase.
        /// </summary>
        public int ConsumeDrawBonus()
        {
            int bonus = 0;
            foreach (var inst in _manager.AllActive)
            {
                // Supplies that grant draw bonuses write to RuntimeState["draw_bonus"]
                if (inst.RuntimeState.TryGetValue("draw_bonus", out float b) && b > 0f)
                {
                    bonus += (int)b;
                    inst.RuntimeState["draw_bonus"] = 0f; // consume
                }
            }
            return bonus;
        }

        // ── Free Card Query ───────────────────────────────────

        /// <summary>
        /// Returns true if any supply has marked the next card as free.
        /// Consuming clears the flag.
        /// </summary>
        public bool ConsumeNextCardFree()
        {
            foreach (var inst in _manager.AllActive)
            {
                if (inst.RuntimeState.TryGetValue("next_card_free", out float v) && v > 0f)
                {
                    inst.RuntimeState["next_card_free"] = 0f;
                    return true;
                }
            }
            return false;
        }

        // ── Timer Modifier Queries ────────────────────────────

        /// <summary>
        /// Returns the timer speed multiplier from the Clock zone supply.
        /// 1.0 = normal speed; 0.9 = 10% slower (Office Clock effect).
        /// </summary>
        public float GetTimerMultiplier()
        {
            var inst = _manager.GetSupplyInZone(DeskZone.Clock);
            if (inst?.Effect is OfficeClockEffect clock)
                return clock.GetTimerMultiplier();
            return 1f;
        }

        /// <summary>
        /// If the Office Clock's once-per-shift grace period is available,
        /// consume it (extends timer by 60s) and return true.
        /// Called by TickTimer when the countdown would otherwise reach zero.
        /// </summary>
        public bool TryConsumeClockGracePeriod(SupplyContext ctx)
        {
            var inst = _manager.GetSupplyInZone(DeskZone.Clock);
            if (inst?.Effect is OfficeClockEffect clock)
                return clock.TryConsumeGracePeriod(ctx);
            return false;
        }

        // ── Soul Cost Modifier Chain ──────────────────────────

        /// <summary>
        /// Compute the final soul cost after all active supply modifiers (zone order).
        /// Returns at minimum 0.
        /// </summary>
        public float ApplySoulCostModifiers(float baseSoulCost)
        {
            if (_manager == null || _manager.ActiveCount == 0)
                return baseSoulCost;

            float cost = baseSoulCost;

            foreach (var zone in ZONE_ORDER)
            {
                var inst = _manager.GetSupplyInZone(zone);
                if (inst == null) continue;

                float prev = cost;
                cost = inst.Effect.ModifySoulCost(cost);

                if (!Mathf.Approximately(prev, cost))
                    Debug.Log($"[SynergyResolver] {inst.Data.DisplayName}: " +
                              $"soul cost {prev:F2} → {cost:F2}");
            }

            return Mathf.Max(0f, cost);
        }
    }
}
