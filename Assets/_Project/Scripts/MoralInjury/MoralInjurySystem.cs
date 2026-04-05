// ============================================================
// DESK 42 — Moral Injury System
//
// Tracks the cumulative moral weight of every unethical act
// the player commits across a run. Soul integrity is the gauge;
// moral injury is the mechanism that drains it.
//
// Design:
//   Each UnethicalActionType has a base soul cost and an
//   escalation multiplier: repeat offences of the same type
//   cost progressively MORE soul (the player knows what they
//   are doing the second time).
//
//   Injury Scars: once a category's total cost crosses a
//   threshold, a permanent "scar" applies — a persistent soul
//   cap reduction that persists even after the shift ends.
//   Scars are stored on MetaProgressData.
//
//   Fugue State trigger: soul reaches 0. All card injection
//   fails; the player must wait for the auto-recovery tick.
//   If the player has scars, the effective max soul is lower,
//   making future Fugue States easier to trigger.
//
// ── Thresholds ───────────────────────────────────────────
//   Scar 1 (Callous):     category total ≥ 15 soul
//   Scar 2 (Complicit):   category total ≥ 35 soul
//   Scar 3 (Irredeemable): category total ≥ 60 soul
//
// Soul drain is published via RumorMill so RunStateController
// and the NarratorSystem both react.
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;

namespace Desk42.MoralInjury
{
    // ── Scar Level ────────────────────────────────────────────

    public enum ScarLevel { None, Callous, Complicit, Irredeemable }

    // ── Injury Category Record ────────────────────────────────

    public sealed class InjuryCategoryRecord
    {
        public UnethicalActionType ActionType;
        public int                 Count;        // how many times committed
        public float               TotalSoul;    // cumulative soul cost from this category
        public ScarLevel           Scar;
    }

    // ── Soul Cost Table ───────────────────────────────────────

    public static class InjuryCostTable
    {
        // Base soul cost per first offence
        public static readonly Dictionary<UnethicalActionType, float> BaseCost = new()
        {
            [UnethicalActionType.EmpathyDenied]        = 3f,
            [UnethicalActionType.ExcessiveNDA]          = 4f,
            [UnethicalActionType.FalseReport]           = 6f,
            [UnethicalActionType.TaxFraud]              = 8f,
            [UnethicalActionType.EvidenceDestroyed]     = 10f,
            [UnethicalActionType.WrongfulTermination]   = 15f,
        };

        // Escalation multiplier per subsequent offence (compound)
        public static readonly Dictionary<UnethicalActionType, float> Escalation = new()
        {
            [UnethicalActionType.EmpathyDenied]        = 1.10f,
            [UnethicalActionType.ExcessiveNDA]          = 1.15f,
            [UnethicalActionType.FalseReport]           = 1.20f,
            [UnethicalActionType.TaxFraud]              = 1.25f,
            [UnethicalActionType.EvidenceDestroyed]     = 1.30f,
            [UnethicalActionType.WrongfulTermination]   = 1.50f,
        };

        // Scar thresholds (cumulative soul cost per category)
        public const float SCAR_1_THRESHOLD = 15f;
        public const float SCAR_2_THRESHOLD = 35f;
        public const float SCAR_3_THRESHOLD = 60f;

        // Max soul cap reduction per scar level
        public const float SCAR_1_CAP_REDUCTION = 5f;
        public const float SCAR_2_CAP_REDUCTION = 15f;
        public const float SCAR_3_CAP_REDUCTION = 30f;
    }

    // ── Moral Injury System ───────────────────────────────────

    public sealed class MoralInjurySystem
    {
        // Per-run category records
        private readonly Dictionary<UnethicalActionType, InjuryCategoryRecord> _records = new();

        // Effective soul cap (starts at 100, reduced by scars)
        private float _soulCap = 100f;

        // ── Init ──────────────────────────────────────────────

        public MoralInjurySystem() { }

        /// <summary>
        /// Load existing scars from MetaProgressData at run start
        /// to apply persistent cap reductions from prior shifts.
        /// </summary>
        public void LoadScarsFromMeta(MetaProgressData meta)
        {
            if (meta == null) return;

            float totalCapReduction = 0f;
            foreach (var scarEntry in meta.ActiveScars)
            {
                totalCapReduction += scarEntry.Value switch
                {
                    ScarLevel.Callous      => InjuryCostTable.SCAR_1_CAP_REDUCTION,
                    ScarLevel.Complicit    => InjuryCostTable.SCAR_2_CAP_REDUCTION,
                    ScarLevel.Irredeemable => InjuryCostTable.SCAR_3_CAP_REDUCTION,
                    _                      => 0f,
                };
            }

            _soulCap = Mathf.Max(10f, 100f - totalCapReduction);
            Debug.Log($"[MoralInjurySystem] Soul cap after scar load: {_soulCap}.");
        }

