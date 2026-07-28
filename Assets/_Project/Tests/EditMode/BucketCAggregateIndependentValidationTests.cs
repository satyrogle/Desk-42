using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Desk42.Core;
using Desk42.Encounter;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    /// <summary>
    /// Cross-delta probes for the final Bucket C aggregate. These tests focus
    /// on the boundary that none of the individual delta suites owned:
    /// carry-forward keeps an encounter identity, while an authored later
    /// appearance is reconstructed from proof state and receives a new one.
    /// </summary>
    public sealed class BucketCAggregateIndependentValidationTests
        : Bucket1PersistenceFixture
    {
        private readonly List<UnityEngine.Object> _owned = new();

        [TearDown]
        public void TearDownAggregateObjects()
        {
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] != null)
                    UnityEngine.Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear();
        }

        [Test]
        public void EliasShift2ToShift5_AfterDiskRestart_IsRecurrenceWithNewEncounterId()
        {
            EliasProofContent content = Own(
                ScriptableObject.CreateInstance<EliasProofContent>());
            Meta.EliasProof =
                EliasProofSessionState.Create("aggregate-elias-proof");
            Meta.EliasProof.Shift1Disposition =
                EliasShift1Disposition.Approved;

            List<ActiveClaimData> shift2Queue = OrdinaryQueue(8, "s2");
            Assert.IsTrue(EliasProofScheduler.TryReplaceScheduledClaim(
                shift2Queue, 2, content, Meta.EliasProof,
                out ActiveClaimData shift2Elias));

            RunData shift2Run = RunData(
                "AGGREGATE-ELIAS-SHIFT2", 2, shift2Elias);
            Present(shift2Elias, shift2Run, Meta);
            string shift2EncounterId = shift2Elias.EncounterId;

            CommitResult shift2Commit = Commit(
                shift2Elias,
                ClaimResolutionKind.Approve,
                Controller(shift2Run),
                Meta);
            Assert.IsTrue(shift2Commit.Committed);

            // These are the factual proof outputs normally written by the
            // Shift 2 procedure/disposition façade. Persist them only after
            // the real encounter transaction has completed.
            Meta.EliasProof.Shift2Branch =
                EliasShift2Branch.LegacyException;
            Meta.EliasProof.Shift2FinalDisposition =
                ClaimResolutionKind.Approve;
            Meta.EliasProof.RecordedAppearanceKeys.Add(
                EliasProofContent.Shift1AppearanceKey);
            Meta.EliasProof.RecordedAppearanceKeys.Add(
                EliasProofContent.Shift2AppearanceKey);
            Assert.IsTrue(SaveSystem.SaveMeta(Meta));

            MetaProgressData restartedMeta = SaveSystem.LoadMeta();
            Assert.AreEqual(
                EliasShift2Branch.LegacyException,
                restartedMeta.EliasProof.Shift2Branch);
            Assert.IsTrue(
                restartedMeta.Encounters.IsCompleted(shift2EncounterId));

            List<ActiveClaimData> shift5Queue = OrdinaryQueue(9, "s5");
            Assert.IsTrue(EliasProofScheduler.TryReplaceScheduledClaim(
                shift5Queue, 5, content, restartedMeta.EliasProof,
                out ActiveClaimData shift5Elias));
            Assert.IsNull(shift5Elias.EncounterId,
                "The authored return must not inherit the old encounter.");
            Assert.AreEqual("elias_shift_5b_claim", shift5Elias.ClaimId);

            RunData shift5Run = RunData(
                "AGGREGATE-ELIAS-SHIFT5", 5, shift5Elias);
            Present(shift5Elias, shift5Run, restartedMeta);
            string shift5EncounterId = shift5Elias.EncounterId;

            Assert.AreNotEqual(shift2EncounterId, shift5EncounterId);
            Assert.AreEqual(
                EliasProofContent.CanonicalClaimantId,
                shift2Elias.ClientVariantId);
            Assert.AreEqual(
                shift2Elias.ClientVariantId,
                shift5Elias.ClientVariantId);
            Assert.AreEqual(2, restartedMeta.Encounters.Count);
            Assert.AreEqual(1, restartedMeta.Encounters.Records.Count(
                record => record.EncounterId == shift2EncounterId
                    && record.Completed
                    && record.AuthoredAppearanceKey
                        == EliasProofContent.Shift2AppearanceKey));
            Assert.AreEqual(1, restartedMeta.Encounters.Records.Count(
                record => record.EncounterId == shift5EncounterId
                    && !record.Completed
                    && record.AuthoredAppearanceKey
                        == EliasProofContent.Shift5AppearanceKey));
        }

        [Test]
        public void AuthoredSchedulers_ReinvocationReplacesSlotsWithoutDuplicatingClaimants()
        {
            EliasProofContent content = Own(
                ScriptableObject.CreateInstance<EliasProofContent>());
            EliasProofSessionState state =
                EliasProofSessionState.Create("aggregate-scheduler");
            state.Shift1Disposition = EliasShift1Disposition.Approved;
            state.Shift2Branch = EliasShift2Branch.NormalisedAddress;
            state.Shift2FinalDisposition = ClaimResolutionKind.Deny;

            List<ActiveClaimData> eliasQueue = OrdinaryQueue(9, "elias");
            Assert.IsTrue(EliasProofScheduler.TryReplaceScheduledClaim(
                eliasQueue, 5, content, state, out _));
            Assert.IsTrue(EliasProofScheduler.TryReplaceScheduledClaim(
                eliasQueue, 5, content, state, out _));
            Assert.AreEqual(1, eliasQueue.Count(claim =>
                claim.ClientVariantId
                    == EliasProofContent.CanonicalClaimantId));

            List<ActiveClaimData> maraQueue = OrdinaryQueue(8, "mara");
            Assert.IsTrue(
                ControlClaimantContent.TryScheduleControlClaimant(
                    maraQueue, 3, out _));
            Assert.IsTrue(
                ControlClaimantContent.TryScheduleControlClaimant(
                    maraQueue, 3, out _));
            Assert.AreEqual(1, maraQueue.Count(claim =>
                claim.ClientVariantId
                    == ControlClaimantContent.StableClaimantId));
        }

        [Test]
        public void ApprovalLiability_DoesNotMutateProofCarryOrQueueState()
        {
            Meta.EliasProof =
                EliasProofSessionState.Create("aggregate-side-effect-audit");
            Meta.EliasProof.Shift1Disposition =
                EliasShift1Disposition.Denied;
            Meta.EliasProof.Shift2Branch =
                EliasShift2Branch.PhysicalVerification;

            ActiveClaimData carried = Claim(
                "CLM-CARRIED-SENTINEL", "same_claimant", "human");
            RunData interruptedRun = RunData(
                "AGGREGATE-CARRY", 2, carried);
            Present(carried, interruptedRun, Meta);
            Assert.IsTrue(EncounterCommitService.InterruptEncounter(
                carried, interruptedRun, Meta));

            ActiveClaimData approved = Claim(
                "CLM-APPROVAL-SOURCE", "same_claimant", "human");
            RunData approvalRun = RunData(
                "AGGREGATE-APPROVAL", 3, approved);
            ActiveClaimData queuedMara =
                ControlClaimantContent.BuildClaim();
            approvalRun.PendingClaims.Add(queuedMara);
            string proofBefore =
                JsonConvert.SerializeObject(Meta.EliasProof);

            Present(approved, approvalRun, Meta);
            Assert.IsTrue(Commit(
                approved,
                ClaimResolutionKind.Approve,
                Controller(approvalRun),
                Meta).Committed);

            Assert.AreEqual(
                proofBefore,
                JsonConvert.SerializeObject(Meta.EliasProof));
            Assert.AreEqual(1, Meta.CarriedEncounters.Count);
            Assert.AreEqual(
                carried.EncounterId,
                Meta.CarriedEncounters.Records[0].EncounterId);
            Assert.AreEqual(1, approvalRun.PendingClaims.Count);
            Assert.AreSame(queuedMara, approvalRun.PendingClaims[0]);
            Assert.AreEqual(1, Meta.ApprovalLiabilities.Count);
            Assert.AreEqual(
                approved.EncounterId,
                Meta.ApprovalLiabilities.Records[0].SourceEncounterId);
        }

        [Test]
        public void CampaignPersistenceSchema_HasNoGenericScheduledReturnLedger()
        {
            static bool IsForbidden(string name)
                => name.IndexOf("ScheduledReturn",
                       StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("RecurrenceLedger",
                       StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("ReturnTimestamp",
                       StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("ReturnObligation",
                       StringComparison.OrdinalIgnoreCase) >= 0;

            Type metaType = typeof(MetaProgressData);
            string[] members = metaType
                .GetMembers(BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic)
                .Select(member => member.Name)
                .Where(IsForbidden)
                .ToArray();
            string[] types = metaType.Assembly.GetTypes()
                .Where(type => type.Namespace == typeof(MetaProgressData).Namespace)
                .Select(type => type.Name)
                .Where(IsForbidden)
                .ToArray();

            CollectionAssert.IsEmpty(members,
                "Meta persistence unexpectedly gained generic recurrence state.");
            CollectionAssert.IsEmpty(types,
                "Production unexpectedly gained a generic recurrence ledger.");
        }

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            _owned.Add(value);
            return value;
        }

        private static List<ActiveClaimData> OrdinaryQueue(
            int count, string prefix)
        {
            var claims = new List<ActiveClaimData>(count);
            for (int index = 0; index < count; index++)
            {
                claims.Add(Claim(
                    $"{prefix}-claim-{index}",
                    $"{prefix}-claimant-{index}",
                    "human"));
            }
            return claims;
        }
    }
}
