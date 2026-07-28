using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Desk42.Claims;
using Desk42.Core;
using Desk42.Encounter;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Independent CΔ1 probes. These deliberately exercise the live
    /// ShiftManager queue-generation call site and the authoritative encounter
    /// commit/save path rather than relying only on the pure scheduling helper
    /// or direct EncounterHistory mutation.
    /// </summary>
    public sealed class BucketCDelta1IndependentValidationTests
        : Bucket1PersistenceFixture
    {
        private const BindingFlags InstancePrivate =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate =
            BindingFlags.Static | BindingFlags.NonPublic;

        private static readonly MethodInfo GenerateInitialQueue =
            typeof(ShiftManager).GetMethod(
                "GenerateInitialQueue", InstancePrivate);
        private static readonly MethodInfo StartShiftManager =
            typeof(ShiftManager).GetMethod("Start", InstancePrivate);

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
        private static readonly FieldInfo GameManagerProofField =
            typeof(GameManager).GetField(
                "<EliasProof>k__BackingField", InstancePrivate);
        private static readonly FieldInfo EliasContentField =
            typeof(GameManager).GetField("_eliasProofContent", InstancePrivate);

        private readonly List<UnityEngine.Object> _owned = new();

        public enum ProofLifecycle
        {
            NeverStarted,
            Inactive,
            Active,
            Archived,
            NoProofObject,
            NoGameManager,
        }

        [SetUp]
        public void ValidateReflectionSeams()
        {
            Assert.IsNotNull(GenerateInitialQueue);
            Assert.IsNotNull(StartShiftManager);
            Assert.IsNotNull(ClaimTemplatesField);
            Assert.IsNotNull(AnomalyTagsField);
            Assert.IsNotNull(TideTuningField);
            Assert.IsNotNull(GameManagerInstanceField);
            Assert.IsNotNull(GameManagerRunField);
            Assert.IsNotNull(GameManagerProofField);
            Assert.IsNotNull(EliasContentField);

            GameManagerInstanceField.SetValue(null, null);
        }

        [TearDown]
        public void TearDownDelta1ValidationObjects()
        {
            GameManagerInstanceField?.SetValue(null, null);

            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] != null)
                    UnityEngine.Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear();
        }

        [TestCase(ProofLifecycle.NeverStarted)]
        [TestCase(ProofLifecycle.Inactive)]
        [TestCase(ProofLifecycle.Active)]
        [TestCase(ProofLifecycle.Archived)]
        [TestCase(ProofLifecycle.NoProofObject)]
        [TestCase(ProofLifecycle.NoGameManager)]
        public void ShiftManager_ActuallySchedulesMara_RegardlessOfProofLifecycle(
            ProofLifecycle lifecycle)
        {
            ConfigureGameManager(lifecycle);
            var runData = RunData(seed: $"MARA-{lifecycle}", shift: 3);

            InvokeGenerateInitialQueue(
                ConfiguredShiftManager($"Mara_{lifecycle}"), 3, runData);

            Assert.AreEqual(1, CountMara(runData),
                $"Live queue generation did not schedule exactly one Mara for {lifecycle}.");
            ActiveClaimData mara = runData.PendingClaims.Single(
                c => c.ClientVariantId == ControlClaimantContent.StableClaimantId);
            Assert.AreEqual(ControlClaimantContent.ClaimId, mara.ClaimId);
            Assert.IsNull(mara.AuthoredAppearanceKey);
        }

        [Test]
        public void ShiftManager_ActualGeneration_RestrictsMaraToShiftThree()
        {
            ConfigureGameManager(ProofLifecycle.NoProofObject);

            foreach (int shiftNumber in new[] { 1, 2, 3, 4, 5 })
            {
                SeedEngine.Init(93000 + shiftNumber);
                var runData = RunData(seed: $"WINDOW-{shiftNumber}", shift: shiftNumber);

                InvokeGenerateInitialQueue(
                    ConfiguredShiftManager($"Window_{shiftNumber}"),
                    shiftNumber,
                    runData);

                Assert.AreEqual(shiftNumber == 3 ? 1 : 0, CountMara(runData),
                    $"Unexpected live Mara insertion count on shift {shiftNumber}.");
            }
        }

        [Test]
        public void ShiftManager_Reconstruction_DoesNotRegenerateMaraIntoSavedQueue()
        {
            var runData = RunData(seed: "MARA-RECONSTRUCT", shift: 3);
            runData.CurrentPhase = ShiftPhase.MorningBlock;
            RunStateController run = Controller(runData);
            ConfigureGameManager(ProofLifecycle.NoProofObject, run);

            ShiftManager first = ConfiguredShiftManager("FirstScene");
            StartShiftManager.Invoke(first, null);

            Assert.IsNotNull(runData.ActiveClaim);
            Assert.AreEqual(1, CountMara(runData),
                "Initial production Start should materialise one Mara across active + pending.");

            int pendingBefore = runData.PendingClaims.Count;
            string activeEncounterClaim = runData.ActiveClaim.ClaimId;

            ShiftManager reconstructed = ConfiguredShiftManager("ReconstructedScene");
            StartShiftManager.Invoke(reconstructed, null);

            Assert.AreEqual(1, CountMara(runData),
                "A reconstructed ShiftManager must reuse the saved queue, not insert Mara again.");
            Assert.AreEqual(pendingBefore, runData.PendingClaims.Count);
            Assert.AreEqual(activeEncounterClaim, runData.ActiveClaim.ClaimId);
        }

        [TestCase("elias_venn")]
        [TestCase(ControlClaimantContent.StableClaimantId)]
        public void AuthoredIdentity_UsesClientVariant_NotDisplayNameOrEncounterId(
            string claimantId)
        {
            var first = Claim("CLM-ID-A", claimantId, "human");
            first.ClaimantName = "Original Display Name";
            var second = Claim("CLM-ID-B", claimantId, "human");
            second.ClaimantName = "Renamed Localised Display";

            RunData data = RunData("AUTHORED-ID", 1, first);
            RunStateController run = Controller(data);

            Present(first, data, Meta);
            Assert.IsTrue(Commit(
                first, ClaimResolutionKind.Approve, run, Meta).Committed);

            data.ShiftNumber = 2;
            data.ActiveClaim = second;
            Present(second, data, Meta);
            Assert.IsTrue(Commit(
                second, ClaimResolutionKind.Deny, run, Meta).Committed);

            MetaProgressData loaded = SaveSystem.LoadMeta();
            IReadOnlyList<EncounterRecord> history =
                loaded.Encounters.CommittedDispositionsFor(claimantId);

            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(claimantId, history[0].ClientVariantId);
            Assert.AreEqual(claimantId, history[1].ClientVariantId);
            Assert.AreNotEqual(history[0].EncounterId, history[1].EncounterId);
            Assert.AreEqual(ClaimResolutionKind.Approve, history[0].Outcome);
            Assert.AreEqual(ClaimResolutionKind.Deny, history[1].Outcome);
        }

        [Test]
        public void LatestDisposition_IgnoresLaterInterruptedEncounter_AfterDiskReload()
        {
            var completed = Claim("CLM-LATEST-A", "latest_claimant");
            RunData data = RunData("LATEST", 1, completed);
            RunStateController run = Controller(data);

            Present(completed, data, Meta);
            Assert.IsTrue(Commit(
                completed, ClaimResolutionKind.Approve, run, Meta).Committed);

            var interrupted = Claim("CLM-LATEST-B", "latest_claimant");
            data.ActiveClaim = interrupted;
            Present(interrupted, data, Meta);
            SaveSystem.SaveRun(data);
            SaveSystem.SaveMeta(Meta);

            MetaProgressData loaded = SaveSystem.LoadMeta();

            Assert.AreEqual(
                ClaimResolutionKind.Approve,
                loaded.Encounters.LatestDispositionFor("latest_claimant"));
            Assert.IsTrue(
                loaded.Encounters.HasCommittedHistory("latest_claimant"));
            Assert.AreEqual(
                1,
                loaded.Encounters.CommittedDispositionsFor("latest_claimant").Count);
        }

        [Test]
        public void DuplicateAuthoritativeCommit_DoesNotDuplicateOrRewriteHistoryView()
        {
            var claim = Claim("CLM-DUP-HISTORY", "duplicate_history");
            RunData data = RunData("DUP-HISTORY", 1, claim);
            RunStateController run = Controller(data);

            Present(claim, data, Meta);
            CommitResult first = Commit(
                claim, ClaimResolutionKind.Liquify, run, Meta);
            CommitResult duplicate = Commit(
                claim, ClaimResolutionKind.Approve, run, Meta);

            Assert.IsTrue(first.Committed);
            Assert.IsFalse(duplicate.Committed);
            Assert.AreEqual(CommitRejection.AlreadyCommitted, duplicate.Rejection);

            MetaProgressData loaded = SaveSystem.LoadMeta();
            IReadOnlyList<EncounterRecord> history =
                loaded.Encounters.CommittedDispositionsFor("duplicate_history");

            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(ClaimResolutionKind.Liquify, history[0].Outcome);
            Assert.AreEqual(claim.EncounterId, history[0].EncounterId);
        }

        [Test]
        public void HistoricalQuery_IsReadOnlyAndPreservesAppendOrder()
        {
            var a1 = Claim("CLM-A1", "claimant_a");
            var b1 = Claim("CLM-B1", "claimant_b");
            var a2 = Claim("CLM-A2", "claimant_a");
            RunData data = RunData("READ-ONLY", 1);
            RunStateController run = Controller(data);

            CommitClaim(a1, ClaimResolutionKind.Approve, data, run);
            CommitClaim(b1, ClaimResolutionKind.Liquify, data, run);
            CommitClaim(a2, ClaimResolutionKind.Deny, data, run);

            string[] globalOrderBefore =
                Meta.Encounters.Records.Select(r => r.EncounterId).ToArray();
            int countBefore = Meta.Encounters.Count;

            IReadOnlyList<EncounterRecord> a =
                Meta.Encounters.CommittedDispositionsFor("claimant_a");
            _ = Meta.Encounters.LatestDispositionFor("claimant_a");
            _ = Meta.Encounters.HasCommittedHistory("claimant_a");
            _ = Meta.Encounters.CommittedDispositionsFor(" ");

            Assert.AreEqual(countBefore, Meta.Encounters.Count);
            CollectionAssert.AreEqual(
                globalOrderBefore,
                Meta.Encounters.Records.Select(r => r.EncounterId).ToArray());
            CollectionAssert.AreEqual(
                new[] { a1.EncounterId, a2.EncounterId },
                a.Select(r => r.EncounterId).ToArray());
        }

        [Test]
        public void MaraScheduleCommitAndHistory_WorkWithoutEliasState()
        {
            ConfigureGameManager(ProofLifecycle.NoProofObject);
            var data = RunData("MARA-HISTORY", 3);
            InvokeGenerateInitialQueue(
                ConfiguredShiftManager("MaraHistory"), 3, data);

            ActiveClaimData mara = data.PendingClaims.Single(
                c => c.ClientVariantId == ControlClaimantContent.StableClaimantId);
            data.PendingClaims.Remove(mara);
            data.ActiveClaim = mara;
            RunStateController run = Controller(data);

            Present(mara, data, Meta);
            Assert.IsTrue(Commit(
                mara, ClaimResolutionKind.Approve, run, Meta).Committed);

            MetaProgressData loaded = SaveSystem.LoadMeta();
            IReadOnlyList<EncounterRecord> history =
                loaded.Encounters.CommittedDispositionsFor(
                    ControlClaimantContent.StableClaimantId);

            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(mara.EncounterId, history[0].EncounterId);
            Assert.AreEqual(ClaimResolutionKind.Approve, history[0].Outcome);
            Assert.IsFalse(loaded.EliasProof.IsActive);
            Assert.IsEmpty(loaded.CompletedProofSessions);
        }

        [Test]
        public void ProceduralGenerator_DrawsFreshVariantIds_IndependentOfDisplayName()
        {
            ClaimTemplateData template = Template();
            template.ClaimantNamePool = new[] { "Same Display Name" };
            SeedEngine.Init(164051);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < 12; i++)
            {
                ActiveClaimData generated = ClaimGenerator.Generate(
                    3, new[] { template }, Array.Empty<AnomalyTagData>(), Meta);

                Assert.AreEqual("Same Display Name", generated.ClaimantName);
                StringAssert.IsMatch("^unregistered_alien_[0-9]{3}$",
                    generated.ClientVariantId);
                ids.Add(generated.ClientVariantId);
            }

            Assert.Greater(ids.Count, 1,
                "Procedural identity is freshly drawn per claim, not stable by display name.");
        }

        private void CommitClaim(
            ActiveClaimData claim,
            ClaimResolutionKind kind,
            RunData data,
            RunStateController run)
        {
            data.ActiveClaim = claim;
            Present(claim, data, Meta);
            Assert.IsTrue(Commit(claim, kind, run, Meta).Committed);
        }

        private GameManager ConfigureGameManager(
            ProofLifecycle lifecycle,
            RunStateController run = null)
        {
            GameManagerInstanceField.SetValue(null, null);
            if (lifecycle == ProofLifecycle.NoGameManager)
                return null;

            var host = new GameObject($"Delta1_GameManager_{_owned.Count + 1}");
            host.SetActive(false);
            var manager = host.AddComponent<GameManager>();
            _owned.Add(host);

            GameManagerInstanceField.SetValue(null, manager);
            GameManagerRunField.SetValue(manager, run);
            manager.SetMetaForTesting(Meta);

            if (lifecycle == ProofLifecycle.NoProofObject)
            {
                GameManagerProofField.SetValue(manager, null);
                return manager;
            }

            EliasProofSessionController proof =
                Component<EliasProofSessionController>(
                    $"Proof_{lifecycle}_{_owned.Count + 1}");
            GameManagerProofField.SetValue(manager, proof);

            var content = ScriptableObject.CreateInstance<EliasProofContent>();
            _owned.Add(content);
            EliasContentField.SetValue(manager, content);

            switch (lifecycle)
            {
                case ProofLifecycle.Active:
                    Meta.EliasProof = EliasProofSessionState.Create("delta1-active");
                    break;
                case ProofLifecycle.Archived:
                    Meta.EliasProof = new EliasProofSessionState();
                    Meta.CompletedProofSessions.Add(
                        EliasProofSessionState.Create("delta1-archived"));
                    break;
                default:
                    Meta.EliasProof = new EliasProofSessionState();
                    break;
            }

            return manager;
        }

        private ShiftManager ConfiguredShiftManager(string label)
        {
            ShiftManager shift = Component<ShiftManager>($"Delta1_{label}");
            ClaimTemplatesField.SetValue(shift, new[] { Template() });
            AnomalyTagsField.SetValue(shift, Array.Empty<AnomalyTagData>());

            var tuning = ScriptableObject.CreateInstance<TideTuningData>();
            _owned.Add(tuning);
            TideTuningField.SetValue(shift, tuning);
            return shift;
        }

        private ClaimTemplateData Template()
        {
            var template = ScriptableObject.CreateInstance<ClaimTemplateData>();
            _owned.Add(template);
            template.TemplateId = $"delta1_template_{_owned.Count}";
            template.SpeciesPool = new[] { "unregistered_alien" };
            template.ClaimantNamePool = new[] { "Procedural Control" };
            template.DeptNamePool = new[] { "Validation" };
            template.IncidentTextVariants = new[] { "{claimant} at {dept} for {amount}." };
            template.AnomalyTagSlots = 0;
            template.ClaimAmountMin = 42;
            template.ClaimAmountMax = 42;
            template.MinShiftNumber = 1;
            template.SpawnWeight = 1f;
            return template;
        }

        private static void InvokeGenerateInitialQueue(
            ShiftManager shift,
            int shiftNumber,
            RunData runData)
            => GenerateInitialQueue.Invoke(
                shift, new object[] { shiftNumber, runData });

        private static int CountMara(RunData data)
        {
            int count = data.PendingClaims.Count(
                c => c?.ClientVariantId == ControlClaimantContent.StableClaimantId);
            if (data.ActiveClaim?.ClientVariantId
                == ControlClaimantContent.StableClaimantId)
            {
                count++;
            }
            return count;
        }
    }
}
