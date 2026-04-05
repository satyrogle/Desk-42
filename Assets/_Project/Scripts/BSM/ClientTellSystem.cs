// ============================================================
// DESK 42 — Client Tell System
//
// Fires pre-transition "tells" 1.5-2s before a BSM state
// changes. The player learns to read these as a mastery curve.
//
// Tells are visual + audio micro-events:
//   - "StraightensPapers"  (approaching LITIGIOUS)
//   - "GlancesAtDoor"      (approaching PARANOID)
//   - "DrumsFingersSlower" (approaching RESIGNED)
//   - "LeanForward"        (approaching SMUG)
//   - etc.
//
// Repeat Offenders have DIFFERENT, harder-to-read tells
// compared to first-timers — experienced players learn to
// spot the subtler version.
//
// Architecture:
//   ClientStateMachine calls RequestTell(tellType, intensity)
//   via ClientContext.TriggerTell. This system then:
//     1. Checks if a visual Tell exists (animation event).
//     2. Fires an audio cue (binaural or spatial).
//     3. Starts the countdown before the actual transition.
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using Desk42.Core;

namespace Desk42.BSM
{
    [System.Serializable]
    public sealed class TellDefinition
    {
        public string   TellType;
        public float    LeadTimeSeconds;  // how long before transition this fires
        public string   AnimationTrigger; // Animator trigger name
        public string   AudioCueKey;      // key into AudioManager
        public bool     IsSubtleVariant;  // true = repeat offender version
    }

    public sealed class ClientTellSystem
    {
        private readonly List<TellDefinition> _tellLibrary;
        private readonly int                  _visitCount;

        // Pending tells: (tell, timeUntilFire)
        private readonly Queue<(TellDefinition tell, float delay)> _pendingTells
            = new(4);

        // ── Init ──────────────────────────────────────────────

        public ClientTellSystem(List<TellDefinition> library, int visitCount)
        {
            _tellLibrary = library ?? BuildDefaultLibrary();
            _visitCount  = visitCount;
        }

        // ── API ───────────────────────────────────────────────

        /// <summary>
        /// Enqueue a tell to fire after its LeadTime elapses.
        /// Called by ClientContext.TriggerTell from state Tick methods.
        /// </summary>
        public void RequestTell(string tellType, float intensity)
        {
            foreach (var def in _tellLibrary)
            {
                if (!string.Equals(def.TellType, tellType,
                    System.StringComparison.OrdinalIgnoreCase)) continue;

                // Repeat offenders use subtle variant (harder to read)
                if (def.IsSubtleVariant && _visitCount == 0) continue;
                if (!def.IsSubtleVariant && _visitCount > 0) continue;

                _pendingTells.Enqueue((def, def.LeadTimeSeconds * intensity));
                return;
            }

            // No matching tell — log for debug
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[TellSystem] No tell defined for type '{tellType}' " +
                      $"(visitCount: {_visitCount})");
#endif
        }

        /// <summary>
        /// Tick tells down. Fire any that have elapsed.
        /// </summary>
        public void Tick(float deltaTime,
            System.Action<TellDefinition> onTellFired)
        {
            if (_pendingTells.Count == 0) return;

            // Process all pending tells
            int count = _pendingTells.Count;
            for (int i = 0; i < count; i++)
            {
                var (tell, delay) = _pendingTells.Dequeue();
                delay -= deltaTime;

                if (delay <= 0f)
                    onTellFired?.Invoke(tell);  // fire!
                else
                    _pendingTells.Enqueue((tell, delay)); // re-queue
            }
        }

        public void Clear() => _pendingTells.Clear();

        // ── Default Library ───────────────────────────────────

        private static List<TellDefinition> BuildDefaultLibrary() =>
            new()
            {
                // Approaching LITIGIOUS
                new() { TellType = "ApproachingLitigious", LeadTimeSeconds = 2.0f,
                    AnimationTrigger = "StraightenPapers",  AudioCueKey = "tell_paper_rustle",
                    IsSubtleVariant = false },
                new() { TellType = "ApproachingLitigious", LeadTimeSeconds = 1.5f,
                    AnimationTrigger = "MicroStraightenPapers", AudioCueKey = "tell_paper_micro",
                    IsSubtleVariant = true },  // repeat offender: faster, subtler

                // Approaching PARANOID
                new() { TellType = "GlanceAtDoor", LeadTimeSeconds = 1.8f,
                    AnimationTrigger = "GlanceAtDoor", AudioCueKey = "tell_chair_creak",
                    IsSubtleVariant = false },
                new() { TellType = "GlanceAtDoor", LeadTimeSeconds = 1.0f,
                    AnimationTrigger = "MicroEyeShift", AudioCueKey = "tell_breathing_change",
                    IsSubtleVariant = true },

                // Approaching AGITATED
                new() { TellType = "ApproachingAgitated", LeadTimeSeconds = 2.5f,
                    AnimationTrigger = "CheckWatch", AudioCueKey = "tell_watch_click",
                    IsSubtleVariant = false },
                new() { TellType = "ApproachingAgitated", LeadTimeSeconds = 1.5f,
                    AnimationTrigger = "TapFinger", AudioCueKey = "tell_tap_subtle",
                    IsSubtleVariant = true },

                // SMUG trap
                new() { TellType = "FeetOnDesk", LeadTimeSeconds = 0.5f,
                    AnimationTrigger = "FeetOnDesk", AudioCueKey = "tell_chair_lean",
                    IsSubtleVariant = false },
                new() { TellType = "VoluntaryReveal_Trap", LeadTimeSeconds = 1.0f,
                    AnimationTrigger = "SmugSmile", AudioCueKey = "tell_smug_intake",
                    IsSubtleVariant = false },

                // DISSOCIATING
                new() { TellType = "StareThrough", LeadTimeSeconds = 0.8f,
                    AnimationTrigger = "VacantStare", AudioCueKey = "tell_ambient_drop",
                    IsSubtleVariant = false },

                // COOPERATIVE
                new() { TellType = "SmallTalkAttempt", LeadTimeSeconds = 0.3f,
                    AnimationTrigger = "OpenMouthSlightly", AudioCueKey = "tell_inhale",
                    IsSubtleVariant = false },

                // Post-it reading (PARANOID / SUSPICIOUS tells)
                new() { TellType = "GlanceAtPostIts", LeadTimeSeconds = 1.2f,
                    AnimationTrigger = "EyeShiftLeft", AudioCueKey = "tell_page_turn",
                    IsSubtleVariant = false },
                new() { TellType = "CoverMouth", LeadTimeSeconds = 0.6f,
                    AnimationTrigger = "CoverMouth", AudioCueKey = "tell_muffled",
                    IsSubtleVariant = false },

                // RESIGNED
                new() { TellType = "HeavySigh", LeadTimeSeconds = 0.4f,
                    AnimationTrigger = "ShoulderDrop", AudioCueKey = "tell_sigh",
                    IsSubtleVariant = false },
            };
    }
}
