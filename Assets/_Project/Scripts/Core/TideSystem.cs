// ============================================================
// DESK 42 — Tide System
//
// The office's institutional resistance — the ambient pressure
// that pushes back when the player is too efficient.
//
// Design:
//   The Tide tracks how fast the player resolves claims. Three
//   consecutive "fast" resolutions (< FAST_THRESHOLD seconds)
//   increment the pressure level (0-3). Each pressure level
//   shortens the hazard interval and unlocks more severe hazards.
//
//   Pressure does NOT auto-decay. It only drops when the player
//   struggles (fugue state triggered, or explicit Relief events).
//
// Hazard intervals by pressure level:
//   0 → 240s   calm — printer jam, coffee machine
//   1 → 180s   building — + mandatory meeting
//   2 → 120s   intense — + fire drill
//   3 →  90s   overwhelming — + system crash, unscheduled audit
//
// Hazard type pools by shift phase:
//   Morning:   light (PrinterJam, CoffeeMachineDown)
//   Afternoon: medium (MandatoryMeeting, PrinterJam, FireDrill)
//   Overtime:  heavy (SystemCrash, MandatoryMeeting, UnscheduledAudit)
//
// Not a MonoBehaviour. Owned and ticked by ShiftManager.
// ShiftManager calls NotifyClaimResolved / OnPhaseChanged / etc.
// TideSystem publishes OfficeHazardEvent and TideEscalatedEvent
// directly to RumorMill (same pattern as MoralInjurySystem).
// ============================================================

using UnityEngine;

namespace Desk42.Core
{
    public sealed class TideSystem
    {
        // ── Constants ─────────────────────────────────────────

        // Encounter faster than this (seconds) counts as overperformance
        private const float FAST_THRESHOLD          = 90f;

        // Consecutive fast resolutions needed to bump pressure
        private const int   FAST_STREAK_THRESHOLD   = 3;

        // Hazard intervals (seconds) indexed by pressure level 0-3
        private static readonly float[] HazardIntervals = { 240f, 180f, 120f, 90f };

        // Minimum gap between any two hazards regardless of pressure
        private const float MIN_HAZARD_SPACING = 60f;

        // Overtime hazard interval (always intense)
        private const float OVERTIME_HAZARD_INTERVAL = 60f;

        // ── State ─────────────────────────────────────────────

        private int   _pressureLevel;             // 0-3
        private int   _consecutiveFastCount;
        private float _timeSinceLastHazard;
        private float _nextHazardIn;
        private ShiftPhase _currentPhase = ShiftPhase.ClockIn;

        // ── Queries ───────────────────────────────────────────

        public int PressureLevel => _pressureLevel;

        // ── Lifecycle ─────────────────────────────────────────

        /// <summary>
        /// Call once when the shift begins. ShiftNumber scales the base interval
        /// so later shifts are slightly more hostile even at pressure 0.
        /// </summary>
        public void Initialize(int shiftNumber)
        {
            _pressureLevel         = 0;
            _consecutiveFastCount  = 0;
            _timeSinceLastHazard   = 0f;

            // Start first hazard on a somewhat randomised delay so shift 1 isn't instant
            float baseInterval = HazardIntervals[0];
            float shiftBonus   = Mathf.Max(0, (shiftNumber - 1) * 10f); // 10s faster per shift
            _nextHazardIn = Mathf.Max(MIN_HAZARD_SPACING * 2f,
                                      baseInterval - shiftBonus);

            Debug.Log($"[TideSystem] Initialized. Shift {shiftNumber}. " +
                      $"First hazard in {_nextHazardIn:F0}s.");
        }

        /// <summary>Called every frame by ShiftManager.Update().</summary>
        public void Tick(float dt)
        {
            _timeSinceLastHazard += dt;

            if (_timeSinceLastHazard < _nextHazardIn) return;
            if (_timeSinceLastHazard < MIN_HAZARD_SPACING) return;

            FireHazard();
        }

        /// <summary>Called by ShiftManager when a new encounter begins.</summary>
        public void NotifyEncounterStarted()
        {
            // Nothing to update per-encounter at present;
            // kept as a hook for future encounter-scoped pressure logic.
        }

        /// <summary>
        /// Called by ShiftManager after a claim is resolved.
        /// <paramref name="durationSeconds"/> is the time from encounter start to resolution.
        /// </summary>
        public void NotifyClaimResolved(float durationSeconds, bool triggeredFugue)
        {
            if (triggeredFugue)
            {
                // Player is clearly struggling — reduce pressure as relief
                if (_pressureLevel > 0)
                    SetPressureLevel(_pressureLevel - 1, isOverperformance: false);
                _consecutiveFastCount = 0;
                return;
            }

            if (durationSeconds < FAST_THRESHOLD)
            {
                _consecutiveFastCount++;
                if (_consecutiveFastCount >= FAST_STREAK_THRESHOLD)
                {
                    _consecutiveFastCount = 0;
                    if (_pressureLevel < 3)
                        SetPressureLevel(_pressureLevel + 1, isOverperformance: true);
                }
            }
            else
            {
                // Not a fast resolution — reset streak
                _consecutiveFastCount = 0;
            }
        }

