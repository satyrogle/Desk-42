// ============================================================
// DESK 42 — Shift Manager (MonoBehaviour)
//
// Lives in the Shift scene. Orchestrates the entire shift loop:
//
//   1. At scene load: restore or generate the claim queue.
//   2. Per-frame: tick the impatience timer and TideSystem.
//   3. On claim resolution: advance queue, check ante quota,
//      transition phases, try to generate a conspiracy memo.
//   4. On timer expiry: enter Overtime.
//   5. On quota completion: end the shift.
//
// Phase flow:
//   ClockIn → MorningBlock → LunchBreak → AfternoonBlock
//     → ClockOut (quota met)
//     → Overtime   (timer expired mid-block) → ClockOut
//
// Ante system:
//   Morning block and afternoon block each have an independent
//   claim quota. Quotas scale up slightly with GlobalShiftNumber.
//
// Encounter protocol (future EncounterManager integration):
//   ShiftManager fires ClaimQueuedEvent when a new encounter
//   is ready. The encounter system subscribes and spawns the
//   client. When the encounter ends, the encounter system
//   publishes ClaimResolvedEvent — ShiftManager picks it up
//   and advances the queue.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Desk42.Claims;
using Desk42.UI;

namespace Desk42.Core
{
    [DisallowMultipleComponent]
    public sealed class ShiftManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────

        [Header("Claim Data")]
        [Tooltip("All ClaimTemplateData SOs available this shift.")]
        [SerializeField] private ClaimTemplateData[] _claimTemplates;

        [Tooltip("All AnomalyTagData SOs available this shift.")]
        [SerializeField] private AnomalyTagData[]    _anomalyTags;

        [Header("Shift Config")]
        [Tooltip("Baseline claims required to complete the morning block (ante 1).")]
        [SerializeField] private int _morningBlockQuota    = 3;

        [Tooltip("Baseline claims required to complete the afternoon block (ante 2).")]
        [SerializeField] private int _afternoonBlockQuota  = 4;

        [Tooltip("Duration of the lunch break in seconds.")]
        [SerializeField] private float _lunchBreakDuration = 60f;

        [Tooltip("Additional shift time (seconds) granted when Overtime begins.")]
        [SerializeField] private float _overtimeDuration   = 300f;

        [Header("UI")]
        [Tooltip("The passive-aggressive UI controller in this scene.")]
        [SerializeField] private PassiveAggressiveUIController _uiController;

        // ── State ─────────────────────────────────────────────

        private TideSystem _tide;
        private float      _lunchBreakTimer;
        private float      _encounterStartTime;   // Time.time when current encounter began
        private bool       _shiftEnding;          // guard against double EndShift calls

        // ── Unity Lifecycle ───────────────────────────────────

        private void Start()
        {
            var run = GameManager.Instance?.Run;
            if (run == null)
            {
                Debug.LogError("[ShiftManager] No active RunStateController. " +
                               "ShiftManager requires an active run.");
                return;
            }

            _tide = new TideSystem();
            _tide.Initialize(run.ShiftNumber);

            SubscribeToRumorMill();

            var runData = run.RawData;

            // Generate or restore claim queue
            if (runData.PendingClaims.Count == 0 && runData.ActiveClaim == null)
                GenerateInitialQueue(run.ShiftNumber, runData);

            // Ensure the ante quota is set for the current phase
            SyncAnteQuota(run, runData);

            // Advance from the boot ClockIn phase immediately
            if (runData.CurrentPhase == ShiftPhase.ClockIn)
            {
                run.AdvancePhase(ShiftPhase.MorningBlock);
                _tide.OnPhaseChanged(ShiftPhase.MorningBlock);
            }

            // Tell the UI how many clients are expected
            int total = runData.PendingClaims.Count
                        + (runData.ActiveClaim != null ? 1 : 0);
            _uiController?.SetClientTotal(total);

            // Fire the queued event to kick off the first encounter
            if (runData.ActiveClaim != null)
            {
                // Resume mid-encounter — re-signal to the encounter system
                _encounterStartTime = Time.time; // approximate; fine for pressure tracking
                RumorMill.PublishDeferred(
                    new ClaimQueuedEvent(runData.ActiveClaim, runData.PendingClaims.Count));
            }
            else
            {
                DequeueNextClaim(run, runData);
            }

            Debug.Log($"[ShiftManager] Started. Phase: {runData.CurrentPhase}, " +
                      $"Queue: {runData.PendingClaims.Count} claims, " +
                      $"Quota: {runData.ClaimsProcessedThisAnte}/{runData.QuotaForCurrentAnte}.");
        }

