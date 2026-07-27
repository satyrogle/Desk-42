using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Desk42.Claims;
using Desk42.Core;
using Desk42.Encounter;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Final independent probes added after the 9e49d2b repair. These focus on
    /// lifecycle boundaries not exercised by the original adversarial suite.
    /// </summary>
    public sealed class Bucket1FinalIndependentValidationTests
        : Bucket1PersistenceFixture
    {
        private const BindingFlags InstancePrivate =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo RunDataField =
            typeof(RunStateController).GetField("_data", InstancePrivate);

        private static readonly FieldInfo GameManagerInstanceField =
            typeof(GameManager).GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly FieldInfo GameManagerRunField =
            typeof(GameManager).GetField(
                "<Run>k__BackingField", InstancePrivate);

        private static readonly MethodInfo HandleClaimResolved =
            typeof(ShiftManager).GetMethod(
                "HandleClaimResolved", InstancePrivate);

        private static readonly MethodInfo StartShiftManager =
            typeof(ShiftManager).GetMethod("Start", InstancePrivate);

        private static readonly FieldInfo TideField =
            typeof(ShiftManager).GetField("_tide", InstancePrivate);

        private static readonly FieldInfo TideTuningField =
            typeof(ShiftManager).GetField("_tideTuning", InstancePrivate);

        private static readonly FieldInfo AnomalyTagsField =
            typeof(ShiftManager).GetField("_anomalyTags", InstancePrivate);

        private readonly List<UnityEngine.Object> _ownedObjects = new();

        [TearDown]
        public void TearDownFinalValidationObjects()
        {
            for (int i = _ownedObjects.Count - 1; i >= 0; i--)
            {
                if (_ownedObjects[i] != null)
                    UnityEngine.Object.DestroyImmediate(_ownedObjects[i]);
            }
            _ownedObjects.Clear();

            if (GameManagerInstanceField != null)
                GameManagerInstanceField.SetValue(null, null);
        }

        [Test]
        public void BeginNewRun_SameSeedCreatesDistinctOpaqueRunIdentity()
        {
            const int gameplaySeed = 421001;
            var first = Controller(RunData());
            var second = Controller(RunData());

            first.BeginNewRun(gameplaySeed, "auditor", 1, Meta);
            var firstData = first.RawData;
            string firstId = firstData.RunInstanceId;
            string repeated = EncounterCommitService.EnsureRunInstanceId(firstData);

            second.BeginNewRun(gameplaySeed, "auditor", 1, Meta);
            var secondData = second.RawData;

            Assert.AreEqual(firstId, repeated,
                "One run must allocate its identity exactly once.");
            Assert.AreNotEqual(firstId, secondData.RunInstanceId,
                "Independent replays of one gameplay seed need distinct namespaces.");
            StringAssert.IsMatch("^[0-9a-f]{12}$", firstId,
                "Run identity should be opaque GUID-derived hexadecimal.");
            StringAssert.IsMatch("^[0-9a-f]{12}$", secondData.RunInstanceId);
            Assert.AreEqual(firstData.MasterSeed, secondData.MasterSeed);
            Assert.AreEqual(firstData.SeedCode, secondData.SeedCode);
            Assert.AreEqual(
                firstData.EscalatingRegulationCardId,
                secondData.EscalatingRegulationCardId,
                "Run identity must not perturb deterministic gameplay draws.");
        }

        [Test]
        public void SaveResumeAndControllerReconstruction_PreserveRunAndEncounterIdentity()
        {
            var claim = Claim();
            var data = RunData(activeClaim: claim);
            string encounterId =
                EncounterCommitService.EnsureEncounterId(claim, data);
            string runInstanceId = data.RunInstanceId;

            Assert.IsTrue(SaveSystem.SaveRun(data));
            var firstLoad = SaveSystem.LoadRun();
            var firstController = Controller(firstLoad);
            string firstResumeId =
                EncounterCommitService.EnsureEncounterId(
                    firstLoad.ActiveClaim, firstController.RawData);

            Assert.IsTrue(SaveSystem.SaveRun(firstController.RawData));
            var secondLoad = SaveSystem.LoadRun();
            var reconstructedController = Controller(secondLoad);
            string secondResumeId =
                EncounterCommitService.EnsureEncounterId(
                    secondLoad.ActiveClaim, reconstructedController.RawData);

            Assert.AreEqual(runInstanceId, firstLoad.RunInstanceId);
            Assert.AreEqual(runInstanceId, secondLoad.RunInstanceId);
            Assert.AreEqual(encounterId, firstResumeId);
            Assert.AreEqual(encounterId, secondResumeId);
            Assert.AreEqual(1, secondLoad.EncounterSequence,
                "Reconstruction of the same encounter must not advance sequence.");
        }

        [Test]
        public void LegacyRunWithoutRunInstanceId_AllocatesAndPersistsCompatibilityIdentity()
        {
            File.WriteAllText(
                Path.Combine(SaveDirectory, "run.json"),
                @"{
                  'SaveVersion': 3,
                  'SeedCode': 'LEGACY-SEED',
                  'ShiftNumber': 2,
                  'EncounterSequence': 0,
                  'ActiveClaim': {
                    'ClaimId': 'CLM-LEGACY',
                    'ClientVariantId': 'legacy_claimant',
                    'ClientSpeciesId': 'human'
                  }
                }");

            var legacy = SaveSystem.LoadRun();
            Assert.IsTrue(string.IsNullOrWhiteSpace(legacy.RunInstanceId));

            string encounterId = EncounterCommitService.EnsureEncounterId(
                legacy.ActiveClaim, legacy);
            string allocatedRunId = legacy.RunInstanceId;
            Assert.IsTrue(SaveSystem.SaveRun(legacy));

            var reloaded = SaveSystem.LoadRun();

            Assert.IsNotEmpty(allocatedRunId);
            Assert.AreEqual(allocatedRunId, reloaded.RunInstanceId);
            Assert.AreEqual(encounterId, reloaded.ActiveClaim.EncounterId);
            StringAssert.StartsWith(
                $"ENC-{allocatedRunId}-S2-", encounterId);
        }

        [Test]
        public void MultipleEncounters_ShareRunNamespaceButUseDistinctSequence()
        {
            var data = RunData(seed: "REPLAYABLE", shift: 3);
            var first = Claim("CLM-COLLIDE");
            var second = Claim("CLM-COLLIDE");
            var third = Claim("CLM-COLLIDE");

            string firstId = EncounterCommitService.EnsureEncounterId(first, data);
            string secondId = EncounterCommitService.EnsureEncounterId(second, data);
            string thirdId = EncounterCommitService.EnsureEncounterId(third, data);
            string prefix = $"ENC-{data.RunInstanceId}-S3-";

            StringAssert.StartsWith(prefix, firstId);
            StringAssert.StartsWith(prefix, secondId);
            StringAssert.StartsWith(prefix, thirdId);
            Assert.AreEqual(3, new HashSet<string>
                { firstId, secondId, thirdId }.Count);
            StringAssert.EndsWith("-001", firstId);
            StringAssert.EndsWith("-002", secondId);
            StringAssert.EndsWith("-003", thirdId);
            Assert.AreEqual(3, data.EncounterSequence);
        }

        [TestCase(ClaimResolutionKind.Approve)]
        [TestCase(ClaimResolutionKind.Deny)]
        [TestCase(ClaimResolutionKind.Liquify)]
        public void CommitCrashBoundary_ReloadIsConsistentAndDuplicateSafe(
            ClaimResolutionKind kind)
        {
            var claim = Claim($"CLM-{kind}", "boundary_claimant", "human");
            var data = RunData(activeClaim: claim);
            var run = Controller(data);
            Present(claim, data, Meta);

            var committed = Commit(claim, kind, run, Meta);
            int sequenceAfterCommit = data.EncounterSequence;
            int creditsAfterCommit = data.CorporateCredits;
            int darkIntelligenceAfterCommit = data.DarkIntelligence;

            var loadedRun = SaveSystem.LoadRun();
            var loadedMeta = SaveSystem.LoadMeta();
            var loadedRecord =
                loadedMeta.Encounters.Find(claim.EncounterId);

            Assert.IsTrue(committed.Committed);
            Assert.IsTrue(loadedRecord.Completed);
            Assert.AreEqual(kind, loadedRecord.Outcome);
            Assert.IsNotNull(loadedRun.ActiveClaim);
            Assert.IsTrue(loadedRun.ActiveClaim.IsResolved);
            Assert.AreEqual(kind, loadedRun.ActiveClaim.ResolutionKind);
            Assert.AreEqual(claim.EncounterId, loadedRun.ActiveClaim.EncounterId);
            Assert.AreEqual(claim.ClaimId, loadedRecord.ClaimId);
            Assert.AreEqual(claim.ClientVariantId, loadedRecord.ClientVariantId);
            Assert.AreEqual(1, loadedRun.ResolvedClaims.Count);
            Assert.AreEqual(
                claim.EncounterId,
                loadedRun.ResolvedClaims[0].EncounterId);
            Assert.AreEqual(1,
                loadedMeta.GetTotalVisits(claim.ClientVariantId));

            var resumed = Controller(loadedRun);
            var duplicate = Commit(
                loadedRun.ActiveClaim, kind, resumed, loadedMeta);
            Present(loadedRun.ActiveClaim, loadedRun, loadedMeta);

            Assert.IsFalse(duplicate.Committed);
            Assert.AreEqual(
                CommitRejection.AlreadyCommitted, duplicate.Rejection);
            Assert.AreEqual(1, loadedRun.ResolvedClaims.Count);
            Assert.AreEqual(sequenceAfterCommit, loadedRun.EncounterSequence);
            Assert.AreEqual(creditsAfterCommit, loadedRun.CorporateCredits);
            Assert.AreEqual(
                darkIntelligenceAfterCommit,
                loadedRun.DarkIntelligence);
            Assert.AreEqual(1,
                loadedMeta.GetTotalPresentations(claim.ClientVariantId));
            Assert.AreEqual(1,
                loadedMeta.GetTotalVisits(claim.ClientVariantId));
        }

        [Test]
        public void NextEncounter_AfterResolvedClaimDiscard_UsesNextIdentityNormally()
        {
            const string variant = "returning_claimant";
            var first = Claim("CLM-FIRST", variant);
            var data = RunData(activeClaim: first);
            var run = Controller(data);
            Present(first, data, Meta);
            Assert.IsTrue(
                Commit(first, ClaimResolutionKind.Approve, run, Meta).Committed);

            string runInstanceId = data.RunInstanceId;
            data.ActiveClaim = null;

            var next = Claim("CLM-NEXT", variant);
            data.ActiveClaim = next;
            var nextBaseline = Present(next, data, Meta);

            Assert.AreEqual(2, data.EncounterSequence);
            StringAssert.StartsWith(
                $"ENC-{runInstanceId}-S1-", next.EncounterId);
            Assert.AreNotEqual(first.EncounterId, next.EncounterId);
            Assert.AreEqual(1, nextBaseline.PriorVisits);
            Assert.AreEqual(2, nextBaseline.CurrentVisitNumber);
            Assert.IsFalse(next.IsResolved);
            Assert.AreEqual(1, data.ResolvedClaims.Count);
            Assert.AreEqual(first.EncounterId,
                data.ResolvedClaims[0].EncounterId);
            Assert.AreEqual(1, Meta.GetTotalVisits(variant),
                "Presentation of the next encounter must not advance visits.");
        }

        [Test]
        public void BonusIndex_FirstClaimCannotSelfAwardOrIndexBeforeStart()
        {
            var current = Claim("CLM-FIRST", species: "same_species");
            current.AnomalyTagIds = new[] { "tag-current" };
            var data = RunData(activeClaim: current);
            var run = Controller(data);
            var shift = ShiftManagerWithTags(
                Tag("tag-current", "CurrentCategory"));
            Present(current, data, Meta);

            CommitResult result = default;
            Assert.DoesNotThrow(() =>
                result = CommitWithBonusRates(
                    current, ClaimResolutionKind.Approve,
                    run, Meta, shift.BonusRates));

            Assert.IsTrue(result.Committed,
                "The first claim must commit successfully.");
            Assert.AreEqual(12, data.CorporateCredits,
                "A sole current claim must receive only its base outcome, " +
                "not a previous-claim bonus from comparing itself.");
            Assert.AreEqual(1, data.ResolvedClaims.Count);
            Assert.AreSame(current, data.ResolvedClaims[0]);
            Assert.IsTrue(current.ConsequencesApplied);
        }

        [Test]
        public void BonusIndex_ComparesActualPreviousClaimRatherThanCurrentClaim()
        {
            var nonMatchingPrevious =
                ResolvedClaim("CLM-NONMATCH-PREVIOUS", "species-a",
                    "tag-previous-nonmatch");
            var nonMatchingCurrent =
                Claim("CLM-NONMATCH-CURRENT", species: "species-b");
            nonMatchingCurrent.AnomalyTagIds =
                new[] { "tag-current-nonmatch" };
            var nonMatchingData = RunData(activeClaim: nonMatchingCurrent);
            nonMatchingData.ResolvedClaims.Add(nonMatchingPrevious);
            var nonMatchingRun = Controller(nonMatchingData);
            var nonMatchingShift = ShiftManagerWithTags(
                Tag("tag-previous-nonmatch", "Category-A"),
                Tag("tag-current-nonmatch", "Category-B"));
            Present(nonMatchingCurrent, nonMatchingData, Meta);

            var nonMatchingCommit = CommitWithBonusRates(
                nonMatchingCurrent, ClaimResolutionKind.Approve,
                nonMatchingRun, Meta, nonMatchingShift.BonusRates);

            Assert.IsTrue(nonMatchingCommit.Committed);
            Assert.AreEqual(12, nonMatchingData.CorporateCredits,
                "Different previous/current claims must not earn the +5/+3 " +
                "that a current-vs-itself comparison would fabricate.");

            var matchingMeta = new MetaProgressData();
            var matchingPrevious =
                ResolvedClaim("CLM-MATCH-PREVIOUS", "same-species",
                    "tag-previous-match");
            var matchingCurrent =
                Claim("CLM-MATCH-CURRENT", species: "same-species");
            matchingCurrent.AnomalyTagIds =
                new[] { "tag-current-match" };
            var matchingData = RunData(activeClaim: matchingCurrent);
            matchingData.ResolvedClaims.Add(matchingPrevious);
            var matchingRun = Controller(matchingData);
            var matchingShift = ShiftManagerWithTags(
                Tag("tag-previous-match", "SharedCategory"),
                Tag("tag-current-match", "SharedCategory"));
            Present(matchingCurrent, matchingData, matchingMeta);

            var matchingCommit = CommitWithBonusRates(
                matchingCurrent, ClaimResolutionKind.Approve,
                matchingRun, matchingMeta, matchingShift.BonusRates);

            Assert.IsTrue(matchingCommit.Committed);
            Assert.AreEqual(20, matchingData.CorporateCredits,
                "The actual previous/current pair should award the base 12, " +
                "+5 matching-species bonus and +3 matching-category bonus exactly once.");
        }

        [Test]
        public void CategorySynergy_IdenticalValidTagWithResolver_AwardsOnce()
        {
            var previous = ResolvedClaim(
                "CLM-VALID-TAG-PREVIOUS", "species-a", "known-tag");
            var current = Claim(
                "CLM-VALID-TAG-CURRENT", species: "species-b");
            current.AnomalyTagIds = new[] { "known-tag" };
            var data = RunData(activeClaim: current);
            data.ResolvedClaims.Add(previous);
            var run = Controller(data);
            var shift = ShiftManagerWithTags(
                Tag("known-tag", "SharedCategory"));
            Present(current, data, Meta);

            var committed = CommitWithBonusRates(
                current, ClaimResolutionKind.Approve,
                run, Meta, shift.BonusRates);

            Assert.IsTrue(committed.Committed);
            Assert.AreEqual(15, data.CorporateCredits,
                "A known identical tag proves one shared authored category: " +
                "base 12 plus one +3 synergy.");
        }

        [Test]
        public void CategorySynergy_IdenticalUnknownTagWithResolver_DoesNotAward()
        {
            var previous = ResolvedClaim(
                "CLM-UNKNOWN-TAG-PREVIOUS", "species-a", "unknown-tag");
            var current = Claim(
                "CLM-UNKNOWN-TAG-CURRENT", species: "species-b");
            current.AnomalyTagIds = new[] { "unknown-tag" };
            var data = RunData(activeClaim: current);
            data.ResolvedClaims.Add(previous);
            var run = Controller(data);
            var shift = ShiftManagerWithTags();
            Present(current, data, Meta);

            var committed = CommitWithBonusRates(
                current, ClaimResolutionKind.Approve,
                run, Meta, shift.BonusRates);

            Assert.IsTrue(committed.Committed);
            Assert.AreEqual(12, data.CorporateCredits,
                "Identical unknown IDs do not prove that an authored category exists.");
            Assert.IsTrue(SaveSystem.LoadMeta().Encounters
                .IsCompleted(current.EncounterId),
                "An unresolved tag must not prevent the authoritative save.");
        }

        [Test]
        public void CategorySynergy_DistinctUnknownOrMalformedTags_DoNotAward()
        {
            var previous = ResolvedClaim(
                "CLM-UNKNOWN-DISTINCT-PREVIOUS",
                "species-a",
                "unknown-tag-a");
            previous.AnomalyTagIds =
                new[] { "unknown-tag-a", null, " " };
            var current = Claim(
                "CLM-UNKNOWN-DISTINCT-CURRENT", species: "species-b");
            current.AnomalyTagIds =
                new[] { "unknown-tag-b", string.Empty, "\t" };
            var data = RunData(activeClaim: current);
            data.ResolvedClaims.Add(previous);
            var run = Controller(data);
            var shift = ShiftManagerWithTags();
            Present(current, data, Meta);

            var committed = CommitWithBonusRates(
                current, ClaimResolutionKind.Approve,
                run, Meta, shift.BonusRates);

            Assert.IsTrue(committed.Committed);
            Assert.AreEqual(12, data.CorporateCredits);
            var loaded = SaveSystem.LoadRun();
            Assert.IsTrue(loaded.ActiveClaim.IsResolved);
            Assert.IsTrue(loaded.ActiveClaim.ConsequencesApplied);
        }

        [Test]
        public void CategorySynergy_MultipleQualifyingPairs_AwardOnlyOnce()
        {
            var previous = ResolvedClaim(
                "CLM-MULTI-PREVIOUS", "species-a", "tag-a1");
            previous.AnomalyTagIds = new[] { "tag-a1", "tag-a2" };
            var current = Claim(
                "CLM-MULTI-CURRENT", species: "species-b");
            current.AnomalyTagIds = new[] { "tag-b1", "tag-b2" };
            var data = RunData(activeClaim: current);
            data.ResolvedClaims.Add(previous);
            var run = Controller(data);
            var shift = ShiftManagerWithTags(
                Tag("tag-a1", "SharedCategory"),
                Tag("tag-a2", "SharedCategory"),
                Tag("tag-b1", "SharedCategory"),
                Tag("tag-b2", "SharedCategory"));
            Present(current, data, Meta);

            var committed = CommitWithBonusRates(
                current, ClaimResolutionKind.Approve,
                run, Meta, shift.BonusRates);

            Assert.IsTrue(committed.Committed);
            Assert.AreEqual(15, data.CorporateCredits,
                "Any number of qualifying pairs must produce one +3 synergy.");
        }

        [Test]
        public void CategorySynergy_AbsentResolver_DoesNotInferFromRawTagIdentity()
        {
            var previous = ResolvedClaim(
                "CLM-NO-RESOLVER-PREVIOUS", "species-a", "unproven-tag");
            var current = Claim(
                "CLM-NO-RESOLVER-CURRENT", species: "species-b");
            current.AnomalyTagIds = new[] { "unproven-tag" };
            var data = RunData(activeClaim: current);
            data.ResolvedClaims.Add(previous);
            var run = Controller(data);
            Present(current, data, Meta);

            var committed = CommitWithBonusRates(
                current, ClaimResolutionKind.Approve,
                run, Meta, new ClaimBonusRates(5, 3));

            Assert.IsTrue(committed.Committed);
            Assert.AreEqual(12, data.CorporateCredits,
                "Without an authoritative resolver, raw ID identity is not " +
                "evidence of an authored category.");
        }

        [Test]
        public void CategorySynergy_ThrowingResolver_FailsClosedAndStillSaves()
        {
            var previous = ResolvedClaim(
                "CLM-THROWING-PREVIOUS", "species-a", "tag-a");
            var current = Claim(
                "CLM-THROWING-CURRENT", species: "species-b");
            current.AnomalyTagIds = new[] { "tag-b" };
            var data = RunData(activeClaim: current);
            data.ResolvedClaims.Add(previous);
            var run = Controller(data);
            Present(current, data, Meta);
            var rates = new ClaimBonusRates(
                5, 3,
                _ => throw new InvalidOperationException("Malformed tag lookup."));

            CommitResult committed = default;
            Assert.DoesNotThrow(() =>
                committed = CommitWithBonusRates(
                    current, ClaimResolutionKind.Approve,
                    run, Meta, rates));

            Assert.IsTrue(committed.Committed);
            Assert.AreEqual(12, data.CorporateCredits);
            var loaded = SaveSystem.LoadRun();
            var loadedMeta = SaveSystem.LoadMeta();
            Assert.IsTrue(loaded.ActiveClaim.IsResolved);
            Assert.IsTrue(loaded.ActiveClaim.ConsequencesApplied);
            Assert.IsTrue(loadedMeta.Encounters.IsCompleted(current.EncounterId));
            Assert.AreEqual(1,
                loadedMeta.GetTotalVisits(current.ClientVariantId));
        }

        [Test]
        public void DeferredClaimIdGuard_StaleEventCannotReapplyCurrentClaimBonuses()
        {
            SeedEngine.Init(7001);
            var previous = Claim("CLM-PREVIOUS", species: "same_species");
            previous.IsResolved = true;
            previous.ResolutionKind = ClaimResolutionKind.Approve;

            var current = Claim("CLM-CURRENT", species: "same_species");
            var data = RunData(activeClaim: current);
            data.CurrentPhase = ShiftPhase.ClockIn;
            data.QuotaForCurrentAnte = 99;
            data.ResolvedClaims.Add(previous);

            var manager = GameManagerWithRun(data);
            var shift = ShiftManagerWithTide();
            Present(current, data, Meta);
            var result = Commit(
                current, ClaimResolutionKind.Approve, manager.Run, Meta);

            InvokeHandleClaimResolved(shift, new ClaimResolvedEvent(result.Applied));
            int creditsAfterCurrent = data.CorporateCredits;
            int memosAfterCurrent = data.GeneratedMemoIds.Count;

            InvokeHandleClaimResolved(shift, new ClaimResolvedEvent(result.Applied));
            Assert.AreEqual(creditsAfterCurrent, data.CorporateCredits,
                "A duplicate callback for the committed ClaimId has no " +
                "persistent consequence authority.");
            Assert.AreEqual(memosAfterCurrent, data.GeneratedMemoIds.Count);

            var stalePreviousResult = new AppliedClaimResolution(
                previous.ClaimId,
                previous.ClientVariantId,
                previous.ClientSpeciesId,
                ClaimResolutionKind.Approve,
                creditsDelta: 0,
                sanityDelta: 0f,
                soulIntegrityDelta: 0f,
                darkIntelligenceDelta: 0,
                quotaBefore: 0,
                quotaAfter: 1,
                quotaRequired: 99,
                complianceStreakBefore: 1f,
                complianceStreakAfter: 1f);
            InvokeHandleClaimResolved(
                shift, new ClaimResolvedEvent(stalePreviousResult));

            Assert.AreEqual(creditsAfterCurrent, data.CorporateCredits,
                "A stale event for another ClaimId must not reapply the " +
                "latest claim's cross-claim bonus.");
            Assert.AreEqual(memosAfterCurrent, data.GeneratedMemoIds.Count);
            Assert.AreEqual(2, data.ResolvedClaims.Count);
        }

        [Test]
        public void ReloadBeforeDeferredFlow_PreservesEarnedCrossClaimBonusExactlyOnce()
        {
            var previous = Claim("CLM-PREVIOUS", species: "same_species");
            previous.IsResolved = true;
            previous.ResolutionKind = ClaimResolutionKind.Approve;

            var current = Claim("CLM-CURRENT", species: "same_species");
            var data = RunData(activeClaim: current);
            data.CurrentPhase = ShiftPhase.MorningBlock;
            data.QuotaForCurrentAnte = 99;
            data.ResolvedClaims.Add(previous);
            var run = Controller(data);
            Present(current, data, Meta);

            var committed = Commit(
                current, ClaimResolutionKind.Approve, run, Meta);
            int authoritativeCredits = committed.Applied.CreditsDelta;

            // Simulate process loss after the authoritative save but before
            // the deferred ClaimResolvedEvent reaches ShiftManager.
            var loaded = SaveSystem.LoadRun();
            loaded.PendingClaims.Add(Claim("CLM-NEXT", species: "other_species"));
            var manager = GameManagerWithRun(loaded);
            var shift = ShiftManagerWithTide();
            InvokeStart(shift);

            Assert.AreEqual(
                authoritativeCredits + 5,
                loaded.CorporateCredits,
                "The already-earned previous/current species bonus must be " +
                "present exactly once after crash-boundary resume.");
            Assert.IsNotNull(loaded.ActiveClaim,
                "Flow should advance to the next legitimate encounter.");
            Assert.AreEqual("CLM-NEXT", loaded.ActiveClaim.ClaimId);
            Assert.AreEqual(2, loaded.ResolvedClaims.Count);
            Assert.AreEqual(1,
                Meta.GetTotalVisits(current.ClientVariantId));
        }

        [Test]
        public void ConsequencesApplied_PersistsAcrossReloadAndIsScopedPerEncounter()
        {
            SeedEngine.Init(7101);
            var previous = ResolvedClaim(
                "CLM-CONSEQUENCE-PREVIOUS", "same-species", "shared-tag");
            var current = Claim(
                "CLM-CONSEQUENCE-CURRENT",
                "consequence-current",
                "same-species");
            current.AnomalyTagIds = new[] { "shared-tag" };
            var data = RunData(activeClaim: current);
            data.ResolvedClaims.Add(previous);
            var run = Controller(data);
            var shift = ShiftManagerWithTags(
                Tag("shared-tag", "SharedCategory"));
            Present(current, data, Meta);

            var committed = CommitWithBonusRates(
                current, ClaimResolutionKind.Approve,
                run, Meta, shift.BonusRates);
            int creditsAfterCommit = data.CorporateCredits;
            int memosAfterCommit = data.GeneratedMemoIds.Count;

            Assert.IsTrue(committed.Committed);
            Assert.AreEqual(20, creditsAfterCommit,
                "The committed encounter should receive base 12 plus +5/+3 once.");
            Assert.IsTrue(current.ConsequencesApplied);

            var loaded = SaveSystem.LoadRun();
            var loadedMeta = SaveSystem.LoadMeta();
            Assert.IsTrue(loaded.ActiveClaim.ConsequencesApplied,
                "The resolved ActiveClaim copy must persist the exact-once marker.");
            Assert.IsTrue(loaded.ResolvedClaims[^1].ConsequencesApplied,
                "The resolved-history copy must persist the exact-once marker.");

            var resumed = Controller(loaded);
            var duplicate = CommitWithBonusRates(
                loaded.ActiveClaim, ClaimResolutionKind.Approve,
                resumed, loadedMeta, shift.BonusRates);

            Assert.IsFalse(duplicate.Committed);
            Assert.AreEqual(CommitRejection.AlreadyCommitted, duplicate.Rejection);
            Assert.AreEqual(creditsAfterCommit, loaded.CorporateCredits);
            Assert.AreEqual(memosAfterCommit, loaded.GeneratedMemoIds.Count);

            loaded.ActiveClaim = null;
            var next = Claim(
                "CLM-CONSEQUENCE-NEXT",
                "consequence-next",
                "same-species");
            next.AnomalyTagIds = new[] { "shared-tag" };
            loaded.ActiveClaim = next;
            Present(next, loaded, loadedMeta);

            Assert.IsFalse(next.ConsequencesApplied,
                "A new encounter must not inherit the previous claim's marker.");

            int beforeNext = loaded.CorporateCredits;
            var nextCommit = CommitWithBonusRates(
                next, ClaimResolutionKind.Deny,
                resumed, loadedMeta, shift.BonusRates);

            Assert.IsTrue(nextCommit.Committed);
            Assert.IsTrue(next.ConsequencesApplied);
            Assert.AreEqual(beforeNext + 8, loaded.CorporateCredits,
                "The legitimate next encounter must apply its own +5/+3 consequences.");
        }

        [Test]
        public void ConsequencesApplied_LegacyFalseDoesNotReplayHistoricalConsequences()
        {
            SeedEngine.Init(7102);
            var legacyResolved = Claim(
                "CLM-LEGACY-RESOLVED",
                "legacy-resolved",
                "legacy-species");
            legacyResolved.AnomalyTagIds = new[] { "legacy-tag" };
            legacyResolved.IsResolved = true;
            legacyResolved.ResolutionKind = ClaimResolutionKind.Approve;
            Assert.IsFalse(legacyResolved.ConsequencesApplied,
                "This models the additive default in an older save.");

            var current = Claim(
                "CLM-POST-LEGACY",
                "post-legacy",
                "different-species");
            current.AnomalyTagIds = new[] { "different-tag" };
            var data = RunData(activeClaim: current);
            data.ResolvedClaims.Add(legacyResolved);
            var run = Controller(data);
            Present(current, data, Meta);

            var committed = Commit(
                current, ClaimResolutionKind.Deny, run, Meta);

            Assert.IsTrue(committed.Committed);
            Assert.AreEqual(0, data.CorporateCredits,
                "Loading an old false marker must not replay an old claim's bonus.");
            Assert.IsFalse(legacyResolved.ConsequencesApplied,
                "The historical claim must not be retroactively reprocessed.");
            Assert.IsTrue(current.ConsequencesApplied,
                "Only the newly committed encounter owns this transaction.");

            var loaded = SaveSystem.LoadRun();
            Assert.IsFalse(loaded.ResolvedClaims[0].ConsequencesApplied);
            Assert.IsTrue(loaded.ResolvedClaims[1].ConsequencesApplied);
        }

        [Test]
        public void MemoState_IsDurableBeforePresentationAndDuplicateSafe()
        {
            SeedEngine.Init(7103);
            var claim = Claim(
                "CLM-MEMO-EXACT-ONCE",
                "memo-claimant",
                "anomalous_adjacent");
            claim.AnomalyTagIds = new[]
            {
                "memo-tag-1", "memo-tag-2", "memo-tag-3", "memo-tag-4"
            };
            var data = RunData(activeClaim: claim);
            var run = Controller(data);
            Present(claim, data, Meta);

            Action<MemoGeneratedEvent> losePresentation =
                _ => throw new InvalidOperationException(
                    "Simulated process loss after durable save.");
            RumorMill.OnMemoGenerated += losePresentation;

            Assert.Throws<InvalidOperationException>(() =>
                Commit(claim, ClaimResolutionKind.Approve, run, Meta));

            RumorMill.OnMemoGenerated -= losePresentation;
            int creditsAfterCommit = data.CorporateCredits;
            var immediateDuplicate = Commit(
                claim, ClaimResolutionKind.Approve, run, Meta);
            Assert.IsFalse(immediateDuplicate.Committed);
            Assert.AreEqual(
                CommitRejection.AlreadyCommitted,
                immediateDuplicate.Rejection);
            Assert.AreEqual(creditsAfterCommit, data.CorporateCredits);
            Assert.AreEqual(1, data.GeneratedMemoIds.Count);
            Assert.AreEqual(1, Meta.ConspiracyBoard.Fragments.Count);

            // The event is after PersistQuietly. Even though presentation was
            // interrupted, both halves of persistent memo state must reload.
            var loaded = SaveSystem.LoadRun();
            var loadedMeta = SaveSystem.LoadMeta();
            Assert.AreEqual(1, loaded.GeneratedMemoIds.Count);
            Assert.AreEqual(1, loadedMeta.ConspiracyBoard.Fragments.Count);
            Assert.AreEqual(
                loaded.GeneratedMemoIds[0],
                loadedMeta.ConspiracyBoard.Fragments[0].FragmentId);
            Assert.IsTrue(loaded.ActiveClaim.ConsequencesApplied);

            var resumed = Controller(loaded);
            int creditsBeforeDuplicate = loaded.CorporateCredits;
            var duplicate = Commit(
                loaded.ActiveClaim, ClaimResolutionKind.Approve,
                resumed, loadedMeta);

            Assert.IsFalse(duplicate.Committed);
            Assert.AreEqual(CommitRejection.AlreadyCommitted, duplicate.Rejection);
            Assert.AreEqual(creditsBeforeDuplicate, loaded.CorporateCredits);
            Assert.AreEqual(1, loaded.GeneratedMemoIds.Count);
            Assert.AreEqual(1, loadedMeta.ConspiracyBoard.Fragments.Count);
        }

        private GameManager GameManagerWithRun(RunData data)
        {
            if (GameManager.Instance != null)
                UnityEngine.Object.DestroyImmediate(
                    GameManager.Instance.gameObject);
            GameManagerInstanceField?.SetValue(null, null);

            var host = new GameObject(
                $"Bucket1_Final_GameManager_{_ownedObjects.Count + 1}");
            host.SetActive(false);
            var manager = host.AddComponent<GameManager>();
            _ownedObjects.Add(host);

            var run = Controller(data);
            GameManagerRunField.SetValue(manager, run);
            GameManagerInstanceField.SetValue(null, manager);
            manager.SetMetaForTesting(Meta);
            return manager;
        }

        private ShiftManager ShiftManagerWithTide()
        {
            var shift = OwnedComponent<ShiftManager>("ShiftManager");
            var tuning = ScriptableObject.CreateInstance<TideTuningData>();
            _ownedObjects.Add(tuning);

            TideTuningField.SetValue(shift, tuning);
            var tide = new TideSystem(tuning);
            tide.Initialize(1);
            TideField.SetValue(shift, tide);
            return shift;
        }

        private ShiftManager ShiftManagerWithTags(params AnomalyTagData[] tags)
        {
            var shift = ShiftManagerWithTide();
            AnomalyTagsField.SetValue(shift, tags);
            return shift;
        }

        private AnomalyTagData Tag(string id, string category)
        {
            var tag = ScriptableObject.CreateInstance<AnomalyTagData>();
            tag.TagId = id;
            tag.TagCategory = category;
            _ownedObjects.Add(tag);
            return tag;
        }

        private T OwnedComponent<T>(string label) where T : Component
        {
            var host = new GameObject(
                $"Bucket1_Final_{label}_{_ownedObjects.Count + 1}");
            _ownedObjects.Add(host);
            return host.AddComponent<T>();
        }

        private static ActiveClaimData ResolvedClaim(
            string claimId,
            string species,
            string anomalyTagId)
        {
            var claim = Claim(claimId, species: species);
            claim.AnomalyTagIds = new[] { anomalyTagId };
            claim.IsResolved = true;
            claim.ResolutionKind = ClaimResolutionKind.Approve;
            claim.ConsequencesApplied = true;
            return claim;
        }

        private static CommitResult CommitWithBonusRates(
            ActiveClaimData claim,
            ClaimResolutionKind kind,
            RunStateController run,
            MetaProgressData meta,
            ClaimBonusRates bonusRates)
        {
            ClaimResolutionOutcome outcome = kind switch
            {
                ClaimResolutionKind.Approve => new ClaimResolutionOutcome(
                    kind, creditsEarned: 12, sanityCost: 3f, soulCost: 1f),
                ClaimResolutionKind.Deny => new ClaimResolutionOutcome(
                    kind, creditsEarned: 0, sanityCost: 3f, soulCost: 0f),
                ClaimResolutionKind.Liquify =>
                    ClaimResolutionConsequencePolicy.Liquify(),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(kind), kind, null),
            };
            return EncounterCommitService.CommitEncounterResult(
                claim, outcome, run, meta,
                proof: null, eliasContent: null, bonusRates);
        }

        private static void InvokeHandleClaimResolved(
            ShiftManager shift,
            ClaimResolvedEvent evt)
        {
            Assert.IsNotNull(HandleClaimResolved);
            HandleClaimResolved.Invoke(shift, new object[] { evt });
        }

        private static void InvokeStart(ShiftManager shift)
        {
            Assert.IsNotNull(StartShiftManager);
            StartShiftManager.Invoke(shift, null);
        }
    }
}
