// ============================================================
// DESK 42 — Office Supply Manager (MonoBehaviour)
//
// Owns all active supplies on the desk.
// Subscribes to RumorMill events and dispatches them to the
// correct supply instances. Also exposes the SynergyResolver
// API for per-slam modifier queries.
//
// Lives on the GameManager GameObject (one per session).
//
// Desk zone rules:
//   - Each DeskZone holds at most one supply.
//   - Placing a supply in an occupied zone sells the existing one.
//   - Max 5 active supplies at once (one per zone).
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Cards;
using Desk42.Core;
using Desk42.RedTape;

namespace Desk42.OfficeSupplies
{
    [DisallowMultipleComponent]
    public sealed class OfficeSupplyManager : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────

        // Zone → active supply instance (null = empty zone)
        private readonly Dictionary<DeskZone, OfficeSupplyInstance> _zones
            = new(5)
            {
                [DeskZone.Inbox]  = null,
                [DeskZone.Lamp]   = null,
                [DeskZone.Clock]  = null,
                [DeskZone.Tray]   = null,
                [DeskZone.Corner] = null,
            };

        // All supplies currently active (flattened from zones)
        private readonly List<OfficeSupplyInstance> _active = new(5);

        // ── Dependencies (set by GameManager) ─────────────────

        private System.Func<SupplyContext> _ctxFactory;

        /// <summary>SynergyResolver for modifier chain queries (StateInjector).</summary>
        public SynergyResolver Resolver { get; private set; }

        // ── Init ──────────────────────────────────────────────

        public void Initialize(System.Func<SupplyContext> contextFactory)
        {
            _ctxFactory = contextFactory;
            Resolver    = new SynergyResolver(this);
            SubscribeToRumorMill();
        }

        private void OnDestroy()
        {
            UnsubscribeFromRumorMill();
        }

        // ── Queries ───────────────────────────────────────────

        public bool          HasSupplyInZone(DeskZone zone) => _zones[zone] != null;
        public int           ActiveCount                    => _active.Count;
        public IReadOnlyList<OfficeSupplyInstance> AllActive => _active;

        public OfficeSupplyInstance GetSupplyInZone(DeskZone zone)
            => _zones[zone];

        // ── Place / Remove ────────────────────────────────────

        /// <summary>
        /// Place a supply on the desk. If the zone is occupied, the existing
        /// supply is removed first (credits refund handled by shop, not here).
        /// </summary>
        public void PlaceSupply(OfficeSupplyData data)
        {
            var effect   = OfficeSupplyRegistry.Create(data.SupplyId);
            if (effect == null)
            {
                Debug.LogWarning($"[SupplyManager] No effect registered for '{data.SupplyId}'.");
                return;
            }

            var instance = new OfficeSupplyInstance(data, effect);
            var zone     = data.Zone;

            if (_zones[zone] != null)
                RemoveFromZone(zone);

            _zones[zone] = instance;
            _active.Add(instance);

            instance.Place(MakeCtx());
        }

        /// <summary>Remove a supply from its zone (sold or consumed).</summary>
        public void RemoveSupply(OfficeSupplyInstance instance)
        {
            RemoveFromZone(instance.Data.Zone);
        }

        private void RemoveFromZone(DeskZone zone)
        {
            var existing = _zones[zone];
            if (existing == null) return;

            existing.Remove(MakeCtx());
            _zones[zone] = null;
            _active.Remove(existing);
        }

        // ── Save / Restore ────────────────────────────────────

        /// <summary>
        /// Serialize all active supplies into RunData for persistence.
        /// </summary>
        public List<ActiveSupplyData> Serialize()
        {
            var result = new List<ActiveSupplyData>(_active.Count);
            foreach (var inst in _active)
                result.Add(inst.Serialize());
            return result;
        }