        private void OnDestroy()
        {
            UnsubscribeFromRumorMill();
        }

        // ── Update ────────────────────────────────────────────

        private void Update()
        {
            var run = GameManager.Instance?.Run;
            if (run == null || _shiftEnding) return;

            var runData = run.RawData;

            _tide.Tick(Time.deltaTime);

            // During lunch: count down the break, skip timer
            if (runData.CurrentPhase == ShiftPhase.LunchBreak)
            {
                _lunchBreakTimer -= Time.deltaTime;
                if (_lunchBreakTimer <= 0f)
                    StartAfternoonBlock(run, runData);
                return;
            }

            // Tick the impatience timer (handles Office Clock multiplier + grace period)
            if (run.TickTimer(Time.deltaTime))
                OnTimerExpired(run, runData);
        }

        // ── Public API (for encounter system) ─────────────────

        /// <summary>
        /// Called by the encounter system when a claim has been accepted
        /// and NDA requirements are satisfied, making the claim ready
        /// for the player to work on.
        /// </summary>
        public ActiveClaimData GetActiveClaim()
            => GameManager.Instance?.Run?.RawData?.ActiveClaim;

        // ── RumorMill ─────────────────────────────────────────

        private void SubscribeToRumorMill()
        {
            RumorMill.OnClaimResolved += HandleClaimResolved;
            RumorMill.OnSanityChanged += HandleSanityChanged;
        }

        private void UnsubscribeFromRumorMill()
        {
            RumorMill.OnClaimResolved -= HandleClaimResolved;
            RumorMill.OnSanityChanged -= HandleSanityChanged;
        }

        private void HandleClaimResolved(ClaimResolvedEvent e)
        {
            var run = GameManager.Instance?.Run;
            if (run == null) return;
            var runData = run.RawData;

            // Record encounter timing for Tide pressure calculation
            float duration = Time.time - _encounterStartTime;
            _tide.NotifyClaimResolved(duration, triggeredFugue: false);

            // Move the active claim to resolved
            if (runData.ActiveClaim != null)
            {
                TryGenerateMemo(runData.ActiveClaim, run, runData);

                runData.ActiveClaim.IsResolved = true;
                runData.ActiveClaim.WasHumane  = !e.ResolvedCorrectly;
                runData.ResolvedClaims.Add(runData.ActiveClaim);
                runData.ActiveClaim = null;
            }

            // Check whether the current ante is now complete
            if (IsAnteComplete(runData))
            {
                OnAnteComplete(run, runData);
                return; // phase transition handles claim dequeue
            }

            // Continue in the current block
            if (runData.CurrentPhase == ShiftPhase.MorningBlock  ||
                runData.CurrentPhase == ShiftPhase.AfternoonBlock ||
                runData.CurrentPhase == ShiftPhase.Overtime)
            {
                DequeueNextClaim(run, runData);
            }
        }

        private void HandleSanityChanged(SanityChangedEvent e)
        {
            if (e.TriggeredFugue)
            {
                // Let the Tide know the player hit a fugue state
                _tide.NotifyClaimResolved(durationSeconds: float.MaxValue, triggeredFugue: true);
            }
        }

