using System.Reflection;
using Desk42.Core;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    public sealed class EliasProcedureApplicationTests
    {
        private GameObject _host;
        private EliasProofSessionController _proof;
        private RunStateController _run;
        private RunData _data;

        [SetUp]
        public void SetUp()
        {
            RumorMill.ClearAllSubscriptions();
            _host = new GameObject("EliasProcedureTests");
            _proof =
                _host.AddComponent<EliasProofSessionController>();
            _run = _host.AddComponent<RunStateController>();
            _data = new RunData
            {
                CorporateCredits = 12,
                Sanity = 70f,
                SoulIntegrity = 80f,
                ComboMultiplier = 1f,
                QuotaForCurrentAnte = 3,
            };
            typeof(RunStateController)
                .GetField("_data",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_run, _data);

            _proof.BeginProofSession("procedure-test");
            _proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey);
            _proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey);
        }

        [TearDown]
        public void TearDown()
        {
            RumorMill.ClearAllSubscriptions();
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [TestCase(EliasProcedureActionId.AmendRecord,
            EliasShift2Branch.NormalisedAddress, 1f)]
        [TestCase(EliasProcedureActionId.RetainLegacyUnit,
            EliasShift2Branch.LegacyException, 0f)]
        [TestCase(EliasProcedureActionId.ReferForReview,
            EliasShift2Branch.PhysicalVerification, 0f)]
        public void Shift2Procedure_PreviewsThenAppliesOneAtomicNonterminalResult(
            EliasProcedureActionId actionId,
            EliasShift2Branch expectedBranch,
            float expectedStreakDelta)
        {
            int events = 0;
            AppliedEliasProcedure eventResult = default;
            System.Action<EliasProcedureAppliedEvent> handler = e =>
            {
                events++;
                eventResult = e.Result;
            };
            RumorMill.OnEliasProcedureApplied += handler;

            ProjectedEliasProcedure projection =
                _proof.PreviewProcedure(
                    _run,
                    EliasProofContent.CanonicalClaimantId,
                    EliasProofContent.Shift2AppearanceKey,
                    actionId);

            Assert.IsTrue(projection.IsAvailable);
            Assert.IsFalse(projection.IsTerminal);
            Assert.AreEqual(expectedBranch, projection.ResultingBranch);
            Assert.AreEqual(expectedStreakDelta,
                projection.ComplianceStreakDelta, 0.001f);
            Assert.AreEqual(EliasShift2Branch.None,
                _proof.State.Shift2Branch);
            Assert.AreEqual(1f, _data.ComboMultiplier, 0.001f);
            Assert.AreEqual(0, _data.ClaimsProcessedThisAnte);
            Assert.AreEqual(0, _data.Stats.ClaimsProcessed);

            bool succeeded = _proof.TryApplyProcedure(
                _run,
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey,
                actionId,
                out AppliedEliasProcedure applied,
                out EliasProcedureFailureReason failure);

            Assert.IsTrue(succeeded);
            Assert.AreEqual(EliasProcedureFailureReason.None, failure);
            Assert.IsFalse(applied.IsTerminal);
            Assert.AreEqual(expectedBranch,
                _proof.State.Shift2Branch);
            Assert.AreEqual(actionId,
                _proof.State.Shift2ProcedureAction);
            Assert.AreEqual(applied.ReceiptId,
                _proof.State.Shift2ProcedureReceiptId);
            Assert.IsTrue(
                _proof.State.AppliedProcedureAppearanceKeys.Contains(
                    EliasProofContent.Shift2AppearanceKey));
            Assert.AreEqual(projection.ActionId, applied.ActionId);
            Assert.AreEqual(projection.ResultingBranch,
                applied.ResultingBranch);
            Assert.AreEqual(projection.PriorVisits,
                applied.PriorVisits);
            Assert.AreEqual(projection.CurrentVisitNumber,
                applied.CurrentVisitNumber);
            Assert.AreEqual(projection.CreditsDelta,
                applied.CreditsDelta);
            Assert.AreEqual(projection.SanityDelta,
                applied.SanityDelta, 0.001f);
            Assert.AreEqual(projection.SoulIntegrityDelta,
                applied.SoulIntegrityDelta, 0.001f);
            Assert.AreEqual(projection.ComplianceStreakDelta,
                applied.ComplianceStreakDelta, 0.001f);
            Assert.AreEqual(0, _data.ClaimsProcessedThisAnte);
            Assert.AreEqual(0, _data.Stats.ClaimsProcessed);
            Assert.AreEqual(1, events);
            Assert.AreEqual(applied.ReceiptId,
                eventResult.ReceiptId);

            if (actionId == EliasProcedureActionId.AmendRecord)
            {
                Assert.AreEqual(EliasProcedurePolicy.OriginalAddress,
                    applied.AddressBefore);
                Assert.AreEqual(EliasProcedurePolicy.AmendedAddress,
                    applied.AddressAfter);
                Assert.AreEqual(
                    EliasProcedurePolicy.MiriamRegisteredAt18A,
                    applied.MiriamRegistrationReference);
            }
            else
            {
                Assert.AreEqual(EliasProcedurePolicy.OriginalAddress,
                    applied.AddressBefore);
                Assert.AreEqual(EliasProcedurePolicy.OriginalAddress,
                    applied.AddressAfter);
                Assert.IsNull(applied.MiriamRegistrationReference);
            }
        }

        [Test]
        public void Shift2Procedure_CannotOverwriteBranch_AndDispositionIsSeparate()
        {
            Assert.IsTrue(_proof.TryApplyProcedure(
                _run,
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey,
                EliasProcedureActionId.AmendRecord,
                out _,
                out _));

            bool secondSucceeded = _proof.TryApplyProcedure(
                _run,
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey,
                EliasProcedureActionId.RetainLegacyUnit,
                out _,
                out EliasProcedureFailureReason secondFailure);

            Assert.IsFalse(secondSucceeded);
            Assert.AreEqual(
                EliasProcedureFailureReason.ProcedureAlreadyApplied,
                secondFailure);
            Assert.AreEqual(EliasShift2Branch.NormalisedAddress,
                _proof.State.Shift2Branch);

            AppliedClaimResolution disposition =
                BuildDisposition(ClaimResolutionKind.Deny);
            _proof.RecordDisposition(
                EliasProofContent.Shift2AppearanceKey,
                disposition);

            Assert.AreEqual(ClaimResolutionKind.Deny,
                _proof.State.Shift2FinalDisposition);
            Assert.AreEqual(EliasShift2Branch.NormalisedAddress,
                _proof.State.Shift2Branch);
            Assert.AreEqual(EliasProcedureActionId.AmendRecord,
                _proof.State.Shift2ProcedureAction);
        }

        [Test]
        public void Shift1Disposition_Converges_AndLiquifyUsesContinuityHold()
        {
            Assert.IsFalse(_proof.TryValidateDisposition(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey,
                ClaimResolutionKind.Liquify,
                out string liquifyReason));
            Assert.AreEqual(
                EliasProofSessionController.ContinuityHoldFailureReason,
                liquifyReason);

            Assert.IsTrue(_proof.TryValidateDisposition(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey,
                ClaimResolutionKind.Approve,
                out _));

            _proof.RecordDisposition(
                EliasProofContent.Shift1AppearanceKey,
                BuildDisposition(ClaimResolutionKind.Approve));

            Assert.AreEqual(EliasShift1Disposition.Approved,
                _proof.State.Shift1Disposition);
        }

        [TestCase(EliasProcedureActionId.AmendRecord, true, true)]
        [TestCase(EliasProcedureActionId.RetainLegacyUnit, false, true)]
        [TestCase(EliasProcedureActionId.ReferForReview, true, false)]
        public void Shift5Preview_UsesStoredBranchForToolLocks(
            EliasProcedureActionId shift2Action,
            bool requestClarificationAvailable,
            bool referForReviewAvailable)
        {
            Assert.IsTrue(_proof.TryApplyProcedure(
                _run,
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey,
                shift2Action,
                out _,
                out _));
            _proof.RecordDisposition(
                EliasProofContent.Shift2AppearanceKey,
                BuildDisposition(ClaimResolutionKind.Approve));
            _proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift5AppearanceKey);

            ProjectedEliasProcedure request =
                _proof.PreviewProcedure(
                    _run,
                    EliasProofContent.CanonicalClaimantId,
                    EliasProofContent.Shift5AppearanceKey,
                    EliasProcedureActionId.RequestClarification);
            ProjectedEliasProcedure refer =
                _proof.PreviewProcedure(
                    _run,
                    EliasProofContent.CanonicalClaimantId,
                    EliasProofContent.Shift5AppearanceKey,
                    EliasProcedureActionId.ReferForReview);

            Assert.AreEqual(requestClarificationAvailable,
                request.IsAvailable);
            Assert.AreEqual(referForReviewAvailable,
                refer.IsAvailable);
            if (!requestClarificationAvailable)
            {
                Assert.AreEqual(
                    EliasProcedureFailureReason.ToolLockedByBranch,
                    request.FailureReason);
            }
            if (!referForReviewAvailable)
            {
                Assert.AreEqual(
                    EliasProcedureFailureReason.ToolLockedByBranch,
                    refer.FailureReason);
            }
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
    }
}
