// ============================================================
// DESK 42 — Feedback Budget
//
// Static throttle that prevents the screen from screaming with
// every visual feedback channel firing at once. Each channel
// asks RequestBurst(kind) before playing its effect; only one
// "loud" burst is allowed per channel within its cooldown
// window, and only one burst across ALL channels per global
// window.
//
// Channels (loosest to loudest):
//   Toast       — text pop above HUD (NotificationFeed, ShiftFeedbackOverlay)
//   Flash       — full-canvas tint pulse (CardSlamFeedback, hazard flash)
//   Shake       — camera/RT jitter (CardSlamFeedback shake)
//   Particle    — long-lived particle burst (DeskEntropyRenderer, hazards)
//   Distortion  — sustained screen warp (FormCorruptionEffect, BinauralStress)
//
// "Quiet" channels (always allowed): Audio, LogLine, DataChange.
// Those don't go through this budget at all.
//
// Cooldowns are intentional and small. The goal is "don't
// trip three flashes in the same frame on the same event"
// rather than gating spaced-out feedback.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace Desk42.UI
{
    public enum FeedbackKind
    {
        Toast,
        Flash,
        Shake,
        Particle,
        Distortion,
    }

    public static class FeedbackBudget
    {
        // Per-channel cooldowns (seconds).
        private static readonly Dictionary<FeedbackKind, float> _cooldowns = new()
        {
            { FeedbackKind.Toast,      0.20f },
            { FeedbackKind.Flash,      0.35f },
            { FeedbackKind.Shake,      0.50f },
            { FeedbackKind.Particle,   0.40f },
            { FeedbackKind.Distortion, 0.60f },
        };

        // Global "no more than one loud thing per N seconds" gate.
        // Toast doesn't count toward this — it's the quietest channel.
        private const float GlobalLoudCooldown = 0.15f;

        private static readonly Dictionary<FeedbackKind, float> _lastFiredAt = new();
        private static float _lastLoudFiredAt = -999f;

        /// <summary>
        /// Returns true if the channel may fire its effect now.
        /// On true, the cooldown for that channel (and the global
        /// loud window) is updated automatically — callers don't
        /// need to mark anything.
        /// </summary>
        public static bool RequestBurst(FeedbackKind kind)
        {
            float now = Time.unscaledTime;

            // Per-channel gate
            if (_lastFiredAt.TryGetValue(kind, out float last))
            {
                float cd = _cooldowns.TryGetValue(kind, out float c) ? c : 0.25f;
                if (now - last < cd) return false;
            }

            // Global loud gate (Toast is exempt)
            if (kind != FeedbackKind.Toast)
            {
                if (now - _lastLoudFiredAt < GlobalLoudCooldown) return false;
                _lastLoudFiredAt = now;
            }

            _lastFiredAt[kind] = now;
            return true;
        }

        /// <summary>For tests / scene reload. Clears all timers.</summary>
        public static void ResetAll()
        {
            _lastFiredAt.Clear();
            _lastLoudFiredAt = -999f;
        }
    }
}