        // ── Claim Queue ───────────────────────────────────────

        private void GenerateInitialQueue(int shiftNumber, RunData runData)
        {
            if (_claimTemplates == null || _claimTemplates.Length == 0)
            {
                Debug.LogWarning("[ShiftManager] No ClaimTemplateData SOs assigned. " +
                                 "Assign them in the Shift scene Inspector.");
                return;
            }

            int morning   = ComputeQuota(_morningBlockQuota, shiftNumber);
            int afternoon = ComputeQuota(_afternoonBlockQuota, shiftNumber);
            int total     = morning + afternoon + 1; // +1 overflow buffer

            var claims = ClaimGenerator.GenerateQueue(
                total, shiftNumber,
                _claimTemplates, _anomalyTags,
                GameManager.Instance?.Meta);

            runData.PendingClaims.AddRange(claims);

            Debug.Log($"[ShiftManager] Generated {claims.Count} claims for shift {shiftNumber}.");
        }

        private void DequeueNextClaim(RunStateController run, RunData runData)
        {
            if (runData.PendingClaims.Count == 0)
            {
                // Overflow: try to generate more claims if we haven't hit ClockOut
                if (runData.CurrentPhase != ShiftPhase.ClockOut)
                    GenerateMoreClaims(run.ShiftNumber, runData, count: 2);

                if (runData.PendingClaims.Count == 0)
                {
                    Debug.Log("[ShiftManager] Queue exhausted — checking shift completion.");
                    CheckForEarlyCompletion(run, runData);
                    return;
                }
            }

            // Pop from the front of the queue
            var claim = runData.PendingClaims[0];
            runData.PendingClaims.RemoveAt(0);
            runData.ActiveClaim = claim;

            _encounterStartTime = Time.time;
            _tide.NotifyEncounterStarted();

            RumorMill.PublishDeferred(
                new ClaimQueuedEvent(claim, runData.PendingClaims.Count));

            Debug.Log($"[ShiftManager] Dequeued: {claim.ClaimId} " +
                      $"({claim.ClientSpeciesId}). " +
                      $"Remaining: {runData.PendingClaims.Count}.");
        }

        private void GenerateMoreClaims(int shiftNumber, RunData runData, int count)
        {
            if (_claimTemplates == null || _claimTemplates.Length == 0) return;

            var extra = ClaimGenerator.GenerateQueue(
                count, shiftNumber,
                _claimTemplates, _anomalyTags,
                GameManager.Instance?.Meta);

            runData.PendingClaims.AddRange(extra);
            Debug.Log($"[ShiftManager] Generated {extra.Count} additional claims.");
        }

        // ── Ante / Phase Logic ────────────────────────────────

        private bool IsAnteComplete(RunData runData)
        {
            return runData.CurrentPhase == ShiftPhase.MorningBlock
                || runData.CurrentPhase == ShiftPhase.AfternoonBlock
                ? runData.ClaimsProcessedThisAnte >= runData.QuotaForCurrentAnte
                : false; // Overtime doesn't have a quota-based completion
        }

        private void OnAnteComplete(RunStateController run, RunData runData)
        {
            switch (runData.CurrentPhase)
            {
                case ShiftPhase.MorningBlock:
                    StartLunchBreak(run, runData);
                    break;

                case ShiftPhase.AfternoonBlock:
                    StartClockOut(run);
                    break;
            }
        }

        private void SyncAnteQuota(RunStateController run, RunData runData)
        {
            int shift = run.ShiftNumber;
            runData.QuotaForCurrentAnte = runData.CurrentPhase switch
            {
                ShiftPhase.ClockIn or ShiftPhase.MorningBlock =>
                    ComputeQuota(_morningBlockQuota, shift),
                ShiftPhase.AfternoonBlock =>
                    ComputeQuota(_afternoonBlockQuota, shift),
                ShiftPhase.Overtime =>
                    ComputeQuota(_afternoonBlockQuota, shift), // same target in overtime
                _ => runData.QuotaForCurrentAnte,
            };
        }

