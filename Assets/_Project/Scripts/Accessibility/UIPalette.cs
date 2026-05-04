// ============================================================
// DESK 42 — UI Palette
//
// Named color tokens for runtime UI. Each token returns the
// normal-mode value or a high-contrast alternative based on
// AccessibilitySettings.HighContrast.
//
// Usage in panel builders:
//   img.color = UIPalette.CardBackground;
//   tmp.color = UIPalette.AccentTitle;
//
// Colors are read-on-call. Panels that build at runtime pick
// up new values on next rebuild (toggle High Contrast → reopen
// the panel). Persistent HUD picks up on next scene load.
// ============================================================

using UnityEngine;

namespace Desk42.Accessibility
{
    public static class UIPalette
    {
        // ── Backdrops & Card Surfaces ─────────────────────────

        /// <summary>Modal/overlay backdrop. Higher alpha in HC for
        /// stronger separation from gameplay underneath.</summary>
        public static Color Backdrop =>
            AccessibilitySettings.HighContrast
                ? new Color(0f, 0f, 0f, 0.95f)
                : new Color(0f, 0f, 0f, 0.78f);

        /// <summary>Card / panel body fill.</summary>
        public static Color CardBackground =>
            AccessibilitySettings.HighContrast
                ? new Color(0f, 0f, 0f, 1f)
                : new Color(0.10f, 0.10f, 0.12f, 0.98f);

        // ── Text ──────────────────────────────────────────────

        public static Color TextPrimary =>
            AccessibilitySettings.HighContrast
                ? Color.white
                : Color.white;

        /// <summary>Subdued body/description text.</summary>
        public static Color TextSecondary =>
            AccessibilitySettings.HighContrast
                ? Color.white
                : new Color(0.75f, 0.75f, 0.78f, 1f);

        // ── Accents ───────────────────────────────────────────

        /// <summary>Headers, titles, primary highlight (yellow tone).</summary>
        public static Color AccentTitle =>
            AccessibilitySettings.HighContrast
                ? new Color(1f, 1f, 0f, 1f)
                : new Color(0.95f, 0.85f, 0.50f, 1f);

        /// <summary>Warning / danger / unethical-choice tone.</summary>
        public static Color AccentWarn =>
            AccessibilitySettings.HighContrast
                ? new Color(1f, 0.4f, 0.4f, 1f)
                : new Color(0.85f, 0.30f, 0.50f, 1f);

        /// <summary>Success / ethical-choice / approve tone.</summary>
        public static Color AccentSuccess =>
            AccessibilitySettings.HighContrast
                ? new Color(0f, 1f, 0.4f, 1f)
                : new Color(0.20f, 0.50f, 0.30f, 1f);

        // ── Buttons ───────────────────────────────────────────

        public static Color ButtonNormal =>
            AccessibilitySettings.HighContrast
                ? new Color(0.05f, 0.05f, 0.05f, 1f)
                : new Color(0.20f, 0.20f, 0.25f, 1f);

        public static Color ButtonHighlighted =>
            AccessibilitySettings.HighContrast
                ? new Color(1f, 1f, 1f, 0.5f)
                : new Color(0.35f, 0.35f, 0.40f, 1f);
    }
}