        // ── Queries ───────────────────────────────────────────

        public float EffectiveSoulCap => _soulCap;

        public InjuryCategoryRecord GetRecord(UnethicalActionType type)
        {
            _records.TryGetValue(type, out var rec);
            return rec;
        }

        public ScarLevel GetScar(UnethicalActionType type)
            => GetRecord(type)?.Scar ?? ScarLevel.None;

        /// <summary>The worst scar level across all action categories this run.</summary>
        public ScarLevel HighestScar
        {
            get
            {
                var highest = ScarLevel.None;
                foreach (var rec in _records.Values)
                    if (rec.Scar > highest) highest = rec.Scar;
                return highest;
            }
        }

        // ── Record Unethical Act ──────────────────────────────

        /// <summary>
        /// Register one act of the given type. Computes soul cost with
        /// escalation, checks for new scars, returns the soul cost applied.
        /// </summary>
        public float RecordAct(UnethicalActionType type, string claimId)
        {
            if (!_records.TryGetValue(type, out var rec))
            {
                rec = new InjuryCategoryRecord { ActionType = type };
                _records[type] = rec;
            }

            // Compute escalated cost
            float base_ = InjuryCostTable.BaseCost[type];
            float mult  = InjuryCostTable.Escalation[type];
            float cost  = base_ * Mathf.Pow(mult, rec.Count);
            cost        = Mathf.Round(cost * 10f) / 10f; // 1 decimal place

            rec.Count    += 1;
            rec.TotalSoul += cost;

            // Check scar progression
            ScarLevel prevScar = rec.Scar;
            rec.Scar = rec.TotalSoul switch
            {
                >= InjuryCostTable.SCAR_3_THRESHOLD => ScarLevel.Irredeemable,
                >= InjuryCostTable.SCAR_2_THRESHOLD => ScarLevel.Complicit,
                >= InjuryCostTable.SCAR_1_THRESHOLD => ScarLevel.Callous,
                _                                   => ScarLevel.None,
            };

            if (rec.Scar > prevScar)
                OnNewScar(type, rec.Scar);

            Debug.Log($"[MoralInjurySystem] Act: {type} #{rec.Count}. " +
                      $"Soul cost: {cost:F1}. Category total: {rec.TotalSoul:F1}. " +
                      $"Scar: {rec.Scar}.");

            return cost;
        }

        // ── Scar Applied ──────────────────────────────────────

        private void OnNewScar(UnethicalActionType type, ScarLevel newScar)
        {
            // Reduce the effective soul cap
            float reduction = newScar switch
            {
                ScarLevel.Callous      => InjuryCostTable.SCAR_1_CAP_REDUCTION,
                ScarLevel.Complicit    => InjuryCostTable.SCAR_2_CAP_REDUCTION,
                ScarLevel.Irredeemable => InjuryCostTable.SCAR_3_CAP_REDUCTION,
                _                      => 0f,
            };

            _soulCap = Mathf.Max(10f, _soulCap - reduction);

            RumorMill.PublishDeferred(new MilestoneReachedEvent(
                MilestoneID.RedactedTruth,
                0,
                $"scar_{type}_{newScar}"));

            Debug.Log($"[MoralInjurySystem] New scar: {type} → {newScar}. " +
                      $"Soul cap: {_soulCap}.");
        }

        // ── Persist Scars ─────────────────────────────────────

        /// <summary>
        /// Write newly acquired scars to MetaProgressData before saving.
        /// Only the highest scar per category is stored.
        /// </summary>
        public void PersistScarsToMeta(MetaProgressData meta)
        {
            if (meta == null) return;

            foreach (var kv in _records)
            {
                if (kv.Value.Scar == ScarLevel.None) continue;

                // Store the worst scar per category
                if (!meta.ActiveScars.TryGetValue(kv.Key.ToString(), out ScarLevel existing) ||
                    kv.Value.Scar > existing)
                {
                    meta.ActiveScars[kv.Key.ToString()] = kv.Value.Scar;
                }
            }
        }

        // ── Whistleblower Unlock Tracker ──────────────────────

        /// <summary>
        /// Returns true if conditions for the Whistleblower ending are met.
        /// Requires at least one Irredeemable scar this run AND soul < 10.
        /// </summary>
        public bool IsWhistleblowerConditionMet(float currentSoul)
        {
            if (currentSoul > 10f) return false;
            foreach (var rec in _records.Values)
                if (rec.Scar == ScarLevel.Irredeemable) return true;
            return false;
        }
    }
}
