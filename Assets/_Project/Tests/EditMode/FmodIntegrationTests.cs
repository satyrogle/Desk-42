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
        [Test]
        public void Desk42FmodDefine_IsEnabled()
        {
            // Positive proof the define is ON. Without this the FMOD-gated
            // code compiles as stubs and every other test still passes, which
            // would make a failed activation invisible.
#if DESK42_FMOD
            Assert.Pass("DESK42_FMOD is defined.");
#else
            Assert.Fail("DESK42_FMOD is NOT defined — FMOD-gated code is stubbed.");
#endif
        }

        [Test]
        public void FmodPackage_IsPresentAtTheConventionalLocation()
        {
            Assert.IsTrue(Directory.Exists("Assets/Plugins/FMOD"),
                "FMOD must live at the conventional Assets/Plugins/FMOD.");
            Assert.IsTrue(File.Exists("Assets/Plugins/FMOD/FMODUnity.asmdef"),
                "FMODUnity.asmdef missing — the assembly reference cannot resolve.");
        }

        [Test]
        public void FmodVersion_IsTheLocked_2_03_14()
        {
            // fmod.cs carries the native version as a packed constant.
            const string fmodSrc = "Assets/Plugins/FMOD/src/fmod.cs";
            Assert.IsTrue(File.Exists(fmodSrc), $"{fmodSrc} missing.");

            Assert.IsTrue(File.ReadAllText(fmodSrc).Contains("0x00020314"),
                "FMOD version is not the locked 2.03.14 (0x00020314).");
        }

        [Test]
        public void WindowsX64_NativeLibrariesArePresent()
        {
            Assert.IsTrue(
                File.Exists("Assets/Plugins/FMOD/platforms/win/lib/x86_64/fmodstudio.dll"),
                "Windows x64 native library missing — standalone would " +
                "DllNotFoundException at runtime.");
        }

        [Test]
        public void CoreAssembly_ReferencesFmodUnity()
        {
            string asmdef = File.ReadAllText("Assets/_Project/Scripts/Desk42.Core.asmdef");
            Assert.IsTrue(asmdef.Contains("FMODUnity"),
                "Desk42.Core must reference FMODUnity or the gated code cannot compile.");
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
