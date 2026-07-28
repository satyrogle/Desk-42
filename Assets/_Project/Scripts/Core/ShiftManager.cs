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
        [SerializeField] private float _lunchBreakDuration = 5f;

        [Tooltip("Additional shift time (seconds) granted when Overtime begins.")]
        [SerializeField] private float _overtimeDuration   = 300f;

        [Tooltip("Pressure thresholds and office-hazard timing.")]
        [SerializeField] private TideTuningData _tideTuning;

        [Header("Sequential Synergy")]
        [Tooltip("Flat credit bonus awarded when two consecutive resolved claims share a tag category.")]
        [SerializeField] private int _sequentialSynergyBonus = 3;

        [Header("Cross-Claim Deduction")]
        [Tooltip("Flat credit bonus awarded when two consecutive resolved claims share a client species.")]
        [SerializeField] private int _crossClaimBonus = 5;

        [Header("UI")]
        [Tooltip("The passive-aggressive UI controller in this scene.")]
        [SerializeField] private PassiveAggressiveUIController _uiController;

        [Header("Unpaid Overtime")]
        [Tooltip("Shift number at which Unpaid Overtime loop activates (inclusive).")]
        [SerializeField] private int   _unpaidOvertimeShiftThreshold = 5;
        [Tooltip("Seconds removed from the base timer per overtime iteration.")]
        [SerializeField] private float _overtimeTimerReduction       = 30f;
        [Tooltip("Minimum timer duration floor for overtime iterations.")]
        [SerializeField] private float _overtimeTimerFloor           = 60f;
        [Tooltip("Hard cap on overtime iterations before forcing run end.")]
        [SerializeField] private int   _maxOvertimeIterations        = 3;

        /// <summary>
        /// Designer-owned bonus values consumed by the authoritative commit
        /// transaction. ShiftManager still OWNS the tunables; it no longer
        /// owns applying them.
        /// </summary>
        public Encounter.ClaimBonusRates BonusRates
            => new(_crossClaimBonus, _sequentialSynergyBonus, GetTagCategory);

        // ── State ─────────────────────────────────────────────

        private TideSystem _tide;
        private float      _lunchBreakTimer;
        private float      _encounterStartTime;   // Time.time when current encounter began

        private bool       _shiftEnding;          // guard against double EndShift calls
        private int        _overtimeIteration;     // how many overtime loops we've done this shift
        private int        _totalClaimsThisShift;  // hard safety cap
        private const int  ABSOLUTE_CLAIM_CAP = 30; // never exceed this many claims per shift

        // ── Unity Lifecycle ───────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_claimTemplates == null || _claimTemplates.Length == 0)
                Debug.LogWarning("[ShiftManager] No ClaimTemplateData assigned — shift will have no claims.", this);
            if (_anomalyTags == null || _anomalyTags.Length == 0)
                Debug.LogWarning("[ShiftManager] No AnomalyTagData assigned — anomaly tags unavailable.", this);
            if (_uiController == null)
                Debug.LogWarning("[ShiftManager] _uiController not assigned — passive-aggressive UI will be silent.", this);
            if (_tideTuning == null)
                Debug.LogWarning("[ShiftManager] _tideTuning is required.", this);
        }
#endif

        private void Awake()
        {
            Desk42Services.Register(this);
        }

        private void Start()
        {
            var run = GameManager.Instance?.Run;
            if (run == null)
            {
                Debug.LogError("[ShiftManager] No active RunStateController. " +
                               "ShiftManager requires an active run.");
                return;
            }

            if (_tideTuning == null)
            {
                Debug.LogError("[ShiftManager] Cannot start without TideTuningData.", this);
                return;
            }

            _tide = new TideSystem(_tideTuning);
            _tide.Initialize(run.ShiftNumber);

            SubscribeToRumorMill();

            var runData = run.RawData;

            // Validation builds start a fresh proof at Shift 1. Later shifts
            // reuse the same DontDestroyOnLoad proof state while each receives
            // a fresh RunData instance.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (run.ShiftNumber == 1
                && runData.PendingClaims.Count == 0
                && runData.ActiveClaim == null
                && GameManager.Instance?.EliasContent != null)
            {
                GameManager.Instance.EliasProof.BeginProofSession(
                    $"proof-{run.SeedCode}");
            }
