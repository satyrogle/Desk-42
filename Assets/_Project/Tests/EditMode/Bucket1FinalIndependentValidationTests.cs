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

        private static readonly MethodInfo AwardCrossClaimBonus =
            typeof(ShiftManager).GetMethod(
                "AwardCrossClaimBonus", InstancePrivate);

        private static readonly MethodInfo AwardSequentialSynergyBonus =
            typeof(ShiftManager).GetMethod(
                "AwardSequentialSynergyBonus", InstancePrivate);

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
            var data = RunData();
            data.ResolvedClaims.Add(current);
            var run = Controller(data);
            var shift = ShiftManagerWithTags(
                Tag("tag-current", "CurrentCategory"));

            Assert.DoesNotThrow(() =>
            {
                InvokeBonus(
                    AwardCrossClaimBonus, shift, current, run, data);
                InvokeBonus(
                    AwardSequentialSynergyBonus, shift, current, run, data);
            });
            Assert.AreEqual(0, data.CorporateCredits,
                "A sole current claim must not be compared with itself.");
        }

        [Test]
        public void BonusIndex_ComparesActualPreviousClaimRatherThanCurrentClaim()
        {
            var previous = Claim("CLM-PREVIOUS", species: "species-a");
            previous.AnomalyTagIds = new[] { "tag-previous" };
            var current = Claim("CLM-CURRENT", species: "species-b");
            current.AnomalyTagIds = new[] { "tag-current" };

            var previousTag = Tag("tag-previous", "Category-A");
            var currentTag = Tag("tag-current", "Category-B");
            var data = RunData();
            data.ResolvedClaims.Add(previous);
            data.ResolvedClaims.Add(current);
            var run = Controller(data);
            var shift = ShiftManagerWithTags(previousTag, currentTag);

            InvokeBonus(AwardCrossClaimBonus, shift, current, run, data);
            InvokeBonus(AwardSequentialSynergyBonus, shift, current, run, data);
            Assert.AreEqual(0, data.CorporateCredits,
                "Different previous/current claims must not earn a self-match bonus.");

            previous.ClientSpeciesId = current.ClientSpeciesId;
            previousTag.TagCategory = currentTag.TagCategory;
            InvokeBonus(AwardCrossClaimBonus, shift, current, run, data);
            InvokeBonus(AwardSequentialSynergyBonus, shift, current, run, data);

            Assert.AreEqual(8, data.CorporateCredits,
                "The actual previous claim should award +5 species and +3 category.");
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

        private static void InvokeBonus(
            MethodInfo method,
            ShiftManager shift,
            ActiveClaimData current,
            RunStateController run,
            RunData data)
        {
            Assert.IsNotNull(method);
            method.Invoke(shift, new object[] { current, run, data });
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
