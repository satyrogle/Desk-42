// ============================================================
// DESK 42 — Milestone Tracker (MonoBehaviour)
//
// Centralised ending-condition watcher. Subscribes to RumorMill
// events and evaluates all ending conditions in one place,
// replacing the scattered checks previously in
// RunStateController.CompleteRun() and MoralInjurySystem.
//
// Ending Catalog:
//   TheCompanyMan    — Soul 0% + Efficiency >= 1500
//   TheBreach        — Callous-level moral scar
//   RedactedTruth    — Any other max-level scar
//   TheGreatAudit    — Conspiracy Board fully solved
//   TheWhistleblower — Leaked enough documents (Whistleblower archetype)
//
// Child of GameManager (DontDestroyOnLoad). Persists across
// scenes so it can track cross-shift state.
// ============================================================

using UnityEngine;

namespace Desk42.Core
{
    [DisallowMultipleComponent]
    public sealed class MilestoneTracker : MonoBehaviour
    {
        // ── Config ────────────────────────────────────────────

        [Header("Whistleblower Ending")]
        [Tooltip("Number of leaked documents required for the Whistleblower ending.")]
        [SerializeField] private int _leakThreshold = 5;

        // ── State ─────────────────────────────────────────────

        private int _leakedDocumentCount;

        // ── RumorMill Subscriptions ───────────────────────────

        private void OnEnable()
        {
            RumorMill.OnClaimResolved         += HandleClaimResolved;
            RumorMill.OnSoulIntegrityChanged  += HandleSoulChanged;
            RumorMill.OnMilestoneReached      += HandleMilestoneReached;
        }

        private void OnDisable()
        {
            RumorMill.OnClaimResolved         -= HandleClaimResolved;
            RumorMill.OnSoulIntegrityChanged  -= HandleSoulChanged;
            RumorMill.OnMilestoneReached      -= HandleMilestoneReached;
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Called by the Whistleblower archetype ability when a document is leaked.
        /// </summary>
        public void RecordLeakedDocument()
        {
            _leakedDocumentCount++;
            Debug.Log($"[MilestoneTracker] Leaked document #{_leakedDocumentCount}/{_leakThreshold}.");

            if (_leakedDocumentCount >= _leakThreshold)
            {
                var meta = GameManager.Instance?.Meta;
                if (meta != null && !meta.IsMilestoneUnlocked(MilestoneID.TheWhistleblower))
                {
                    RumorMill.PublishDeferred(new MilestoneReachedEvent(
                        MilestoneID.TheWhistleblower));
                }
            }
        }

        /// <summary>
        /// Evaluate all run-completion endings. Called by
        /// RunStateController.CompleteRun() after stats are finalised.
        /// </summary>
        public void EvaluateRunEndConditions(RunData runData, MetaProgressData meta)
        {
            if (runData == null || meta == null) return;

            // TheCompanyMan — perfect corporate drone: zero soul, peak efficiency
            if (Mathf.Approximately(runData.SoulIntegrity, 0f)
                && runData.Stats.EfficiencyRating >= 1500f
                && !meta.IsMilestoneUnlocked(MilestoneID.TheCompanyMan))
            {
                RumorMill.PublishDeferred(new MilestoneReachedEvent(MilestoneID.TheCompanyMan));
            }

            // TheGreatAudit — conspiracy board fully solved
            if (meta.ConspiracyBoard.GreatAuditUnlocked
                && !meta.IsMilestoneUnlocked(MilestoneID.TheGreatAudit))
            {
                RumorMill.PublishDeferred(new MilestoneReachedEvent(MilestoneID.TheGreatAudit));
            }
        }

        /// <summary>
        /// Reset per-run tracking state when a new run starts.
        /// </summary>
        public void ResetForNewRun()
        {
            _leakedDocumentCount = 0;
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleClaimResolved(ClaimResolvedEvent e)
        {
            // Future: track sequential patterns, total approvals/denials, etc.
        }

        private void HandleSoulChanged(SoulIntegrityChangedEvent e)
        {
            // TheBreach and RedactedTruth are already handled by
            // MoralInjurySystem when scars escalate — no need to duplicate.
            // This handler exists for future soul-threshold endings.
        }

        private void HandleMilestoneReached(MilestoneReachedEvent e)
        {
            var meta = GameManager.Instance?.Meta;
            if (meta == null) return;

            meta.UnlockMilestone(e.MilestoneId);
            SaveSystem.SaveMeta(meta);

            Debug.Log($"[MilestoneTracker] Milestone unlocked: {e.MilestoneId}.");
        }
    }
}

