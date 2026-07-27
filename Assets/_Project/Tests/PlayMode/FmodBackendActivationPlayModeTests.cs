using System.Collections;
using Desk42.Audio;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Desk42.Tests.PlayMode
{
    /// <summary>
    /// D1 State B — positive verification that the FMOD backend is the one
    /// actually installed behind AudioService, and that touching the native
    /// runtime does not throw into gameplay.
    ///
    /// STATE-AWARE BY DESIGN. Desk-42 is a public repository whose committed
    /// configuration has DESK42_FMOD undefined, so in State A these tests
    /// assert the NullAudioBackend contract instead of failing. Both states
    /// must report zero failures.
    ///
    /// WHAT THIS DOES NOT CLAIM: nothing here is evidence that a participant
    /// heard anything. It verifies wiring and native initialisation only.
    /// Audible output requires human confirmation.
    /// </summary>
    public sealed class FmodBackendActivationPlayModeTests
    {
        /// <summary>
        /// These tests deliberately drive the native FMOD path in an
        /// environment that may have no banks authored. The backend's contract
        /// is to CATCH and LOG such failures rather than throw, so its
        /// diagnostics are the expected output, not a test failure — and FMOD's
        /// own RuntimeManager logs an unhandled exception before rethrowing,
        /// which no amount of correctness on our side suppresses.
        ///
        /// Must be called inside the test body: the flag does not survive
        /// [SetUp] for [UnityTest] coroutines.
        /// </summary>
        private static void TolerateNativeDiagnostics()
            => LogAssert.ignoreFailingMessages = true;

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            AudioService.ResetToNull();
        }

        [UnityTest]
        public IEnumerator AudioService_UsesTheExpectedBackendForThisBuildState()
        {
            AudioService.SetBackend(new FmodAudioBackend());
            yield return null;

            Assert.AreEqual("FmodAudioBackend", AudioService.BackendName,
                "AudioService must route through FmodAudioBackend once installed, " +
                "never silently fall back to NullAudioBackend.");

#if DESK42_FMOD
            // The real backend compiles in. Availability still depends on banks,
            // which is asserted separately — presence is the claim here.
            Assert.IsInstanceOf<IAudioBackend>(new FmodAudioBackend());
#else
            // Stub build: the backend exists as a type but must report itself
            // unavailable, so a missing package stays diagnosable.
            Assert.IsFalse(AudioService.IsAvailable,
                "Without DESK42_FMOD the backend must report unavailable.");
#endif
        }

        /// <summary>
        /// Exercises the native load path. A missing or mismatched
        /// fmodstudio.dll surfaces here as a caught, logged failure rather
        /// than an exception escaping into a shift.
        /// </summary>
        [UnityTest]
        public IEnumerator BackendInitialise_NeverThrowsIntoGameplay()
        {
            TolerateNativeDiagnostics();
            var backend = new FmodAudioBackend();

            Assert.DoesNotThrow(() => backend.Initialize(1),
                "Initialize must absorb native failures, not throw into gameplay.");
            yield return null;

            Assert.DoesNotThrow(() => { var _ = backend.IsAvailable; });
        }

        /// <summary>
        /// The load-bearing honesty check: with no authored banks, a request
        /// must never come back Requested. Requested means "handed to FMOD",
        /// and fabricating it here would manufacture evidence of working audio.
        /// </summary>
        [UnityTest]
        public IEnumerator WithoutBanks_RequestIsNeverReportedAsRequested()
        {
            TolerateNativeDiagnostics();
            AudioService.SetBackend(new FmodAudioBackend());
            AudioService.Initialize(1);
            yield return null;

            var result = AudioService.PlayOneShot(
                AudioEventId.DeskInteraction, new AudioRequestContext(1));

            if (AudioService.IsAvailable)
            {
                // Banks are loaded in this environment: either the technical
                // event resolved, or it is genuinely absent from the banks.
                Assert.That(result,
                    Is.EqualTo(AudioRequestResult.Requested)
                        .Or.EqualTo(AudioRequestResult.UnknownEvent),
                    "With banks loaded the result must be a real outcome.");
            }
            else
            {
                Assert.AreNotEqual(AudioRequestResult.Requested, result,
                    "No banks loaded, so playback must not be reported as requested.");
            }
        }

        /// <summary>
        /// Shift 5 must not be able to acoustically name the Shift 2 cause,
        /// regardless of which backend is installed.
        /// </summary>
        [UnityTest]
        public IEnumerator Shift5_CannotRequestTheCausalIdentity_WithFmodInstalled()
        {
            TolerateNativeDiagnostics();
            AudioService.SetBackend(new FmodAudioBackend());
            AudioService.Initialize(5);
            yield return null;

            var result = AudioService.PlayOneShot(
                AudioEventId.EliasRegistrationCausal, new AudioRequestContext(5));

            Assert.AreEqual(AudioRequestResult.Suppressed, result,
                "The causal identity must be refused at the boundary on Shift 5 " +
                "even when a real FMOD backend is installed.");
        }
    }
}
