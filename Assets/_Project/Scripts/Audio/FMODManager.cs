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
            _busMaster   = FMODUnity.RuntimeManager.GetBus(BUS_MASTER);
            _busMusic    = FMODUnity.RuntimeManager.GetBus(BUS_MUSIC);
            _busSFX      = FMODUnity.RuntimeManager.GetBus(BUS_SFX);
            _busAmbience = FMODUnity.RuntimeManager.GetBus(BUS_AMBIENCE);
            Debug.Log("[FMODManager] Buses resolved.");
#else
            Debug.Log("[FMODManager] FMOD not available (DESK42_FMOD not defined). " +
                      "Audio stubs active.");
#endif
        }

        // ── Public API ────────────────────────────────────────

        /// <summary>
        /// Set a global FMOD parameter by name. Parameters drive
        /// dynamic mix automation (sanity, tide pressure, etc.).
        /// </summary>
        public void SetGlobalParameter(string name, float value)
        {
#if DESK42_FMOD
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName(name, value);
#else
            Debug.Log($"[FMODManager:Stub] SetGlobalParameter({name}, {value:F2})");
#endif
        }

        /// <summary>Set volume on a named bus (0 = silent, 1 = full).</summary>
        public void SetBusVolume(string busPath, float volume)
        {
#if DESK42_FMOD
            var bus = FMODUnity.RuntimeManager.GetBus(busPath);
            bus.setVolume(Mathf.Clamp01(volume));
#else
            Debug.Log($"[FMODManager:Stub] SetBusVolume({busPath}, {volume:F2})");
#endif
        }

        /// <summary>Play a one-shot FMOD event by path.</summary>
        public void PlayOneShot(string eventPath, Vector3 position = default)
        {
#if DESK42_FMOD
            FMODUnity.RuntimeManager.PlayOneShot(eventPath, position);
#else
            Debug.Log($"[FMODManager:Stub] PlayOneShot({eventPath})");
#endif
        }

        // ── Volume Presets ────────────────────────────────────

        public void SetMasterVolume(float volume)
        {
#if DESK42_FMOD
            _busMaster.setVolume(Mathf.Clamp01(volume));
#endif
        }

        public void SetMusicVolume(float volume)
        {
#if DESK42_FMOD
            _busMusic.setVolume(Mathf.Clamp01(volume));
#endif
        }

        public void SetSFXVolume(float volume)
        {
#if DESK42_FMOD
            _busSFX.setVolume(Mathf.Clamp01(volume));
#endif
        }
    }
}
