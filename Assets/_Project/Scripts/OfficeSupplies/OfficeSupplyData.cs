// ============================================================
// DESK 42 — Office Supply Data (ScriptableObject)
//
// Blueprint for one type of office supply (relic).
// Lives as a SO asset; one asset per supply type.
//
// Designer workflow:
//   Create > Desk42 > Supplies > Office Supply
//   Set trigger, acquisition cost, rarity, zone.
//
// Runtime behaviour lives in IOfficeSupplyEffect subclasses
// (one per supply type). The SupplyId field maps this asset
// to the correct effect implementation via OfficeSupplyRegistry.
//
// Zone system: each desk zone holds one supply.
//   INBOX  — productivity (draw, hand size)
//   LAMP   — visibility (reveal, info)
//   CLOCK  — time (impatience, phases)
//   TRAY   — card manipulation
//   CORNER — passive defence / hazard resistance
// ============================================================

using UnityEngine;

namespace Desk42.OfficeSupplies
{
    [CreateAssetMenu(
        menuName = "Desk42/Supplies/Office Supply",
        fileName = "Supply_")]
    public sealed class OfficeSupplyData : ScriptableObject
    {
        // ── Identity ──────────────────────────────────────────

        [Header("Identity")]
        [Tooltip("Stable ID — must match key in OfficeSupplyRegistry.")]
        public string SupplyId;

        public string DisplayName    = "Office Supply";
        public string EffectSummary  = "Does something useful.";

        [TextArea(2, 4)]
        public string FlavourText    = "";

        // ── Acquisition ───────────────────────────────────────

        [Header("Acquisition")]
        public SupplyRarity  Rarity          = SupplyRarity.Common;
        public int           AcquisitionCost = 15;   // credits in shop
        public bool          IsStarterSupply = false; // given free at run start

        // ── Zone ──────────────────────────────────────────────

        [Header("Zone")]
        [Tooltip("Which desk zone this supply occupies. One supply per zone.")]
        public DeskZone Zone = DeskZone.Tray;

        // ── Trigger ───────────────────────────────────────────

        [Header("Trigger")]
        [Tooltip("When does this supply activate? Used for UI tooltip and debug.")]
        public SupplyTrigger PrimaryTrigger = SupplyTrigger.OnCardSlammed;

        // ── Visual / Audio ────────────────────────────────────

        [Header("Presentation")]
        public Sprite SupplyArtwork;
        public string PickupSoundKey = "sfx_supply_place";

        // ── Validation ────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(SupplyId))
                SupplyId = name.ToLowerInvariant().Replace(" ", "_");
        }
#endif
    }

    // ── Enums ─────────────────────────────────────────────────

    public enum SupplyRarity
    {
        Common,
        Uncommon,
        Rare,
        Cursed,
    }

    public enum DeskZone
    {
        Inbox,   // draw / hand productivity
        Lamp,    // information / reveal
        Clock,   // time manipulation
        Tray,    // card manipulation
        Corner,  // defence / hazard resistance
    }

    public enum SupplyTrigger
    {
        Passive,             // active every tick
        OnCardSlammed,       // fires after each successful slam
        OnStateTransition,   // fires when client BSM changes state
        OnClaimResolved,     // fires when a claim is stamped
        OnHazard,            // fires when an office hazard triggers
        OnEncounterStart,    // fires when a new client sits down
        OnEncounterEnd,      // fires when a client leaves
        OnShiftStart,        // fires once when shift begins
        OnDraw,              // fires when cards are drawn
    }
}
