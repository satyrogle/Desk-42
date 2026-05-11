// ============================================================
// DESK 42 — Audio Settings
//
// Master / Music / SFX volume sliders. Backed by PlayerPrefs so
// settings persist across runs and survive meta wipes.
//
// On change, pushes the new value to FMODManager (no-op when
// FMOD isn't available). UI bindings should subscribe to
// OnChanged to refresh sliders if external code changes a value.
// ============================================================

using System;
using UnityEngine;

namespace Desk42.Audio
{
    public static class AudioSettings
    {
        private const string K_Master = "desk42.audio.master";
        private const string K_Music  = "desk42.audio.music";
        private const string K_SFX    = "desk42.audio.sfx";

        private static float _master = 1f;
        private static float _music  = 0.8f;
        private static float _sfx    = 1f;
        private static bool  _loaded;

        public static event Action OnChanged;

        public static float MasterVolume
        {
            get { EnsureLoaded(); return _master; }
            set
            {
                EnsureLoaded();
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_master, clamped)) return;
                _master = clamped;
                PlayerPrefs.SetFloat(K_Master, clamped);
                PlayerPrefs.Save();
                FMODManager.Instance?.SetMasterVolume(clamped);
                OnChanged?.Invoke();
            }
        }

        public static float MusicVolume
        {
            get { EnsureLoaded(); return _music; }
            set
            {
                EnsureLoaded();
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_music, clamped)) return;
                _music = clamped;
                PlayerPrefs.SetFloat(K_Music, clamped);
                PlayerPrefs.Save();
                FMODManager.Instance?.SetMusicVolume(clamped);
                OnChanged?.Invoke();
            }
        }

        public static float SFXVolume
        {
            get { EnsureLoaded(); return _sfx; }
            set
            {
                EnsureLoaded();
                float clamped = Mathf.Clamp01(value);
                if (Mathf.Approximately(_sfx, clamped)) return;
                _sfx = clamped;
                PlayerPrefs.SetFloat(K_SFX, clamped);
                PlayerPrefs.Save();
                FMODManager.Instance?.SetSFXVolume(clamped);
                OnChanged?.Invoke();
            }
        }

        /// <summary>Push the current values to FMODManager. Call
        /// on FMODManager.Awake so the player's saved volumes
        /// take effect on every boot.</summary>
        public static void ApplyToBuses()
        {
            EnsureLoaded();
            var mgr = FMODManager.Instance;
            if (mgr == null) return;
            mgr.SetMasterVolume(_master);
            mgr.SetMusicVolume(_music);
            mgr.SetSFXVolume(_sfx);
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _master = Mathf.Clamp01(PlayerPrefs.GetFloat(K_Master, 1f));
            _music  = Mathf.Clamp01(PlayerPrefs.GetFloat(K_Music,  0.8f));
            _sfx    = Mathf.Clamp01(PlayerPrefs.GetFloat(K_SFX,    1f));
        }
    }
}
