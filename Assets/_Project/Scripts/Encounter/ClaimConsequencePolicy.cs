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

        /// <summary>
        /// Maps an anomaly tag id to its category. Sequential synergy matches on
        /// CATEGORY, not tag id — two different tags in the same category must
        /// still earn the bonus. Supplied by ShiftManager, which owns the
        /// AnomalyTagData assets.
        ///
        /// Null means NO authoritative category evidence exists, so category
        /// synergy fails closed. It does NOT fall back to raw id comparison:
        /// tag identity is not evidence that a tag belongs to an authored
        /// category, and the resolver-absent path is reachable in production
        /// via ClaimBonusRates.Default.
        /// </summary>
        public readonly System.Func<string, string> TagCategoryResolver;

        public ClaimBonusRates(int crossClaim, int sequentialSynergy,
            System.Func<string, string> tagCategoryResolver = null)
        {
            CrossClaim          = crossClaim;
            SequentialSynergy   = sequentialSynergy;
            TagCategoryResolver = tagCategoryResolver;
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

                if (SharesTagCategory(committedClaim, previous, rates.TagCategoryResolver))
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

        /// <summary>
        /// Matches on tag CATEGORY, mirroring the original ShiftManager rule.
        /// Comparing raw tag ids would silently narrow the bonus to identical
        /// tags and drop legitimate same-category pairs.
        /// </summary>
        /// <summary>
        /// Sequential category synergy: at least one anomaly tag from each
        /// claim must RESOLVE to an authored category, and those categories
        /// must be equal.
        ///
        /// There is deliberately no tag-identity shortcut. Two claims carrying
        /// the same tag id prove nothing on their own — an unknown tag is
        /// still unknown when it appears twice — and treating identity as a
        /// match awarded synergy for unresolved tags. Resolution must succeed
        /// first; equality is then checked on the resolved categories.
        ///
        /// Existential: the first qualifying pair wins and the caller awards
        /// the synergy once, regardless of how many pairs would qualify.
        /// </summary>
        private static bool SharesTagCategory(
            ActiveClaimData a, ActiveClaimData b,
            System.Func<string, string> categoryOf)
        {
            if (a?.AnomalyTagIds == null || a.AnomalyTagIds.Length == 0) return false;
            if (b?.AnomalyTagIds == null || b.AnomalyTagIds.Length == 0) return false;

            // No resolver means no authoritative category evidence at all.
            // Fail closed: other consequences still commit normally.
            if (categoryOf == null) return false;

            foreach (string tag in a.AnomalyTagIds)
            {
                if (string.IsNullOrWhiteSpace(tag)) continue;

                string catA = SafeCategory(categoryOf, tag);
                if (string.IsNullOrWhiteSpace(catA)) continue;   // unresolved -> no match

                foreach (string other in b.AnomalyTagIds)
                {
                    if (string.IsNullOrWhiteSpace(other)) continue;

                    string catB = SafeCategory(categoryOf, other);
                    if (string.IsNullOrWhiteSpace(catB)) continue;   // unresolved -> no match

                    if (string.Equals(catA, catB, System.StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Resolves a tag's authored category. A resolver that throws on a
        /// malformed id must not take down an encounter commit, so failure is
        /// treated as "unresolved", which contributes no match.
        /// </summary>
        private static string SafeCategory(
            System.Func<string, string> categoryOf, string tagId)
        {
            try { return categoryOf(tagId); }
            catch { return null; }
        }
    }
}
