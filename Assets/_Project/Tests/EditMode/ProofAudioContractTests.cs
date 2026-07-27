using System.Collections.Generic;
using System.Linq;
using Desk42.Audio;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Package-independent D1 coverage. These prove the CONTRACT and the
    /// no-FMOD safety path. None of them claims FMOD playback works — that is
    /// unprovable until the package exists.
    /// </summary>
    public sealed class ProofAudioContractTests
    {
        private sealed class RecordingBackend : IAudioBackend
        {
            public readonly List<(ProofAudioEvent id, string path)> Calls = new();
            public bool Available = true;
            public bool Throw;
            public int InitCount;

            public bool IsAvailable => Available;
            public void Initialize(int shiftNumber) => InitCount++;

            public AudioRequestResult PlayOneShot(
                ProofAudioEvent id, string path, AudioRequestContext ctx)
            {
                if (Throw) throw new System.InvalidOperationException("backend blew up");
                Calls.Add((id, path));
                return AudioRequestResult.Requested;
            }
        }

        [TearDown]
        public void TearDown() => AudioService.ResetToNull();

        // ── No-FMOD safety ───────────────────────────────────

        [Test]
        public void WithoutFmod_ServiceIsUnavailableAndNoOps()
        {
            AudioService.ResetToNull();

            Assert.IsFalse(AudioService.IsAvailable);
            Assert.AreEqual(AudioRequestResult.Unavailable,
                AudioService.PlayOneShot(ProofAudioEvent.DeskInteraction),
                "A missing package must be diagnosable, not silently 'fine'.");
        }

        [Test]
        public void UnknownEvent_IsReportedNotThrown()
            => Assert.AreEqual(AudioRequestResult.UnknownEvent,
                AudioService.PlayOneShot(ProofAudioEvent.None));

        [Test]
        public void BackendException_DoesNotEscapeIntoGameplay()
        {
            AudioService.SetBackend(new RecordingBackend { Throw = true });

            Assert.DoesNotThrow(() =>
                AudioService.PlayOneShot(ProofAudioEvent.DeskInteraction));
            Assert.AreEqual(AudioRequestResult.Unavailable,
                AudioService.PlayOneShot(ProofAudioEvent.DeskInteraction));
        }

        [Test]
        public void Initialize_IsSafeWithoutABackend()
        {
            AudioService.ResetToNull();
            Assert.DoesNotThrow(() => AudioService.Initialize(1));
            Assert.AreEqual(1, AudioService.CurrentShift);
        }

        [Test]
        public void OneShot_RoutesToBackendExactlyOnce()
        {
            var backend = new RecordingBackend();
            AudioService.SetBackend(backend);

            var result = AudioService.PlayOneShot(
                ProofAudioEvent.DeskInteraction, new AudioRequestContext(1));

            Assert.AreEqual(AudioRequestResult.Requested, result);
            Assert.AreEqual(1, backend.Calls.Count);
            Assert.AreEqual("event:/Desk/Interaction", backend.Calls[0].path);
        }

        // ── Event identity contract ──────────────────────────

        [Test]
        public void EveryContractIdentity_HasAUniqueStablePath()
        {
            var paths = ProofAudioCatalog.All
                .Select(ProofAudioCatalog.TryGetPath).ToList();

            CollectionAssert.AllItemsAreNotNull(paths);
            CollectionAssert.AllItemsAreUnique(paths);
            CollectionAssert.AllItemsAreUnique(ProofAudioCatalog.All.ToList());
        }

        [Test]
        public void ContractCoversTheFiveRequiredCategories()
        {
            foreach (var required in new[]
                     {
                         ProofAudioEvent.DeskInteraction,
                         ProofAudioEvent.ProcedureFeedback,
                         ProofAudioEvent.EliasRegistrationCausal,
                         ProofAudioEvent.ComplianceStreakConfirm,
                         ProofAudioEvent.Shift5EliasReturn,
                     })
                Assert.IsNotNull(ProofAudioCatalog.TryGetPath(required), $"{required} unmapped.");
        }

        [Test]
        public void Shift2Causal_AndShift5Return_AreDistinctIdentities()
        {
            Assert.AreNotEqual(ProofAudioEvent.EliasRegistrationCausal,
                ProofAudioEvent.Shift5EliasReturn);
            Assert.AreNotEqual(
                ProofAudioCatalog.TryGetPath(ProofAudioEvent.EliasRegistrationCausal),
                ProofAudioCatalog.TryGetPath(ProofAudioEvent.Shift5EliasReturn),
                "The return must not resolve to the causal motif.");
            Assert.IsTrue(ProofAudioCatalog.IsCausalIdentity(
                ProofAudioEvent.EliasRegistrationCausal));
            Assert.IsFalse(ProofAudioCatalog.IsCausalIdentity(
                ProofAudioEvent.Shift5EliasReturn));
        }

        // ── Shift 5 suppression is structural ────────────────

        [Test]
        public void CausalIdentity_IsRefusedOnShift5_AtTheBoundary()
        {
            var backend = new RecordingBackend();
            AudioService.SetBackend(backend);

            var result = AudioService.PlayOneShot(
                ProofAudioEvent.EliasRegistrationCausal, new AudioRequestContext(5));

            Assert.AreEqual(AudioRequestResult.Suppressed, result);
            Assert.IsEmpty(backend.Calls,
                "The experiment must not acoustically name the earlier cause.");
        }

        [Test]
        public void CausalIdentity_IsAllowedOnShift2()
        {
            var backend = new RecordingBackend();
            AudioService.SetBackend(backend);

            Assert.AreEqual(AudioRequestResult.Requested,
                AudioService.PlayOneShot(
                    ProofAudioEvent.EliasRegistrationCausal, new AudioRequestContext(2)));
            Assert.AreEqual(1, backend.Calls.Count);
        }

        [Test]
        public void GenericReturn_IsPermittedOnShift5()
        {
            var backend = new RecordingBackend();
            AudioService.SetBackend(backend);

            Assert.AreEqual(AudioRequestResult.Requested,
                AudioService.PlayOneShot(
                    ProofAudioEvent.Shift5EliasReturn, new AudioRequestContext(5)));
            Assert.IsTrue(ProofAudioCatalog.IsPermittedOnShift5(
                ProofAudioEvent.Shift5EliasReturn));
            Assert.IsFalse(ProofAudioCatalog.IsPermittedOnShift5(
                ProofAudioEvent.EliasRegistrationCausal));
        }

        // ── Exclusions ───────────────────────────────────────

        [Test]
        public void MercyAndFlow_AreNotInTheProofContract()
        {
            foreach (var id in ProofAudioCatalog.All)
            {
                string path = ProofAudioCatalog.TryGetPath(id) ?? "";
                Assert.IsFalse(path.Contains("Mercy"), $"{id} maps to a Mercy state.");
                Assert.IsFalse(path.Contains("Flow"), $"{id} maps to a Flow state.");
            }
            Assert.IsFalse(System.Enum.GetNames(typeof(ProofAudioEvent))
                .Any(n => n.Contains("Mercy") || n.Contains("Flow")));
        }

        [Test]
        public void ControlClaimant_HasNoEliasCausalAudioMapping()
        {
            // Mara Kest has no audio identity of her own and, critically, no
            // route to the Elias causal identity.
            foreach (var id in ProofAudioCatalog.All)
            {
                string path = ProofAudioCatalog.TryGetPath(id) ?? "";
                Assert.IsFalse(path.Contains("Mara") || path.Contains("control_"),
                    $"{id} maps to the control claimant.");
            }
        }

        // ── Gameplay must not reach FMOD directly ────────────

        [Test]
        public void GameplayCode_DoesNotReferenceFmodDirectly()
        {
            string[] files = System.IO.Directory.GetFiles(
                "Assets/_Project/Scripts", "*.cs", System.IO.SearchOption.AllDirectories);

            foreach (string f in files)
            {
                string norm = f.Replace('\\', '/');
                // Only the Audio layer may name FMOD types at all.
                if (norm.Contains("/Scripts/Audio/")) continue;

                string src = System.IO.File.ReadAllText(f);
                Assert.IsFalse(src.Contains("FMODUnity.RuntimeManager"),
                    $"Gameplay file calls RuntimeManager directly: {norm}");
            }
        }

        [Test]
        public void AudioBoundary_HasNoUnconditionalFmodReference()
        {
            foreach (string file in new[]
                     {
                         "Assets/_Project/Scripts/Audio/AudioService.cs",
                         "Assets/_Project/Scripts/Audio/ProofAudioEvents.cs",
                     })
            {
                string src = System.IO.File.ReadAllText(file);
                Assert.IsFalse(src.Contains("using FMOD"),
                    $"{file} must compile without the FMOD package.");
                Assert.IsFalse(src.Contains("FMODUnity."),
                    $"{file} must not reference FMOD types.");
            }
        }
    }
}
