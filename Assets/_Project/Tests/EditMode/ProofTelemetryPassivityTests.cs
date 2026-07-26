using System.Reflection;
using Desk42.Core;
using Desk42.Debugging;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// The telemetry layer must be a pure observer. These tests assert that
    /// structurally, not by convention: a future edit that gives it a mutating
    /// call fails here rather than silently corrupting a proof run.
    /// </summary>
    public sealed class ProofTelemetryPassivityTests
    {
        // ── Fingerprinting does not mutate ───────────────────

        [Test]
        public void Fingerprinting_DoesNotMutateProofState()
        {
            var state = EliasProofSessionState.Create("passivity");
            state.Shift2Branch = EliasShift2Branch.NormalisedAddress;
            state.RecordedAppearanceKeys.Add(EliasProofContent.Shift2AppearanceKey);

            string before = ProofVerificationTelemetry.FingerprintProofState(state);
            for (int i = 0; i < 5; i++)
                ProofVerificationTelemetry.FingerprintProofState(state);
            string after = ProofVerificationTelemetry.FingerprintProofState(state);

            Assert.AreEqual(before, after, "Observation must be repeatable.");
            Assert.AreEqual(EliasShift2Branch.NormalisedAddress, state.Shift2Branch);
            Assert.AreEqual(1, state.RecordedAppearanceKeys.Count);
            Assert.IsTrue(state.IsActive);
        }

        [Test]
        public void Fingerprint_ChangesOnlyWhenStateActuallyChanges()
        {
            var state = EliasProofSessionState.Create("delta");
            string a = ProofVerificationTelemetry.FingerprintProofState(state);

            state.Shift2Branch = EliasShift2Branch.LegacyException;
            string b = ProofVerificationTelemetry.FingerprintProofState(state);

            Assert.AreNotEqual(a, b,
                "A before/after pair must be able to detect real interference.");
        }

        [Test]
        public void Fingerprint_HandlesNullWithoutThrowing()
            => Assert.AreEqual("null",
                ProofVerificationTelemetry.FingerprintProofState(null));

        // ── Structural passivity ─────────────────────────────

        [Test]
        public void Telemetry_ExposesNoPublicMutator()
        {
            var methods = typeof(ProofVerificationTelemetry).GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.DeclaredOnly);

            foreach (var m in methods)
            {
                Assert.IsFalse(
                    m.Name.StartsWith("Set") || m.Name.StartsWith("Apply")
                    || m.Name.StartsWith("Modify") || m.Name.StartsWith("Commit")
                    || m.Name.StartsWith("Record") || m.Name.StartsWith("Advance"),
                    $"Telemetry exposed a mutating-looking API: {m.Name}");
            }
        }

        [Test]
        public void Telemetry_DoesNotReferenceForbiddenMutations()
        {
            // Source-level guard: the observer must never call into the
            // authoritative write paths or touch time scaling.
            string src = System.IO.File.ReadAllText(
                "Assets/_Project/Scripts/Debug/ProofVerificationTelemetry.cs");

            // Strip the header comment block, which names these deliberately.
            int bodyStart = src.IndexOf("using System;", System.StringComparison.Ordinal);
            string body = bodyStart > 0 ? src.Substring(bodyStart) : src;

            foreach (string forbidden in new[]
                     {
                         "CommitEncounterResult(", "ApplyClaimResolution(",
                         "ModifySanity(", "ModifySoulIntegrity(", "AddCredits(",
                         "BeginProofSession(", "EndProofSession(",
                         "RecordAppearance(", "RecordDisposition(",
                         "TryApplyProcedure(", "MarkCompleted(",
                         "BeginPresentation(", "SaveRun(", "SaveMeta(",
                         "Time.timeScale", "AdvancePhase(",
                     })
            {
                Assert.IsFalse(body.Contains(forbidden),
                    $"Telemetry must not call '{forbidden}' — it is a passive observer.");
            }
        }

        [Test]
        public void Telemetry_OnlyWritesToItsOwnEvidenceFolder()
        {
            Assert.AreEqual("ProofEvidence",
                ProofVerificationTelemetry.EvidenceFolderName);
            Assert.IsTrue(
                ProofVerificationTelemetry.EvidenceDirectory.EndsWith("ProofEvidence"),
                "Evidence must not be written beside gameplay saves or PlaytestLogs.");
        }

        [Test]
        public void Telemetry_RequiresNoProductionCallSites()
        {
            // The layer subscribes to the existing bus. If a production file
            // ever has to call it, passivity is no longer structural.
            string[] files = System.IO.Directory.GetFiles(
                "Assets/_Project/Scripts", "*.cs", System.IO.SearchOption.AllDirectories);

            foreach (string f in files)
            {
                if (f.Replace('\\', '/').EndsWith("Debug/ProofVerificationTelemetry.cs"))
                    continue;

                Assert.IsFalse(
                    System.IO.File.ReadAllText(f).Contains("ProofVerificationTelemetry"),
                    $"Production file references the telemetry layer: {f}");
            }
        }
    }
}
