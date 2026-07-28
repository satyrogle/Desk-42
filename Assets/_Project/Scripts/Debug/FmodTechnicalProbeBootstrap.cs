#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Collections;
using Desk42.Audio;
using UnityEngine;

namespace Desk42.Debugging
{
    /// <summary>
    /// D1 step 9 — standalone technical-transport probe.
    ///
    /// Inert unless <c>-desk42FmodTechnicalProbe</c> is supplied, so it adds no
    /// gameplay surface and cannot fire during a scored proof run. It exists
    /// because "the banks loaded" is NOT the same claim as "a request reached
    /// FMOD in a shipped player", and the second claim needs its own evidence.
    ///
    /// Requests the NON-PRODUCTION technical event exactly once through the
    /// ordinary gameplay boundary (AudioService, by logical identity) and logs
    /// the AudioRequestResult.
    ///
    /// WHAT THIS PROVES: invocation reached the backend.
    /// WHAT IT DOES NOT PROVE: that any sound was audible. Requested means
    /// "handed to FMOD", never "heard". Audibility needs a human.
    /// </summary>
    public sealed class FmodTechnicalProbeBootstrap : MonoBehaviour
    {
        private const string ProbeArgument = "-desk42FmodTechnicalProbe";

        private static bool _created;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfRequested()
        {
            if (_created) return;
            if (!HasArgument(ProbeArgument)) return;

            _created = true;
            Application.runInBackground = true;

            var host = new GameObject(nameof(FmodTechnicalProbeBootstrap));
            DontDestroyOnLoad(host);
            host.AddComponent<FmodTechnicalProbeBootstrap>()
                .StartCoroutine(Probe());
        }

        private static IEnumerator Probe()
        {
            // The backend is installed before scene load, but Initialize is
            // driven by ShiftLifecycleEvent. Drive it here so the probe does
            // not depend on reaching a shift.
            AudioService.Initialize(1);
            yield return null;

            Debug.Log($"[FmodTechnicalProbe] backend={AudioService.BackendName} " +
                      $"available={AudioService.IsAvailable}");

            AudioRequestResult direct = AudioService.PlayOneShot(
                AudioEventId.DeskInteraction, new AudioRequestContext(1));
            Debug.Log($"[FmodTechnicalProbe] DeskInteraction => {direct}");

            // Positional variant: coordinates must survive the boundary.
            AudioRequestResult positional = AudioService.PlayOneShot(
                AudioEventId.PneumaticTubeThreat,
                new AudioRequestContext(1, worldPosition: new Vector3(1f, 2f, 3f)));
            Debug.Log($"[FmodTechnicalProbe] PneumaticTubeThreat(1,2,3) => {positional}");

            // The Elias causal slot is deliberately unauthored; it must report
            // UnknownEvent, proving the diagnostic works in a shipped player.
            AudioRequestResult unauthored = AudioService.PlayOneShot(
                AudioEventId.EliasRegistrationCausal, new AudioRequestContext(2));
            Debug.Log($"[FmodTechnicalProbe] EliasRegistrationCausal => {unauthored}");

            // Let the one-shot run so a listener could in principle hear it.
            yield return new WaitForSeconds(1.5f);

            Debug.Log("[FmodTechnicalProbe] RESULT " +
                      (direct == AudioRequestResult.Requested ? "PASS" : "FAIL") +
                      " (invocation only; audibility NOT machine-observable)");
            Application.Quit(direct == AudioRequestResult.Requested ? 0 : 1);
        }

        private static bool HasArgument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], key, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}

#endif
