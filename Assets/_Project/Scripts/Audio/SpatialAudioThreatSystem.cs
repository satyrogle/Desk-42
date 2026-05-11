// ============================================================
// DESK 42 — Spatial Audio Threat System (MonoBehaviour)
//
// Telegraphs in-world threats via positional one-shots. The
// office is the player's mental map; we use stereo position
// to point them at things they should be aware of:
//
//   Pending claim queue  → soft typewriter / file-shuffle from
//                          the LEFT (in-tray side).
//   Office hazard        → directional alert from the hazard
//                          source (Printer, Coffee, Meeting room).
//   Tide escalation      → low rumble from BEHIND the player
//                          (over the cubicle wall).
//   Sanity threshold cross → whisper from OFF-AXIS (LFO-jittered
//                          position) — uncanny / unlocatable.
//
// Output goes through FMODManager.PlayOneShot when DESK42_FMOD
// is defined; otherwise just logs the event. The triggering
// logic runs unconditionally so the architecture is real and
// can be QA'd without the FMOD plugin imported.
//
// All sound positions are scene-local. The Shift scene puts an
// AudioListener at desk position; this script publishes one-shots
// at offsets relative to that listener.
// ============================================================

using UnityEngine;
using Desk42.Accessibility;
using Desk42.Core;

namespace Desk42.Audio
{
    [DisallowMultipleComponent]
    public sealed class SpatialAudioThreatSystem : MonoBehaviour
    {
        // ── FMOD Event Paths ──────────────────────────────────
        // Match these to events authored in the FMOD Studio project.

        [Header("FMOD Event Paths")]
        [SerializeField] private string _evtQueuePressure  = "event:/Threat/QueuePressure";
        [SerializeField] private string _evtHazardAlert    = "event:/Threat/HazardAlert";
        [SerializeField] private string _evtTideRumble     = "event:/Threat/TideRumble";
        [SerializeField] private string _evtSanityWhisper  = "event:/Threat/SanityWhisper";

        // ── Spatial Anchors (relative to the listener) ────────

        [Header("Spatial Anchors")]
        [Tooltip("In-tray / pending queue side. Default: left.")]
        [SerializeField] private Vector3 _queuePosition  = new(-2.5f, 0.2f, 0.5f);
        [Tooltip("Behind the cubicle wall.")]
        [SerializeField] private Vector3 _tidePosition   = new(0f, 1.0f, -2.0f);

        [Header("Tuning")]
        [SerializeField] private int   _queueThreshold   = 3;   // play when pending claims >= this
        [SerializeField] private float _queueCooldown    = 6f;  // min seconds between queue-pressure cues
        [SerializeField] private float _whisperJitter    = 1.5f;

        private float _lastQueueCueTime  = -999f;

        // Don't draw from SeedEngine until a run is active —
        // SeedEngine throws in dev builds before Init.
        private bool _runActive;

        // ── Lifecycle ─────────────────────────────────────────

        private void OnEnable()
        {
            RumorMill.OnClaimQueued    += HandleClaimQueued;
            RumorMill.OnOfficeHazard   += HandleOfficeHazard;
            RumorMill.OnTideEscalated  += HandleTideEscalated;
            RumorMill.OnSanityChanged  += HandleSanityChanged;
            RumorMill.OnShiftLifecycle += HandleShiftLifecycle;
        }

