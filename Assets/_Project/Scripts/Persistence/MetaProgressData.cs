// ============================================================
// DESK 42 — Meta Progress Data
//
// Persistent across ALL runs. Never lost on death.
// Saved to: {persistentDataPath}/meta.json
//
// Contains:
//   - Employee Handbook (permanent skill upgrades)
//   - Repeat Offender Database (adaptive AI counter-traits)
//   - Conspiracy Board fragments
//   - Narrative milestones reached
//   - Shift count, total credits earned, lifetime stats
//   - Cosmetic unlocks (Retirement Fund)
//   - Performance Review completion matrix
// ============================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Desk42.MoralInjury;

namespace Desk42.Core
{
    // ── Repeat Offender Database ──────────────────────────────

    [Serializable]
    public sealed class ClientTacticProfile
    {
        [JsonProperty] public string ClientVariantId;

        // How many times each card type has beaten this variant
        [JsonProperty] public Dictionary<PunchCardType, int> CardUsageCounts
            = new();

        // Counter-traits generated against this variant (SO asset GUIDs)
        [JsonProperty] public List<string> CounterTraitIds = new();

        // LEGACY — do not read, do not write.
        //
        // Superseded by MetaProgressData.Encounters, the authoritative encounter
        // history (handoff §3.5: TotalVisits derives from completed encounter
        // records; no second serialized count may exist).
        //
        // Retained only so pre-existing meta.json deserializes without loss. It
        // was never incremented in any shipped build — RecordVisit had zero
        // callers — so no real save carries a non-zero value.
        [JsonProperty] public int TotalVisits;

        public void RecordCardUsed(PunchCardType card)
        {
            CardUsageCounts.TryGetValue(card, out int count);
            CardUsageCounts[card] = count + 1;
        }

        public int GetCardUsage(PunchCardType card)
        {
            CardUsageCounts.TryGetValue(card, out int count);
            return count;
        }
    }

    // ── Employee Handbook (Skill Tree) ────────────────────────

    [Serializable]
    public sealed class EmployeeHandbookState
    {
        [JsonProperty] public HashSet<string> UnlockedBenefitIds = new();
        [JsonProperty] public int             TotalCreditsSpent;

        public bool HasBenefit(string id) => UnlockedBenefitIds.Contains(id);

        public void UnlockBenefit(string id, int cost)
        {
            UnlockedBenefitIds.Add(id);
            TotalCreditsSpent += cost;
        }
    }

    // ── Conspiracy Board ──────────────────────────────────────

    [Serializable]
    public sealed class PostItFragment
    {
        [JsonProperty] public string FragmentId;
        [JsonProperty] public string Content;        // generated text snippet
        [JsonProperty] public int    ShiftDiscovered;
        [JsonProperty] public bool   IsPlacedOnBoard;
        [JsonProperty] public float  BoardPositionX; // normalised 0-1
        [JsonProperty] public float  BoardPositionY;
    }

    [Serializable]
    public sealed class ConspiracyBoardState
    {
        [JsonProperty] public List<PostItFragment>         Fragments         = new();
        [JsonProperty] public List<(string, string)>       ConfirmedLinks    = new(); // fragment ID pairs
        [JsonProperty] public HashSet<string>              SolvedClusters    = new();
        [JsonProperty] public bool                         GreatAuditUnlocked;
    }

    // ── Cosmetic Unlocks (Retirement Fund) ────────────────────

    [Serializable]
    public sealed class CosmeticUnlocks
    {
        [JsonProperty] public HashSet<string> UnlockedDeskThemes     = new() { "default" };
        [JsonProperty] public HashSet<string> UnlockedWallpapers      = new() { "default" };
        [JsonProperty] public HashSet<string> UnlockedMugSkins        = new() { "default" };
        [JsonProperty] public HashSet<string> UnlockedStampDesigns    = new() { "default" };
        [JsonProperty] public HashSet<string> UnlockedDeskOrnaments   = new();
        [JsonProperty] public HashSet<string> UnlockedSupplyStickers  = new();
        [JsonProperty] public int             AuditPoints;            // secondary currency

        [JsonProperty] public string ActiveDeskTheme   = "default";
        [JsonProperty] public string ActiveWallpaper   = "default";
        [JsonProperty] public string ActiveMugSkin     = "default";
    }

    // ── Performance Review (Completion Matrix) ────────────────

