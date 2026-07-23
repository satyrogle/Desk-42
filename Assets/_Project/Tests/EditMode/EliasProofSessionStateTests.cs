using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Desk42.Core;

namespace Desk42.Tests.EditMode
{
    public sealed class EliasProofSessionStateTests
    {
        private GameObject _host;
        private EliasProofSessionController _controller;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("EliasProofSessionTests");
            _controller = _host.AddComponent<EliasProofSessionController>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void BeginProofSession_CreatesCanonicalCleanState()
        {
            EliasProofSessionState state =
                _controller.BeginProofSession("proof-session-test");

            Assert.IsTrue(state.IsActive);
            Assert.AreEqual("proof-session-test", state.ProofSessionId);
            Assert.AreEqual(EliasShift1Disposition.None,
                state.Shift1Disposition);
            Assert.AreEqual(EliasShift2Branch.None, state.Shift2Branch);
            Assert.AreEqual(ClaimResolutionKind.Unspecified,
                state.Shift2FinalDisposition);
            Assert.AreEqual(ClaimResolutionKind.Unspecified,
                state.Shift5FinalDisposition);
            Assert.AreEqual(EliasProcedureActionId.Unspecified,
                state.Shift2ProcedureAction);
            Assert.IsNull(state.Shift2ProcedureReceiptId);
            Assert.AreEqual(EliasProcedureActionId.Unspecified,
                state.Shift5ProcedureAction);
            Assert.IsNull(state.Shift5ProcedureReceiptId);
            Assert.IsEmpty(state.RecordedAppearanceKeys);
            Assert.IsEmpty(state.AppliedProcedureAppearanceKeys);
            Assert.IsNotNull(state.ActiveAftermathModifier);
            Assert.IsFalse(state.ActiveAftermathModifier.IsActive);
        }

        [Test]
        public void BeginAndEnd_AreExplicitResetBoundaries()
        {
            EliasProofSessionState first =
                _controller.BeginProofSession("first");
            EliasProofSessionState second =
                _controller.BeginProofSession("second");

            Assert.AreNotSame(first, second);
            Assert.AreEqual("second", second.ProofSessionId);
            Assert.IsTrue(_controller.HasActiveSession);

            _controller.EndProofSession();

            Assert.IsFalse(_controller.HasActiveSession);
            Assert.IsFalse(_controller.State.IsActive);
            Assert.IsNull(_controller.State.ProofSessionId);
            Assert.IsEmpty(_controller.State.RecordedAppearanceKeys);
            Assert.IsEmpty(
                _controller.State.AppliedProcedureAppearanceKeys);
        }

        [Test]
        public void State_IsSerializableButNotOwnedByRunOrMetaSchemas()
        {
            Assert.IsTrue(Attribute.IsDefined(
                typeof(EliasProofSessionState), typeof(SerializableAttribute)));

            AssertSchemaDoesNotOwnProofState(typeof(RunData));
            AssertSchemaDoesNotOwnProofState(typeof(MetaProgressData));
        }

        [Test]
        public void Appearances_RecordExactOnceWithPriorAndCurrentVisits()
        {
            _controller.BeginProofSession("visit-sequence");

            EliasVisitTransaction shift1 = _controller.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey);
            EliasVisitTransaction shift1Replay = _controller.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift1AppearanceKey);
            EliasVisitTransaction shift2 = _controller.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift2AppearanceKey);
            EliasVisitTransaction shift5 = _controller.RecordAppearance(
                EliasProofContent.CanonicalClaimantId,
                EliasProofContent.Shift5AppearanceKey);

            AssertVisit(shift1, 0, 1, wasNew: true);
            AssertVisit(shift1Replay, 0, 1, wasNew: false);
            AssertVisit(shift2, 1, 2, wasNew: true);
            AssertVisit(shift5, 2, 3, wasNew: true);
            Assert.AreEqual(3,
                _controller.State.RecordedAppearanceKeys.Count);
        }

        [Test]
        public void Appearance_RejectsUnstableIdentityAndOutOfOrderVisit()
        {
            _controller.BeginProofSession("visit-guards");

            Assert.Throws<InvalidOperationException>(() =>
                _controller.RecordAppearance(
                    "Elias-4821",
                    EliasProofContent.Shift1AppearanceKey));
            Assert.Throws<InvalidOperationException>(() =>
                _controller.RecordAppearance(
                    EliasProofContent.CanonicalClaimantId,
                    EliasProofContent.Shift2AppearanceKey));
            Assert.IsEmpty(_controller.State.RecordedAppearanceKeys);
        }

        [Test]
        public void ProofContent_OwnsCanonicalIdentityAndSchedule()
        {
            var content =
                ScriptableObject.CreateInstance<EliasProofContent>();
            try
            {
                Assert.AreEqual("elias_venn", content.StableClaimantId);
                Assert.AreEqual("Elias Venn", content.DisplayName);
                Assert.AreEqual("moth_accountant",
                    content.VisualProfileId);
                Assert.AreEqual(3, content.Appearances.Length);
                Assert.IsTrue(content.TryGetAppearance(
                    EliasProofContent.Shift5AppearanceKey,
                    out EliasAuthoredAppearance shift5));
                Assert.AreEqual(5, shift5.ShiftNumber);
                Assert.AreEqual(3, shift5.QueuePosition);
                Assert.AreEqual(3, shift5.AuthoredClaimIds.Length);
                Assert.AreEqual(5,
                    content.AuthoredFollowUpClaimIds.Length);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(content);
            }
        }

        private static void AssertVisit(EliasVisitTransaction transaction,
            int priorVisits, int currentVisit, bool wasNew)
        {
            Assert.AreEqual(priorVisits, transaction.PriorVisits);
            Assert.AreEqual(currentVisit, transaction.CurrentVisitNumber);
            Assert.AreEqual(wasNew, transaction.WasNewlyRecorded);
        }

        private static void AssertSchemaDoesNotOwnProofState(Type schema)
        {
            const BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Public | BindingFlags.NonPublic;
            bool ownsProofState = schema.GetFields(flags)
                    .Any(field => field.FieldType
                        == typeof(EliasProofSessionState))
                || schema.GetProperties(flags)
                    .Any(property => property.PropertyType
                        == typeof(EliasProofSessionState));
            Assert.IsFalse(ownsProofState,
                $"{schema.Name} must not own Elias proof-session state.");
        }
    }
}
