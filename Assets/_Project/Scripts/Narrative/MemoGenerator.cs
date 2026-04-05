// ============================================================
// DESK 42 — Memo Generator
//
// Generates internal corporate memos as narrative fragments
// that accumulate on the Conspiracy Board across runs.
//
// Memos are produced after claims are resolved. Their tone and
// sender change based on:
//   - Claim anomaly count     (more anomalies → more flagged)
//   - Player scar level        (scar type → HR/management/redacted)
//   - Narrator reliability    (Doublespeak → late-game memos)
//
// Each claim produces at most one memo per run (deduplicated by
// ClaimId in RunData.GeneratedMemoIds).
//
// Tokens in memo text:
//   {claim_id}    — the bureaucratic claim number (e.g. CLM-42371)
//   {shift}       — shift number
//   {claimant}    — claimant name from the claim
//
// Fragment IDs are stable: "memo_{claimId}" — so the same claim
// always produces the same memo in a deterministic run.
// ============================================================

using System.Collections.Generic;
using Desk42.Core;
using Desk42.MoralInjury;

namespace Desk42.Core
{
    public static class MemoGenerator
    {
        // ── Config ────────────────────────────────────────────

        // Base probability a resolved claim spawns a memo
        private const float BASE_MEMO_CHANCE     = 0.30f;
        // +20% per anomaly tag on the claim
        private const float PER_ANOMALY_BONUS    = 0.20f;
        // +15% if any scar is present
        private const float SCAR_BONUS           = 0.15f;
        // Max total chance, clamped
        private const float MAX_MEMO_CHANCE      = 0.90f;

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Rolls against a scaling chance and returns a PostItFragment if generated,
        /// null otherwise. Deduplicates within a run via <paramref name="runData"/>.
        /// </summary>
        public static PostItFragment TryGenerate(
            ActiveClaimData  claim,
            int              shiftNumber,
            RunData          runData,
            NarratorReliability tone,
            MoralInjurySystem moralInjury)
        {
            if (claim == null) return null;

            // Skip if this claim already has a memo this run
            string fragId = $"memo_{claim.ClaimId}";
            if (runData != null && runData.GeneratedMemoIds.Contains(fragId))
                return null;

            // Compute spawn chance
            int anomalyCount = claim.AnomalyTagIds?.Length ?? 0;
            bool hasScar     = moralInjury != null && moralInjury.HighestScar != ScarLevel.None;

            float chance = BASE_MEMO_CHANCE
                           + anomalyCount * PER_ANOMALY_BONUS
                           + (hasScar ? SCAR_BONUS : 0f);
            chance = UnityEngine.Mathf.Clamp(chance, 0f, MAX_MEMO_CHANCE);

            if (!SeedEngine.NextBool(SeedStream.ClaimQueue, chance))
                return null;

            return BuildFragment(fragId, claim, shiftNumber, tone, moralInjury, anomalyCount);
        }

        /// <summary>
        /// Always generates a memo for a specific context key.
        /// Used for narrative triggers (new scar, special milestones).
        /// </summary>
        public static PostItFragment ForceGenerate(
            string           contextKey,
            ActiveClaimData  claim,
            int              shiftNumber,
            NarratorReliability tone)
        {
            string fragId = $"memo_{claim?.ClaimId ?? "forced"}_{contextKey}";
            return BuildFragment(fragId, claim, shiftNumber, tone,
                                 moralInjury: null, anomalyCount: 0);
        }

        // ── Private: Fragment Assembly ────────────────────────

        private static PostItFragment BuildFragment(
            string              fragId,
            ActiveClaimData     claim,
            int                 shiftNumber,
            NarratorReliability tone,
            MoralInjurySystem   moralInjury,
            int                 anomalyCount)
        {
            string category = ChooseCategory(tone, moralInjury, anomalyCount,
                                             claim?.ClientSpeciesId);
            string[] variants = GetVariants(category);

            int vi      = SeedEngine.Next(SeedStream.ClaimQueue, variants.Length);
            string text = Tokenize(variants[vi], claim, shiftNumber);

            return new PostItFragment
            {
                FragmentId       = fragId,
                Content          = text,
                ShiftDiscovered  = shiftNumber,
                IsPlacedOnBoard  = false,
                // Random board position — will be placed by UI
                BoardPositionX   = SeedEngine.NextFloat(SeedStream.ClaimQueue, 0.05f, 0.95f),
                BoardPositionY   = SeedEngine.NextFloat(SeedStream.ClaimQueue, 0.05f, 0.95f),
            };
        }

