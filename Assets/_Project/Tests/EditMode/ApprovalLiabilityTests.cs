using Desk42.Core;
using Desk42.Encounter;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Bucket C Δ2 — approval liability. Creation, exact-once, persistence,
    /// query seam and orphan safety.
    /// </summary>
    public sealed class ApprovalLiabilityTests
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

        /// <summary>Presents and completes an encounter without a live run.</summary>
        private static string Commit(MetaProgressData meta, RunData run,
            ActiveClaimData claim, ClaimResolutionKind kind)
        {
            EncounterCommitService.BeginEncounter(claim, run, meta);
            meta.Encounters.MarkCompleted(claim.EncounterId, kind, 1L);

            if (ApprovalLiabilityPolicy.IsLiabilityCreating(kind))
                meta.ApprovalLiabilities.Create(claim.EncounterId, claim.ClientVariantId);

            return claim.EncounterId;
        }

        // ── Creation and eligible dispositions ───────────────

        [Test]
        public void Approve_CreatesOneLiability_AnchoredToItsSourceEncounter()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L1", ShiftNumber = 1 };
            var claim = Claim("CLM-1", "elias_venn");

            string encId = Commit(meta, run, claim, ClaimResolutionKind.Approve);

            Assert.IsTrue(ApprovalLiabilityPolicy.HasApprovalLiability(meta, encId));
            var record = ApprovalLiabilityPolicy.TryGet(meta, encId);

            Assert.AreEqual(encId, record.SourceEncounterId);
            Assert.AreEqual("elias_venn", record.SourceClientVariantId);
            Assert.IsFalse(record.Resolved);
            Assert.IsNotNull(meta.Encounters.Find(encId), "Source history must exist.");
        }

        [Test]
        public void Approve_LiabilitySurvivesSaveLoad()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L2", ShiftNumber = 1 };
            string encId = Commit(meta, run, Claim("CLM-2", "elias_venn"),
                ClaimResolutionKind.Approve);

            var reloaded = RoundTrip(meta);

            Assert.IsTrue(ApprovalLiabilityPolicy.HasApprovalLiability(reloaded, encId));
            Assert.AreEqual(1, ApprovalLiabilityPolicy.ActiveLiabilities(reloaded).Count);
        }

        [Test]
        public void Deny_CreatesNoLiability()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L3", ShiftNumber = 1 };
            string encId = Commit(meta, run, Claim("CLM-3", "v1"), ClaimResolutionKind.Deny);

            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(meta, encId));
            Assert.AreEqual(0, meta.ApprovalLiabilities.Count);
        }

        [Test]
        public void Liquify_CreatesNoLiability()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L4", ShiftNumber = 1 };
            string encId = Commit(meta, run, Claim("CLM-4", "v1"),
                ClaimResolutionKind.Liquify);

            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(meta, encId),
                "Liquify must not be reinterpreted as approval.");
            Assert.AreEqual(0, meta.ApprovalLiabilities.Count);
        }

        [Test]
        public void InterruptedEncounter_CreatesNoLiability()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L5", ShiftNumber = 1 };
            var claim = Claim("CLM-5", "v1");

            EncounterCommitService.BeginEncounter(claim, run, meta);   // never completed

            Assert.IsFalse(
                ApprovalLiabilityPolicy.HasApprovalLiability(meta, claim.EncounterId));
            Assert.AreEqual(0, meta.ApprovalLiabilities.Count);
        }

        // ── Exact-once ───────────────────────────────────────

        [Test]
        public void DuplicateCommit_CreatesOnlyOneLiability()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L6", ShiftNumber = 1 };
            var claim = Claim("CLM-6", "elias_venn");

            string encId = Commit(meta, run, claim, ClaimResolutionKind.Approve);
            meta.ApprovalLiabilities.Create(encId, claim.ClientVariantId);
            meta.ApprovalLiabilities.Create(encId, claim.ClientVariantId);

            Assert.AreEqual(1, meta.ApprovalLiabilities.Count);
        }

        [Test]
        public void ReloadThenRetry_DoesNotDuplicateLiability()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L7", ShiftNumber = 1 };
            var claim = Claim("CLM-7", "elias_venn");
            string encId = Commit(meta, run, claim, ClaimResolutionKind.Approve);

            var reloaded = RoundTrip(meta);
            reloaded.ApprovalLiabilities.Create(encId, claim.ClientVariantId);

            Assert.AreEqual(1, reloaded.ApprovalLiabilities.Count,
                "Exact-once must be enforced by persisted state, not a runtime flag.");
        }

        [Test]
        public void TwoDistinctApprovals_CreateTwoLiabilities_EvenForSameClaimant()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L8", ShiftNumber = 1 };

            string a = Commit(meta, run, Claim("CLM-8A", "elias_venn"),
                ClaimResolutionKind.Approve);
            string b = Commit(meta, run, Claim("CLM-8B", "elias_venn"),
                ClaimResolutionKind.Approve);

            Assert.AreNotEqual(a, b);
            Assert.AreEqual(2, meta.ApprovalLiabilities.Count,
                "Liability is keyed by encounter, never collapsed by claimant.");
            Assert.AreEqual(2, ApprovalLiabilityPolicy.ForClaimant(meta, "elias_venn").Count);
        }

        // ── Procedural claimant limitation ───────────────────

        [Test]
        public void ProceduralClaimant_LiabilityIsValid_DespiteUnstableIdentity()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L9", ShiftNumber = 1 };

            // CΔ1 proved procedural ids are freshly generated per claim.
            string first = Commit(meta, run, Claim("CLM-9A", "moth_accountant_123"),
                ClaimResolutionKind.Approve);
            Commit(meta, run, Claim("CLM-9B", "moth_accountant_456"),
                ClaimResolutionKind.Approve);

            // Attribution survives regardless of whether the later claimant is
            // recognisably the same person.
            Assert.IsTrue(ApprovalLiabilityPolicy.HasApprovalLiability(meta, first));
            Assert.AreEqual(2, ApprovalLiabilityPolicy.ActiveLiabilities(meta).Count);
            Assert.AreEqual(1,
                ApprovalLiabilityPolicy.ForClaimant(meta, "moth_accountant_123").Count);
        }

        // ── Query safety and orphans ─────────────────────────

        [Test]
        public void UnknownSource_ReturnsSafeEmptyResult()
        {
            var meta = new MetaProgressData();

            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(meta, "ENC-NOPE"));
            Assert.IsNull(ApprovalLiabilityPolicy.TryGet(meta, "ENC-NOPE"));
            Assert.IsNull(ApprovalLiabilityPolicy.TryGet(meta, null));
            Assert.IsEmpty(ApprovalLiabilityPolicy.ActiveLiabilities(meta));
            Assert.IsEmpty(ApprovalLiabilityPolicy.ForClaimant(meta, "nobody"));
        }

        [Test]
        public void OrphanLiability_WithNoSourceEncounter_IsIgnoredNotThrown()
        {
            var meta = new MetaProgressData();
            meta.ApprovalLiabilities.Create("ENC-GHOST", "v1");

            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(meta, "ENC-GHOST"),
                "A liability with no source encounter is not a valid liability.");
            Assert.IsEmpty(ApprovalLiabilityPolicy.ActiveLiabilities(meta));
            Assert.DoesNotThrow(() => RoundTrip(meta), "Malformed data must still load.");
        }

        [Test]
        public void LiabilityPointingAtADeny_IsNotTreatedAsValid()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L10", ShiftNumber = 1 };
            var claim = Claim("CLM-10", "v1");

            string encId = Commit(meta, run, claim, ClaimResolutionKind.Deny);
            meta.ApprovalLiabilities.Create(encId, claim.ClientVariantId);  // malformed

            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(meta, encId));
            Assert.IsEmpty(ApprovalLiabilityPolicy.ActiveLiabilities(meta));
        }

        [Test]
        public void LiabilityPointingAtAnIncompleteEncounter_IsNotValid()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L11", ShiftNumber = 1 };
            var claim = Claim("CLM-11", "v1");

            EncounterCommitService.BeginEncounter(claim, run, meta);        // presented only
            meta.ApprovalLiabilities.Create(claim.EncounterId, claim.ClientVariantId);

            Assert.IsFalse(
                ApprovalLiabilityPolicy.HasApprovalLiability(meta, claim.EncounterId));
        }

        [Test]
        public void Attribution_SurvivesADisplayNameChange()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L12", ShiftNumber = 1 };
            var claim = Claim("CLM-12", "elias_venn");
            claim.ClaimantName = "Elias Venn";

            string encId = Commit(meta, run, claim, ClaimResolutionKind.Approve);
            claim.ClaimantName = "SOMETHING ELSE ENTIRELY";

            Assert.IsTrue(ApprovalLiabilityPolicy.HasApprovalLiability(meta, encId),
                "Attribution is anchored by EncounterId, never display prose.");
        }

        [Test]
        public void CreateWithoutASourceEncounterId_Throws()
        {
            var meta = new MetaProgressData();
            Assert.Throws<System.ArgumentException>(
                () => meta.ApprovalLiabilities.Create(null, "v1"));
            Assert.Throws<System.ArgumentException>(
                () => meta.ApprovalLiabilities.Create("  ", "v1"));
        }

        // ── Legacy saves ─────────────────────────────────────

        [Test]
        public void LegacyMeta_LoadsWithEmptyLedger_AndNoBackfill()
        {
            // A save written before Δ2 has no liability field, and historical
            // approvals must NOT retroactively become liabilities.
            const string legacy =
                "{\"GlobalShiftNumber\":4,\"TutorialCompleted\":true}";

            var meta = JsonConvert.DeserializeObject<MetaProgressData>(legacy, Settings);

            Assert.IsNotNull(meta.ApprovalLiabilities);
            Assert.AreEqual(0, meta.ApprovalLiabilities.Count);
            Assert.IsEmpty(ApprovalLiabilityPolicy.ActiveLiabilities(meta));
        }

        [Test]
        public void HistoricalApprovals_AreNotBackfilled()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "L13", ShiftNumber = 1 };
            var claim = Claim("CLM-13", "v1");

            // History records an approval, but no liability was created.
            EncounterCommitService.BeginEncounter(claim, run, meta);
            meta.Encounters.MarkCompleted(claim.EncounterId,
                ClaimResolutionKind.Approve, 1L);

            Assert.IsFalse(
                ApprovalLiabilityPolicy.HasApprovalLiability(meta, claim.EncounterId),
                "Liability is authoritative at creation time; history alone " +
                "must not synthesise it.");
        }

        // ── Mara independence ────────────────────────────────

        [Test]
        public void MaraApproval_CreatesLiability_WithNoEliasProofState()
        {
            var meta = new MetaProgressData();          // no proof session at all
            var run  = new RunData { SeedCode = "L14", ShiftNumber = 3 };
            var claim = ControlClaimantContent.BuildClaim();

            string encId = Commit(meta, run, claim, ClaimResolutionKind.Approve);

            Assert.IsTrue(ApprovalLiabilityPolicy.HasApprovalLiability(meta, encId));
            Assert.AreEqual(ControlClaimantContent.StableClaimantId,
                ApprovalLiabilityPolicy.TryGet(meta, encId).SourceClientVariantId);
            Assert.IsFalse(meta.EliasProof.IsActive,
                "General liability must not create or require Elias proof state.");
        }
    }
}
