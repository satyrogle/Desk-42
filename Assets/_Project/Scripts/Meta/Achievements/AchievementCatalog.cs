// ============================================================
// DESK 42 — Achievement Catalog
//
// Single source of truth for achievement ids. These strings
// must match the Steamworks dashboard entries exactly.
//
// Grouped by what triggers them so AchievementBootstrap can
// fan out from RumorMill events without scattering literals.
// ============================================================

namespace Desk42.Meta.Achievements
{
    public static class AchievementCatalog
    {
        // ── First-time milestones ─────────────────────────────
        public const string FirstShiftCompleted   = "ACH_FIRST_SHIFT";
        public const string FirstClaimResolved    = "ACH_FIRST_CLAIM";
        public const string FirstNDASigned        = "ACH_FIRST_NDA";
        public const string FirstFugueSurvived    = "ACH_FIRST_FUGUE";

        // ── Endings ───────────────────────────────────────────
        public const string EndingTheCompanyMan   = "ACH_ENDING_COMPANY_MAN";
        public const string EndingTheWhistleblower = "ACH_ENDING_WHISTLEBLOWER";
        public const string EndingTheGreatAudit   = "ACH_ENDING_GREAT_AUDIT";
        public const string EndingRedactedTruth   = "ACH_ENDING_REDACTED_TRUTH";
        public const string EndingTheBreach       = "ACH_ENDING_THE_BREACH";
        public const string EndingTaskFailedSuccessfully = "ACH_ENDING_TASK_FAILED_SUCCESSFULLY";

        // ── Mastery ───────────────────────────────────────────
        public const string ShiftPerfectSoul      = "ACH_SHIFT_PERFECT_SOUL";   // shift completed at 100% soul
        public const string ShiftZeroNDA          = "ACH_SHIFT_ZERO_NDA";       // shift completed without signing any NDA
        public const string Shift10Claims         = "ACH_SHIFT_10_CLAIMS";      // 10+ claims processed in one shift
        public const string Shift25Claims         = "ACH_SHIFT_25_CLAIMS";      // 25+ claims in one shift
        public const string TidePeak              = "ACH_TIDE_PEAK";            // reached tide level 3

        // ── Cumulative ────────────────────────────────────────
        public const string Career10Shifts        = "ACH_CAREER_10";
        public const string Career50Shifts        = "ACH_CAREER_50";
        public const string Career100Shifts       = "ACH_CAREER_100";
    }
}
