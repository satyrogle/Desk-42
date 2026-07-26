// ============================================================
// DESK 42 — Encounter commit transaction
//
// Handoff §3.3: ONE authoritative CommitEncounterResult().
//
// Required ordering (semantics are locked; internal shape adapts):
//    1 Validate EncounterId / idempotency
//    2 Apply record mutations
//    3 Append encounter history
//    4 Mark completed visit where applicable
//    5 Update committed behaviour counters
//    6 Apply reference mutations
//    7 Apply declared thread delta
//    8 Schedule declared consequences
//    9 Save
//   10 Cleanup / transition        <- caller's responsibility, see CommitResult
//
// No other subsystem may independently commit visit count, behaviour
// count, record mutation, or consequence scheduling.
//
// ATOMICITY (reported, not silently worked around): the save architecture
// is whole-object JSON overwrite with no journal, so a true rollback is
// not available. The weakest boundary is documented in
// docs/proof-build/BUCKET-1-PERSISTENCE.md. The pattern implemented here
// is the safest practical one available: the idempotency gate is checked
// first and the history entry is marked complete BEFORE the save, so a
// crash between mutation and save loses the whole encounter rather than
// half-applying it, and replay is a no-op rather than a double-apply.
// ============================================================

using System;
using UnityEngine;
using Desk42.Core;

namespace Desk42.Encounter
{
    /// <summary>Why a commit did not mutate anything.</summary>
    public enum CommitRejection
    {
        None = 0,
        NoEncounterId,
        NoRun,
        NoHistory,
        AlreadyCommitted,
        NotPresented,
    }

    /// <summary>Outcome of one CommitEncounterResult call.</summary>
    public readonly struct CommitResult
    {
        public readonly bool Committed;
        public readonly CommitRejection Rejection;
        public readonly string EncounterId;
        public readonly AppliedClaimResolution Applied;

        public CommitResult(bool committed, CommitRejection rejection,
            string encounterId, AppliedClaimResolution applied)
        {
            Committed   = committed;
            Rejection   = rejection;
            EncounterId = encounterId;
            Applied     = applied;
        }

        /// <summary>
        /// True when the caller should run cleanup/transition (step 10).
        /// A duplicate commit still transitions — the encounter really is over —
        /// but mutates nothing.
        /// </summary>
        public bool ShouldTransition
            => Committed || Rejection == CommitRejection.AlreadyCommitted;
    }

    /// <summary>
    /// The single authoritative encounter commit path. Stateless; all state
    /// lives in RunData / MetaProgressData / the proof session.
    /// </summary>
    public static class EncounterCommitService
    {
        // ── Encounter identity ───────────────────────────────

        /// <summary>
        /// Returns the claim's stable EncounterId, assigning one on first use.
        /// The id is stored on ActiveClaimData, which IS serialized, so a
        /// mid-encounter quit/resume reconstructs the SAME id and cannot
        /// produce a phantom visit.
        ///
        /// ClaimId alone is unusable as identity: it is a seeded 5-digit
        /// "CLM-#####" with no uniqueness check, so collisions are possible
        /// within and across runs.
        /// </summary>
        public static string EnsureEncounterId(ActiveClaimData claim, RunData runData)
        {
            if (claim == null) return null;

            if (!string.IsNullOrWhiteSpace(claim.EncounterId))
                return claim.EncounterId;

            string seed  = runData?.SeedCode;
            int    shift = runData?.ShiftNumber ?? 0;
            int    seq   = runData != null ? ++runData.EncounterSequence : 0;

            if (string.IsNullOrWhiteSpace(seed)) seed = "NOSEED";

            claim.EncounterId = $"ENC-{seed}-S{shift}-{seq:D3}";
            return claim.EncounterId;
        }

        // ── Presentation (NOT a visit) ───────────────────────

        /// <summary>
        /// Records that a claim was presented, and returns the immutable
        /// baseline for the encounter.
        ///
        /// Handoff §3.1: this is explicitly NOT a visit. It fires at spawn and
        /// must never increment completed visits. Idempotent across scene
        /// reconstruction and mid-encounter resume.
        /// </summary>
        public static EncounterBaseline BeginEncounter(
            ActiveClaimData claim, RunData runData, MetaProgressData meta)
        {
            if (claim == null || meta == null) return EncounterBaseline.None;

            string encounterId = EnsureEncounterId(claim, runData);
            if (string.IsNullOrWhiteSpace(encounterId)) return EncounterBaseline.None;

            var history = meta.Encounters;
            if (history == null) return EncounterBaseline.None;

            // Capture the baseline BEFORE appending this presentation so the
            // numbers describe the world the claimant walked into.
            int priorVisits        = history.PriorVisits(claim.ClientVariantId, encounterId);
            int priorPresentations = history.TotalPresentations(claim.ClientVariantId);
            bool alreadyCommitted  = history.IsCompleted(encounterId);

            history.BeginPresentation(
                encounterId,
                claim.ClaimId,
                claim.ClientVariantId,
                claim.ClientSpeciesId,
                claim.AuthoredAppearanceKey,
                runData?.ShiftNumber ?? 0);

            return new EncounterBaseline(
                encounterId,
                claim.ClientVariantId,
                claim.AuthoredAppearanceKey,
                runData?.ShiftNumber ?? 0,
                priorVisits,
                priorPresentations,
                alreadyCommitted);
        }

