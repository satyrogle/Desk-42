// ============================================================
// DESK 42 — Binaural Stress Engine (MonoBehaviour)
//
// Drives an FMOD event with a continuously modulated parameter
// tied to player sanity. As sanity drops, the binaural beat
// frequency differential narrows (lower = more unsettling).
//
// Subscribes to RumorMill.OnSanityChanged for reactive updates,
// with a smooth lerp in Update() for gradual transitions.
// ============================================================

using UnityEngine;
using Desk42.Core;

namespace Desk42.Audio
{
    [DisallowMultipleComponent]
    public sealed class BinauralStressEngine : MonoBehaviour
    {
        [Header("FMOD")]
        [SerializeField] private string _eventPath     = "event:/Music/BinauralStress";
        [SerializeField] private string _parameterName = "SanityLevel";

        [Header("Config")]
        [SerializeField] private float _lerpSpeed = 2f;

        // ── State ─────────────────────────────────────────────

        private float _currentSanity = 100f;
        private float _targetSanity  = 100f;

#if DESK42_FMOD
        private FMOD.Studio.EventInstance _instance;
#endif

        // ── Unity Lifecycle ───────────────────────────────────

        private void OnEnable()
        {
            RumorMill.OnSanityChanged  += HandleSanityChanged;
            RumorMill.OnShiftLifecycle += HandleShiftLifecycle;

            int phase = GameManager.Instance?.Meta?.HighestPhaseReached ?? 4;
            if (phase < 3) return;

#if DESK42_FMOD
            _instance = FMODUnity.RuntimeManager.CreateInstance(_eventPath);
            _instance.start();
#endif
        }

        private void OnDisable()
        {
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
            int phase = GameManager.Instance?.Meta?.HighestPhaseReached ?? 4;
            if (phase < 3) return;

            _currentSanity = Mathf.MoveTowards(
                _currentSanity, _targetSanity, _lerpSpeed * Time.deltaTime * 100f);

            // Normalise: 0 = max stress, 1 = full sanity
            float normalised = _currentSanity / 100f;

#if DESK42_FMOD
            if (_instance.isValid())
            {
                _instance.setParameterByName(_parameterName, normalised);
            }
#endif

            // Also push to the global parameter for other FMOD events
            FMODManager.Instance?.SetGlobalParameter("Sanity", normalised);
        }

        // ── Event Handlers ────────────────────────────────────

        private void HandleSanityChanged(SanityChangedEvent e)
        {
            _targetSanity = e.Current;
        }

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            if (e.IsStart)
            {
                _targetSanity  = 100f;
                _currentSanity = 100f;
            }
        }
    }
}