#endif

            // Generate or restore claim queue
            if (runData.PendingClaims.Count == 0 && runData.ActiveClaim == null)
                GenerateInitialQueue(run.ShiftNumber, runData);

            // Bucket C Δ3A: reinsert unresolved carried work. Runs AFTER queue
            // generation so a fresh queue cannot discard it, and dedupes by
            // EncounterId so repeated generation, reload or scene
            // reconstruction cannot enqueue the same encounter twice.
            RestoreCarriedEncounters(runData);

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

            // Fire the queued event to kick off the first encounter.
            // A resolved ActiveClaim means the process ended between the
            // commit's save and the flow cleanup — the encounter is already
            // committed, so re-presenting it would resurrect a completed
            // encounter. Drop the slot and move on instead.
            if (runData.ActiveClaim != null && runData.ActiveClaim.IsResolved)
            {
                Debug.Log($"[ShiftManager] Discarding already-resolved active claim " +
                          $"'{runData.ActiveClaim.ClaimId}' on resume.");
                runData.ActiveClaim = null;
            }

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
            Desk42Services.Unregister(this);
        }

        // ── Update ────────────────────────────────────────────

        private void Update()
        {
            var run = GameManager.Instance?.Run;
            if (run == null || _shiftEnding) return;

            var runData = run.RawData;

            _tide.Tick(Time.deltaTime);
            OfficeEnvironmentState.Tick(Time.deltaTime);

            // During lunch: count down the break, skip timer
            if (runData.CurrentPhase == ShiftPhase.LunchBreak)
            {
                _lunchBreakTimer -= Time.deltaTime;
                if (_lunchBreakTimer <= 0f)
                    StartAfternoonBlock(run, runData);
                return;
            }

            // Drip-feed: Impatience timer disabled on Run 1
            int phase = GameManager.Phase;
            if (phase >= 2)
            {
                // Tick the impatience timer (handles Office Clock multiplier + grace period)
                if (run.TickTimer(Time.deltaTime))
                    OnTimerExpired(run, runData);
            }
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

            // Persistent encounter state is no longer owned here.
            // EncounterCommitService marks the claim resolved, appends it to
            // ResolvedClaims and clears ActiveClaim INSIDE the transaction,
            // before the save. Doing it here — after the authoritative save —
            // meant a crash or reload in between resurrected a completed
            // encounter as an unresolved active claim.
            //
            // This handler has NO persistent authority.
            //
            // Cross-claim bonus, sequential synergy and persistent memo state
            // are applied by ClaimConsequencePolicy inside the commit
            // transaction, before the save, bound to the committed claim.
            // Owning them here caused two defects: selecting ResolvedClaims[^1]
            // instead of the event's own claim let a stale event reapply the
            // latest claim's bonuses, and running after the save let a crash
            // lose an earned bonus. Both are closed by moving the authority,
            // not by guarding this callback harder.
            //
            // It is now safe for this handler to fire late, twice, or never.

            // Flow cleanup only. The claim is already persisted as resolved by
            // the commit transaction; clearing the desk slot here just frees it
            // for the next encounter.
            if (runData.ActiveClaim != null && runData.ActiveClaim.IsResolved)
                runData.ActiveClaim = null;

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

                // End the run — sanity bottomed out
                Debug.Log("[ShiftManager] Fugue triggered — ending run.");
                StartClockOut(GameManager.Instance?.Run);
            }
        }

        // ── Claim Queue ───────────────────────────────────────

        /// <summary>
        /// Puts carried (interrupted) encounters back at the front of the
        /// queue, deduped by EncounterId — never by claimant or display name,
        /// since one claimant may legitimately have several encounters.
        ///
        /// Front-of-queue is the narrowest deterministic ordering that makes
        /// carry-forward functional: outstanding work is dealt with before new
        /// work. No existing design rule is overridden and the scheduler is
        /// otherwise untouched.
        /// </summary>
        private void RestoreCarriedEncounters(RunData runData)
        {
            var meta = GameManager.Instance?.Meta;
            var ledger = meta?.CarriedEncounters;
            if (ledger == null || ledger.Count == 0) return;

            foreach (var carried in ledger.Canonical())
            {
                string encounterId = carried.EncounterId;

                // Already terminally resolved: release rather than re-present.
                if (meta.Encounters?.IsCompleted(encounterId) == true)
                {
                    ledger.Release(encounterId);
                    continue;
                }

                bool alreadyQueued =
                    (runData.ActiveClaim != null && string.Equals(
                        runData.ActiveClaim.EncounterId, encounterId,
                        System.StringComparison.Ordinal))
                    || runData.PendingClaims.Exists(c => c != null && string.Equals(
                        c.EncounterId, encounterId, System.StringComparison.Ordinal));

                if (alreadyQueued) continue;

                runData.PendingClaims.Insert(0, carried.Claim);
                Debug.Log($"[ShiftManager] Restored carried encounter " +
                          $"'{encounterId}' ({carried.ClientVariantId}).");
            }
        }

        private void GenerateInitialQueue(int shiftNumber, RunData runData)
        {
            if (_claimTemplates == null || _claimTemplates.Length == 0)
            {
                Debug.LogWarning("[ShiftManager] No ClaimTemplateData SOs assigned. " +
                                 "Assign them in the Shift scene Inspector.");
                return;
            }

            int morning   = ComputeQuotaWithVows(GameManager.Instance?.Run, _morningBlockQuota, shiftNumber);
            int afternoon = ComputeQuotaWithVows(GameManager.Instance?.Run, _afternoonBlockQuota, shiftNumber);
            int total     = morning + afternoon + 1; // +1 overflow buffer

            var claims = ClaimGenerator.GenerateQueue(
                total, shiftNumber,
                _claimTemplates, _anomalyTags,
                GameManager.Instance?.Meta);

            var gameManager = GameManager.Instance;
            if (gameManager?.EliasProof?.HasActiveSession == true)
            {
                EliasProofScheduler.TryReplaceScheduledClaim(
                    claims,
                    shiftNumber,
                    gameManager.EliasContent,
                    gameManager.EliasProof.State,
                    out _);

            }

            // Causal-confabulation control. Scheduled from the same queue as any
            // other claimant, on the shift between the Shift 2 cause and the
            // Shift 5 return.
            //
            // DELIBERATELY OUTSIDE the Elias-proof block. This call previously
            // sat inside it, so Mara only appeared when an Elias proof session
            // happened to be active — which made the control claimant depend on
            // the very thing she exists to be independent of. Her eligibility
            // must not consult EliasProofSessionState at all, and the fix is at
            // the call site: the scheduler itself was already proof-free.
            if (ControlClaimantContent.TryScheduleControlClaimant(
                    claims, shiftNumber, out var control))
            {
                Debug.Log($"[ShiftManager] Control claimant scheduled: " +
                          $"{control.ClaimantName} ({ControlClaimantContent.Anchor}).");
            }

            runData.PendingClaims.AddRange(claims);

            Debug.Log($"[ShiftManager] Generated {claims.Count} claims for shift {shiftNumber}.");
        }

        private void DequeueNextClaim(RunStateController run, RunData runData)
        {
            // ── Hard safety cap — never exceed ABSOLUTE_CLAIM_CAP per shift ──
            if (_totalClaimsThisShift >= ABSOLUTE_CLAIM_CAP)
            {
                Debug.LogWarning($"[ShiftManager] Absolute claim cap ({ABSOLUTE_CLAIM_CAP}) hit. Ending shift.");
                StartClockOut(run);
                return;
            }

            // ── Don't dequeue if the quota for this ante is already satisfied ──
            if (runData.ClaimsProcessedThisAnte >= runData.QuotaForCurrentAnte)
            {
                Debug.Log($"[ShiftManager] Ante quota met ({runData.ClaimsProcessedThisAnte}/{runData.QuotaForCurrentAnte}). Triggering phase transition.");
                OnAnteComplete(run, runData);
                return;
            }

            if (runData.PendingClaims.Count == 0)
            {
                // Only generate more if we genuinely need them to meet quota
                int remaining = runData.QuotaForCurrentAnte - runData.ClaimsProcessedThisAnte;
                if (remaining > 0 && runData.CurrentPhase != ShiftPhase.ClockOut)
                    GenerateMoreClaims(run.ShiftNumber, runData, count: Mathf.Min(remaining, 3));

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
            _totalClaimsThisShift++;

            _encounterStartTime = Time.time;
            _tide.NotifyEncounterStarted();

            RumorMill.PublishDeferred(
                new ClaimQueuedEvent(claim, runData.PendingClaims.Count));

            Debug.Log($"[ShiftManager] Dequeued: {claim.ClaimId} " +
                      $"({claim.ClientSpeciesId}). " +
                      $"Claim {_totalClaimsThisShift}/{runData.QuotaForCurrentAnte} this ante. " +
                      $"Remaining in queue: {runData.PendingClaims.Count}.");
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
            // All work phases (Morning, Afternoon, Overtime) use quota-based completion
            if (runData.CurrentPhase == ShiftPhase.MorningBlock
                || runData.CurrentPhase == ShiftPhase.AfternoonBlock
                || runData.CurrentPhase == ShiftPhase.Overtime)
            {
                return runData.ClaimsProcessedThisAnte >= runData.QuotaForCurrentAnte;
            }
            return false;
        }

        private void OnAnteComplete(RunStateController run, RunData runData)
        {
            switch (runData.CurrentPhase)
            {
                case ShiftPhase.MorningBlock:
                    StartLunchBreak(run, runData);
                    break;

                case ShiftPhase.AfternoonBlock:
                    if (run.ShiftNumber >= _unpaidOvertimeShiftThreshold)
                    {
                        StartUnpaidOvertimeLoop(run, runData);
                    }
                    else
                    {
                        StartClockOut(run);
                    }
                    break;

                case ShiftPhase.Overtime:
                    // Overtime quota met — end the shift
                    Debug.Log("[ShiftManager] Overtime quota met. Ending shift.");
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
                    ComputeQuotaWithVows(run, _morningBlockQuota, shift),
                ShiftPhase.AfternoonBlock =>
                    ComputeQuotaWithVows(run, _afternoonBlockQuota, shift),
                ShiftPhase.Overtime =>
                    ComputeQuotaWithVows(run, _afternoonBlockQuota, shift), // same target in overtime
                _ => runData.QuotaForCurrentAnte,
            };
        }

        private static int ComputeQuotaWithVows(RunStateController run, int baseQuota, int shiftNumber)
        {
            int quota = Mathf.Min(baseQuota + shiftNumber / 3, baseQuota + 4);
            int dqRank = run?.GetVowRank("double_quota") ?? 0;
            if (dqRank > 0)
                quota += dqRank * 2;
            return quota;
        }

        // ── Phase Transitions ─────────────────────────────────

        private void StartLunchBreak(RunStateController run, RunData runData)
        {
            run.AdvancePhase(ShiftPhase.LunchBreak);
            // Hardcoding this to 5s to bypass the 60s Inspector override that causes the game to appear frozen
            _lunchBreakTimer = 5f; 
            Debug.Log($"[ShiftManager] Lunch break started (5s).");
        }

        private void StartAfternoonBlock(RunStateController run, RunData runData)
        {
            runData.CurrentAnteNumber       = 2;
            runData.ClaimsProcessedThisAnte = 0;
            runData.QuotaForCurrentAnte     = ComputeQuotaWithVows(run, _afternoonBlockQuota, run.ShiftNumber);

            run.AdvancePhase(ShiftPhase.AfternoonBlock);
            _tide.OnPhaseChanged(ShiftPhase.AfternoonBlock);

            DequeueNextClaim(run, runData);
            Debug.Log("[ShiftManager] Afternoon block started.");
        }

        private void StartClockOut(RunStateController run)
        {
            if (_shiftEnding) return; // already ending — guard against re-entry
            if (run == null) return;

            // Bucket C Δ3A: the shift is ending. An unresolved claim at the desk
            // is interrupted work, not an abandoned one — mark it explicitly so
            // it survives the run boundary under its ORIGINAL EncounterId.
            // Interruption records no disposition and applies no consequences.
            var endingData = run.RawData;
            if (endingData?.ActiveClaim != null && !endingData.ActiveClaim.IsResolved)
            {
                Encounter.EncounterCommitService.InterruptEncounter(
                    endingData.ActiveClaim, endingData, GameManager.Instance?.Meta);
            }

            run.AdvancePhase(ShiftPhase.ClockOut);
            StartCoroutine(EndShiftSequence());
        }

        /// <summary>
        /// Unpaid Overtime loop (Phase 7): instead of ending, loop back into
        /// MorningBlock with escalated Tide, reduced timer, and fresh claims.
        /// The shift never ends until an ending condition fires.
        /// </summary>
        private void StartUnpaidOvertimeLoop(RunStateController run, RunData runData)
        {
            // Cap overtime iterations — the worker eventually breaks
            if (_overtimeIteration >= _maxOvertimeIterations)
            {
                Debug.Log($"[ShiftManager] Overtime cap reached ({_maxOvertimeIterations}) — ending run.");
                StartClockOut(run);
                return;
            }

            _overtimeIteration++;

            // Reset ante tracking for the new iteration
            runData.CurrentAnteNumber       = 1;
            runData.ClaimsProcessedThisAnte = 0;
            runData.QuotaForCurrentAnte     = ComputeQuotaWithVows(run,
                _morningBlockQuota + _overtimeIteration, run.ShiftNumber);

            // Generate more claims for the overtime loop
            GenerateMoreClaims(run.ShiftNumber, runData, count: runData.QuotaForCurrentAnte + 2);

            // Reduce timer each iteration (floor-clamped)
            float newTimer = Mathf.Max(
                _overtimeDuration - _overtimeIteration * _overtimeTimerReduction,
                _overtimeTimerFloor);
            run.ExtendTimer(newTimer);

            // Escalate Tide pressure
            _tide.ForceEscalate();

            // Transition back to MorningBlock
            run.AdvancePhase(ShiftPhase.MorningBlock);
            _tide.OnPhaseChanged(ShiftPhase.MorningBlock);

            DequeueNextClaim(run, runData);

            Debug.Log($"[ShiftManager] Unpaid Overtime iteration {_overtimeIteration}. " +
                      $"Timer: {newTimer:F0}s. Quota: {runData.QuotaForCurrentAnte}.");
        }

        private void OnTimerExpired(RunStateController run, RunData runData)
        {
            if (runData.CurrentPhase == ShiftPhase.Overtime)
            {
                if (run.ShiftNumber >= _unpaidOvertimeShiftThreshold)
                {
                    // Unpaid Overtime: loop back instead of ending
                    StartUnpaidOvertimeLoop(run, runData);
                }
                else
                {
                    Debug.Log("[ShiftManager] Overtime timer expired. Ending shift.");
                    StartClockOut(run);
                }
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

        private string GetTagCategory(string tagId)
        {
            if (_anomalyTags == null) return null;
            foreach (var tag in _anomalyTags)
                if (tag != null && tag.TagId == tagId)
                    return tag.TagCategory;
            return null;
        }

    }
}
