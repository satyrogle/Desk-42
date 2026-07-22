// ============================================================
// DESK 42 — Personal Obligation Lifecycle
//
// Generates one concrete, deterministic list at shift start. The list is
// serialized into RunData, shown during the shift, and applied exactly once
// at clock-out. Resume restores rows; it never rolls the stream again.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Desk42.Core
{
    public static class PersonalExpenseGenerator
    {
        private static readonly ExpenseDef[] AllExpenses =
        {
            new("rent",    "Rent",           15, 3),
            new("food",    "Food & Coffee",   8, 2),
            new("medical", "Medical Copay",  10, 2),
            new("transit", "Transit Pass",    5, 1),
        };

        /// <summary>
        /// Generates and stores the shift's concrete obligations. Repeated calls
        /// for the same shift are a no-op so callers cannot reroll the bill.
        /// </summary>
        public static IReadOnlyList<PersonalObligationData> GenerateForShift(
            RunData runData, MetaProgressData meta)
        {
            if (runData == null) return System.Array.Empty<PersonalObligationData>();

            runData.PersonalObligations ??= new List<PersonalObligationData>();
            int shift = runData.ShiftNumber > 0
                ? runData.ShiftNumber
                : Mathf.Max(1, meta?.GlobalShiftNumber ?? 1);

            if (runData.ObligationsShiftNumber == shift
                && (runData.PersonalObligations.Count > 0 || runData.ObligationsApplied))
            {
                return runData.PersonalObligations;
            }

            runData.PersonalObligations.Clear();
            runData.ObligationsShiftNumber = shift;
            runData.ObligationsApplied = false;

            int count = SeedEngine.Next(SeedStream.PersonalExpenses, 2, 5);
            int[] indices = new int[AllExpenses.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            SeedEngine.Shuffle(SeedStream.PersonalExpenses, indices);

            for (int i = 0; i < count && i < indices.Length; i++)
            {
                var definition = AllExpenses[indices[i]];
                runData.PersonalObligations.Add(new PersonalObligationData
                {
                    Id = definition.Id,
                    Label = definition.Label,
                    Amount = definition.BaseCost + definition.ScalePerShift * shift,
                });
            }

            return runData.PersonalObligations;
        }

        /// <summary>
        /// Applies the already-generated ledger exactly once. Per-row markers
        /// also make a partially applied serialized ledger safe to resume.
        /// </summary>
        public static IReadOnlyList<PersonalObligationData> ProcessEndOfShiftExpenses(
            RunData runData)
        {
            if (runData == null) return System.Array.Empty<PersonalObligationData>();

            runData.PersonalObligations ??= new List<PersonalObligationData>();
            if (runData.ObligationsApplied) return runData.PersonalObligations;

            if (runData.PersonalObligations.Count == 0)
            {
                Debug.LogWarning(
                    "[PersonalExpenses] No generated obligation ledger; clock-out made no charge.");
                return runData.PersonalObligations;
            }

            foreach (var obligation in runData.PersonalObligations)
            {
                if (obligation == null || obligation.Applied) continue;

                int amount = Mathf.Max(0, obligation.Amount);
                int paid = Mathf.Min(runData.CorporateCredits, amount);
                int amountShort = amount - paid;

                runData.CorporateCredits -= paid;
                runData.PersonalExpenseDebt += amountShort;
                obligation.AmountPaid = paid;
                obligation.AmountShort = amountShort;
                obligation.Applied = true;

                if (amountShort > 0)
                {
                    RumorMill.PublishDeferred(
                        new ExpenseUnmetEvent(obligation.Id, amountShort));
                    Debug.Log($"[PersonalExpenses] {obligation.Label}: paid ¢{paid}, " +
                              $"short ¢{amountShort}. Debt accumulating.");
                }
                else
                {
                    Debug.Log($"[PersonalExpenses] Paid {obligation.Label}: ¢{paid}. " +
                              $"Remaining: ¢{runData.CorporateCredits}.");
                }
            }

            runData.ObligationsApplied = true;
            return runData.PersonalObligations;
        }

        private readonly struct ExpenseDef
        {
            public readonly string Id;
            public readonly string Label;
            public readonly int BaseCost;
            public readonly int ScalePerShift;

            public ExpenseDef(string id, string label, int baseCost, int scalePerShift)
            {
                Id = id;
                Label = label;
                BaseCost = baseCost;
                ScalePerShift = scalePerShift;
            }
        }
    }
}
