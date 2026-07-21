// ============================================================
// DESK 42 — Personal Expense Generator
//
// Fires between shifts during ClockOut to drain Corporate Credits.
// Generates 2-4 expenses (Rent, Food, Medical, Transit) that
// scale with GlobalShiftNumber. If the player can't afford an
// expense, publishes ExpenseUnmetEvent so RunStateController
// can accumulate PersonalExpenseDebt and add desk entropy.
//
// Called by GameManager.EndShift() before CompleteRun().
// ============================================================

using UnityEngine;

namespace Desk42.Core
{
    public static class PersonalExpenseGenerator
    {
        // ── Expense Definitions ──────────────────────────────

        private static readonly ExpenseDef[] _allExpenses =
        {
            new("rent",    "Rent",           15, 3),   // base 15, +3 per global shift
            new("food",    "Food & Coffee",   8, 2),
            new("medical", "Medical Copay",  10, 2),
            new("transit", "Transit Pass",    5, 1),
        };

        // ── Public API ───────────────────────────────────────

        /// <summary>
        /// Generate and apply personal expenses for the current shift.
        /// Call during ClockOut, before CompleteRun().
        /// </summary>
        public static void ProcessEndOfShiftExpenses(RunData runData, MetaProgressData meta)
        {
            if (runData == null || meta == null) return;

            int globalShift = meta.GlobalShiftNumber;

            // Pick 2-4 expenses deterministically
            int count = SeedEngine.Next(SeedStream.PersonalExpenses, 2, 5); // [2, 4]

            // Shuffle to vary which expenses appear
            int[] indices = new int[_allExpenses.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            SeedEngine.Shuffle(SeedStream.PersonalExpenses, indices);

            for (int i = 0; i < count && i < indices.Length; i++)
            {
                var def = _allExpenses[indices[i]];
                int cost = def.BaseCost + def.ScalePerShift * globalShift;

                if (runData.CorporateCredits >= cost)
                {
                    runData.CorporateCredits -= cost;
                    Debug.Log($"[PersonalExpenses] Paid {def.Label}: ¢{cost}. " +
                              $"Remaining: ¢{runData.CorporateCredits}.");
                }
                else
                {
                    int amountShort = cost - runData.CorporateCredits;
                    runData.CorporateCredits = 0;

                    // Accumulate debt directly — the deferred event won't drain
                    // before CompleteRun() computes EfficiencyRating.
                    runData.PersonalExpenseDebt += amountShort;

                    // Still publish for side-effects (entropy, logging, listeners).
                    RumorMill.PublishDeferred(new ExpenseUnmetEvent(def.Id, amountShort));
                    Debug.Log($"[PersonalExpenses] Cannot afford {def.Label}: " +
                              $"¢{cost} (short ¢{amountShort}). Debt accumulating.");
                }
            }
        }

        // ── Internal ─────────────────────────────────────────

        private readonly struct ExpenseDef
        {
            public readonly string Id;
            public readonly string Label;
            public readonly int    BaseCost;
            public readonly int    ScalePerShift;

            public ExpenseDef(string id, string label, int baseCost, int scalePerShift)
            {
                Id            = id;
                Label         = label;
                BaseCost      = baseCost;
                ScalePerShift = scalePerShift;
            }
        }
    }
}

