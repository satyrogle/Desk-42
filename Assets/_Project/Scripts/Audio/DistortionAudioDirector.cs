#pragma warning disable 0414
// ============================================================
// DESK 42 — Distortion Audio Director (MonoBehaviour)
//
// Drives the *discrete* audio side of the Environmental Distortion
// Scale: tier-cross stingers as Sanity moves between bands, plus the
// mode snapshots (Fugue / Mercy Window / Flow).
//
// Continuous dread is handled elsewhere and this director deliberately
// does NOT touch those parameters, so nothing double-drives them:
//   - BinauralStressEngine owns the bed + pushes global "Sanity" (0..1).
//   - StressCrescendo pushes global "TidePressure".
// This director only *reacts* to Sanity tier crossings and toggles
// snapshots.
//
// Event-bus driven (RumorMill.OnSanityChanged) — same pattern as the
// other audio drivers. FMOD instances are created the same way they are
// in BinauralStressEngine / StressCrescendo. Compiles to a no-op until
// the FMOD plugin is imported and DESK42_FMOD is defined.
//
// Author these in the FMOD Studio project to match the serialized paths:
//   event:/Threat/TierCross  one-shot; local "Direction" param (-1 worse / +1 recovering)
//   snapshot:/Fugue          dissociation mode
//   snapshot:/MercyWindow    The Tide's Ebb
//   snapshot:/Flow           The Tide's Flow (optional)
//
// Fugue is driven automatically from SanityChangedEvent.TriggeredFugue.
// Mercy / Flow have no dedicated bus event — TideSystem should call
// EnterMercyWindow()/ExitMercyWindow() (and EnterFlow()/ExitFlow()) on
// Ebb / Flow.  // TODO(wire): hook these from Core/TideSystem.
// ============================================================

using UnityEngine;
using Desk42.Core;

namespace Desk42.Audio
{
    [DisallowMultipleComponent]
    public sealed class DistortionAudioDirector : MonoBehaviour
    {
        [Header("FMOD event / snapshot paths")]
        [SerializeField] private string _tierCrossStinger = "event:/Threat/TierCross";
        [SerializeField] private string _fugueSnapshot    = "snapshot:/Fugue";
        [SerializeField] private string _mercySnapshot    = "snapshot:/MercyWindow";
        [SerializeField] private string _flowSnapshot     = "snapshot:/Flow";
        [SerializeField] private string _directionParam   = "Direction";

        [Header("Tuning")]
        [Tooltip("Sanity (0..100) deadband around tier thresholds, to stop stinger spam at boundaries.")]
        [SerializeField] private float _tierHysteresis = 3f;

        // ── State ─────────────────────────────────────────────
        // Tier 0 = healthiest. Bands: [75..100]=0  [50..75)=1  [25..50)=2  [0..25)=3.
        // Fugue (Sanity 0 as a MODE) is tracked separately via _inFugue.
        private int  _currentTier;
        private bool _hasTier;
        private bool _inFugue;

#if DESK42_FMOD
        private FMOD.Studio.EventInstance _fugue;
        private FMOD.Studio.EventInstance _mercy;
        private FMOD.Studio.EventInstance _flow;
#endif

        // Gate FMOD work behind the same phase unlock the other drivers use.
        private bool AudioReady => GameManager.Instance?.IsPhaseUnlocked(4) ?? true;

        // ── Unity Lifecycle ───────────────────────────────────

        private void OnEnable()
        {
            RumorMill.OnSanityChanged  += HandleSanityChanged;
            RumorMill.OnShiftLifecycle += HandleShiftLifecycle;
        }

        private void OnDisable()
        {
            RumorMill.OnSanityChanged  -= HandleSanityChanged;
            RumorMill.OnShiftLifecycle -= HandleShiftLifecycle;
            StopAllSnapshots();
        }

        private void OnDestroy() => StopAllSnapshots();

        // ── Event Handlers ────────────────────────────────────

        private void HandleSanityChanged(SanityChangedEvent e)
        {
            // Fugue is a MODE — driven by the flag, not by Sanity == 0, so a
            // momentary dip while recovering doesn't fully muffle the mix.
            if (e.TriggeredFugue)                 EnterFugue();
            else if (_inFugue && e.Current > 0f)  ExitFugue();

            UpdateTier(e.Current);
        }

