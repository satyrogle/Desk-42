#pragma warning disable 0414
// ============================================================
// DESK 42 — Stress Crescendo (MonoBehaviour)
//
// 4-layer dynamic music system:
//   Layer 0: Ambient       (always playing)
//   Layer 1: Tension        (TidePressure >= 1)
//   Layer 2: Urgency        (TidePressure >= 2)
//   Layer 3: Panic          (TidePressure >= 3 or Fugue state)
//
// Drives an FMOD multi-track event with a "TidePressure"
// parameter that automates volume/filter automation in the
// FMOD Studio project.
// ============================================================

using UnityEngine;
using Desk42.Core;

namespace Desk42.Audio
{
    [DisallowMultipleComponent]
    public sealed class StressCrescendo : MonoBehaviour
    {
        [Header("FMOD")]
        [SerializeField] private string _eventPath     = "event:/Music/ShiftCrescendo";
        [SerializeField] private string _parameterName = "TidePressure";

        [Header("Config")]
        [SerializeField] private float _lerpSpeed = 1.5f;

        // ── State ─────────────────────────────────────────────

        private float _currentPressure;
        private float _targetPressure;

#if DESK42_FMOD
        private FMOD.Studio.EventInstance _instance;
#endif

        // ── Unity Lifecycle ───────────────────────────────────

        private void OnEnable()
        {
            RumorMill.OnTideEscalated  += HandleTideEscalated;
            RumorMill.OnSanityChanged  += HandleSanityChanged;
            RumorMill.OnShiftLifecycle += HandleShiftLifecycle;

            if (!(GameManager.Instance?.IsPhaseUnlocked(4) ?? true)) return;

#if DESK42_FMOD
            _instance = FMODUnity.RuntimeManager.CreateInstance(_eventPath);
            _instance.start();
#endif
        }

        private void OnDisable()
        {
            RumorMill.OnTideEscalated  -= HandleTideEscalated;
            RumorMill.OnSanityChanged  -= HandleSanityChanged;
            RumorMill.OnShiftLifecycle -= HandleShiftLifecycle;

#if DESK42_FMOD
            if (_instance.isValid())
            {
                _instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                _instance.release();
            }
#endif
        }

        private void Update()
        {
            if (!(GameManager.Instance?.IsPhaseUnlocked(4) ?? true)) return;

            _currentPressure = Mathf.MoveTowards(
                _currentPressure, _targetPressure, _lerpSpeed * Time.deltaTime);

#if DESK42_FMOD
            if (_instance.isValid())
            {
                _instance.setParameterByName(_parameterName, _currentPressure);
            }
#endif

            FMODManager.Instance?.SetGlobalParameter("TidePressure", _currentPressure);
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleTideEscalated(TideEscalatedEvent e)
        {
            _targetPressure = e.NewLevel;
        }

        private void HandleSanityChanged(SanityChangedEvent e)
        {
            // Fugue overrides to max pressure
            if (e.TriggeredFugue)
                _targetPressure = 3f;
        }

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            if (e.IsStart)
            {
                _targetPressure  = 0f;
                _currentPressure = 0f;
            }
        }
    }
}