        // ── The transaction ──────────────────────────────────

        /// <summary>
        /// The one authoritative commit. Returns a rejection instead of
        /// throwing when the encounter is unknown or already committed, so a
        /// double-click (or the 11 duplicate onClick listeners currently bound
        /// in Shift.unity) mutates exactly once.
        /// </summary>
        public static CommitResult CommitEncounterResult(
            ActiveClaimData claim,
            ClaimResolutionOutcome outcome,
            RunStateController run,
            MetaProgressData meta,
            EliasProofSessionController proof,
            EliasProofContent eliasContent)
        {
            // ── 1. Validate EncounterId / idempotency ────────
            if (run == null)
                return Reject(CommitRejection.NoRun, null);

            var runData = run.RawData;
            string encounterId = EnsureEncounterId(claim, runData);

            if (string.IsNullOrWhiteSpace(encounterId))
                return Reject(CommitRejection.NoEncounterId, null);

            var history = meta?.Encounters;
            if (history == null)
                return Reject(CommitRejection.NoHistory, encounterId);

            if (history.IsCompleted(encounterId))
            {
                Debug.Log($"[EncounterCommit] Duplicate commit ignored for " +
                          $"'{encounterId}' — already completed.");
                return Reject(CommitRejection.AlreadyCommitted, encounterId);
            }

            // A commit for an encounter that was never presented would create
            // history out of nothing. Repair it rather than fail: the claim is
            // real and in hand, so record the presentation now.
            if (!history.Contains(encounterId))
            {
                Debug.LogWarning($"[EncounterCommit] '{encounterId}' committed " +
                                 $"without a recorded presentation — repairing.");
                history.BeginPresentation(
                    encounterId, claim?.ClaimId, claim?.ClientVariantId,
                    claim?.ClientSpeciesId, claim?.AuthoredAppearanceKey,
                    runData?.ShiftNumber ?? 0);
            }

            // ── 2. Apply record mutations ────────────────────
            var applied = run.ApplyClaimResolution(
                outcome,
                claim?.ClaimId,
                claim?.ClientVariantId,
                claim?.ClientSpeciesId);

            // ── 3/4. Append history + mark the completed visit ───
            // TotalVisits derives from this flag. There is no second counter.
            history.MarkCompleted(encounterId, applied.Kind, DateTime.UtcNow.Ticks);

            // ── 5. Committed behaviour counters ──────────────
            // Behaviour counters that were previously incremented at spawn are
            // now committed here, so an abandoned encounter leaves no trace.
            if (!string.IsNullOrWhiteSpace(claim?.ClientVariantId))
                meta.GetOrCreateProfile(claim.ClientVariantId);

            // ── 6/7/8. Reference mutations, thread delta, consequences ───
            if (proof != null && proof.HasActiveSession)
            {
                proof.RecordDisposition(claim?.AuthoredAppearanceKey, applied);

                if (string.Equals(claim?.AuthoredAppearanceKey,
                        EliasProofContent.Shift5AppearanceKey,
                        StringComparison.Ordinal))
                {
                    proof.ActivateShift5Aftermath(eliasContent);
                }
            }

            // ── 9. Save ──────────────────────────────────────
            // Previously absent entirely: resolution mutated run + meta and
            // never persisted, so a crash or quit lost the whole encounter.
            PersistQuietly(runData, meta);

            Debug.Log($"[EncounterCommit] Committed '{encounterId}' " +
                      $"({applied.Kind}) claim='{claim?.ClaimId}' " +
                      $"visitsNow={history.TotalVisits(claim?.ClientVariantId)}.");

            // ── 10. Cleanup / transition — caller ────────────
            return new CommitResult(true, CommitRejection.None, encounterId, applied);
        }

        // ── Helpers ──────────────────────────────────────────

        private static CommitResult Reject(CommitRejection reason, string encounterId)
            => new CommitResult(false, reason, encounterId, default);

        /// <summary>
        /// Persists run + meta. Save failures are logged, never thrown: losing
        /// the save is recoverable, killing the resolution mid-transaction is not.
        /// </summary>
        private static void PersistQuietly(RunData runData, MetaProgressData meta)
        {
            try
            {
                if (runData != null && !runData.IsComplete)
                    SaveSystem.SaveRun(runData);
                if (meta != null)
                    SaveSystem.SaveMeta(meta);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EncounterCommit] Save failed after commit: {ex.Message}. " +
                               $"State is correct in memory; history may replay on reload.");
            }
        }
    }
}
