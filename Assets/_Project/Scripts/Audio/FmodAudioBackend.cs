// ============================================================
// DESK 42 — FMOD backend for AudioService (D1 step 5)
//
// The ONLY place FMOD is reached from the proof audio path:
//
//   gameplay -> AudioService -> FmodAudioBackend -> FMOD
//
// Gameplay never sees FMODUnity, RuntimeManager, bank handles or event
// paths. FMODManager stays a backend-internal helper.
//
// Compiles to a clearly-unavailable stub when DESK42_FMOD is undefined,
// so the file is safe to keep in a build without the package.
//
// FAILURE POSTURE: a missing bank or event is DIAGNOSABLE, never silent.
// PlayOneShot returns Unavailable and logs the specific event path rather
// than reporting a fabricated success — AudioRequestResult.Requested must
// continue to mean "handed to FMOD", not "the participant heard it".
// ============================================================

using UnityEngine;

namespace Desk42.Audio
{
    /// <summary>
    /// FMOD-backed one-shot playback. Installed by FmodAudioBootstrap after
    /// the package is present; otherwise AudioService keeps NullAudioBackend.
    /// </summary>
    public sealed class FmodAudioBackend : IAudioBackend
    {
#if DESK42_FMOD
        private bool _initialised;
        private bool _banksLoaded;

        /// <summary>
        /// True only when FMOD's core system is actually running. Reported
        /// honestly so a broken install is distinguishable from silence.
        /// </summary>
        public bool IsAvailable
        {
            get
            {
                try
                {
                    return FMODUnity.RuntimeManager.IsInitialized && _banksLoaded;
                }
                catch (System.Exception)
                {
                    // RuntimeManager throws if native init failed outright.
                    return false;
                }
            }
        }

        /// <summary>
        /// Number of banks currently loaded, or 0 if that cannot be
        /// determined. Never throws: it is called from the initialisation
        /// path whose whole job is to absorb native faults.
        /// </summary>
        private static int LoadedBankCount()
        {
            try
            {
                var result = FMODUnity.RuntimeManager.StudioSystem
                    .getBankCount(out int count);
                return result == FMOD.RESULT.OK ? count : 0;
            }
            catch (System.Exception)
            {
                return 0;
            }
        }

        public void Initialize(int shiftNumber)
        {
            if (_initialised) return;

            try
            {
                // Touching StudioSystem forces native init and surfaces a
                // DllNotFoundException here rather than mid-encounter.
                _ = FMODUnity.RuntimeManager.StudioSystem;

                // HaveAllBanksLoaded is VACUOUSLY TRUE when no banks are
                // configured at all — every one of zero banks is loaded. On
                // its own it made the backend log "FMOD ready" against an
                // empty project, which is exactly the fabricated-readiness
                // signal this class exists to avoid. Require at least one
                // actual bank.
                _banksLoaded = FMODUnity.RuntimeManager.HaveAllBanksLoaded
                               && LoadedBankCount() > 0;
                _initialised = true;

                if (!_banksLoaded)
                {
                    Debug.LogWarning(
                        $"[FmodAudioBackend] FMOD initialised but banks are NOT " +
                        $"usable (loaded bank count = {LoadedBankCount()}). Check " +
                        $"the Studio project's build output and the bank load " +
                        $"settings in the FMOD Unity integration. Audio will report " +
                        $"Unavailable rather than failing silently.");
                }
                else
                {
                    Debug.Log($"[FmodAudioBackend] FMOD ready for shift " +
                              $"{shiftNumber} ({LoadedBankCount()} bank(s) loaded).");
                }
            }
            catch (System.Exception ex)
            {
                _initialised = false;
                _banksLoaded = false;
                Debug.LogError(
                    $"[FmodAudioBackend] FMOD initialisation FAILED: {ex.GetType().Name}: " +
                    $"{ex.Message}. Continuing silent — this is a real integration " +
                    $"fault, not an authoring gap.");
            }
        }

        public AudioRequestResult PlayOneShot(
            AudioEventId id, string resolvedPath, AudioRequestContext context)
        {
            if (string.IsNullOrWhiteSpace(resolvedPath))
                return AudioRequestResult.UnknownEvent;

            if (!IsAvailable)
                return AudioRequestResult.Unavailable;

            try
            {
                // Fail loudly on an unauthored event rather than pretending it
                // played: an absent event is an authoring gap we must see.
                var desc = FMODUnity.RuntimeManager.GetEventDescription(resolvedPath);
                if (!desc.isValid())
                {
                    Debug.LogWarning(
                        $"[FmodAudioBackend] Event not found in loaded banks: " +
                        $"'{resolvedPath}' (identity {id}).");
                    return AudioRequestResult.UnknownEvent;
                }

                if (context.WorldPosition.HasValue)
                    FMODUnity.RuntimeManager.PlayOneShot(
                        resolvedPath, context.WorldPosition.Value);
                else
                    FMODUnity.RuntimeManager.PlayOneShot(resolvedPath);

                return AudioRequestResult.Requested;
            }
            catch (FMODUnity.EventNotFoundException)
            {
                // GetEventDescription THROWS for an unauthored path rather than
                // returning an invalid handle, so the desc.isValid() branch
                // above never runs. Without this case a missing event reports
                // Unavailable — conflating "the backend is broken" with "nobody
                // has authored this yet", which is the exact distinction the
                // result enum exists to preserve.
                Debug.LogWarning(
                    $"[FmodAudioBackend] Event not found in loaded banks: " +
                    $"'{resolvedPath}' (identity {id}).");
                return AudioRequestResult.UnknownEvent;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"[FmodAudioBackend] PlayOneShot failed for '{resolvedPath}': " +
                    $"{ex.Message}.");
                return AudioRequestResult.Unavailable;
            }
        }
#else
        /// <summary>
        /// DESK42_FMOD is undefined: the backend cannot do anything. Reporting
        /// Unavailable keeps a missing package diagnosable.
        /// </summary>
        public bool IsAvailable => false;

        public void Initialize(int shiftNumber) { }

        public AudioRequestResult PlayOneShot(
            AudioEventId id, string resolvedPath, AudioRequestContext context)
            => AudioRequestResult.Unavailable;
#endif
    }

    /// <summary>
    /// Installs the FMOD backend and drives AudioService.Initialize from the
    /// existing ShiftLifecycleEvent.
    ///
    /// The lifecycle event is published AFTER scene activation, because
    /// LoadSceneAsync's FlushQueue wipes pending events beforehand (B5).
    /// Audio tolerates that ordering rather than moving it: the backend is
    /// installed at load, and initialisation follows whenever the shift
    /// actually starts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FmodAudioBootstrap : MonoBehaviour
    {
#if DESK42_FMOD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            AudioService.SetBackend(new FmodAudioBackend());

            var host = new GameObject("[FmodAudioBootstrap]");
            host.AddComponent<FmodAudioBootstrap>();
            DontDestroyOnLoad(host);
        }

        private void OnEnable()
            => Core.RumorMill.OnShiftLifecycle += HandleShiftLifecycle;

        private void OnDisable()
            => Core.RumorMill.OnShiftLifecycle -= HandleShiftLifecycle;

        private void HandleShiftLifecycle(Core.ShiftLifecycleEvent e)
        {
            if (e.IsStart) AudioService.Initialize(e.ShiftNumber);
        }
#endif
    }
}
