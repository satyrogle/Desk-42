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
        /// Returns this run instance's durable id, allocating one on first use.
        ///
        /// Deliberately NOT derived from SeedCode: the gameplay seed is a
        /// determinism input and two different runs may legitimately share it
        /// (replays, seeded runs, daily brief). Encounter history is cross-run
        /// and persistent, so an id namespace built from seed + shift +
        /// per-run sequence aliases independent runs onto the same historical
        /// encounter, and the second legitimate encounter is then rejected as
        /// AlreadyCommitted.
        ///
        /// Allocated once and persisted, so it survives save/load and is never
        /// regenerated on resume or on encounter reconstruction.
        /// </summary>
        public static string EnsureRunInstanceId(RunData runData)
        {
            if (runData == null) return null;

            if (string.IsNullOrWhiteSpace(runData.RunInstanceId))
                runData.RunInstanceId = NewRunInstanceId();

            return runData.RunInstanceId;
        }

        /// <summary>Opaque, collision-resistant run identity. Not a seed.</summary>
        internal static string NewRunInstanceId()
            => Guid.NewGuid().ToString("N").Substring(0, 12);

        /// <summary>
        /// Returns the claim's stable EncounterId, assigning one on first use.
        /// The id is stored on ActiveClaimData, which IS serialized, so a
        /// mid-encounter quit/resume reconstructs the SAME id and cannot
        /// produce a phantom visit.
        ///
        /// Namespaced by RunInstanceId so two runs sharing a seed, shift and
        /// sequence position cannot collide. ClaimId is not usable as identity
        /// either: it is a seeded 5-digit "CLM-#####" with no uniqueness check,
        /// and different runs may legitimately meet the same claim identity.
        /// </summary>
        public static string EnsureEncounterId(ActiveClaimData claim, RunData runData)
        {
            if (claim == null) return null;

            if (!string.IsNullOrWhiteSpace(claim.EncounterId))
                return claim.EncounterId;

            int shift = runData?.ShiftNumber ?? 0;
            int seq   = runData != null ? ++runData.EncounterSequence : 0;

            // A null RunData cannot carry identity; fall back to a one-shot
            // namespace so the id is still unique rather than silently shared.
            string runId = runData != null
                ? EnsureRunInstanceId(runData)
                : NewRunInstanceId();

            claim.EncounterId = $"ENC-{runId}-S{shift}-{seq:D3}";
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
            EliasProofContent eliasContent,
            ClaimBonusRates bonusRates = default)
        {
            if (bonusRates.CrossClaim == 0 && bonusRates.SequentialSynergy == 0)
                bonusRates = ClaimBonusRates.Default;

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

            // ── 8b. Resolve the persistent active claim ──────
            // MUST happen before the save. Encounter history is about to be
            // written as Completed; if RunData still carried this claim as an
            // unresolved ActiveClaim, the same disk snapshot would contradict
            // itself and a reload would resurrect a completed encounter as
            // active. Persistence authority belongs to this transaction, not
            // to a deferred UI/flow listener that runs after the save.
            ResolveActiveClaim(runData, claim, applied.Kind);

            // ── 8c. Deterministic persistent consequences ────
            // Cross-claim bonus, sequential synergy and persistent memo state
            // are consequences OF this encounter, so they must be inside the
            // transaction. Previously they ran in ShiftManager's deferred
            // handler after the save, which meant a crash lost an earned bonus
            // and a stale callback could reapply one to the wrong claim.
            // Bound to `claim` by reference, and durably exact-once via
            // ActiveClaimData.ConsequencesApplied.
            var consequences = ClaimConsequencePolicy.Apply(
                claim, run, runData, meta, bonusRates);


            // ── 9. Save ──────────────────────────────────────
            // Previously absent entirely: resolution mutated run + meta and
            // never persisted, so a crash or quit lost the whole encounter.
            PersistQuietly(runData, meta);

            // Presentation for the persistent memo written above. The state is
            // already durable; this notification is deferred UI only, so losing
            // it costs nothing.
            if (consequences.GeneratedMemo)
            {
                RumorMill.Publish(new MemoGeneratedEvent(
                    consequences.MemoFragmentId,
                    claim?.ClaimId,
                    $"Memo: {claim?.ClientVariantId ?? "client"}",
                    string.Empty));
            }

            Debug.Log($"[EncounterCommit] Committed '{encounterId}' " +
                      $"({applied.Kind}) claim='{claim?.ClaimId}' " +
                      $"visitsNow={history.TotalVisits(claim?.ClientVariantId)} " +
                      $"bonus={consequences.TotalCredits} " +
                      $"memo={(consequences.GeneratedMemo ? consequences.MemoFragmentId : "none")}.");

            // ── 10. Cleanup / transition — caller ────────────
            return new CommitResult(true, CommitRejection.None, encounterId, applied);
        }

        // ── Helpers ──────────────────────────────────────────

        private static CommitResult Reject(CommitRejection reason, string encounterId)
            => new CommitResult(false, reason, encounterId, default);

        /// <summary>
        /// Marks the committed claim resolved and appends it to ResolvedClaims
        /// so the persisted run agrees with the persisted history.
        ///
        /// The claim is deliberately left in RunData.ActiveClaim, now flagged
        /// IsResolved. The invariant is "no persisted representation of this
        /// encounter as UNRESOLVED", not "no representation at all" — and
        /// keeping the reference means a duplicate commit arriving after a
        /// reload can still be matched to its EncounterId and rejected as
        /// AlreadyCommitted rather than silently failing to resolve.
        ///
        /// Clearing the slot is flow, not persistence: ShiftManager does it on
        /// the deferred event, and ShiftManager.Start refuses to re-present a
        /// claim that is already resolved.
        ///
        /// Idempotent, and a no-op when this claim is not the one at the desk.
        /// </summary>
        private static void ResolveActiveClaim(
            RunData runData, ActiveClaimData claim, ClaimResolutionKind kind)
        {
            if (runData == null || claim == null) return;

            claim.IsResolved     = true;
            claim.ResolutionKind = kind;

            if (!ReferenceEquals(runData.ActiveClaim, claim)) return;

            runData.ResolvedClaims ??= new System.Collections.Generic.List<ActiveClaimData>();

            // Guard against a double append if this ever runs twice.
            bool alreadyRecorded = runData.ResolvedClaims.Count > 0
                && ReferenceEquals(runData.ResolvedClaims[^1], claim);

            if (!alreadyRecorded)
                runData.ResolvedClaims.Add(claim);
        }

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
