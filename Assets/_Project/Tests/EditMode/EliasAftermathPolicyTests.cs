using System;
using System.Reflection;
using Desk42.Core;
using NUnit.Framework;
using UnityEngine;

namespace Desk42.Tests.EditMode
{
    public sealed class EliasAftermathPolicyTests
    {
        private GameObject _host;
        private EliasProofSessionController _proof;
        private RunStateController _run;
        private EliasProofContent _content;
        private int _eventCount;
        private AppliedEliasAftermath _lastEvent;

        [SetUp]
        public void SetUp()
        {
            _eventCount = 0;
            _lastEvent = default;
            _host = new GameObject("EliasAftermathTests");
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
            _proof.BeginProofSession("aftermath-test");
            RumorMill.OnEliasAftermathApplied += HandleApplied;
        }

        [TearDown]
        public void TearDown()
        {
            RumorMill.OnEliasAftermathApplied -= HandleApplied;
            UnityEngine.Object.DestroyImmediate(_content);
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [TestCase(
            EliasProcedureActionId.AmendRecord,
            EliasAftermathPolicy.HouseholdDuplicateReview,
            2)]
        [TestCase(
            EliasProcedureActionId.RetainLegacyUnit,
            EliasAftermathPolicy.InternalAuditLockdown,
            1)]
        [TestCase(
            EliasProcedureActionId.ReferForReview,
            EliasAftermathPolicy.VerificationBacklog,
            2)]
        public void AuthoredClaims_ApplyExactlyOnce_AndExpire(
            EliasProcedureActionId shift2Action,
            string expectedModifierId,
            int expectedClaimCount)
        {
            AdvanceThroughShift5(shift2Action);
            EliasAftermathModifierState modifier =
                _proof.ActivateShift5Aftermath(_content);
            EliasAftermathDefinition definition =
                EliasAftermathPolicy.ForBranch(
                    _content, _proof.State.Shift2Branch);

            Assert.AreEqual(expectedModifierId, modifier.ModifierId);
            Assert.AreEqual(expectedClaimCount,
                modifier.PendingClaimIds.Count);
            Assert.IsTrue(modifier.IsActive);

            Assert.IsFalse(_proof.TryApplyAftermathToClaim(
                "unrelated_claim", out _));
            Assert.AreEqual(expectedClaimCount,
                modifier.PendingClaimIds.Count);
            Assert.AreEqual(0, _eventCount);

            for (int i = 0; i < definition.ClaimIds.Length; i++)
            {
                string claimId = definition.ClaimIds[i];
                Assert.IsTrue(_proof.TryApplyAftermathToClaim(
                    claimId, out AppliedEliasAftermath applied));
                Assert.AreEqual(claimId, applied.ClaimId);
                Assert.AreEqual(i + 1, applied.AppliedCount);
                Assert.AreEqual(expectedClaimCount,
                    applied.TotalClaimCount);
                Assert.AreEqual(
                    expectedClaimCount - i - 1,
                    applied.RemainingClaimCount);
                Assert.AreEqual(i + 1, _eventCount);
                Assert.AreEqual(claimId, _lastEvent.ClaimId);

                Assert.IsFalse(_proof.TryApplyAftermathToClaim(
                    claimId, out _),
                    "Encounter reconstruction must not apply twice.");
                Assert.AreEqual(i + 1, _eventCount);
            }

            Assert.IsFalse(modifier.IsActive);
            Assert.IsTrue(modifier.IsExpired);
            Assert.AreEqual(expectedClaimCount,
                modifier.AppliedClaimIds.Count);
            Assert.AreEqual(0, modifier.PendingClaimIds.Count);
        }

        [Test]
        public void DuplicateActivation_FailsLoudly()
        {
            AdvanceThroughShift5(EliasProcedureActionId.AmendRecord);
            _proof.ActivateShift5Aftermath(_content);

            InvalidOperationException exception =
                Assert.Throws<InvalidOperationException>(() =>
                    _proof.ActivateShift5Aftermath(_content));
            StringAssert.Contains(
                "more than once", exception.Message);
        }

        [Test]
        public void EndProofSession_RemovesEveryAftermathEffect()
        {
            AdvanceThroughShift5(EliasProcedureActionId.AmendRecord);
            _proof.ActivateShift5Aftermath(_content);

            _proof.EndProofSession();

            Assert.IsFalse(_proof.HasActiveSession);
            Assert.IsFalse(
                _proof.State.ActiveAftermathModifier.IsActive);
            Assert.IsFalse(_proof.TryApplyAftermathToClaim(
                "elias_aftermath_5a_1", out _));
        }

        [Test]
        public void AuthoredCopy_ReportsConsequencesWithoutGrading()
        {
            foreach (string claimId
                     in _content.AuthoredFollowUpClaimIds)
            {
                EliasAftermathPolicy.GetClaimCopy(
                    claimId, out string claimant, out string incident);
                Assert.IsNotEmpty(claimant);
                Assert.IsNotEmpty(incident);
                string copy = $"{claimant} {incident}".ToLowerInvariant();
                foreach (string banned in new[]
                         {
                             "correct", "valid choice", "better choice",
                             "optimal", "mistake", "wasted", "penalty",
                             "should have",
                         })
                {
                    StringAssert.DoesNotContain(banned, copy);
                }
            }
        }

        private void AdvanceThroughShift5(
            EliasProcedureActionId shift2Action)
        {
            _proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey);
            _proof.RecordDisposition(
                EliasProofContent.Shift1AppearanceKey,
                BuildDisposition(ClaimResolutionKind.Approve));
            _proof.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey);
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

            EliasProcedureActionId shift5Action =
                shift2Action
                    == EliasProcedureActionId.RetainLegacyUnit
                    ? EliasProcedureActionId.ReferForReview
                    : EliasProcedureActionId.RequestClarification;
            Assert.IsTrue(_proof.TryApplyProcedure(
                _run,
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift5AppearanceKey,
                shift5Action,
                out _,
                out _));
            _proof.RecordDisposition(
                EliasProofContent.Shift5AppearanceKey,
                BuildDisposition(ClaimResolutionKind.Deny));
            Assert.AreEqual(
                ClaimResolutionKind.Deny,
                _proof.State.Shift5FinalDisposition);
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

        private void HandleApplied(EliasAftermathAppliedEvent e)
        {
            _eventCount++;
            _lastEvent = e.Result;
        }
    }
}
