using Desk42.Core;
using Desk42.Encounter;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Bucket C Δ3A — interrupted / carried-forward encounter lifecycle.
    ///
    /// The locked rule throughout: a carried encounter is THE SAME encounter.
    /// Its EncounterId and claimant provenance survive interruption, save,
    /// restart, requeue, repeated interruption and terminal resolution.
    /// </summary>
    public sealed class CarriedEncounterTests
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting            = Formatting.Indented,
            NullValueHandling     = NullValueHandling.Include,
            DefaultValueHandling  = DefaultValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Error,
            TypeNameHandling      = TypeNameHandling.None,
        };

        private static MetaProgressData RoundTrip(MetaProgressData meta)
            => JsonConvert.DeserializeObject<MetaProgressData>(
                JsonConvert.SerializeObject(meta, Settings), Settings);

        private static ActiveClaimData Claim(string claimId, string variant)
            => new()
            {
                ClaimId = claimId,
                ClientVariantId = variant,
                ClientSpeciesId = "moth_accountant",
            };

        private static (MetaProgressData meta, RunData run, ActiveClaimData claim)
            PresentedEncounter(string seed, string variant = "moth_accountant_314")
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = seed, ShiftNumber = 1 };
            var claim = Claim("CLM-X", variant);
            run.ActiveClaim = claim;
            EncounterCommitService.BeginEncounter(claim, run, meta);
            return (meta, run, claim);
        }

        // ── Basic interruption ───────────────────────────────

        [Test]
        public void Interruption_PreservesIdentity_AndCommitsNothing()
        {
            var (meta, run, claim) = PresentedEncounter("I1");
            string encId = claim.EncounterId;

            Assert.IsTrue(EncounterCommitService.InterruptEncounter(claim, run, meta));

            Assert.IsTrue(meta.CarriedEncounters.Has(encId));
            Assert.AreEqual(encId, meta.CarriedEncounters.Find(encId).EncounterId);

            // Not a disposition.
            Assert.AreEqual(EncounterStatus.Interrupted,
                meta.Encounters.StatusOf(encId, activeEncounterId: null));
            Assert.IsEmpty(meta.Encounters.CommittedDispositionsFor(claim.ClientVariantId));
            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(meta, encId));
            Assert.AreEqual(0, meta.GetTotalVisits(claim.ClientVariantId));
        }

        [Test]
        public void ResolvedEncounter_IsNeverCarried()
        {
            var (meta, run, claim) = PresentedEncounter("I2");
            claim.IsResolved = true;

            Assert.IsFalse(EncounterCommitService.InterruptEncounter(claim, run, meta));
            Assert.AreEqual(0, meta.CarriedEncounters.Count);
        }

        [Test]
        public void CompletedEncounter_CannotBeResurrectedAsCarried()
        {
            var (meta, run, claim) = PresentedEncounter("I3");
            meta.Encounters.MarkCompleted(claim.EncounterId,
                ClaimResolutionKind.Approve, 1L);

            Assert.IsFalse(EncounterCommitService.InterruptEncounter(claim, run, meta));
            Assert.AreEqual(0, meta.CarriedEncounters.Count);
        }

        // ── Save / restart ───────────────────────────────────

        [Test]
        public void CarriedEncounter_SurvivesSaveLoad_WithIdentityAndProvenance()
        {
            var (meta, run, claim) = PresentedEncounter("I4", "gel_anomaly_777");
            string encId = claim.EncounterId;
            EncounterCommitService.InterruptEncounter(claim, run, meta);

            var reloaded = RoundTrip(meta);
            var carried  = reloaded.CarriedEncounters.Find(encId);

            Assert.IsNotNull(carried, "Carried work must survive a restart.");
            Assert.AreEqual(encId, carried.EncounterId);
            Assert.AreEqual("gel_anomaly_777", carried.ClientVariantId,
                "Procedural provenance is preserved, never regenerated.");
            Assert.IsNotNull(carried.Claim, "The claim must be reconstructable.");
            Assert.AreEqual("CLM-X", carried.Claim.ClaimId);
        }

        // ── Repeated interruption ────────────────────────────

        [Test]
        public void RepeatedInterruption_KeepsOneRecordAndOneIdentity()
        {
            var (meta, run, claim) = PresentedEncounter("I5");
            string encId = claim.EncounterId;

            EncounterCommitService.InterruptEncounter(claim, run, meta);
            EncounterCommitService.InterruptEncounter(claim, run, meta);
            EncounterCommitService.InterruptEncounter(claim, run, meta);

            Assert.AreEqual(1, meta.CarriedEncounters.Count,
                "Interruption is idempotent by EncounterId.");
            Assert.AreEqual(3, meta.CarriedEncounters.Find(encId).InterruptCount);
            Assert.AreEqual(encId, claim.EncounterId, "Identity never changes.");
            Assert.AreEqual(1, meta.Encounters.TotalPresentations(claim.ClientVariantId),
                "Re-presentation must not add history identities.");
        }

        // ── Terminal resolution releases the carry ───────────

        [Test]
        public void TerminalCompletion_ReleasesCarriedWork()
        {
            var (meta, run, claim) = PresentedEncounter("I6");
            string encId = claim.EncounterId;
            EncounterCommitService.InterruptEncounter(claim, run, meta);
            Assert.IsTrue(meta.CarriedEncounters.Has(encId));

            meta.Encounters.MarkCompleted(encId, ClaimResolutionKind.Approve, 1L);
            meta.CarriedEncounters.Release(encId);

            Assert.IsFalse(meta.CarriedEncounters.Has(encId),
                "A resolved encounter must not return as outstanding work.");
            Assert.AreEqual(1, meta.Encounters.TotalVisits(claim.ClientVariantId));
            Assert.AreEqual(1,
                meta.Encounters.CommittedDispositionsFor(claim.ClientVariantId).Count);
        }

        [Test]
        public void ReleaseIsSafeForUnknownEncounters()
        {
            var meta = new MetaProgressData();
            Assert.IsFalse(meta.CarriedEncounters.Release("ENC-NOPE"));
            Assert.DoesNotThrow(() => meta.CarriedEncounters.Release(null));
        }

        // ── Historical disposition interaction (CΔ1 frozen) ──

        [Test]
        public void InterruptedEncounter_IsExcludedUntilFinalResolution()
        {
            var (meta, run, claim) = PresentedEncounter("I7", "elias_venn");
            EncounterCommitService.InterruptEncounter(claim, run, meta);

            Assert.IsEmpty(meta.Encounters.CommittedDispositionsFor("elias_venn"),
                "Interrupted work is not a final disposition.");

            meta.Encounters.MarkCompleted(claim.EncounterId,
                ClaimResolutionKind.Deny, 1L);

            var history = meta.Encounters.CommittedDispositionsFor("elias_venn");
            Assert.AreEqual(1, history.Count, "Exactly once after resolution.");
            Assert.AreEqual(ClaimResolutionKind.Deny, history[0].Outcome);
        }

        // ── Identity is per-encounter, not per-claimant ──────

        [Test]
        public void OnlyTheInterruptedEncounter_IsCarried_NotTheClaimant()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "I8", ShiftNumber = 1 };

            var carriedClaim = Claim("CLM-A", "same_claimant");
            var otherClaim   = Claim("CLM-B", "same_claimant");
            EncounterCommitService.BeginEncounter(carriedClaim, run, meta);
            EncounterCommitService.BeginEncounter(otherClaim, run, meta);

            run.ActiveClaim = carriedClaim;
            EncounterCommitService.InterruptEncounter(carriedClaim, run, meta);

            Assert.AreEqual(1, meta.CarriedEncounters.Count);
            Assert.IsTrue(meta.CarriedEncounters.Has(carriedClaim.EncounterId));
            Assert.IsFalse(meta.CarriedEncounters.Has(otherClaim.EncounterId),
                "A different encounter for the same claimant is not carried.");
        }

        // ── Malformed persisted state ────────────────────────

        [Test]
        public void MalformedDuplicates_ExposeOneLogicalCarriedEncounter()
        {
            var (meta, run, claim) = PresentedEncounter("I9");
            EncounterCommitService.InterruptEncounter(claim, run, meta);

            var field = typeof(CarriedEncounterLedger).GetField("_records",
                System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);
            var list = (System.Collections.Generic.List<CarriedEncounterRecord>)
                field.GetValue(meta.CarriedEncounters);
            list.Add(new CarriedEncounterRecord
            {
                EncounterId = claim.EncounterId,
                ClientVariantId = "someone-else",
                Claim = Claim("CLM-DUP", "someone-else"),
                InterruptCount = 1,
            });

            Assert.AreEqual(2, meta.CarriedEncounters.Count, "Raw rows remain.");
            Assert.AreEqual(1, meta.CarriedEncounters.Canonical().Count,
                "One logical carried encounter per EncounterId.");
            Assert.AreEqual(claim.ClientVariantId,
                meta.CarriedEncounters.Canonical()[0].ClientVariantId,
                "First occurrence is canonical.");
        }

        [Test]
        public void MalformedRecord_WithNoClaim_IsIgnoredNotCrashed()
        {
            var meta = new MetaProgressData();
            var field = typeof(CarriedEncounterLedger).GetField("_records",
                System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);
            var list = (System.Collections.Generic.List<CarriedEncounterRecord>)
                field.GetValue(meta.CarriedEncounters);
            list.Add(new CarriedEncounterRecord { EncounterId = "ENC-BROKEN", Claim = null });

            Assert.IsEmpty(meta.CarriedEncounters.Canonical(),
                "An unreconstructable record must be ignored, not returned.");
            Assert.DoesNotThrow(() => RoundTrip(meta), "Loading must not crash.");
        }

        [Test]
        public void CarryRequiresAnEncounterId()
        {
            var meta = new MetaProgressData();
            Assert.Throws<System.ArgumentException>(
                () => meta.CarriedEncounters.Carry(Claim("CLM-1", "v"), 1));
            Assert.Throws<System.ArgumentException>(
                () => meta.CarriedEncounters.Carry(null, 1));
        }

        // ── Legacy saves ─────────────────────────────────────

        [Test]
        public void LegacyMeta_LoadsWithEmptyLedger_AndSynthesisesNothing()
        {
            const string legacy = "{\"GlobalShiftNumber\":5,\"TutorialCompleted\":true}";
            var meta = JsonConvert.DeserializeObject<MetaProgressData>(legacy, Settings);

            Assert.IsNotNull(meta.CarriedEncounters);
            Assert.AreEqual(0, meta.CarriedEncounters.Count);
            Assert.IsEmpty(meta.CarriedEncounters.Canonical());
        }
    }
}
