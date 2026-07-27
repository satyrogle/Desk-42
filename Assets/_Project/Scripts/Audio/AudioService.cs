// ============================================================
// DESK 42 — AudioService boundary (D1A)
//
// The single application-facing audio abstraction. Gameplay requests a
// LOGICAL identity (ProofAudioEvent); it never touches FMODUnity,
// RuntimeManager, bank handles or event paths.
//
// This file has NO unconditional FMOD reference and compiles with
// DESK42_FMOD undefined. Until the package is installed the active
// backend is NullAudioBackend, which safely no-ops and reports
// Unavailable — silence, never an exception, never a fabricated success.
//
// Deliberately NOT included: mixer, music system, parameters, snapshots,
// voices, buses. D1 is a proof slice, not the long-term audio design.
//
// LIFECYCLE: Initialize(shift) is called from the existing
// ShiftLifecycleEvent, which GameManager publishes AFTER scene activation
// because LoadSceneAsync's FlushQueue wipes pending events beforehand
// (B5). Audio tolerates that ordering rather than forcing it earlier:
// requests before Initialize are answered Unavailable, not queued or
// dropped silently.
// ============================================================

using System;
using UnityEngine;

namespace Desk42.Audio
{
    /// <summary>Backend contract. One implementation is active at a time.</summary>
    public interface IAudioBackend
    {
        bool IsAvailable { get; }

        /// <summary>Prepare for a shift. Must be safe to call repeatedly.</summary>
        void Initialize(int shiftNumber);

        /// <summary>
        /// Fire a one-shot. Returning Requested means "handed to the backend",
        /// NOT "the participant heard it" — only a real FMOD implementation can
        /// confirm invocation.
        /// </summary>
        AudioRequestResult PlayOneShot(ProofAudioEvent id, string resolvedPath,
            AudioRequestContext context);
    }

    /// <summary>
    /// Active backend when FMOD is absent. Reports Unavailable so a missing
    /// package is diagnosable rather than silently indistinguishable from a
    /// working install that happens to be quiet.
    /// </summary>
    public sealed class NullAudioBackend : IAudioBackend
    {
        public bool IsAvailable => false;

        public void Initialize(int shiftNumber) { }

        public AudioRequestResult PlayOneShot(
            ProofAudioEvent id, string resolvedPath, AudioRequestContext context)
            => AudioRequestResult.Unavailable;
    }

    /// <summary>
    /// The application-facing audio boundary. Static so gameplay call sites
    /// stay trivial, with an injectable backend so tests need no FMOD.
    /// </summary>
    public static class AudioService
    {
        private static IAudioBackend _backend = new NullAudioBackend();
        private static bool _initialized;
        private static int _currentShift;

        /// <summary>Every request, for passive observers. Never triggers playback.</summary>
        public static event Action<AudioRequestObservation> RequestObserved;

        public static bool IsAvailable => _backend?.IsAvailable ?? false;
        public static bool IsInitialized => _initialized;
        public static int CurrentShift => _currentShift;

        /// <summary>Backend name, for provenance logging.</summary>
        public static string BackendName => _backend?.GetType().Name ?? "none";

        /// <summary>
        /// Installs a backend. Called by the FMOD bootstrap after the package
        /// is imported; tests use it to inject a recording double.
        /// </summary>
        public static void SetBackend(IAudioBackend backend)
        {
            _backend = backend ?? new NullAudioBackend();
            _initialized = false;
            Debug.Log($"[AudioService] Backend set: {BackendName} " +
                      $"(available={_backend.IsAvailable}).");
        }

        /// <summary>Restores the safe default. Used by test teardown.</summary>
        public static void ResetToNull() => SetBackend(new NullAudioBackend());

        /// <summary>
        /// Shift lifecycle boundary. Safe to call repeatedly and safe to call
        /// when no backend exists.
        /// </summary>
        public static void Initialize(int shiftNumber)
        {
            _currentShift = shiftNumber;

            try
            {
                _backend.Initialize(shiftNumber);
                _initialized = true;
            }
            catch (Exception ex)
            {
                _initialized = false;
                Debug.LogWarning(
                    $"[AudioService] Backend initialise failed for shift " +
                    $"{shiftNumber}: {ex.Message}. Continuing silent.");
            }
        }

        /// <summary>
        /// Request a one-shot by logical identity.
        ///
        /// Never throws. Shift 5 suppression of the Shift 2 causal identity is
        /// enforced HERE, at the boundary, so no call site can bypass it.
        /// </summary>
        public static AudioRequestResult PlayOneShot(
            ProofAudioEvent id, AudioRequestContext context = default)
        {
            string path = ProofAudioCatalog.TryGetPath(id);
            AudioRequestResult result;

            if (id == ProofAudioEvent.None || path == null)
            {
                result = AudioRequestResult.UnknownEvent;
                Debug.LogWarning($"[AudioService] No mapping for '{id}'.");
            }
            else if (context.ShiftNumber == 5 && ProofAudioCatalog.IsCausalIdentity(id))
            {
                // Locked: the scored return must not acoustically name the
                // earlier cause. Refused at the boundary, not by convention.
                result = AudioRequestResult.Suppressed;
                Debug.LogWarning(
                    $"[AudioService] SUPPRESSED causal identity '{id}' on Shift 5.");
            }
            else
            {
                try
                {
                    result = _backend.PlayOneShot(id, path, context);
                }
                catch (Exception ex)
                {
                    result = AudioRequestResult.Unavailable;
                    Debug.LogWarning(
                        $"[AudioService] Backend threw for '{id}': {ex.Message}.");
                }
            }

            try
            {
                RequestObserved?.Invoke(
                    new AudioRequestObservation(id, result, context, path));
            }
            catch (Exception ex)
            {
                // An observer must never be able to affect gameplay.
                Debug.LogWarning($"[AudioService] Observer threw: {ex.Message}.");
            }

            return result;
        }
    }
}
