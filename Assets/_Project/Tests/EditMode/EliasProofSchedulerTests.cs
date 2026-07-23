using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Desk42.Core;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    public sealed class EliasProofSchedulerTests
    {
        private GameObject _host;
        private EliasProofSessionController _proof;
        private RunStateController _run;
        private EliasProofContent _content;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("EliasSchedulerTests");
            _proof =
                _host.AddComponent<EliasProofSessionController>();
            _run = _host.AddComponent<RunStateController>();
            typeof(RunStateController)
                .GetField("_data",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_run, new RunData
                {
                    CorporateCredits = 10,
                    Sanity = 100f,
                    SoulIntegrity = 100f,
                    ComboMultiplier = 1f,
                });
            _content =
                ScriptableObject.CreateInstance<EliasProofContent>();
            _proof.BeginProofSession("scheduler-test");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_content);
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void GenerateThenReplace_PreservesQueueAndClaimSeedStream()
        {
            List<ActiveClaimData> queue = MakeOrdinaryQueue(8);
            string[] beforeIds =
                queue.Select(claim => claim.ClaimId).ToArray();

            SeedEngine.Init(4242);
            bool replaced =
                EliasProofScheduler.TryReplaceScheduledClaim(
                    queue, 1, _content, _proof.State,
                    out ActiveClaimData elias);
            int nextAfterReplacement =
                SeedEngine.Next(SeedStream.ClaimQueue, int.MaxValue);

            SeedEngine.Init(4242);
            int nextWithoutReplacement =
                SeedEngine.Next(SeedStream.ClaimQueue, int.MaxValue);

            Assert.IsTrue(replaced);
            Assert.AreEqual(nextWithoutReplacement,
                nextAfterReplacement);
            Assert.AreEqual(8, queue.Count);
            Assert.AreSame(elias, queue[1]);
            Assert.AreEqual("ordinary-1", queue[0].ClaimId);
            CollectionAssert.AreEqual(
                beforeIds.Skip(2).ToArray(),
                queue.Skip(2).Select(claim => claim.ClaimId).ToArray());
            AssertAuthoredClaim(
                elias,
                EliasProofContent.Shift1AppearanceKey,
                "elias_shift_1_claim");
            Assert.AreEqual(1, queue.Count(IsElias));
        }

        [TestCase(ClaimResolutionKind.Approve)]
        [TestCase(ClaimResolutionKind.Deny)]
        public void Shift2_ReplacesClaimTwoAfterEitherShift1Disposition(
            ClaimResolutionKind shift1Disposition)
        {
            RecordShift1(shift1Disposition);
            List<ActiveClaimData> queue = MakeOrdinaryQueue(8);

            Assert.IsTrue(
                EliasProofScheduler.TryReplaceScheduledClaim(
                    queue, 2, _content, _proof.State,
                    out ActiveClaimData elias));

            Assert.AreSame(elias, queue[1]);
            AssertAuthoredClaim(
                elias,
                EliasProofContent.Shift2AppearanceKey,
                "elias_shift_2_claim");
        }

        [TestCase(EliasProcedureActionId.AmendRecord,
            "elias_shift_5a_claim")]
        [TestCase(EliasProcedureActionId.RetainLegacyUnit,
            "elias_shift_5b_claim")]
        [TestCase(EliasProcedureActionId.ReferForReview,
            "elias_shift_5c_claim")]
        public void Shift5_ReplacesClaimThreeFromAuthoritativeBranch(
            EliasProcedureActionId actionId,
            string expectedClaimId)
        {
            AdvanceThroughShift2(actionId);
            List<ActiveClaimData> queue = MakeOrdinaryQueue(9);

            Assert.IsTrue(
                EliasProofScheduler.TryReplaceScheduledClaim(
                    queue, 5, _content, _proof.State,
                    out ActiveClaimData elias));

            Assert.AreSame(elias, queue[2]);
            AssertAuthoredClaim(
                elias,
                EliasProofContent.Shift5AppearanceKey,
                expectedClaimId);
            Assert.AreEqual(1, queue.Count(IsElias));
        }

        [Test]
        public void Shift5_WithMissingBranch_FailsLoudly()
        {
            RecordShift1(ClaimResolutionKind.Approve);

            var exception = Assert.Throws<System.InvalidOperationException>(
                () => EliasProofScheduler.TryReplaceScheduledClaim(
                    MakeOrdinaryQueue(9),
                    5,
                    _content,
                    _proof.State,
                    out _));

            StringAssert.Contains("no Shift 2 branch",
                exception.Message);
        }

        [Test]
        public void InactiveProof_DoesNotChangeGeneratedQueue()
        {
            _proof.EndProofSession();
            List<ActiveClaimData> queue = MakeOrdinaryQueue(8);
            string[] before =
                queue.Select(claim => claim.ClaimId).ToArray();

            Assert.IsFalse(
                EliasProofScheduler.TryReplaceScheduledClaim(
                    queue, 1, _content, _proof.State, out _));
            CollectionAssert.AreEqual(
                before,
                queue.Select(claim => claim.ClaimId).ToArray());
        }

        private void RecordShift1(ClaimResolutionKind kind)
        {
            _proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey);
            _proof.RecordDisposition(
                EliasProofContent.Shift1AppearanceKey,
                BuildDisposition(kind));
        }

        private void AdvanceThroughShift2(
            EliasProcedureActionId actionId)
        {
            RecordShift1(ClaimResolutionKind.Approve);
            _proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey);
            Assert.IsTrue(_proof.TryApplyProcedure(
                _run,
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey,
                actionId,
                out _,
                out _));
            _proof.RecordDisposition(
                EliasProofContent.Shift2AppearanceKey,
                BuildDisposition(ClaimResolutionKind.Approve));
        }

        private static AppliedClaimResolution BuildDisposition(
            ClaimResolutionKind kind)
            => new(
                "elias-test-claim",
                EliasProofContent.CanonicalClaimantId,
                "moth_accountant",
                kind,
                0,
                0f,
                0f,
                0,
                0,
                1,
                3,
                1f,
                1f);

        private static List<ActiveClaimData> MakeOrdinaryQueue(
            int count)
        {
            var claims = new List<ActiveClaimData>(count);
            for (int i = 1; i <= count; i++)
            {
                claims.Add(new ActiveClaimData
                {
                    ClaimId = $"ordinary-{i}",
                    ClientVariantId = $"ordinary-client-{i}",
                    ClientSpeciesId = "human",
                    ClaimantName = $"Claimant {i}",
                });
            }
            return claims;
        }

        private static void AssertAuthoredClaim(
            ActiveClaimData claim,
            string appearanceKey,
            string claimId)
        {
            Assert.AreEqual(claimId, claim.ClaimId);
            Assert.AreEqual(
                EliasProofContent.CanonicalClaimantId,
                claim.ClientVariantId);
            Assert.AreEqual(appearanceKey,
                claim.AuthoredAppearanceKey);
            Assert.AreEqual("Elias Venn", claim.ClaimantName);
            Assert.IsFalse(claim.IsResolved);
            Assert.AreEqual(ClaimResolutionKind.Unspecified,
                claim.ResolutionKind);
        }

        private static bool IsElias(ActiveClaimData claim)
            => claim?.ClientVariantId
                == EliasProofContent.CanonicalClaimantId;
    }
}
