using System.Collections.Generic;
using System.Reflection;
using Desk42.Core;
using Desk42.Encounter;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// CΔ2 malformed-duplicate repair.
    ///
    /// Normal production creation cannot produce two rows for one source
    /// encounter. Malformed or hand-edited persisted state can. Identity is
    /// the source encounter, so those rows are ONE logical liability and the
    /// query seams must say so — without deleting anything on load.
    ///
    /// Collection-level cases run in memory; the transaction and disk claim is
    /// proved separately through the real commit service and SaveSystem.
    /// </summary>
    public sealed class ApprovalLiabilityDuplicateTests
    {
        private static ActiveClaimData Claim(string claimId, string variant)
            => new()
            {
                ClaimId = claimId,
                ClientVariantId = variant,
                ClientSpeciesId = "moth_accountant",
            };

        /// <summary>
        /// Injects a malformed duplicate row directly into the persisted list,
        /// bypassing Create — which is exactly what a hand-edited save does.
        /// </summary>
        private static void InjectDuplicate(
            MetaProgressData meta, string sourceEncounterId,
            string clientVariantId, bool resolved)
        {
            var field = typeof(ApprovalLiabilityLedger).GetField(
                "_records", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = (List<ApprovalLiabilityRecord>)field.GetValue(meta.ApprovalLiabilities);

            list.Add(new ApprovalLiabilityRecord
            {
                SourceEncounterId     = sourceEncounterId,
                SourceClientVariantId = clientVariantId,
                Resolved              = resolved,
            });
        }

        private static string ApproveInMemory(
            MetaProgressData meta, RunData run, ActiveClaimData claim)
        {
            EncounterCommitService.BeginEncounter(claim, run, meta);
            meta.Encounters.MarkCompleted(claim.EncounterId,
                ClaimResolutionKind.Approve, 1L);
            meta.ApprovalLiabilities.Create(claim.EncounterId, claim.ClientVariantId);
            return claim.EncounterId;
        }

        // ── 1. Two unresolved rows for one source ────────────

        [Test]
        public void DuplicateActiveRows_CollapseToOneLogicalLiability()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "D1", ShiftNumber = 1 };
            string encId = ApproveInMemory(meta, run, Claim("CLM-1", "claimant-a"));

            InjectDuplicate(meta, encId, "claimant-a", resolved: false);

            Assert.AreEqual(2, meta.ApprovalLiabilities.Count,
                "Physical duplicates may remain — loading stays non-destructive.");
            Assert.AreEqual(1, ApprovalLiabilityPolicy.ActiveLiabilities(meta).Count);
            Assert.AreEqual(1, ApprovalLiabilityPolicy.ForClaimant(meta, "claimant-a").Count);
            Assert.AreSame(meta.ApprovalLiabilities.Records[0],
                ApprovalLiabilityPolicy.TryGet(meta, encId));
        }

        // ── 2. First resolved, second active ─────────────────

        [Test]
        public void LaterDuplicate_CannotResurrectAResolvedLiability()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "D2", ShiftNumber = 1 };
            string encId = ApproveInMemory(meta, run, Claim("CLM-2", "claimant-a"));

            meta.ApprovalLiabilities.Records[0].Resolved = true;      // canonical: resolved
            InjectDuplicate(meta, encId, "claimant-a", resolved: false);

            Assert.IsTrue(ApprovalLiabilityPolicy.TryGet(meta, encId).Resolved,
                "TryGet must return the first/canonical row.");
            Assert.IsEmpty(ApprovalLiabilityPolicy.ActiveLiabilities(meta),
                "A malformed duplicate must not resurrect a resolved liability.");
        }

        // ── 3. First active, second resolved ─────────────────

        [Test]
        public void LaterResolvedDuplicate_DoesNotSuppressOrDuplicateTheActiveOne()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "D3", ShiftNumber = 1 };
            string encId = ApproveInMemory(meta, run, Claim("CLM-3", "claimant-a"));

            InjectDuplicate(meta, encId, "claimant-a", resolved: true);

            var active = ApprovalLiabilityPolicy.ActiveLiabilities(meta);
            Assert.AreEqual(1, active.Count, "Active exactly once.");
            Assert.IsFalse(active[0].Resolved);
        }

        // ── 4. Conflicting claimant provenance ───────────────

        [Test]
        public void ConflictingProvenance_AttributesOnlyToTheCanonicalClaimant()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "D4", ShiftNumber = 1 };
            string encId = ApproveInMemory(meta, run, Claim("CLM-4", "claimant-a"));

            InjectDuplicate(meta, encId, "claimant-b", resolved: false);

            Assert.AreEqual(1, ApprovalLiabilityPolicy.ForClaimant(meta, "claimant-a").Count);
            Assert.IsEmpty(ApprovalLiabilityPolicy.ForClaimant(meta, "claimant-b"),
                "A duplicate row must not attribute this source to a second claimant.");
            Assert.AreEqual("claimant-a",
                ApprovalLiabilityPolicy.TryGet(meta, encId).SourceClientVariantId,
                "Fields are never combined across duplicate rows.");
        }

        // ── 5. Duplicate rows over a malformed source ────────

        [Test]
        public void DuplicateRows_OverANonQualifyingSource_YieldNothing()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "D5", ShiftNumber = 1 };
            var claim = Claim("CLM-5", "claimant-a");

            EncounterCommitService.BeginEncounter(claim, run, meta);
            meta.Encounters.MarkCompleted(claim.EncounterId, ClaimResolutionKind.Deny, 1L);

            InjectDuplicate(meta, claim.EncounterId, "claimant-a", resolved: false);
            InjectDuplicate(meta, claim.EncounterId, "claimant-a", resolved: false);

            Assert.IsFalse(
                ApprovalLiabilityPolicy.HasApprovalLiability(meta, claim.EncounterId));
            Assert.IsEmpty(ApprovalLiabilityPolicy.ActiveLiabilities(meta));
            Assert.IsEmpty(ApprovalLiabilityPolicy.ForClaimant(meta, "claimant-a"),
                "Validation is applied to the canonical row; later duplicates are " +
                "never searched for one that makes a bad source look valid.");
        }

        [Test]
        public void DuplicateRows_OverAMissingSource_YieldNothing()
        {
            var meta = new MetaProgressData();
            InjectDuplicate(meta, "ENC-GHOST", "claimant-a", resolved: false);
            InjectDuplicate(meta, "ENC-GHOST", "claimant-a", resolved: false);

            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(meta, "ENC-GHOST"));
            Assert.IsEmpty(ApprovalLiabilityPolicy.ActiveLiabilities(meta));
        }

        // ── Seams agree with each other ──────────────────────

        [Test]
        public void AllQuerySeams_AgreeUnderDuplicates()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "D6", ShiftNumber = 1 };
            string encId = ApproveInMemory(meta, run, Claim("CLM-6", "claimant-a"));
            InjectDuplicate(meta, encId, "claimant-b", resolved: true);

            Assert.IsTrue(ApprovalLiabilityPolicy.HasApprovalLiability(meta, encId));
            Assert.AreEqual(1, ApprovalLiabilityPolicy.CanonicalRecords(meta).Count);
            Assert.AreEqual(1, ApprovalLiabilityPolicy.ActiveLiabilities(meta).Count);
            Assert.AreEqual(1, ApprovalLiabilityPolicy.ForClaimant(meta, "claimant-a").Count);
            Assert.IsEmpty(ApprovalLiabilityPolicy.ForClaimant(meta, "claimant-b"));
        }

        [Test]
        public void ThreeDuplicates_StillCollapseToOne()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "D7", ShiftNumber = 1 };
            string encId = ApproveInMemory(meta, run, Claim("CLM-7", "claimant-a"));

            for (int i = 0; i < 3; i++)
                InjectDuplicate(meta, encId, "claimant-a", resolved: false);

            Assert.AreEqual(4, meta.ApprovalLiabilities.Count);
            Assert.AreEqual(1, ApprovalLiabilityPolicy.ActiveLiabilities(meta).Count);
        }

        [Test]
        public void DistinctSources_AreNotCollapsed()
        {
            var meta = new MetaProgressData();
            var run  = new RunData { SeedCode = "D8", ShiftNumber = 1 };

            string a = ApproveInMemory(meta, run, Claim("CLM-8A", "claimant-a"));
            string b = ApproveInMemory(meta, run, Claim("CLM-8B", "claimant-a"));

            Assert.AreNotEqual(a, b);
            Assert.AreEqual(2, ApprovalLiabilityPolicy.ActiveLiabilities(meta).Count,
                "Canonicalisation is per source encounter, not per claimant.");
        }
    }
}