        private void HandleShiftLifecycle(ShiftLifecycleEvent e)
        {
            if (!e.IsStart) return;
            // New shift -> clean baseline. _hasTier stays true so we don't fire
            // a spurious stinger on the first crossing back down.
            _currentTier = 0;
            _hasTier     = true;
            if (_inFugue) ExitFugue();
        }

        // ── Tier crossings ────────────────────────────────────

        private void UpdateTier(float sanity)
        {
            if (!_hasTier)
            {
                _currentTier = TierOf(sanity);
                _hasTier = true;
                return;
            }

            // Stay in the current tier while still inside its padded band
            // (hysteresis), so hovering on a boundary doesn't retrigger.
            float low  = TierLow(_currentTier);
            float high = TierHigh(_currentTier);
            if (sanity >= low - _tierHysteresis && sanity <= high + _tierHysteresis)
                return;

            int newTier = TierOf(sanity);
            if (newTier == _currentTier) return;

            int direction = newTier > _currentTier ? -1 : +1; // -1 = worse, +1 = recovering
            PlayTierStinger(direction);                        // one stinger even on a multi-tier drop
            _currentTier = newTier;
        }

        private void PlayTierStinger(int direction)
        {
#if DESK42_FMOD
            if (!AudioReady || string.IsNullOrEmpty(_tierCrossStinger)) return;
            var s = FMODUnity.RuntimeManager.CreateInstance(_tierCrossStinger);
            s.setParameterByName(_directionParam, direction);
            s.start();
            s.release(); // fire-and-forget; frees itself when it finishes
#endif
        }

        // ── Mode snapshots ────────────────────────────────────

        /// <summary>Dissociation begins. Driven automatically from SanityChangedEvent.TriggeredFugue.</summary>
        public void EnterFugue()
        {
            if (_inFugue) return;
            _inFugue = true;
#if DESK42_FMOD
            if (!AudioReady || string.IsNullOrEmpty(_fugueSnapshot)) return;
            _fugue = FMODUnity.RuntimeManager.CreateInstance(_fugueSnapshot);
            _fugue.start();
#endif
        }

        public void ExitFugue()
        {
            if (!_inFugue) return;
            _inFugue = false;
#if DESK42_FMOD
            Stop(ref _fugue);
#endif
        }

        /// <summary>The Tide grants an Ebb / mercy break — call from TideSystem.</summary>
        public void EnterMercyWindow()
        {
#if DESK42_FMOD
            if (!AudioReady || _mercy.isValid() || string.IsNullOrEmpty(_mercySnapshot)) return;
            _mercy = FMODUnity.RuntimeManager.CreateInstance(_mercySnapshot);
            _mercy.start();
#endif
        }

        public void ExitMercyWindow()
        {
#if DESK42_FMOD
            Stop(ref _mercy);
#endif
        }

        /// <summary>The Tide's Flow tightens the mix — call from TideSystem (optional).</summary>
        public void EnterFlow()
        {
#if DESK42_FMOD
            if (!AudioReady || _flow.isValid() || string.IsNullOrEmpty(_flowSnapshot)) return;
            _flow = FMODUnity.RuntimeManager.CreateInstance(_flowSnapshot);
            _flow.start();
#endif
        }

        public void ExitFlow()
        {
#if DESK42_FMOD
            Stop(ref _flow);
#endif
        }

        // ── Helpers ───────────────────────────────────────────

        private void StopAllSnapshots()
        {
#if DESK42_FMOD
            Stop(ref _fugue);
            Stop(ref _mercy);
            Stop(ref _flow);
#endif
        }

#if DESK42_FMOD
        private static void Stop(ref FMOD.Studio.EventInstance inst)
        {
            if (inst.isValid())
            {
                inst.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                inst.release();
            }
        }
#endif

        // Sanity 0..100 -> tier index 0..3.
        private static int TierOf(float s)
        {
            if (s >= 75f) return 0;
            if (s >= 50f) return 1;
            if (s >= 25f) return 2;
            return 3;
        }

        private static float TierLow(int t)  => t == 0 ? 75f  : t == 1 ? 50f : t == 2 ? 25f : 0f;
        private static float TierHigh(int t) => t == 0 ? 100f : t == 1 ? 75f : t == 2 ? 50f : 25f;
    }
}
