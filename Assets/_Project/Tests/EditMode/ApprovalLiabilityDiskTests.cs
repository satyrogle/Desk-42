using System.Collections.Generic;
using System.Reflection;
using Desk42.Core;
using Desk42.Encounter;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// CΔ2 malformed duplicates through the REAL boundaries: an actual
    /// EncounterCommitService transaction, a real SaveSystem round-trip, and
    /// queries against the reloaded Meta.
    ///
    /// Deliberately separate from the in-memory collection tests — helper-only
    /// coverage cannot prove the transaction or disk claim.
    /// </summary>
    public sealed class ApprovalLiabilityDiskTests : Bucket1PersistenceFixture
    {
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

        [Test]
        public void RealCommit_ThenInjectedDuplicate_ThenReload_YieldsOneLogicalLiability()
        {
            // 1. a legitimate approval through the real transaction
            var claim = Claim("CLM-DISK-1", "claimant-a");
            var data  = RunData(activeClaim: claim);
            var run   = Controller(data);
            Present(claim, data, Meta);

            var commit = Commit(claim, ClaimResolutionKind.Approve, run, Meta);
            Assert.IsTrue(commit.Committed);

            string encId = claim.EncounterId;
            Assert.IsTrue(ApprovalLiabilityPolicy.HasApprovalLiability(Meta, encId),
                "The real commit must create the liability before its save.");

            // 2. malformed duplicate injected into persisted state
            InjectDuplicate(Meta, encId, "claimant-b", resolved: false);

            // 3. real save, 4. real reload
            Assert.IsTrue(SaveSystem.SaveMeta(Meta));
            var reloaded = SaveSystem.LoadMeta();

            // 5. query the reloaded Meta
            Assert.AreEqual(2, reloaded.ApprovalLiabilities.Count,
                "Physical duplicates survive on disk — load must not repair or " +
                "rewrite the file.");

            Assert.AreEqual(1, ApprovalLiabilityPolicy.ActiveLiabilities(reloaded).Count,
                "One source encounter is one logical liability.");
            Assert.AreEqual(1,
                ApprovalLiabilityPolicy.ForClaimant(reloaded, "claimant-a").Count);
            Assert.IsEmpty(ApprovalLiabilityPolicy.ForClaimant(reloaded, "claimant-b"),
                "A duplicate row must not attribute this source to a second claimant.");

            var canonical = ApprovalLiabilityPolicy.TryGet(reloaded, encId);
            Assert.AreEqual("claimant-a", canonical.SourceClientVariantId);
            Assert.IsFalse(canonical.Resolved);
        }

        [Test]
        public void RealCommit_ResolvedCanonical_DuplicateCannotResurrectAcrossDisk()
        {
            var claim = Claim("CLM-DISK-2", "claimant-a");
            var data  = RunData(activeClaim: claim);
            var run   = Controller(data);
            Present(claim, data, Meta);
            Assert.IsTrue(Commit(claim, ClaimResolutionKind.Approve, run, Meta).Committed);

            string encId = claim.EncounterId;
            Meta.ApprovalLiabilities.Find(encId).Resolved = true;
            InjectDuplicate(Meta, encId, "claimant-a", resolved: false);

            Assert.IsTrue(SaveSystem.SaveMeta(Meta));
            var reloaded = SaveSystem.LoadMeta();

            Assert.IsTrue(ApprovalLiabilityPolicy.TryGet(reloaded, encId).Resolved);
            Assert.IsEmpty(ApprovalLiabilityPolicy.ActiveLiabilities(reloaded),
                "A later duplicate must not resurrect a resolved liability after reload.");
        }

        [Test]
        public void RealDenyCommit_WithInjectedDuplicates_YieldsNoLiability()
        {
            var claim = Claim("CLM-DISK-3", "claimant-a");
            var data  = RunData(activeClaim: claim);
            var run   = Controller(data);
            Present(claim, data, Meta);
            Assert.IsTrue(Commit(claim, ClaimResolutionKind.Deny, run, Meta).Committed);

            string encId = claim.EncounterId;
            Assert.AreEqual(0, Meta.ApprovalLiabilities.Count,
                "Deny must not create liability through the real transaction.");

            InjectDuplicate(Meta, encId, "claimant-a", resolved: false);
            InjectDuplicate(Meta, encId, "claimant-a", resolved: false);

            Assert.IsTrue(SaveSystem.SaveMeta(Meta));
            var reloaded = SaveSystem.LoadMeta();

            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(reloaded, encId));
            Assert.IsEmpty(ApprovalLiabilityPolicy.ActiveLiabilities(reloaded));
        }

        [Test]
        public void RealCommit_NoDuplicates_RemainsExactlyOneAcrossDisk()
        {
            // Guards the repair against over-correcting: a normal approval must
            // still produce exactly one liability after a real round-trip.
            var claim = Claim("CLM-DISK-4", "elias_venn");
            var data  = RunData(activeClaim: claim);
            var run   = Controller(data);
            Present(claim, data, Meta);
            Assert.IsTrue(Commit(claim, ClaimResolutionKind.Approve, run, Meta).Committed);

            Assert.IsTrue(SaveSystem.SaveMeta(Meta));
            var reloaded = SaveSystem.LoadMeta();

            Assert.AreEqual(1, reloaded.ApprovalLiabilities.Count);
            Assert.AreEqual(1, ApprovalLiabilityPolicy.ActiveLiabilities(reloaded).Count);
            Assert.AreEqual(1,
                ApprovalLiabilityPolicy.ForClaimant(reloaded, "elias_venn").Count);
        }
    }
}
