// ============================================================
// DESK 42 — Narrator System
//
// The narrator's voice is unreliable by design. As soul
// integrity drops, the corporate mask slips. Button labels,
// tooltips, status messages, and even error dialogs change
// tone — from Professional management-speak through to
// Doublespeak, where the UI admits what is actually happening.
//
// The system is a two-key lookup:
//   (contextKey, NarratorReliability) → string line
//
// contextKey format: category.item — e.g. "btn.submit_claim",
// "tooltip.soul_gauge", "status.shift_progress".
//
// Lines are hard-coded here for Ship Tier. In Expansion Tier
// they can be moved to a localisation table (SO asset).
// ============================================================

using System.Collections.Generic;
using Desk42.Core;

namespace Desk42.UI
{
    public static class NarratorSystem
    {
        // ── Line Bank ─────────────────────────────────────────
        // Layout: contextKey → [Professional, SlightlyHonest, Honest, Doublespeak]

        private static readonly Dictionary<string, string[]> _lines = new()
        {
            // ── Buttons ────────────────────────────────────────

            ["btn.submit_claim"] = new[]
            {
                "Submit Claim",
                "Submit Claim",
                "Submit Claim (Proceed)",
                "Submit Claim\n(You Have No Choice)",
            },

            ["btn.reject_claim"] = new[]
            {
                "Reject Claim",
                "Reject Claim",
                "Reject Claim (Deny)",
                "Reject Claim\n(Deny Everything)",
            },

            ["btn.next_client"] = new[]
            {
                "Next Client",
                "Next Client",
                "Next. Keep moving.",
                "Next. Don't look back.",
            },

            ["btn.sign_nda"] = new[]
            {
                "Sign NDA",
                "Sign NDA",
                "Sign Away",
                "Sign It.\nYou Always Do.",
            },

            ["btn.use_ability"] = new[]
            {
                "Use Ability",
                "Use Ability",
                "Do It",
                "Use It. You're Desperate.",
            },

            ["btn.skip_draft"] = new[]
            {
                "Skip (Refund)",
                "Skip (Refund)",
                "Pass. Take the money.",
                "Skip.\nThe cards don't matter.",
            },

            ["btn.pause"] = new[]
            {
                "Pause",
                "Pause",
                "Step Away",
                "You Can't Pause This.",
            },

            // ── Tooltips ────────────────────────────────────────

            ["tooltip.soul_gauge"] = new[]
            {
                "Soul Integrity: measures moral resilience.",
                "Soul Integrity: still holding.",
                "Soul Integrity: lower than it should be.",
                "Soul Integrity: this number is a joke.",
            },

            ["tooltip.sanity_gauge"] = new[]
            {
                "Cognitive Budget: mental capacity remaining.",
                "Cognitive Budget: adequate.",
                "Cognitive Budget: diminishing.",
                "Cognitive Budget: what's left of it.",
            },

            ["tooltip.impatience_timer"] = new[]
            {
                "Time Remaining: shift ends at zero.",
                "Time Remaining: keep pace.",
                "Time Remaining: it's running out.",
                "Time Remaining: it always runs out.",
            },

            ["tooltip.credits"] = new[]
            {
                "Corporate Credits: spend in the supply shop.",
                "Corporate Credits: what you've earned.",
                "Corporate Credits: what you've taken.",
                "Corporate Credits: the only thing that matters here.",
            },

            ["tooltip.desk_entropy"] = new[]
            {
                "Desk Condition: optimal.",
                "Desk Condition: manageable.",
                "Desk Condition: deteriorating.",
                "Desk Condition: like the rest of this.",
            },

            ["tooltip.punch_card"] = new[]
            {
                "Punch Card: slam into the machine to inject a client state.",
                "Punch Card: works on most clients.",
                "Punch Card: works whether they like it or not.",
                "Punch Card: a lever. You are the machine.",
            },

            ["tooltip.fatigue"] = new[]
            {
                "Fatigue: repeated use reduces effectiveness.",
                "Fatigue: use sparingly.",
                "Fatigue: even tools wear out.",
                "Fatigue: everything wears out.",
            },

            ["tooltip.counter_trait"] = new[]
            {
                "Counter-Trait: this client has adapted to this approach.",
                "Counter-Trait: they've seen this before.",
                "Counter-Trait: they've had time to prepare.",
                "Counter-Trait: they learned it from you.",
            },

            ["tooltip.mutation_warning"] = new[]
            {
                "Pattern Detected: client behaviour may adapt.",
                "Pattern Detected: change tactics.",
                "Pattern Detected: they're onto you.",
                "Pattern Detected: of course they are.",
            },

            // ── Status Bar ──────────────────────────────────────

            ["status.shift_progress"] = new[]
            {
                "Client {n} of {total}. On track.",
                "Client {n} of {total}. Adequate.",
                "Client {n} of {total}. Keep going.",
                "Client {n} of {total}.",
            },

            ["status.quota_met"] = new[]
            {
                "Quota met. Excellent work.",
                "Quota met. Good enough.",
                "Quota met. For now.",
                "Quota met. It'll be higher next shift.",
            },

            ["status.overtime"] = new[]
            {
                "Overtime initiated. Additional compensation pending review.",
                "Overtime. You'll manage.",
                "Overtime. Again.",
                "Overtime. There's no additional compensation.",
            },

            ["status.fugue_warning"] = new[]
            {
                "Cognitive load approaching critical threshold.",
                "You're running low. Be careful.",
                "You are not well.",
                "You're still here.",
            },

            ["status.client_agitated"] = new[]
            {
                "Client Agitated: de-escalation recommended.",
                "Client is upset. Handle it.",
                "They're angry. Not without reason.",
                "They're angry. You know why.",
            },

            ["status.client_litigious"] = new[]
            {
                "Client Litigious: legal intervention required.",
                "Legal escalation in progress.",
                "They're going to sue. You made that happen.",
                "They're going to sue.\nWe both know why.",
            },

            ["status.client_dissociating"] = new[]
            {
                "Client Unresponsive: temporary disengagement protocol.",
                "Client has checked out.",
                "Client has stopped responding. You pushed too hard.",
                "They're gone somewhere you can't reach.\nYou did that.",
            },

            ["status.claim_flagged"] = new[]
            {
                "Claim Flagged: anomaly detected, review initiated.",
                "Flagged for review.",
                "Something's wrong with this claim.",
                "Something's wrong. You already knew.",
            },

            // ── Dilemma prompts ──────────────────────────────────

            ["dilemma.header"] = new[]
            {
                "A decision is required.",
                "This requires your attention.",
                "There is a choice here.",
                "There is no good choice here.",
            },

            ["dilemma.ethical"] = new[]
            {
                "The right thing to do.",
                "The slower, costlier option.",
                "It costs you. Do it anyway.",
                "It won't matter. Do it anyway.",
            },

            ["dilemma.bureaucratic"] = new[]
            {
                "Efficient. Procedurally sound.",
                "Faster. Cheaper. Fine.",
                "You'll regret this.",
                "You stopped regretting these.",
            },

            // ── Moral injury feedback ─────────────────────────────

            ["injury.empathy_denied"] = new[]
            {
                "Processing continues.",
                "Noted. Moving on.",
                "You've done this before.",
                "Another one. Same as the last.",
            },

            ["injury.evidence_destroyed"] = new[]
            {
                "Document retention policy applied.",
                "It's gone now.",
                "No one will find it.",
                "No one was looking.",
            },

            ["injury.wrongful_termination"] = new[]
            {
                "Employment contract terminated per Section 7b.",
                "They're gone.",
                "That was someone's livelihood.",
                "You've done worse.",
            },

            ["injury.scar_new"] = new[]
            {
                "Behavioural pattern recorded.",
                "This is becoming a habit.",
                "You've crossed a line you can't uncross.",
                "You barely noticed.",
            },

            // ── NDA ──────────────────────────────────────────────

            ["nda.applied"] = new[]
            {
                "Non-Disclosure Agreement applied to claim documentation.",
                "NDA on file.",
                "Covered up.",
                "The screen is getting smaller.",
            },

            ["nda.warning_3"] = new[]
            {
                "Multiple NDAs active: documentation coverage at 40%.",
                "Several areas are restricted now.",
                "You can't see much of the desk anymore.",
                "You signed away most of what you could see.",
            },

            // ── Narrator tone shift announcements ─────────────────

            ["narrator.tone_shift.slightly_honest"] = new[]
            {
                "", "", // won't be used at this tone level
                "Processing continues. Nothing to report.",
                "",
            },

            ["narrator.tone_shift.honest"] = new[]
            {
                "", "",
                "Integrity metrics have entered a notable range.",
                "",
            },

            ["narrator.tone_shift.doublespeak"] = new[]
            {
                "", "", "",
                "You should know: I'm not being honest with you anymore.",
            },
        };

        // ── Query API ─────────────────────────────────────────

        /// <summary>
        /// Get a narrator line for the given context key and current tone.
        /// Returns an empty string for unknown keys.
        /// </summary>
        public static string GetLine(string contextKey, NarratorReliability tone)
        {
            if (!_lines.TryGetValue(contextKey, out var variants))
                return $"[{contextKey}]"; // missing key fallback

            int idx = (int)tone; // enum maps to array index
            if (idx >= 0 && idx < variants.Length)
                return variants[idx];
            return variants[0];
        }

        /// <summary>
        /// Fill in template tokens {n} and {total} in a status line.
        /// </summary>
        public static string GetStatusLine(string contextKey, NarratorReliability tone,
            int n = 0, int total = 0)
        {
            return GetLine(contextKey, tone)
                .Replace("{n}",     n.ToString())
                .Replace("{total}", total.ToString());
        }

        /// <summary>
        /// Returns true if a line exists for this key.
        /// </summary>
        public static bool HasLine(string contextKey) => _lines.ContainsKey(contextKey);

        /// <summary>
        /// All registered context keys — for editor tooling / validation.
        /// </summary>
        public static IEnumerable<string> AllKeys => _lines.Keys;
    }
}