    [Serializable]
    public sealed class ArchetypeVowCompletion
    {
        [JsonProperty] public string ArchetypeId;
        [JsonProperty] public int    HighestVowTierCompleted; // 0 = never, 1-8 = tier
        [JsonProperty] public bool[] VowTiersCompleted = new bool[8];
    }

    [Serializable]
    public sealed class PerformanceReviewState
    {
        // [archetypeId] -> completion data
        [JsonProperty] public Dictionary<string, ArchetypeVowCompletion> Completions = new();

        [JsonProperty] public HashSet<string> EarnedShiftBadgeIds = new();

        public void RecordCompletion(string archetypeId, int vowTier)
        {
            if (!Completions.ContainsKey(archetypeId))
                Completions[archetypeId] = new ArchetypeVowCompletion
                    { ArchetypeId = archetypeId };

            var c = Completions[archetypeId];
            if (vowTier >= 1 && vowTier <= 8)
            {
                c.VowTiersCompleted[vowTier - 1] = true;
                if (vowTier > c.HighestVowTierCompleted)
                    c.HighestVowTierCompleted = vowTier;
            }
        }
    }

    // ── Lifetime Stats ────────────────────────────────────────

    [Serializable]
    public sealed class LifetimeStats
    {
        [JsonProperty] public int   TotalShiftsCompleted;
        [JsonProperty] public int   TotalShiftsFailed;
        [JsonProperty] public int   TotalClaimsProcessed;
        [JsonProperty] public int   TotalApprovedClaims;
        [JsonProperty] public int   TotalDeniedClaims;
        [JsonProperty] public int   TotalLiquifiedClaims;
        [JsonProperty] public int   LegacyTotalHumaneResolutions;
        [JsonProperty] public int   LegacyTotalBureaucraticResolutions;

        // Load-only v2 aliases. These totals remain legacy data and stop accruing.
        [JsonProperty("TotalHumaneResolutions")]
        private int LegacyTotalHumaneResolutionsV2
        {
            set => LegacyTotalHumaneResolutions = value;
        }

        [JsonProperty("TotalBureaucraticResolutions")]
        private int LegacyTotalBureaucraticResolutionsV2
        {
            set => LegacyTotalBureaucraticResolutions = value;
        }
        [JsonProperty] public int   TotalNDAsSigned;
        [JsonProperty] public int   TotalCardSlams;
        [JsonProperty] public float LowestSoulIntegrityReached;  // lifetime minimum
        [JsonProperty] public int   TotalFugueStatesSurvived;
        [JsonProperty] public long  TotalPlayTimeSeconds;
    }

    // ── Root Meta Progress Data ───────────────────────────────

    [Serializable]
    public sealed class MetaProgressData
    {
        [JsonProperty] public int    SaveVersion = 3;      // for migration
        [JsonProperty] public string LastSavedUtc;

        // Repeat Offender Database — keyed by client variant ID (SO GUID)
        [JsonProperty] public Dictionary<string, ClientTacticProfile> RepeatOffenderDB
            = new();

        [JsonProperty] public EmployeeHandbookState   EmployeeHandbook  = new();
        [JsonProperty] public ConspiracyBoardState    ConspiracyBoard   = new();
        [JsonProperty] public CosmeticUnlocks         Cosmetics         = new();
        [JsonProperty] public PerformanceReviewState  PerformanceReview = new();
        [JsonProperty] public LifetimeStats           LifetimeStats     = new();

        // Milestones unlocked (IDs of MilestoneID enum values)
        [JsonProperty] public HashSet<string>         UnlockedMilestones = new();

        // Moral Injury scars — worst scar per UnethicalActionType key
        // Value: ScarLevel enum as string for JSON stability
        [JsonProperty] public Dictionary<string, MoralInjury.ScarLevel> ActiveScars = new();

        // Total Corporate Credits available to spend in Internal Audit
        [JsonProperty] public int   BankBalance;

        // Which shift number we're on across ALL runs
        [JsonProperty] public int   GlobalShiftNumber;

        // First-run tutorial completion flag (cross-run)
        [JsonProperty] public bool  TutorialCompleted;

        // ── Phase 8/9 Additions ───────────────────────────────

        // Drip-feed progression (null means pre-v2 save, defaults to 4 on migration)
        [JsonProperty] public int?   HighestPhaseReached;
        
        // Dark Intelligence Layer 3/4
        [JsonProperty] public HashSet<string> DarkIntelligenceUnlocks = new();
        [JsonProperty] public int             ExeFragmentsCollected;
        [JsonProperty] public bool            HasEscapedTheLoop;