        private static int ComputeQuota(int baseQuota, int shiftNumber)
            => Mathf.Min(baseQuota + shiftNumber / 3, baseQuota + 4);

        // ── Phase Transitions ─────────────────────────────────

        private void StartLunchBreak(RunStateController run, RunData runData)
        {
            run.AdvancePhase(ShiftPhase.LunchBreak);
            _lunchBreakTimer = _lunchBreakDuration;
            Debug.Log($"[ShiftManager] Lunch break started ({_lunchBreakDuration}s).");
        }

        private void StartAfternoonBlock(RunStateController run, RunData runData)
        {
            runData.CurrentAnteNumber       = 2;
            runData.ClaimsProcessedThisAnte = 0;
            runData.QuotaForCurrentAnte     = ComputeQuota(_afternoonBlockQuota, run.ShiftNumber);

            run.AdvancePhase(ShiftPhase.AfternoonBlock);
            _tide.OnPhaseChanged(ShiftPhase.AfternoonBlock);

            DequeueNextClaim(run, runData);
            Debug.Log("[ShiftManager] Afternoon block started.");
        }

        private void StartClockOut(RunStateController run)
        {
            run.AdvancePhase(ShiftPhase.ClockOut);
            StartCoroutine(EndShiftSequence());
        }

        private void OnTimerExpired(RunStateController run, RunData runData)
        {
            if (runData.CurrentPhase == ShiftPhase.Overtime)
            {
                // Timer expired in overtime — Phase 7 will handle the death state.
                // For now: end the shift as if clocked out.
                Debug.Log("[ShiftManager] Overtime timer expired. Ending shift.");
                StartClockOut(run);
                return;
            }

            // Enter Overtime: extend timer, escalate Tide
            Debug.Log("[ShiftManager] Timer expired — entering Overtime.");
            run.AdvancePhase(ShiftPhase.Overtime);
            run.ExtendTimer(_overtimeDuration);
            _tide.OnPhaseChanged(ShiftPhase.Overtime);
        }

        private void CheckForEarlyCompletion(RunStateController run, RunData runData)
        {
            // Queue empty before quota met — this shouldn't happen in a well-tuned shift,
            // but gracefully end the block if it does.
            if (runData.CurrentPhase == ShiftPhase.MorningBlock)
            {
                Debug.LogWarning("[ShiftManager] Morning queue exhausted before quota. " +
                                 "Transitioning to lunch.");
                StartLunchBreak(run, runData);
            }
            else if (runData.CurrentPhase == ShiftPhase.AfternoonBlock ||
                     runData.CurrentPhase == ShiftPhase.Overtime)
            {
                Debug.LogWarning("[ShiftManager] Afternoon queue exhausted. Ending shift.");
                StartClockOut(run);
            }
        }

        private IEnumerator EndShiftSequence()
        {
            if (_shiftEnding) yield break;
            _shiftEnding = true;

            // Brief pause for end-of-shift UI (stamp sound, result card, etc.)
            yield return new WaitForSeconds(2f);

            GameManager.Instance?.EndShift();
        }

        // ── Memo Generation ───────────────────────────────────

        private void TryGenerateMemo(ActiveClaimData claim,
            RunStateController run, RunData runData)
        {
            var meta = GameManager.Instance?.Meta;
            if (meta == null) return;

            var fragment = MemoGenerator.TryGenerate(
                claim,
                run.ShiftNumber,
                runData,
                run.NarratorTone,
                run.MoralInjury);

            if (fragment == null) return;

            meta.ConspiracyBoard.Fragments.Add(fragment);
            runData.GeneratedMemoIds.Add(fragment.FragmentId);

            Debug.Log($"[ShiftManager] Memo generated: {fragment.FragmentId} " +
                      $"for claim {claim.ClaimId}.");
        }
    }
}