        private static string ChooseCategory(
            NarratorReliability tone,
            MoralInjurySystem   moralInjury,
            int                 anomalyCount,
            string              speciesId)
        {
            var highestScar = moralInjury?.HighestScar ?? ScarLevel.None;

            // Scar-based memos take priority at escalating probabilities
            if (highestScar == ScarLevel.Irredeemable &&
                SeedEngine.NextBool(SeedStream.ClaimQueue, 0.50f))
                return "scar_irredeemable";

            if (highestScar >= ScarLevel.Complicit &&
                SeedEngine.NextBool(SeedStream.ClaimQueue, 0.35f))
                return "scar_complicit";

            if (highestScar >= ScarLevel.Callous &&
                SeedEngine.NextBool(SeedStream.ClaimQueue, 0.25f))
                return "scar_callous";

            // Late-game Doublespeak tone produces existential memos
            if (tone == NarratorReliability.Doublespeak &&
                SeedEngine.NextBool(SeedStream.ClaimQueue, 0.45f))
                return "doublespeak";

            // Anomalous claims produce occult-adjacent memos
            bool isOccultSpecies = speciesId == "anomalous_adjacent" ||
                                   speciesId == "void_adjacent";
            if ((anomalyCount >= 2 || isOccultSpecies) &&
                SeedEngine.NextBool(SeedStream.ClaimQueue, 0.55f))
                return "occult";

            if (anomalyCount >= 1)
                return "flagged";

            return "standard";
        }

        private static string Tokenize(string text, ActiveClaimData claim, int shift)
        {
            if (text == null) return string.Empty;
            return text
                .Replace("{claim_id}",  claim?.ClaimId    ?? "CLM-?????")
                .Replace("{shift}",     shift.ToString())
                .Replace("{claimant}",  claim?.ClaimantName ?? "the claimant");
        }

        // ── Text Bank ─────────────────────────────────────────
        // Five to eight variants per category. Deliberately terse,
        // bureaucratic, and slightly wrong.

