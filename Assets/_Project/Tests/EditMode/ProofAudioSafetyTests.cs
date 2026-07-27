using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Desk42.Audio;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// D1 pre-import safety closeout.
    ///
    /// Two hazards are locked here. First, four experimental audio directors
    /// are ATTACHED to Shift.unity and currently compile to no-ops only
    /// because DESK42_FMOD is undefined — defining it would make them live
    /// during the scored proof run. Second, the Shift 5 return must never
    /// request the Shift 2 causal identity, and that must fail at the CALLER,
    /// not be silently absorbed by the AudioService guard.
    /// </summary>
    public sealed class ProofAudioSafetyTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Shift.unity";

        // Script GUIDs — scenes reference scripts by GUID, so a class-name
        // search matches nothing and would report a false pass.
        private static readonly Dictionary<string, string> ExperimentalDirectors = new()
        {
            ["BinauralStressEngine"]     = "a352ef7c2114b2843b8653fe89f83756",
            ["ProceduralJazzGenerator"]  = "28f9c6f99c25e9d41947bfb9581197f9",
            ["StressCrescendo"]          = "5fe965bb406181a4396847a25eab55e7",
            ["SpatialAudioThreatSystem"] = "dea70daac3224db42b8c764cb90e3f9b",
        };

        private const string DistortionAudioDirectorGuid =
            "58bcb9d3"; // prefix only — asserted ABSENT from the scene

        private static readonly Regex ScriptLine = new(
            @"^\s*m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32}),", RegexOptions.Compiled);

        private static readonly Regex EnabledLine = new(
            @"^\s*m_Enabled:\s*([01])\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Enabled flags for every component in the scene referencing a GUID.
        /// Empty list means the component is not attached at all.
        /// </summary>
        private static List<int> EnabledFlagsFor(string guid)
        {
            Assert.IsTrue(File.Exists(ScenePath), $"Scene not found: {ScenePath}");
            string[] lines = File.ReadAllLines(ScenePath);
            var flags = new List<int>();

            for (int i = 0; i < lines.Length; i++)
            {
                var m = ScriptLine.Match(lines[i]);
                if (!m.Success || m.Groups[1].Value != guid) continue;

                for (int j = i - 1; j >= 0 && j > i - 40; j--)
                {
                    if (lines[j].TrimStart().StartsWith("--- !u!")) break;
                    var em = EnabledLine.Match(lines[j]);
                    if (!em.Success) continue;
                    flags.Add(int.Parse(em.Groups[1].Value));
                    break;
                }
            }
            return flags;
        }

        // ── Directors must not go live when the define flips ──

        [Test]
        public void ExperimentalDirectors_AreDisabledInShiftScene()
        {
            foreach (var (name, guid) in ExperimentalDirectors)
            {
                var flags = EnabledFlagsFor(guid);

                Assert.IsNotEmpty(flags,
                    $"{name} was expected to be attached to Shift.unity. If it was " +
                    $"removed, update this test deliberately rather than losing the guard.");

                foreach (int flag in flags)
                    Assert.AreEqual(0, flag,
                        $"{name} is ENABLED in Shift.unity. Defining DESK42_FMOD would " +
                        $"make it live during the scored proof run.");
            }
        }

        [Test]
        public void DistortionAudioDirector_IsNotAttached()
        {
            string scene = File.ReadAllText(ScenePath);
            Assert.IsFalse(scene.Contains(DistortionAudioDirectorGuid),
                "DistortionAudioDirector must stay compiled-but-inactive: it owns " +
                "Mercy and Flow, which remain unwired for the proof candidate.");
        }

        [Test]
        public void MercyAndFlow_HaveNoGameplayCallers()
        {
            // Locked: no gameplay caller may be created to make them reachable.
            foreach (string file in Directory.GetFiles(
                         "Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string norm = file.Replace('\\', '/');
                if (norm.EndsWith("Audio/DistortionAudioDirector.cs")) continue;

                string src = File.ReadAllText(file);
                Assert.IsFalse(src.Contains("EnterMercyWindow") || src.Contains("ExitMercyWindow"),
                    $"Mercy Window gained a caller: {norm}");
                Assert.IsFalse(src.Contains("EnterFlow(") || src.Contains("ExitFlow("),
                    $"Flow gained a caller: {norm}");
            }
        }

        // ── Shift 5 suppression, at the caller ───────────────

        [Test]
        public void Shift5ProofPath_NeverNamesTheCausalIdentity()
        {
            // Caller-level contract. The AudioService guard is defence in depth;
            // a wrong Shift 5 caller must fail HERE rather than be silently
            // masked by the service refusing the request at runtime.
            string[] shift5PathFiles =
            {
                "Assets/_Project/Scripts/Narrative/EliasShift5Policy.cs",
                "Assets/_Project/Scripts/Narrative/EliasProofScheduler.cs",
                "Assets/_Project/Scripts/Narrative/EliasAftermathPolicy.cs",
                "Assets/_Project/Scripts/Encounter/EncounterManager.cs",
                "Assets/_Project/Scripts/Encounter/EncounterCommitService.cs",
            };

            foreach (string file in shift5PathFiles)
            {
                if (!File.Exists(file)) continue;

                Assert.IsFalse(
                    File.ReadAllText(file).Contains(
                        nameof(AudioEventId.EliasRegistrationCausal)),
                    $"{file} names the Shift 2 causal identity. The scored return " +
                    $"must not acoustically identify the earlier cause.");
            }
        }

        [Test]
        public void OnlyPermittedIdentity_ForShift5IsTheGenericReturn()
        {
            Assert.IsTrue(ProofAudioCatalog.IsPermittedOnShift5(
                AudioEventId.Shift5EliasReturn));
            Assert.IsFalse(ProofAudioCatalog.IsPermittedOnShift5(
                AudioEventId.EliasRegistrationCausal));
        }

        [Test]
        public void ServiceGuard_RemainsAsDefenceInDepth()
        {
            // Belt and braces: even if a caller regressed, the boundary refuses.
            AudioService.ResetToNull();
            Assert.AreEqual(AudioRequestResult.Suppressed,
                AudioService.PlayOneShot(
                    AudioEventId.EliasRegistrationCausal, new AudioRequestContext(5)));
        }

        // ── One gameplay-facing audio boundary ───────────────

        [Test]
        public void NoGameplayFile_CallsRuntimeManagerDirectly()
        {
            foreach (string file in Directory.GetFiles(
                         "Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string norm = file.Replace('\\', '/');
                if (norm.Contains("/Scripts/Audio/")) continue;   // the audio layer may

                Assert.IsFalse(File.ReadAllText(file).Contains("FMODUnity.RuntimeManager"),
                    $"Gameplay file calls RuntimeManager directly: {norm}");
            }
        }

        /// <summary>
        /// HARD INVARIANT. Gameplay must not know FMODManager exists; the
        /// gameplay-facing boundary is AudioService. The previous temporary
        /// exception for PneumaticTube is gone — it now routes through the
        /// boundary, so there are zero gameplay-facing call sites.
        ///
        /// FMODManager may remain an internal helper for the eventual
        /// FmodAudioBackend inside the Audio layer.
        /// </summary>
        [Test]
        public void NoGameplayFile_CallsFmodManagerDirectly()
        {
            var offenders = new List<string>();

            foreach (string file in Directory.GetFiles(
                         "Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string norm = file.Replace('\\', '/');
                if (norm.Contains("/Scripts/Audio/")) continue;   // backend-internal

                if (File.ReadAllText(file).Contains("FMODManager."))
                    offenders.Add(norm);
            }

            Assert.IsEmpty(offenders,
                "Gameplay must route audio through AudioService: "
                + string.Join(", ", offenders));
        }

        // ── PneumaticTube migration ──────────────────────────

        [Test]
        public void PneumaticTube_RequestsItsIdentityThroughAudioService()
        {
            string src = File.ReadAllText("Assets/_Project/Scripts/UI/PneumaticTube.cs");

            Assert.IsTrue(src.Contains("AudioService.PlayOneShot"),
                "PneumaticTube must request audio through the boundary.");
            Assert.IsTrue(src.Contains(nameof(AudioEventId.PneumaticTubeThreat)),
                "PneumaticTube must request its own logical identity.");
            Assert.IsFalse(src.Contains("\"event:/"),
                "No FMOD path string may appear in gameplay code.");
        }

        [Test]
        public void PneumaticTubeIdentity_MapsToTheIntendedPath()
            => Assert.AreEqual("event:/Threat/PneumaticTube",
                ProofAudioCatalog.TryGetPath(AudioEventId.PneumaticTubeThreat));

        [Test]
        public void PneumaticTube_PreservesWorldPosition()
        {
            string src = File.ReadAllText("Assets/_Project/Scripts/UI/PneumaticTube.cs");
            Assert.IsTrue(src.Contains("transform.position"),
                "Spatial intent must survive the migration, not be discarded.");

            var ctx = AudioRequestContext.AtPosition(3, new UnityEngine.Vector3(1f, 2f, 3f));
            Assert.IsTrue(ctx.WorldPosition.HasValue);
            Assert.AreEqual(new UnityEngine.Vector3(1f, 2f, 3f), ctx.WorldPosition.Value);
        }

        [Test]
        public void PneumaticTube_CannotRequestAnEliasCausalIdentity()
        {
            string src = File.ReadAllText("Assets/_Project/Scripts/UI/PneumaticTube.cs");

            Assert.IsFalse(src.Contains(nameof(AudioEventId.EliasRegistrationCausal)));
            Assert.IsFalse(src.Contains(nameof(AudioEventId.Shift5EliasReturn)));
            Assert.IsFalse(ProofAudioCatalog.IsCausalIdentity(
                AudioEventId.PneumaticTubeThreat),
                "The tube identity must never be treated as a proof identity.");
        }

        [Test]
        public void PneumaticTubeIdentity_IsNotAFallbackForAnyProofEvent()
        {
            // Distinct identity, distinct path — it cannot stand in for a proof
            // event, and no proof event resolves to its path.
            string tubePath = ProofAudioCatalog.TryGetPath(AudioEventId.PneumaticTubeThreat);

            foreach (var id in ProofAudioCatalog.All)
            {
                if (id == AudioEventId.PneumaticTubeThreat) continue;
                Assert.AreNotEqual(tubePath, ProofAudioCatalog.TryGetPath(id),
                    $"{id} collides with the pneumatic tube path.");
            }
        }
    }
}
