// ============================================================
// DESK 42 — Deterministic persistent consequences of a commit
//
// Cross-claim bonus, sequential-synergy bonus and persistent memo state
// are deterministic consequences of a committed encounter. They used to
// run in ShiftManager's deferred ClaimResolvedEvent handler — AFTER the
// authoritative save — which produced two defects:
//
//   1. A stale event for an older ClaimId reapplied bonuses, because the
//      handler selected ResolvedClaims[^1] rather than the event's own
//      claim. Credits moved 17 -> 22.
//   2. A crash between the save and the deferred handler lost an earned
//      bonus outright: 17 expected, 12 observed after reload.
//
// Both are boundary defects, not arithmetic defects: a successful
// authoritative save did not contain every persistent consequence of the
// encounter it claimed to have committed.
//
// This service is invoked synchronously by EncounterCommitService BEFORE
// the save, bound to the exact committed claim. ShiftManager keeps only
// presentation and flow.
//
// Exact-once is durable, not runtime: ActiveClaimData.ConsequencesApplied
// is persisted, so a replayed commit, a stale callback, or a reload can
// neither duplicate nor lose the effect.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;

namespace Desk42.Encounter
{
    /// <summary>Designer-owned bonus values, passed in from ShiftManager.</summary>
    public readonly struct ClaimBonusRates
    {
        public readonly int CrossClaim;
        public readonly int SequentialSynergy;

        public ClaimBonusRates(int crossClaim, int sequentialSynergy)
        {
            CrossClaim        = crossClaim;
            SequentialSynergy = sequentialSynergy;
        }

        /// <summary>Values matching the ShiftManager inspector defaults.</summary>
        public static ClaimBonusRates Default => new(5, 3);
    }

    /// <summary>What a commit actually awarded. Presentation reads this.</summary>
    public readonly struct AppliedClaimConsequences
    {
        public readonly int CrossClaimCredits;
        public readonly int SequentialSynergyCredits;
        public readonly string MemoFragmentId;

        public AppliedClaimConsequences(
            int crossClaimCredits, int sequentialSynergyCredits, string memoFragmentId)
        {
            CrossClaimCredits        = crossClaimCredits;
            SequentialSynergyCredits = sequentialSynergyCredits;
            MemoFragmentId           = memoFragmentId;
        }

        public int TotalCredits => CrossClaimCredits + SequentialSynergyCredits;
        public bool GeneratedMemo => !string.IsNullOrEmpty(MemoFragmentId);
    }

    public static class ClaimConsequencePolicy
    {
        /// <summary>
        /// Applies the deterministic persistent consequences of ONE committed
        /// claim. Bound to that claim by reference — never to a list tail.
        ///
        /// Idempotent via the persisted ConsequencesApplied marker, so this is
        /// safe to call again after a replayed commit or a reload.
        /// </summary>
        public static AppliedClaimConsequences Apply(
            ActiveClaimData committedClaim,
            RunStateController run,
            RunData runData,
            MetaProgressData meta,
            ClaimBonusRates rates)
        {
            if (committedClaim == null || run == null || runData == null)
                return default;

            // Durable exact-once. A runtime field could not survive the crash
            // boundary this exists to close.
            if (committedClaim.ConsequencesApplied)
                return default;

            committedClaim.ConsequencesApplied = true;

            ActiveClaimData previous = FindPreviousResolved(runData, committedClaim);

            int crossClaim = 0;
            int synergy    = 0;

            if (previous != null)
            {
                if (string.Equals(previous.ClientSpeciesId,
                        committedClaim.ClientSpeciesId, System.StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(committedClaim.ClientSpeciesId))
                {
                    crossClaim = rates.CrossClaim;
                    run.AddCredits(crossClaim);
                    Debug.Log($"[ClaimConsequence] Cross-claim deduction: +{crossClaim} " +
                              $"(same species: {committedClaim.ClientSpeciesId}) " +
                              $"for '{committedClaim.ClaimId}'.");
                }

                if (SharesTagCategory(committedClaim, previous))
                {
                    synergy = rates.SequentialSynergy;
                    run.AddCredits(synergy);
                    Debug.Log($"[ClaimConsequence] Sequential synergy: +{synergy} " +
                              $"({previous.ClaimId} -> {committedClaim.ClaimId}).");
                }
            }

            string memoId = TryGeneratePersistentMemo(committedClaim, run, runData, meta);

            return new AppliedClaimConsequences(crossClaim, synergy, memoId);
        }

        /// <summary>
        /// The resolved claim immediately preceding the committed one, located
        /// by the committed claim's own index. Never the list tail: a stale or
        /// out-of-order callback must not be able to retarget the comparison.
        /// </summary>
        private static ActiveClaimData FindPreviousResolved(
            RunData runData, ActiveClaimData committedClaim)
        {
            var resolved = runData.ResolvedClaims;
            if (resolved == null || resolved.Count < 2) return null;

            for (int i = resolved.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(resolved[i], committedClaim)) continue;
                return i > 0 ? resolved[i - 1] : null;
            }
            return null;
        }

        /// <summary>
        /// Persistent half of memo generation: the fragment and its ids. The
        /// UI notification stays deferred and is published by the caller.
        /// </summary>
        private static string TryGeneratePersistentMemo(
            ActiveClaimData claim,
            RunStateController run,
            RunData runData,
            MetaProgressData meta)
        {
            if (meta == null) return null;

            var fragment = MemoGenerator.TryGenerate(
                claim, run.ShiftNumber, runData, run.NarratorTone, run.MoralInjury);

            if (fragment == null) return null;

            meta.ConspiracyBoard.Fragments.Add(fragment);
            runData.GeneratedMemoIds ??= new List<string>();
            runData.GeneratedMemoIds.Add(fragment.FragmentId);

            Debug.Log($"[ClaimConsequence] Memo generated: {fragment.FragmentId} " +
                      $"for claim {claim.ClaimId}.");
            return fragment.FragmentId;
        }

        private static bool SharesTagCategory(ActiveClaimData a, ActiveClaimData b)
        {
            if (a?.AnomalyTagIds == null || a.AnomalyTagIds.Length == 0) return false;
            if (b?.AnomalyTagIds == null || b.AnomalyTagIds.Length == 0) return false;

            foreach (string tag in a.AnomalyTagIds)
            {
                if (string.IsNullOrEmpty(tag)) continue;
                foreach (string other in b.AnomalyTagIds)
                {
                    if (string.Equals(tag, other, System.StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }
    }
}
