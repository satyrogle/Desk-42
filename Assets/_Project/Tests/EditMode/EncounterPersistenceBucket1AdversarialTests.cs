using System.IO;
using Desk42.Core;
using Desk42.Encounter;
using NUnit.Framework;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Independent Bucket 1 attempts to falsify identity, history,
    /// idempotency, recurrence, migration, and authoritative routing.
    /// </summary>
    public sealed class EncounterPersistenceBucket1AdversarialTests
        : Bucket1PersistenceFixture
    {
        [Test]
        public void ActiveEncounterId_AfterRunSaveReload_RemainsStable()
        {
            var claim = Claim();
            var run = RunData(activeClaim: claim);
            string before = EncounterCommitService.EnsureEncounterId(claim, run);

            Assert.IsTrue(SaveSystem.SaveRun(run));
            var loaded = SaveSystem.LoadRun();

            Assert.IsNotNull(loaded?.ActiveClaim);
            Assert.AreEqual(before, loaded.ActiveClaim.EncounterId);
            Assert.AreEqual(before,
                EncounterCommitService.EnsureEncounterId(loaded.ActiveClaim, loaded));
            Assert.AreEqual(1, loaded.EncounterSequence,
                "Reload must preserve the sequence used to allocate the active id.");
        }

        [Test]
        public void DifferentRuns_WithCollidingClaimIdsAndSeed_DoNotShareEncounterIdentity()
        {
            var firstClaim = Claim(claimId: "CLM-COLLISION");
            var firstRun = RunData(seed: "REPLAYED-SEED", shift: 1);
            string firstId =
                EncounterCommitService.EnsureEncounterId(firstClaim, firstRun);

            var secondClaim = Claim(claimId: "CLM-COLLISION");
            var secondRun = RunData(seed: "REPLAYED-SEED", shift: 1);
            string secondId =
                EncounterCommitService.EnsureEncounterId(secondClaim, secondRun);

            Assert.AreNotEqual(firstId, secondId,
                "Distinct encounters must not collapse merely because a new run " +
                "reuses the same seed, shift, sequence position, and ClaimId.");
        }

        [Test]
        public void PresentedIncompleteEncounter_AfterMetaReload_IsNotAVisit()
        {
            var claim = Claim();
            var run = RunData(activeClaim: claim);
            Present(claim, run, Meta);

            Assert.IsTrue(SaveSystem.SaveMeta(Meta));
            var loaded = SaveSystem.LoadMeta();
            var record = OnlyRecord(loaded);

            Assert.IsFalse(record.Completed);
            Assert.AreEqual(ClaimResolutionKind.Unspecified, record.Outcome);
            Assert.AreEqual(0L, record.CommittedAtUtcTicks);
            Assert.AreEqual(1, loaded.GetTotalPresentations(claim.ClientVariantId));
            Assert.AreEqual(0, loaded.GetTotalVisits(claim.ClientVariantId));
        }

        [TestCase(ClaimResolutionKind.Approve)]
        [TestCase(ClaimResolutionKind.Deny)]
        public void ApproveAndDeny_CommitThroughAuthoritativeHistoryAndSave(
            ClaimResolutionKind kind)
        {
            var claim = Claim(claimId: $"CLM-{kind}");
            var data = RunData(activeClaim: claim);
            var run = Controller(data);
            Present(claim, data, Meta);

            var result = Commit(claim, kind, run, Meta);
            var loadedMeta = SaveSystem.LoadMeta();
            var loadedRun = SaveSystem.LoadRun();

            Assert.IsTrue(result.Committed);
            Assert.AreEqual(kind, loadedMeta.Encounters.Find(claim.EncounterId).Outcome);
            Assert.AreEqual(1, loadedMeta.GetTotalVisits(claim.ClientVariantId));
            Assert.AreEqual(1, loadedRun.Stats.ClaimsProcessed);
            Assert.AreEqual(
                kind == ClaimResolutionKind.Approve ? 1 : 0,
                loadedRun.Stats.ApprovedClaims);
            Assert.AreEqual(
                kind == ClaimResolutionKind.Deny ? 1 : 0,
                loadedRun.Stats.DeniedClaims);
        }

        [Test]
        public void RepeatCommit_InMemory_AppliesEverySideEffectExactlyOnce()
        {
            var claim = Claim();
            var data = RunData(activeClaim: claim);
            var run = Controller(data);
            Present(claim, data, Meta);

            var first = Commit(claim, ClaimResolutionKind.Approve, run, Meta);
            int creditsAfterFirst = data.CorporateCredits;
            float sanityAfterFirst = data.Sanity;
            var second = Commit(claim, ClaimResolutionKind.Deny, run, Meta);

            Assert.IsTrue(first.Committed);
            Assert.IsFalse(second.Committed);
            Assert.AreEqual(CommitRejection.AlreadyCommitted, second.Rejection);
            Assert.AreEqual(1, data.Stats.ClaimsProcessed);
            Assert.AreEqual(1, data.Stats.ApprovedClaims);
            Assert.AreEqual(0, data.Stats.DeniedClaims);
            Assert.AreEqual(creditsAfterFirst, data.CorporateCredits);
            Assert.AreEqual(sanityAfterFirst, data.Sanity);
            Assert.AreEqual(1, Meta.GetTotalVisits(claim.ClientVariantId));
            Assert.AreEqual(
                ClaimResolutionKind.Approve,
                Meta.Encounters.Find(claim.EncounterId).Outcome,
                "A duplicate with a different disposition must not overwrite outcome.");
        }

        [Test]
        public void RepeatCommit_AfterSaveReload_RemainsIdempotent()
        {
            var claim = Claim();
            var originalData = RunData(activeClaim: claim);
            var originalRun = Controller(originalData);
            Present(claim, originalData, Meta);
            Assert.IsTrue(
                Commit(claim, ClaimResolutionKind.Approve, originalRun, Meta).Committed);

            var loadedData = SaveSystem.LoadRun();
            var loadedMeta = SaveSystem.LoadMeta();
            var resumedRun = Controller(loadedData);
            var duplicate = Commit(
                loadedData.ActiveClaim,
                ClaimResolutionKind.Deny,
                resumedRun,
                loadedMeta);

            Assert.IsFalse(duplicate.Committed);
            Assert.AreEqual(CommitRejection.AlreadyCommitted, duplicate.Rejection);
            Assert.AreEqual(1, loadedData.Stats.ClaimsProcessed);
            Assert.AreEqual(1, loadedData.Stats.ApprovedClaims);
            Assert.AreEqual(0, loadedData.Stats.DeniedClaims);
            Assert.AreEqual(1,
                loadedMeta.GetTotalVisits(claim.ClientVariantId));
            Assert.AreEqual(
                ClaimResolutionKind.Approve,
                loadedMeta.Encounters.Find(claim.EncounterId).Outcome);
        }

        [Test]
        public void LiquifyCommit_PersistsHistoryVisitConsequenceAndRunMutation()
        {
            var claim = Claim(claimId: "CLM-LIQUIFY");
            var data = RunData(activeClaim: claim);
            data.DarkIntelligence = 7;
            var run = Controller(data);
            Present(claim, data, Meta);

            var result = Commit(claim, ClaimResolutionKind.Liquify, run, Meta);
            var loadedMeta = SaveSystem.LoadMeta();
            var loadedRun = SaveSystem.LoadRun();
            var record = loadedMeta.Encounters.Find(claim.EncounterId);

            Assert.IsTrue(result.Committed);
            Assert.AreEqual(ClaimResolutionKind.Liquify, record.Outcome);
            Assert.IsTrue(record.Completed);
            Assert.AreEqual(1, loadedMeta.GetTotalVisits(claim.ClientVariantId));
            Assert.AreEqual(10, loadedRun.DarkIntelligence,
                "Liquify's declared +3 consequence must survive reload.");
            Assert.AreEqual(1, loadedRun.Stats.ClaimsProcessed);
            Assert.AreEqual(1, loadedRun.Stats.LiquifiedClaims);
            Assert.IsTrue(File.Exists(Path.Combine(SaveDirectory, "run.json")));
            Assert.IsTrue(File.Exists(Path.Combine(SaveDirectory, "meta.json")));
        }

        [Test]
        public void EliasProof_LiquifyIsRejectedBeforeDispositionMutation()
        {
            var proof = Component<EliasProofSessionController>("EliasProof");
            proof.BeginProofSession("bucket1-liquify-proof");
            proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey);

            bool allowed = proof.TryValidateDisposition(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey,
                ClaimResolutionKind.Liquify,
                out string failureReason);

            Assert.IsFalse(allowed);
            Assert.AreEqual(
                EliasProofSessionController.ContinuityHoldFailureReason,
                failureReason);
            Assert.AreEqual(
                EliasShift1Disposition.None,
                proof.State.Shift1Disposition,
                "Rejected Liquify must not write a proof disposition.");
            Assert.AreEqual(0, Meta.Encounters.Count,
                "The validation gate must run before the authoritative commit.");
        }

        [Test]
        public void RepeatVisitConsumer_DerivesFromHistory_NotPoisonedLegacyCount()
        {
            const string variant = "repeat_consumer";
            var data = RunData();

            for (int i = 0; i < 2; i++)
            {
                var prior = Claim($"CLM-PRIOR-{i}", variant);
                Present(prior, data, Meta);
                Meta.Encounters.MarkCompleted(
                    prior.EncounterId, ClaimResolutionKind.Approve, i + 1);
            }
            Meta.GetOrCreateProfile(variant).TotalVisits = 99;

            var current = Claim("CLM-CURRENT", variant);
            var baseline = Present(current, data, Meta);

            Assert.AreEqual(2, baseline.PriorVisits);
            Assert.AreEqual(3, baseline.CurrentVisitNumber);
            Assert.AreEqual(2, Meta.GetTotalVisits(variant));
        }

        [Test]
        public void OldMetaWithoutEncounterHistory_LoadsWithSafeEmptyHistory()
        {
            File.WriteAllText(
                Path.Combine(SaveDirectory, "meta.json"),
                "{ 'SaveVersion': 3, 'BankBalance': 42 }");

            var loaded = SaveSystem.LoadMeta();

            Assert.IsNotNull(loaded);
            Assert.IsNotNull(loaded.Encounters);
            Assert.AreEqual(0, loaded.Encounters.Count);
            Assert.AreEqual(0, loaded.GetTotalVisits("legacy_claimant"));
            Assert.AreEqual(0, loaded.GetTotalPresentations("legacy_claimant"));
        }

        [Test]
        public void AdditiveSchemaDefault_DoesNotFabricateLegacyCompletedVisits()
        {
            File.WriteAllText(
                Path.Combine(SaveDirectory, "meta.json"),
                @"{
                  'SaveVersion': 3,
                  'RepeatOffenderDB': {
                    'legacy_claimant': {
                      'ClientVariantId': 'legacy_claimant',
                      'TotalVisits': 17
                    }
                  }
                }");

            var loaded = SaveSystem.LoadMeta();

            Assert.AreEqual(17,
                loaded.GetOrCreateProfile("legacy_claimant").TotalVisits,
                "The legacy value may deserialize for compatibility.");
            Assert.AreEqual(0, loaded.GetTotalVisits("legacy_claimant"),
                "A legacy counter must not fabricate completed encounter records.");
            Assert.AreEqual(0, loaded.Encounters.Count);
        }

        [Test]
        public void ThreeSequentialCompletedEncounters_DeriveVisitsOneTwoThree()
        {
            const string variant = "three_visit_claimant";
            var data = RunData();
            var run = Controller(data);

            for (int expectedVisit = 1; expectedVisit <= 3; expectedVisit++)
            {
                var claim = Claim($"CLM-{expectedVisit}", variant);
                data.ActiveClaim = claim;

                var baseline = Present(claim, data, Meta);
                Assert.AreEqual(expectedVisit, baseline.CurrentVisitNumber);

                var result = Commit(
                    claim, ClaimResolutionKind.Approve, run, Meta);
                Assert.IsTrue(result.Committed);
                Assert.AreEqual(expectedVisit, Meta.GetTotalVisits(variant));
            }

            Assert.AreEqual(3, Meta.GetTotalPresentations(variant));
            Assert.AreEqual(3, data.Stats.ClaimsProcessed);
        }

        [Test]
        public void ActiveIncompleteEncounter_RemainsDistinctAfterReload()
        {
            var claim = Claim();
            var data = RunData(activeClaim: claim);
            Present(claim, data, Meta);
            Assert.IsTrue(SaveSystem.SaveRun(data));
            Assert.IsTrue(SaveSystem.SaveMeta(Meta));

            var loadedRun = SaveSystem.LoadRun();
            var loadedMeta = SaveSystem.LoadMeta();
            var record = loadedMeta.Encounters.Find(
                loadedRun.ActiveClaim.EncounterId);

            Assert.IsNotNull(record);
            Assert.IsFalse(record.Completed);
            Assert.AreEqual(ClaimResolutionKind.Unspecified, record.Outcome);
            Assert.AreEqual(0L, record.CommittedAtUtcTicks);
            Assert.IsFalse(loadedRun.ActiveClaim.IsResolved);
            Assert.AreEqual(0, loadedMeta.GetTotalVisits(claim.ClientVariantId));
        }

        [Test]
        public void ReloadAfterCommit_DoesNotExposeCompletedEncounterAsActiveIncomplete()
        {
            var claim = Claim();
            var data = RunData(activeClaim: claim);
            var run = Controller(data);
            Present(claim, data, Meta);
            Assert.IsTrue(
                Commit(claim, ClaimResolutionKind.Approve, run, Meta).Committed);

            var loadedRun = SaveSystem.LoadRun();
            var loadedMeta = SaveSystem.LoadMeta();
            var record = loadedMeta.Encounters.Find(claim.EncounterId);

            Assert.IsTrue(record.Completed);
            Assert.That(
                loadedRun.ActiveClaim == null || loadedRun.ActiveClaim.IsResolved,
                "The same disk snapshot must not describe an encounter as " +
                "completed in history while retaining it as an unresolved active claim.");
        }
    }
}
