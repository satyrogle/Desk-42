// ============================================================
// DESK 42 — Retirement Catalog
//
// Catalog of cosmetic items the player can buy with AuditPoints
// in the RetirementLoungePanel. Items are pure cosmetic — they
// flip an unlock flag in MetaProgressData.Cosmetics.
//
// Categories map to the existing CosmeticUnlocks buckets:
//   DeskTheme, Wallpaper, MugSkin, StampDesign,
//   DeskOrnament, SupplySticker.
//
// The "ActiveX" string on Cosmetics is set when a theme/wallpaper
// /mug is equipped (handled by RetirementLoungePanel).
// ============================================================

using System.Collections.Generic;

namespace Desk42.Meta.RetirementFund
{
    public enum CosmeticCategory
    {
        DeskTheme,
        Wallpaper,
        MugSkin,
        DeskOrnament,
        SupplySticker,
    }

    public sealed class CosmeticItem
    {
        public string           Id;
        public string           DisplayName;
        public string           Description;
        public int              Cost;
        public CosmeticCategory Category;

        public CosmeticItem(string id, string name, string desc, int cost,
            CosmeticCategory category)
        {
            Id = id;
            DisplayName = name;
            Description = desc;
            Cost = cost;
            Category = category;
        }
    }

    public static class RetirementCatalog
    {
        public static readonly IReadOnlyList<CosmeticItem> Items = new[]
        {
            new CosmeticItem("desk_oak",      "Oak Desk",
                "A handsome oak finish for your desk. Suggests stability.",
                40,  CosmeticCategory.DeskTheme),
            new CosmeticItem("desk_void",     "Void Desk",
                "Polished obsidian. Stares back when you stare in.",
                120, CosmeticCategory.DeskTheme),

            new CosmeticItem("wall_motivational", "Motivational Poster",
                "\"HANG IN THERE\" — a kitten dangles from a branch. Forever.",
                25,  CosmeticCategory.Wallpaper),
            new CosmeticItem("wall_breach",       "Containment Breach Diagram",
                "Detailed schematic of an incident HR officially denies happened.",
                90,  CosmeticCategory.Wallpaper),

            new CosmeticItem("mug_world_okayest", "World's Okayest Adjuster",
                "Damp the existential dread by 0%. Cosmetic only.",
                15,  CosmeticCategory.MugSkin),
            new CosmeticItem("mug_redacted",      "[REDACTED] Mug",
                "The mug's surface refuses to be photographed.",
                60,  CosmeticCategory.MugSkin),

            new CosmeticItem("orn_stress_ball",   "Stress Ball",
                "Squeezing it releases a faint corporate sigh.",
                30,  CosmeticCategory.DeskOrnament),
            new CosmeticItem("orn_bonsai",        "Compliance Bonsai",
                "Pruned to spell HR slogans depending on angle.",
                75,  CosmeticCategory.DeskOrnament),

            new CosmeticItem("stk_gold_star",     "Gold Star Sticker",
                "Apply to a supply for a small +5 affection rating.",
                20,  CosmeticCategory.SupplySticker),
            new CosmeticItem("stk_void_seal",     "Void-Seal Sticker",
                "Apply to a supply. It is now a problem for the void.",
                110, CosmeticCategory.SupplySticker),
        };
    }
}
