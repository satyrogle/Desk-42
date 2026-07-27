using System.IO;
using Desk42.Audio;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// D1 binary integration verification.
    ///
    /// These assert the INTEGRATION, not that a participant heard anything.
    /// Actual playback is verified in the Editor/standalone passes; nothing
    /// here claims authored audio exists.
    /// </summary>
    public sealed class FmodIntegrationTests
    {
        /// <summary>
        /// Desk-42 is a PUBLIC repository, so the vendor FMOD package is not
        /// committed. DESK42_FMOD is therefore deliberately OFF in committed
        /// settings — committing it while the plugin is untracked made every
        /// clean clone fail to compile.
        ///
        /// The define is a LOCAL activation, enabled only in an environment
        /// that has imported the package. This test asserts the two states are
        /// consistent rather than demanding either one.
        /// </summary>
        [Test]
        public void FmodActivation_MatchesPackagePresence()
        {
            bool packagePresent = Directory.Exists("Assets/Plugins/FMOD");

#if DESK42_FMOD
            Assert.IsTrue(packagePresent,
                "DESK42_FMOD is defined but Assets/Plugins/FMOD is absent — " +
                "this configuration cannot compile. Import the package or " +
                "clear the define.");
#else
            Assert.Pass(packagePresent
                ? "Package imported locally; define not yet enabled."
                : "No package and no define — consistent committed state.");
#endif
        }

        [Test]
        public void FmodPackage_WhenPresent_IsAtTheConventionalLocation()
        {
            if (!Directory.Exists("Assets/Plugins/FMOD"))
                Assert.Ignore("FMOD not imported in this environment (public repo).");

            Assert.IsTrue(Directory.Exists("Assets/Plugins/FMOD"),
                "FMOD must live at the conventional Assets/Plugins/FMOD.");
            Assert.IsTrue(File.Exists("Assets/Plugins/FMOD/FMODUnity.asmdef"),
                "FMODUnity.asmdef missing — the assembly reference cannot resolve.");
        }

        [Test]
        public void FmodVersion_IsTheLocked_2_03_14()
        {
            if (!Directory.Exists("Assets/Plugins/FMOD"))
                Assert.Ignore("FMOD not imported in this environment.");

            // fmod.cs carries the native version as a packed constant.
            const string fmodSrc = "Assets/Plugins/FMOD/src/fmod.cs";
            Assert.IsTrue(File.Exists(fmodSrc), $"{fmodSrc} missing.");

            Assert.IsTrue(File.ReadAllText(fmodSrc).Contains("0x00020314"),
                "FMOD version is not the locked 2.03.14 (0x00020314).");
        }

        [Test]
        public void WindowsX64_NativeLibrariesArePresent()
        {
            if (!Directory.Exists("Assets/Plugins/FMOD"))
                Assert.Ignore("FMOD not imported in this environment.");

            Assert.IsTrue(
                File.Exists("Assets/Plugins/FMOD/platforms/win/lib/x86_64/fmodstudio.dll"),
                "Windows x64 native library missing — standalone would " +
                "DllNotFoundException at runtime.");
        }

        [Test]
        public void CoreAssembly_ReferencesFmodUnity()
        {
            string asmdef = File.ReadAllText("Assets/_Project/Scripts/Desk42.Core.asmdef");
#if DESK42_FMOD
            Assert.IsTrue(asmdef.Contains("FMODUnity"),
                "DESK42_FMOD is on, so Desk42.Core must reference FMODUnity.");
#else
            Assert.IsFalse(asmdef.Contains("FMODUnity"),
                "Committed settings must not reference FMODUnity while the " +
                "vendor package is untracked — a clean clone could not compile.");
#endif
        }

        [Test]
        public void FmodBackend_ExistsAndImplementsTheBoundary()
        {
            var backend = new FmodAudioBackend();
            Assert.IsInstanceOf<IAudioBackend>(backend);

            // Safe to probe before initialisation; must not throw.
            Assert.DoesNotThrow(() => { var _ = backend.IsAvailable; });
        }

        [Test]
        public void MissingEventOrBank_FailsSafelyAndVisibly()
        {
            // With no banks authored yet the backend must report Unavailable or
            // UnknownEvent — never a fabricated Requested.
            var backend = new FmodAudioBackend();
            var result = backend.PlayOneShot(
                AudioEventId.DeskInteraction,
                AudioEventCatalog.TryGetPath(AudioEventId.DeskInteraction),
                new AudioRequestContext(1));

            Assert.AreNotEqual(AudioRequestResult.Requested, result,
                "Playback must not be reported as requested when banks/events " +
                "are absent — that would fabricate evidence of working audio.");
        }

        [Test]
        public void BackendStillRoutesThroughAudioService_NotDirectly()
        {
            // The gameplay-facing boundary is unchanged by the binary step.
            foreach (string file in Directory.GetFiles(
                         "Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string norm = file.Replace('\\', '/');
                if (norm.Contains("/Scripts/Audio/")) continue;

                string src = File.ReadAllText(file);
                Assert.IsFalse(src.Contains("FMODUnity.RuntimeManager"),
                    $"Gameplay file reaches FMOD directly: {norm}");
                Assert.IsFalse(src.Contains("FMODManager."),
                    $"Gameplay file uses the backend-internal helper: {norm}");
            }
        }
    }
}
