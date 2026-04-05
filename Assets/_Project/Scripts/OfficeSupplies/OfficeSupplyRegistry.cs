// ============================================================
// DESK 42 — Office Supply Registry
//
// Single lookup table: supply ID → IOfficeSupplyEffect factory.
// OfficeSupplyManager calls Create() when a supply is placed.
//
// Add new supplies here when implementing Expansion Tier.
// ============================================================

using UnityEngine;

namespace Desk42.OfficeSupplies
{
    public static class OfficeSupplyRegistry
    {
        /// <summary>
        /// Instantiate the effect for a given supply ID.
        /// Returns null (with a warning) for unknown IDs.
        /// </summary>
        public static IOfficeSupplyEffect Create(string supplyId)
        {
            return supplyId switch
            {
                "paperclip"            => new PaperclipEffect(),
                "stapler"              => new StaplerEffect(),
                "coffee_mug"           => new CoffeeMugEffect(),
                "post_it_note"         => new PostItNoteEffect(),
                "red_tape"             => new RedTapeDispenserEffect(),
                "broken_printer"       => new BrokenPrinterEffect(),
                "rubber_stamp"         => new RubberStampEffect(),
                "filing_cabinet"       => new FilingCabinetEffect(),
                "desktop_fan"          => new DesktopFanEffect(),
                "motivational_poster"  => new MotivationalPosterEffect(),
                "shredder"             => new ShredderEffect(),
                "paper_weight"         => new PaperWeightEffect(),
                "inbox_tray"           => new InboxTrayEffect(),
                "desk_lamp"            => new DeskLampEffect(),
                "clock"                => new OfficeClockEffect(),
                _                      => Unknown(supplyId),
            };
        }

        private static IOfficeSupplyEffect Unknown(string id)
        {
            Debug.LogWarning($"[OfficeSupplyRegistry] Unknown supply id: '{id}'.");
            return null;
        }

        /// <summary>All Ship Tier supply IDs, in acquisition-order.</summary>
        public static readonly string[] AllShipTierIds =
        {
            "paperclip",
            "stapler",
            "coffee_mug",
            "post_it_note",
            "paper_weight",
            "inbox_tray",
            "desk_lamp",
            "desktop_fan",
            "rubber_stamp",
            "filing_cabinet",
            "red_tape",
            "motivational_poster",
            "broken_printer",
            "shredder",
            "clock",
        };
    }
}