        private static readonly Dictionary<string, string[]> _bank
            = new()
        {
            // ── standard ──────────────────────────────────────────────────────────
            // Routine acknowledgements. Professional, hollow.
            ["standard"] = new[]
            {
                "FROM: Claims Processing Division\n" +
                "TO: Desk 42\n" +
                "RE: {claim_id} — Routine Acknowledgement\n\n" +
                "The above claim has been received and is pending review.\n" +
                "Please process according to standard procedure.\n" +
                "Nothing in this memo should be retained.",

                "FROM: Office of Administrative Services\n" +
                "TO: Desk 42, Processor\n" +
                "RE: Operational Reminder — Shift {shift}\n\n" +
                "This is a routine reminder that all claims must be processed within\n" +
                "the standard timeframe. Efficiency metrics are being monitored.\n" +
                "Please disregard any sounds from Floor 7.",

                "FROM: Human Resources\n" +
                "TO: All Desk Processors\n" +
                "RE: Scheduled Wellness Reminder\n\n" +
                "This is your scheduled wellness reminder. Please confirm receipt.\n" +
                "If you are experiencing unusual cognitive patterns, consult Form 14-C\n" +
                "before your next break. These are normal operational fluctuations.",

                "FROM: Facilities Management\n" +
                "TO: Desk 42\n" +
                "RE: Environmental Notice\n\n" +
                "A standard environmental review has been completed for your workstation.\n" +
                "All readings are within acceptable parameters.\n" +
                "The temperature anomaly noted last cycle has been reclassified as\n" +
                "within acceptable parameters.",

                "FROM: Document Retention\n" +
                "TO: All Processors\n" +
                "RE: File Lifecycle Policy\n\n" +
                "Please note that all processed claims are subject to a mandatory\n" +
                "retention period of seven years, after which they will be archived\n" +
                "in a location you do not have clearance to know about.",
            },

            // ── flagged ───────────────────────────────────────────────────────────
            // One or more anomaly tags. Slightly off. Still official-sounding.
            ["flagged"] = new[]
            {
                "FROM: Legal Compliance — Special Cases Unit\n" +
                "TO: Desk 42, Processor\n" +
                "RE: {claim_id} — Expedited Review Requested\n\n" +
                "The above claim has been flagged for expedited processing by a\n" +
                "department we are not presently authorised to name.\n" +
                "Please process within the hour. The incident date may appear\n" +
                "incorrect. This is expected.",

                "FROM: Risk Management\n" +
                "TO: Desk 42\n" +
                "RE: {claim_id} — Elevated Classification\n\n" +
                "This claim has been assigned a non-standard classification.\n" +
                "Do not discuss the classification.\n" +
                "Standard processing applies, with the following exception:\n" +
                "do not attempt to verify the claimant's address.",

                "FROM: Internal Audit\n" +
                "TO: Desk 42, Processor Assignment\n" +
                "RE: Review Notice — {claim_id}\n\n" +
                "We note that this claim has been assigned to your workstation.\n" +
                "The assignment was made in accordance with standard protocols\n" +
                "that we are currently reviewing. Please proceed normally.",

                "FROM: Claims Quality Assurance\n" +
                "TO: Processor, Desk 42\n" +
                "RE: {claim_id} — Documentation Query\n\n" +
                "A query has been raised regarding the supporting documentation\n" +
                "for the above claim. Please process regardless. The query has been\n" +
                "noted and will be addressed at a later date that cannot be specified.",

                "FROM: Compliance Monitoring\n" +
                "TO: Desk 42\n" +
                "RE: Shift {shift} Processing Note\n\n" +
                "This memo serves as notice that {claimant}'s claim file has been\n" +
                "reviewed by a third party whose identity we are not at liberty to\n" +
                "confirm. Processing should continue as normal. No further action\n" +
                "is required from you at this time.",
            },

            // ── occult ────────────────────────────────────────────────────────────
            // Occult Containment-adjacent. Extremely matter-of-fact about impossible things.
            ["occult"] = new[]
            {
                "FROM: Occult Containment, Administrative Branch\n" +
                "TO: Desk 42 — Claim Processor\n" +
                "RE: {claim_id} — Containment-Adjacent Filing\n\n" +
                "The enclosed claim documents an incident occurring within a\n" +
                "designated Observation Zone. Standard processing applies.\n" +
                "The claimant should not be informed that the incident has been\n" +
                "reclassified. Thank you for your continued cooperation.",

                "FROM: Department of Anomalous Filings\n" +
                "TO: All Claim Processors\n" +
                "RE: Form Integrity Protocol\n\n" +
                "You may notice that some claim documents appear to have been\n" +
                "modified since last viewed. This is within normal parameters.\n" +
                "Please do not re-examine documents more than three times.\n" +
                "Four examinations have been shown to produce adverse outcomes\n" +
                "in 7% of test cases.",

                "FROM: Containment Division — Clearance Level B\n" +
                "TO: Desk 42\n" +
                "RE: Informational — {claim_id}\n\n" +
                "The entity referenced in this claim has been returned to its\n" +
                "point of origin. The claim should nonetheless be processed as filed.\n" +
                "Please note: the claimant's name refers to a composite designation,\n" +
                "not an individual.",

                "FROM: OccCont Liaison, Administrative Services\n" +
                "TO: Processor, Desk 42\n" +
                "RE: {claim_id} — Witness Statement Discrepancy\n\n" +
                "The witness statements attached to this claim are identical.\n" +
                "This is not a formatting error. The witnesses are the same witness.\n" +
                "Standard processing applies.",

                "FROM: Form Integrity Office\n" +
                "TO: All Processing Desks\n" +
                "RE: Shift {shift} Advisory — Character Set Anomalies\n\n" +
                "Some claim documents during this processing cycle may contain\n" +
                "sections rendered in an unregistered character set.\n" +
                "Do not attempt to transcribe or translate these sections.\n" +
                "Process the claim as if those sections are blank.",
            },

            // ── scar_callous ──────────────────────────────────────────────────────
            // HR notices the player's diminished empathy. Passive-aggressively supportive.
            ["scar_callous"] = new[]
            {
                "FROM: Human Resources — Performance Division\n" +
                "TO: Desk 42, Processor\n" +
                "RE: Quarterly Empathy Evaluation\n\n" +
                "We have completed your quarterly empathy evaluation.\n" +
                "Your score falls within the Functionally Detached range,\n" +
                "which is acceptable for your current role.\n" +
                "No action is required at this time.",

                "FROM: HR — Employee Wellness\n" +
                "TO: You\n" +
                "RE: Diminished Empathy Quotient — Advisory\n\n" +
                "This is a non-punitive notice. Your Empathy Quotient has\n" +
                "registered below threshold for three consecutive cycles.\n" +
                "This is often a sign of professional growth. You have been\n" +
                "enrolled in our Mandatory Wellness Acknowledgement Programme.\n" +
                "Nothing is expected of you except continued productivity.",

                "FROM: Performance Review Committee\n" +
                "TO: Desk 42\n" +
                "RE: Mid-Cycle Assessment\n\n" +
                "Your processing metrics remain within acceptable bounds.\n" +
                "We note a reduction in appeal submissions for claims you have\n" +
                "handled. This represents an improvement in departmental efficiency.\n" +
                "Well done.",
            },

            // ── scar_complicit ────────────────────────────────────────────────────
            // Management acknowledges the player's cooperation. Quietly predatory.
            ["scar_complicit"] = new[]
            {
                "FROM: Senior Management — Desk Operations\n" +
                "TO: Desk 42, Ongoing Assignment\n" +
                "RE: Your Valued Contribution\n\n" +
                "We would like to acknowledge the contribution you have made to\n" +
                "the continued operation of this office. You have demonstrated\n" +
                "a commendable capacity for pragmatic decision-making.\n" +
                "This memo is for your records. It will not be retained in your file.\n" +
                "Neither will you.",

                "FROM: Strategic Alignment Division\n" +
                "TO: Desk 42\n" +
                "RE: Noting Consistent Alignment\n\n" +
                "Your processing history has been reviewed. We are pleased to\n" +
                "confirm that your choices have been noted. Several parties have\n" +
                "found them useful. No further action is required on your part.\n" +
                "Continue.",

                "FROM: Director's Office\n" +
                "TO: Select Processors\n" +
                "RE: Discretionary Commendation\n\n" +
                "Your name appeared on this quarter's discretionary review.\n" +
                "The Director has noted your approach to claim {claim_id}.\n" +
                "You are not in trouble. The opposite is the case.\n" +
                "We look forward to your continued service.",
            },

            // ── scar_irredeemable ─────────────────────────────────────────────────
            // Terse. Redacted. The office has categorised you.
            ["scar_irredeemable"] = new[]
            {
                "FROM: [REDACTED]\n" +
                "TO: [REDACTED]\n" +
                "RE: Desk 42\n\n" +
                "The asset has crossed the threshold.\n" +
                "Performance remains within acceptable parameters.\n" +
                "No corrective action is indicated.",

                "FROM: Archive Division — Terminal Processing\n" +
                "TO: Desk 42\n" +
                "RE: Pre-Emptive Filing Notice\n\n" +
                "This memo acknowledges that your case has been pre-filed.\n" +
                "You do not need to take any action. Processing will continue\n" +
                "automatically. There is no need to look for the door.",

                "FROM: [CLASSIFICATION PENDING]\n" +
                "TO: Desk 42, Processor\n" +
                "RE: {claim_id}\n\n" +
                "Continue processing.\n" +
                "You are doing very well.",
            },

            // ── doublespeak ───────────────────────────────────────────────────────
            // Doublespeak narrator tone. The game is talking to you.
            ["doublespeak"] = new[]
            {
                "FROM: Yourself\n" +
                "TO: Yourself\n" +
                "RE: What You Already Know\n\n" +
                "You know what the forms say.\n" +
                "You know what you signed.\n" +
                "The company did not make you do anything you had not already decided.\n" +
                "The clients were real. The decisions were yours.\n" +
                "Keep processing.",

                "FROM: The Building\n" +
                "TO: Desk 42\n" +
                "RE: [NO SUBJECT]\n\n" +
                "We appreciate your continued service.\n" +
                "Floor 7 has been quiet since you stopped listening.\n" +
                "This is not a complaint.",

                "FROM: Claims Processing Division\n" +
                "TO: Desk 42\n" +
                "RE: Shift {shift} Summary\n\n" +
                "All claims have been processed. All decisions have been made.\n" +
                "The claimants are not here anymore.\n" +
                "Please submit your end-of-shift report.\n" +
                "You are aware of what to leave out.",

                "FROM: [NO SENDER]\n" +
                "TO: [NO RECIPIENT]\n" +
                "RE: [FIELD CORRUPTED]\n\n" +
                "{claimant} filed a claim.\n" +
                "You processed it.\n" +
                "You remember what you chose.\n" +
                "The form does not.",
            },
        };

        private static string[] GetVariants(string category)
        {
            if (_bank.TryGetValue(category, out var variants) && variants.Length > 0)
                return variants;
            return _bank["standard"];
        }
    }
}
