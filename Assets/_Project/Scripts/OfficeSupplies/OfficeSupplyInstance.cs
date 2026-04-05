// ============================================================
// DESK 42 — Office Supply Instance
//
// Runtime binding of a supply's data asset (SO) and its
// behavioural implementation (IOfficeSupplyEffect).
//
// OfficeSupplyManager owns a list of these. Each instance
// tracks how many times it has triggered this shift and
// any persistent runtime state (for save/restore).
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Desk42.OfficeSupplies
{
    public sealed class OfficeSupplyInstance
    {
        // ── Data ──────────────────────────────────────────────

        public readonly OfficeSupplyData      Data;
        public readonly IOfficeSupplyEffect   Effect;

        // ── Mutable State ─────────────────────────────────────

        public int  TriggerCount    { get; private set; }
        public int  EvolutionLevel  { get; private set; }  // future: evolution threshold
        public bool IsActive        { get; private set; } = true;

        /// <summary>Arbitrary per-supply persistence bag (survives save/load).</summary>
        public Dictionary<string, float> RuntimeState { get; } = new();

        // ── Init ──────────────────────────────────────────────

        public OfficeSupplyInstance(OfficeSupplyData data, IOfficeSupplyEffect effect)
        {
            Data   = data;
            Effect = effect;
        }

        // ── Lifecycle ─────────────────────────────────────────

        public void Place(SupplyContext ctx)
        {
            IsActive = true;
            Effect.OnPlace(ctx);
            Debug.Log($"[Supply] Placed: {Data.DisplayName}");
        }

        public void Remove(SupplyContext ctx)
        {
            IsActive = false;
            Effect.OnRemove(ctx);
            Debug.Log($"[Supply] Removed: {Data.DisplayName}");
        }

        // ── Event Dispatch ────────────────────────────────────

        public void DispatchCardSlammed(SupplyContext ctx)
        {
            if (!IsActive) return;
            ctx.TriggerCount = TriggerCount;
            Effect.OnCardSlammed(ctx);
            TriggerCount++;
        }

        public void DispatchStateTransition(SupplyContext ctx)
        {
            if (!IsActive) return;
            ctx.TriggerCount = TriggerCount;
            Effect.OnStateTransition(ctx);
        }

        public void DispatchClaimResolved(SupplyContext ctx)
        {
            if (!IsActive) return;
            ctx.TriggerCount = TriggerCount;
            Effect.OnClaimResolved(ctx);
            TriggerCount++;
        }

        public void DispatchHazard(SupplyContext ctx)
        {
            if (!IsActive) return;
            ctx.TriggerCount = TriggerCount;
            Effect.OnHazard(ctx);
        }

        public void DispatchEncounterStart(SupplyContext ctx)
        {
            if (!IsActive) return;
            Effect.OnEncounterStart(ctx);
        }

        public void DispatchEncounterEnd(SupplyContext ctx)
        {
            if (!IsActive) return;
            Effect.OnEncounterEnd(ctx);
        }

        public void DispatchShiftStart(SupplyContext ctx)
        {
            if (!IsActive) return;
            TriggerCount = 0; // reset per-shift
            Effect.OnShiftStart(ctx);
        }

        public void Tick(float dt, SupplyContext ctx)
        {
            if (!IsActive) return;
            Effect.Tick(dt, ctx);
        }

        // ── Save / Restore ────────────────────────────────────

        public Core.ActiveSupplyData Serialize() => new()
        {
            SupplyId       = Data.SupplyId,
            ZoneId         = Data.Zone.ToString(),
            EvolutionLevel = EvolutionLevel,
            TriggerCount   = TriggerCount,
            RuntimeState   = new Dictionary<string, float>(RuntimeState),
        };

        public void RestoreFrom(Core.ActiveSupplyData saved)
        {
            EvolutionLevel = saved.EvolutionLevel;
            TriggerCount   = saved.TriggerCount;
            RuntimeState.Clear();
            if (saved.RuntimeState != null)
                foreach (var kv in saved.RuntimeState)
                    RuntimeState[kv.Key] = kv.Value;
        }
    }
}