        /// <summary>
        /// Restore supplies from saved RunData.
        /// Reconstructs a minimal runtime-only OfficeSupplyData stub from the saved
        /// ZoneId and SupplyId — no SO asset lookup required. OnPlace is NOT called
        /// (the effect was already applied before the save).
        /// </summary>
        public void RestoreFrom(List<ActiveSupplyData> saved)
        {
            foreach (var s in saved)
            {
                if (!System.Enum.TryParse<DeskZone>(s.ZoneId, out var zone))
                {
                    Debug.LogWarning($"[SupplyManager] Unknown zone '{s.ZoneId}' " +
                                     $"for supply '{s.SupplyId}' — skipped.");
                    continue;
                }

                var effect = OfficeSupplyRegistry.Create(s.SupplyId);
                if (effect == null)
                {
                    Debug.LogWarning($"[SupplyManager] No effect registered for '{s.SupplyId}' — skipped.");
                    continue;
                }

                // Build a minimal runtime stub — the SO asset is only needed for
                // shop display and inspector; at runtime only SupplyId and Zone matter.
                var stub = UnityEngine.ScriptableObject.CreateInstance<OfficeSupplyData>();
                stub.name     = s.SupplyId;
                stub.SupplyId = s.SupplyId;
                stub.Zone     = zone;

                var instance = new OfficeSupplyInstance(stub, effect);
                instance.RestoreFrom(s);

                _zones[zone] = instance;
                _active.Add(instance);
            }
        }

        // ── RumorMill Subscription ────────────────────────────

        private void SubscribeToRumorMill()
        {
            RumorMill.OnCardSlammed         += HandleCardSlammed;
            RumorMill.OnStateTransition     += HandleStateTransition;
            RumorMill.OnClaimResolved       += HandleClaimResolved;
            RumorMill.OnOfficeHazard        += HandleHazard;
            RumorMill.OnShiftLifecycle      += HandleShiftLifecycle;
        }

        private void UnsubscribeFromRumorMill()
        {
            RumorMill.OnCardSlammed         -= HandleCardSlammed;
            RumorMill.OnStateTransition     -= HandleStateTransition;
            RumorMill.OnClaimResolved       -= HandleClaimResolved;
            RumorMill.OnOfficeHazard        -= HandleHazard;
            RumorMill.OnShiftLifecycle      -= HandleShiftLifecycle;
        }

        private void HandleCardSlammed(CardSlammedEvent e)
        {
            var ctx = MakeCtx();
            ctx.LastCardType       = e.CardType;
            ctx.LastCardInstanceId = e.CardInstanceId;

            foreach (var inst in _active)
                inst.DispatchCardSlammed(ctx);
        }

        private void HandleStateTransition(StateTransitionEvent e)
        {
            var ctx = MakeCtx();
            ctx.PrevState = e.From;
            ctx.NewState  = e.To;

            foreach (var inst in _active)
                inst.DispatchStateTransition(ctx);
        }

        private void HandleClaimResolved(ClaimResolvedEvent e)
        {
            var ctx = MakeCtx();
            ctx.ClaimWasHumane = !e.ResolvedCorrectly;

            foreach (var inst in _active)
                inst.DispatchClaimResolved(ctx);
        }

        private void HandleHazard(OfficeHazardEvent e)
        {
            var ctx = MakeCtx();
            ctx.HazardType = e.HazardType;

            foreach (var inst in _active)
                inst.DispatchHazard(ctx);
        }

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            if (!e.IsStart) return;
            var ctx = MakeCtx();
            foreach (var inst in _active)
                inst.DispatchShiftStart(ctx);
        }

        // ── Encounter Events (called directly by encounter system) ──

        public void NotifyEncounterStart()
        {
            var ctx = MakeCtx();
            foreach (var inst in _active) inst.DispatchEncounterStart(ctx);
        }

        public void NotifyEncounterEnd()
        {
            var ctx = MakeCtx();
            foreach (var inst in _active) inst.DispatchEncounterEnd(ctx);
        }

        // ── Tick ──────────────────────────────────────────────

        private void Update()
        {
            if (_active.Count == 0) return;
            float dt  = Time.deltaTime;
            var   ctx = MakeCtx();
            foreach (var inst in _active)
                inst.Tick(dt, ctx);
        }

        // ── Context Factory ───────────────────────────────────

        private SupplyContext MakeCtx()
            => _ctxFactory?.Invoke() ?? new SupplyContext();
    }
}
