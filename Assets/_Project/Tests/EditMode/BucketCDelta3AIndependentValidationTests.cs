using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Desk42.Claims;
using Desk42.Core;
using Desk42.Encounter;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Independent Delta 3A probes through the real interruption/commit/save
    /// seams and ShiftManager's actual restoration method.
    /// </summary>
    public sealed class BucketCDelta3AIndependentValidationTests
        : Bucket1PersistenceFixture
    {
        private const BindingFlags InstancePrivate =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate =
            BindingFlags.Static | BindingFlags.NonPublic;

        private static readonly MethodInfo RestoreCarried =
            typeof(ShiftManager).GetMethod(
                "RestoreCarriedEncounters", InstancePrivate);
        private static readonly MethodInfo StartShiftManager =
            typeof(ShiftManager).GetMethod("Start", InstancePrivate);
        private static readonly MethodInfo GenerateInitialQueue =
            typeof(ShiftManager).GetMethod(
                "GenerateInitialQueue", InstancePrivate);
        private static readonly FieldInfo ClaimTemplatesField =
            typeof(ShiftManager).GetField("_claimTemplates", InstancePrivate);
        private static readonly FieldInfo AnomalyTagsField =
            typeof(ShiftManager).GetField("_anomalyTags", InstancePrivate);
        private static readonly FieldInfo TideTuningField =
            typeof(ShiftManager).GetField("_tideTuning", InstancePrivate);
        private static readonly FieldInfo GameManagerInstanceField =
            typeof(GameManager).GetField(
                "<Instance>k__BackingField", StaticPrivate);
        private static readonly FieldInfo GameManagerRunField =
            typeof(GameManager).GetField(
                "<Run>k__BackingField", InstancePrivate);
        private static readonly FieldInfo CarryRecordsField =
            typeof(CarriedEncounterLedger).GetField(
                "_records", InstancePrivate);

        private readonly List<UnityEngine.Object> _owned = new();

        [SetUp]
        public void ValidateDelta3AReflectionSeams()
        {
            Assert.IsNotNull(RestoreCarried);
            Assert.IsNotNull(StartShiftManager);
            Assert.IsNotNull(GenerateInitialQueue);
            Assert.IsNotNull(ClaimTemplatesField);
            Assert.IsNotNull(AnomalyTagsField);
            Assert.IsNotNull(TideTuningField);
            Assert.IsNotNull(GameManagerInstanceField);
            Assert.IsNotNull(GameManagerRunField);
            Assert.IsNotNull(CarryRecordsField);
            GameManagerInstanceField.SetValue(null, null);
        }

        [TearDown]
        public void TearDownDelta3AObjects()
        {
            GameManagerInstanceField?.SetValue(null, null);
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] != null)
                    UnityEngine.Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear();
        }

        [Test]
        public void Interrupt_IsDurableMetaOwned_AndCommitsNoTerminalEffects()
        {
            ActiveClaimData claim = DetailedClaim(
                "CLM-INTERRUPT", "moth_347", "unresolved payload");
            RunData oldRun = RunData("DELTA3A-OLD-RUN", 1, claim);
            Present(claim, oldRun, Meta);
            string encounterId = claim.EncounterId;
            string runInstanceId = oldRun.RunInstanceId;
            int creditsBefore = oldRun.CorporateCredits;

            Assert.AreEqual(EncounterStatus.Active,
                Meta.Encounters.StatusOf(encounterId, encounterId));
            Assert.IsTrue(EncounterCommitService.InterruptEncounter(
                claim, oldRun, Meta));

            Assert.AreEqual(EncounterStatus.Interrupted,
                Meta.Encounters.StatusOf(encounterId, null));
            Assert.AreEqual(1, Meta.CarriedEncounters.Count);
            Assert.AreEqual(0, Meta.GetTotalVisits(claim.ClientVariantId));
            Assert.IsEmpty(
                Meta.Encounters.CommittedDispositionsFor(claim.ClientVariantId));
            Assert.IsFalse(
                ApprovalLiabilityPolicy.HasApprovalLiability(Meta, encounterId));
            Assert.IsFalse(claim.IsResolved);
            Assert.IsFalse(claim.ConsequencesApplied);
            Assert.AreEqual(creditsBefore, oldRun.CorporateCredits);
            Assert.IsEmpty(oldRun.ResolvedClaims);

            MetaProgressData loadedMeta = SaveSystem.LoadMeta();
            RunData loadedOldRun = SaveSystem.LoadRun();
            CarriedEncounterRecord carried =
                loadedMeta.CarriedEncounters.Find(encounterId);

            Assert.IsNotNull(carried);
            Assert.AreEqual(encounterId, carried.Claim.EncounterId);
            Assert.AreEqual("moth_347", carried.Claim.ClientVariantId);
            Assert.AreEqual("unresolved payload", carried.Claim.IncidentText);
            Assert.AreEqual(runInstanceId, loadedOldRun.RunInstanceId);

            var replacementRun = RunData("DELTA3A-NEW-RUN", 2);
            Assert.AreNotEqual(runInstanceId, replacementRun.RunInstanceId,
                "A replacement RunData has its own run namespace.");
            Assert.IsTrue(loadedMeta.CarriedEncounters.Has(encounterId),
                "Meta ownership must survive replacement of old RunData.");
        }

        [Test]
        public void ShiftManager_Start_GeneratesFreshQueueThenRestoresCarriedAtFrontOnce()
        {
            ActiveClaimData carriedClaim = DetailedClaim(
                "CLM-CARRY-QUEUE", "procedural_347", "carry me");
            RunData oldRun = RunData("DELTA3A-QUEUE-OLD", 1, carriedClaim);
            Present(carriedClaim, oldRun, Meta);
            string encounterId = carriedClaim.EncounterId;
            Assert.IsTrue(EncounterCommitService.InterruptEncounter(
                carriedClaim, oldRun, Meta));

            RunData nextRun = RunData("DELTA3A-QUEUE-NEXT", 2);
            nextRun.CurrentPhase = ShiftPhase.ClockIn;
            RunStateController controller = Controller(nextRun);
            ConfigureGameManager(Meta, controller);
            ShiftManager shift = ConfiguredShiftManager("NextShift");

            StartShiftManager.Invoke(shift, null);

            Assert.AreSame(carriedClaim, nextRun.ActiveClaim,
                "Front insertion should make outstanding work the next active claim.");
            Assert.AreEqual(encounterId, nextRun.ActiveClaim.EncounterId);
            Assert.AreEqual("procedural_347", nextRun.ActiveClaim.ClientVariantId);
            Assert.AreEqual("carry me", nextRun.ActiveClaim.IncidentText);
            Assert.AreEqual(1, CountEncounter(nextRun, encounterId));
            Assert.IsNotEmpty(nextRun.PendingClaims,
                "Fresh generated claims must remain after carried work is restored.");
            Assert.IsTrue(nextRun.PendingClaims.All(
                c => c.EncounterId != encounterId));

            int normalClaimsBeforeRegeneration =
                nextRun.PendingClaims.Count;
            GenerateInitialQueue.Invoke(
                shift, new object[] { nextRun.ShiftNumber, nextRun });
            RestoreCarried.Invoke(
                shift, new object[] { nextRun });
            Assert.AreEqual(1, CountEncounter(nextRun, encounterId),
                "Actual repeated queue generation must not duplicate carried X.");
            Assert.Greater(nextRun.PendingClaims.Count,
                normalClaimsBeforeRegeneration,
                "Repeated generation should add fresh work without replacing X.");

            int pendingBefore = nextRun.PendingClaims.Count;
            StartShiftManager.Invoke(shift, null);
            Assert.AreEqual(1, CountEncounter(nextRun, encounterId));
            Assert.AreEqual(pendingBefore, nextRun.PendingClaims.Count,
                "Repeated Start/queue setup must not duplicate carried work.");
        }

        [Test]
        public void Restore_DedupesActiveAndPendingByEncounterId_NotClaimant()
        {
            ActiveClaimData x = DetailedClaim(
                "CLM-DEDUPE-X", "same_claimant", "X");
            RunData oldRun = RunData("DELTA3A-DEDUPE-OLD", 1, x);
            Present(x, oldRun, Meta);
            Assert.IsTrue(EncounterCommitService.InterruptEncounter(
                x, oldRun, Meta));
            string xId = x.EncounterId;

            ActiveClaimData y = DetailedClaim(
                "CLM-DEDUPE-Y", "same_claimant", "Y");
            RunData identityRun = RunData("DELTA3A-DEDUPE-Y", 2);
            Present(y, identityRun, Meta);
            Assert.AreNotEqual(xId, y.EncounterId);

            RunData activeCase = RunData(
                "DELTA3A-ACTIVE-DEDUPE", 2, x);
            ShiftManager shift = RestorationShift(Meta, activeCase, "Active");
            RestoreCarried.Invoke(shift, new object[] { activeCase });
            Assert.AreEqual(1, CountEncounter(activeCase, xId));
            Assert.IsEmpty(activeCase.PendingClaims);

            RunData pendingCase = RunData("DELTA3A-PENDING-DEDUPE", 2);
            pendingCase.PendingClaims.Add(x);
            shift = RestorationShift(Meta, pendingCase, "Pending");
            RestoreCarried.Invoke(shift, new object[] { pendingCase });
            Assert.AreEqual(1, CountEncounter(pendingCase, xId));

            RunData sameClaimantCase = RunData("DELTA3A-SAME-CLAIMANT", 2);
            sameClaimantCase.PendingClaims.Add(y);
            shift = RestorationShift(Meta, sameClaimantCase, "SameClaimant");
            RestoreCarried.Invoke(
                shift, new object[] { sameClaimantCase });

            Assert.AreEqual(1, CountEncounter(sameClaimantCase, xId));
            Assert.AreEqual(1,
                CountEncounter(sameClaimantCase, y.EncounterId));
            Assert.AreSame(x, sameClaimantCase.PendingClaims[0],
                "Carried work is inserted at the front without replacing Y.");
            Assert.AreSame(y, sameClaimantCase.PendingClaims[1]);
        }

        [Test]
        public void QueuedAndResumedActive_RestartsKeepOneOriginalEncounter()
        {
            ActiveClaimData x = DetailedClaim(
                "CLM-RESTART-X", "restart_347", "restart payload");
            RunData oldRun = RunData("DELTA3A-RESTART-OLD", 1, x);
            Present(x, oldRun, Meta);
            Assert.IsTrue(EncounterCommitService.InterruptEncounter(
                x, oldRun, Meta));
            string encounterId = x.EncounterId;

            RunData nextRun = RunData("DELTA3A-RESTART-NEXT", 2);
            nextRun.PendingClaims.Add(x);
            Assert.IsTrue(SaveSystem.SaveRun(nextRun));
            Assert.IsTrue(SaveSystem.SaveMeta(Meta));

            MetaProgressData loadedMeta = SaveSystem.LoadMeta();
            RunData queuedReload = SaveSystem.LoadRun();
            ShiftManager shift =
                RestorationShift(loadedMeta, queuedReload, "QueuedReload");
            RestoreCarried.Invoke(shift, new object[] { queuedReload });
            Assert.AreEqual(1, CountEncounter(queuedReload, encounterId));
            Assert.AreEqual(0,
                loadedMeta.GetTotalVisits(x.ClientVariantId));

            queuedReload.ActiveClaim = queuedReload.PendingClaims[0];
            queuedReload.PendingClaims.Clear();
            Assert.IsTrue(SaveSystem.SaveRun(queuedReload));
            Assert.IsTrue(SaveSystem.SaveMeta(loadedMeta));

            MetaProgressData activeMeta = SaveSystem.LoadMeta();
            RunData activeReload = SaveSystem.LoadRun();
            shift = RestorationShift(activeMeta, activeReload, "ActiveReload");
            RestoreCarried.Invoke(shift, new object[] { activeReload });

            Assert.AreEqual(1, CountEncounter(activeReload, encounterId));
            Assert.AreEqual(encounterId, activeReload.ActiveClaim.EncounterId);
            Assert.AreEqual("restart_347",
                activeReload.ActiveClaim.ClientVariantId);
            Assert.AreEqual(0,
                activeMeta.GetTotalVisits(x.ClientVariantId));
            Assert.AreEqual(1, activeMeta.Encounters.Count);
        }

        [Test]
        public void RepeatedInterruption_PreservesOneIdentityAndCountsRealTransitions()
        {
            ActiveClaimData x = DetailedClaim(
                "CLM-REINTERRUPT", "repeat_347", "repeat payload");
            RunData firstRun = RunData("DELTA3A-REPEAT-1", 1, x);
            Present(x, firstRun, Meta);
            string encounterId = x.EncounterId;

            Assert.IsTrue(EncounterCommitService.InterruptEncounter(
                x, firstRun, Meta));
            RunData secondRun = RunData("DELTA3A-REPEAT-2", 2, x);
            Present(x, secondRun, Meta);
            Assert.IsTrue(EncounterCommitService.InterruptEncounter(
                x, secondRun, Meta));

            CarriedEncounterRecord carried =
                Meta.CarriedEncounters.Find(encounterId);
            Assert.AreEqual(1, Meta.CarriedEncounters.Count);
            Assert.AreEqual(2, carried.InterruptCount);
            Assert.AreEqual(encounterId, carried.Claim.EncounterId);
            Assert.AreEqual("repeat_347", carried.Claim.ClientVariantId);
            Assert.AreEqual(1, Meta.Encounters.Count);
            Assert.AreEqual(0, Meta.GetTotalVisits("repeat_347"));
            Assert.IsFalse(
                ApprovalLiabilityPolicy.HasApprovalLiability(
                    Meta, encounterId));
        }

        [TestCase(ClaimResolutionKind.Approve, true)]
        [TestCase(ClaimResolutionKind.Deny, false)]
        [TestCase(ClaimResolutionKind.Liquify, false)]
        public void TerminalResolutionAfterResume_IsExactOnceAndReleasesCarry(
            ClaimResolutionKind disposition,
            bool expectLiability)
        {
            ActiveClaimData x = DetailedClaim(
                $"CLM-TERMINAL-{disposition}",
                $"terminal_{disposition}",
                $"terminal payload {disposition}");
            RunData firstRun = RunData(
                $"DELTA3A-TERMINAL-OLD-{disposition}", 1, x);
            Present(x, firstRun, Meta);
            string encounterId = x.EncounterId;
            Assert.IsTrue(EncounterCommitService.InterruptEncounter(
                x, firstRun, Meta));

            RunData resumedRun = RunData(
                $"DELTA3A-TERMINAL-NEW-{disposition}", 2, x);
            RunStateController controller = Controller(resumedRun);
            Present(x, resumedRun, Meta);
            Assert.AreEqual(EncounterStatus.Active,
                Meta.Encounters.StatusOf(encounterId, encounterId));

            CommitResult committed = Commit(
                x, disposition, controller, Meta);
            CommitResult duplicate = Commit(
                x, ClaimResolutionKind.Approve, controller, Meta);

            Assert.IsTrue(committed.Committed);
            Assert.IsFalse(duplicate.Committed);
            Assert.AreEqual(
                CommitRejection.AlreadyCommitted, duplicate.Rejection);
            Assert.AreEqual(encounterId, committed.EncounterId);
            Assert.AreEqual(EncounterStatus.Completed,
                Meta.Encounters.StatusOf(encounterId, null));
            Assert.AreEqual(1, Meta.Encounters.Count);
            Assert.AreEqual(1, Meta.GetTotalVisits(x.ClientVariantId));
            Assert.AreEqual(1,
                Meta.Encounters.CommittedDispositionsFor(
                    x.ClientVariantId).Count);
            Assert.AreEqual(disposition,
                Meta.Encounters.Find(encounterId).Outcome);
            Assert.AreEqual(0, Meta.CarriedEncounters.Count);
            Assert.AreEqual(expectLiability,
                ApprovalLiabilityPolicy.HasApprovalLiability(
                    Meta, encounterId));
            Assert.IsTrue(x.ConsequencesApplied);

            MetaProgressData loaded = SaveSystem.LoadMeta();
            Assert.AreEqual(0, loaded.CarriedEncounters.Count);
            Assert.AreEqual(1,
                loaded.GetTotalVisits(x.ClientVariantId));
            Assert.AreEqual(expectLiability,
                ApprovalLiabilityPolicy.HasApprovalLiability(
                    loaded, encounterId));
        }

        [Test]
        public void MalformedDuplicateAndMissingPayload_RestoreSafelyWithFirstPayload()
        {
            ActiveClaimData canonical = DetailedClaim(
                "CLM-CANONICAL-CARRY", "canonical_claimant", "canonical");
            RunData oldRun = RunData("DELTA3A-MALFORMED", 1, canonical);
            Present(canonical, oldRun, Meta);
            Assert.IsTrue(EncounterCommitService.InterruptEncounter(
                canonical, oldRun, Meta));

            List<CarriedEncounterRecord> raw =
                (List<CarriedEncounterRecord>)
                CarryRecordsField.GetValue(Meta.CarriedEncounters);
            raw.Add(new CarriedEncounterRecord
            {
                EncounterId = canonical.EncounterId,
                ClientVariantId = "duplicate_claimant",
                Claim = DetailedClaim(
                    "CLM-DUPLICATE-CARRY",
                    "duplicate_claimant",
                    "duplicate"),
                InterruptCount = 99,
            });
            raw.Add(new CarriedEncounterRecord
            {
                EncounterId = "ENC-MISSING-PAYLOAD",
                ClientVariantId = "broken",
                Claim = null,
            });

            RunData nextRun = RunData("DELTA3A-MALFORMED-NEXT", 2);
            ShiftManager shift =
                RestorationShift(Meta, nextRun, "Malformed");
            Assert.DoesNotThrow(() =>
                RestoreCarried.Invoke(shift, new object[] { nextRun }));

            Assert.AreEqual(1,
                CountEncounter(nextRun, canonical.EncounterId));
            Assert.AreSame(canonical, nextRun.PendingClaims[0]);
            Assert.AreEqual("canonical", nextRun.PendingClaims[0].IncidentText);
            Assert.IsFalse(nextRun.PendingClaims.Exists(
                c => c?.EncounterId == "ENC-MISSING-PAYLOAD"));
            Assert.AreEqual(3, Meta.CarriedEncounters.Count,
                "Malformed physical rows remain non-destructively.");
        }

        [Test]
        public void CompletedStaleCarry_IsNeverQueuedAndIsReleased()
        {
            ActiveClaimData x = DetailedClaim(
                "CLM-STALE-CARRY", "stale_claimant", "stale");
            RunData oldRun = RunData("DELTA3A-STALE", 1, x);
            Present(x, oldRun, Meta);
            Assert.IsTrue(EncounterCommitService.InterruptEncounter(
                x, oldRun, Meta));
            Meta.Encounters.MarkCompleted(
                x.EncounterId, ClaimResolutionKind.Deny, 1L);

            RunData nextRun = RunData("DELTA3A-STALE-NEXT", 2);
            ShiftManager shift =
                RestorationShift(Meta, nextRun, "Stale");
            RestoreCarried.Invoke(shift, new object[] { nextRun });

            Assert.AreEqual(0, CountEncounter(nextRun, x.EncounterId));
            Assert.AreEqual(0, Meta.CarriedEncounters.Count);
            Assert.IsTrue(SaveSystem.SaveMeta(Meta));
            Assert.AreEqual(0,
                SaveSystem.LoadMeta().CarriedEncounters.Count);
        }

        [Test]
        public void LegacyMetaWithoutCarry_LoadsEmptyThenAcceptsNewInterruption()
        {
            var legacy = new MetaProgressData();
            JObject json = JObject.FromObject(legacy);
            json.Remove(nameof(MetaProgressData.CarriedEncounters));
            Directory.CreateDirectory(SaveDirectory);
            File.WriteAllText(
                Path.Combine(SaveDirectory, "meta.json"), json.ToString());

            MetaProgressData loaded = SaveSystem.LoadMeta();
            Assert.IsNotNull(loaded.CarriedEncounters);
            Assert.AreEqual(0, loaded.CarriedEncounters.Count);

            ActiveClaimData claim = DetailedClaim(
                "CLM-LEGACY-CARRY", "legacy_claimant", "legacy carry");
            RunData run = RunData("DELTA3A-LEGACY", 1, claim);
            Present(claim, run, loaded);
            Assert.IsTrue(EncounterCommitService.InterruptEncounter(
                claim, run, loaded));
            Assert.AreEqual(1,
                SaveSystem.LoadMeta().CarriedEncounters.Count);
        }

        private ShiftManager RestorationShift(
            MetaProgressData meta,
            RunData data,
            string label)
        {
            RunStateController run = Controller(data);
            ConfigureGameManager(meta, run);
            return ConfiguredShiftManager(label);
        }

        private GameManager ConfigureGameManager(
            MetaProgressData meta,
            RunStateController run)
        {
            GameManagerInstanceField.SetValue(null, null);
            var host = new GameObject($"Delta3A_GameManager_{_owned.Count}");
            host.SetActive(false);
            GameManager manager = host.AddComponent<GameManager>();
            _owned.Add(host);
            GameManagerInstanceField.SetValue(null, manager);
            GameManagerRunField.SetValue(manager, run);
            manager.SetMetaForTesting(meta);
            return manager;
        }

        private ShiftManager ConfiguredShiftManager(string label)
        {
            ShiftManager shift = Component<ShiftManager>(
                $"Delta3A_Shift_{label}");
            ClaimTemplatesField.SetValue(shift, new[] { Template() });
            AnomalyTagsField.SetValue(
                shift, Array.Empty<AnomalyTagData>());
            var tuning = ScriptableObject.CreateInstance<TideTuningData>();
            _owned.Add(tuning);
            TideTuningField.SetValue(shift, tuning);
            return shift;
        }

        private ClaimTemplateData Template()
        {
            var template =
                ScriptableObject.CreateInstance<ClaimTemplateData>();
            _owned.Add(template);
            template.TemplateId = $"delta3a_template_{_owned.Count}";
            template.SpeciesPool = new[] { "unregistered_alien" };
            template.ClaimantNamePool = new[] { "Fresh Claim" };
            template.DeptNamePool = new[] { "Generated" };
            template.IncidentTextVariants =
                new[] { "{claimant} at {dept} for {amount}." };
            template.AnomalyTagSlots = 0;
            template.ClaimAmountMin = 11;
            template.ClaimAmountMax = 11;
            template.MinShiftNumber = 1;
            template.SpawnWeight = 1f;
            return template;
        }

        private static ActiveClaimData DetailedClaim(
            string claimId,
            string variant,
            string incidentText)
        {
            ActiveClaimData claim = Claim(
                claimId, variant, "moth_accountant");
            claim.IncidentText = incidentText;
            claim.ClaimantName = $"Display {variant}";
            claim.ClaimAmount = 347;
            claim.AnomalyTagIds = new[] { "tag_preserved" };
            return claim;
        }

        private static int CountEncounter(RunData data, string encounterId)
        {
            int count = data.PendingClaims.Count(c =>
                c != null
                && string.Equals(
                    c.EncounterId, encounterId, StringComparison.Ordinal));
            if (data.ActiveClaim != null
                && string.Equals(
                    data.ActiveClaim.EncounterId,
                    encounterId,
                    StringComparison.Ordinal))
            {
                count++;
            }
            return count;
        }
    }
}
