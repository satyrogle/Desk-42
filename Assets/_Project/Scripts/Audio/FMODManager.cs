// ============================================================
// DESK 42 — FMOD Manager (MonoBehaviour)
//
// Singleton wrapper around the FMOD Unity plugin.
// Manages global bus references and exposes a parameter API
// for game systems (sanity, tide, entropy) to drive audio.
//
// Until the FMOD plugin is imported, this file compiles as
// a no-op stub via DESK42_FMOD conditional symbol.
// Add DESK42_FMOD to Player Settings → Scripting Define Symbols
// once com.fmod.unity is in the Packages manifest.
// ============================================================

using UnityEngine;
using Desk42.Core;

namespace Desk42.Audio
{
    [DisallowMultipleComponent]
    public sealed class FMODManager : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────

        public static FMODManager Instance { get; private set; }

        // ── Bus Paths ─────────────────────────────────────────
        // Match these to your FMOD Studio mixer layout.

        private const string BUS_MASTER   = "bus:/";
        private const string BUS_MUSIC    = "bus:/Music";
        private const string BUS_SFX      = "bus:/SFX";
        private const string BUS_AMBIENCE = "bus:/Ambience";

#if DESK42_FMOD
        private FMOD.Studio.Bus _busMaster;
        private FMOD.Studio.Bus _busMusic;
        private FMOD.Studio.Bus _busSFX;
        private FMOD.Studio.Bus _busAmbience;

        /// <summary>
        /// False until the native system is up AND every bus resolved. Every
        /// FMOD call below is gated on this: the runtime throws
        /// SystemNotInitializedException when FMODStudioSettings.asset is
        /// missing or no banks are built, and an audio wrapper must not turn
        /// an unconfigured install into a gameplay-breaking exception.
        /// </summary>
        private bool _busesResolved;
#endif

        // ── Unity Lifecycle ───────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

#if DESK42_FMOD
            try
            {
                _busMaster   = FMODUnity.RuntimeManager.GetBus(BUS_MASTER);
                _busMusic    = FMODUnity.RuntimeManager.GetBus(BUS_MUSIC);
                _busSFX      = FMODUnity.RuntimeManager.GetBus(BUS_SFX);
                _busAmbience = FMODUnity.RuntimeManager.GetBus(BUS_AMBIENCE);
                _busesResolved = true;
                Debug.Log("[FMODManager] Buses resolved.");
            }
            catch (FMODUnity.BusNotFoundException ex)
            {
                // Buses live in the Master Bank. Not finding them means no
                // banks are loaded yet — an AUTHORING gap, and the expected
                // state before the Studio project has been built. Warning, not
                // error, matching how FmodAudioBackend classifies the same
                // condition; an error here would fail every PlayMode test for
                // a situation we already know about.
                _busesResolved = false;
                Debug.LogWarning(
                    $"[FMODManager] Buses unavailable ({ex.Message}). No banks " +
                    $"are loaded, so bus control is inert. Build the FMOD Studio " +
                    $"project to populate them.");
            }
            catch (System.Exception ex)
            {
                // Anything else — failed native init, missing settings asset —
                // is a genuine INTEGRATION fault and must be loud.
                _busesResolved = false;
                Debug.LogError(
                    $"[FMODManager] Bus resolution FAILED: {ex.GetType().Name}: " +
                    $"{ex.Message}. FMOD is imported but not usable — the usual " +
                    $"cause is a missing FMODStudioSettings.asset. Continuing " +
                    $"silent; this is an integration fault, not an authoring gap.");
            }
#else
            Debug.Log("[FMODManager] FMOD not available (DESK42_FMOD not defined). " +
                      "Audio stubs active.");
#endif

            // Apply player-saved volumes to the buses so the boot
            // mix reflects their last session.
            AudioSettings.ApplyToBuses();
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Set a global FMOD parameter by name. Parameters drive
        /// dynamic mix automation (sanity, tide pressure, etc.).
        /// </summary>
        public void SetGlobalParameter(string name, float value)
        {
#if DESK42_FMOD
            if (!_busesResolved) return;
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName(name, value);
#endif
            // Stub: silent — called every frame, can't log
        }

        /// <summary>Set volume on a named bus (0 = silent, 1 = full).</summary>
        public void SetBusVolume(string busPath, float volume)
        {
#if DESK42_FMOD
            if (!_busesResolved) return;
            var bus = FMODUnity.RuntimeManager.GetBus(busPath);
            bus.setVolume(Mathf.Clamp01(volume));
#endif
        }

        /// <summary>Play a one-shot FMOD event by path.</summary>
        public void PlayOneShot(string eventPath, Vector3 position = default)
        {
#if DESK42_FMOD
            if (!_busesResolved) return;
            FMODUnity.RuntimeManager.PlayOneShot(eventPath, position);
#endif
        }

        // ── Volume Presets ────────────────────────────────────

        public void SetMasterVolume(float volume)
        {
#if DESK42_FMOD
            if (!_busesResolved) return;
            _busMaster.setVolume(Mathf.Clamp01(volume));
#endif
        }

        public void SetMusicVolume(float volume)
        {
#if DESK42_FMOD
            if (!_busesResolved) return;
            _busMusic.setVolume(Mathf.Clamp01(volume));
#endif
        }

        public void SetSFXVolume(float volume)
        {
#if DESK42_FMOD
            if (!_busesResolved) return;
            _busSFX.setVolume(Mathf.Clamp01(volume));
#endif
        }
    }
}