        private void OnDisable()
        {
            RumorMill.OnClaimQueued    -= HandleClaimQueued;
            RumorMill.OnOfficeHazard   -= HandleOfficeHazard;
            RumorMill.OnTideEscalated  -= HandleTideEscalated;
            RumorMill.OnSanityChanged  -= HandleSanityChanged;
            RumorMill.OnShiftLifecycle -= HandleShiftLifecycle;
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleClaimQueued(ClaimQueuedEvent e)
        {
            if (e.QueueRemaining < _queueThreshold) return;
            if (Time.unscaledTime - _lastQueueCueTime < _queueCooldown) return;
            _lastQueueCueTime = Time.unscaledTime;
            Play(_evtQueuePressure, _queuePosition,
                $"queue pressure ({e.QueueRemaining} pending)");
        }

        private void HandleOfficeHazard(OfficeHazardEvent e)
        {
            Play(_evtHazardAlert, HazardPosition(e.HazardType),
                $"hazard {e.HazardType}");
        }

        private void HandleTideEscalated(TideEscalatedEvent e)
        {
            // Only play on rises (not de-escalations) and skip the
            // tutorial-level 0→1 step.
            if (e.NewLevel <= e.OldLevel || e.NewLevel < 2) return;
            Play(_evtTideRumble, _tidePosition, $"tide {e.OldLevel}→{e.NewLevel}");
        }

        private float _lastSanityBucket = 100f;
        private void HandleSanityChanged(SanityChangedEvent e)
        {
            // Trigger a whisper when crossing 50, 25, 10
            float prevBucket = Bucket(_lastSanityBucket);
            float newBucket  = Bucket(e.Current);
            if (newBucket < prevBucket)
            {
                Vector3 pos = JitteredOffAxis();
                Play(_evtSanityWhisper, pos, $"sanity threshold {newBucket}");
            }
            _lastSanityBucket = e.Current;
        }

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            if (e.IsStart)
            {
                _lastQueueCueTime  = -999f;
                _lastSanityBucket = 100f;
                _runActive        = true;
            }
            else
            {
                _runActive = false;
            }
        }

        // ── Helpers ───────────────────────────────────────────

        private Vector3 HazardPosition(OfficeHazardType type)
        {
            // Rough "office geography" — tweak in level design.
            return type switch
            {
                OfficeHazardType.PrinterJam        => new Vector3( 3.0f, 0.5f, 1.0f),
                OfficeHazardType.CoffeeMachineDown => new Vector3(-1.5f, 0.5f, 2.5f),
                OfficeHazardType.MandatoryMeeting  => new Vector3( 0.0f, 1.0f, 4.0f),
                OfficeHazardType.SystemCrash       => new Vector3( 0.0f, 0.5f, 0.0f),
                OfficeHazardType.FireDrill         => new Vector3( 0.0f, 2.0f, 0.0f),
                OfficeHazardType.UnscheduledAudit  => new Vector3( 4.0f, 0.5f, 3.0f),
                _ => Vector3.zero,
            };
        }

        private Vector3 JitteredOffAxis()
        {
            // Use SeedEngine for determinism so a seeded replay
            // hears the same whispers in the same places. Bail
            // if SeedEngine isn't initialized yet (e.g., scene
            // opened directly in editor before a run starts).
            if (!_runActive) return new Vector3(0f, 1.6f, 0f);
            float x = SeedEngine.NextFloat(SeedStream.FormCorruption,
                -_whisperJitter, _whisperJitter);
            float z = SeedEngine.NextFloat(SeedStream.FormCorruption,
                -_whisperJitter, _whisperJitter);
            return new Vector3(x, 1.6f, z);
        }

        private static float Bucket(float s)
        {
            if (s >= 50f) return 100f;
            if (s >= 25f) return 50f;
            if (s >= 10f) return 25f;
            if (s > 0f)   return 10f;
            return 0f;
        }

        private void Play(string eventPath, Vector3 position, string debugLabel)
        {
            // Reduced motion isn't quite the right semantic here
            // — but if the player has muted screen distortion, we
            // also dial back the unsettling whispers. The other
            // cues (queue, hazard, tide) still fire.
            if (eventPath == _evtSanityWhisper &&
                AccessibilitySettings.DisableScreenDistortion) return;

            FMODManager.Instance?.PlayOneShot(eventPath, position);

#if UNITY_EDITOR
            Debug.Log($"[SpatialThreat] {debugLabel} @ {position}");
#endif
        }
    }
}
