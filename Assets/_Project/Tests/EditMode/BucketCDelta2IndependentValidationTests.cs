using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Desk42.Core;
using Desk42.Encounter;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Independent C Delta 2 validation through the authoritative encounter
    /// transaction and real SaveSystem boundary. Helper-only ledger mutation
    /// is reserved for deliberately malformed persisted-state probes.
    /// </summary>
    public sealed class BucketCDelta2IndependentValidationTests
        : Bucket1PersistenceFixture
    {
        private static readonly FieldInfo LiabilityRecordsField =
            typeof(ApprovalLiabilityLedger).GetField(
                "_records", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void ValidateLiabilityReflectionSeam()
        {
            Assert.IsNotNull(LiabilityRecordsField);
        }

        [TestCase(ClaimResolutionKind.Approve, 1)]
        [TestCase(ClaimResolutionKind.Deny, 0)]
        [TestCase(ClaimResolutionKind.Liquify, 0)]
        public void AuthoritativeCommit_CreatesLiabilityOnlyForApprove(
            ClaimResolutionKind disposition,
            int expectedLiabilities)
        {
            ActiveClaimData claim = Claim(
                $"CLM-MATRIX-{disposition}", $"matrix_{disposition}");
            RunData data = RunData(
                $"LIABILITY-{disposition}", 1, claim);
            RunStateController run = Controller(data);

            Present(claim, data, Meta);
            CommitResult commit = Commit(claim, disposition, run, Meta);

            Assert.IsTrue(commit.Committed);
            Assert.AreEqual(expectedLiabilities, Meta.ApprovalLiabilities.Count);

            EncounterRecord source = Meta.Encounters.Find(claim.EncounterId);
            Assert.IsNotNull(source);
            Assert.IsTrue(source.Completed);
            Assert.AreEqual(disposition, source.Outcome);

            MetaProgressData loaded = SaveSystem.LoadMeta();
            Assert.AreEqual(expectedLiabilities,
                loaded.ApprovalLiabilities.Count);
            Assert.AreEqual(
                disposition == ClaimResolutionKind.Approve,
                ApprovalLiabilityPolicy.HasApprovalLiability(
                    loaded, claim.EncounterId));

            if (disposition == ClaimResolutionKind.Approve)
            {
                ApprovalLiabilityRecord liability =
                    ApprovalLiabilityPolicy.TryGet(loaded, claim.EncounterId);
                Assert.AreEqual(claim.EncounterId, liability.SourceEncounterId);
                Assert.AreEqual(claim.ClientVariantId,
                    liability.SourceClientVariantId);
                Assert.IsFalse(liability.Resolved);
            }
        }

        [Test]
        public void PresentedButIncompleteEncounter_NeverCreatesLiability()
        {
            ActiveClaimData claim = Claim("CLM-INTERRUPTED", "interrupted");
            RunData data = RunData("LIABILITY-INTERRUPTED", 1, claim);

            Present(claim, data, Meta);
            Assert.IsTrue(SaveSystem.SaveRun(data));
            Assert.IsTrue(SaveSystem.SaveMeta(Meta));

            MetaProgressData loaded = SaveSystem.LoadMeta();
            Assert.AreEqual(0, loaded.ApprovalLiabilities.Count);
            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(
                loaded, claim.EncounterId));
            Assert.IsFalse(loaded.Encounters.Find(claim.EncounterId).Completed);
        }

        [Test]
        public void DuplicateCommit_ImmediateAndAfterReload_RemainsExactOnce()
        {
            ActiveClaimData claim = Claim("CLM-DUP-LIABILITY", "dup_claimant");
            RunData data = RunData("LIABILITY-DUP", 1, claim);
            RunStateController run = Controller(data);

            Present(claim, data, Meta);
            CommitResult first =
                Commit(claim, ClaimResolutionKind.Approve, run, Meta);
            CommitResult immediate =
                Commit(claim, ClaimResolutionKind.Deny, run, Meta);

            Assert.IsTrue(first.Committed);
            Assert.IsFalse(immediate.Committed);
            Assert.AreEqual(CommitRejection.AlreadyCommitted,
                immediate.Rejection);
            Assert.AreEqual(1, Meta.ApprovalLiabilities.Count);

            MetaProgressData loadedMeta = SaveSystem.LoadMeta();
            RunData loadedData = SaveSystem.LoadRun();
            RunStateController reconstructed = Controller(loadedData);
            CommitResult afterReload = Commit(
                loadedData.ActiveClaim,
                ClaimResolutionKind.Liquify,
                reconstructed,
                loadedMeta);

            Assert.IsFalse(afterReload.Committed);
            Assert.AreEqual(CommitRejection.AlreadyCommitted,
                afterReload.Rejection);
            Assert.AreEqual(1, loadedMeta.ApprovalLiabilities.Count);
            Assert.AreEqual(ClaimResolutionKind.Approve,
                loadedMeta.Encounters.Find(claim.EncounterId).Outcome);

            // Reconstructing another runtime façade is itself side-effect free.
            _ = Controller(loadedData);
            Assert.AreEqual(1, loadedMeta.ApprovalLiabilities.Count);
        }

        [Test]
        public void SameAuthoredClaimant_TwoApprovals_CreateTwoEncounterLiabilities()
        {
            const string claimant = "elias_venn";
            ActiveClaimData first = Claim("CLM-ELIAS-A", claimant);
            ActiveClaimData second = Claim("CLM-ELIAS-B", claimant);
            RunData data = RunData("LIABILITY-AUTHORED", 1, first);
            RunStateController run = Controller(data);

            CommitActual(first, ClaimResolutionKind.Approve, data, run, Meta);
            data.ShiftNumber = 2;
            CommitActual(second, ClaimResolutionKind.Approve, data, run, Meta);

            Assert.AreNotEqual(first.EncounterId, second.EncounterId);
            Assert.AreEqual(2, Meta.ApprovalLiabilities.Count);
            CollectionAssert.AreEquivalent(
                new[] { first.EncounterId, second.EncounterId },
                ApprovalLiabilityPolicy.ForClaimant(Meta, claimant)
                    .Select(r => r.SourceEncounterId));

            MetaProgressData loaded = SaveSystem.LoadMeta();
            Assert.IsNotNull(
                ApprovalLiabilityPolicy.TryGet(loaded, first.EncounterId));
            Assert.IsNotNull(
                ApprovalLiabilityPolicy.TryGet(loaded, second.EncounterId));
        }

        [Test]
        public void ProceduralProvenanceCollision_RemainsDistinctByEncounterId()
        {
            const string collidingProvenance = "moth_accountant_123";
            ActiveClaimData first = Claim(
                "CLM-PROCEDURAL-A", collidingProvenance, "moth_accountant");
            ActiveClaimData second = Claim(
                "CLM-PROCEDURAL-B", collidingProvenance, "moth_accountant");
            RunData data = RunData("LIABILITY-PROCEDURAL", 1, first);
            RunStateController run = Controller(data);

            CommitActual(first, ClaimResolutionKind.Approve, data, run, Meta);
            CommitActual(second, ClaimResolutionKind.Approve, data, run, Meta);

            Assert.AreNotEqual(first.EncounterId, second.EncounterId);
            Assert.AreEqual(2, Meta.ApprovalLiabilities.Count);
            Assert.AreEqual(2,
                ApprovalLiabilityPolicy.ForClaimant(
                    Meta, collidingProvenance).Count);

            MetaProgressData loaded = SaveSystem.LoadMeta();
            Assert.AreEqual(first.EncounterId,
                ApprovalLiabilityPolicy.TryGet(loaded, first.EncounterId)
                    .SourceEncounterId);
            Assert.AreEqual(second.EncounterId,
                ApprovalLiabilityPolicy.TryGet(loaded, second.EncounterId)
                    .SourceEncounterId);
        }

        [Test]
        public void CommitSaveCrashBoundary_HasLiabilityBeforeDeferredFlow()
        {
            ActiveClaimData claim = Claim(
                "CLM-CRASH-BOUNDARY", "crash_boundary");
            RunData data = RunData("LIABILITY-CRASH", 1, claim);
            RunStateController run = Controller(data);

            Present(claim, data, Meta);
            Assert.IsTrue(Commit(
                claim, ClaimResolutionKind.Approve, run, Meta).Committed);

            // Deliberately run no ShiftManager cleanup or presentation callback.
            MetaProgressData loadedMeta = SaveSystem.LoadMeta();
            RunData loadedRun = SaveSystem.LoadRun();

            Assert.AreEqual(1, loadedMeta.ApprovalLiabilities.Count);
            Assert.IsTrue(ApprovalLiabilityPolicy.HasApprovalLiability(
                loadedMeta, claim.EncounterId));
            Assert.IsTrue(loadedRun.ActiveClaim.IsResolved);
            Assert.AreEqual(ClaimResolutionKind.Approve,
                loadedMeta.Encounters.Find(claim.EncounterId).Outcome);
        }

        [Test]
        public void TrustedQueries_RejectEveryMalformedSourceDisposition()
        {
            RunData data = RunData("LIABILITY-MALFORMED", 1);
            RunStateController run = Controller(data);

            Meta.ApprovalLiabilities.Create("ENC-ORPHAN", "malformed");
            InjectDuplicate(
                Meta, "ENC-ORPHAN", "duplicate_provenance", resolved: false);

            ActiveClaimData deny = Claim("CLM-MAL-DENY", "malformed");
            CommitActual(deny, ClaimResolutionKind.Deny, data, run, Meta);
            Meta.ApprovalLiabilities.Create(
                deny.EncounterId, deny.ClientVariantId);
            InjectDuplicate(
                Meta, deny.EncounterId, "duplicate_provenance", resolved: false);

            ActiveClaimData liquify = Claim("CLM-MAL-LIQUIFY", "malformed");
            CommitActual(
                liquify, ClaimResolutionKind.Liquify, data, run, Meta);
            Meta.ApprovalLiabilities.Create(
                liquify.EncounterId, liquify.ClientVariantId);
            InjectDuplicate(
                Meta, liquify.EncounterId, "duplicate_provenance", resolved: false);

            ActiveClaimData interrupted = Claim(
                "CLM-MAL-INCOMPLETE", "malformed");
            data.ActiveClaim = interrupted;
            Present(interrupted, data, Meta);
            Meta.ApprovalLiabilities.Create(
                interrupted.EncounterId, interrupted.ClientVariantId);
            InjectDuplicate(
                Meta,
                interrupted.EncounterId,
                "duplicate_provenance",
                resolved: false);

            ActiveClaimData approved = Claim(
                "CLM-MAL-APPROVE", "malformed");
            CommitActual(
                approved, ClaimResolutionKind.Approve, data, run, Meta);

            foreach (string invalidSource in new[]
                     {
                         "ENC-ORPHAN",
                         deny.EncounterId,
                         liquify.EncounterId,
                         interrupted.EncounterId,
                     })
            {
                Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(
                    Meta, invalidSource), invalidSource);
                Assert.IsNull(ApprovalLiabilityPolicy.TryGet(
                    Meta, invalidSource), invalidSource);
            }

            Assert.AreEqual(1,
                ApprovalLiabilityPolicy.ActiveLiabilities(Meta).Count);
            Assert.AreEqual(1,
                ApprovalLiabilityPolicy.ForClaimant(Meta, "malformed").Count);
            Assert.AreEqual(approved.EncounterId,
                ApprovalLiabilityPolicy.ActiveLiabilities(Meta)[0]
                    .SourceEncounterId);
        }

        [Test]
        public void Queries_AreSafeReadOnlyViews_AndDoNotWriteSaveFiles()
        {
            ActiveClaimData claim = Claim("CLM-READONLY", "readonly");
            RunData data = RunData("LIABILITY-READONLY", 1, claim);
            RunStateController run = Controller(data);
            CommitActual(
                claim, ClaimResolutionKind.Approve, data, run, Meta);

            string metaPath = Path.Combine(SaveDirectory, "meta.json");
            string diskBefore = File.ReadAllText(metaPath);
            string memoryBefore = JsonConvert.SerializeObject(Meta);
            int encounterCount = Meta.Encounters.Count;
            bool proofActive = Meta.EliasProof.IsActive;

            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(
                Meta, "unknown"));
            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(
                Meta, null));
            Assert.IsNull(ApprovalLiabilityPolicy.TryGet(Meta, "  "));
            Assert.IsEmpty(ApprovalLiabilityPolicy.ForClaimant(Meta, null));
            Assert.IsEmpty(ApprovalLiabilityPolicy.ForClaimant(Meta, " "));
            Assert.AreEqual(1,
                ApprovalLiabilityPolicy.ActiveLiabilities(Meta).Count);
            Assert.AreEqual(1,
                ApprovalLiabilityPolicy.ForClaimant(Meta, "readonly").Count);

            Assert.AreEqual(memoryBefore, JsonConvert.SerializeObject(Meta));
            Assert.AreEqual(diskBefore, File.ReadAllText(metaPath));
            Assert.AreEqual(encounterCount, Meta.Encounters.Count);
            Assert.AreEqual(proofActive, Meta.EliasProof.IsActive);
        }

        [Test]
        public void LegacyMetaWithoutField_LoadsEmpty_DoesNotBackfill_ThenAcceptsNewApproval()
        {
            var legacy = new MetaProgressData();
            ActiveClaimData historical = Claim(
                "CLM-LEGACY-HISTORY", "legacy_claimant");
            RunData historicalRun = RunData(
                "LIABILITY-LEGACY-HISTORY", 1, historical);
            Present(historical, historicalRun, legacy);
            legacy.Encounters.MarkCompleted(
                historical.EncounterId,
                ClaimResolutionKind.Approve,
                1L);

            JObject json = JObject.FromObject(legacy);
            json.Remove(nameof(MetaProgressData.ApprovalLiabilities));
            Directory.CreateDirectory(SaveDirectory);
            File.WriteAllText(
                Path.Combine(SaveDirectory, "meta.json"),
                json.ToString(Formatting.Indented));

            MetaProgressData loaded = SaveSystem.LoadMeta();
            Assert.IsNotNull(loaded.ApprovalLiabilities);
            Assert.AreEqual(0, loaded.ApprovalLiabilities.Count);
            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(
                loaded, historical.EncounterId),
                "Historical approvals must not be retroactively backfilled.");

            ActiveClaimData current = Claim(
                "CLM-LEGACY-NEW", "legacy_claimant");
            RunData currentRun = RunData(
                "LIABILITY-LEGACY-NEW", 2, current);
            RunStateController controller = Controller(currentRun);
            CommitActual(
                current,
                ClaimResolutionKind.Approve,
                currentRun,
                controller,
                loaded);

            MetaProgressData reloaded = SaveSystem.LoadMeta();
            Assert.AreEqual(1, reloaded.ApprovalLiabilities.Count);
            Assert.IsTrue(ApprovalLiabilityPolicy.HasApprovalLiability(
                reloaded, current.EncounterId));
            Assert.IsFalse(ApprovalLiabilityPolicy.HasApprovalLiability(
                reloaded, historical.EncounterId));
        }

        [Test]
        public void ResolvedRecord_IsExcludedFromActiveView_ButSourceLookupIsDeterministic()
        {
            ActiveClaimData claim = Claim("CLM-RESOLVED", "resolved_claimant");
            RunData data = RunData("LIABILITY-RESOLVED", 1, claim);
            RunStateController run = Controller(data);
            CommitActual(
                claim, ClaimResolutionKind.Approve, data, run, Meta);

            ApprovalLiabilityRecord record =
                Meta.ApprovalLiabilities.Find(claim.EncounterId);
            record.Resolved = true;
            Assert.IsTrue(SaveSystem.SaveMeta(Meta));

            MetaProgressData loaded = SaveSystem.LoadMeta();
            Assert.IsEmpty(
                ApprovalLiabilityPolicy.ActiveLiabilities(loaded));
            Assert.IsTrue(ApprovalLiabilityPolicy.TryGet(
                loaded, claim.EncounterId).Resolved,
                "Source lookup returns the persisted record regardless of active state.");
            Assert.IsTrue(ApprovalLiabilityPolicy.HasApprovalLiability(
                loaded, claim.EncounterId));
            Assert.AreEqual(1,
                ApprovalLiabilityPolicy.ForClaimant(
                    loaded, "resolved_claimant").Count);
        }

        [Test]
        public void MaraScheduledAndApprovedWithoutElias_HasDurableGeneralLiability()
        {
            var queue = new List<ActiveClaimData>
            {
                Claim("CLM-FILLER-1", "filler_1"),
                Claim("CLM-FILLER-2", "filler_2"),
                Claim("CLM-FILLER-3", "filler_3"),
            };
            Assert.IsTrue(ControlClaimantContent.TryScheduleControlClaimant(
                queue, 3, out ActiveClaimData mara));
            Assert.AreSame(mara, queue[ControlClaimantContent.QueuePosition - 1]);

            RunData data = RunData("LIABILITY-MARA", 3, mara);
            RunStateController run = Controller(data);
            CommitActual(
                mara, ClaimResolutionKind.Approve, data, run, Meta);

            MetaProgressData loaded = SaveSystem.LoadMeta();
            ApprovalLiabilityRecord liability =
                ApprovalLiabilityPolicy.TryGet(loaded, mara.EncounterId);
            Assert.IsNotNull(liability);
            Assert.AreEqual(ControlClaimantContent.StableClaimantId,
                liability.SourceClientVariantId);
            Assert.IsFalse(loaded.EliasProof.IsActive);
            Assert.IsEmpty(loaded.CompletedProofSessions);
        }

        [TestCase(true, false, 0)]
        [TestCase(false, true, 1)]
        public void CanonicalFirst_ConflictingStateAndProvenance_AreNonDestructiveOnDisk(
            bool canonicalResolved,
            bool duplicateResolved,
            int expectedActive)
        {
            ActiveClaimData claim = Claim(
                "CLM-CANONICAL-DISK", "canonical_claimant");
            RunData data = RunData("LIABILITY-CANONICAL-DISK", 1, claim);
            RunStateController run = Controller(data);
            CommitActual(
                claim, ClaimResolutionKind.Approve, data, run, Meta);

            ApprovalLiabilityRecord canonical =
                Meta.ApprovalLiabilities.Find(claim.EncounterId);
            canonical.Resolved = canonicalResolved;
            InjectDuplicate(
                Meta,
                claim.EncounterId,
                "duplicate_claimant",
                duplicateResolved);

            Assert.IsTrue(SaveSystem.SaveMeta(Meta));
            string metaPath = Path.Combine(SaveDirectory, "meta.json");
            string persistedBeforeLoad = File.ReadAllText(metaPath);

            MetaProgressData loaded = SaveSystem.LoadMeta();
            Assert.AreEqual(2, loaded.ApprovalLiabilities.Count,
                "Load must preserve both malformed physical rows.");

            ApprovalLiabilityRecord trusted =
                ApprovalLiabilityPolicy.TryGet(loaded, claim.EncounterId);
            Assert.AreEqual("canonical_claimant",
                trusted.SourceClientVariantId);
            Assert.AreEqual(canonicalResolved, trusted.Resolved);
            Assert.IsTrue(ApprovalLiabilityPolicy.HasApprovalLiability(
                loaded, claim.EncounterId));
            Assert.AreEqual(expectedActive,
                ApprovalLiabilityPolicy.ActiveLiabilities(loaded).Count);
            Assert.AreEqual(1,
                ApprovalLiabilityPolicy.ForClaimant(
                    loaded, "canonical_claimant").Count);
            Assert.IsEmpty(ApprovalLiabilityPolicy.ForClaimant(
                loaded, "duplicate_claimant"));

            Assert.AreEqual(2, loaded.ApprovalLiabilities.Count,
                "Trusted reads must not normalize the in-memory ledger.");
            Assert.AreEqual(persistedBeforeLoad, File.ReadAllText(metaPath),
                "Load and trusted reads must not rewrite the malformed save.");
        }

        [Test]
        public void MalformedDuplicateRows_DoNotMultiplyOneSourceLiability()
        {
            ActiveClaimData claim = Claim(
                "CLM-MALFORMED-DUPLICATE", "duplicate_row_claimant");
            RunData data = RunData("LIABILITY-MALFORMED-DUP", 1, claim);
            RunStateController run = Controller(data);
            CommitActual(
                claim, ClaimResolutionKind.Approve, data, run, Meta);

            InjectDuplicate(
                Meta,
                claim.EncounterId,
                claim.ClientVariantId,
                resolved: false);
            Assert.AreEqual(2, Meta.ApprovalLiabilities.Count,
                "The malformed fixture must contain a duplicate persisted row.");
            Assert.IsTrue(SaveSystem.SaveMeta(Meta));

            MetaProgressData loaded = SaveSystem.LoadMeta();
            Assert.DoesNotThrow(() =>
                ApprovalLiabilityPolicy.TryGet(loaded, claim.EncounterId));
            int activeCount =
                ApprovalLiabilityPolicy.ActiveLiabilities(loaded).Count;
            int claimantCount = ApprovalLiabilityPolicy.ForClaimant(
                loaded, claim.ClientVariantId).Count;

            Assert.IsTrue(activeCount == 1 && claimantCount == 1,
                "One source EncounterId must not multiply query effects. " +
                $"ActiveLiabilities={activeCount}, ForClaimant={claimantCount}.");
        }

        private static void InjectDuplicate(
            MetaProgressData meta,
            string sourceEncounterId,
            string clientVariantId,
            bool resolved)
        {
            List<ApprovalLiabilityRecord> records =
                (List<ApprovalLiabilityRecord>)
                LiabilityRecordsField.GetValue(meta.ApprovalLiabilities);
            records.Add(new ApprovalLiabilityRecord
            {
                SourceEncounterId = sourceEncounterId,
                SourceClientVariantId = clientVariantId,
                Resolved = resolved,
            });
        }

        private static void CommitActual(
            ActiveClaimData claim,
            ClaimResolutionKind disposition,
            RunData data,
            RunStateController run,
            MetaProgressData meta)
        {
            data.ActiveClaim = claim;
            Present(claim, data, meta);
            Assert.IsTrue(Commit(
                claim, disposition, run, meta).Committed);
        }
    }
}