        // Rogue Subroutine — when the player last claimed the
        // hidden bounty (used as a per-session cooldown so the
        // pixel doesn't farm fragments infinitely).
        [JsonProperty] public int             RogueSubroutineLastClaimedShift = -1;

        // ── Encounter History (authoritative — handoff §3.5) ──

        /// <summary>
        /// The single source of truth for presentations and completed visits.
        /// Lives here rather than in RunData because recurrence must survive a
        /// run boundary AND an application restart.
        /// </summary>
        [JsonProperty] public EncounterHistory Encounters = new();

        /// <summary>
        /// Approval liabilities keyed by source EncounterId (Bucket C Δ2).
        /// Campaign-persistent so a liability survives run boundary, save/load
        /// and restart. Additive: legacy meta.json loads with an empty ledger
        /// and NO liability is backfilled from historical approvals.
        /// </summary>
        [JsonProperty] public ApprovalLiabilityLedger ApprovalLiabilities = new();

        // ── Five-Shift Proof State (persisted — Bucket 2) ─────

        /// <summary>
        /// The live authored-proof session. Previously owned by
        /// EliasProofSessionController and session-only, so an application
        /// restart silently destroyed the Shift 2 -> Shift 5 causal chain.
        /// EliasProofSessionController is now a runtime façade over this.
        /// </summary>
        [JsonProperty] public EliasProofSessionState EliasProof = new();

        /// <summary>
        /// Proof sessions that reached their terminal disposition, retained as
        /// evidence. EndProofSession ARCHIVES into this list rather than
        /// erasing, so attribution and post-test inspection survive the
        /// session boundary.
        /// </summary>
        [JsonProperty] public List<EliasProofSessionState> CompletedProofSessions = new();

        /// <summary>
        /// Completed visits by this claimant, DERIVED from encounter history.
        /// Replaces the never-incremented ClientTacticProfile.TotalVisits.
        /// </summary>
        public int GetTotalVisits(string clientVariantId)
            => Encounters?.TotalVisits(clientVariantId) ?? 0;

        /// <summary>Presentations of this claimant, derived from history.</summary>
        public int GetTotalPresentations(string clientVariantId)
            => Encounters?.TotalPresentations(clientVariantId) ?? 0;

        // ── Repeat Offender Helpers ───────────────────────────

        public ClientTacticProfile GetOrCreateProfile(string clientVariantId)
        {
            if (!RepeatOffenderDB.TryGetValue(clientVariantId, out var profile))
            {
                profile = new ClientTacticProfile { ClientVariantId = clientVariantId };
                RepeatOffenderDB[clientVariantId] = profile;
            }
            return profile;
        }

        /// <summary>
        /// REMOVED — visits are derived, not counted (handoff §3.5).
        ///
        /// This had zero callers at a432a8b, so ClientTacticProfile.TotalVisits
        /// was never incremented and every claimant read as a first-time visitor
        /// forever. It is kept as a compile-time error rather than deleted so any
        /// attempt to reintroduce an independent visit counter fails loudly.
        ///
        /// Use EncounterCommitService.CommitEncounterResult to complete a visit,
        /// and GetTotalVisits(clientVariantId) to read one.
        /// </summary>
        [Obsolete("Visits derive from MetaProgressData.Encounters. Commit via " +
                  "EncounterCommitService.CommitEncounterResult; read via GetTotalVisits.",
                  error: true)]
        public void RecordVisit(string clientVariantId)
            => throw new InvalidOperationException(
                "RecordVisit is removed. Visits derive from encounter history.");

        public void RecordCardUsed(string clientVariantId, PunchCardType card)
            => GetOrCreateProfile(clientVariantId).RecordCardUsed(card);

        public bool HasCounterTrait(string clientVariantId, string traitId)
        {
            if (RepeatOffenderDB.TryGetValue(clientVariantId, out var p))
                return p.CounterTraitIds.Contains(traitId);
            return false;
        }

        public void AddCounterTrait(string clientVariantId, string traitId)
            => GetOrCreateProfile(clientVariantId).CounterTraitIds.Add(traitId);

        // ── Milestone Helpers ────────────────────────────────

        public bool IsMilestoneUnlocked(MilestoneID id)
            => UnlockedMilestones.Contains(id.ToString());

        public void UnlockMilestone(MilestoneID id)
            => UnlockedMilestones.Add(id.ToString());

        // ── Timestamp ────────────────────────────────────────

        public void RefreshTimestamp()
            => LastSavedUtc = DateTime.UtcNow.ToString("o");
    }
}