        /// <summary>Called by ShiftManager when the ShiftPhase changes.</summary>
        public void OnPhaseChanged(ShiftPhase newPhase)
        {
            _currentPhase = newPhase;

            // Overtime always jumps to pressure 2+ and uses its own interval
            if (newPhase == ShiftPhase.Overtime && _pressureLevel < 2)
                SetPressureLevel(2, isOverperformance: true);

            ScheduleNextHazard();

            Debug.Log($"[TideSystem] Phase changed to {newPhase}. " +
                      $"Pressure: {_pressureLevel}.");
        }

        public void Reset()
        {
            _pressureLevel        = 0;
            _consecutiveFastCount = 0;
            _timeSinceLastHazard  = 0f;
            _nextHazardIn         = HazardIntervals[0];
        }

        // ── Private ───────────────────────────────────────────

        private void FireHazard()
        {
            var type = PickHazard();
            bool isOverperf = _pressureLevel >= 2;

            Debug.Log($"[TideSystem] Hazard fired: {type}. " +
                      $"Pressure: {_pressureLevel}, overperf: {isOverperf}.");

            RumorMill.PublishDeferred(
                new OfficeHazardEvent(type, duration: 0f, isOverperf));

            _timeSinceLastHazard = 0f;
            ScheduleNextHazard();
        }

        private void ScheduleNextHazard()
        {
            float interval = _currentPhase == ShiftPhase.Overtime
                ? OVERTIME_HAZARD_INTERVAL
                : HazardIntervals[Mathf.Clamp(_pressureLevel, 0, 3)];

            // Small random jitter ±15% so hazards don't feel mechanical
            float jitter  = SeedEngine.NextFloat(SeedStream.RumorMillEvents, -0.15f, 0.15f);
            _nextHazardIn = interval * (1f + jitter);
        }

        private OfficeHazardType PickHazard()
        {
            // Hazard pools and weights indexed by phase + pressure
            if (_currentPhase == ShiftPhase.Overtime)
            {
                // Overtime: always heavy rotation
                float[] w = { 4f, 3f, 2.5f };  // SystemCrash, MandatoryMeeting, UnscheduledAudit
                return (OfficeHazardType)PickFromWeighted(w,
                    new[] { OfficeHazardType.SystemCrash,
                            OfficeHazardType.MandatoryMeeting,
                            OfficeHazardType.UnscheduledAudit });
            }

            if (_currentPhase == ShiftPhase.AfternoonBlock)
            {
                float[] w = _pressureLevel >= 2
                    ? new[] { 3f, 3f, 2f, 1.5f }  // high pressure
                    : new[] { 4f, 3f, 1f, 0f };    // low pressure
                return (OfficeHazardType)PickFromWeighted(w,
                    new[] { OfficeHazardType.MandatoryMeeting,
                            OfficeHazardType.PrinterJam,
                            OfficeHazardType.FireDrill,
                            OfficeHazardType.UnscheduledAudit });
            }

            // Morning (or default)
            {
                float[] w = _pressureLevel >= 2
                    ? new[] { 3f, 3f, 2f, 1f }
                    : new[] { 5f, 3f, 0f, 0f };
                return (OfficeHazardType)PickFromWeighted(w,
                    new[] { OfficeHazardType.PrinterJam,
                            OfficeHazardType.CoffeeMachineDown,
                            OfficeHazardType.MandatoryMeeting,
                            OfficeHazardType.FireDrill });
            }
        }

        private static int PickFromWeighted(float[] weights, OfficeHazardType[] types)
        {
            int idx = SeedEngine.WeightedRandom(SeedStream.RumorMillEvents, weights);
            return (int)types[Mathf.Clamp(idx, 0, types.Length - 1)];
        }

        private void SetPressureLevel(int newLevel, bool isOverperformance)
        {
            int oldLevel = _pressureLevel;
            _pressureLevel = Mathf.Clamp(newLevel, 0, 3);

            if (oldLevel == _pressureLevel) return;

            Debug.Log($"[TideSystem] Pressure: {oldLevel} → {_pressureLevel}. " +
                      $"Overperformance: {isOverperformance}.");

            RumorMill.PublishDeferred(
                new TideEscalatedEvent(oldLevel, _pressureLevel, isOverperformance));

            // Reschedule next hazard based on new pressure level
            ScheduleNextHazard();
        }
    }
}
